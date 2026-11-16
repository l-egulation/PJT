using System;
using System.Globalization;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class StageActorSessionTests
    {
        static StageMoveDefinition Definition(double duration = .5, int facing = -1, float depth = -2) => new StageMoveDefinition("fixture-move", duration, facing, depth);
        static StagePose Baseline(float x = 7) => new StagePose(new StagePoint(x, 3), 0, 1, false);
        [Test] public void DefinitionAndPoseValidationRejectUnsupportedValuesAndKeepStableHashes()
        {
            var definition = Definition(); var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); Assert.That(Definition().ContentHash, Is.EqualTo(definition.ContentHash));
                Assert.That(Definition(1).ContentHash, Is.Not.EqualTo(definition.ContentHash)); Assert.That(Definition(facing: 1).ContentHash, Is.Not.EqualTo(definition.ContentHash));
                Assert.That(Definition(depth: 2).ContentHash, Is.Not.EqualTo(definition.ContentHash));
            }
            finally { CultureInfo.CurrentCulture = culture; }
            foreach (double bad in new[] { 0, -1, double.NaN, double.PositiveInfinity }) Assert.Throws<ArgumentOutOfRangeException>(() => Definition(bad));
            Assert.Throws<ArgumentException>(() => new StageMoveDefinition(" ", 1, 1, 0)); Assert.Throws<ArgumentOutOfRangeException>(() => Definition(facing: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Definition(depth: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StagePoint(float.PositiveInfinity, 0)); Assert.Throws<ArgumentOutOfRangeException>(() => new StagePoint(0, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StagePose(default, 0, 0, true));
            foreach (double bad in new[] { -1, double.NaN, double.PositiveInfinity }) Assert.Throws<ArgumentOutOfRangeException>(() => definition.ProgressAt(bad));
            Assert.That(definition.ProgressAt(.25), Is.EqualTo(.5)); Assert.That(definition.ProgressAt(10), Is.EqualTo(1));
        }
        [Test] public void ExclusiveOwnerAndRetainedPoseReturnOnlyAtScopeEnd()
        {
            using (var session = new StageActorSession()) using (var owner = session.OpenScope("sequence"))
            {
                Assert.That(session.IsOwned, Is.True); Assert.Throws<InvalidOperationException>(() => session.OpenScope("other"));
                Assert.That(session.Resolve(Baseline()), Is.EqualTo(Baseline()));
                var definition = Definition(); var move = owner.Play(definition, new StagePoint(1, 2), new StagePoint(5, 6)); var first = move.Pose;
                Assert.That(move.SessionId, Is.EqualTo(session.SessionId)); Assert.That(move.DefinitionVersion, Is.EqualTo(definition.ContentHash));
                move.Poll(.25); Assert.That(move.Pose.Position, Is.EqualTo(new StagePoint(3, 4))); Assert.That(move.Pose.Depth, Is.EqualTo(-2)); Assert.That(move.Pose.Facing, Is.EqualTo(-1)); Assert.That(move.Pose.Visible, Is.True);
                Assert.That(first.Position, Is.EqualTo(new StagePoint(1, 2)), "Previously read poses stay immutable.");
                move.Poll(.5); Assert.That(move.Status, Is.EqualTo(StageMoveStatus.Completed)); Assert.That(move.IsRetained, Is.True);
                move.Poll(0); Assert.That(session.Resolve(Baseline()).Position, Is.EqualTo(new StagePoint(5, 6)));
                owner.Dispose(); Assert.That(move.Status, Is.EqualTo(StageMoveStatus.Completed)); Assert.That(move.IsRetained, Is.False); Assert.That(session.IsOwned, Is.False);
                Assert.That(session.Resolve(Baseline(9)), Is.EqualTo(Baseline(9)));
            }
        }
        [Test] public void AValidReplacementKeepsOnlyTheCurrentPoseAndInvalidReplacementPreservesIt()
        {
            using (var session = new StageActorSession()) using (var owner = session.OpenScope("owner"))
            {
                var first = owner.Play(Definition(), default, new StagePoint(2, 0)); first.Poll(.5);
                Assert.Throws<ArgumentNullException>(() => owner.Play(null, default, default)); Assert.That(first.IsRetained, Is.True);
                var next = owner.Play(Definition(1, 1, 0), new StagePoint(2, 0), new StagePoint(4, 0));
                Assert.That(first.Status, Is.EqualTo(StageMoveStatus.Completed)); Assert.That(first.IsRetained, Is.False); Assert.That(next.Pose.Position.X, Is.EqualTo(2));
                first.Cancel(); first.Poll(1); next.Poll(.5); Assert.That(session.Resolve(Baseline()).Position.X, Is.EqualTo(3));
                next.Cancel(); Assert.That(next.Status, Is.EqualTo(StageMoveStatus.Cancelled)); Assert.That(session.Resolve(Baseline()), Is.EqualTo(Baseline()));
                Assert.That(session.IsOwned, Is.True, "The scope still owns the view until it returns the lease.");
            }
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)] [TestCase(.125)]
        public void InvalidOrBackwardTimeFailsWithoutRevivingTheMove(double elapsed)
        {
            using (var session = new StageActorSession()) using (var owner = session.OpenScope("owner"))
            {
                var move = owner.Play(Definition(), default, new StagePoint(2, 0)); move.Poll(.25); move.Poll(elapsed);
                Assert.That(move.Status, Is.EqualTo(StageMoveStatus.Failed)); Assert.That(move.Error, Does.Contain("monotonic")); Assert.That(move.IsRetained, Is.False);
                Assert.That(session.Resolve(Baseline()), Is.EqualTo(Baseline())); move.Poll(.5); Assert.That(move.Status, Is.EqualTo(StageMoveStatus.Failed));
            }
        }
        [Test] public void ResetAndDisposeRejectOldOwnersAndOldHandlesCannotReturnTheNewView()
        {
            var session = new StageActorSession(); var oldOwner = session.OpenScope("same name"); var old = oldOwner.Play(Definition(), default, new StagePoint(1, 0)); var id = session.SessionId;
            old.Poll(.5); session.Reset(); Assert.That(session.SessionId, Is.Not.EqualTo(id)); Assert.That(oldOwner.IsActive, Is.False); Assert.Throws<ObjectDisposedException>(() => oldOwner.Play(Definition(), default, default));
            using (var nextOwner = session.OpenScope("same name"))
            {
                var next = nextOwner.Play(Definition(), default, new StagePoint(3, 0)); oldOwner.Dispose(); old.Cancel(); old.Poll(1);
                Assert.That(next.IsRetained, Is.True); Assert.That(session.IsOwned, Is.True); session.Dispose(); session.Dispose(); next.Poll(1);
                Assert.That(next.Status, Is.EqualTo(StageMoveStatus.Cancelled)); Assert.That(session.IsOwned || session.IsAvailable, Is.False);
                Assert.Throws<ObjectDisposedException>(() => session.OpenScope("late"));
            }
        }
        [Test] public void InterpolationBetweenExtremeFiniteCoordinatesDoesNotOverflow()
        {
            using (var session = new StageActorSession()) using (var owner = session.OpenScope("owner"))
            {
                var move = owner.Play(Definition(), new StagePoint(-float.MaxValue, 0), new StagePoint(float.MaxValue, 0)); move.Poll(.25);
                Assert.That(move.Pose.Position.X, Is.Zero); move.Poll(.5); Assert.That(move.Pose.Position.X, Is.EqualTo(float.MaxValue));
                Assert.Throws<ArgumentException>(() => session.Resolve(default));
            }
        }
    }
}
