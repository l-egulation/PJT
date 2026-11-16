using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ContactBombExecutionTests
    {
        sealed class State
        {
            public ContactBombExecution Execution;
            public float Time, GravityValue = 20, VelocityX = 8.25f, VelocityY = -1.75f, X = 1.5f, Y = -2.25f;
            public float DestinationX, DestinationY;
            public bool HasStopped, ExpiryExplodes = true;
            public int Explosions, Removals, GravityReads;
            public readonly List<string> Events = new List<string>();
            public Action ReadingGravity, VelocityWritten, Swept;
        }
        struct Host : IContactBombHost
        {
            public State State;
            public int VelocityWrites, Sweeps;
            public float Now { get { State.Events.Add("now"); return State.Time; } }
            public bool Stopped { get { State.Events.Add("stopped"); return State.HasStopped; } }
            public bool ExplodeOnExpiry { get { State.Events.Add("expiry"); return State.ExpiryExplodes; } }
            public float Gravity
            {
                get
                {
                    State.Events.Add("gravity"); State.GravityReads++; State.ReadingGravity?.Invoke();
                    return State.GravityValue;
                }
            }
            public float VelocityX { get { State.Events.Add("vx"); return State.VelocityX; } }
            public float VelocityY { get { State.Events.Add("vy"); return State.VelocityY; } }
            public float PositionX { get { State.Events.Add("px"); return State.X; } }
            public float PositionY { get { State.Events.Add("py"); return State.Y; } }
            public void SetVelocityY(float velocity)
            {
                State.Events.Add("set-vy"); VelocityWrites++; State.VelocityY = velocity; State.VelocityWritten?.Invoke();
            }
            public void SweepTo(float x, float y)
            {
                State.Events.Add("sweep"); Sweeps++; State.DestinationX = State.X = x; State.DestinationY = State.Y = y;
                State.Swept?.Invoke();
            }
            public void Explode()
            {
                if (State.HasStopped) return;
                State.HasStopped = true; State.Events.Add("explode"); State.Explosions++;
            }
            public void Remove() { State.HasStopped = true; State.Events.Add("remove"); State.Removals++; }
        }
        static Host Create(float now = 0, float lifetime = 5, bool explode = true)
        {
            var state = new State { Time = now, ExpiryExplodes = explode };
            state.Execution.Initialize(now, lifetime); return new Host { State = state };
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        static float After(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) + 1);
        static void AssertFloat(float actual, float expected)
        {
            if (float.IsNaN(expected)) Assert.That(float.IsNaN(actual), Is.True);
            else Assert.That(Bits(actual), Is.EqualTo(Bits(expected)));
        }
        static void AssertFlight(ref Host host, float dt, float step)
        {
            var state = host.State;
            float x = state.X, y = state.Y, vx = state.VelocityX, vy = state.VelocityY, gravity = state.GravityValue;
            float dx = vx * step + 0f * (.5f * gravity * step * step);
            float dy = vy * step + -1f * (.5f * gravity * step * step);
            state.Execution.Step(dt, ref host);
            AssertFloat(state.DestinationX, x + dx); AssertFloat(state.DestinationY, y + dy);
            AssertFloat(state.VelocityX, vx); AssertFloat(state.VelocityY, vy - gravity * step);
            Assert.That(host.VelocityWrites, Is.EqualTo(1)); Assert.That(host.Sweeps, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void AbsoluteDeadlineIsInclusiveWithoutAToleranceOrAnAccumulatedAge(bool explode)
        {
            var before = Create(2, .5f, explode); before.State.Time = Before(2.5f);
            before.State.Execution.Step(0, ref before);
            Assert.That(before.Sweeps, Is.EqualTo(1)); Assert.That(before.State.HasStopped, Is.False);
            foreach (float now in new[] { 2.5f, After(2.5f) })
            {
                var host = Create(2, .5f, explode); host.State.Time = now;
                host.State.Execution.Step(-100, ref host);
                Assert.That(host.Sweeps, Is.Zero); Assert.That(host.VelocityWrites, Is.Zero);
                Assert.That(host.State.HasStopped, Is.True);
                Assert.That(host.State.Explosions, Is.EqualTo(explode ? 1 : 0));
                Assert.That(host.State.Removals, Is.EqualTo(explode ? 0 : 1));
                Assert.That(host.State.Events, Is.EqualTo(new[] { "now", "expiry", explode ? "explode" : "remove" }));
            }
        }
        [TestCase(0f)] [TestCase(-.125f)]
        public void NonpositiveLifetimeStillTerminatesBeforeAnyMovement(float lifetime)
        {
            var host = Create(4, lifetime); host.State.Execution.Step(.02f, ref host);
            Assert.That(host.State.Explosions, Is.EqualTo(1)); Assert.That(host.Sweeps, Is.Zero);
            Assert.That(host.State.GravityReads, Is.Zero);
        }
        [Test] public void InitializeUsesTheOriginalRoundedNowPlusLifetimeSum()
        {
            float now = 8388608, lifetime = .75f, deadline = now + lifetime;
            var host = Create(now, lifetime); host.State.Time = Before(deadline);
            host.State.Execution.Step(0, ref host); Assert.That(host.State.HasStopped, Is.False);
            host.State.Time = deadline; host.State.Execution.Step(0, ref host);
            Assert.That(host.State.Explosions, Is.EqualTo(1)); Assert.That(host.Sweeps, Is.EqualTo(1));
        }
        [TestCase(.02f, 20f)] [TestCase(.1234567f, -20f)] [TestCase(.375f, 0f)]
        [TestCase(0f, 20f)] [TestCase(-.125f, 20f)]
        public void FlightPreservesTheOriginalFloatOperationOrderAndDoesNotClampNonpositiveTicks(float dt, float gravity)
        {
            var host = Create(); host.State.GravityValue = gravity; AssertFlight(ref host, dt, dt);
            Assert.That(host.State.HasStopped, Is.False);
        }
        [Test] public void SignedZeroProductsAndTheHorizontalZeroGravityTermAreNotSimplifiedAway()
        {
            foreach (float gravity in new[] { BitConverter.Int32BitsToSingle(int.MinValue), float.PositiveInfinity, float.NegativeInfinity, float.NaN })
            {
                var host = Create(); host.State.GravityValue = gravity;
                host.State.VelocityX = BitConverter.Int32BitsToSingle(int.MinValue); host.State.VelocityY = 0;
                AssertFlight(ref host, .125f, .125f);
                if (!float.IsInfinity(gravity) && !float.IsNaN(gravity)) continue;
                Assert.That(float.IsNaN(host.State.DestinationX), Is.True);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void LongTickClampsFlightButOnlyExpiryExplosionsOccurAtItsEnd(bool explode)
        {
            var host = Create(3, .375f, explode); host.State.Time = 3.25f;
            AssertFlight(ref host, .5f, .125f);
            Assert.That(host.State.Explosions, Is.EqualTo(explode ? 1 : 0)); Assert.That(host.State.Removals, Is.Zero);
            Assert.That(host.State.HasStopped, Is.EqualTo(explode));
            if (explode) return;
            host.State.Time = 3.375f; host.State.Execution.Step(.02f, ref host);
            Assert.That(host.State.Removals, Is.EqualTo(1)); Assert.That(host.Sweeps, Is.EqualTo(1));
        }
        [Test] public void PostflightExpiryIncludesTheExactTickEndButNotAnEarlierTickEnd()
        {
            var outside = Create(lifetime: 1); outside.State.Time = .5f;
            outside.State.Execution.Step(.25f, ref outside);
            Assert.That(outside.State.Explosions, Is.Zero);
            var exact = Create(lifetime: 1); exact.State.Time = .5f;
            exact.State.Execution.Step(.5f, ref exact); Assert.That(exact.State.Explosions, Is.EqualTo(1));
        }
        [Test] public void RepeatedStepsWithoutAdvancingTheHostClockDoNotConsumeAnotherLifetime()
        {
            var host = Create(lifetime: 1);
            for (int i = 0; i < 8; i++) host.State.Execution.Step(.25f, ref host);
            Assert.That(host.Sweeps, Is.EqualTo(8)); Assert.That(host.State.HasStopped, Is.False);
            host.State.Time = 1; host.State.Execution.Step(0, ref host);
            Assert.That(host.State.Explosions, Is.EqualTo(1)); Assert.That(host.Sweeps, Is.EqualTo(8));
        }
        [Test] public void ReinitializationReplacesTheDeadlineWithoutSharingStateBetweenBombs()
        {
            var first = Create(lifetime: 1); var second = Create(lifetime: 2);
            first.State.Execution.Initialize(4, .5f); first.State.Time = 1; second.State.Time = 2;
            first.State.Execution.Step(0, ref first); second.State.Execution.Step(0, ref second);
            Assert.That(first.State.HasStopped, Is.False); Assert.That(second.State.Explosions, Is.EqualTo(1));
            first.State.Time = 4.5f; first.State.Execution.Step(0, ref first);
            Assert.That(first.State.Explosions, Is.EqualTo(1));
        }
        [Test] public void VelocitySnapshotPrecedesGravityAndTheVelocityWritePrecedesLivePositionReads()
        {
            var host = Create(); var state = host.State;
            state.ReadingGravity = () =>
            {
                if (state.GravityReads == 1) { state.VelocityX = 99; state.VelocityY = 23; }
                else { state.VelocityY = 77; state.GravityValue = 8; }
            };
            state.VelocityWritten = () => { state.X = 40; state.Y = 50; };
            state.Execution.Step(.125f, ref host);
            AssertFloat(state.DestinationX, 40 + 8.25f * .125f + 0f * (.5f * 20 * .125f * .125f));
            AssertFloat(state.DestinationY, 50 + (-1.75f * .125f + -1f * (.5f * 20 * .125f * .125f)));
            AssertFloat(state.VelocityY, 23 - 8 * .125f); Assert.That(state.VelocityX, Is.EqualTo(99));
            Assert.That(state.Events, Is.EqualTo(new[] { "now", "now", "vx", "vy", "gravity", "vy", "gravity", "set-vy", "px", "py", "sweep", "stopped", "now" }));
            Assert.That(host.VelocityWrites, Is.EqualTo(1)); Assert.That(host.Sweeps, Is.EqualTo(1));
        }
        [Test] public void ContactExplosionUsesTheExistingStoppedClaimAndShortCircuitsPostflightReads()
        {
            var host = Create(lifetime: .125f); var state = host.State;
            state.Swept = () => { var contact = new Host { State = state }; contact.Explode(); };
            state.Execution.Step(1, ref host);
            Assert.That(state.Explosions, Is.EqualTo(1)); Assert.That(state.Removals, Is.Zero);
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo("stopped"));
            Assert.That(state.Events, Does.Not.Contain("expiry"));
        }
        [Test] public void ReleaseDuringSweepSuppressesExpiryWithoutAddingASecondTerminalFlag()
        {
            var host = Create(lifetime: .125f); var state = host.State;
            state.Swept = () => { var contact = new Host { State = state }; contact.Remove(); };
            state.Execution.Step(1, ref host);
            Assert.That(state.Removals, Is.EqualTo(1)); Assert.That(state.Explosions, Is.Zero);
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo("stopped"));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExpiryPolicyIsReadAfterTheNativeSweepRatherThanCapturedBeforeIt(bool explode)
        {
            var host = Create(lifetime: .125f, explode: !explode); var state = host.State;
            state.Swept = () => state.ExpiryExplodes = explode;
            state.Execution.Step(1, ref host);
            Assert.That(state.Explosions, Is.EqualTo(explode ? 1 : 0)); Assert.That(state.Removals, Is.Zero);
        }
        [TestCase(0f, 1)] [TestCase(-2f, 0)]
        public void PostflightComparisonReadsTheCurrentClockAfterSweepCallbacks(float changedTime, int explosions)
        {
            var host = Create(lifetime: .125f); var state = host.State;
            state.Swept = () => state.Time = changedTime;
            state.Execution.Step(.125f, ref host);
            Assert.That(state.Explosions, Is.EqualTo(explosions));
            var advanced = Create(lifetime: 1); var advancedState = advanced.State;
            advancedState.Swept = () => advancedState.Time = 1;
            advancedState.Execution.Step(.02f, ref advanced);
            Assert.That(advancedState.Explosions, Is.EqualTo(1));
        }
        [Test] public void ASweepExceptionKeepsTheEnteredVelocityWriteAndDoesNotInventAnExpiryClaim()
        {
            var host = Create(lifetime: .125f); var state = host.State;
            state.Swept = () => throw new InvalidOperationException("native sweep interrupted");
            Assert.Throws<InvalidOperationException>(() => state.Execution.Step(1, ref host));
            AssertFloat(state.VelocityY, -1.75f - 20 * .125f);
            Assert.That(state.HasStopped, Is.False); Assert.That(state.Explosions, Is.Zero);
            Assert.That(host.VelocityWrites, Is.EqualTo(1)); Assert.That(host.Sweeps, Is.EqualTo(1));
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo("sweep"));
        }
    }
}
