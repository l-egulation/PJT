using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        CombatActorHandle hitSource;
        CombatCommandKey hitCommand;
        bool hitSourceCaptured;
        internal CombatHitOrigin HitOrigin(int index, CombatHitKind kind = CombatHitKind.Melee)
        {
            var hit = index == 0 && meleeHit != null ? meleeHit : index == 0 && chargedHit != null ? chargedHit :
                index >= 0 && index < Definition.Hits.Count ? Definition.Hits[index] : null;
            bool independent = hit != null && (hit.TrackAnchor.HasValue
                ? hit.TrackAnchor.Value != Deviant.Definitions.ActionHitAnchor.Actor : hit.Anchor != "actor");
            if (kind == CombatHitKind.Melee && independent) kind = CombatHitKind.Independent;
            return new CombatHitOrigin(hitSource, hitCommand, ExecutionId, index, hit?.Id, independent ? ExecutionId : Guid.Empty, kind,
                stamp: attackStamp, actionId: Definition.Id, isBasicAction: isBasic, sourceFacing: direction);
        }
    }
    public partial class TestEnemy
    {
        CombatCommandKey emittedAttackCommand;
        CombatActorHandle emittedAttackSource;
        protected internal CombatHitOrigin AttackHitOrigin(int index, CombatHitKind kind = CombatHitKind.Melee)
        {
            return new CombatHitOrigin(emittedAttackSource, emittedAttackCommand,
                attackExecution, index, null, Guid.Empty, kind);
        }
    }
}
