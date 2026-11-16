using System;

namespace Deviant.Combat.Contracts
{
    public enum CombatHitKind { Direct, Melee, Projectile, Blast, Fragment, Laser, Missile, Parry, EndPrimary, Independent, InkPart }

    // Captured at execution/emission, never reconstructed from a later currentCast.
    // Source/Command/Execution remain the original emitter after reflection;
    // the reflector is a separate captured cause and EffectiveSource is explicit.
    public readonly struct CombatHitOrigin
    {
        public CombatActorHandle Source { get; }
        public CombatCommandKey Command { get; }
        public Guid ExecutionId { get; }
        public int HitIndex { get; }
        public string HitId { get; }
        public Guid EffectId { get; }
        public int InkGeneration { get; }
        public CombatHitKind Kind { get; }
        public CombatAttackStamp Stamp { get; }
        public string ActionId { get; }
        public bool IsBasicAction { get; }
        public int SourceFacing { get; }
        public CombatActorHandle Reflector { get; }
        public CombatCommandKey ReflectorCommand { get; }
        public Guid ReflectorExecutionId { get; }
        public CombatActorHandle EffectiveSource => Reflector.ActorId > 0 ? Reflector : Source;
        public bool IsSpecified => ExecutionId != Guid.Empty;
        public CombatHitOrigin(CombatActorHandle source, CombatCommandKey command, Guid executionId, int hitIndex,
            string hitId, Guid effectId, CombatHitKind kind, CombatActorHandle reflector = default,
            CombatCommandKey reflectorCommand = default, Guid reflectorExecutionId = default, CombatAttackStamp stamp = default,
            string actionId = null, bool isBasicAction = false, int sourceFacing = 0, int inkGeneration = 0)
        {
            InkGeneration = inkGeneration; Source = source; Command = command; ExecutionId = executionId; HitIndex = hitIndex; HitId = hitId;
            EffectId = effectId; Kind = kind; Reflector = reflector; ReflectorCommand = reflectorCommand; ReflectorExecutionId = reflectorExecutionId;
            Stamp = stamp;
            ActionId = actionId ?? stamp.ActionId; IsBasicAction = isBasicAction || stamp.Basic; SourceFacing = sourceFacing;
        }
        public CombatHitOrigin WithEffect(Guid effectId, CombatHitKind kind) => new CombatHitOrigin(Source, Command, ExecutionId,
            HitIndex, HitId, effectId, kind, Reflector, ReflectorCommand, ReflectorExecutionId, Stamp, ActionId, IsBasicAction, SourceFacing, InkGeneration);
        public CombatHitOrigin WithInkGeneration(int generation) => new CombatHitOrigin(Source, Command, ExecutionId,
            HitIndex, HitId, EffectId, Kind, Reflector, ReflectorCommand, ReflectorExecutionId, Stamp, ActionId, IsBasicAction, SourceFacing, generation);
        public CombatHitOrigin ReflectedBy(CombatHitOrigin reflector) => new CombatHitOrigin(Source, Command, ExecutionId,
            HitIndex, HitId, EffectId, Kind, reflector.Source, reflector.Command, reflector.ExecutionId, Stamp, ActionId, IsBasicAction, SourceFacing, InkGeneration);
    }

    // A committed accepted ApplyDamage, including zero damage/complete absorption.
    // Published before existing control/log/death callbacks; those callbacks may
    // throw or reset without retracting this fact. Not an admission or HP delta.
    public readonly struct CombatAcceptedHit
    {
        public long EventSequence { get; }
        public long TickCount { get; }
        public float Now { get; }
        public CombatHitOrigin Origin { get; }
        public CombatActorHandle Target { get; }
        public float RequestedDamage { get; }
        public float Damage { get; }
        public float Absorbed { get; }
        public ActorVitalsSnapshot Before { get; }
        public ActorVitalsSnapshot After { get; }
        public ActorKinematicsSnapshot TargetPose { get; }
        public CombatHitContact Contact { get; }
        // The player's damage roll doubled this hit. Presentation only reads it; damage is already final.
        public bool Critical { get; }
        public CombatAcceptedHit(long sequence, long tick, float now, CombatHitOrigin origin, CombatActorHandle target,
            float requestedDamage, ActorVitalsSnapshot before, ActorDamageResult applied, ActorKinematicsSnapshot targetPose, CombatHitContact contact = default,
            bool critical = false)
        {
            EventSequence = sequence; TickCount = tick; Now = now; Origin = origin; Target = target;
            RequestedDamage = requestedDamage; Damage = applied.Damage; Absorbed = applied.Absorbed;
            Before = before; After = applied.State; TargetPose = targetPose;
            Contact = contact; Critical = critical;
        }
    }
    public readonly struct CombatHitPage
    {
        public CombatOutcomeCursor Next { get; }
        public int Count { get; }
        public long DroppedBefore { get; }
        public long LatestEventSequence { get; }
        public CombatHitPage(CombatOutcomeCursor next, int count, long droppedBefore, long latest)
        { Next = next; Count = count; DroppedBefore = droppedBefore; LatestEventSequence = latest; }
    }
    public interface ICombatHitEvents
    {
        // Bind at the current end so opening a consumer never replays old flashes.
        bool TryGetCursor(out CombatOutcomeCursor cursor);
        bool TryRead(CombatOutcomeCursor cursor, CombatAcceptedHit[] destination, out CombatHitPage page);
    }
}
