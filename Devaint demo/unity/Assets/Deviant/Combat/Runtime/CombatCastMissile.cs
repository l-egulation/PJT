using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        MissileExecution missileExecution;
        ref MissileExecution MissilePolicy
        {
            get
            {
                if (!missileExecution.Initialized) missileExecution = new MissileExecution(Definition.Missile, arena.Movement.RiseGravity, arena.Movement.FallGravity);
                return ref missileExecution;
            }
        }
        bool missileVisualHidden;
        SpriteRenderer missileVisual;
        bool ownerVisualEnabled;
        bool MissileEnded => missileExecution.Ended;
        float MissileFlightVelocity() => MissilePolicy.FlightVelocity(direction, dt);
        public bool MissileFlying
        {
            get
            {
                if (Definition.Kind != "missile") return false;
                var host = new MissileHost(this); return MissilePolicy.IsFlying(ref host);
            }
        }
        public bool MissileRecoiling
        {
            get
            {
                if (Definition.Kind != "missile" || !missileExecution.Initialized) return false;
                var host = new MissileHost(this); return missileExecution.IsRecoiling(ref host);
            }
        }
        public float MissileFlightAge => missileExecution.Initialized ? missileExecution.FlightAge(arena.Now) : Mathf.Max(0, arena.Now);
        float missileLaunchX;
        // 발진 위치부터 폭발 위치까지의 수평 비행 거리(비행 거리 비례 피해용).
        float MissileTraveled => owner ? Mathf.Abs(owner.Position.x - missileLaunchX) : 0;
        float MissileDamage => Definition.Missile.DamageAfter(Definition.Hits[0].Damage, MissileTraveled) * damageMultiplier;
        void LaunchMissile()
        {
            missileLaunchX = owner.Position.x;
            MissilePolicy.Launch(arena.Now);
            if (owner == arena.Player) arena.Audio?.StartMissile(this);
            owner.velocity=Vector2.zero;
            if(!(owner==arena.Player && TryApprovedMissileVisual()))
            {
                missileVisual=new GameObject("MissileForm_Temporary").AddComponent<SpriteRenderer>();
                missileVisual.sprite=arena.CircleSprite; missileVisual.color=new Color(.15f,.4f,.8f);
                missileVisual.sortingOrder=30; missileVisual.transform.localScale=new Vector3(1.5f,.6f,1);
                missileVisual.transform.position=owner.Center;
                if(arena.spriteMaterial) missileVisual.sharedMaterial=arena.spriteMaterial;
                var nose=new GameObject("Nose").AddComponent<SpriteRenderer>(); nose.transform.SetParent(missileVisual.transform,false);
                nose.sprite=arena.CircleSprite; nose.color=new Color(1,.55f,.1f); nose.sortingOrder=31;
                nose.transform.localPosition=new Vector3(direction*.35f,0,0); nose.transform.localScale=Vector3.one*.3f;
                if(arena.spriteMaterial) nose.sharedMaterial=arena.spriteMaterial;
            }
            if(owner.visual) { ownerVisualEnabled=owner.visual.enabled; owner.visual.enabled=false; missileVisualHidden=true; }
        }
        // Approved missile form: a comet body that follows the hidden owner, a backblast left at the launch
        // point and fire-trail segments laid every `segment` units of travel. Presentation only.
        bool missileTrailing;
        float missileTrailFrom;
        bool TryApprovedMissileVisual()
        {
            var body=BombSkillFx.Play(arena,"missile_body",owner.Center,direction,BombSkillFx.BodyOrder);
            if(!body) return false;
            missileVisual=body.View;
            BombSkillFx.Play(arena,"missile_launch",owner.Center,direction,BombSkillFx.LaunchOrder);
            missileTrailing=true; missileTrailFrom=owner.Center.x;
            return true;
        }
        void UpdateMissileVisual()
        {
            if(!missileVisual) return;
            Vector2 center=owner.Center;
            missileVisual.transform.position=center;
            if(!missileTrailing) return;
            var trail=BombSkillFx.Find("missile_trail");
            float segment=trail!=null ? trail.data.segment : 0;
            if(segment<=0) return;
            while((center.x-missileTrailFrom)*direction>=segment)
            {
                missileTrailFrom+=segment*direction;
                BombSkillFx.Play(arena,"missile_trail",new Vector2(missileTrailFrom,center.y),direction,BombSkillFx.TrailOrder);
            }
        }
        public bool TryMissileDetonate()
        {
            if (Definition.Kind != "missile") return false;
            var host = new MissileHost(this); return MissilePolicy.TryManualDetonation(ref host);
        }
        void PrepareMissileStep(float step)
        {
            var host = new MissileHost(this); ref var execution = ref MissilePolicy;
            if (!execution.Prepare(step, ref host)) return;
            // The original character body sweeps against both terrain and living enemies.
            // Compare contacts before moving so an enemy behind a wall cannot detonate us beyond it.
            float nearest=execution.Move;
            foreach(var hit in Physics2D.BoxCastAll(owner.Center,owner.size-Vector2.one*.005f,0,Vector2.right*direction,execution.Move))
            {
                var surface=hit.collider.GetComponent<ArenaSurface>();
                var hurt=hit.collider.GetComponent<HurtRegion>();
                bool solid=surface && !surface.oneWay;
                bool enemy=hurt && hurt.actor && hurt.actor!=owner && !hurt.actor.isPlayer && !hurt.actor.isDecoy && !hurt.actor.Dead;
                if(!solid && !enemy) continue;
                // A horizontal cast can report the floor's contact margin at distance zero.
                // Ignore surfaces below the feet, as the character movement sweep does.
                if(solid && hit.collider.bounds.max.y<=owner.Position.y+.02f) continue;
                if(solid && hit.normal.sqrMagnitude>.1f && Vector2.Dot(hit.normal,Vector2.right*direction)>-.1f) continue;
                float distance=Mathf.Max(0,hit.distance-(solid ? .005f : 0));
                if(distance<=nearest) { nearest=distance; execution.Contact(); }
            }
            execution.SetMove(nearest);
        }
        void AfterMissileMovement()
        {
            var host = new MissileHost(this); MissilePolicy.AfterMovement(ref host);
        }
        void ExplodeMissile(Vector2 center)
        {
            ClearMissileVisual();
            if (owner is CombatPlayer emerging) emerging.MissileEmergedAt = arena.Now;
            var seen=new HashSet<int>();
            foreach(var c in Physics2D.OverlapCircleAll(center,Definition.Missile.BlastRadius))
            {
                var h=c.GetComponent<HurtRegion>();
                if(!h || !h.actor || h.actor==owner || h.actor.isPlayer || h.actor.isDecoy || h.actor.Dead || !seen.Add(h.actor.ActorId)) continue;
                Vector2 point=h.actor.hurtCollider.ClosestPoint(center);
                if(Vector2.Distance(center,point)>Definition.Missile.BlastRadius+.0001f || arena.Occluded(center,point)) continue;
                h.actor.Receive(MissileDamage,owner,h.actor.Center.x<center.x ? -1 : 1,false,null, origin: HitOrigin(0, CombatHitKind.Missile), contact: CombatHitContacts.Radial(center,point));
            }
            arena.BombExploded(center,Definition.Missile.BlastRadius,false,null,HitOrigin(0, CombatHitKind.Missile),direction);
        }
        float MissileRecoilVelocity()
        {
            return MissilePolicy.RecoilVelocity(direction, arena.Now, dt);
        }
        void ClearMissileVisual()
        {
            if(missileVisual) { Destroy(missileVisual.gameObject); missileVisual=null; }
            if(missileVisualHidden && owner && owner.visual) owner.visual.enabled=ownerVisualEnabled;
            missileVisualHidden=false; missileTrailing=false;
        }
        struct MissileHost : IMissileExecutionHost
        {
            readonly CombatCast cast;
            Vector2 explosionCenter;
            public MissileHost(CombatCast cast) { this.cast = cast; explosionCenter = default; }
            public bool Activated => cast.CastActivated;
            public bool Cancelled => cast.CastCancelled;
            public bool MotionDone => cast.CastMotionDone;
            public bool OwnerAlive => cast.owner && !cast.owner.Dead;
            public float Now => cast.arena.Now;
            public float StartedAt => cast.started;
            public float StepDuration => cast.dt;
            public float MovedX => cast.owner.Position.x - cast.previousOwner.x;
            public bool WallHit => cast.owner.wallHit;
            public bool Grounded => cast.owner.grounded;
            public void UpdateVisual() => cast.UpdateMissileVisual();
            public void Explode() { explosionCenter = cast.owner.Center; cast.ExplodeMissile(explosionCenter); }
            public void StartHop(float verticalSpeed) { cast.owner.VelocityY = verticalSpeed; cast.owner.grounded = false; }
            public void SetMotionEnd(float elapsed) => cast.lifecycle.SetMotionEnd(elapsed);
            public void RecordEnd() => cast.arena.Record("missile end at=" + explosionCenter + " traveled=" + cast.MissileTraveled + " damage=" + cast.MissileDamage);
        }
    }
}
