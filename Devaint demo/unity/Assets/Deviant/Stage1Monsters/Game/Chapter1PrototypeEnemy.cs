using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    // Uses approved native presentation with the arena motor and accepted-hit ledger.
    public sealed class Chapter1PrototypeEnemy : FieldMouseEnemy
    {
        public enum Phase { Idle, Telegraph, Charge, Cooldown }
        public Chapter1NewMonsterSpec Spec { get; private set; }
        public Phase State { get; private set; }
        public Vector2 LockedGround { get; private set; }
        public int AttacksStarted { get; private set; }
        public int HitsDelivered { get; private set; }
        public float Travelled { get; private set; }
        public float ReadyAt { get; private set; }
        public Bounds BlastBounds=>new Bounds(LockedGround+Vector2.up*(Spec.blastSize[1]/2), new Vector3(Spec.blastSize[0],Spec.blastSize[1],1));
        public bool AttackInProgress=>State==Phase.Telegraph||State==Phase.Charge;
        public float AttackAge=>Mathf.Max(0,arena.Now-began);
        public override void DelayRunInkMotion(float seconds) { if (AttackInProgress) began += seconds; }
        public float LastImpactAt { get; private set; } = -1;
        public bool ShotLaunched { get; private set; }
        public Camera PresentationCamera=>world?world.sceneCamera:Camera.main;
        public Chapter1NewMonsterPresentation Presentation { get; private set; }
        ForestCombatWorld world;
        Rect? encounterBounds;
        Func<Vector2,bool> containsEncounterTarget;
        Sprite baseSprite;
        SpriteRenderer warning,orb;
        float began,flashUntil,firstDelay;
        bool firstDelayStarted;
        int direction;
        bool resetting;
        readonly HashSet<int> struck=new HashSet<int>();
        static Sprite square;

        public static Chapter1PrototypeEnemy Create(Component parent,CombatArena owner,CombatEnemySpawn request,
            Chapter1NewMonsterSpec spec,StaticArenaSurfaceIndex surfaces)
        {
            var go=new GameObject(spec.name);go.transform.SetParent(parent.transform,false);
            var actor=go.AddComponent<Chapter1PrototypeEnemy>();actor.Spec=spec;actor.SurfaceIndex=surfaces;
            actor.world=parent.GetComponent<ForestCombatWorld>();
            actor.InitializeSpecies(owner,request.Position,spec.definition);actor.EnablePursuitFor(spec.id);
            actor.profile=request.Profile;actor.spawn=request.Position;actor.aggressive=request.Aggressive;
            actor.baseSprite=Resources.Load<Sprite>(spec.resource);
            if(!actor.baseSprite)throw new InvalidOperationException("Missing individual prototype sprite: "+spec.resource);
            var image=new GameObject("Selected base pose - prototype motion");image.transform.SetParent(go.transform,false);
            actor.visual=image.AddComponent<SpriteRenderer>();actor.visual.sprite=actor.baseSprite;
            actor.visual.sharedMaterial=owner.spriteMaterial;actor.visual.sortingOrder=request.SortingOrder;
            actor.warning=actor.MakePart("Telegraph 2x2 / charge lane",new Color(1,.45f,.05f,.28f));
            actor.orb=actor.MakePart("Cosmetic shell / explosion",Color.clear);
            Chapter1EnemyRole.Mark(actor,Chapter1EnemyRole.Normal);actor.ResetEnemy();
            if(spec.id=="draft_06_stationary_turret"||spec.id=="draft_07_heavy_charger")
            {
                actor.Presentation=go.AddComponent<Chapter1NewMonsterPresentation>();
                actor.Presentation.Bind(actor,Chapter1NewMonsterArt.Shared);
            }
            return actor;
        }
        // A later chapter supplies its own room lookup without depending on Chapter1 world coordinates.
        public void BindEncounter(Rect bounds,Func<Vector2,bool> containsTarget)
        {
            encounterBounds=bounds;containsEncounterTarget=containsTarget??throw new ArgumentNullException(nameof(containsTarget));
        }
        public override float Telegraph=>State==Phase.Telegraph?Mathf.Clamp01((arena.Now-began)/Definition.prepare):-1;
        protected override float HitStaggerSeconds=>AttackInProgress?0:base.HitStaggerSeconds;
        protected override void OnAcceptedHit(CombatActor source,int hitDirection)
        {
            // Damage/feedback/knock are already committed by CombatActor.Receive. Keep this execution alive.
            if(!Dead&&AttackInProgress&&arena.Now>=stunUntil)return;
            base.OnAcceptedHit(source,hitDirection);
        }
        public override void ResetEnemy()
        {
            resetting=true;base.ResetEnemy();resetting=false;
            State=Phase.Idle;Travelled=0;AttacksStarted=HitsDelivered=0;struck.Clear();flashUntil=0;
            LastImpactAt=-1;ShotLaunched=false;
            if(Spec!=null){var seed=unchecked(Mathf.RoundToInt(spawn.x*1000)^Mathf.RoundToInt(spawn.y*3100)^(world?world.Data.generationSeed:0));
                firstDelay=Mathf.Lerp(Spec.firstDelayMin,Spec.firstDelayMax,(float)new System.Random(seed).NextDouble());ReadyAt=arena.Now;firstDelayStarted=false;}
            if(visual){visual.sprite=baseSprite;visual.enabled=true;visual.color=Color.white;}
            ClearWarning();
        }
        public override void CancelAttack()
        {
            bool active=AttackInProgress;base.CancelAttack();
            if(active&&!resetting){State=Phase.Cooldown;ReadyAt=Mathf.Max(arena.Now,stunUntil)+Definition.recovery;}
            ClearWarning();
        }
        void Finish()
        {
            FinishSpeciesCommand();State=Phase.Cooldown;ReadyAt=arena.Now+Definition.recovery;ClearWarning();
        }
        public bool TryGround(Vector2 feet,out Vector2 ground)
        {
            ground=feet;float nearest=float.PositiveInfinity;bool found=false;
            var bounds=encounterBounds;
            float distance=bounds.HasValue?Mathf.Max(1,feet.y-bounds.Value.yMin+.1f):32;
            foreach(var hit in Physics2D.RaycastAll(feet+Vector2.up*.02f,Vector2.down,distance))
            {
                if(!hit.collider.GetComponent<ArenaSurface>()||hit.normal.y<.9f||hit.distance>=nearest)continue;
                if(bounds.HasValue&&(hit.point.y<bounds.Value.yMin-.1f||hit.point.y>bounds.Value.yMax+.1f))continue;
                nearest=hit.distance;ground=hit.point;found=true;
            }
            return found;
        }
        public bool TargetEligible(CombatActor target)=>target&&!target.Dead&&!target.Hidden&&(containsEncounterTarget==null||containsEncounterTarget(target.Position))&&
            Vector2.Distance(Position,target.Position)<=Definition.senseX;
        public bool CanBeginCharge(CombatActor target)=>TargetEligible(target)&&
            Mathf.Abs(target.Position.x-Position.x)<=Definition.startGap&&Mathf.Abs(target.Position.y-Position.y)<=Definition.startHeight;
        bool SafeApproach(float dx)
        {
            float left=Position.x+offset.x*facing+dx-size.x*.5f,right=left+size.x;
            var bounds=encounterBounds;
            if(bounds.HasValue&&(left<bounds.Value.xMin+.05f||right>bounds.Value.xMax-.05f))return false;
            return arena.FindFloor(new Vector2(dx>0?right:left,Position.y+.08f),.2f).HasValue;
        }
        bool Begin(CombatActor target)
        {
            if(Spec.ranged&&!TryGround(target.Position,out var at))return false;
            int facingTarget=FacingTowardTarget(target.Position.x);if(!SyncFacing(facingTarget))return false;
            if(Spec.ranged){TryGround(target.Position,out var locked);LockedGround=locked;}
            SubmitIntent(new EnemyIntent(EnemyIntentKind.BeginAttack,facingTarget,0));
            return State==Phase.Telegraph;
        }
        protected override void BeginAttackExecution()
        {
            // This opt-in brain owns its own observed intent; FieldMouseBrain remains for legacy species.
            direction=facing;began=arena.Now;Travelled=0;struck.Clear();State=Phase.Telegraph;AttacksStarted++;
            ShotLaunched=false;LastImpactAt=-1;
        }
        // The scene's activation rule as for FieldMouseEnemy (chapter 1: the player's room and its passage neighbours,
        // the distance rule outside rooms): an idle prototype that was never hit, is not hunting or returning and has
        // no decoy to look at rests in a sleeping room.
        bool Resting=>(OutsideActiveArea!=null||distantIdleRange.x>0)&&arena.Player&&ReceivedHitCount==0&&!Hunting&&
            !(Pursuit!=null&&Pursuit.Returning)&&State==Phase.Idle&&arena.decoys.Count==0&&
            (OutsideActiveArea!=null?OutsideActiveArea(this):BeyondDistantIdleRange());
        public override void Step(float dt)
        {
            if(Spec==null||dt<=0)return;
            if(Dead){CancelAttack();if(visual)visual.enabled=false;return;}
            if(!aggressive){if(AttackInProgress)CancelAttack();PhysicsStep(dt,0,false);return;}
            if(Resting){velocity=Vector2.zero;return;}
            if(arena.Now<stunUntil||(!Spec.ranged&&arena.Now<rootUntil))
            {
                if(AttackInProgress)CancelAttack();
                ReadyAt=Mathf.Max(ReadyAt,Mathf.Max(stunUntil,rootUntil)+Definition.recovery);
                PhysicsStep(dt,0,false);return;
            }
            float now=arena.Now,end=now+dt;
            if(State==Phase.Cooldown&&now>=ReadyAt)State=Phase.Idle;
            var target=arena.Player;
            if(State==Phase.Idle&&!firstDelayStarted&&TargetEligible(target)){firstDelayStarted=true;ReadyAt=now+firstDelay;}
            // The charger keeps hunting once it has detected the target: it routes over platforms between charges.
            // Discovery needs a clear sight line: a wall or platform between them hides the target.
            bool spotted=Pursuit!=null&&target&&!target.Dead&&!target.Hidden&&(containsEncounterTarget==null||containsEncounterTarget(target.Position))&&
                Vector2.Distance(Position,target.Position)<=Definition.senseX*Pursuit.settings.detectionScale&&InSight(target);
            // Off-screen give-up between charges: walk home, re-detection on screen resumes the hunt.
            if(Pursuit!=null&&Pursuit.StepReturn(spawn,State==Phase.Idle||State==Phase.Cooldown,spotted,now<staggerUntil,Definition.walkSpeed,dt,Alert,out float home,out _))
            {if(home!=0)SyncFacing(home<0?-1:1);PhysicsStep(dt,home,false);return;}
            if(Alert!=null)
            {
                // "!" and a reaction pause before hunting; the first spotter alerts its neighbours.
                if(spotted)Alert.Spot(now);
                Alert.Tick(now);
                if(Alert.Reacting(now)){if(target)SyncFacing(FacingTowardTarget(target.Position.x));PhysicsStep(dt,0,false);return;}
            }
            if(Pursuit!=null&&!Pursuit.Hunting&&(Alert!=null?Alert.Ready(now):spotted))Pursuit.Begin();
            if(Hunting&&target&&!target.Dead&&State==Phase.Idle&&now>=staggerUntil&&now>=rootUntil)
            {
                if(now>=ReadyAt&&!Pursuit.Busy&&CanBeginCharge(target))Begin(target);
                else{float v=Pursuit.Step(target,target.Position,target.grounded,Definition.walkSpeed,target.Hidden,dt);
                    if(v!=0)SyncFacing(v<0?-1:1);PhysicsStep(dt,v,false);return;}
            }
            if(State==Phase.Idle&&now>=ReadyAt&&now>=staggerUntil&&TargetEligible(target))
            {
                if(Spec.ranged||CanBeginCharge(target))Begin(target);
                else if(now>=rootUntil){float speed=TargetXAligned(target.Position.x)?0:Mathf.Sign(target.Position.x-Position.x)*Definition.walkSpeed;
                    if(speed!=0)SyncFacing(speed<0?-1:1);PhysicsStep(dt,speed!=0&&SafeApproach(speed*dt)?speed:0,false);return;}
            }
            if(State==Phase.Telegraph&&Spec.ranged&&!ShotLaunched&&end>=began+Spec.windupSeconds)
            {
                // Freeze the target at the first visible warning / muzzle-pop, not at inflation start.
                if(TargetEligible(target)&&TryGround(target.Position,out var ground))LockedGround=ground;
                ShotLaunched=true;
            }
            if(State==Phase.Telegraph&&Spec.ranged&&end>=began+Definition.prepare)
            {
                LastImpactAt=began+Definition.prepare;Explode();Finish();flashUntil=end+.18f;
            }
            if(!Spec.ranged&&State==Phase.Telegraph&&end>=began+Definition.prepare)
            {
                State=Phase.Charge;
            }
            if(State==Phase.Charge)
            {
                float activeDt=Mathf.Min(dt,Mathf.Max(0,end-(began+Definition.prepare)));
                float desired=direction*Mathf.Min(Definition.chargeSpeed*activeDt/dt,(Definition.chargeDistance-Travelled)/dt);
                // Short charge: an edge whose floor below has no route back ends the charge at the edge.
                bool edge=false;if(Pursuit!=null)desired=Pursuit.LimitCharge(desired*dt,Definition.walkSpeed,out edge)/dt;
                Vector2 previous=Position;PhysicsStep(dt,desired,false);
                // Only explicitly bound encounters constrain the charge.
                var bounds=encounterBounds;
                if(bounds.HasValue){float min=bounds.Value.xMin+size.x/2-offset.x*facing,max=bounds.Value.xMax-size.x/2-offset.x*facing;
                    float x=Mathf.Clamp(Position.x,min,max);if(x!=Position.x){Position=new Vector2(x,Position.y);wallHit=true;}}
                Travelled+=Mathf.Abs(Position.x-previous.x);Sweep(previous);
                if(wallHit||edge||Travelled>=Definition.chargeDistance-.001f)Finish();
                return;
            }
            PhysicsStep(dt,0,false);
        }
        void Explode()
        {
            foreach(var target in arena.TargetsInBox(BlastBounds.center,BlastBounds.size))
                Hit(target,target.Center.x<LockedGround.x?-1:1);
        }
        void Sweep(Vector2 previous)
        {
            var delta=Position-previous;var center=previous+new Vector2(Definition.hurtOffset[0]*facing,Definition.hurtOffset[1]);
            var hitSize=new Vector2(Definition.hurtSize[0],Definition.hurtSize[1]);
            foreach(var hit in Physics2D.BoxCastAll(center,hitSize,0,delta.normalized,delta.magnitude))
                Hit(hit.collider.GetComponentInParent<CombatActor>(),direction);
            foreach(var target in arena.TargetsInBox(Position+new Vector2(Definition.hurtOffset[0]*facing,Definition.hurtOffset[1]),hitSize))Hit(target,direction);
        }
        void Hit(CombatActor target,int hitDirection)
        {
            if(!target||target.Dead||target.Hidden||target!=arena.Player&&!target.isDecoy)return;
            if(!Spec.ranged&&arena.Occluded(Center,target.Center,false))return;
            if(!struck.Add(target.ActorId))return;
            if(!DeliverSpeciesHit(target,Definition.damage,0,hitDirection))return;
            HitsDelivered++;
            // The millstone's charge sends the player flying (playtest 2026-11-06): along the charge, up and away.
            if(!Spec.ranged&&Spec.playerLaunchSeconds>0&&target==arena.Player&&!target.Dead)
            {
                var p=arena.Player;p.CancelAction();p.grounded=false;p.VelocityY=Spec.playerLaunchUp;
                p.SetKnockMotion(arena.Now+Spec.playerLaunchSeconds,hitDirection*Spec.playerLaunchSpeed);
            }
        }
        SpriteRenderer MakePart(string label,Color color)
        {
            if(!square){var texture=new Texture2D(1,1);texture.SetPixel(0,0,Color.white);texture.Apply();square=Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);}
            var go=new GameObject(label);go.transform.SetParent(transform,false);var r=go.AddComponent<SpriteRenderer>();
            r.sprite=square;r.sharedMaterial=arena.spriteMaterial;r.sortingOrder=18;r.color=color;r.enabled=false;return r;
        }
        void ClearWarning(){if(warning)warning.enabled=false;if(orb)orb.enabled=false;}
        void LateUpdate()=>DrawPrototype();
        public void DrawPrototype()
        {
            if(Spec==null||Definition==null||!arena)return;
            if(Dead||!aggressive){ClearWarning();if(Presentation)Presentation.Clear();return;}
            if(Presentation){ClearWarning();Presentation.Draw();return;}
            float progress=Telegraph;
            if(visual){visual.transform.localPosition=Vector3.right*(progress>=0?Mathf.Sin(arena.Now*35)*.035f:0);
                visual.color=Color.white;
                visual.transform.localRotation=Quaternion.Euler(0,0,progress>=0?direction*progress*6:State==Phase.Charge?direction*-8:0);}
            warning.enabled=State==Phase.Telegraph;
            if(warning.enabled){var bounds=Spec.ranged?BlastBounds:new Bounds(Center+Vector2.right*direction*Definition.chargeDistance/2,new Vector3(Definition.chargeDistance+size.x,size.y,1));
                warning.transform.position=bounds.center;warning.transform.localScale=bounds.size;
                warning.color=new Color(1,.35f,.05f,.12f+.18f*progress);}
            orb.enabled=Spec.ranged&&(State==Phase.Telegraph||arena.Now<flashUntil);
            if(orb.enabled){bool flash=arena.Now<flashUntil;float t=Mathf.Clamp01(progress);
                orb.transform.position=flash?BlastBounds.center:(Vector3)Vector2.Lerp(Center,LockedGround+Vector2.up,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*3);
                orb.transform.localScale=flash?BlastBounds.size:Vector3.one*.22f;orb.color=flash?new Color(1,.5f,.08f,.6f):new Color(.15f,.15f,.12f);}
        }
    }
}
