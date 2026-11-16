namespace Deviant.Combat.Contracts
{
    // How a slot-targeted start follows the other weapon's action.
    // CancelAfterHit means the replaced action already committed its first hit
    // or effect; Chain is a reserved start at that action's completion. Both
    // receive the switch windup reduction.
    public enum WeaponSwitchStart { None, Cancel, CancelAfterHit, Chain }

    // Slot-targeted intent is checked without committing a weapon switch.
    public interface ITargetedPlayerActionHost
    {
        int ActiveSlot { get; }
        // The running action has committed its first hit/effect (resources activated).
        bool ActionActivated { get; }
        // The running basic landed a primary hit with a recovery-cancel ink and
        // is now in recovery, so the other weapon's basic may replace it.
        bool HitCancelReady { get; }
        CombatCommandReason CheckAttack(int slot, bool special);
        CombatActionAttempt StartAttack(int slot, bool special, CombatCommandKey command, float? startAt, bool comboSuccessor, WeaponSwitchStart weaponSwitch);
    }

    // Synchronous bridge while action bodies/resources still live in Unity actors.
    // Queries are live: a successful attempt can cancel/change the current action.
    public interface IPlayerActionHost
    {
        bool HasAction { get; }
        bool IsBasicAction { get; }
        bool Dashing { get; }
        bool MissileFlying { get; }
        // Availability, charge, pending return, and reservation prerequisites.
        // StartSpecial must recheck ground/target conditions at execution.
        bool CanRequestSpecial { get; }
        bool HasSpecialCharge { get; }
        CombatCommandReason SpecialUnavailableReason { get; }
        CombatActionAttempt DropThrough(CombatCommandKey command);
        CombatActionAttempt TryDash(CombatCommandKey command);
        CombatActionAttempt TryJump(CombatCommandKey command);
        CombatActionAttempt TryApproachRecast(CombatCommandKey command);
        // Flight ignores all other presses even when detonation is too early.
        CombatActionAttempt RequestMissileDetonation(CombatCommandKey command);
        CombatActionAttempt StartBasic(CombatCommandKey command);
        CombatActionAttempt StartNextBasic(CombatCommandKey command, float completedAt);
        // null uses current simulation time; a queued action uses its predecessor's end.
        CombatActionAttempt StartSpecial(CombatCommandKey command, float? startAt);
        void OpenComboWindow(float completedAt);
        CombatActionAttempt SwitchWeapon(CombatCommandKey command);
        void RecordOutcome(CombatCommandKey command, CombatCommandState state, CombatCommandReason reason,
            System.Guid executionId = default, string detail = null);
    }
}
