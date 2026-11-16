using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class TimedBombExecutionTests
    {
        const float Epsilon = .000001f;
        // Like the actual adapter, this host embeds the owner and never returns
        // it by value. Callbacks report landing to that same mutable field.
        sealed class Bomb : ITimedBombHost
        {
            TimedBombExecution execution;
            public float FlightLifetime { get; set; } = 5;
            public float FuseSeconds { get; set; } = 1.5f;
            public readonly List<float> Flights = new List<float>();
            public int Explosions;
            public Action<float> DuringFlight;
            public Action DuringExplosion;
            public TimedBombSnapshot Read() => execution.Read();
            public void Step(float dt) => execution.Step(dt, this);
            public void Land(float duration, float contactDistance, float length) => execution.Land(duration, contactDistance, length);
            public bool Stop() => execution.Stop();
            public bool Landed => execution.IsLanded(FuseSeconds);
            public float Remaining => execution.FuseRemaining(FuseSeconds);
            public void AdvanceFlight(float duration) { Flights.Add(duration); DuringFlight?.Invoke(duration); }
            public void Detonate() { Explosions++; DuringExplosion?.Invoke(); }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        static float After(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) + 1);
        [Test] public void FreshBombsHaveNoLandingAndSnapshotsDoNotChangeWithTheOwner()
        {
            var bomb = new Bomb(); var initial = bomb.Read();
            Assert.That(initial.Age, Is.Zero); Assert.That(initial.LandedAt, Is.EqualTo(-1));
            Assert.That(initial.Stopped, Is.False); Assert.That(initial.IsLanded(1.5f), Is.False);
            Assert.That(initial.Deadline(5, 1.5f), Is.EqualTo(5)); Assert.That(initial.FuseRemaining(1.5f), Is.Zero);
            bomb.Step(.01f); bomb.Land(0, 1, 1); bomb.Stop();
            Assert.That(initial.Age, Is.Zero); Assert.That(initial.LandedAt, Is.EqualTo(-1)); Assert.That(initial.Stopped, Is.False);
            Assert.That(bomb.Read().Stopped, Is.True); Assert.That(bomb.Read().LandedAt, Is.EqualTo(.01f));
            var fresh = new Bomb(); fresh.Step(.01f);
            Assert.That(fresh.Read().Age, Is.EqualTo(.01f)); Assert.That(fresh.Landed, Is.False); Assert.That(fresh.Read().Stopped, Is.False);
        }
        [TestCase(-1f)] [TestCase(0f)] [TestCase(.0000005f)] [TestCase(Epsilon)]
        public void NonadvancingCallsDoNotIntegrateOrEvenCheckAnAlreadyReachedDeadline(float dt)
        {
            var bomb = new Bomb { FlightLifetime = 0 };
            bomb.Step(dt);
            Assert.That(bomb.Flights, Is.Empty); Assert.That(bomb.Explosions, Is.Zero);
            Assert.That(bomb.Read().Age, Is.Zero); Assert.That(bomb.Read().Stopped, Is.False);
            bomb.Step(.000002f);
            Assert.That(bomb.Explosions, Is.EqualTo(1)); Assert.That(bomb.Flights, Is.Empty);
        }
        [Test] public void FlightIsScheduledInTwentyMillisecondSlicesWithTheOriginalRemainderOrder()
        {
            var bomb = new Bomb(); bomb.Step(.05f);
            Assert.That(bomb.Flights.Count, Is.EqualTo(3));
            Assert.That(Bits(bomb.Flights[0]), Is.EqualTo(Bits(.02f))); Assert.That(Bits(bomb.Flights[1]), Is.EqualTo(Bits(.02f)));
            Assert.That(Bits(bomb.Flights[2]), Is.EqualTo(Bits(.05f - .02f - .02f)));
            Assert.That(Bits(bomb.Read().Age), Is.EqualTo(Bits(.05f))); Assert.That(bomb.Explosions, Is.Zero);
        }
        [Test] public void SubmicrosecondTickRemaindersAreDiscardedNotCarriedByAnotherClock()
        {
            var bomb = new Bomb(); bomb.Step(.02f + Epsilon * .5f); bomb.Step(.02f + Epsilon * .5f); bomb.Step(Epsilon);
            Assert.That(bomb.Flights, Is.EqualTo(new[] { .02f, .02f }));
            Assert.That(Bits(bomb.Read().Age), Is.EqualTo(Bits(.04f))); Assert.That(bomb.Landed, Is.False);
        }
        [Test] public void FlightDeadlineClampsTheLastSliceRatherThanIntegratingTheWholeTick()
        {
            var bomb = new Bomb { FlightLifetime = .05f }; bomb.Step(.2f);
            Assert.That(bomb.Flights.Count, Is.EqualTo(3));
            Assert.That(bomb.Flights[0], Is.EqualTo(.02f)); Assert.That(bomb.Flights[1], Is.EqualTo(.02f));
            Assert.That(Bits(bomb.Flights[2]), Is.EqualTo(Bits(.05f - .04f)));
            Assert.That(Bits(bomb.Read().Age), Is.EqualTo(Bits(.05f))); Assert.That(bomb.Explosions, Is.EqualTo(1));
            Assert.That(bomb.Read().Stopped, Is.True); Assert.That(bomb.Landed, Is.False); Assert.That(bomb.Remaining, Is.Zero);
        }
        [Test] public void AdjacentFloatsOutsideAndInsideFlightToleranceKeepTheirDeadlineSemantics()
        {
            float edge = .01f - Epsilon;
            var before = new Bomb { FlightLifetime = .01f }; before.Step(Before(edge));
            Assert.That(before.Explosions, Is.Zero); Assert.That(before.Read().Stopped, Is.False);
            Assert.That(Bits(before.Read().Age), Is.EqualTo(Bits(Before(edge))));
            var after = new Bomb { FlightLifetime = .01f }; after.Step(After(edge));
            Assert.That(after.Explosions, Is.EqualTo(1)); Assert.That(Bits(after.Read().Age), Is.EqualTo(Bits(After(edge))));
            Assert.That(after.Read().Age, Is.LessThan(.01f));
        }
        [Test] public void DeadlineAlreadyInsideToleranceExplodesBeforeAnyFlightIntegration()
        {
            var bomb = new Bomb { FlightLifetime = Epsilon }; bomb.Step(.02f);
            Assert.That(bomb.Explosions, Is.EqualTo(1)); Assert.That(bomb.Flights, Is.Empty); Assert.That(bomb.Read().Age, Is.Zero);
        }
        [Test] public void FiveSecondUnlandedTimeoutExplodesOnceWithoutReadingAnExternalClock()
        {
            var bomb = new Bomb(); bomb.Step(4.99f);
            Assert.That(bomb.Landed, Is.False); Assert.That(bomb.Explosions, Is.Zero);
            bomb.Step(.02f);
            Assert.That(bomb.Explosions, Is.EqualTo(1)); Assert.That(Bits(bomb.Read().Age), Is.EqualTo(Bits(5)));
            foreach (float duration in bomb.Flights) Assert.That(duration, Is.InRange(0f, .02f));
            int count = bomb.Flights.Count; bomb.Step(10);
            Assert.That(bomb.Flights.Count, Is.EqualTo(count)); Assert.That(bomb.Explosions, Is.EqualTo(1));
        }
        [Test] public void FractionalLandingUsesTheContactTimeButTheEnteredSubstepFinishesItsAge()
        {
            var bomb = new Bomb(); bomb.DuringFlight = duration => bomb.Land(duration, 1, 4);
            bomb.Step(.05f); var landed = bomb.Read();
            Assert.That(bomb.Flights, Is.EqualTo(new[] { .02f }));
            Assert.That(Bits(landed.LandedAt), Is.EqualTo(Bits(.005f))); Assert.That(Bits(landed.Age), Is.EqualTo(Bits(.05f)));
            Assert.That(Bits(landed.Deadline(5, 1.5f)), Is.EqualTo(Bits(1.505f)));
            Assert.That(Bits(bomb.Remaining), Is.EqualTo(Bits(1.505f - .05f)));
            Assert.That(Bits(landed.FuseRemaining(1.5f)), Is.EqualTo(Bits(bomb.Remaining))); Assert.That(bomb.Explosions, Is.Zero);
        }
        [TestCase(-2f, .01f)] [TestCase(0f, .01f)] [TestCase(.5f, .015f)] [TestCase(2f, .03f)] [TestCase(3f, .03f)]
        public void LandingFractionIsClampedBeforeMultiplicationAndAddition(float distance, float expectedLanding)
        {
            var bomb = new Bomb(); bomb.Step(.01f); bomb.Land(.02f, distance, 2);
            Assert.That(Bits(bomb.Read().LandedAt), Is.EqualTo(Bits(expectedLanding)));
            Assert.That(bomb.Read().Age, Is.EqualTo(.01f)); Assert.That(bomb.Landed, Is.True);
        }
        [Test] public void ExternalZeroDurationSweepArmsAtCurrentAgeAndDoesNotAdvanceTime()
        {
            var bomb = new Bomb(); bomb.Step(.01f); bomb.Land(0, .25f, 1);
            Assert.That(Bits(bomb.Read().LandedAt), Is.EqualTo(Bits(.01f))); Assert.That(bomb.Remaining, Is.EqualTo(1.5f));
            int flightCount = bomb.Flights.Count; bomb.Step(.1f);
            Assert.That(bomb.Flights.Count, Is.EqualTo(flightCount)); Assert.That(bomb.Read().Age, Is.GreaterThan(.1f));
            Assert.That(bomb.Explosions, Is.Zero);
        }
        [Test] public void LandingOnTheLastFlightSliceGetsTheWholeFuseBeyondTheFlightDeadline()
        {
            var bomb = new Bomb { FlightLifetime = .03f };
            bomb.DuringFlight = duration => { if (bomb.Flights.Count == 2) bomb.Land(duration, 1, 1); };
            bomb.Step(.03f); var landing = bomb.Read();
            Assert.That(bomb.Flights.Count, Is.EqualTo(2)); Assert.That(Bits(landing.LandedAt), Is.EqualTo(Bits(.03f)));
            Assert.That(bomb.Explosions, Is.Zero); Assert.That(Bits(bomb.Remaining), Is.EqualTo(Bits(1.53f - .03f)));
            bomb.Step(1.49f); Assert.That(bomb.Explosions, Is.Zero); Assert.That(bomb.Read().Age, Is.GreaterThan(bomb.FlightLifetime));
            bomb.Step(.02f); Assert.That(bomb.Explosions, Is.EqualTo(1)); Assert.That(bomb.Flights.Count, Is.EqualTo(2));
        }
        [Test] public void ALongTickCanLandWaitAndExplodeWithoutASecondNativeFlight()
        {
            var bomb = new Bomb { FlightLifetime = .02f }; bomb.DuringFlight = duration => bomb.Land(duration, 1, 2);
            bomb.Step(2);
            Assert.That(bomb.Flights, Is.EqualTo(new[] { .02f })); Assert.That(bomb.Read().LandedAt, Is.EqualTo(.01f));
            Assert.That(bomb.Read().Age, Is.InRange(1.51f - Epsilon, 1.51f)); Assert.That(bomb.Remaining, Is.InRange(0f, Epsilon));
            Assert.That(bomb.Explosions, Is.EqualTo(1));
        }
        [Test] public void AdjacentFloatsOutsideAndInsideFuseToleranceUseTheLandingDeadline()
        {
            var before = new Bomb { FlightLifetime = 0, FuseSeconds = .01f }; before.Land(0, 0, 1);
            float edge = .01f - Epsilon; before.Step(Before(edge)); Assert.That(before.Explosions, Is.Zero);
            var after = new Bomb { FlightLifetime = 0, FuseSeconds = .01f }; after.Land(0, 0, 1); after.Step(After(edge));
            Assert.That(after.Explosions, Is.EqualTo(1)); Assert.That(Bits(after.Read().Age), Is.EqualTo(Bits(After(edge))));
            Assert.That(after.Read().LandedAt, Is.Zero); Assert.That(after.Read().Age, Is.LessThan(.01f));
            Assert.That(after.Remaining, Is.GreaterThan(0), "The legacy tolerance may stop just short of exact zero remaining.");
        }
        [Test] public void LiveDeadlineChangesDuringNativeFlightApplyToThePostflightComparison()
        {
            var bomb = new Bomb(); bomb.DuringFlight = duration => bomb.FlightLifetime = .01f;
            bomb.Step(.1f);
            Assert.That(bomb.Flights, Is.EqualTo(new[] { .02f })); Assert.That(bomb.Read().Age, Is.EqualTo(.02f));
            Assert.That(bomb.Explosions, Is.EqualTo(1));
        }
        [Test] public void LiveFuseChangesUseTheRecordedLandingAndNeverRestartItsClock()
        {
            var bomb = new Bomb(); bomb.Land(0, 0, 1); bomb.Step(.1f); var landing = bomb.Read();
            bomb.FuseSeconds = .5f;
            Assert.That(Bits(bomb.Remaining), Is.EqualTo(Bits(.5f - landing.Age))); Assert.That(bomb.Landed, Is.True);
            bomb.Step(.2f); Assert.That(bomb.Explosions, Is.Zero);
            bomb.FuseSeconds = .2f; bomb.Step(.02f);
            Assert.That(bomb.Explosions, Is.EqualTo(1)); Assert.That(bomb.Remaining, Is.Zero); Assert.That(bomb.Flights, Is.Empty);
            Assert.That(bomb.Read().LandedAt, Is.Zero);
        }
        [Test] public void DisablingThenRestoringTheFuseChangesQueriesWithoutErasingLandingHistory()
        {
            var bomb = new Bomb(); bomb.Step(.01f); bomb.Land(0, 1, 1); var landing = bomb.Read();
            bomb.FuseSeconds = 0;
            Assert.That(bomb.Landed, Is.False); Assert.That(bomb.Remaining, Is.Zero);
            Assert.That(landing.IsLanded(0), Is.False); Assert.That(landing.FuseRemaining(0), Is.Zero);
            Assert.That(landing.Deadline(5, 0), Is.EqualTo(5));
            bomb.FuseSeconds = 1.5f;
            Assert.That(bomb.Landed, Is.True); Assert.That(bomb.Remaining, Is.EqualTo(1.5f));
            Assert.That(bomb.Read().LandedAt, Is.EqualTo(.01f));
        }
        [Test] public void SeparateBombsKeepIndependentLandingAndWaitTimes()
        {
            var first = new Bomb(); var second = new Bomb(); first.Land(0, 0, 1); first.Step(.5f);
            second.Step(.02f); second.Land(0, 1, 1); var before = second.Read(); float remaining = second.Remaining;
            first.Step(1.1f);
            Assert.That(first.Explosions, Is.EqualTo(1)); Assert.That(second.Explosions, Is.Zero);
            Assert.That(Bits(second.Read().Age), Is.EqualTo(Bits(before.Age)));
            Assert.That(Bits(second.Remaining), Is.EqualTo(Bits(remaining))); Assert.That(second.Remaining, Is.EqualTo(1.5f));
            second.Step(1.51f); Assert.That(second.Explosions, Is.EqualTo(1));
        }
        [Test] public void StopClosesFutureFlightLandingAndExplosionButRetainsTheLastObservation()
        {
            var bomb = new Bomb(); bomb.Step(.01f); bomb.Land(0, 1, 1); var before = bomb.Read(); float remaining = bomb.Remaining;
            Assert.That(bomb.Stop(), Is.True); Assert.That(bomb.Stop(), Is.False);
            bomb.Land(10, 1, 1); bomb.Step(10);
            Assert.That(bomb.Read().Stopped, Is.True); Assert.That(bomb.Read().Age, Is.EqualTo(before.Age));
            Assert.That(bomb.Read().LandedAt, Is.EqualTo(before.LandedAt)); Assert.That(bomb.Remaining, Is.EqualTo(remaining));
            Assert.That(bomb.Landed, Is.True); Assert.That(bomb.Explosions, Is.Zero); Assert.That(bomb.Flights.Count, Is.EqualTo(1));
        }
        [Test] public void NativeStopDuringFlightStillFinishesTheEnteredSubstepBeforeReturning()
        {
            var bomb = new Bomb(); bomb.DuringFlight = duration => { bomb.Land(duration, 1, 4); bomb.Stop(); };
            bomb.Step(.2f);
            Assert.That(bomb.Read().Stopped, Is.True); Assert.That(bomb.Read().Age, Is.EqualTo(.02f));
            Assert.That(bomb.Read().LandedAt, Is.EqualTo(.005f)); Assert.That(bomb.Flights, Is.EqualTo(new[] { .02f }));
            Assert.That(bomb.Explosions, Is.Zero); Assert.That(Bits(bomb.Remaining), Is.EqualTo(Bits(1.505f - .02f)));
        }
        [Test] public void NativeStopAtTheDeadlineCannotEmitAnExplosionAfterTheStopWasClaimed()
        {
            var bomb = new Bomb { FlightLifetime = .02f }; bomb.DuringFlight = duration => bomb.Stop(); bomb.Step(.2f);
            Assert.That(bomb.Read().Age, Is.EqualTo(.02f)); Assert.That(bomb.Explosions, Is.Zero);
            Assert.That(bomb.Read().Stopped, Is.True); Assert.That(bomb.Flights.Count, Is.EqualTo(1));
        }
        [Test] public void ExplosionClaimsTerminationBeforeSynchronousReentry()
        {
            var bomb = new Bomb { FlightLifetime = .01f }; bool observedStopped = false;
            bomb.DuringExplosion = () => { observedStopped = bomb.Read().Stopped; bomb.Step(1); bomb.Land(1, 1, 1); };
            bomb.Step(1);
            Assert.That(observedStopped, Is.True); Assert.That(bomb.Explosions, Is.EqualTo(1));
            Assert.That(bomb.Flights, Is.EqualTo(new[] { .01f })); Assert.That(bomb.Read().LandedAt, Is.EqualTo(-1));
            Assert.That(bomb.Read().Age, Is.EqualTo(.01f));
        }
        [Test] public void NativeReentryAccumulatesOnTheSameEmbeddedOwnerWithoutACopiedState()
        {
            var bomb = new Bomb(); bool nested = false;
            bomb.DuringFlight = duration => {
                if (nested) return; nested = true; bomb.Step(.01f); bomb.Land(0, 1, 1);
            };
            bomb.Step(.02f);
            Assert.That(bomb.Flights, Is.EqualTo(new[] { .02f, .01f }));
            Assert.That(Bits(bomb.Read().Age), Is.EqualTo(Bits(.03f))); Assert.That(bomb.Read().LandedAt, Is.EqualTo(.01f));
            Assert.That(Bits(bomb.Remaining), Is.EqualTo(Bits(1.51f - .03f))); Assert.That(bomb.Explosions, Is.Zero);
        }
        [Test] public void NativeFlightFailureDoesNotInventElapsedTimeOrTermination()
        {
            var bomb = new Bomb(); bomb.DuringFlight = duration => throw new InvalidOperationException("native flight failed");
            Assert.Throws<InvalidOperationException>(() => bomb.Step(.1f));
            Assert.That(bomb.Read().Age, Is.Zero); Assert.That(bomb.Read().Stopped, Is.False); Assert.That(bomb.Explosions, Is.Zero);
            bomb.DuringFlight = null; bomb.Step(.01f);
            Assert.That(bomb.Read().Age, Is.EqualTo(.01f)); Assert.That(bomb.Read().Stopped, Is.False);
        }
        [Test] public void NativeExplosionFailureRetainsItsClaimAndCannotRetryTheExplosion()
        {
            var bomb = new Bomb { FlightLifetime = .01f }; bomb.DuringExplosion = () => throw new InvalidOperationException("native explosion failed");
            Assert.Throws<InvalidOperationException>(() => bomb.Step(.1f));
            Assert.That(bomb.Read().Stopped, Is.True); Assert.That(bomb.Read().Age, Is.EqualTo(.01f));
            bomb.DuringExplosion = null; bomb.Step(.1f);
            Assert.That(bomb.Explosions, Is.EqualTo(1)); Assert.That(bomb.Flights.Count, Is.EqualTo(1));
        }
    }
}
