using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Presentation.Tests
{
    public class AudioCueOutputTests
    {
        const int Frequency = 48000;
        sealed class Clock : ICueClock { public float Time; public float ReadElapsed() => Time; }
        sealed class Delayed : ICueViewFactory
        {
            readonly AudioCueFactory factory;
            readonly List<Request> requests = new List<Request>();
            public Delayed(AudioCueFactory factory) { this.factory = factory; }
            public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
            {
                var request = new Request { Definition = definition, Anchor = anchor, Resolve = resolved }; requests.Add(request); return request;
            }
            public Request At(int index) => requests[index];
            public AudioCueOutputBinding Deliver(int index)
            {
                AudioCueOutputBinding output = null; var request = requests[index];
                // Deliver even after cancellation to exercise a stale external
                // loader, not a cooperative fixture which silently drops it.
                factory.Begin(request.Definition, request.Anchor, result => { output = (AudioCueOutputBinding)result.View; request.Resolve(result); });
                return output;
            }
            public sealed class Request : IDisposable
            {
                public ICueDefinition Definition;
                public CueAnchor Anchor;
                public Action<CueViewResult> Resolve;
                public int Returns;
                public void Dispose() { Returns++; }
            }
        }
        static AudioClip Clip(string id) => AudioClip.Create(id, Frequency, 1, Frequency, false);
        static AudioCueDefinition Definition(string id, bool loop = false) => new AudioCueDefinition(id, "fixture.generated.pcm", Frequency, Frequency, loop);
        static ICueHandle Play(ICueScope owner, AudioCueDefinition definition, Clock clock) => owner.Play(definition, new CueOccurrence(Guid.NewGuid(), definition.Id), new CueAnchor(3, 4, -1), clock);
        static AudioCueOutputBinding Output(AudioCueDefinition definition) => GameObject.Find(definition.Id).GetComponent<AudioCueOutputBinding>();
        static void Returned(AudioCueOutputBinding output, AudioSource source)
        {
            Assert.That(output.IsDisposed, Is.True); Assert.That(source.isPlaying, Is.False);
            Assert.That(source.enabled, Is.False); Assert.That(source.clip, Is.Null); Assert.That(source.gameObject.activeSelf, Is.False);
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator ActualAudioSourceSamplesHeldBackwardSlowAndCompletedClock()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True);
            var listener = new GameObject("AudioFixtureListener", typeof(AudioListener)); var clip = Clip("AudioFixtureClockClip");
            try
            {
                using (var factory = new AudioCueFactory()) using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("clock fixture"))
                {
                    var definition = Definition("AudioFixtureClock"); factory.Publish(definition, clip); var clock = new Clock();
                    var handle = Play(owner, definition, clock); var output = Output(definition); var source = output.Source;
                    Assert.That(source.enabled, Is.True); Assert.That(source.clip, Is.SameAs(clip)); Assert.That(source.playOnAwake, Is.False);
                    Assert.That(source.pitch, Is.EqualTo(1)); Assert.That(source.loop, Is.False); Assert.That(source.isPlaying, Is.False);
                    Assert.That(source.timeSamples, Is.Zero); Assert.That(output.transform.position, Is.EqualTo(new Vector3(3, 4, 0)));
                    Assert.That(output.GetComponent<Collider2D>(), Is.Null); Assert.That(output.GetComponent<Rigidbody2D>(), Is.Null);
                    Assert.That(handle.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(factory.LiveOutputCount, Is.EqualTo(1));
                    clock.Time = .25f; handle.Poll(); Assert.That(source.isPlaying, Is.True);
                    Assert.That(source.timeSamples, Is.EqualTo(12000).Within(512), "Native DSP can interpolate between the seek and observation.");
                    handle.Poll(); Assert.That(source.isPlaying, Is.False); Assert.That(source.timeSamples, Is.EqualTo(12000));
                    yield return new WaitForSecondsRealtime(.05f);
                    Assert.That(source.timeSamples, Is.EqualTo(12000)); Assert.That(handle.Status, Is.EqualTo(CueStatus.Playing));
                    // Real time advances faster than the injected clock. Seeking
                    // uses injected time, not DSP time or Time.deltaTime.
                    clock.Time = .28125f; handle.Poll(); Assert.That(source.isPlaying, Is.True);
                    Assert.That(source.timeSamples, Is.EqualTo(13500).Within(512)); handle.Poll();
                    Assert.That(source.isPlaying, Is.False); Assert.That(source.timeSamples, Is.EqualTo(13500));
                    clock.Time = .125f; handle.Poll(); Assert.That(source.isPlaying, Is.False); Assert.That(source.timeSamples, Is.EqualTo(6000));
                    clock.Time = -.25f; handle.Poll(); Assert.That(source.timeSamples, Is.Zero); Assert.That(source.isPlaying, Is.False);
                    clock.Time = .5f; handle.Poll(); Assert.That(source.isPlaying, Is.True); handle.Poll();
                    yield return new WaitForSecondsRealtime(1.05f);
                    Assert.That(source.timeSamples, Is.EqualTo(24000)); Assert.That(source.isPlaying, Is.False);
                    Assert.That(handle.Status, Is.EqualTo(CueStatus.Playing), "DSP/end-of-clip is not a completion clock.");
                    clock.Time = 1; handle.Poll(); Assert.That(handle.Status, Is.EqualTo(CueStatus.Completed)); Returned(output, source);
                    Assert.That(player.ActiveCount, Is.Zero); Assert.That(factory.LiveOutputCount, Is.Zero);
                    handle.Cancel(); handle.Poll(); Assert.That(handle.Status, Is.EqualTo(CueStatus.Completed));
                    yield return null; Assert.That(source == null, Is.True); Assert.That(output == null, Is.True);
                }
            }
            finally { if (clip) Object.Destroy(clip); if (listener) Object.Destroy(listener); }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator AudioAndSpriteShareOnePlayerAndReturnOnlyTheirOwnerWithCapturedBindings()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True);
            var listener = new GameObject("AudioSpriteFixtureListener", typeof(AudioListener)); var clip = Clip("AudioFixtureOriginal"); var replacement = Clip("AudioFixtureReplacement");
            var texture = new Texture2D(2, 1); var first = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32); var second = Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32);
            try
            {
                using (var audio = new AudioCueFactory()) using (var sprites = new SpriteCueFactory()) using (var player = new CuePlayer(new CueMediaFactory(sprites, audio)))
                using (var audioOwner = player.OpenScope("audio owner")) using (var spriteOwner = player.OpenScope("sprite owner")) using (var otherOwner = player.OpenScope("other audio owner"))
                {
                    var definition = Definition("AudioSpriteFixtureAudio", true); audio.Publish(definition, clip); var clock = new Clock { Time = 1.25f };
                    var handle = Play(audioOwner, definition, clock); var output = Output(definition); var source = output.Source;
                    // Begin from a late loop position, then cross a loop boundary
                    // using the supplied elapsed time rather than source time.
                    Assert.That(source.timeSamples, Is.EqualTo(12000));
                    clock.Time = 2.25f; handle.Poll(); handle.Poll(); Assert.That(source.timeSamples, Is.EqualTo(12000));
                    var sprite = new PresentationCueDefinition("AudioSpriteFixtureSprite", new[] { "a", "b" }, new[] { .2f, .1f }, new[] { 1f, 2f }, new[] { 3f, 4f }, true, true);
                    sprites.Publish(sprite, new[] { first, second }); var spriteClock = new Clock { Time = .2f };
                    var spriteHandle = spriteOwner.Play(sprite, new CueOccurrence(Guid.NewGuid(), sprite.Id), new CueAnchor(10, 20, -1), spriteClock);
                    var renderer = GameObject.Find(sprite.Id).GetComponent<SpriteRenderer>();
                    Assert.That(handle.SessionId, Is.EqualTo(spriteHandle.SessionId)); Assert.That(player.ActiveCount, Is.EqualTo(2));
                    Assert.That(source.loop, Is.True); Assert.That(source.timeSamples, Is.EqualTo(12000));
                    Assert.That(renderer.sprite, Is.SameAs(second)); Assert.That(renderer.flipX, Is.True);
                    Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(8, 24, 0))); Assert.That(renderer.transform.localScale.x, Is.EqualTo(-1));
                    audio.Publish(definition, replacement);
                    var other = Play(otherOwner, definition, new Clock()); var otherOutput = Output(definition); var otherSource = otherOutput.Source;
                    // Find is not an identity source when cue IDs repeat; use
                    // clip identity to identify the newly created output.
                    foreach (var candidate in Object.FindObjectsByType<AudioCueOutputBinding>(FindObjectsSortMode.None))
                        if (candidate.Source.clip == replacement) { otherOutput = candidate; otherSource = candidate.Source; }
                    Assert.That(otherSource.clip, Is.SameAs(replacement)); Assert.That(source.clip, Is.SameAs(clip));
                    clock.Time = 1.5f; handle.Poll(); handle.Poll(); Assert.That(source.timeSamples, Is.EqualTo(24000));
                    spriteClock.Time = .31f; spriteHandle.Poll(); Assert.That(renderer.sprite, Is.SameAs(first));
                    audioOwner.Dispose(); Returned(output, source); Assert.That(handle.Status, Is.EqualTo(CueStatus.Cancelled));
                    Assert.That(spriteHandle.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(other.Status, Is.EqualTo(CueStatus.Playing));
                    Assert.That(audio.LiveOutputCount, Is.EqualTo(1)); Assert.That(sprites.LiveViewCount, Is.EqualTo(1));
                    other.Cancel(); other.Cancel(); Returned(otherOutput, otherSource); Assert.That(other.Status, Is.EqualTo(CueStatus.Cancelled));
                    player.Reset(); Assert.That(spriteHandle.Status, Is.EqualTo(CueStatus.Cancelled)); Assert.That(renderer.gameObject.activeSelf, Is.False);
                    Assert.That(player.ActiveCount, Is.Zero); Assert.That(audio.LiveOutputCount, Is.Zero); Assert.That(sprites.LiveViewCount, Is.Zero);
                }
            }
            finally
            {
                if (first) Object.Destroy(first); if (second) Object.Destroy(second); if (texture) Object.Destroy(texture);
                if (clip) Object.Destroy(clip); if (replacement) Object.Destroy(replacement); if (listener) Object.Destroy(listener);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator DisabledDestroyedAndUnavailableAudioReturnsTheSourceWithoutRevival()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True);
            var listener = new GameObject("AudioUnavailableFixtureListener", typeof(AudioListener)); var clip = Clip("AudioUnavailableFixtureClip");
            try
            {
                using (var factory = new AudioCueFactory()) using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("availability fixture"))
                {
                    foreach (string fault in new[] { "binding", "source", "inactive", "clip", "destroy-source", "destroy-output", "factory" })
                    {
                        var definition = Definition("AudioUnavailableFixture-" + fault, true); factory.Publish(definition, clip); var clock = new Clock();
                        var handle = Play(owner, definition, clock); var output = Output(definition); var source = output.Source;
                        clock.Time = .25f; handle.Poll(); Assert.That(source.isPlaying, Is.True);
                        switch (fault)
                        {
                            case "binding": output.enabled = false; break;
                            case "source": source.enabled = false; break;
                            case "inactive": output.gameObject.SetActive(false); break;
                            case "clip": source.clip = null; break;
                            case "destroy-source": Object.DestroyImmediate(source); break;
                            case "destroy-output": Object.DestroyImmediate(output); break;
                            case "factory": factory.Dispose(); break;
                        }
                        handle.Poll(); Assert.That(handle.Status, Is.EqualTo(CueStatus.Failed), fault); Assert.That(handle.Error, Is.Not.Empty);
                        Assert.That(factory.LiveOutputCount, Is.Zero); Assert.That(player.ActiveCount, Is.Zero);
                        if (source)
                        {
                            Assert.That(source.isPlaying, Is.False); Assert.That(source.enabled, Is.False); Assert.That(source.clip, Is.Null);
                            // Re-enabling a returned native source cannot restore
                            // a handle, media reference or original output owner.
                            source.enabled = true; source.gameObject.SetActive(true); handle.Poll();
                            Assert.That(source.isPlaying, Is.False); Assert.That(handle.Status, Is.EqualTo(CueStatus.Failed));
                        }
                        yield return null;
                        Assert.That(output == null, Is.True); Assert.That(source == null, Is.True);
                    }
                }
            }
            finally { if (clip) Object.Destroy(clip); if (listener) Object.Destroy(listener); }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator LateDuplicateAndResetAudioResultsNeverStartAnOldSource()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True);
            var listener = new GameObject("AudioLateFixtureListener", typeof(AudioListener)); var clip = Clip("AudioLateFixtureClip");
            try
            {
                using (var factory = new AudioCueFactory())
                {
                    var delayed = new Delayed(factory);
                    using (var player = new CuePlayer(delayed))
                    {
                        var oldOwner = player.OpenScope("old loader"); var definition = Definition("AudioLateFixtureOld", true); factory.Publish(definition, clip);
                        var old = Play(oldOwner, definition, new Clock()); player.Reset(); Assert.That(old.Status, Is.EqualTo(CueStatus.Cancelled));
                        var stale = delayed.Deliver(0); Assert.That(stale.IsDisposed, Is.True); Assert.That(stale.Source.isPlaying, Is.False);
                        Assert.That(delayed.At(0).Returns, Is.EqualTo(1)); Assert.That(factory.LiveOutputCount, Is.Zero);
                        using (var fresh = player.OpenScope("fresh loader"))
                        {
                            var activeDefinition = Definition("AudioLateFixtureActive"); factory.Publish(activeDefinition, clip); var clock = new Clock { Time = .25f };
                            var active = Play(fresh, activeDefinition, clock); var accepted = delayed.Deliver(1); var source = accepted.Source;
                            Assert.That(accepted.gameObject.activeSelf, Is.False); Assert.That(source.isPlaying, Is.False);
                            delayed.At(1).Resolve(CueViewResult.Ready(accepted)); Assert.That(accepted.IsDisposed, Is.False);
                            active.Poll(); Assert.That(source.timeSamples, Is.EqualTo(12000)); Assert.That(source.isPlaying, Is.False);
                            clock.Time = .5f; active.Poll(); active.Poll(); Assert.That(source.timeSamples, Is.EqualTo(24000));
                            var duplicate = delayed.Deliver(1); Assert.That(duplicate.IsDisposed, Is.True); Assert.That(duplicate.Source.isPlaying, Is.False);
                            Assert.That(accepted.IsDisposed, Is.False); Assert.That(active.Status, Is.EqualTo(CueStatus.Playing));
                            active.Cancel(); Returned(accepted, source); delayed.At(1).Resolve(CueViewResult.Ready(accepted));
                            Assert.That(delayed.At(1).Returns, Is.EqualTo(1)); Assert.That(active.Status, Is.EqualTo(CueStatus.Cancelled));
                            var expiredDefinition = Definition("AudioLateFixtureExpired"); factory.Publish(expiredDefinition, clip); var end = new Clock();
                            var expired = Play(fresh, expiredDefinition, end); end.Time = 1; var tooLate = delayed.Deliver(2); expired.Poll();
                            Assert.That(expired.Status, Is.EqualTo(CueStatus.Failed)); Returned(tooLate, tooLate.Source);
                            Assert.That(factory.LiveOutputCount, Is.Zero); Assert.That(player.ActiveCount, Is.Zero);
                        }
                        oldOwner.Dispose();
                    }
                }
            }
            finally { if (clip) Object.Destroy(clip); if (listener) Object.Destroy(listener); }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator MissingOrDestroyedClipAndMismatchedVersionCannotProduceAnAudioSource()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True);
            CheckMissingOrDestroyedClip();
            yield return new ExitPlayMode();
        }
        // Allocate the Assert.Throws closure after EnterPlayMode's domain reload.
        static void CheckMissingOrDestroyedClip()
        {
            var clip = Clip("AudioMissingFixtureClip");
            try
            {
                using (var factory = new AudioCueFactory()) using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("missing fixture"))
                {
                    var missing = Definition("AudioMissingFixture"); var handle = Play(owner, missing, new Clock());
                    Assert.That(handle.Status, Is.EqualTo(CueStatus.Failed)); Assert.That(factory.LiveOutputCount, Is.Zero);
                    Assert.Throws<ArgumentException>(() => factory.Publish(new AudioCueDefinition("wrong", "fixture", 24000, Frequency), clip));
                    factory.Publish(missing, clip); var changed = new AudioCueDefinition(missing.Id, "different.clip.id", Frequency, Frequency);
                    Assert.That(Play(owner, changed, new Clock()).Status, Is.EqualTo(CueStatus.Failed));
                    var destroyed = Definition("AudioDestroyedClipFixture"); factory.Publish(destroyed, clip); Object.DestroyImmediate(clip);
                    Assert.That(Play(owner, destroyed, new Clock()).Status, Is.EqualTo(CueStatus.Failed));
                    Assert.That(factory.LiveOutputCount, Is.Zero); Assert.That(player.ActiveCount, Is.Zero);
                }
            }
            finally { if (clip) Object.Destroy(clip); }
        }
    }
}
