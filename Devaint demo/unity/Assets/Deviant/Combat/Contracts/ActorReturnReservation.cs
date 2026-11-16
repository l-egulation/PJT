using System;
using System.Collections.Generic;

namespace Deviant.Combat.Contracts
{
    // Runtime execution identity and captured facts, not a saved item/actor ID.
    public sealed class ActorReturnReservation
    {
        public Guid Id { get; }
        public Guid WeaponId { get; }
        public int TargetActorId { get; }
        public ActorPose Origin { get; }
        public float Due { get; }
        public float Gap { get; }
        public ActorReturnReservation(Guid id, Guid weaponId, int targetActorId, ActorPose origin, float now, float delay, float gap)
        {
            if (id == Guid.Empty || weaponId == Guid.Empty || targetActorId <= 0 || origin.Facing != -1 && origin.Facing != 1) throw new ArgumentException("A return requires execution, weapon, target and origin identities.");
            if (!ActorBodyFrame.Finite(now) || now < 0 || !ActorBodyFrame.Finite(delay) || delay < 0 || !ActorBodyFrame.Finite(gap) || gap < 0 || !ActorBodyFrame.Finite(now + delay)) throw new ArgumentOutOfRangeException(nameof(now), "Return timing and gap must remain finite and nonnegative.");
            Id = id; WeaponId = weaponId; TargetActorId = targetActorId; Origin = origin; Due = now + delay; Gap = gap;
        }
    }
    public interface IActorReturnSchedule
    {
        IReadOnlyList<ActorReturnReservation> Read();
        bool Contains(Guid weaponId);
        float Remaining(Guid weaponId, float now);
        bool TryAdd(ActorReturnReservation reservation);
        bool TryTakeDue(Guid reservationId, Guid weaponId, float now, out ActorReturnReservation reservation);
        void Clear();
    }
}
