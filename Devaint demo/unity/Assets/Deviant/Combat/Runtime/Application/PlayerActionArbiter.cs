using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public enum ReservedPlayerAction { None, Basic, Special, Swap }

    // Owns selection/reservation, not action bodies, resource spending or physics.
    // Called once for a live player's accepted tick input. The caller handles
    // death/input blocking/automatic returns and verifies completion identity.
    public sealed class PlayerActionArbiter
    {
        static readonly CombatButtons[] TargetedPriority = {
            CombatButtons.Weapon1Special, CombatButtons.Weapon2Special,
            CombatButtons.Weapon1Basic, CombatButtons.Weapon2Basic
        };
        readonly IPlayerActionHost host;
        int reservedSlot = -1;
        public ReservedPlayerAction Reserved { get; private set; }
        public CombatCommandKey ReservedOrigin { get; private set; }
        public PlayerActionArbiter(IPlayerActionHost host) { this.host = host ?? throw new ArgumentNullException(nameof(host)); }
        public void ClearReservations(CombatCommandReason reason = CombatCommandReason.Interrupted)
        {
            var origin = TakeReservation();
            Reject(origin, reason);
        }
        CombatCommandKey TakeReservation()
        {
            var origin = ReservedOrigin;
            Reserved = ReservedPlayerAction.None; ReservedOrigin = default; reservedSlot = -1;
            return origin;
        }
        void Reserve(ReservedPlayerAction action, CombatCommandKey origin, int slot = -1)
        {
            var previous = ReservedOrigin;
            Reserved = action; ReservedOrigin = origin; reservedSlot = slot;
            Reject(previous, CombatCommandReason.Priority);
        }

        public void Evaluate(CombatInput input, CombatCommandOrigins origins = default)
        {
            CombatButtons handled = CombatButtons.None;
            CombatCommandKey attempted = default;
            try { Evaluate(input, origins, ref handled, ref attempted); }
            catch (Exception exception)
            {
                host.RecordOutcome(attempted, CombatCommandState.Failed, CombatCommandReason.Exception, detail: exception.Message);
                for (int i = 0; i < CombatInput.ButtonCount; i++)
                {
                    var button = (CombatButtons)(1 << i);
                    if (input.Has(button) && (handled & button) == 0 && !origins.For(button).Equals(attempted))
                        Reject(origins.For(button), CombatCommandReason.Priority);
                }
                handled = input.Pressed;
                throw;
            }
            finally
            {
                for (int i = 0; i < CombatInput.ButtonCount; i++)
                {
                    var button = (CombatButtons)(1 << i);
                    if (input.Has(button) && (handled & button) == 0) Reject(origins.For(button), CombatCommandReason.Priority);
                }
            }
            StartHitCancel();
        }
        // A reserved other-weapon basic cuts the recovery a landed hit unlocked. It runs
        // after this tick's presses (a dash or jump clears the reservation first) and
        // before movement, so the new action is prepared like any pressed start.
        void StartHitCancel()
        {
            if (Reserved != ReservedPlayerAction.Basic || reservedSlot < 0 || !(host is ITargetedPlayerActionHost targeted) ||
                reservedSlot == targeted.ActiveSlot || !targeted.HitCancelReady) return;
            int slot = reservedSlot;
            var key = TakeReservation();
            try { Apply(key, targeted.StartAttack(slot, false, key, null, false, WeaponSwitchStart.CancelAfterHit)); }
            catch (Exception exception) { host.RecordOutcome(key, CombatCommandState.Failed, CombatCommandReason.Exception, detail: exception.Message); throw; }
        }
        void Evaluate(CombatInput input, CombatCommandOrigins origins, ref CombatButtons handled, ref CombatCommandKey attempted)
        {
            if (host.MissileFlying)
            {
                var special = host is ITargetedPlayerActionHost targeted
                    ? (targeted.ActiveSlot == 0 ? CombatButtons.Weapon1Special : CombatButtons.Weapon2Special) : CombatButtons.None;
                if (!input.Has(special)) special = CombatButtons.Special;
                if (input.Has(special))
                { attempted = origins.For(special); Apply(attempted, host.RequestMissileDetonation(attempted)); handled |= special; }
                return;
            }
            if (input.Has(CombatButtons.Dash))
            {
                var key = origins.For(CombatButtons.Dash);
                attempted = key;
                bool started = Apply(key, host.TryDash(key)); handled |= CombatButtons.Dash;
                if (started) { ClearReservations(CombatCommandReason.Priority); return; }
            }
            if (input.Has(CombatButtons.Drop))
            {
                var key = origins.For(CombatButtons.Drop);
                attempted = key;
                bool started = false;
                if ((!host.HasAction || host.IsBasicAction) && !host.Dashing) started = Apply(key, host.DropThrough(key));
                else Reject(key, CombatCommandReason.Busy);
                handled |= CombatButtons.Drop;
                if (started) { ClearReservations(CombatCommandReason.Priority); return; }
            }
            if (input.Has(CombatButtons.Jump))
            {
                var key = origins.For(CombatButtons.Jump);
                attempted = key;
                bool started = Apply(key, host.TryJump(key)); handled |= CombatButtons.Jump;
                if (started) { ClearReservations(CombatCommandReason.Priority); return; }
            }
            if ((input.Pressed & CombatInput.TargetedAttacks) != 0)
            {
                EvaluateTargeted(input, origins, ref handled, ref attempted);
                return;
            }
            if (input.Has(CombatButtons.Swap))
            {
                var key = origins.For(CombatButtons.Swap);
                attempted = key;
                if (host.HasAction || host.Dashing) Reserve(ReservedPlayerAction.Swap, key);
                else { handled |= CombatButtons.Swap; Switch(key); }
                handled |= CombatButtons.Swap;
            }
            else if (Reserved != ReservedPlayerAction.Swap && !host.Dashing)
            {
                var special = origins.For(CombatButtons.Special);
                if (input.Has(CombatButtons.Special))
                {
                    attempted = special;
                    var recast = host.TryApproachRecast(special);
                    if (recast.Started)
                    { Apply(special, recast); handled |= CombatButtons.Special; ClearReservations(CombatCommandReason.Priority); return; }
                }
                if (input.Has(CombatButtons.Special) && host.CanRequestSpecial)
                {
                    if (host.IsBasicAction || !host.HasAction)
                    {
                        if (Apply(special, host.StartSpecial(special, null)))
                            ClearReservations(CombatCommandReason.Priority);
                    }
                    else Reject(special, CombatCommandReason.Busy);
                    handled |= CombatButtons.Special;
                }
                else
                {
                    if (input.Has(CombatButtons.Special))
                    { Reject(special, host.SpecialUnavailableReason); handled |= CombatButtons.Special; }
                    if (input.Has(CombatButtons.Basic))
                    {
                        var key = origins.For(CombatButtons.Basic);
                        attempted = key;
                        if (host.IsBasicAction && Reserved != ReservedPlayerAction.Special) Reserve(ReservedPlayerAction.Basic, key);
                        else if (!host.HasAction) Apply(key, host.StartBasic(key));
                        else Reject(key, CombatCommandReason.Busy);
                        handled |= CombatButtons.Basic;
                    }
                }
            }
        }
        void EvaluateTargeted(CombatInput input, CombatCommandOrigins origins, ref CombatButtons handled, ref CombatCommandKey attempted)
        {
            // Special retains priority over basic; slot 1 wins simultaneous equal
            // priority presses. At most one start or reservation is admitted.
            // The other weapon's skill replaces a basic at any time and a skill once
            // it has activated. The other weapon's basic is reserved until the action
            // ends unless a landed hit unlocked a recovery cancel. Same-weapon basics
            // queue the combo successor and same-weapon skills replace a running basic.
            for (int i = 0; i < TargetedPriority.Length; i++)
            {
                var button = TargetedPriority[i];
                if (!input.Has(button)) continue;
                int slot = i % 2; bool special = i < 2;
                var key = origins.For(button); attempted = key; handled |= button;
                if (!(host is ITargetedPlayerActionHost targeted)) { Reject(key, CombatCommandReason.Unavailable); continue; }
                if (host.Dashing || Reserved == ReservedPlayerAction.Swap) { Reject(key, CombatCommandReason.Busy); continue; }
                if (special && slot == targeted.ActiveSlot)
                {
                    var recast = host.TryApproachRecast(key);
                    if (recast.Started) { Apply(key, recast); ClearReservations(CombatCommandReason.Priority); return; }
                }
                var reason = targeted.CheckAttack(slot, special);
                if (reason != CombatCommandReason.None) { Reject(key, reason); continue; }
                if (!host.HasAction) { if (Apply(key, targeted.StartAttack(slot, special, key, null, false, WeaponSwitchStart.None))) return; continue; }
                var weaponSwitch = WeaponSwitchStart.None;
                if (slot != targeted.ActiveSlot)
                {
                    bool activated = targeted.ActionActivated;
                    if (!host.IsBasicAction && !activated) { Reject(key, CombatCommandReason.Busy); continue; }
                    if (!special && !targeted.HitCancelReady) { Reserve(ReservedPlayerAction.Basic, key, slot); return; }
                    weaponSwitch = activated ? WeaponSwitchStart.CancelAfterHit : WeaponSwitchStart.Cancel;
                }
                else if (!host.IsBasicAction) { Reject(key, CombatCommandReason.Busy); continue; }
                else if (!special) { Reserve(ReservedPlayerAction.Basic, key, slot); return; }
                if (Apply(key, targeted.StartAttack(slot, special, key, null, false, weaponSwitch))) { ClearReservations(CombatCommandReason.Priority); return; }
            }
        }
        public void ActionFinished(bool wasBasic, float completedAt)
        {
            if (reservedSlot >= 0 && host is ITargetedPlayerActionHost targeted)
            {
                int slot = reservedSlot; bool special = Reserved == ReservedPlayerAction.Special;
                var key = TakeReservation();
                try
                {
                    var reason = targeted.CheckAttack(slot, special);
                    if (reason != CombatCommandReason.None) Reject(key, reason);
                    else
                    {
                        bool other = slot != targeted.ActiveSlot;
                        Apply(key, targeted.StartAttack(slot, special, key, completedAt, wasBasic && !special && !other, other ? WeaponSwitchStart.Chain : WeaponSwitchStart.None));
                    }
                }
                catch (Exception exception) { host.RecordOutcome(key, CombatCommandState.Failed, CombatCommandReason.Exception, detail: exception.Message); throw; }
                return;
            }
            if (Reserved == ReservedPlayerAction.Swap) { SwitchReservation(); return; }
            if (Reserved == ReservedPlayerAction.Special)
            {
                var key = TakeReservation();
                try
                {
                    if (host.HasSpecialCharge) Apply(key, host.StartSpecial(key, completedAt));
                    else Reject(key, CombatCommandReason.Cooldown);
                }
                catch (Exception exception) { host.RecordOutcome(key, CombatCommandState.Failed, CombatCommandReason.Exception, detail: exception.Message); throw; }
                return;
            }
            if (wasBasic)
            {
                if (Reserved == ReservedPlayerAction.Basic)
                {
                    var key = TakeReservation();
                    try { Apply(key, host.StartNextBasic(key, completedAt)); }
                    catch (Exception exception) { host.RecordOutcome(key, CombatCommandState.Failed, CombatCommandReason.Exception, detail: exception.Message); throw; }
                }
                else host.OpenComboWindow(completedAt);
            }
        }
        public void AfterMovement()
        {
            if (!host.HasAction && !host.Dashing && Reserved == ReservedPlayerAction.Swap) SwitchReservation();
        }
        void Switch(CombatCommandKey key)
        {
            ClearReservations(CombatCommandReason.Priority);
            Apply(key, host.SwitchWeapon(key));
        }
        void SwitchReservation()
        {
            var key = TakeReservation();
            try { Switch(key); }
            catch (Exception exception) { host.RecordOutcome(key, CombatCommandState.Failed, CombatCommandReason.Exception, detail: exception.Message); throw; }
        }
        bool Apply(CombatCommandKey key, CombatActionAttempt attempt)
        {
            if (attempt.OutcomeRecorded) return attempt.Started;
            if (attempt.Started)
            {
                host.RecordOutcome(key, CombatCommandState.Started, CombatCommandReason.None, attempt.ExecutionId);
                if (attempt.Immediate) host.RecordOutcome(key, CombatCommandState.Completed, CombatCommandReason.None, attempt.ExecutionId);
            }
            else if (!attempt.Deferred) Reject(key, attempt.Reason);
            return attempt.Started;
        }
        void Reject(CombatCommandKey key, CombatCommandReason reason) => host.RecordOutcome(key, CombatCommandState.Rejected, reason);
    }
}
