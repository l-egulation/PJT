using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public sealed class CombatHitEventStream
    {
        public const int DefaultCapacity = 2048;
        readonly CombatAcceptedHit[] values;
        int start, count;
        long latest;
        public long LatestSequence => latest;
        public CombatHitEventStream(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            values = new CombatAcceptedHit[capacity];
        }
        public void Add(long tick, float now, CombatHitOrigin origin, CombatActorHandle target,
            float requested, ActorVitalsSnapshot before, ActorDamageResult applied, ActorKinematicsSnapshot pose, CombatHitContact contact = default, bool critical = false)
        {
            if (!applied.Accepted) return;
            if (count == values.Length) { start = (start + 1) % values.Length; count--; }
            values[(start + count++) % values.Length] = new CombatAcceptedHit(checked(++latest), tick, now, origin, target, requested, before, applied, pose, contact, critical);
        }
        public bool TryRead(CombatInputSession current, CombatOutcomeCursor cursor, CombatAcceptedHit[] destination, out CombatHitPage page)
        {
            page = default;
            if (!current.Equals(cursor.Session) || cursor.AfterEventSequence < 0 || cursor.AfterEventSequence > LatestSequence || destination == null || destination.Length == 0) return false;
            long first = count == 0 ? LatestSequence + 1 : values[start].EventSequence;
            long dropped = Math.Max(0, first - cursor.AfterEventSequence - 1);
            long after = Math.Max(cursor.AfterEventSequence, first - 1); int copied = 0;
            for (int i = (int)(after - first + 1); i < count && copied < destination.Length; i++) destination[copied++] = values[(start + i) % values.Length];
            long next = copied == 0 ? after : destination[copied - 1].EventSequence;
            page = new CombatHitPage(new CombatOutcomeCursor(current, next), copied, dropped, LatestSequence);
            return true;
        }
    }
}
