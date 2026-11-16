using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative.Contracts;
using Deviant.Narrative.Tests;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using Deviant.UI.Unity;
using Deviant.World;
using Deviant.World.Contracts;
using Deviant.World.Tests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SequenceAudioIntegrationTests
    {
        const int Frequency = 48000;
        const string Game = "Assets/Deviant/Game/Scenes/deviant.unity";
        const string Lab = "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity";
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class CueClock : ICueClock { public float Time; public float ReadElapsed() => Time; }
        sealed class Endpoint : ISequenceSkipEndpoint
        {
            public int Calls;
            public bool Validate(string endpointId, out string error) { error = null; return endpointId == "fixture.audio.end"; }
            public ISequenceOperation Begin(Guid executionId, string endpointId) { Calls++; return new Completed(); }
            sealed class Completed : ISequenceOperation
            {
                public SequenceOperationStatus Status => SequenceOperationStatus.Completed;
                public string Error => null;
                public void Poll(double elapsed) { }
                public void Dispose() { }
            }
        }
        sealed class DelayedCatalog : ICueCatalog
        {
            readonly ICueCatalog catalog;
            public bool DelayAudio;
            public readonly List<Request> Requests = new List<Request>();
            public DelayedCatalog(ICueCatalog catalog) { this.catalog = catalog; }
            public ICueCatalogSnapshot Capture(IReadOnlyList<string> cueIds) => new Snapshot(this, catalog.Capture(cueIds));
            public sealed class Request : IDisposable
            {
                public ICueCatalogSnapshot Catalog;
                public ICueDefinition Definition;
                public CueAnchor Anchor;
                public Action<CueViewResult> Resolve;
                public int Returns;
                public void Dispose() { Returns++; }
                public AudioCueOutputBinding Deliver()
                {
                    AudioCueOutputBinding output = null;
                    Catalog.Begin(Definition, Anchor, result => { output = (AudioCueOutputBinding)result.View; Resolve(result); });
                    return output;
                }
            }
            sealed class Snapshot : ICueCatalogSnapshot
            {
                readonly DelayedCatalog owner;
                readonly ICueCatalogSnapshot captured;
                public Guid Version => captured.Version;
                public Snapshot(DelayedCatalog owner, ICueCatalogSnapshot captured) { this.owner = owner; this.captured = captured; }
                public ICueDefinition Read(string cueId) => captured.Read(cueId);
                public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
                {
                    if (!owner.DelayAudio || !(definition is AudioCueDefinition)) return captured.Begin(definition, anchor, resolved);
                    owner.DelayAudio = false;
                    var request = new Request { Catalog = captured, Definition = definition, Anchor = anchor, Resolve = resolved };
                    owner.Requests.Add(request); return request;
                }
            }
        }
        sealed class Fixture : IDisposable
        {
            public readonly CombatArena Arena;
            readonly GameObject sources, root, owner, canvas, eventOwner;
            readonly DialogueView dialogueView;
            readonly GameObject module, marker;
            readonly Texture2D texture;
            public readonly Sprite First, Second;
            public readonly AudioClip Original, Replacement;
            public readonly SpriteCueFactory Sprites = new SpriteCueFactory();
            public readonly AudioCueFactory Audio = new AudioCueFactory();
            public readonly SequenceCueCatalog Catalog;
            public readonly DelayedCatalog Media;
            public readonly CuePlayer Cues;
            public readonly Clock Clock = new Clock();
            public readonly Endpoint End = new Endpoint();
            public readonly ChapterWorldHost World;
            public readonly SequenceHost Host;
            public readonly SequenceControlPolicy Policy = new SequenceControlPolicy(InputChannels.Combat | InputChannels.Interaction, true);
            public AudioCueDefinition AudioDefinition(bool changed = false, bool loop = false) => new AudioCueDefinition("SequenceAudioFixture", changed ? "fixture.changed.pcm" : "fixture.original.pcm", Frequency, Frequency, loop);
            public PresentationCueDefinition SpriteDefinition(bool changed = false) => new PresentationCueDefinition("SequenceAudioSpriteFixture", new[] { "fixture.a", "fixture.b" },
                new[] { .25f, .25f }, new[] { 0f, 0f }, new[] { 0f, 0f }, changed);
            public Fixture(CombatArena arena)
            {
                Arena = arena;
                sources = new GameObject("AudioSequenceSources"); sources.SetActive(false);
                module = new GameObject("AudioSequenceFloor"); module.transform.SetParent(sources.transform, false);
                var floor = module.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
                marker = new GameObject("AudioSequenceMarker"); marker.transform.SetParent(sources.transform, false);
                root = new GameObject("AudioSequenceChapter"); root.transform.position = new Vector3(0, 1000, 0); World = root.AddComponent<ChapterWorldHost>();
                owner = new GameObject("AudioSequenceOwner"); Host = owner.AddComponent<SequenceHost>();
                canvas = SequenceIntegrationTests.CreateView(out dialogueView, out _, out _);
                if (!Object.FindAnyObjectByType<EventSystem>()) eventOwner = new GameObject("AudioSequenceEvents", typeof(EventSystem));
                texture = new Texture2D(2, 1); First = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32); Second = Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32);
                // Silent generated PCM is a technical fixture, not product sound.
                Original = AudioClip.Create("SequenceAudioOriginal", Frequency, 1, Frequency, false);
                Replacement = AudioClip.Create("SequenceAudioReplacement", Frequency, 1, Frequency, false);
                Catalog = new SequenceCueCatalog(Sprites, Audio); Media = new DelayedCatalog(Catalog); Cues = new CuePlayer(new CueMediaFactory(Sprites, Audio));
            }
            public void Open(IChapterLayoutProvider provider)
            {
                var layout = ChapterFixtures.Good();
                Assert.That(World.TryOpen(provider, new LayoutRequest(layout.Id, layout.Version, 17), UnityChapterModules.ProfileFor(Arena.Player),
                    new Dictionary<string, GameObject> { { "floor-8", module } }, new Dictionary<string, GameObject> { { "marker", marker } }, out var error), Is.True, error);
            }
            public void Publish(bool changed = false) => Catalog.Publish(SequenceCueMedia.Audio(AudioDefinition(changed), changed ? Replacement : Original),
                SequenceCueMedia.Sprite(SpriteDefinition(changed), changed ? new[] { Second, First } : new[] { First, Second }));
            public ChapterSequenceBindings Bind(ICueCatalog catalog = null) => new ChapterSequenceBindings(World, World.Session.Id, new DialogueFixtures.Targets(), new DialogueFixtures.Conditions(),
                new InteractionPolicy(2, true, false, true), dialogueView, new DialogueFixtures.Text(), Arena.Controls,
                new DialoguePresentationPolicy(InputChannels.None, false), Cues, catalog ?? Media, Arena.CameraRequests, Array.Empty<SequenceDialogueBinding>(),
                new[] { new SequenceCueBinding("audio", "focus", AudioDefinition().Id, -1), new SequenceCueBinding("sprite", "focus", SpriteDefinition().Id, 1) }, Array.Empty<SequenceCameraBinding>(), End);
            public bool Start(SequenceDefinition definition, ISequenceBindings bindings = null) => Host.TryStart(definition, bindings ?? Bind(), Arena.Controls, Policy, Clock, out _);
            public void Poll(double elapsed) { Clock.Time = elapsed; Host.Refresh(); }
            public AudioCueOutputBinding Output() => Object.FindObjectsByType<AudioCueOutputBinding>(FindObjectsSortMode.None).Single(o => o.name == AudioDefinition().Id);
            public SpriteRenderer Renderer() => Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Single(r => r.name == SpriteDefinition().Id);
            public void DisableOwner() => owner.SetActive(false);
            public void RestoreOwner() => owner.SetActive(true);
            public ScreenView Modal(string name)
            {
                var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)); panel.transform.SetParent(canvas.transform, false);
                var button = new GameObject(name + "Button", typeof(RectTransform), typeof(Image), typeof(Button)); button.transform.SetParent(panel.transform, false);
                var view = panel.AddComponent<ScreenView>();
                view.Configure(panel, panel.GetComponent<CanvasGroup>(), Object.FindAnyObjectByType<EventSystem>(), button.GetComponent<Button>(), null, true, true);
                return view;
            }
            public void Dispose()
            {
                Host.Close(); World.Close(); Cues.Dispose(); Audio.Dispose(); Sprites.Dispose();
                Object.Destroy(owner); Object.Destroy(root); Object.Destroy(sources); Object.Destroy(canvas);
                if (eventOwner) Object.Destroy(eventOwner);
                Object.Destroy(First); Object.Destroy(Second); Object.Destroy(texture); Object.Destroy(Original); Object.Destroy(Replacement);
            }
        }
        static SequenceDefinition Definition(bool parallel, bool before = false, double timeout = 2)
        {
            var steps = new List<SequenceStep>();
            if (before) steps.Add(SequenceStep.Wait("before", .125));
            if (parallel) steps.Add(SequenceStep.ParallelAll("together", SequenceStep.Cue("audio", "audio", timeout), SequenceStep.Cue("sprite", "sprite", 2)));
            else { steps.Add(SequenceStep.Cue("audio", "audio", timeout)); steps.Add(SequenceStep.Cue("sprite", "sprite", 2)); }
            return new SequenceDefinition("sequence-audio-fixture", steps.ToArray(), new[] { "focus" }, new SequenceSkipDefinition("fixture.audio.end", 1));
        }
        static void Returned(AudioCueOutputBinding output, AudioSource source)
        {
            Assert.That(output.IsDisposed, Is.True); Assert.That(source.isPlaying, Is.False); Assert.That(source.enabled, Is.False);
            Assert.That(source.clip, Is.Null); Assert.That(source.gameObject.activeSelf, Is.False);
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameSequentialAudioAndSpriteCaptureCatalogAndSampleSequenceClock() => Timing(Game, false);
        [UnityTest] public IEnumerator LabSequentialAudioAndSpriteCaptureCatalogAndSampleSequenceClock() => Timing(Lab, false);
        [UnityTest] public IEnumerator GameParallelAudioAndSpriteCaptureCatalogAndSampleSequenceClock() => Timing(Game, true);
        [UnityTest] public IEnumerator LabParallelAudioAndSpriteCaptureCatalogAndSampleSequenceClock() => Timing(Lab, true);
        static IEnumerator Timing(string scene, bool parallel)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation()) using (var fixture = new Fixture(arena))
            {
                foreach (var provider in new IChapterLayoutProvider[] { new AuthoredLayoutProvider(ChapterFixtures.Good()), new ChapterFixtures.CapturedProvider() })
                {
                    driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                    fixture.Open(provider); fixture.Publish(); fixture.Clock.Time = 0;
                    float time = arena.Now, health = arena.Player.health;
                    var bindings = fixture.Bind(); var version = fixture.Catalog.Version;
                    Assert.That(fixture.Start(Definition(parallel, true), bindings), Is.True);
                    Assert.That(fixture.Audio.LiveOutputCount, Is.Zero); Assert.That(fixture.Sprites.LiveViewCount, Is.Zero);
                    fixture.Publish(true); Assert.That(fixture.Catalog.Version, Is.Not.EqualTo(version));
                    fixture.Poll(.125); var output = fixture.Output(); var source = output.Source;
                    Assert.That(source.clip, Is.SameAs(fixture.Original)); Assert.That(source.isPlaying, Is.False); Assert.That(source.timeSamples, Is.Zero);
                    Assert.That(source.spatialBlend, Is.Zero, "The existing injected audio definition remains non-spatial.");
                    Assert.That(fixture.World.TryGetWorldAnchor(fixture.World.Session.Id, "focus", out var point, out _), Is.True);
                    Assert.That(source.transform.position, Is.EqualTo(new Vector3(point.x, point.y, 0)));
                    Assert.That(output.GetComponent<Collider2D>(), Is.Null); Assert.That(output.GetComponent<Rigidbody2D>(), Is.Null);
                    if (parallel) Assert.That(fixture.Renderer().sprite, Is.SameAs(fixture.First));
                    fixture.Poll(.375); Assert.That(source.isPlaying, Is.True); Assert.That(source.timeSamples, Is.EqualTo(12000).Within(512));
                    fixture.Host.Refresh(); Assert.That(source.isPlaying, Is.False); Assert.That(source.timeSamples, Is.EqualTo(12000));
                    if (parallel) Assert.That(fixture.Renderer().sprite, Is.SameAs(fixture.Second));
                    using (var screens = new ScreenRouter(arena.Controls))
                    {
                        Assert.That(screens.TryOpen("audio outer modal", fixture.Modal("AudioOuterModal"), new ScreenPolicy(1, InputChannels.Navigation, true, false), out var outer, out var error), Is.True, error);
                        Assert.That(screens.TryOpen("audio inner modal", fixture.Modal("AudioInnerModal"), new ScreenPolicy(2, InputChannels.Dialogue, true, false), out var inner, out error), Is.True, error);
                        fixture.Host.Refresh(); yield return new WaitForSecondsRealtime(.05f);
                        Assert.That(source.isPlaying, Is.False); Assert.That(source.timeSamples, Is.EqualTo(12000)); Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Running));
                        inner.Dispose(); Assert.That(outer.Status, Is.EqualTo(ScreenStatus.Open)); Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                        fixture.Poll(.625); Assert.That(source.isPlaying, Is.True); fixture.Host.Refresh(); Assert.That(source.timeSamples, Is.EqualTo(24000));
                        if (parallel)
                        {
                            Assert.That(fixture.Sprites.LiveViewCount, Is.Zero);
                            Assert.That(fixture.Host.Current.Read().Children.Single(c => c.Id == "sprite").Status, Is.EqualTo(SequenceStepStatus.Completed));
                        }
                        fixture.Poll(1.125); Returned(output, source);
                        if (!parallel)
                        {
                            Assert.That(fixture.Renderer().sprite, Is.SameAs(fixture.First)); Assert.That(fixture.Renderer().flipX, Is.False);
                            fixture.Poll(1.375); Assert.That(fixture.Renderer().sprite, Is.SameAs(fixture.Second)); fixture.Poll(1.625);
                        }
                        Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed)); Assert.That(fixture.Cues.ActiveCount, Is.Zero);
                        Assert.That(outer.Status, Is.EqualTo(ScreenStatus.Open)); Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation));
                    }
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.False); Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.None));
                    Assert.That(arena.Now, Is.EqualTo(time)); Assert.That(arena.Player.health, Is.EqualTo(health));
                    // Reusing the adapter captures the newly published version only
                    // for a new execution, including its actual clip references.
                    Assert.That(fixture.Start(Definition(parallel), bindings), Is.True); var next = fixture.Output(); var nextSource = next.Source;
                    Assert.That(nextSource.clip, Is.SameAs(fixture.Replacement));
                    if (parallel) { Assert.That(fixture.Renderer().sprite, Is.SameAs(fixture.Second)); Assert.That(fixture.Renderer().flipX, Is.True); }
                    fixture.Host.Close(); Returned(next, nextSource); Assert.That(fixture.Audio.LiveOutputCount, Is.Zero); Assert.That(fixture.Sprites.LiveViewCount, Is.Zero);
                    fixture.World.Close(); yield return null;
                }
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator GameAudioSequenceFailureAndLateResultsReturnOnlyOwnedOutputs() => Lifetimes(Game);
        [UnityTest] public IEnumerator LabAudioSequenceFailureAndLateResultsReturnOnlyOwnedOutputs() => Lifetimes(Lab);
        static IEnumerator Lifetimes(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation()) using (var fixture = new Fixture(arena))
            {
                driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                fixture.Open(new AuthoredLayoutProvider(ChapterFixtures.Good())); fixture.Publish();
                var bindings = fixture.Bind(); var definition = Definition(true);
                using (var modal = arena.Controls.OpenScope("external audio modal")) using (var foreign = fixture.Cues.OpenScope("unrelated audio"))
                {
                    var pause = modal.PauseSimulation(); var block = modal.BlockInput(InputChannels.Navigation);
                    var foreignDefinition = new AudioCueDefinition("SequenceAudioForeignFixture", "fixture.foreign", Frequency, Frequency, true);
                    fixture.Audio.Publish(foreignDefinition, fixture.Original); var foreignClock = new CueClock();
                    var foreignHandle = foreign.Play(foreignDefinition, new CueOccurrence(Guid.NewGuid(), foreignDefinition.Id), new CueAnchor(0, 0, 1), foreignClock);
                    var foreignOutput = GameObject.Find(foreignDefinition.Id).GetComponent<AudioCueOutputBinding>();
                    foreach (var mode in new[] { "cancel", "skip", "timeout", "output", "source", "clip", "owner", "chapter" })
                    {
                        double start = fixture.Clock.Time; Assert.That(fixture.Start(mode == "timeout" ? Definition(true, timeout: .25) : definition, bindings), Is.True, mode);
                        var output = fixture.Output(); var source = output.Source;
                        fixture.Poll(start + .125); Assert.That(source.isPlaying, Is.True); fixture.Host.Refresh();
                        switch (mode)
                        {
                            case "cancel": Assert.That(fixture.Host.Current.TryCancel(fixture.Host.Current.Read().ExecutionId, out _), Is.True); break;
                            case "skip": Assert.That(fixture.Host.Current.TrySkip(fixture.Host.Current.Read().ExecutionId, out _), Is.True); break;
                            case "timeout": fixture.Poll(start + .25); break;
                            case "output": output.enabled = false; fixture.Host.Refresh(); break;
                            case "source": source.enabled = false; fixture.Host.Refresh(); break;
                            case "clip": source.clip = null; fixture.Host.Refresh(); break;
                            case "owner": fixture.DisableOwner(); break;
                            case "chapter": fixture.World.Close(); fixture.Host.Refresh(); break;
                        }
                        Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(mode == "cancel" || mode == "owner" ? SequenceStatus.Cancelled : mode == "skip" ? SequenceStatus.Skipped : SequenceStatus.Failed), mode);
                        if (mode == "timeout") Assert.That(fixture.Host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.TimedOut));
                        Returned(output, source); Assert.That(fixture.Sprites.LiveViewCount, Is.Zero); Assert.That(fixture.Audio.LiveOutputCount, Is.EqualTo(1));
                        Assert.That(foreignHandle.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(foreignOutput.IsDisposed, Is.False);
                        Assert.That(pause.IsActive && block.IsActive, Is.True); Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation));
                        fixture.RestoreOwner();
                        if (mode == "chapter") { fixture.Open(new ChapterFixtures.CapturedProvider()); bindings = fixture.Bind(); }
                        yield return null;
                    }
                    Assert.That(fixture.End.Calls, Is.EqualTo(1));
                    // Missing required future media is rejected before the leading
                    // wait can progress or acquire a control/view scope.
                    fixture.Catalog.Publish(SequenceCueMedia.Sprite(fixture.SpriteDefinition(), new[] { fixture.First, fixture.Second }));
                    long revision = arena.Controls.Current.Revision;
                    Assert.That(fixture.Start(Definition(true, true), bindings), Is.False); Assert.That(fixture.Host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed));
                    Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision)); Assert.That(fixture.Audio.LiveOutputCount, Is.EqualTo(1)); fixture.Publish();
                    var destroyed = AudioClip.Create("SequenceRequiredDestroyed", Frequency, 1, Frequency, false);
                    fixture.Catalog.Publish(SequenceCueMedia.Audio(fixture.AudioDefinition(), destroyed), SequenceCueMedia.Sprite(fixture.SpriteDefinition(), new[] { fixture.First, fixture.Second }));
                    Object.DestroyImmediate(destroyed); revision = arena.Controls.Current.Revision;
                    Assert.That(fixture.Start(Definition(true, true), bindings), Is.False); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision)); fixture.Publish();
                    var lostBeforeLeaf = AudioClip.Create("SequencePinnedMediaLost", Frequency, 1, Frequency, false);
                    fixture.Catalog.Publish(SequenceCueMedia.Audio(fixture.AudioDefinition(), lostBeforeLeaf), SequenceCueMedia.Sprite(fixture.SpriteDefinition(), new[] { fixture.First, fixture.Second }));
                    double waitingAt = fixture.Clock.Time; Assert.That(fixture.Start(Definition(true, true), bindings), Is.True);
                    Object.DestroyImmediate(lostBeforeLeaf); fixture.Publish(true); fixture.Poll(waitingAt + .125);
                    Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed));
                    Assert.That(fixture.Host.Current.Read().Children.Single(c => c.Id == "sprite").Status, Is.EqualTo(SequenceStepStatus.Pending));
                    Assert.That(fixture.Audio.LiveOutputCount, Is.EqualTo(1)); Assert.That(foreignHandle.Status, Is.EqualTo(CueStatus.Playing));
                    Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); fixture.Publish();
                    fixture.Catalog.Publish(SequenceCueMedia.Audio(fixture.AudioDefinition(loop: true), fixture.Original),
                        SequenceCueMedia.Sprite(fixture.SpriteDefinition(), new[] { fixture.First, fixture.Second }));
                    revision = arena.Controls.Current.Revision; Assert.That(fixture.Start(Definition(true, true), bindings), Is.False);
                    Assert.That(fixture.Host.Current.Read().Error, Does.Contain("finite one-shot")); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision)); fixture.Publish();
                    foreach (var mode in new[] { "cancel", "skip", "timeout", "failure", "late-end", "late-middle", "late-clip-loss" })
                    {
                        double start = fixture.Clock.Time; fixture.Media.DelayAudio = true;
                        Assert.That(fixture.Start(mode == "timeout" ? Definition(true, timeout: .25) : definition, bindings), Is.True);
                        var request = fixture.Media.Requests.Last(); Assert.That(request.Definition.ContentHash, Is.EqualTo(fixture.AudioDefinition().ContentHash));
                        fixture.Publish(true);
                        if (mode == "late-clip-loss")
                        {
                            var missing = AudioClip.Create("SequenceDelayedClipLost", Frequency, 1, Frequency, false);
                            // Capture a new delayed request whose original clip is
                            // then destroyed; the newer catalog cannot rescue it.
                            fixture.Host.Close(); fixture.Catalog.Publish(SequenceCueMedia.Audio(fixture.AudioDefinition(), missing),
                                SequenceCueMedia.Sprite(fixture.SpriteDefinition(), new[] { fixture.First, fixture.Second }));
                            fixture.Media.DelayAudio = true; Assert.That(fixture.Start(definition, bindings), Is.True); request = fixture.Media.Requests.Last();
                            Object.DestroyImmediate(missing); fixture.Publish(true);
                            try { request.Deliver(); } catch (InvalidOperationException exception) { request.Resolve(CueViewResult.Failure(exception.Message)); }
                            fixture.Host.Refresh(); Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(fixture.Sprites.LiveViewCount, Is.Zero);
                        }
                        else if (mode == "late-middle")
                        {
                            fixture.Clock.Time = start + .25; var accepted = request.Deliver(); Assert.That(accepted.gameObject.activeSelf, Is.False);
                            fixture.Host.Refresh(); Assert.That(accepted.Source.clip, Is.SameAs(fixture.Original)); Assert.That(accepted.Source.timeSamples, Is.EqualTo(12000));
                            Assert.That(fixture.Renderer().sprite, Is.SameAs(fixture.Second)); fixture.Host.Close(); Returned(accepted, accepted.Source);
                        }
                        else
                        {
                            if (mode == "cancel") fixture.Host.Current.TryCancel(fixture.Host.Current.Read().ExecutionId, out _);
                            else if (mode == "skip") fixture.Host.Current.TrySkip(fixture.Host.Current.Read().ExecutionId, out _);
                            else if (mode == "timeout") fixture.Poll(start + .25);
                            else if (mode == "failure") { request.Resolve(CueViewResult.Failure("fixture late audio factory failure")); fixture.Host.Refresh(); }
                            else { fixture.Clock.Time = start + 1; }
                            var terminal = fixture.Host.Current.Read().Status; var stale = request.Deliver();
                            if (mode == "late-end") { fixture.Host.Refresh(); Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); }
                            else Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(terminal));
                            Returned(stale, stale.Source); Assert.That(fixture.Sprites.LiveViewCount, Is.Zero);
                        }
                        Assert.That(request.Returns, Is.EqualTo(1)); Assert.That(fixture.Audio.LiveOutputCount, Is.EqualTo(1)); Assert.That(fixture.Cues.ActiveCount, Is.EqualTo(1));
                        Assert.That(foreignHandle.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(pause.IsActive && block.IsActive, Is.True);
                        fixture.Publish(); yield return null;
                    }
                }
                Assert.That(fixture.Cues.ActiveCount, Is.Zero); Assert.That(fixture.Audio.LiveOutputCount, Is.Zero);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.False); Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.None));
                // Reset before the first Cue invalidates the captured player
                // session. It cannot lazily open a scope in the new generation.
                double waiting = fixture.Clock.Time; Assert.That(fixture.Start(Definition(true, true), bindings), Is.True);
                fixture.Cues.Reset(); fixture.Poll(waiting + .125);
                Assert.That(fixture.Host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed));
                Assert.That(fixture.Audio.LiveOutputCount, Is.Zero); Assert.That(fixture.Sprites.LiveViewCount, Is.Zero);
                Assert.That(fixture.Start(definition, bindings), Is.True); fixture.Cues.Reset(); fixture.Host.Refresh();
                Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(fixture.Cues.ActiveCount, Is.Zero);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                // Closing the injected output factory fails the current handle;
                // a new sequence is rejected before obtaining control leases.
                Assert.That(fixture.Start(definition, bindings), Is.True); var finalOutput = fixture.Output(); var finalSource = finalOutput.Source;
                fixture.Audio.Dispose(); fixture.Host.Refresh(); Returned(finalOutput, finalSource);
                Assert.That(fixture.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(fixture.Sprites.LiveViewCount, Is.Zero);
                long closedRevision = arena.Controls.Current.Revision; Assert.That(fixture.Start(definition, bindings), Is.False);
                Assert.That(fixture.Host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(closedRevision));
            }
            yield return new ExitPlayMode();
        }
    }
}
