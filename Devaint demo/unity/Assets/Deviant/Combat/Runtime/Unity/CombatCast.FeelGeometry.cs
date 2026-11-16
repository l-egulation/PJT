using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        // The same captured effective hit geometry used by ApplyBox; presentation only.
        internal Bounds FeelPrimaryBounds {
            get {
                if(debugBoxes.Count>0)return debugBoxes[debugBoxes.Count-1];
                var box=meleeHit?.Box ?? (Definition.Hits.Count>0?Definition.Hits[0].Box:null);
                return box==null?new Bounds(owner.Center,Vector3.zero):new Bounds(
                    owner.Position+new Vector2(box.X*direction,box.Y),new Vector3(box.Width,box.Height,0));
            }
        }
        internal int FeelFacing => direction;
        internal Vector2 FeelInstallCenter => installCenter;
        internal float PresentationImpactTime => Definition.Id == "greatsword_follow_up"
            ? lifecycle.Activation : ImpactTime;
        // Tuned basic step (attack_feel_motion.json): the player steps forward on every basic,
        // with or without direction input, easing out until impact, and stops short of the
        // nearest live enemy ahead instead of passing through it. Other casts keep the native advance.
        float FeelStepVelocity(float native)
        {
            var motion = arena.FeelMotion;
            if (!isBasic || !owner || !owner.isPlayer || !owner.grounded || motion == null ||
                !motion.TryGet(Definition.Id, out var step) || step.step <= 0 || dt <= 0 || physicalStep <= 0) return native;
            // Ink changes the action clock, while the motor integrates the real simulation
            // interval. Preserve the authored step distance when windup finishes sooner.
            float impact = Mathf.Max(.05f, meleeAction.Definition.Impact), from = meleeAction.Age(InkNow), to = from + dt;
            float Ease(float t) { float u = Mathf.Clamp01(t / impact); return 1 - (1 - u) * (1 - u); }
            float distance = step.step * (Ease(to) - Ease(from));
            if (distance <= 0) return 0;
            float front = owner.Center.x + direction * owner.size.x * .5f;
            foreach (var enemy in arena.enemies)
            {
                if (!enemy || enemy.Dead || !enemy.isActiveAndEnabled || (enemy.Center.x - owner.Center.x) * direction <= 0) continue;
                float gap = (enemy.Center.x - direction * enemy.size.x * .5f - front) * direction;
                distance = Mathf.Min(distance, Mathf.Max(0, gap - motion.stepGap));
            }
            return direction * distance / physicalStep;
        }
    }
}
