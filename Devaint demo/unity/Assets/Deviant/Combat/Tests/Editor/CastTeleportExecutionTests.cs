using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CastTeleportExecutionTests
    {
        sealed class State
        {
            public CastTeleportExecution Execution;
            public float Elapsed, Position = 3, Target = 10, Destination, Previous = -1;
            public int Facing = 1, TargetFacing = -1, Records;
            public bool Alive = true, Hit = true;
            public readonly List<string> Events = new List<string>();
            public Action ReadElapsed, ReadAlive, ReadHit, Face, Capture, Move, PreviousOwner, Record;
        }
        struct Host : ICastTeleportHost
        {
            public State State;
            public float Elapsed { get { State.Events.Add("elapsed"); State.ReadElapsed?.Invoke(); return State.Elapsed; } }
            public bool MarkedTargetAlive { get { State.Events.Add("alive"); State.ReadAlive?.Invoke(); return State.Alive; } }
            public bool TargetHit { get { State.Events.Add("hit"); State.ReadHit?.Invoke(); return State.Hit; } }
            public void FaceMarkedTarget() { State.Events.Add("face"); State.Facing = -State.TargetFacing; State.Face?.Invoke(); }
            public void CaptureStrikeDestination() { State.Events.Add("destination"); State.Destination = State.Target; State.Capture?.Invoke(); }
            public void TeleportToStrikeDestination() { State.Events.Add("strike"); State.Position = State.Destination; State.Move?.Invoke(); }
            public void CapturePreviousOwner() { State.Events.Add("previous"); State.Previous = State.Position; State.PreviousOwner?.Invoke(); }
            public void RecordStrike() { State.Events.Add("record"); State.Records++; State.Record?.Invoke(); }
            public void TeleportToMarkedTarget() { State.Events.Add("marked"); State.Position = State.Target; State.Move?.Invoke(); }
            public void TeleportForward() { State.Events.Add("forward"); State.Position += State.Facing * 2; State.Move?.Invoke(); }
        }
        static Host Create(string type = "target_teleport_strike", float start = .25f)
        {
            var state = new State { Execution = new CastTeleportExecution(type == "absent" ? null : new SkillMovementDefinition(new MoveData { type = type, start = start })) };
            return new Host { State = state };
        }
        static void Prepare(ref Host host, float step = .25f) => host.State.Execution.Prepare(step, ref host);
        static void Reenter(State state)
        {
            var nested = new Host { State = state }; int effects = state.Events.Count;
            Prepare(ref nested, 100); Assert.That(state.Events.Count, Is.EqualTo(effects), "Reentry observes the field's claim without resampling the host.");
        }

        [TestCase("absent")]
        [TestCase("dash")]
        [TestCase("target_dash_recast")]
        public void UnownedMovementDoesNotReadTheHostOrClaim(string type)
        {
            var host = Create(type); var state = host.State; state.ReadElapsed = () => throw new InvalidOperationException("Not a teleport");
            Prepare(ref host, 100); Assert.That(state.Execution.Claimed, Is.False); Assert.That(state.Events, Is.Empty); Assert.That(state.Position, Is.EqualTo(3));
            var empty = default(CastTeleportExecution); empty.Prepare(100, ref host); Assert.That(empty.Claimed, Is.False);
        }
        [Test] public void BeforeStartIsNotBufferedAndExactCrossingClaimsOnce()
        {
            var host = Create(); var state = host.State; state.Elapsed = .125f;
            Prepare(ref host, .1240234375f); Assert.That(state.Execution.Claimed, Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "elapsed" }));
            Assert.That(state.Position, Is.EqualTo(3)); state.Events.Clear(); Prepare(ref host, .125f);
            Assert.That(state.Execution.Claimed, Is.True); Assert.That(state.Position, Is.EqualTo(10)); Assert.That(state.Previous, Is.EqualTo(10));
            Assert.That(state.Events, Is.EqualTo(new[] { "elapsed", "face", "destination", "strike", "previous", "record" }));
            state.Elapsed = 99; state.Target = 40; state.Events.Clear(); Prepare(ref host, 1);
            Assert.That(state.Events, Is.Empty); Assert.That(state.Position, Is.EqualTo(10)); Assert.That(state.Records, Is.EqualTo(1));
        }
        [Test] public void CoarseStepUsesTheSameOneShotAndDoesNotAccumulateAnIndependentClock()
        {
            var host = Create(start: 2); var state = host.State;
            for (int i = 0; i < 8; i++) Prepare(ref host, .5f);
            Assert.That(state.Execution.Claimed, Is.False); Assert.That(state.Position, Is.EqualTo(3));
            state.Elapsed = 1.5f; Prepare(ref host, .5f); Assert.That(state.Execution.Claimed, Is.True); Assert.That(state.Records, Is.EqualTo(1));
            state.Elapsed = 0; Prepare(ref host, 10); Assert.That(state.Records, Is.EqualTo(1));
        }
        [TestCase(float.NaN, .25f)]
        [TestCase(0, float.NaN)]
        [TestCase(float.NegativeInfinity, .25f)]
        public void UnorderedStartComparisonDoesNotAcquireAClaim(float elapsed, float step)
        {
            var host = Create(); var state = host.State; state.Elapsed = elapsed; Prepare(ref host, step);
            Assert.That(state.Execution.Claimed, Is.False); Assert.That(state.Events, Is.EqualTo(new[] { "elapsed" }));
        }
        [Test] public void ElapsedGetterFailurePrecedesTheClaimAndCanBeRetriedWithoutAReset()
        {
            var host = Create(); var state = host.State; var error = new InvalidOperationException("clock"); state.ReadElapsed = () => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Prepare(ref host)), Is.SameAs(error));
            Assert.That(state.Execution.Claimed, Is.False); Assert.That(state.Facing, Is.EqualTo(1)); Assert.That(state.Records, Is.Zero);
            state.ReadElapsed = null; Prepare(ref host); Assert.That(state.Execution.Claimed, Is.True); Assert.That(state.Records, Is.EqualTo(1));
        }
        [Test] public void ClaimPrecedesEveryStrikeCallbackAndReentryCannotMoveAgain()
        {
            var host = Create(); var state = host.State; int callbacks = 0;
            Action observe = () => { callbacks++; Assert.That(state.Execution.Claimed, Is.True); Reenter(state); };
            state.Face = state.Capture = state.Move = state.PreviousOwner = state.Record = observe;
            Prepare(ref host); Assert.That(callbacks, Is.EqualTo(5)); Assert.That(state.Records, Is.EqualTo(1));
            Assert.That(state.Events, Is.EqualTo(new[] { "elapsed", "face", "destination", "strike", "previous", "record" }));
        }
        [TestCase("face", 3, -1, 0)]
        [TestCase("destination", 3, -1, 0)]
        [TestCase("strike", 10, -1, 0)]
        [TestCase("previous", 10, 10, 0)]
        [TestCase("record", 10, 10, 1)]
        public void StrikeExceptionsRetainTheClaimAndEveryEarlierAppliedFact(string boundary, float position, float previous, int records)
        {
            var host = Create(); var state = host.State; state.TargetFacing = 1; var error = new InvalidOperationException(boundary);
            Action fail = () => { Assert.That(state.Execution.Claimed, Is.True); throw error; };
            if (boundary == "face") state.Face = fail;
            if (boundary == "destination") state.Capture = fail;
            if (boundary == "strike") state.Move = fail;
            if (boundary == "previous") state.PreviousOwner = fail;
            if (boundary == "record") state.Record = fail;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Prepare(ref host)), Is.SameAs(error));
            Assert.That(state.Execution.Claimed, Is.True); Assert.That(state.Facing, Is.EqualTo(-1)); Assert.That(state.Position, Is.EqualTo(position));
            Assert.That(state.Previous, Is.EqualTo(previous)); Assert.That(state.Records, Is.EqualTo(records));
            state.Face = state.Capture = state.Move = state.PreviousOwner = state.Record = null; state.Events.Clear(); Prepare(ref host, 100);
            Assert.That(state.Events, Is.Empty); Assert.That(state.Position, Is.EqualTo(position)); Assert.That(state.Previous, Is.EqualTo(previous)); Assert.That(state.Records, Is.EqualTo(records));
        }
        [Test] public void FacingAndDestinationCallbacksUseLiveHostFactsAndPreviousOwnerReadsActualArrival()
        {
            var host = Create(); var state = host.State;
            state.Face = () => { state.Target = 12; state.Position = 4; state.Alive = state.Hit = false; };
            state.Capture = () => state.Target = 99;
            state.Move = () => state.Position = 8;
            state.PreviousOwner = () => state.Position = 9;
            state.Record = () =>
            {
                Assert.That(state.Position, Is.EqualTo(9)); Assert.That(state.Previous, Is.EqualTo(8)); Reenter(state);
            };
            Prepare(ref host);
            Assert.That(state.Destination, Is.EqualTo(12)); Assert.That(state.Target, Is.EqualTo(99)); Assert.That(state.Facing, Is.EqualTo(1));
            Assert.That(state.Previous, Is.EqualTo(8)); Assert.That(state.Records, Is.EqualTo(1));
            Assert.That(state.Events, Does.Not.Contain("alive").And.Not.Contain("hit"), "Strike keeps the already selected reference and does not add a target-liveness policy.");
        }
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void TargetTeleportSamplesLiveAliveThenAcceptedHitAfterClaim(bool alive, bool hit, bool moves)
        {
            var host = Create("target_teleport"); var state = host.State; state.Alive = alive; state.Hit = hit;
            state.ReadAlive = () => { Assert.That(state.Execution.Claimed, Is.True); Reenter(state); };
            state.ReadHit = () => { Assert.That(state.Execution.Claimed, Is.True); Reenter(state); };
            Prepare(ref host); Assert.That(state.Execution.Claimed, Is.True); Assert.That(state.Position, Is.EqualTo(moves ? 10 : 3));
            Assert.That(state.Events, Is.EqualTo(!alive ? new[] { "elapsed", "alive" } : hit ? new[] { "elapsed", "alive", "hit", "marked" } : new[] { "elapsed", "alive", "hit" }));
            state.Alive = state.Hit = true; state.Target = 99; state.Events.Clear(); Prepare(ref host, 100);
            Assert.That(state.Events, Is.Empty); Assert.That(state.Position, Is.EqualTo(moves ? 10 : 3)); Assert.That(state.Previous, Is.EqualTo(-1)); Assert.That(state.Records, Is.Zero);
        }
        [Test] public void TargetHitIsNotPrematurelyCapturedBeforeTheAliveGetterOrMovementEffect()
        {
            var host = Create("target_teleport"); var state = host.State; state.Hit = false;
            state.ReadAlive = () => { state.Hit = true; state.Target = 12; };
            state.ReadHit = () => state.Target = 15;
            state.Move = () => { Assert.That(state.Position, Is.EqualTo(15)); Reenter(state); };
            Prepare(ref host); Assert.That(state.Position, Is.EqualTo(15)); Assert.That(state.Facing, Is.EqualTo(1));
            Assert.That(state.Events, Is.EqualTo(new[] { "elapsed", "alive", "hit", "marked" }));
        }
        [TestCase("alive")]
        [TestCase("hit")]
        [TestCase("marked")]
        public void TargetGetterOrMovementExceptionsCannotReopenTheOneShot(string boundary)
        {
            var host = Create("target_teleport"); var state = host.State; var error = new InvalidOperationException(boundary);
            Action fail = () => { Assert.That(state.Execution.Claimed, Is.True); throw error; };
            if (boundary == "alive") state.ReadAlive = fail;
            if (boundary == "hit") state.ReadHit = fail;
            if (boundary == "marked") state.Move = fail;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Prepare(ref host)), Is.SameAs(error)); Assert.That(state.Execution.Claimed, Is.True);
            state.ReadAlive = state.ReadHit = state.Move = null; state.Events.Clear(); Prepare(ref host, 100); Assert.That(state.Events, Is.Empty);
            Assert.That(state.Position, Is.EqualTo(boundary == "marked" ? 10 : 3));
        }
        [TestCase("teleport")]
        [TestCase("other-movement")]
        [TestCase(null)]
        public void EveryOtherAdmittedMovementDispatchesForwardWithoutTargetChecks(string type)
        {
            var host = Create(type); var state = host.State; state.Alive = state.Hit = false; state.Facing = -1;
            state.Move = () => { Assert.That(state.Execution.Claimed, Is.True); Reenter(state); };
            Prepare(ref host); Assert.That(state.Position, Is.EqualTo(1)); Assert.That(state.Events, Is.EqualTo(new[] { "elapsed", "forward" }));
            Assert.That(state.Previous, Is.EqualTo(-1)); Assert.That(state.Records, Is.Zero);
        }
        [Test] public void ForwardExceptionLeavesItsAppliedMovementAndClaimIntact()
        {
            var host = Create("teleport"); var state = host.State; var error = new InvalidOperationException("forward");
            state.Move = () => throw error; Assert.That(Assert.Throws<InvalidOperationException>(() => Prepare(ref host)), Is.SameAs(error));
            Assert.That(state.Execution.Claimed, Is.True); Assert.That(state.Position, Is.EqualTo(5));
            state.Move = null; state.Events.Clear(); Prepare(ref host); Assert.That(state.Events, Is.Empty); Assert.That(state.Position, Is.EqualTo(5));
        }
    }
}
