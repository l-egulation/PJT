using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        // Target selection may choose one movement anchor, but never limits the
        // victims of an area skill or its already-admitted follow-up.
        readonly List<CombatActor> primaryTargets = new List<CombatActor>();
        bool HasLivingPrimaryTarget
        {
            get { foreach (var target in primaryTargets) if (target && !target.Dead) return true; return false; }
        }
        void RememberPrimaryTarget(int index, CombatActor target)
        {
            if (index != 0 || !(Definition.Effect?.Type == "target_mark" || Definition.Movement?.PrimaryPush == true) || primaryTargets.Contains(target)) return;
            primaryTargets.Add(target);
            // A boss may end a rush inside an entered area sweep. Later victims
            // in that same sweep still receive their one admitted end hit.
            if (Definition.Movement?.PrimaryPush == true && hitResponse.PrimaryEndClaimed)
                ReceivePrimaryEnd(target, Definition.Movement.EndDamage * damageMultiplier);
        }
        void ApplyMarkedTargets(int index)
        {
            if (Definition.Hits[index].Targets != "all_marked")
            { if (marked && !marked.Dead) ApplyTarget(index, Definition.Hits[index], marked); return; }
            foreach (var target in primaryTargets)
                if (target && !target.Dead) ApplyTarget(index, Definition.Hits[index], target);
        }
        void ReceivePrimaryEnd(CombatActor target, float damage)
        {
            if (target && !target.Dead)
                target.Receive(damage, owner, direction, false, null, origin: HitOrigin(-1, CombatHitKind.EndPrimary));
        }
        void ReceivePrimaryEnds(float damage)
        {
            foreach (var target in primaryTargets) ReceivePrimaryEnd(target, damage);
        }
    }
}
