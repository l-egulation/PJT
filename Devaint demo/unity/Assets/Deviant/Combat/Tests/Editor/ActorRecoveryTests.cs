using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorRecoveryTests
    {
        sealed class Query : IActorRecoveryQuery
        {
            public readonly List<float> Observed = new List<float>();
            public Func<ActorPose, bool> Safe;
            public bool IsSafe(ActorPose pose) { Observed.Add(pose.X); return Safe(pose); }
        }
        [Test] public void CurrentInvalidSamplesAreSkippedNewestFirstWithoutSelectingFutureGround()
        {
            var history = new SafeGroundHistory();
            history.Record(1, 10, 0, -1); history.Record(2, 20, 1, 1); history.Record(3, 30, 2, -1); history.Record(4, 40, 3, 1);
            var query = new Query { Safe = pose => pose.X == 10 };
            Assert.That(history.TrySafeBefore(3, query, out var sample), Is.True);
            Assert.That(query.Observed, Is.EqualTo(new[] { 30f, 20f, 10f }));
            Assert.That(sample.Time, Is.EqualTo(1)); Assert.That(sample.Y, Is.Zero); Assert.That(sample.Facing, Is.EqualTo(-1));
            query.Safe = pose => pose.X == 20; query.Observed.Clear();
            Assert.That(history.TrySafeBefore(2, query, out sample), Is.True); Assert.That(sample.X, Is.EqualTo(20));
            Assert.That(query.Observed, Is.EqualTo(new[] { 20f }));
        }
        [Test] public void NewerRecordingCannotDiscardTheOnlyCurrentlySafeOlderPose()
        {
            var history = new SafeGroundHistory(); history.Record(0, -2, 0, 1);
            for (int i = 1; i <= 500; i++) history.Record(i * .02f, 5, 0, 1);
            var query = new Query { Safe = pose => pose.X == -2 };
            Assert.That(history.TrySafeBefore(7, query, out var sample), Is.True);
            Assert.That(sample.Time, Is.Zero); Assert.That(sample.X, Is.EqualTo(-2));
            history.Clear(); query.Observed.Clear();
            Assert.That(history.TrySafeBefore(7, query, out _), Is.False); Assert.That(query.Observed, Is.Empty);
        }
        [Test] public void FailedOrThrowingQueriesDoNotConsumeHistoryOrInventAFallback()
        {
            var history = new SafeGroundHistory(); history.Record(1, 2, 0, 1);
            var query = new Query { Safe = pose => false };
            Assert.That(history.TrySafeBefore(1, query, out _), Is.False);
            query.Safe = pose => throw new InvalidOperationException("current terrain unavailable");
            Assert.Throws<InvalidOperationException>(() => history.TrySafeBefore(1, query, out _));
            query.Safe = pose => true;
            Assert.That(history.TrySafeBefore(1, query, out var sample), Is.True); Assert.That(sample.X, Is.EqualTo(2));
        }
    }
}
