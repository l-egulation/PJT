using System;
using System.Globalization;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class FadeDirectorTests
    {
        static FadeDefinition Definition(bool hold, double duration = .5, float from = 0, float to = 1)
            => new FadeDefinition("fixture-fade", .25f, .5f, .75f, from, to, duration, hold);
        static FadeFrame Baseline(float opacity) => new FadeFrame(.1f, .2f, .3f, opacity);
        [Test] public void DefinitionHasStableHashAndExplicitColorTimingAndRetention()
        {
            var definition = Definition(true); var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); Assert.That(Definition(true).ContentHash, Is.EqualTo(definition.ContentHash));
                Assert.That(Definition(false).ContentHash, Is.Not.EqualTo(definition.ContentHash));
                Assert.That(Definition(true, 1).ContentHash, Is.Not.EqualTo(definition.ContentHash));
                Assert.That(Definition(true, from: .1f).ContentHash, Is.Not.EqualTo(definition.ContentHash));
                Assert.That(new FadeDefinition("fixture-fade", 0, .5f, .75f, 0, 1, .5, true).ContentHash, Is.Not.EqualTo(definition.ContentHash));
                Assert.That(definition.OpacityAt(0), Is.Zero); Assert.That(definition.OpacityAt(.25), Is.EqualTo(.5f)); Assert.That(definition.OpacityAt(10), Is.EqualTo(1));
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [Test] public void InvalidDefinitionAndFrameValuesAreRejectedWithoutReplacingTheCurrentRequest()
        {
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, -.01f, 1.01f })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new FadeFrame(value, 0, 0, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => new FadeFrame(0, value, 0, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => new FadeFrame(0, 0, value, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => new FadeFrame(0, 0, 0, value));
                Assert.Throws<ArgumentOutOfRangeException>(() => new FadeDefinition("bad", value, 0, 0, 0, 1, .5, true));
                Assert.Throws<ArgumentOutOfRangeException>(() => Definition(true, from: value));
                Assert.Throws<ArgumentOutOfRangeException>(() => Definition(true, to: value));
            }
            foreach (double value in new[] { double.NaN, double.PositiveInfinity, 0, -1 }) Assert.Throws<ArgumentOutOfRangeException>(() => Definition(true, value));
            foreach (double value in new[] { double.NaN, double.PositiveInfinity, -1 }) Assert.Throws<ArgumentOutOfRangeException>(() => Definition(true).OpacityAt(value));
            Assert.Throws<ArgumentException>(() => new FadeDefinition(" ", 0, 0, 0, 0, 1, .5, true));
            using (var director = new FadeDirector()) using (var owner = director.OpenScope("owner"))
            {
                var handle = owner.Play(Definition(true), 1); handle.Poll(.25);
                Assert.Throws<ArgumentNullException>(() => owner.Play(null, 2)); Assert.Throws<ArgumentException>(() => director.OpenScope(" "));
                Assert.That(handle.IsRetained, Is.True); Assert.That(director.RequestCount, Is.EqualTo(1)); Assert.That(director.Resolve(Baseline(0)).Frame.Opacity, Is.EqualTo(.5f));
            }
        }
        [Test] public void CompletedFadeCanRetainItsTargetUntilTheOwnerReturnsIt()
        {
            using (var director = new FadeDirector()) using (var owner = director.OpenScope("owner"))
            {
                var definition = Definition(true); var handle = owner.Play(definition, 1);
                Assert.That(handle.Status, Is.EqualTo(FadeStatus.Playing)); Assert.That(handle.DefinitionVersion, Is.EqualTo(definition.ContentHash));
                Assert.That(director.Resolve(Baseline(.2f)).Frame, Is.EqualTo(new FadeFrame(.25f, .5f, .75f, 0)));
                handle.Poll(.25); Assert.That(handle.Frame.Opacity, Is.EqualTo(.5f)); handle.Poll(.5);
                Assert.That(handle.Status, Is.EqualTo(FadeStatus.Completed)); Assert.That(handle.IsRetained, Is.True); Assert.That(director.RequestCount, Is.EqualTo(1));
                handle.Poll(0); Assert.That(director.Resolve(Baseline(.2f)).Frame.Opacity, Is.EqualTo(1));
                handle.Dispose(); handle.Cancel(); Assert.That(handle.Status, Is.EqualTo(FadeStatus.Completed)); Assert.That(handle.IsRetained, Is.False);
                Assert.That(director.Resolve(Baseline(.3f)).Frame, Is.EqualTo(Baseline(.3f))); Assert.That(director.RequestCount, Is.Zero);
            }
        }
        [Test] public void FinitePlaybackWithoutRetentionReturnsToTheCurrentBaseline()
        {
            using (var director = new FadeDirector()) using (var owner = director.OpenScope("owner"))
            {
                var handle = owner.Play(Definition(false), 1); handle.Poll(.5);
                Assert.That(handle.Status, Is.EqualTo(FadeStatus.Completed)); Assert.That(handle.Frame.Opacity, Is.EqualTo(1)); Assert.That(handle.IsRetained, Is.False);
                Assert.That(director.Resolve(Baseline(.4f)).Frame, Is.EqualTo(Baseline(.4f))); Assert.That(director.RequestCount, Is.Zero);
            }
        }
        [Test] public void TheNextFadeReplacesOnlyItsOwnChannelAndOldHeldOutputCannotReappear()
        {
            using (var director = new FadeDirector()) using (var owner = director.OpenScope("same owner"))
            {
                var held = owner.Play(Definition(true), 1); held.Poll(.5);
                var next = owner.Play(Definition(false, from: 1, to: 0), 1);
                Assert.That(held.Status, Is.EqualTo(FadeStatus.Completed)); Assert.That(held.IsRetained, Is.False); Assert.That(director.RequestCount, Is.EqualTo(1));
                next.Poll(.25); Assert.That(director.Resolve(Baseline(0)).Frame.Opacity, Is.EqualTo(.5f)); next.Poll(.5);
                Assert.That(director.Resolve(Baseline(.125f)).Frame, Is.EqualTo(Baseline(.125f)), "The previous held fade must not return after a later fade ends.");
                var active = owner.Play(Definition(true), 1); var replacement = owner.Play(Definition(true), 2);
                Assert.That(active.Status, Is.EqualTo(FadeStatus.Cancelled)); active.Cancel(); held.Cancel();
                Assert.That(replacement.Status, Is.EqualTo(FadeStatus.Playing)); Assert.That(director.RequestCount, Is.EqualTo(1));
            }
        }
        [Test] public void OtherOwnersAndHiddenPlaybackKeepTheirCurrentFramesWhenARequestReturns()
        {
            using (var director = new FadeDirector())
            {
                var a = director.OpenScope("same name"); using (var b = director.OpenScope("same name"))
                {
                    var first = a.Play(Definition(true), 1); var top = b.Play(Definition(true, from: 1, to: 0), 2); top.Poll(.125); first.Poll(.25);
                    Assert.That(director.Resolve(Baseline(0)).RequestSequence, Is.EqualTo(top.Sequence)); Assert.That(director.Resolve(Baseline(0)).Frame.Opacity, Is.EqualTo(.75f));
                    b.Dispose(); Assert.That(director.Resolve(Baseline(0)).Frame.Opacity, Is.EqualTo(.5f)); Assert.That(first.Status, Is.EqualTo(FadeStatus.Playing));
                    a.Dispose(); a.Dispose(); Assert.That(first.Status, Is.EqualTo(FadeStatus.Cancelled)); Assert.That(director.RequestCount, Is.Zero);
                }
            }
        }
        [Test] public void EqualPriorityUsesLatestRequestAndReturningAnOlderOwnerKeepsTheNewOne()
        {
            using (var director = new FadeDirector())
            {
                var a = director.OpenScope("a"); using (var b = director.OpenScope("b"))
                {
                    var first = a.Play(Definition(true), 5); var next = b.Play(Definition(true, from: 1, to: 0), 5);
                    Assert.That(director.Resolve(Baseline(0)).RequestSequence, Is.EqualTo(next.Sequence)); a.Dispose(); first.Cancel();
                    Assert.That(director.Resolve(Baseline(0)).RequestSequence, Is.EqualTo(next.Sequence)); Assert.That(director.RequestCount, Is.EqualTo(1));
                }
            }
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)] [TestCase(.125)]
        public void InvalidOrBackwardElapsedTimeFailsOnlyTheOwnedRequest(double elapsed)
        {
            using (var director = new FadeDirector()) using (var a = director.OpenScope("a")) using (var b = director.OpenScope("b"))
            {
                var bad = a.Play(Definition(true), 2); var good = b.Play(Definition(true), 1); bad.Poll(.25); bad.Poll(elapsed);
                Assert.That(bad.Status, Is.EqualTo(FadeStatus.Failed)); Assert.That(bad.Error, Does.Contain("monotonic")); Assert.That(bad.IsRetained, Is.False);
                good.Poll(.25); Assert.That(director.Resolve(Baseline(0)).RequestSequence, Is.EqualTo(good.Sequence)); bad.Poll(.5); Assert.That(bad.Status, Is.EqualTo(FadeStatus.Failed));
            }
        }
        [Test] public void ResetAndDisposeInvalidateOldScopesWithoutReleasingANewSession()
        {
            var director = new FadeDirector(); var oldOwner = director.OpenScope("old"); var old = oldOwner.Play(Definition(true), 1); var session = director.SessionId;
            old.Poll(.5); director.Reset(); Assert.That(director.SessionId, Is.Not.EqualTo(session)); Assert.That(oldOwner.IsActive, Is.False); Assert.That(old.IsRetained, Is.False);
            Assert.That(old.Status, Is.EqualTo(FadeStatus.Completed)); Assert.Throws<ObjectDisposedException>(() => oldOwner.Play(Definition(true), 1));
            using (var owner = director.OpenScope("new"))
            {
                var next = owner.Play(Definition(true), 1); oldOwner.Dispose(); old.Poll(.25); old.Dispose();
                Assert.That(next.IsRetained, Is.True); Assert.That(director.RequestCount, Is.EqualTo(1)); director.Dispose(); director.Dispose(); next.Poll(.5);
                Assert.That(next.Status, Is.EqualTo(FadeStatus.Cancelled)); Assert.That(director.IsAvailable, Is.False); Assert.That(director.RequestCount, Is.Zero);
                Assert.Throws<ObjectDisposedException>(() => director.OpenScope("after")); Assert.Throws<ObjectDisposedException>(() => director.Resolve(Baseline(0)));
            }
        }
    }
}
