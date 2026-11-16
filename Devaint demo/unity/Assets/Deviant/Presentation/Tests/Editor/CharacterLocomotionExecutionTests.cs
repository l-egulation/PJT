using System;
using Deviant.Presentation.Contracts;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class CharacterLocomotionExecutionTests
    {
        struct Durations : ICharacterLocomotionDurations
        {
            public float Landing, JumpStart, FallStart;
            public int LandingReads, JumpReads, FallReads;
            public bool FailLanding, FailJump, FailFall;
            public float LandingDuration { get { LandingReads++; if (FailLanding) throw new InvalidOperationException("land"); return Landing; } }
            public float JumpStartDuration { get { JumpReads++; if (FailJump) throw new InvalidOperationException("jump_start"); return JumpStart; } }
            public float FallStartDuration { get { FallReads++; if (FailFall) throw new InvalidOperationException("fall_start"); return FallStart; } }
        }
        static string Draw(ref CharacterLocomotionExecution owner, ref Durations durations, float now, float dt = 0,
            bool grounded = true, float vy = 0, int axis = 0, string form = "base_sword", bool dead = false,
            bool dashing = false, bool approaching = false, bool attacking = false, bool basic = false, int basicIndex = -1)
        {
            var input = new CharacterLocomotionInput(form, now, dt, dead, dashing, approaching, attacking, basic, basicIndex, grounded, vy, axis);
            return owner.Advance(in input, ref durations);
        }
        [Test] public void DefaultOwnerStartsGroundedAndEntryDiscardsDeltaWhileSameStateAccumulatesIt()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations(); var empty = owner.Read();
            Assert.That(empty.State, Is.Null); Assert.That(empty.Form, Is.Null); Assert.That(empty.PreviousGround, Is.True);
            Assert.That(empty.Elapsed, Is.Zero); Assert.That(empty.LastVelocity, Is.Zero); Assert.That(empty.AirStart, Is.Zero); Assert.That(empty.LandingUntil, Is.Zero);
            Assert.That(Draw(ref owner, ref durations, 10, .5f), Is.EqualTo("idle")); Assert.That(owner.Elapsed, Is.Zero);
            Assert.That(Draw(ref owner, ref durations, 10, .125f), Is.EqualTo("idle")); Assert.That(owner.Elapsed, Is.EqualTo(.125f));
            Assert.That(Draw(ref owner, ref durations, 10, .375f), Is.EqualTo("idle")); Assert.That(owner.Elapsed, Is.EqualTo(.5f));
            var first = owner.Read(); var second = owner.Read();
            Assert.That(first.Elapsed, Is.EqualTo(.5f)); Assert.That(second.Elapsed, Is.EqualTo(.5f)); Assert.That(second.Form, Is.EqualTo("base_sword"));
            Assert.That(Draw(ref owner, ref durations, 10, .5f, axis: -1), Is.EqualTo("run")); Assert.That(owner.Elapsed, Is.Zero);
            Assert.That(Draw(ref owner, ref durations, 10, .25f, axis: 1), Is.EqualTo("run")); Assert.That(owner.Elapsed, Is.EqualTo(.25f));
            Assert.That(Draw(ref owner, ref durations, 10, .5f), Is.EqualTo("idle")); Assert.That(owner.Elapsed, Is.Zero);
        }
        [Test] public void ChangingTheDisplayedFormRestartsTheSameStateWithoutResettingAirOrLandingFacts()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { Landing = .5f };
            Draw(ref owner, ref durations, 4, grounded: false, vy: -5);
            Assert.That(Draw(ref owner, ref durations, 4.25f), Is.EqualTo("land"));
            Draw(ref owner, ref durations, 4.25f, .125f); Assert.That(owner.Elapsed, Is.EqualTo(.125f));
            Assert.That(Draw(ref owner, ref durations, 4.25f, .5f, form: "rapier"), Is.EqualTo("land"));
            var changed = owner.Read(); Assert.That(changed.Form, Is.EqualTo("rapier")); Assert.That(changed.Elapsed, Is.Zero);
            Assert.That(changed.AirStart, Is.EqualTo(4)); Assert.That(changed.LandingUntil, Is.EqualTo(4.75f)); Assert.That(durations.LandingReads, Is.EqualTo(1));
            Draw(ref owner, ref durations, 4.25f, .0625f, form: "rapier"); Assert.That(owner.Elapsed, Is.EqualTo(.0625f));
        }
        [Test] public void DoubleJumpUsesStrictVelocityIncreaseAndRestartsTheSameJumpStart()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { JumpStart = 2 };
            Draw(ref owner, ref durations, 9);
            Assert.That(Draw(ref owner, ref durations, 10, .5f, grounded: false, vy: 8), Is.EqualTo("jump_start"));
            Assert.That(owner.Read().AirStart, Is.EqualTo(10)); Assert.That(owner.Elapsed, Is.Zero);
            Draw(ref owner, ref durations, 10.5f, .125f, grounded: false, vy: 6); Assert.That(owner.Elapsed, Is.EqualTo(.125f));
            Draw(ref owner, ref durations, 11, .125f, grounded: false, vy: 7);
            Assert.That(owner.Elapsed, Is.EqualTo(.25f)); Assert.That(owner.Read().AirStart, Is.EqualTo(10), "Exactly one is not a new jump.");
            Draw(ref owner, ref durations, 11, .5f, grounded: false, vy: 8.0625f);
            Assert.That(owner.Elapsed, Is.Zero); Assert.That(owner.Read().AirStart, Is.EqualTo(11));
            Assert.That(Draw(ref owner, ref durations, 13, .5f, grounded: false, vy: 3), Is.EqualTo("jump_rise")); Assert.That(owner.Elapsed, Is.Zero);
        }
        [Test] public void RiseApexAndFallUseTheExistingInclusiveTwoUnitBoundaries()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations();
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: 3), Is.EqualTo("jump_rise"));
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: 2), Is.EqualTo("jump_apex"));
            Assert.That(Draw(ref owner, ref durations, 1, .0625f, grounded: false, vy: 0), Is.EqualTo("jump_apex"));
            Assert.That(Draw(ref owner, ref durations, 1, .0625f, grounded: false, vy: -2), Is.EqualTo("jump_apex")); Assert.That(owner.Elapsed, Is.EqualTo(.125f));
            Assert.That(Draw(ref owner, ref durations, 1, .5f, grounded: false, vy: -2.0625f), Is.EqualTo("fall_start")); Assert.That(owner.Elapsed, Is.Zero);
        }
        [Test] public void FallingSelectsFromPreviousElapsedBeforeAddingThisDrawDelta()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { FallStart = .25f };
            Assert.That(Draw(ref owner, ref durations, 1, .5f, grounded: false, vy: -5), Is.EqualTo("fall_start"));
            Assert.That(owner.Read().AirStart, Is.EqualTo(1)); Assert.That(owner.Elapsed, Is.Zero); Assert.That(durations.FallReads, Is.Zero);
            Assert.That(Draw(ref owner, ref durations, 1, .25f, grounded: false, vy: -5), Is.EqualTo("fall_start")); Assert.That(owner.Elapsed, Is.EqualTo(.25f));
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: -5), Is.EqualTo("fall_loop")); Assert.That(owner.Elapsed, Is.Zero);
            durations.FailFall = true;
            Assert.That(Draw(ref owner, ref durations, 1, .125f, grounded: false, vy: -5), Is.EqualTo("fall_loop")); Assert.That(owner.Elapsed, Is.EqualTo(.125f));
            Assert.That(durations.FallReads, Is.EqualTo(2), "An established fall loop never reads the entry duration again.");
        }
        [Test] public void LandingUsesWallClockOnceButRunningWinsAndDrawTimeDoesNotExpireItsWindow()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { Landing = .5f };
            Draw(ref owner, ref durations, 2, grounded: false, vy: -5);
            Assert.That(Draw(ref owner, ref durations, 2.25f, 1, axis: 1), Is.EqualTo("run")); Assert.That(owner.Read().LandingUntil, Is.EqualTo(2.75f));
            durations.Landing = 10;
            Assert.That(Draw(ref owner, ref durations, 2.5f, 1), Is.EqualTo("land")); Assert.That(owner.Elapsed, Is.Zero);
            Assert.That(Draw(ref owner, ref durations, 2.5f, 3), Is.EqualTo("land")); Assert.That(owner.Elapsed, Is.EqualTo(3));
            Assert.That(owner.Read().LandingUntil, Is.EqualTo(2.75f)); Assert.That(durations.LandingReads, Is.EqualTo(1));
            Assert.That(Draw(ref owner, ref durations, 2.75f, 1), Is.EqualTo("idle")); Assert.That(owner.Elapsed, Is.Zero);
        }
        [Test] public void DeathDashApproachAndAttackOverrideAirAndGroundWithoutOwningTheirState()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { Landing = .5f };
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: -5, axis: 1, dead: true, dashing: true, approaching: true, attacking: true, basic: true, basicIndex: 2), Is.EqualTo("death"));
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: -5, axis: 1, dashing: true, approaching: true, attacking: true, basic: true, basicIndex: 2), Is.EqualTo("dash"));
            Assert.That(Draw(ref owner, ref durations, 1, .125f, grounded: false, vy: -5, axis: 1, approaching: true, attacking: true, basic: true, basicIndex: 2), Is.EqualTo("dash")); Assert.That(owner.Elapsed, Is.EqualTo(.125f));
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: -5, axis: 1, attacking: true, basic: true, basicIndex: 2), Is.EqualTo("attack3"));
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: -5, axis: 1, attacking: true, basicIndex: 3), Is.EqualTo("attack1"));
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: -5, axis: 1, attacking: true, basic: true, basicIndex: 3), Is.EqualTo("attack4"));
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: -5, axis: 1), Is.EqualTo("fall_start"));
            Assert.That(Draw(ref owner, ref durations, 1, axis: -1), Is.EqualTo("run")); Assert.That(Draw(ref owner, ref durations, 1), Is.EqualTo("land"));
        }
        [Test] public void JumpDetectionAndGroundTransitionsStillAdvanceBehindHigherPriorityAttackOrDeath()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { Landing = .5f, FailJump = true, FailFall = true };
            Draw(ref owner, ref durations, 1, .5f, grounded: false, vy: 8, attacking: true, basic: true, basicIndex: 0);
            Draw(ref owner, ref durations, 1.125f, .125f, grounded: false, vy: 6, attacking: true, basic: true, basicIndex: 0); Assert.That(owner.Elapsed, Is.EqualTo(.125f));
            Assert.That(Draw(ref owner, ref durations, 1.25f, .5f, grounded: false, vy: 8, attacking: true, basic: true, basicIndex: 0), Is.EqualTo("attack1"));
            Assert.That(owner.Elapsed, Is.Zero); Assert.That(owner.Read().AirStart, Is.EqualTo(1.25f));
            Assert.That(Draw(ref owner, ref durations, 1.5f, dead: true, attacking: true), Is.EqualTo("death"));
            Assert.That(owner.Read().PreviousGround, Is.True); Assert.That(owner.Read().LastVelocity, Is.Zero); Assert.That(owner.Read().LandingUntil, Is.EqualTo(2));
            Assert.That(durations.JumpReads, Is.Zero); Assert.That(durations.FallReads, Is.Zero); Assert.That(durations.LandingReads, Is.EqualTo(1));
        }
        [Test] public void LiveJumpAndFallDurationsAreNotCompiledOrLatchedByTheOwner()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { JumpStart = 1, FallStart = 1 };
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: 8), Is.EqualTo("jump_start"));
            durations.JumpStart = .25f;
            Assert.That(Draw(ref owner, ref durations, 1.5f, grounded: false, vy: 6), Is.EqualTo("jump_rise"));
            Assert.That(Draw(ref owner, ref durations, 2, grounded: false, vy: -5), Is.EqualTo("fall_start"));
            Draw(ref owner, ref durations, 2, .25f, grounded: false, vy: -5); Assert.That(owner.Elapsed, Is.EqualTo(.25f));
            durations.FallStart = .25f;
            Assert.That(Draw(ref owner, ref durations, 2, grounded: false, vy: -5), Is.EqualTo("fall_loop"));
            Assert.That(durations.JumpReads, Is.EqualTo(2)); Assert.That(durations.FallReads, Is.EqualTo(2));
        }
        [Test] public void UnrelatedMissingDurationBindingsAreNeverRead()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { FailLanding = true, FailJump = true, FailFall = true };
            Draw(ref owner, ref durations, 1); Draw(ref owner, ref durations, 1, axis: 1);
            Draw(ref owner, ref durations, 1, attacking: true); Draw(ref owner, ref durations, 1, dashing: true); Draw(ref owner, ref durations, 1, dead: true);
            Assert.That(Draw(ref owner, ref durations, 1, grounded: false, vy: -5), Is.EqualTo("fall_start"));
            Assert.That(durations.LandingReads, Is.Zero); Assert.That(durations.JumpReads, Is.Zero); Assert.That(durations.FallReads, Is.Zero);
        }
        [Test] public void ResetClearsSelectionVelocityAndLandingButIntentionallyKeepsAirStart()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { JumpStart = 1, Landing = 1 };
            Draw(ref owner, ref durations, 8, grounded: false, vy: 8); Draw(ref owner, ref durations, 8.125f, .125f, grounded: false, vy: 6);
            Draw(ref owner, ref durations, 9); Draw(ref owner, ref durations, 9, .125f);
            Assert.That(owner.Read().LandingUntil, Is.EqualTo(10)); Assert.That(owner.Elapsed, Is.EqualTo(.125f));
            owner.Reset(); var reset = owner.Read();
            Assert.That(reset.State, Is.Null); Assert.That(reset.Form, Is.Null); Assert.That(reset.Elapsed, Is.Zero); Assert.That(reset.LastVelocity, Is.Zero);
            Assert.That(reset.LandingUntil, Is.Zero); Assert.That(reset.PreviousGround, Is.True); Assert.That(reset.AirStart, Is.EqualTo(8));
            Assert.That(Draw(ref owner, ref durations, 1, .5f), Is.EqualTo("idle")); Assert.That(owner.Elapsed, Is.Zero); Assert.That(durations.LandingReads, Is.EqualTo(1));
            Assert.That(Draw(ref owner, ref durations, 2, grounded: false, vy: -5), Is.EqualTo("fall_start")); Assert.That(owner.Read().AirStart, Is.EqualTo(2));
        }
        [Test] public void DurationFailureRetainsOnlyTheFactsAlreadyChangedBeforeTheLookup()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { JumpStart = 1, FailJump = true };
            Draw(ref owner, ref durations, 9); Draw(ref owner, ref durations, 9, .125f);
            var input = new CharacterLocomotionInput("rapier", 10, .5f, false, false, false, false, false, -1, false, 8, 0);
            try { owner.Advance(in input, ref durations); Assert.Fail("The current binding failure must escape."); }
            catch (InvalidOperationException exception) { Assert.That(exception.Message, Is.EqualTo("jump_start")); }
            var failed = owner.Read();
            Assert.That(failed.Form, Is.EqualTo("rapier")); Assert.That(failed.State, Is.Null); Assert.That(failed.AirStart, Is.EqualTo(10));
            Assert.That(failed.Elapsed, Is.EqualTo(.125f)); Assert.That(failed.PreviousGround, Is.True); Assert.That(failed.LastVelocity, Is.Zero);
            durations.FailJump = false;
            Assert.That(Draw(ref owner, ref durations, 10, .5f, grounded: false, vy: 8, form: "rapier"), Is.EqualTo("jump_start")); Assert.That(owner.Elapsed, Is.Zero);
        }
        [Test] public void FailedLandingLookupDoesNotCommitGroundVelocityOrADeadline()
        {
            var owner = new CharacterLocomotionExecution(); var durations = new Durations { Landing = .5f, FailLanding = true };
            Draw(ref owner, ref durations, 1, grounded: false, vy: -5);
            var input = new CharacterLocomotionInput("rapier", 2, .5f, false, false, false, false, false, -1, true, 0, 0);
            try { owner.Advance(in input, ref durations); Assert.Fail("The current binding failure must escape."); }
            catch (InvalidOperationException exception) { Assert.That(exception.Message, Is.EqualTo("land")); }
            var failed = owner.Read();
            Assert.That(failed.Form, Is.EqualTo("rapier")); Assert.That(failed.State, Is.Null); Assert.That(failed.PreviousGround, Is.False);
            Assert.That(failed.LastVelocity, Is.EqualTo(-5)); Assert.That(failed.AirStart, Is.EqualTo(1)); Assert.That(failed.LandingUntil, Is.Zero);
            durations.FailLanding = false;
            Assert.That(Draw(ref owner, ref durations, 2, .5f, form: "rapier"), Is.EqualTo("land")); Assert.That(owner.Read().LandingUntil, Is.EqualTo(2.5f));
        }
    }
}
