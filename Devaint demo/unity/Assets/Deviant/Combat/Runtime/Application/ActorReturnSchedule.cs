using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public sealed class ActorReturnSchedule : IActorReturnSchedule
    {
        readonly List<ActorReturnReservation> pending = new List<ActorReturnReservation>();
        public IReadOnlyList<ActorReturnReservation> Read() => new List<ActorReturnReservation>(pending).AsReadOnly();
        public bool Contains(Guid weaponId)
        { foreach (var entry in pending) if (entry.WeaponId == weaponId) return true; return false; }
        public float Remaining(Guid weaponId, float now)
        {
            Check(now);
            foreach (var entry in pending) if (entry.WeaponId == weaponId) return Math.Max(0, entry.Due - now);
            return 0;
        }
        public bool TryAdd(ActorReturnReservation reservation)
        {
            if (reservation == null) throw new ArgumentNullException(nameof(reservation));
            foreach (var entry in pending) if (entry.Id == reservation.Id || entry.WeaponId == reservation.WeaponId) return false;
            pending.Add(reservation); return true;
        }
        public bool TryTakeDue(Guid reservationId, Guid weaponId, float now, out ActorReturnReservation reservation)
        {
            Check(now); reservation = null;
            for (int i = 0; i < pending.Count; i++)
            {
                var entry = pending[i];
                if (entry.Id != reservationId || entry.WeaponId != weaponId) continue;
                // Keep the existing addition/comparison order and consume before
                // any native placement/effect callback, including failed returns.
                if (now + .00001f < entry.Due) return false;
                pending.RemoveAt(i); reservation = entry; return true;
            }
            return false;
        }
        public void Clear() => pending.Clear();
        static void Check(float now) { if (float.IsNaN(now) || float.IsInfinity(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now)); }
    }
}
