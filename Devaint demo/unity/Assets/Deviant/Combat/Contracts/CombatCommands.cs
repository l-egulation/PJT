using System;

namespace Deviant.Combat.Contracts
{
    public enum CombatCommandKind { None, Basic, Special, Dash, Jump, Swap, Drop, EnemyAttack, EnemyChase }
    public enum CombatCommandState { Rejected, Started, Cancelled, Failed, Completed }
    public enum CombatCommandReason
    {
        None, Session, Sequence, Metadata, Capacity, Blocked, Paused, Dead, Cleared, Reset, Replaced,
        Priority, Busy, Cooldown, Unavailable, GroundRequired, TargetRequired, ReturnPending,
        JumpBuffered, BufferExpired, NoPlatform, Interrupted, Activation, Resources, Exception, ActorUnavailable
    }

    // Input samples already have an actor/session and a monotonic sequence.
    // AI uses its own actor-local monotonic sequence, never player button input.
    public readonly struct CombatCommandKey : IEquatable<CombatCommandKey>
    {
        public CombatActorHandle Actor { get; }
        public long Sequence { get; }
        public CombatCommandKind Kind { get; }
        public bool IsValid => Actor.ActorId > 0 && Actor.Session.ActorId > 0 && Actor.Session.Generation > 0 && Actor.Session.Epoch >= 0 &&
            Sequence > 0 && (uint)Kind >= (uint)CombatCommandKind.Basic && (uint)Kind <= (uint)CombatCommandKind.EnemyChase;
        public CombatCommandKey(CombatActorHandle actor, long sequence, CombatCommandKind kind)
        { Actor = actor; Sequence = sequence; Kind = kind; }
        public bool Equals(CombatCommandKey other) => Actor.Equals(other.Actor) && Sequence == other.Sequence && Kind == other.Kind;
        public override bool Equals(object obj) => obj is CombatCommandKey other && Equals(other);
        public override int GetHashCode() => unchecked((Actor.GetHashCode() * 397 ^ Sequence.GetHashCode()) * 397 ^ (int)Kind);
        public static CombatCommandKind KindFor(CombatButtons button)
        {
            switch (button)
            {
                case CombatButtons.Weapon1Basic: case CombatButtons.Weapon2Basic: case CombatButtons.Basic: return CombatCommandKind.Basic;
                case CombatButtons.Weapon1Special: case CombatButtons.Weapon2Special: case CombatButtons.Special: return CombatCommandKind.Special;
                case CombatButtons.Dash: return CombatCommandKind.Dash;
                case CombatButtons.Jump: return CombatCommandKind.Jump;
                case CombatButtons.Swap: return CombatCommandKind.Swap;
                case CombatButtons.Drop: return CombatCommandKind.Drop;
                default: return CombatCommandKind.None;
            }
        }
        public static CombatCommandKey For(CombatInputSample sample) => new CombatCommandKey(
            new CombatActorHandle(sample.Session, sample.Session.ActorId), sample.Sequence, KindFor(sample.Input.Pressed));
    }

    // First admitted press of each button owns the existing bit-union projection.
    // A reservation copies this key; it does not borrow the next tick's sample.
    public readonly struct CombatCommandOrigins
    {
        readonly CombatInputSession session;
        readonly long basic, special, dash, jump, swap, drop, weapon1Basic, weapon2Basic, weapon1Special, weapon2Special;
        public CombatCommandOrigins(CombatInputSession session, long basic, long special, long dash, long jump, long swap, long drop, long weapon1Basic = 0, long weapon2Basic = 0, long weapon1Special = 0, long weapon2Special = 0)
        { this.session = session; this.basic = basic; this.special = special; this.dash = dash; this.jump = jump; this.swap = swap; this.drop = drop; this.weapon1Basic = weapon1Basic; this.weapon2Basic = weapon2Basic; this.weapon1Special = weapon1Special; this.weapon2Special = weapon2Special; }
        public CombatCommandKey For(CombatButtons button)
        {
            long sequence;
            switch (button)
            {
                case CombatButtons.Weapon1Basic: sequence = weapon1Basic; break;
                case CombatButtons.Weapon2Basic: sequence = weapon2Basic; break;
                case CombatButtons.Weapon1Special: sequence = weapon1Special; break;
                case CombatButtons.Weapon2Special: sequence = weapon2Special; break;
                case CombatButtons.Basic: sequence = basic; break;
                case CombatButtons.Special: sequence = special; break;
                case CombatButtons.Dash: sequence = dash; break;
                case CombatButtons.Jump: sequence = jump; break;
                case CombatButtons.Swap: sequence = swap; break;
                case CombatButtons.Drop: sequence = drop; break;
                default: return default;
            }
            return new CombatCommandKey(new CombatActorHandle(session, session.ActorId), sequence, CombatCommandKey.KindFor(button));
        }
    }

