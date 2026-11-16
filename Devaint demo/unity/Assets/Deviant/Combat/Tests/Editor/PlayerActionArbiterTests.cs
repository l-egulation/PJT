using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class PlayerActionArbiterTests
    {
        sealed class Host : IPlayerActionHost, ITargetedPlayerActionHost
        {
            public readonly List<string> Calls = new List<string>();
            public int ActiveSlot { get; set; }
            public int UnavailableSlot = -1;
            public CombatCommandReason CheckAttack(int slot, bool special) => slot == UnavailableSlot ? CombatCommandReason.Unavailable : special && !HasSpecialCharge ? CombatCommandReason.Cooldown : CombatCommandReason.None;
            public bool ActionActivated { get; set; }
            public bool HitCancelReady { get; set; }
            public WeaponSwitchStart LastSwitch;
            public CombatActionAttempt StartAttack(int slot, bool special, CombatCommandKey command, float? startAt, bool comboSuccessor, WeaponSwitchStart weaponSwitch)
            {
                var reason = CheckAttack(slot, special);
                if (reason != CombatCommandReason.None) return CombatActionAttempt.Reject(reason);
                ActiveSlot = slot; LastSwitch = weaponSwitch; StartedAt = startAt;
                Calls.Add($"slot:{slot}:{(special ? "special" : comboSuccessor ? "next" : "basic")}{(weaponSwitch == WeaponSwitchStart.None ? "" : ":" + weaponSwitch)}");
                if (special) { HasAction = true; Basic = false; }
                ActionActivated = false; HitCancelReady = false;
                return Attempt(true);
            }
            public bool HasAction { get; set; }
            public bool Basic;
            public bool IsBasicAction => HasAction && Basic;
            public bool Dashing { get; set; }
            public bool MissileFlying { get; set; }
            public bool CanRequestSpecial { get; set; } = true;
            public bool HasSpecialCharge { get; set; } = true;
            public bool DashSucceeds, JumpSucceeds, RecastSucceeds, DetonationEndsFlight, DropSucceeds = true;
            public float? StartedAt;
            public readonly List<(CombatCommandKey key, CombatCommandState state, CombatCommandReason reason)> Outcomes = new List<(CombatCommandKey, CombatCommandState, CombatCommandReason)>();
            public CombatCommandReason SpecialUnavailableReason => CombatCommandReason.Cooldown;
            public CombatActionAttempt DropThrough(CombatCommandKey command) { Calls.Add("drop"); if (DropSucceeds) { HasAction = false; return CombatActionAttempt.Start(Guid.NewGuid(), immediate: true); } return CombatActionAttempt.Reject(CombatCommandReason.NoPlatform); }
            public CombatActionAttempt TryDash(CombatCommandKey command) { Calls.Add("dash"); return Attempt(DashSucceeds); }
            public CombatActionAttempt TryJump(CombatCommandKey command) { Calls.Add("jump"); return Attempt(JumpSucceeds); }
            public CombatActionAttempt TryApproachRecast(CombatCommandKey command) { Calls.Add("recast"); return Attempt(RecastSucceeds); }
            public CombatActionAttempt RequestMissileDetonation(CombatCommandKey command) { Calls.Add("detonate"); if (DetonationEndsFlight) { MissileFlying = false; HasAction = false; } return Attempt(DetonationEndsFlight); }
            public CombatActionAttempt StartBasic(CombatCommandKey command) { Calls.Add("basic"); return Attempt(true); }
            public CombatActionAttempt StartNextBasic(CombatCommandKey command, float completedAt) { Calls.Add("next-basic"); StartedAt = completedAt; return Attempt(true); }
            public CombatActionAttempt StartSpecial(CombatCommandKey command, float? startAt) { Calls.Add("special"); HasAction = true; Basic = false; StartedAt = startAt; return Attempt(true); }
            public void OpenComboWindow(float completedAt) { Calls.Add("combo-window"); StartedAt = completedAt; }
            public CombatActionAttempt SwitchWeapon(CombatCommandKey command) { Calls.Add("swap"); return Attempt(true); }
            static CombatActionAttempt Attempt(bool started) => started ? CombatActionAttempt.Start(Guid.NewGuid()) : CombatActionAttempt.Reject(CombatCommandReason.Busy);
            public void RecordOutcome(CombatCommandKey command, CombatCommandState state, CombatCommandReason reason, Guid executionId = default, string detail = null)
            { if (command.IsValid) Outcomes.Add((command, state, reason)); }
        }
        [Test] public void TargetedBasicStartsRequestedSlotOnlyOnceForSimultaneousKeys()
        {
            var h = new Host(); var a = new PlayerActionArbiter(h);
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon1Basic | CombatButtons.Weapon2Basic));
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:0:basic" }));
            h.Calls.Clear(); a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon2Basic));
            Assert.That(h.ActiveSlot, Is.EqualTo(1)); Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:basic" }));
        }
        [Test] public void UnavailableTargetDoesNotChangeSelectionOrReplaceExistingReservation()
        {
            var h = new Host { HasAction = true, Basic = true, UnavailableSlot = 1 }; var a = new PlayerActionArbiter(h);
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon1Basic));
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon2Special));
            Assert.That(h.ActiveSlot, Is.Zero); Assert.That(h.Calls, Is.Empty);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic));
            h.HasAction = false; a.ActionFinished(true, .4f);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:0:next" }));
        }
        [Test] public void BasicReservationRetainsTargetAndRechecksReadinessAtCompletion()
        {
            var h = new Host { HasAction = true, Basic = true }; var a = new PlayerActionArbiter(h);
            var origins = new CombatCommandOrigins(new CombatInputSession(7, 3, 2), 0, 0, 0, 0, 0, 0, weapon1Basic: 7);
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon1Basic), origins);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic)); Assert.That(h.ActiveSlot, Is.Zero);
            h.HasAction = false; h.UnavailableSlot = 0; a.ActionFinished(true, .4f);
            Assert.That(h.Calls, Is.Empty); Assert.That(h.ActiveSlot, Is.Zero); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
            Assert.That(h.Outcomes[h.Outcomes.Count - 1].reason, Is.EqualTo(CombatCommandReason.Unavailable));
        }
        [Test] public void TargetedSkillDuringBasicStartsImmediatelyAndDropsQueuedBasic()
        {
            var h = new Host { HasAction = true, Basic = true }; var a = new PlayerActionArbiter(h);
            var first = new CombatCommandOrigins(new CombatInputSession(7, 3, 2), 0, 0, 0, 0, 0, 0, weapon1Basic: 5);
            var second = new CombatCommandOrigins(new CombatInputSession(7, 3, 2), 0, 0, 0, 0, 0, 0, weapon2Special: 6);
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon1Basic), first);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic));
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon2Special), second);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:special:Cancel" })); Assert.That(h.StartedAt, Is.Null);
            Assert.That(h.ActiveSlot, Is.EqualTo(1)); Assert.That(h.IsBasicAction, Is.False);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
            Assert.That(h.Outcomes[0].key.Equals(second.For(CombatButtons.Weapon2Special)), Is.True);
            Assert.That(h.Outcomes[0].state, Is.EqualTo(CombatCommandState.Started));
            Assert.That(h.Outcomes[1].key.Equals(first.For(CombatButtons.Weapon1Basic)), Is.True);
            Assert.That(h.Outcomes[1].reason, Is.EqualTo(CombatCommandReason.Priority));
            h.Calls.Clear(); h.HasAction = false; a.ActionFinished(false, 1.25f); Assert.That(h.Calls, Is.Empty);
        }
        [Test] public void UnavailableTargetedSkillKeepsBasicAndItsQueuedSuccessor()
        {
            var h = new Host { HasAction = true, Basic = true, HasSpecialCharge = false }; var a = new PlayerActionArbiter(h);
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon1Basic));
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon1Special));
            Assert.That(h.Calls, Is.EqualTo(new[] { "recast" })); Assert.That(h.IsBasicAction, Is.True);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic));
            h.Calls.Clear(); h.HasAction = false; a.ActionFinished(true, .4f);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:0:next" }));
        }
        static readonly CombatCommandOrigins TargetedKeys = new CombatCommandOrigins(new CombatInputSession(7, 3, 2), 0, 0, 0, 0, 0, 0, 1, 2, 3, 4);
        static CombatInput Targeted(CombatButtons button) => new CombatInput(pressed: button);
        [TestCase(false)] [TestCase(true)]
        public void OtherWeaponBasicWaitsForRunningBasicAndChainsAtItsEnd(bool hitOccurred)
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = hitOccurred }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon1Basic), TargetedKeys);
            a.Evaluate(Targeted(CombatButtons.Weapon2Basic), TargetedKeys);
            Assert.That(h.Calls, Is.Empty); Assert.That(h.ActiveSlot, Is.Zero);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic), "One reservation; the newer other-weapon press replaces the successor.");
            Assert.That(h.Outcomes[h.Outcomes.Count - 1].reason, Is.EqualTo(CombatCommandReason.Priority));
            a.Evaluate(new CombatInput()); Assert.That(h.Calls, Is.Empty, "Without a recovery cancel the basic keeps running.");
            h.HasAction = false; a.ActionFinished(true, .4f);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:basic:Chain" })); Assert.That(h.StartedAt, Is.EqualTo(.4f));
        }
        [Test] public void HitCancelReadyStartsOtherWeaponBasicAtOnce()
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = true, HitCancelReady = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Basic), TargetedKeys);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:basic:CancelAfterHit" })); Assert.That(h.StartedAt, Is.Null);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
        }
        [Test] public void ReservedOtherWeaponBasicCutsRecoveryOnceHitCancelOpens()
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Basic), TargetedKeys);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic)); Assert.That(h.Calls, Is.Empty);
            h.HitCancelReady = true; a.Evaluate(new CombatInput());
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:basic:CancelAfterHit" })); Assert.That(h.StartedAt, Is.Null);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
        }
        [Test] public void DashInTheSameTickWinsOverHitCancel()
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = true, DashSucceeds = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Basic), TargetedKeys);
            h.HitCancelReady = true; a.Evaluate(new CombatInput(pressed: CombatButtons.Dash));
            Assert.That(h.Calls, Is.EqualTo(new[] { "dash" })); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
        }
        [Test] public void HitCancelNeverPromotesSameWeaponSuccessor()
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon1Basic), TargetedKeys);
            h.HitCancelReady = true; a.Evaluate(new CombatInput());
            Assert.That(h.Calls, Is.Empty); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic));
        }
        [Test] public void SameWeaponBasicStillQueuesComboSuccessor()
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon1Basic), TargetedKeys);
            Assert.That(h.Calls, Is.Empty); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic));
            h.HasAction = false; a.ActionFinished(true, .4f);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:0:next" })); Assert.That(h.StartedAt, Is.EqualTo(.4f));
        }
        [Test] public void SameWeaponSkillDuringBasicGetsNoSwitchBonus()
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon1Special), TargetedKeys);
            Assert.That(h.Calls, Is.EqualTo(new[] { "recast", "slot:0:special" }));
        }
        [Test] public void OtherWeaponSkillDuringBasicCarriesSwitchKind()
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Special), TargetedKeys);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:special:CancelAfterHit" }));
        }
        [Test] public void ActivatedSkillIsCancelledByOtherWeaponSkill()
        {
            var h = new Host { HasAction = true, ActionActivated = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Special), TargetedKeys);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:special:CancelAfterHit" })); Assert.That(h.ActiveSlot, Is.EqualTo(1));
        }
        [Test] public void ActivatedSkillReservesOtherWeaponBasicUntilItEnds()
        {
            var h = new Host { HasAction = true, ActionActivated = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Basic), TargetedKeys);
            Assert.That(h.Calls, Is.Empty); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic));
            h.HasAction = false; a.ActionFinished(false, 1.5f);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:basic:Chain" })); Assert.That(h.StartedAt, Is.EqualTo(1.5f));
        }
        [TestCase(CombatButtons.Weapon2Basic)] [TestCase(CombatButtons.Weapon2Special)]
        [TestCase(CombatButtons.Weapon1Basic)] [TestCase(CombatButtons.Weapon1Special)]
        public void SkillBeforeActivationRejectsEveryAttackWithoutReservation(CombatButtons button)
        {
            var h = new Host { HasAction = true, ActionActivated = false }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(button), TargetedKeys);
            Assert.That(h.Calls, Has.None.StartsWith("slot:")); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
            Assert.That(h.Outcomes[h.Outcomes.Count - 1].reason, Is.EqualTo(CombatCommandReason.Busy));
        }
        [Test] public void ActivatedSkillStillRejectsSameWeaponAttacks()
        {
            var h = new Host { HasAction = true, ActionActivated = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon1Basic), TargetedKeys);
            Assert.That(h.Calls, Is.Empty); Assert.That(h.Outcomes[0].reason, Is.EqualTo(CombatCommandReason.Busy));
        }
        [Test] public void UnavailableOtherWeaponKeepsRunningBasic()
        {
            var h = new Host { HasAction = true, Basic = true, ActionActivated = true, HasSpecialCharge = false }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Special), TargetedKeys);
            Assert.That(h.Calls, Is.Empty); Assert.That(h.IsBasicAction, Is.True);
            Assert.That(h.Outcomes[0].reason, Is.EqualTo(CombatCommandReason.Cooldown));
        }
        [Test] public void IdleStartIsNotASwitch()
        {
            var h = new Host(); var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Basic), TargetedKeys);
            Assert.That(h.Calls, Is.EqualTo(new[] { "slot:1:basic" }));
        }
        [Test] public void DashStillRejectsOtherWeaponAttacks()
        {
            var h = new Host { Dashing = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Targeted(CombatButtons.Weapon2Basic), TargetedKeys);
            Assert.That(h.Calls, Is.Empty); Assert.That(h.Outcomes[0].reason, Is.EqualTo(CombatCommandReason.Busy));
        }
        [Test] public void OtherSlotSpecialDoesNotDetonateFlyingMissile()
        {
            var h = new Host { HasAction = true, MissileFlying = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon2Special)); Assert.That(h.Calls, Is.Empty);
            a.Evaluate(new CombatInput(pressed: CombatButtons.Weapon1Special)); Assert.That(h.Calls, Is.EqualTo(new[] { "detonate" }));
        }
        static CombatInput Press(CombatButtons buttons) => new CombatInput(0, buttons);
        const CombatButtons Attacks = CombatButtons.Basic | CombatButtons.Special;

        [Test] public void InputCoalescesEdgesWhileContinuousStateUsesLatestSample()
        {
            var input = new CombatInputBuffer();
            input.Submit(new CombatInput(1, CombatButtons.Basic, true));
            var saved = input.Current;
            input.Submit(new CombatInput(-1, CombatButtons.Basic | CombatButtons.Jump, released: CombatButtons.Special));
            Assert.That(input.Current.Pressed, Is.EqualTo(CombatButtons.Basic | CombatButtons.Jump));
            Assert.That(input.Current.Axis, Is.EqualTo(-1)); Assert.That(input.Current.SpecialHeld, Is.False);
            Assert.That(input.Current.Released, Is.EqualTo(CombatButtons.Special));
            Assert.That(saved.Axis, Is.EqualTo(1)); Assert.That(saved.SpecialHeld, Is.True);
            Assert.That(saved.Pressed, Is.EqualTo(CombatButtons.Basic));
            input.SetContinuous(0, true);
            var before = input.Current;
            Assert.Throws<ArgumentOutOfRangeException>(() => input.SetContinuous(2, false));
            Assert.That(input.Current, Is.EqualTo(before));
            input.ClearPressed();
            Assert.That(input.Current.Pressed, Is.EqualTo(CombatButtons.None)); Assert.That(input.Current.SpecialHeld, Is.True);
            Assert.That(input.Current.Released, Is.EqualTo(CombatButtons.None));
            input.Clear(); Assert.That(input.Current, Is.EqualTo(default(CombatInput)));
        }
        [Test] public void IndependentInputOwnersDoNotShareEdgesAndHeldInputDoesNotRepeatPresses()
        {
            var a = new CombatInputBuffer(); var b = new CombatInputBuffer();
            a.Submit(new CombatInput(1, Attacks, true)); a.ClearPressed();
            a.Submit(new CombatInput(1, CombatButtons.None, true));
            Assert.That(a.Current.Pressed, Is.EqualTo(CombatButtons.None));
            Assert.That(b.Current, Is.EqualTo(default(CombatInput)));
        }
        [TestCase(true, true, true, "dash")]
        [TestCase(false, true, true, "dash,jump")]
        [TestCase(false, false, true, "dash,jump,recast,special")]
        [TestCase(false, false, false, "dash,jump,recast,basic")]
        public void OnlySuccessfulCancelWinsOverLowerPriorityInputs(bool dash, bool jump, bool special, string expected)
        {
            var h = new Host { DashSucceeds = dash, JumpSucceeds = jump, CanRequestSpecial = special };
            new PlayerActionArbiter(h).Evaluate(Press(CombatButtons.Dash | CombatButtons.Jump | Attacks));
            Assert.That(string.Join(",", h.Calls), Is.EqualTo(expected));
            Assert.That(h.StartedAt, Is.Null, "An immediate start uses current simulation time.");
        }
        [TestCase(false, "dash")]
        [TestCase(true, "dash")]
        public void SuccessfulDashTakesPriorityOverDrop(bool hasAction, string expected)
        {
            var h = new Host { HasAction = hasAction, DashSucceeds = true };
            new PlayerActionArbiter(h).Evaluate(Press(CombatButtons.Drop | CombatButtons.Dash));
            Assert.That(string.Join(",", h.Calls), Is.EqualTo(expected));
        }
        [TestCase(false)] [TestCase(true)]
        public void DropDuringBasicCancelsOnlyOnSuccessAndKeepsReceiptIdentity(bool succeeds)
        {
            var h = new Host { HasAction = true, Basic = true, DropSucceeds = succeeds }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Press(CombatButtons.Weapon1Basic), TargetedKeys);
            var keys = new CombatCommandOrigins(new CombatInputSession(7, 3, 2), 0, 0, 0, 0, 0, drop: 5);
            a.Evaluate(Press(CombatButtons.Drop), keys);
            Assert.That(h.Calls, Is.EqualTo(new[] { "drop" }));
            Assert.That(h.HasAction, Is.EqualTo(!succeeds));
            Assert.That(a.Reserved, Is.EqualTo(succeeds ? ReservedPlayerAction.None : ReservedPlayerAction.Basic));
            var outcome = h.Outcomes.Find(row => row.key.Equals(keys.For(CombatButtons.Drop)));
            Assert.That(outcome.state, Is.EqualTo(succeeds ? CombatCommandState.Started : CombatCommandState.Rejected));
            Assert.That(outcome.reason, Is.EqualTo(succeeds ? CombatCommandReason.None : CombatCommandReason.NoPlatform));
        }
        [TestCase(true, false)] [TestCase(false, true)]
        public void DropCannotCancelSpecialOrDash(bool special, bool dashing)
        {
            var h = new Host { HasAction = special, Basic = false, Dashing = dashing };
            new PlayerActionArbiter(h).Evaluate(Press(CombatButtons.Drop));
            Assert.That(h.Calls, Is.Empty); Assert.That(h.HasAction, Is.EqualTo(special)); Assert.That(h.Dashing, Is.EqualTo(dashing));
        }
        [Test] public void SpecialStartsImmediatelyAndDropsQueuedBasic()
        {
            var h = new Host { HasAction = true, Basic = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Press(CombatButtons.Basic));
            a.Evaluate(Press(Attacks));
            Assert.That(h.Calls, Is.EqualTo(new[] { "recast", "special" }));
            Assert.That(h.StartedAt, Is.Null);
            Assert.That(h.IsBasicAction, Is.False);
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
            h.Calls.Clear(); a.ActionFinished(false, 1.25f);
            Assert.That(h.Calls, Is.Empty);
        }
        [Test] public void BasicReservationStartsOneSuccessorAtPredecessorEnd()
        {
            var h = new Host { HasAction = true, Basic = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Press(CombatButtons.Basic)); a.Evaluate(Press(CombatButtons.Basic));
            h.HasAction = false; a.ActionFinished(true, .4f);
            Assert.That(h.Calls, Is.EqualTo(new[] { "next-basic" })); Assert.That(h.StartedAt, Is.EqualTo(.4f));
            a.ActionFinished(true, 1f);
            Assert.That(h.Calls, Is.EqualTo(new[] { "next-basic", "combo-window" })); Assert.That(h.StartedAt, Is.EqualTo(1));
        }
        [Test] public void SwapReservationSupersedesAttacksAndExecutesAtCompletion()
        {
            var h = new Host { HasAction = true, Basic = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Press(Attacks)); a.Evaluate(Press(CombatButtons.Swap | Attacks)); a.Evaluate(Press(Attacks));
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Swap));
            h.Calls.Clear(); h.HasAction = false; a.ActionFinished(true, .4f); a.AfterMovement();
            Assert.That(h.Calls, Is.EqualTo(new[] { "swap" })); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
        }
        [Test] public void DashRejectsAttackReservationsAndDefersSwapUntilMovementEnds()
        {
            var h = new Host { Dashing = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Press(Attacks)); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None)); Assert.That(h.Calls, Is.Empty);
            a.Evaluate(Press(CombatButtons.Swap)); a.AfterMovement(); Assert.That(h.Calls, Is.Empty);
            h.Dashing = false; a.AfterMovement(); a.AfterMovement(); Assert.That(h.Calls, Is.EqualTo(new[] { "swap" }));
        }
        [TestCase(CombatButtons.Dash)] [TestCase(CombatButtons.Jump)] [TestCase(CombatButtons.Special)] [TestCase(CombatButtons.Drop)]
        public void SuccessfulCancelOrRecastDiscardsOldReservations(CombatButtons button)
        {
            var h = new Host { HasAction = true, Basic = true }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Press(CombatButtons.Basic));
            a.Evaluate(Press(CombatButtons.Dash | CombatButtons.Jump));
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.Basic), "Rejected attempts must not cancel the queued successor.");
            h.DashSucceeds = h.JumpSucceeds = h.RecastSucceeds = true;
            h.CanRequestSpecial = false; a.Evaluate(Press(button));
            Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
        }
        [TestCase(false)] [TestCase(true)]
        public void MissileFlightDropsAllOtherPressesEvenWhenDetonationChangesState(bool detonates)
        {
            var h = new Host { HasAction = true, MissileFlying = true, DetonationEndsFlight = detonates }; var a = new PlayerActionArbiter(h);
            a.Evaluate(Press(Attacks | CombatButtons.Dash | CombatButtons.Jump | CombatButtons.Swap | CombatButtons.Drop));
            Assert.That(h.Calls, Is.EqualTo(new[] { "detonate" })); Assert.That(a.Reserved, Is.EqualTo(ReservedPlayerAction.None));
            h.HasAction = h.MissileFlying = false; a.AfterMovement(); a.Evaluate(default);
            Assert.That(h.Calls, Is.EqualTo(new[] { "detonate" }));
        }
        [Test] public void IdleSwapWinsOverAttacksAndExplicitClearDropsReservations()
        {
            var h = new Host(); var a = new PlayerActionArbiter(h);
            a.Evaluate(Press(CombatButtons.Swap | Attacks)); Assert.That(h.Calls, Is.EqualTo(new[] { "swap" }));
            h.HasAction = h.Basic = true; a.Evaluate(Press(CombatButtons.Basic)); a.ClearReservations();
            h.Calls.Clear(); h.HasAction = false; a.ActionFinished(true, .4f);
            Assert.That(h.Calls, Is.EqualTo(new[] { "combo-window" }));
        }
        static CombatCommandOrigins Origins(long basic = 0, long special = 0, long dash = 0, long jump = 0, long swap = 0)
            => new CombatCommandOrigins(new CombatInputSession(7, 3, 2), basic, special, dash, jump, swap, 0);
        [Test] public void ReservationsRetainOriginUntilRealStartAndPriorityLossRejectsOriginalKey()
        {
            var h = new Host { HasAction = true, Basic = true }; var arbiter = new PlayerActionArbiter(h);
            var first = Origins(basic: 10); var replacement = Origins(special: 11);
            arbiter.Evaluate(Press(CombatButtons.Basic), first);
            Assert.That(h.Outcomes, Is.Empty); Assert.That(arbiter.ReservedOrigin.Equals(first.For(CombatButtons.Basic)), Is.True);
            arbiter.Evaluate(Press(CombatButtons.Special), replacement);
            Assert.That(h.Outcomes.Count, Is.EqualTo(2));
            Assert.That(h.Outcomes[0].key.Equals(replacement.For(CombatButtons.Special)), Is.True);
            Assert.That(h.Outcomes[0].state, Is.EqualTo(CombatCommandState.Started));
            Assert.That(h.Outcomes[1].key.Equals(first.For(CombatButtons.Basic)), Is.True);
            Assert.That(h.Outcomes[1].state, Is.EqualTo(CombatCommandState.Rejected));
            Assert.That(h.Outcomes[1].reason, Is.EqualTo(CombatCommandReason.Priority));
            Assert.That(h.StartedAt, Is.Null);
            Assert.That(arbiter.Reserved, Is.EqualTo(ReservedPlayerAction.None));
        }
        [Test] public void UnavailableSpecialKeepsBasicAndItsQueuedSuccessor()
        {
            var h = new Host { HasAction = true, Basic = true, CanRequestSpecial = false };
            var arbiter = new PlayerActionArbiter(h); var origins = Origins(special: 20);
            arbiter.Evaluate(Press(CombatButtons.Basic));
            arbiter.Evaluate(Press(CombatButtons.Special), origins);
            Assert.That(h.IsBasicAction, Is.True);
            Assert.That(arbiter.Reserved, Is.EqualTo(ReservedPlayerAction.Basic));
            Assert.That(h.Calls, Does.Not.Contain("special"));
            Assert.That(h.Outcomes.Count, Is.EqualTo(1));
            Assert.That(h.Outcomes[0].state, Is.EqualTo(CombatCommandState.Rejected));
            Assert.That(h.Outcomes[0].reason, Is.EqualTo(CombatCommandReason.Cooldown));
        }
        [Test] public void SimultaneousLowerPriorityPressesHaveDistinctRejectedKeys()
        {
            var h = new Host { DashSucceeds = true }; var origins = Origins(basic: 30, special: 31, dash: 32, jump: 33);
            new PlayerActionArbiter(h).Evaluate(Press(CombatButtons.Basic | CombatButtons.Special | CombatButtons.Dash | CombatButtons.Jump), origins);
            Assert.That(h.Outcomes.Count, Is.EqualTo(4));
            Assert.That(h.Outcomes[0].key.Equals(origins.For(CombatButtons.Dash)), Is.True); Assert.That(h.Outcomes[0].state, Is.EqualTo(CombatCommandState.Started));
            foreach (var outcome in h.Outcomes.GetRange(1, 3))
            { Assert.That(outcome.state, Is.EqualTo(CombatCommandState.Rejected)); Assert.That(outcome.reason, Is.EqualTo(CombatCommandReason.Priority)); }
        }
    }
}
