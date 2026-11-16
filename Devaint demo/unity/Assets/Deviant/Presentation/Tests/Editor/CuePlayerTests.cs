using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class CuePlayerTests
    {
        sealed class Clock : ICueClock { public float Time; public bool Throw; public float ReadElapsed() => Throw ? throw new InvalidOperationException("clock failure") : Time; }
        sealed class View : ICueView
        {
            public PresentationCueDefinition Definition;
            public readonly List<int> Frames = new List<int>();
            public bool Disposed, ThrowShow, ThrowDispose;
            public int DisposalCount;
            public Action OnShow, OnDispose;
            public void Sample(float elapsed) { OnShow?.Invoke(); if (ThrowShow) throw new InvalidOperationException("view failure"); Frames.Add(Definition.FrameAt(elapsed)); }
            public void Dispose() { if (Disposed) return; Disposed = true; DisposalCount++; OnDispose?.Invoke(); if (ThrowDispose) throw new InvalidOperationException("dispose failure"); }
        }
        sealed class Ticket : IDisposable
        {
            public int DisposalCount; public Action OnDispose;
            public void Dispose() { if (DisposalCount++ == 0) OnDispose?.Invoke(); }
        }
        sealed class Factory : ICueViewFactory
        {
            public bool Delayed, Fail, Throw, ReadyThenThrow;
            public int Calls;
            public readonly List<View> Views = new List<View>();
            public readonly List<Ticket> Tickets = new List<Ticket>();
            public Action<CueViewResult> Resolve;
            PresentationCueDefinition definition;
            public Action BeforeResolve;
            public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
            {
                Calls++; this.definition = (PresentationCueDefinition)definition; Resolve = resolved; BeforeResolve?.Invoke();
                if (Throw) throw new InvalidOperationException("factory failure");
                if (Delayed) { var ticket = new Ticket(); Tickets.Add(ticket); return ticket; }
                if (Fail) { resolved(CueViewResult.Failure("missing media")); return null; }
                resolved(CueViewResult.Ready(NewView()));
                if (ReadyThenThrow) throw new InvalidOperationException("factory failed after delivery");
                return null;
            }
            public View NewView() { var view = new View { Definition = definition }; Views.Add(view); return view; }
        }
        static PresentationCueDefinition Definition(bool loop = false) => new PresentationCueDefinition("fixture-cue", new[] { "frame-a", "frame-b" }, new[] { .2f, .1f }, new[] { 1f, 2f }, new[] { 3f, 4f }, false, loop);
        static CueOccurrence Key(PresentationCueDefinition definition, int ordinal = 0) => new CueOccurrence(Guid.NewGuid(), definition.Id, ordinal);
        static ICueHandle Play(ICueScope scope, PresentationCueDefinition definition, Clock clock) => scope.Play(definition, Key(definition), new CueAnchor(1, 2, 1), clock);
        [Test] public void DefinitionAndRuntimeHaveNoUnityCombatOrEditorReference()
        {
            foreach (var assembly in new[] { typeof(PresentationCueDefinition).Assembly, typeof(CuePlayer).Assembly, typeof(ICuePlayer).Assembly })
                Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat")), Is.False);
        }
        [Test] public void DefinitionCopiesFramesOffsetsAndTimingWithCultureIndependentVersion()
        {
            var keys = new[] { "frame-a", "frame-b" }; var seconds = new[] { .2f, .1f }; var x = new[] { 1f, 2f }; var y = new[] { 3f, 4f };
            var definition = new PresentationCueDefinition("fixture-cue", keys, seconds, x, y); var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); Assert.That(Definition().ContentHash, Is.EqualTo(definition.ContentHash));
                keys[0] = "changed"; seconds[0] = 1; x[0] = 90; y[0] = 91;
                Assert.That(definition.Frames[0], Is.EqualTo("frame-a")); Assert.That(definition.Seconds[0], Is.EqualTo(.2f)); Assert.That(definition.X[0], Is.EqualTo(1)); Assert.That(definition.Y[0], Is.EqualTo(3));
                Assert.Throws<NotSupportedException>(() => ((IList<float>)definition.Seconds)[0] = 1);
                Assert.That(definition.ContentHash, Is.Not.EqualTo(new PresentationCueDefinition("fixture-cue", keys, seconds, x, y).ContentHash));
            }
            finally { CultureInfo.CurrentCulture = saved; }
            Assert.That(definition.FrameAt(0), Is.Zero); Assert.That(definition.FrameAt(.2f), Is.EqualTo(1)); Assert.That(definition.FrameAt(.5f), Is.EqualTo(1));
            Assert.That(Definition(true).FrameAt(.31f), Is.Zero);
        }
        [Test] public void PublishedSpriteVersionOneHashesRemainCompatibleAfterAudioSamplingExtension()
        {
            // Measured from the pre-audio Player Definitions DLL, not a second
            // implementation of hashing in this fixture.
            Assert.That(Definition().ContentHash, Is.EqualTo("fee237ff9e97487cd412217b0e737398d6139d6cadf918555b48e22458dee8fa"));
            Assert.That(Definition(true).ContentHash, Is.EqualTo("586f7d0ff2ab44e3d6ce7cd3c8b385d8dad9c69132fd8a3c71502706319c5c53"));
        }
        [Test] public void InvalidDefinitionAndRequestCannotCreateMedia()
        {
            foreach (float invalid in new[] { 0, -1, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new PresentationCueDefinition("invalid", new[] { "a" }, new[] { invalid }, new[] { 0f }, new[] { 0f }));
            Assert.Throws<ArgumentException>(() => new PresentationCueDefinition("invalid", new[] { "a" }, new[] { .1f, .2f }, new[] { 0f }, new[] { 0f }));
            var factory = new Factory(); using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("test"))
            {
                Assert.Throws<ArgumentException>(() => owner.Play(Definition(), default, new CueAnchor(0, 0, 1), new Clock()));
                Assert.Throws<ArgumentException>(() => owner.Play(Definition(), Key(Definition()), default, new Clock()));
                Assert.That(factory.Calls, Is.Zero); Assert.That(player.ActiveCount, Is.Zero);
            }
        }
        [Test] public void SameOccurrenceKeepsStartingVersionAndDoesNotReplayAfterCompletion()
        {
            var factory = new Factory(); var definition = Definition(); var key = Key(definition); var clock = new Clock();
            using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("action"))
            {
                var handle = owner.Play(definition, key, new CueAnchor(0, 0, 1), clock);
                var edited = new PresentationCueDefinition(definition.Id, new[] { "edited" }, new[] { 1f }, new[] { 0f }, new[] { 0f });
                Assert.That(owner.Play(edited, key, new CueAnchor(10, 20, -1), new Clock()), Is.SameAs(handle));
                Assert.That(handle.DefinitionVersion, Is.EqualTo(definition.ContentHash)); Assert.That(factory.Calls, Is.EqualTo(1));
                clock.Time = .2f; player.Tick(); Assert.That(factory.Views[0].Frames.Last(), Is.EqualTo(1));
                clock.Time = .3f; player.Tick(); Assert.That(handle.Status, Is.EqualTo(CueStatus.Completed));
                Assert.That(player.ActiveCount, Is.Zero); handle.Cancel(); handle.Dispose();
                Assert.That(handle.Status, Is.EqualTo(CueStatus.Completed)); Assert.That(factory.Views[0].DisposalCount, Is.EqualTo(1));
                Assert.That(owner.Play(definition, key, new CueAnchor(0, 0, 1), clock), Is.SameAs(handle)); Assert.That(factory.Calls, Is.EqualTo(1));
                Assert.That(owner.Play(definition, new CueOccurrence(key.ExecutionId, key.CueId, 1), new CueAnchor(0, 0, 1), new Clock()).Status, Is.EqualTo(CueStatus.Playing));
                Assert.That(factory.Calls, Is.EqualTo(2), "Different occurrence ordinals are distinct media requests.");
            }
        }
        [Test] public void OwnersWithSameNameReleaseOnlyTheirOwnLoops()
        {
            var factory = new Factory(); using (var player = new CuePlayer(factory))
            {
                var a = player.OpenScope("owner"); using (var b = player.OpenScope("owner"))
                {
                    var clock = new Clock { Time = 10 }; var first = Play(a, Definition(true), clock); var second = Play(b, Definition(true), clock);
                    Assert.That(player.ActiveCount, Is.EqualTo(2)); a.Dispose(); a.Dispose();
                    Assert.That(first.Status, Is.EqualTo(CueStatus.Cancelled)); Assert.That(second.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(player.ActiveCount, Is.EqualTo(1));
                    second.Cancel(); second.Cancel(); Assert.That(second.Status, Is.EqualTo(CueStatus.Cancelled)); Assert.That(player.ActiveCount, Is.Zero);
                    Assert.That(factory.Views.All(v => v.DisposalCount == 1), Is.True);
                }
            }
        }
        [Test] public void ResetInvalidatesOldScopesAndDelayedMediaCannotReviveThem()
        {
            var factory = new Factory { Delayed = true }; using (var player = new CuePlayer(factory))
            {
                var oldOwner = player.OpenScope("old"); var old = Play(oldOwner, Definition(true), new Clock()); var callback = factory.Resolve; var oldSession = player.SessionId;
                player.Reset(); Assert.That(old.Status, Is.EqualTo(CueStatus.Cancelled)); Assert.That(oldOwner.IsActive, Is.False); Assert.That(factory.Tickets[0].DisposalCount, Is.EqualTo(1));
                Assert.That(player.SessionId, Is.Not.EqualTo(oldSession)); Assert.Throws<ObjectDisposedException>(() => Play(oldOwner, Definition(), new Clock()));
                var late = factory.NewView(); callback(CueViewResult.Ready(late)); callback(CueViewResult.Ready(late));
                Assert.That(late.Disposed, Is.True); Assert.That(late.Frames, Is.Empty); Assert.That(player.ActiveCount, Is.Zero);
                using (var next = player.OpenScope("next"))
                {
                    factory.Delayed = false; var active = Play(next, Definition(true), new Clock()); oldOwner.Dispose(); old.Cancel();
                    Assert.That(active.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(player.ActiveCount, Is.EqualTo(1));
                }
            }
        }
        [TestCase(.2f)] [TestCase(.3f)]
        public void PollingOneHandleCannotSampleAPeerWhoseClockHasNotBeenUpdated(float arrival)
        {
            var factory = new Factory(); var readyClock = new Clock(); var lateClock = new Clock();
            using (var player = new CuePlayer(factory)) using (var readyOwner = player.OpenScope("ready")) using (var lateOwner = player.OpenScope("late"))
            {
                var ready = Play(readyOwner, Definition(true), readyClock);
                factory.Delayed = true; var late = Play(lateOwner, Definition(), lateClock); var view = factory.NewView();
                factory.Resolve(CueViewResult.Ready(view)); readyClock.Time = arrival; ready.Poll();
                Assert.That(view.Frames, Is.Empty, "Only the late owner may sample its prepared media."); Assert.That(late.Status, Is.EqualTo(CueStatus.Pending));
                lateClock.Time = arrival; late.Poll();
                if (arrival == .3f)
                { Assert.That(late.Status, Is.EqualTo(CueStatus.Failed)); Assert.That(late.Error, Does.Contain("not shown before")); Assert.That(view.Frames, Is.Empty); Assert.That(view.Disposed, Is.True); }
                else { Assert.That(late.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(view.Frames, Is.EqualTo(new[] { 1 })); }
                Assert.That(ready.Status, Is.EqualTo(CueStatus.Playing));
            }
        }
        [Test] public void HandlePollingRejectsCallbackReentryAndOldHandlesCannotReviveOrSampleNewOwners()
        {
            var factory = new Factory(); var clock = new Clock();
            using (var player = new CuePlayer(factory)) using (var a = player.OpenScope("a")) using (var b = player.OpenScope("b"))
            {
                var first = Play(a, Definition(true), clock); var peer = Play(b, Definition(true), new Clock()); Exception reentry = null;
                int peerFrames = factory.Views[1].Frames.Count;
                factory.Views[0].OnShow = () => { try { peer.Poll(); } catch (Exception exception) { reentry = exception; } };
                clock.Time = .2f; first.Poll(); Assert.That(reentry, Is.TypeOf<InvalidOperationException>()); Assert.That(factory.Views[1].Frames.Count, Is.EqualTo(peerFrames));
                Assert.That(first.Status, Is.EqualTo(CueStatus.Playing)); player.Reset(); first.Poll(); peer.Poll();
                using (var nextOwner = player.OpenScope("new"))
                {
                    var next = Play(nextOwner, Definition(true), new Clock()); int frames = factory.Views.Last().Frames.Count;
                    first.Poll(); peer.Poll(); Assert.That(factory.Views.Last().Frames.Count, Is.EqualTo(frames)); Assert.That(next.Status, Is.EqualTo(CueStatus.Playing));
                    player.Dispose(); next.Poll(); Assert.That(next.Status, Is.EqualTo(CueStatus.Cancelled));
                }
            }
        }
        [Test] public void DelayedLoadingUsesTheSuppliedClockAndExpiresWithoutAVisualReplay()
        {
            var factory = new Factory { Delayed = true }; var clock = new Clock();
            using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("delayed"))
            {
                var handle = Play(owner, Definition(), clock); Assert.That(handle.Status, Is.EqualTo(CueStatus.Pending));
                clock.Time = .2f; var loaded = factory.NewView(); factory.Resolve(CueViewResult.Ready(loaded)); player.Tick();
                Assert.That(handle.Status, Is.EqualTo(CueStatus.Playing)); Assert.That(loaded.Frames, Is.EqualTo(new[] { 1 }));
                factory.Resolve(CueViewResult.Ready(loaded)); Assert.That(loaded.Disposed, Is.False, "Duplicate delivery of the accepted view cannot stop it.");
                var duplicate = factory.NewView(); factory.Resolve(CueViewResult.Ready(duplicate)); Assert.That(duplicate.Disposed, Is.True);
                clock.Time = .3f; player.Tick(); Assert.That(handle.Status, Is.EqualTo(CueStatus.Completed));
                clock.Time = 0; var pending = Play(owner, Definition(), clock); var lateCallback = factory.Resolve;
                clock.Time = 1; player.Tick(); Assert.That(pending.Status, Is.EqualTo(CueStatus.Failed)); Assert.That(pending.Error, Does.Contain("not shown"));
                var late = factory.NewView(); lateCallback(CueViewResult.Ready(late)); Assert.That(late.Disposed, Is.True); Assert.That(late.Frames, Is.Empty);
            }
        }
        [TestCase("missing")] [TestCase("prepare")] [TestCase("ready-then-throw")] [TestCase("clock")] [TestCase("nonfinite")] [TestCase("view")]
        public void PresentationFailureIsTerminalAndOtherOwnersContinue(string fault)
        {
            var factory = new Factory(); var clock = new Clock(); using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("test"))
            {
                factory.Fail = fault == "missing"; factory.Throw = fault == "prepare"; factory.ReadyThenThrow = fault == "ready-then-throw";
                clock.Throw = fault == "clock"; if (fault == "nonfinite") clock.Time = float.NaN;
                var broken = Play(owner, Definition(), clock);
                if (fault == "view") { factory.Views[0].ThrowShow = true; player.Tick(); }
                Assert.That(broken.Status, Is.EqualTo(CueStatus.Failed)); Assert.That(broken.Error, Is.Not.Empty); broken.Cancel();
                factory.Fail = factory.Throw = factory.ReadyThenThrow = false;
                using (var peer = player.OpenScope("unrelated"))
                {
                    var valid = Play(peer, Definition(true), new Clock()); player.Tick(); Assert.That(valid.Status, Is.EqualTo(CueStatus.Playing));
                    Assert.That(factory.Views.Where(v => v != factory.Views.Last()).All(v => v.Disposed), Is.True);
                }
            }
        }
        [Test] public void MediaCallbacksCannotReenterRequestsAndCleanupStillAttemptsEveryOwnedView()
        {
            var factory = new Factory(); var player = new CuePlayer(factory); var owner = player.OpenScope("test");
            Exception reentry = null; var disposalErrors = new List<Exception>();
            factory.BeforeResolve = () => { try { player.Reset(); } catch (Exception error) { reentry = error; } };
            var first = Play(owner, Definition(true), new Clock()); var second = Play(owner, Definition(true), new Clock());
            Assert.That(reentry, Is.TypeOf<InvalidOperationException>()); Assert.That(first.Status, Is.EqualTo(CueStatus.Playing));
            foreach (var view in factory.Views)
            {
                view.ThrowDispose = true;
                view.OnDispose = () => { try { player.OpenScope("reentrant"); } catch (Exception error) { disposalErrors.Add(error); } };
            }
            player.Dispose(); Assert.That(factory.Views.All(v => v.Disposed && v.DisposalCount == 1), Is.True);
            Assert.That(disposalErrors.Count, Is.EqualTo(2)); Assert.That(disposalErrors.All(e => e is ObjectDisposedException), Is.True);
            Assert.That(first.Error, Does.Contain("dispose failure")); Assert.That(second.Error, Does.Contain("dispose failure"));
            Assert.That(player.ActiveCount, Is.Zero); player.Dispose(); owner.Dispose(); Assert.Throws<ObjectDisposedException>(() => player.Tick());
        }
    }
}
