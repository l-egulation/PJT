using System;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Presentation.Tests
{
    public class SequenceCueCatalogTests
    {
        const int Frequency = 48000;
        static AudioCueDefinition Audio(string clipId = "fixture.original") => new AudioCueDefinition("fixture.catalog.audio", clipId, Frequency, Frequency);
        static PresentationCueDefinition Sprite() => new PresentationCueDefinition("fixture.catalog.sprite", new[] { "fixture.frame" }, new[] { 1f }, new[] { 0f }, new[] { 0f });
        sealed class Clock : ICueClock { public float Time; public float ReadElapsed() => Time; }

        [Test] public void PublishingIsAtomicAndCapturedDefinitionsAndMediaSurviveLaterCatalogReplacement()
        {
            var original = AudioClip.Create("CatalogOriginal", Frequency, 1, Frequency, false);
            var replacement = AudioClip.Create("CatalogReplacement", Frequency, 1, Frequency, false);
            var texture = new Texture2D(2, 1); var first = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32);
            var second = UnityEngine.Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32);
            try
            {
                using (var audio = new AudioCueFactory()) using (var sprites = new SpriteCueFactory())
                {
                    var catalog = new SequenceCueCatalog(sprites, audio); var definition = Audio(); var sprite = Sprite();
                    var frames = new[] { first }; var media = SequenceCueMedia.Sprite(sprite, frames); frames[0] = second;
                    catalog.Publish(SequenceCueMedia.Audio(definition, original), media);
                    var selected = catalog.Capture(new[] { definition.Id, sprite.Id, definition.Id }); var version = selected.Version;
                    Assert.That(selected.Read(definition.Id), Is.SameAs(definition)); Assert.That(selected.Read(sprite.Id), Is.SameAs(sprite));
                    Assert.That(audio.LiveOutputCount, Is.Zero); Assert.That(sprites.LiveViewCount, Is.Zero);
                    Assert.Throws<ArgumentException>(() => catalog.Publish(SequenceCueMedia.Audio(definition, replacement),
                        SequenceCueMedia.Audio(new AudioCueDefinition("fixture.bad", "fixture.bad", 24000, Frequency), original)));
                    Assert.That(catalog.Version, Is.EqualTo(version)); Assert.That(catalog.Capture(new[] { definition.Id }).Read(definition.Id), Is.SameAs(definition));
                    catalog.Publish(SequenceCueMedia.Audio(definition, replacement));
                    var sameDefinition = catalog.Capture(new[] { definition.Id }); Assert.That(sameDefinition.Version, Is.Not.EqualTo(version));
                    Assert.That(sameDefinition.Read(definition.Id), Is.SameAs(definition));
                    var changed = Audio("fixture.changed"); catalog.Publish(SequenceCueMedia.Audio(changed, replacement));
                    var next = catalog.Capture(new[] { changed.Id }); Assert.That(next.Version, Is.Not.EqualTo(version)); Assert.That(next.Read(changed.Id), Is.SameAs(changed));
                    Assert.That(selected.Read(definition.Id), Is.SameAs(definition)); Assert.Throws<InvalidOperationException>(() => next.Read(sprite.Id));
                    AudioCueOutputBinding oldOutput = null, newOutput = null; ICueView spriteOutput = null;
                    selected.Begin(definition, new CueAnchor(2, 3, 1), result => oldOutput = (AudioCueOutputBinding)result.View);
                    sameDefinition.Begin(definition, new CueAnchor(2, 3, 1), result => newOutput = (AudioCueOutputBinding)result.View);
                    selected.Begin(sprite, new CueAnchor(2, 3, 1), result => spriteOutput = result.View);
                    try
                    {
                        Assert.That(oldOutput.Source.clip, Is.SameAs(original)); Assert.That(newOutput.Source.clip, Is.SameAs(replacement));
                        Assert.That(oldOutput.gameObject.activeSelf || newOutput.gameObject.activeSelf, Is.False);
                        spriteOutput.Sample(0); Assert.That(GameObject.Find(sprite.Id).GetComponent<SpriteRenderer>().sprite, Is.SameAs(first));
                    }
                    finally { spriteOutput?.Dispose(); oldOutput?.Dispose(); newOutput?.Dispose(); }
                    catalog.Publish(); Assert.Throws<InvalidOperationException>(() => catalog.Capture(new[] { definition.Id }));
                    Assert.That(selected.Read(definition.Id), Is.SameAs(definition));
                }
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(texture); Object.DestroyImmediate(original); Object.DestroyImmediate(replacement); }
        }

        [Test] public void MissingDestroyedOrMismatchedRequiredMediaCannotBeCapturedAndFailedPublishPreservesTheVersion()
        {
            var clip = AudioClip.Create("CatalogRequired", Frequency, 1, Frequency, false);
            try
            {
                using (var factory = new AudioCueFactory())
                {
                    var catalog = new SequenceCueCatalog(audio: factory); var definition = Audio(); catalog.Publish(SequenceCueMedia.Audio(definition, clip)); var version = catalog.Version;
                    Assert.Throws<InvalidOperationException>(() => catalog.Capture(new[] { "missing" })); Assert.That(catalog.Version, Is.EqualTo(version));
                    Assert.Throws<ArgumentException>(() => catalog.Publish(SequenceCueMedia.Audio(new AudioCueDefinition("fixture.wrong", "fixture.pcm", 24000, Frequency), clip)));
                    Assert.That(catalog.Version, Is.EqualTo(version)); Assert.That(catalog.Capture(new[] { definition.Id }).Read(definition.Id), Is.SameAs(definition));
                    Object.DestroyImmediate(clip); Assert.Throws<InvalidOperationException>(() => catalog.Capture(new[] { definition.Id }));
                    Assert.That(factory.LiveOutputCount, Is.Zero);
                }
            }
            finally { if (clip) Object.DestroyImmediate(clip); }
        }

        [Test] public void SameCuePlayerScopesUseTheirCapturedFactoriesAndOneOwnerCannotReturnAnother()
        {
            var clip = AudioClip.Create("CatalogOwned", Frequency, 1, Frequency, false);
            try
            {
                using (var factory = new AudioCueFactory()) using (var player = new CuePlayer(factory))
                {
                    var catalog = new SequenceCueCatalog(audio: factory); var definition = Audio(); catalog.Publish(SequenceCueMedia.Audio(definition, clip));
                    var captured = catalog.Capture(new[] { definition.Id });
                    using (var first = player.OpenScope("first sequence", captured)) using (var second = player.OpenScope("second sequence", captured))
                    {
                        var clock = new Clock(); var a = first.Play(definition, new CueOccurrence(Guid.NewGuid(), definition.Id), new CueAnchor(0, 0, 1), clock);
                        var b = second.Play(definition, new CueOccurrence(Guid.NewGuid(), definition.Id), new CueAnchor(0, 0, 1), clock);
                        Assert.That(a.SessionId, Is.EqualTo(b.SessionId)); Assert.That(player.ActiveCount, Is.EqualTo(2)); Assert.That(factory.LiveOutputCount, Is.EqualTo(2));
                        first.Dispose(); Assert.That(a.Status, Is.EqualTo(CueStatus.Cancelled)); Assert.That(b.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(factory.LiveOutputCount, Is.EqualTo(1));
                        clock.Time = 1; b.Poll(); Assert.That(b.Status, Is.EqualTo(CueStatus.Completed)); Assert.That(player.ActiveCount, Is.Zero); Assert.That(factory.LiveOutputCount, Is.Zero);
                    }
                }
            }
            finally { Object.DestroyImmediate(clip); }
        }
    }
}
