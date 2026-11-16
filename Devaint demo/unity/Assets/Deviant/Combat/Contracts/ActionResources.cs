using System;

namespace Deviant.Combat.Contracts
{
    public readonly struct ActionResourceKey : IEquatable<ActionResourceKey>
    {
        public Guid ExecutionId { get; }
        public Guid WeaponId { get; }
        public bool IsBasic { get; }
        public ActionResourceKey(Guid executionId, Guid weaponId, bool isBasic)
        {
            if (executionId == Guid.Empty || weaponId == Guid.Empty) throw new ArgumentException("Action and weapon instance identities are required.");
            ExecutionId = executionId; WeaponId = weaponId; IsBasic = isBasic;
        }
        public bool Equals(ActionResourceKey other) => ExecutionId == other.ExecutionId && WeaponId == other.WeaponId && IsBasic == other.IsBasic;
        public override bool Equals(object obj) => obj is ActionResourceKey other && Equals(other);
        public override int GetHashCode() => (ExecutionId.GetHashCode() * 397 ^ WeaponId.GetHashCode()) * 397 ^ IsBasic.GetHashCode();
    }
    public sealed class ActionResourceSnapshot
    {
        public ActionResourceKey Key { get; }
        public float ActivatedAt { get; }
        public float DamageMultiplier { get; }
        public ActionResourceSnapshot(ActionResourceKey key, float activatedAt, float damageMultiplier)
        {
            if (key.ExecutionId == Guid.Empty || key.WeaponId == Guid.Empty || !Finite(activatedAt) || activatedAt < 0 || !Finite(damageMultiplier) || damageMultiplier < 0)
                throw new ArgumentException("A live key and finite activation time/damage multiplier are required.");
            Key = key; ActivatedAt = activatedAt; DamageMultiplier = damageMultiplier;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    public enum ActionResourceStatus { Succeeded, Rejected, Cancelled, Failed }
    public sealed class ActionResourceResult
    {
        public ActionResourceStatus Status { get; }
        public ActionResourceSnapshot Snapshot { get; }
        public string Reason { get; }
        public ActionResourceResult(ActionResourceStatus status, ActionResourceSnapshot snapshot = null, string reason = null)
        {
            if (!Enum.IsDefined(typeof(ActionResourceStatus), status) || (status == ActionResourceStatus.Succeeded) != (snapshot != null) || status != ActionResourceStatus.Succeeded && string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A success requires its snapshot; other results require a reason and no snapshot.");
            Status = status; Snapshot = snapshot; Reason = reason;
        }
    }
    public interface IActionResourceSource
    {
        // One synchronous admission/commit. Rejection must not spend a resource.
        // The provider owns the weapon state; this port never owns a copy of it.
        ActionResourceResult TryActivate(ActionResourceKey key, float now);
    }
    public interface IActionResources : IDisposable
    {
        ActionResourceKey Key { get; }
        bool IsActive { get; }
        ActionResourceResult Current { get; }
        ActionResourceResult TryActivate(Guid expectedExecution, float now);
    }
}