    // Returned only by an actual native operation. Deferred is a jump buffer,
    // not Started; a later landing, expiry or cancellation settles its origin.
    public readonly struct CombatActionAttempt
    {
        public bool Started { get; }
        public bool Deferred { get; }
        public bool Immediate { get; }
        public bool OutcomeRecorded { get; }
        public Guid ExecutionId { get; }
        public CombatCommandReason Reason { get; }
        public CombatActionAttempt(bool started, Guid executionId, CombatCommandReason reason = CombatCommandReason.None, bool deferred = false, bool immediate = false, bool outcomeRecorded = false)
        { Started = started; ExecutionId = executionId; Reason = reason; Deferred = deferred; Immediate = immediate; OutcomeRecorded = outcomeRecorded; }
        public static CombatActionAttempt Reject(CombatCommandReason reason) => new CombatActionAttempt(false, Guid.Empty, reason);
        public static CombatActionAttempt Start(Guid executionId, bool immediate = false, bool outcomeRecorded = false) => new CombatActionAttempt(true, executionId, immediate: immediate, outcomeRecorded: outcomeRecorded);
        public static CombatActionAttempt Buffer() => new CombatActionAttempt(false, Guid.Empty, CombatCommandReason.JumpBuffered, deferred: true);
    }

    public readonly struct CombatAICommand
    {
        public CombatCommandKey Key { get; }
        public EnemyIntent Intent { get; }
        public float ObservedAt { get; }
        public CombatAICommand(CombatActorHandle actor, long sequence, EnemyIntent intent, float observedAt)
        {
            Key = new CombatCommandKey(actor, sequence, intent.Kind == EnemyIntentKind.BeginAttack ? CombatCommandKind.EnemyAttack :
                intent.Kind == EnemyIntentKind.Chase ? CombatCommandKind.EnemyChase : CombatCommandKind.None);
            Intent = intent; ObservedAt = observedAt;
        }
    }
    public readonly struct CombatAICommandResult
    {
        public CombatActionAttempt Attempt { get; }
        public float Horizontal { get; }
        public CombatAICommandResult(CombatActionAttempt attempt, float horizontal = 0) { Attempt = attempt; Horizontal = horizontal; }
    }

    public readonly struct CombatCommandOutcome
    {
        public long EventSequence { get; }
        public long TickCount { get; }
        public float Now { get; }
        public CombatCommandKey Command { get; }
        public CombatCommandState State { get; }
        public CombatCommandReason Reason { get; }
        public string Detail { get; }
        public Guid ExecutionId { get; }
        public ActorReadSnapshot Snapshot { get; }
        public CombatCommandOutcome(long eventSequence, long tickCount, float now, CombatCommandKey command,
            CombatCommandState state, CombatCommandReason reason, Guid executionId, ActorReadSnapshot snapshot, string detail = null)
        {
            EventSequence = eventSequence; TickCount = tickCount; Now = now; Command = command;
            State = state; Reason = reason; ExecutionId = executionId; Snapshot = snapshot; Detail = detail;
        }
    }
    public readonly struct CombatOutcomeCursor
    {
        public CombatInputSession Session { get; }
        public long AfterEventSequence { get; }
        public CombatOutcomeCursor(CombatInputSession session, long afterEventSequence = 0)
        { Session = session; AfterEventSequence = afterEventSequence; }
    }
    public readonly struct CombatOutcomePage
    {
        public CombatOutcomeCursor Next { get; }
        public int Count { get; }
        public long DroppedBefore { get; }
        public long LatestEventSequence { get; }
        public CombatOutcomePage(CombatOutcomeCursor next, int count, long droppedBefore, long latestEventSequence)
        { Next = next; Count = count; DroppedBefore = droppedBefore; LatestEventSequence = latestEventSequence; }
    }

    // Admission is unchanged and is never an execution receipt. Continuous and
    // release samples have admission/capture only; outcomes describe actions.
    // Polling copies values into caller-owned storage, never invokes callbacks.
    // A reset requires explicit cursor rebinding; retained events keep old keys.
    public interface ICombatCommands
    {
        CombatInputAdmission SubmitPlayerInput(CombatInputSample sample);
        CombatAICommandResult SubmitAIIntent(CombatAICommand command);
        bool TryReadOutcomes(CombatOutcomeCursor cursor, CombatCommandOutcome[] destination, out CombatOutcomePage page);
    }

    public interface ICombatAICommandHost
    {
        Guid BeginAttack(CombatCommandKey command);
        void SyncFacing(int direction);
        int Facing { get; }
        bool Grounded { get; }
        bool HasChaseFloor();
    }
}
