using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        // Set when this basic's primary hit lands with a recovery-cancel ink (끊어 치기).
        internal bool HitCancelUnlocked;
        internal float HitCancelDamageBonus;
        internal bool InRecovery => ReadActionPhase() == CombatActionReadPhase.Recovery;
        internal ActionReadSnapshot ReadAction()
        {
            var phase = ReadActionPhase();
            return new ActionReadSnapshot(ExecutionId, Definition.Id, DefinitionVersion, ActionTimelineVersion,
                FrameTimingVersion, phase, StartedAt, Age, EndTime);
        }

        CombatActionReadPhase ReadActionPhase()
        {
            if (meleeAction != null)
            {
                switch (meleeAction.State)
                {
                    case MeleeActionState.Preparing: return CombatActionReadPhase.Preparing;
                    case MeleeActionState.Completed: return CombatActionReadPhase.Completed;
                    case MeleeActionState.Cancelled: return CombatActionReadPhase.Cancelled;
                    case MeleeActionState.Failed: return CombatActionReadPhase.Failed;
                    default:
                        return Age < meleeAction.Definition.Impact + meleeAction.Definition.Active
                            ? CombatActionReadPhase.Active : CombatActionReadPhase.Recovery;
                }
            }
            if (CastCancelled) return CombatActionReadPhase.Cancelled;
            if (CastMotionDone) return CombatActionReadPhase.Completed;
            if (Approaching) return CombatActionReadPhase.Approaching;
            if (ChargeActive && !ChargeRelease.HasValue) return CombatActionReadPhase.Charging;
            if (!CastActivated) return CombatActionReadPhase.Preparing;
            if (Definition.Hits != null)
                for (int i = 0; i < Definition.Hits.Count; i++)
                {
                    var hit = Definition.Hits[i];
                    if (hit == null) continue;
                    float start = chargedMeleeExecution?.ImpactTime ?? HitTime(hit);
                    if (Age >= start && Age < start + hit.Active) return CombatActionReadPhase.Active;
                }
            return Age < ImpactTime ? CombatActionReadPhase.Active : CombatActionReadPhase.Recovery;
        }
    }
}
