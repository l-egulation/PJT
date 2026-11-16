using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // One arena owns a fixed value ring. Writers never allocate or call observers;
    // independent cursors can poll the same retained events without consuming them.
    public sealed class CombatCommandOutcomes
    {
        public const int DefaultCapacity = 1024;
        readonly CombatCommandOutcome[] values;
        long latest;
        int start, count;
        public CombatCommandOutcomes(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            values = new CombatCommandOutcome[capacity];
        }
        public void Add(long tick, float now, CombatCommandKey command, CombatCommandState state,
            CombatCommandReason reason, Guid executionId, ActorReadSnapshot snapshot, string detail = null)
        {
            if (!command.IsValid) return;
            if (count == values.Length) { start = (start + 1) % values.Length; count--; }
            values[(start + count++) % values.Length] = new CombatCommandOutcome(checked(++latest), tick, now, command, state, reason, executionId, snapshot, detail);
        }
        public bool TryRead(CombatInputSession session, CombatOutcomeCursor cursor, CombatCommandOutcome[] destination, out CombatOutcomePage page)
        {
            page = default;
            if (!session.Equals(cursor.Session) || cursor.AfterEventSequence < 0 || cursor.AfterEventSequence > latest || destination == null || destination.Length == 0) return false;
            long first = count == 0 ? latest + 1 : values[start].EventSequence;
            long dropped = Math.Max(0, first - cursor.AfterEventSequence - 1);
            long after = Math.Max(cursor.AfterEventSequence, first - 1);
            int copied = 0;
            for (int i = (int)(after - first + 1); i < count && copied < destination.Length; i++)
                destination[copied++] = values[(start + i) % values.Length];
            long next = copied == 0 ? after : destination[copied - 1].EventSequence;
            page = new CombatOutcomePage(new CombatOutcomeCursor(session, next), copied, dropped, latest);
            return true;
        }
    }

    public static class CombatCommandReasons
    {
        public static CombatCommandReason For(CombatInputDiscardReason reason)
        {
            switch (reason)
            {
                case CombatInputDiscardReason.Blocked: return CombatCommandReason.Blocked;
                case CombatInputDiscardReason.Paused: return CombatCommandReason.Paused;
                case CombatInputDiscardReason.Dead: return CombatCommandReason.Dead;
                case CombatInputDiscardReason.Reset: return CombatCommandReason.Reset;
                case CombatInputDiscardReason.Replaced: return CombatCommandReason.Replaced;
                default: return CombatCommandReason.Cleared;
            }
        }
    }

    // Both AI attack and chase preserve their existing synchronous operation
    // order. Session/admission belongs to the arena adapter, not this policy.
    public static class CombatAICommandExecution
    {
        public static CombatAICommandResult Apply<T>(CombatAICommand command, ref T host) where T : struct, ICombatAICommandHost
        {
            if (command.Intent.Kind == EnemyIntentKind.BeginAttack)
            {
                var execution = host.BeginAttack(command.Key);
                host.SyncFacing(command.Intent.Facing);
                return new CombatAICommandResult(CombatActionAttempt.Start(execution));
            }
            host.SyncFacing(command.Intent.Facing);
            float horizontal = host.Facing * command.Intent.Speed;
            if (host.Grounded && !host.HasChaseFloor()) horizontal = 0;
            return new CombatAICommandResult(CombatActionAttempt.Start(Guid.NewGuid()), horizontal);
        }
    }
}
