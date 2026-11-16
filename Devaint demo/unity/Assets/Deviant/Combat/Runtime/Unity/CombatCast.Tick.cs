using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        struct CastTickHost : ICastTickHost
        {
            readonly CombatCast cast;
            Bounds smokeArea;
            public CastTickHost(CombatCast cast) { this.cast = cast; smokeArea = default; }
            public void SetStepDuration(float step) => cast.dt = step;
            public bool OwnerExists => cast.owner;
            public bool OwnerDead => cast.owner.Dead;
            public void Dispose() => cast.Dispose();
            public void ClearDebugBoxes() => cast.debugBoxes.Clear();
            public bool HasChargedMelee => cast.ChargedMelee != null;
            public void StepChargedMelee(float step) => cast.StepChargedMelee(step);
            public bool HasMeleeAction => cast.meleeAction != null;
            public void StepMeleeAction(float step) => cast.meleeAction.Step(cast.InkNow, step);
            public bool MeleeActionTerminal => cast.meleeAction.IsTerminal;
            public bool Activated => cast.CastActivated;
            public void StepSkillWave(float step) => cast.StepSkillWave(step);
            public void EvaluateHitTrack(float step)
            {
                var host = new SkillHitTrackHost(cast); cast.hitTrack.Evaluate(cast.Definition.Hits, step, ref host);
            }
            public float Elapsed => cast.Elapsed;
            public bool SmokeIsActive(float elapsed) => cast.smokeEffect.IsActive(elapsed);
            public void CaptureSmokeArea()
            {
                var box = cast.Definition.Authored != null ? cast.Definition.Effect.Box : null;
                smokeArea = box == null ? new Bounds(cast.savedPose + new Vector2(0, 1.5f), new Vector3(6, 3, 1)) :
                    new Bounds(cast.savedPose + new Vector2(box.X * cast.direction, box.Y), new Vector3(box.Width, box.Height, 1));
            }
            public void AddSmokeDebugBox() => cast.debugBoxes.Add(smokeArea);
            public bool SmokeAreaIntersectsOwner => smokeArea.Intersects(new Bounds(cast.owner.Center, cast.owner.size));
            public float Now => cast.arena.Now;
            public bool TryRefreshSmoke(float elapsed, bool ownerOverlaps, float now, float step, out float until)
                => cast.smokeEffect.TryRefresh(elapsed, ownerOverlaps, now, step, out until);
            public float StartedAt => cast.started;
            public float SmokeEnd => cast.smokeEffect.End;
            public void SetOwnerSmokeUntil(float until) => cast.owner.smokeUntil = until;
            public bool MovementDone => cast.forcedDash.MovementDone;
            public void EndPrimary() => cast.EndPrimary();
            public void CommitHitEvaluation(float elapsed) => cast.hitTrack.CommitEvaluation(elapsed);
            public void AfterHits(float step)
            {
                var host = new CastLifecycleHost(cast); cast.lifecycle.AfterHits(step, cast.Definition, cast.charge, ref host);
            }
        }
    }
}
