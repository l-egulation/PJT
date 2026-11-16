using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        internal float RecoveryCancelDamageBonus { get; private set; }

        void Awake()
        {
            actionArbiter = new PlayerActionArbiter(new PlayerActionHost(this));
            // The wall stamina ring over the head (user request 2026-11-10).
            if (!GetComponent<WallStaminaRing>()) gameObject.AddComponent<WallStaminaRing>();
        }

        readonly struct PlayerComboHost : IPlayerComboHost
        {
            readonly CombatPlayer player;
            public PlayerComboHost(CombatPlayer player) { this.player = player; }
            public float Now => player.arena.Now;
            public int BasicCount => player.Form.basics.Length;
        }

        sealed class PlayerActionHost : IPlayerActionHost, ITargetedPlayerActionHost
        {
            readonly CombatPlayer player;
            CombatCommandReason specialReason;
            public PlayerActionHost(CombatPlayer player) { this.player = player; }
            public int ActiveSlot => player.selected;
            public CombatCommandReason CheckAttack(int slot, bool special)
            {
                if (!player.IsWeaponSlotReady(slot)) return CombatCommandReason.Unavailable;
                // Selection is scoped to a read-only query. No switch callbacks,
                // combo changes or action cancellation are performed here.
                int previous = player.selected;
                try
                {
                    player.selected = slot;
                    if (!special) return CombatCommandReason.None;
                    if (!CanRequestSpecial) return specialReason;
                    if (!player.SpecialGroundAvailable()) return CombatCommandReason.GroundRequired;
                    return CombatCommandReason.None;
                }
                finally { player.selected = previous; }
            }
            public bool ActionActivated => player.currentCast && player.currentCast.ResourceResult?.Status == ActionResourceStatus.Succeeded;
            public bool HitCancelReady => player.currentCast && player.currentCast.isBasic && player.currentCast.HitCancelUnlocked && player.currentCast.InRecovery;
            public CombatActionAttempt StartAttack(int slot, bool special, CombatCommandKey command, float? startAt, bool comboSuccessor, WeaponSwitchStart weaponSwitch)
            {
                var reason = CheckAttack(slot, special);
                if (reason != CombatCommandReason.None) return CombatActionAttempt.Reject(reason);
                // An admitted skill may replace a running basic; BeginSpecial cancels it.
                // The other weapon may replace a basic or an activated skill; the
                // create boundary cancels it only after admission has passed. A chain
                // starts the other weapon after the previous action has completed.
                bool other = slot != player.selected;
                bool replaces = other && (weaponSwitch == WeaponSwitchStart.Cancel || weaponSwitch == WeaponSwitchStart.CancelAfterHit) && HasAction && (IsBasicAction || ActionActivated);
                bool chains = other && weaponSwitch == WeaponSwitchStart.Chain && !HasAction;
                if (Dashing || HasAction && !replaces && !(special && IsBasicAction)) return CombatActionAttempt.Reject(CombatCommandReason.Busy);
                int previous = player.selected;
                var previousCombo = player.combo;
                bool started = false;
                try
                {
                    player.weaponSwitchStart = replaces || chains ? weaponSwitch : WeaponSwitchStart.None;
                    player.weaponSwitchFrom = player.weapons[previous];
                    player.RecoveryCancelDamageBonus = other && !special && weaponSwitch == WeaponSwitchStart.CancelAfterHit && HitCancelReady ? player.currentCast.HitCancelDamageBonus : 0;
                    player.selected = slot;
                    if (slot != previous) player.combo.Clear();
                    CombatActionAttempt result;
                    if (special) result = StartSpecial(command, startAt);
                    else
                    {
                        var comboHost = new PlayerComboHost(player);
                        int index = comboSuccessor && slot == previous ? player.combo.SelectNext(ref comboHost) : player.combo.SelectStart(ref comboHost);
                        result = player.BeginBasic(command, index, startAt ?? -1);
                    }
                    started = result.Started;
                    if (started && slot != previous)
                    {
                        foreach (var cast in player.arena.casts) if (cast && cast.owner == player && cast != player.currentCast) cast.ForgetApproach();
                        player.arena.Record("weapon slot " + (slot + 1));
                    }
                    return result;
                }
                finally
                {
                    player.weaponSwitchStart = WeaponSwitchStart.None; player.weaponSwitchFrom = null; player.RecoveryCancelDamageBonus = 0;
                    if (!started) { player.selected = previous; player.combo = previousCombo; }
                }
            }
            public bool HasAction => player.currentCast;
            public bool IsBasicAction => player.currentCast && player.currentCast.isBasic;
            public bool Dashing => player.Dashing;
            public bool MissileFlying => player.currentCast && player.currentCast.MissileFlying;
            public bool HasSpecialCharge => player.Weapon.ExperimentalChargeStamp || player.Weapon.Cooldown.Available > 0;
            public bool CanRequestSpecial
            {
                get
                {
                    if (!player.IsWeaponSlotReady(player.selected))
                    { specialReason = CombatCommandReason.Unavailable; return false; }
                    if (player.Weapon.ExperimentalChargeStamp) { specialReason=CombatCommandReason.None;return true; }
                    if (player.arena.GetActionTracks(player.Special.id) != null)
                    {
                        if (!HasSpecialCharge) specialReason = CombatCommandReason.Cooldown;
                        else if (!IsBasicAction) specialReason = CombatCommandReason.None;
                        else if (!player.SpecialGroundAvailable()) specialReason = CombatCommandReason.GroundRequired;
                        else specialReason = CombatCommandReason.None;
                        return specialReason == CombatCommandReason.None;
                    }
                    var source = player.SpecialSource();
                    if (source.unavailable) specialReason = CombatCommandReason.Unavailable;
                    else if (!HasSpecialCharge) specialReason = CombatCommandReason.Cooldown;
                    else if (source.ranged != null && source.ranged.returnDelay > 0 && player.ShurikenReturnPending(player.Weapon)) specialReason = CombatCommandReason.ReturnPending;
                    else if (IsBasicAction)
                    {
                        if (!player.SpecialGroundAvailable()) specialReason = CombatCommandReason.GroundRequired;
                        else specialReason = CombatCommandReason.None;
                    }
                    else specialReason = CombatCommandReason.None;
                    return specialReason == CombatCommandReason.None;
                }
            }
            public CombatCommandReason SpecialUnavailableReason => specialReason;
            public CombatActionAttempt DropThrough(CombatCommandKey command)
            {
                if (player.TryWallJump()) return CombatActionAttempt.Start(System.Guid.NewGuid(), immediate: true);
                if (player.TryBufferWallJump(command)) return CombatActionAttempt.Buffer();
                if (!player.DropThrough()) return CombatActionAttempt.Reject(CombatCommandReason.NoPlatform);
                player.CancelAction(CombatCommandReason.Priority);
                return CombatActionAttempt.Start(System.Guid.NewGuid(), immediate: true);
            }
            public CombatActionAttempt TryDash(CombatCommandKey command) => player.TryDash(command);
            public CombatActionAttempt TryJump(CombatCommandKey command) => player.TryJump(command);
            public CombatActionAttempt TryApproachRecast(CombatCommandKey command) => player.TryApproachRecast(command);
            public CombatActionAttempt RequestMissileDetonation(CombatCommandKey command)
            {
                var cast = player.currentCast;
                return cast.TryMissileDetonate() ? CombatActionAttempt.Start(cast.ExecutionId, immediate: true)
                    : CombatActionAttempt.Reject(CombatCommandReason.Cooldown);
            }
            public CombatActionAttempt StartBasic(CombatCommandKey command)
            {
                var host = new PlayerComboHost(player);
                return player.BeginBasic(command, player.combo.SelectStart(ref host));
            }
            public CombatActionAttempt StartNextBasic(CombatCommandKey command, float completedAt)
            {
                var host = new PlayerComboHost(player);
                return player.BeginBasic(command, player.combo.SelectNext(ref host), completedAt);
            }
            public CombatActionAttempt StartSpecial(CombatCommandKey command, float? startAt) => player.BeginSpecial(command, startAt ?? -1);
            public void OpenComboWindow(float completedAt) => player.combo.OpenWindow(completedAt);
            public CombatActionAttempt SwitchWeapon(CombatCommandKey command) => player.SwitchNow(command);
            public void RecordOutcome(CombatCommandKey command, CombatCommandState state, CombatCommandReason reason,
                System.Guid executionId = default, string detail = null) => player.arena.RecordCommand(command, state, reason, executionId, detail);
        }
    }
}
