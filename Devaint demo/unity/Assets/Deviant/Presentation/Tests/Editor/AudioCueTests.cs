using System;
using System.Collections.Generic;
using System.Globalization;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class AudioCueTests
    {
        sealed class Clock : ICueClock
        {
            public float Time;
            public bool Throw;
            public float ReadElapsed() => Throw ? throw new InvalidOperationException("fixture clock failed") : Time;
        }
        sealed class Output : ICueView
        {
            public readonly List<float> Samples = new List<float>();
            public bool ThrowSample, ThrowDispose;
            public int Returns;
            public Action OnSample;
            public void Sample(float elapsed)
            {
                OnSample?.Invoke();
                if (ThrowSample) throw new InvalidOperationException("fixture output failed");
                Samples.Add(elapsed);
            }
            public void Dispose()
            {
                if (Returns != 0) return; Returns++;
                if (ThrowDispose) throw new InvalidOperationException("fixture return failed");
            }
        }
        sealed class Ticket : IDisposable
        {
            public int Returns;
            public void Dispose() { Returns++; }
        }
        sealed class Media : ICueViewFactory
        {
            public bool Delay, Fail, Throw, ReadyThenThrow;
            public int Calls;
            public Action<CueViewResult> Resolve;
            public readonly List<Output> Outputs = new List<Output>();
            public readonly List<Ticket> Tickets = new List<Ticket>();
            public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
            {
                Calls++; Resolve = resolved;
                if (Throw) throw new InvalidOperationException("fixture preparation failed");
                if (Delay) { var ticket = new Ticket(); Tickets.Add(ticket); return ticket; }
                if (Fail) { resolved(CueViewResult.Failure("fixture missing clip")); return null; }
                resolved(CueViewResult.Ready(NewOutput()));
                if (ReadyThenThrow) throw new InvalidOperationException("fixture preparation failed after delivery");
                return null;
            }
            public Output NewOutput() { var output = new Output(); Outputs.Add(output); return output; }
        }
        static AudioCueDefinition Definition(bool loop = false) => new AudioCueDefinition("fixture.audio", "fixture.clip", 48000, 48000, loop);
        static CueOccurrence Key(ICueDefinition definition) => new CueOccurrence(Guid.NewGuid(), definition.Id);
        static ICueHandle Play(ICueScope scope, AudioCueDefinition definition, Clock clock) => scope.Play(definition, Key(definition), new CueAnchor(1, 2, -1), clock);

        [Test] public void AudioMetadataDeterminesDurationSamplesAndCultureIndependentVersion()
        {
            var definition = Definition(); var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                Assert.That(Definition().ContentHash, Is.EqualTo(definition.ContentHash));
            }
            finally { CultureInfo.CurrentCulture = culture; }
            Assert.That(definition.Duration, Is.EqualTo(1)); Assert.That(definition.ClipId, Is.EqualTo("fixture.clip"));
            Assert.That(definition.SampleAt(-.25f), Is.Zero); Assert.That(definition.SampleAt(.25f), Is.EqualTo(12000));
            Assert.That(definition.SampleAt(1), Is.EqualTo(47999)); Assert.That(definition.SampleAt(100), Is.EqualTo(47999));
            Assert.That(Definition(true).SampleAt(1.25f), Is.EqualTo(12000)); Assert.That(Definition(true).SampleAt(-.25f), Is.EqualTo(36000));
            Assert.That(new AudioCueDefinition(definition.Id, "different", 48000, 48000).ContentHash, Is.Not.EqualTo(definition.ContentHash));
            Assert.That(new AudioCueDefinition(definition.Id, definition.ClipId, 24000, 24000).ContentHash, Is.Not.EqualTo(definition.ContentHash));
            Assert.That(Definition(true).ContentHash, Is.Not.EqualTo(definition.ContentHash));
            Assert.Throws<ArgumentException>(() => new AudioCueDefinition("", "clip", 1, 1));
            Assert.Throws<ArgumentException>(() => new AudioCueDefinition("cue", "", 1, 1));
            foreach (int value in new[] { 0, -1 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new AudioCueDefinition("cue", "clip", value, 48000));
                Assert.Throws<ArgumentOutOfRangeException>(() => new AudioCueDefinition("cue", "clip", 48000, value));
            }
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.SampleAt(value));
        }

        [Test] public void AudioUsesTheExistingHandleClockAndOccurrenceHistoryWithoutSyntheticFrames()
        {
            var media = new Media(); var clock = new Clock(); var definition = Definition(); var key = Key(definition);
            using (var player = new CuePlayer(media)) using (var owner = player.OpenScope("audio owner"))
            {
                var handle = owner.Play(definition, key, new CueAnchor(0, 0, 1), clock);
                Assert.That(handle.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(media.Outputs[0].Samples, Is.EqualTo(new[] { 0f }));
                clock.Time = .25f; handle.Poll(); handle.Poll(); clock.Time = .125f; handle.Poll();
                Assert.That(media.Outputs[0].Samples, Is.EqualTo(new[] { 0f, .25f, .25f, .125f }));
                var edited = new AudioCueDefinition(definition.Id, "edited", 96000, 48000);
                Assert.That(owner.Play(edited, key, new CueAnchor(5, 6, -1), new Clock()), Is.SameAs(handle));
                Assert.That(handle.DefinitionVersion, Is.EqualTo(definition.ContentHash)); Assert.That(media.Calls, Is.EqualTo(1));
                clock.Time = 1; handle.Poll(); Assert.That(handle.Status, Is.EqualTo(CueStatus.Completed));
                Assert.That(media.Outputs[0].Returns, Is.EqualTo(1)); Assert.That(player.ActiveCount, Is.Zero);
                clock.Time = 0; handle.Poll(); handle.Cancel(); handle.Dispose();
                Assert.That(owner.Play(definition, key, new CueAnchor(0, 0, 1), clock), Is.SameAs(handle));
                Assert.That(handle.Status, Is.EqualTo(CueStatus.Completed)); Assert.That(media.Calls, Is.EqualTo(1));
            }
        }

        [Test] public void LoopOwnersReturnOnlyTheirOwnAudioAndResetRejectsOldResults()
        {
            var media = new Media(); var clock = new Clock { Time = 100 };
            using (var player = new CuePlayer(media))
            {
                var firstOwner = player.OpenScope("loop"); var secondOwner = player.OpenScope("loop");
                var first = Play(firstOwner, Definition(true), clock); var second = Play(secondOwner, Definition(true), clock);
                firstOwner.Dispose(); firstOwner.Dispose(); Assert.That(first.Status, Is.EqualTo(CueStatus.Cancelled));
                Assert.That(second.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(player.ActiveCount, Is.EqualTo(1));
                media.Delay = true; var pending = Play(secondOwner, Definition(true), new Clock()); var oldResult = media.Resolve; var session = player.SessionId;
                player.Reset(); Assert.That(player.SessionId, Is.Not.EqualTo(session)); Assert.That(secondOwner.IsActive, Is.False);
                Assert.That(second.Status, Is.EqualTo(CueStatus.Cancelled)); Assert.That(pending.Status, Is.EqualTo(CueStatus.Cancelled));
                Assert.That(media.Tickets[0].Returns, Is.EqualTo(1));
                var late = media.NewOutput(); oldResult(CueViewResult.Ready(late)); oldResult(CueViewResult.Ready(late));
                Assert.That(late.Samples, Is.Empty); Assert.That(late.Returns, Is.EqualTo(1));
                using (var freshOwner = player.OpenScope("fresh"))
                {
                    media.Delay = false; var fresh = Play(freshOwner, Definition(true), new Clock());
                    oldResult(CueViewResult.Failure("stale failure")); secondOwner.Dispose(); pending.Cancel(); first.Poll();
                    Assert.That(fresh.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(player.ActiveCount, Is.EqualTo(1));
                    player.Dispose(); Assert.That(fresh.Status, Is.EqualTo(CueStatus.Cancelled)); fresh.Poll();
                }
                secondOwner.Dispose();
            }
        }

        [Test] public void DelayedResultsDoNotStartUntilTheirOwnPollAndDuplicatesCannotStopAcceptedAudio()
        {
            var media = new Media(); var peerClock = new Clock(); var lateClock = new Clock();
            using (var player = new CuePlayer(media)) using (var owner = player.OpenScope("delayed audio"))
            {
                var peer = Play(owner, Definition(true), peerClock); media.Delay = true;
                var late = Play(owner, Definition(), lateClock); var callback = media.Resolve; var output = media.NewOutput();
                callback(CueViewResult.Ready(output)); peerClock.Time = .5f; peer.Poll();
                Assert.That(output.Samples, Is.Empty); Assert.That(late.Status, Is.EqualTo(CueStatus.Pending));
                lateClock.Time = .5f; late.Poll(); Assert.That(output.Samples, Is.EqualTo(new[] { .5f }));
                callback(CueViewResult.Ready(output)); callback(CueViewResult.Failure("duplicate failure"));
                Assert.That(output.Returns, Is.Zero); Assert.That(late.Status, Is.EqualTo(CueStatus.Playing));
                var duplicate = media.NewOutput(); callback(CueViewResult.Ready(duplicate));
                Assert.That(duplicate.Returns, Is.EqualTo(1)); Assert.That(duplicate.Samples, Is.Empty);
                late.Cancel(); late.Cancel(); callback(CueViewResult.Ready(output)); Assert.That(output.Returns, Is.EqualTo(1));
                Assert.That(media.Tickets[0].Returns, Is.EqualTo(1)); Assert.That(late.Status, Is.EqualTo(CueStatus.Cancelled));
                var atEnd = Play(owner, Definition(), new Clock { Time = 1 }); var lateResult = media.Resolve;
                Assert.That(atEnd.Status, Is.EqualTo(CueStatus.Failed)); Assert.That(atEnd.Error, Does.Contain("not shown before"));
                var expired = media.NewOutput(); lateResult(CueViewResult.Ready(expired));
                Assert.That(expired.Returns, Is.EqualTo(1)); Assert.That(expired.Samples, Is.Empty);
            }
        }

        [TestCase("missing")] [TestCase("prepare")] [TestCase("ready-then-throw")] [TestCase("clock")]
        [TestCase("nonfinite")] [TestCase("sample")] [TestCase("return")]
        public void AudioFailureIsTerminalWithoutFailingAnUnrelatedOwner(string fault)
        {
            var media = new Media { Fail = fault == "missing", Throw = fault == "prepare", ReadyThenThrow = fault == "ready-then-throw" };
            var clock = new Clock { Throw = fault == "clock", Time = fault == "nonfinite" ? float.NaN : 0 };
            using (var player = new CuePlayer(media)) using (var owner = player.OpenScope("faulted audio"))
            {
                var failed = Play(owner, Definition(), clock);
                if (fault == "sample") { media.Outputs[0].ThrowSample = true; failed.Poll(); }
                if (fault == "return") { media.Outputs[0].ThrowDispose = true; clock.Time = 1; failed.Poll(); }
                Assert.That(failed.Status, Is.EqualTo(CueStatus.Failed)); Assert.That(failed.Error, Is.Not.Empty);
                failed.Cancel(); failed.Poll(); Assert.That(failed.Status, Is.EqualTo(CueStatus.Failed));
                media.Fail = media.Throw = media.ReadyThenThrow = false;
                using (var peer = player.OpenScope("unrelated audio"))
                {
                    var active = Play(peer, Definition(true), new Clock()); active.Poll();
                    Assert.That(active.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(player.ActiveCount, Is.EqualTo(1));
                }
                foreach (var output in media.Outputs) Assert.That(output.Returns, Is.EqualTo(1));
            }
        }

        [Test] public void AudioSamplingCannotReenterTheExistingLifetime()
        {
            var media = new Media();
            using (var player = new CuePlayer(media)) using (var owner = player.OpenScope("audio callback"))
            {
                var handle = Play(owner, Definition(true), new Clock()); Exception reentry = null;
                media.Outputs[0].OnSample = () => { try { player.Reset(); } catch (Exception error) { reentry = error; } };
                handle.Poll(); Assert.That(reentry, Is.TypeOf<InvalidOperationException>()); Assert.That(handle.Status, Is.EqualTo(CueStatus.Playing));
            }
        }
    }
}
