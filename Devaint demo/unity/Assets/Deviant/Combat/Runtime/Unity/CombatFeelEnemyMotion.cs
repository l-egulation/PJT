using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Test-branch enemy reaction drawn on the visual only: heavy hits lift the sprite on a short
    // arc with one small bounce, and every hit leans the sprite back around its foot pivot, holds
    // through hit-stop and springs back. A finishing blow also throws the body back (kill.fly) and the
    // corpse stays there. Collider, hurt box, AI and position stay on the ground.
    // Runs after the monster presenters (650) and the chapter world (500), which reset the visual.
    [DefaultExecutionOrder(700)]
    public sealed class CombatFeelEnemyMotion : MonoBehaviour
    {
        sealed class State { public Transform visual; public float liftY, liftV, recoil, recoilMax, recoilAngle, flyDistance, flyAge; public Vector3 applied; }
        const float FlySeconds = .16f;
        CombatArena arena;
        CombatFeelSettings settings;
        readonly Dictionary<CombatActor, State> states = new Dictionary<CombatActor, State>();
        float lastNow = -1;
        public void Bind(CombatArena owner, CombatFeelSettings configuration) { arena = owner; settings = configuration; }

        public void Hit(CombatActor target, float lift, bool big, int direction, bool killed = false)
        {
            if (!target || target.isPlayer || !target.visual || !killed && !target.AutomaticHitReactionEnabled) return;
            if (!states.TryGetValue(target, out var s)) states[target] = s = new State { visual = target.visual.transform };
            if (killed && settings.kill.fly > 0) { s.flyDistance = settings.kill.fly * direction; s.flyAge = 0; lift = Mathf.Max(lift, settings.kill.lift); }
            if (lift > 0) s.liftV = Mathf.Max(s.liftV, Mathf.Sqrt(2 * settings.liftGravity * lift));
            float angle = big ? settings.recoil.big : settings.recoil.basic, seconds = big ? settings.recoil.bigSeconds : settings.recoil.basicSeconds;
            if (angle > 0 && seconds > 0) { s.recoil = s.recoilMax = seconds; s.recoilAngle = angle * direction; }
        }
        public void Clear()
        {
            foreach (var pair in states) if (pair.Value.visual) Restore(pair.Value);
            states.Clear(); lastNow = -1;
        }
        static void Restore(State s)
        {
            if ((s.visual.localPosition - s.applied).sqrMagnitude < 1e-8f) s.visual.localPosition -= s.applied;
            s.visual.localRotation = Quaternion.identity; s.applied = Vector3.zero;
        }
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (!arena) return;
            float now = arena.Now, dt = lastNow < 0 || now < lastNow ? 0 : now - lastNow; lastNow = now;
            List<CombatActor> done = null;
            foreach (var pair in states)
            {
                var s = pair.Value;
                if (!pair.Key || !s.visual) { (done ??= new List<CombatActor>()).Add(pair.Key); continue; }
                if (s.liftY > 0 || s.liftV > 0)
                {
                    s.liftV -= settings.liftGravity * dt; s.liftY += s.liftV * dt;
                    if (s.liftY <= 0) { float hard = -s.liftV; s.liftY = 0; s.liftV = hard > 7 ? hard * .28f : 0; }   // 세게 떨어지면 한 번 작게 튄다
                }
                s.recoil = Mathf.Max(0, s.recoil - dt); s.flyAge += dt;
                float angle = 0;
                if (s.recoil > 0)
                {
                    float u = 1 - s.recoil / s.recoilMax;
                    angle = u < .2f ? s.recoilAngle : s.recoilAngle * Mathf.Cos((u - .2f) / .8f * Mathf.PI * 1.5f) * (1 - (u - .2f) / .8f);
                }
                // Presenters reset localPosition every frame; if one did not, remove our previous offset first.
                float k = Mathf.Min(1, s.flyAge / FlySeconds), fly = s.flyDistance * (1 - (1 - k) * (1 - k) * (1 - k));
                var offset = new Vector3(Mathf.Round(fly * 32) / 32, Mathf.Round(s.liftY * 32) / 32, 0);
                var basePosition = (s.visual.localPosition - s.applied).sqrMagnitude < 1e-8f ? s.visual.localPosition - s.applied : s.visual.localPosition;
                s.visual.localPosition = basePosition + offset; s.applied = offset;
                // Lean the top away from the hit: hit toward +x tips the sprite clockwise.
                s.visual.localRotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg);
                // A thrown corpse keeps its offset until the actor is revived or removed.
                if (s.liftY <= 0 && s.liftV <= 0 && s.recoil <= 0 && (s.flyDistance == 0 || !pair.Key.Dead)) { Restore(s); (done ??= new List<CombatActor>()).Add(pair.Key); }
            }
            if (done != null) foreach (var actor in done) states.Remove(actor);
        }
    }
}
