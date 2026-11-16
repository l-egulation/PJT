using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Stage1Monsters.Game;
using ForestCombatV2;
using UnityEngine;
namespace Deviant.Chapter2
{
    public sealed class Chapter2NormalEnemy : FieldMouseEnemy, ICustomPlayerHitReactionSource
    {
        public string ArtId {get;private set;}
        public C2ApprovedNormal Settings {get;private set;}
        public Chapter2NormalPresentation Presentation {get;private set;}
        public bool Attacking {get;private set;}
        public float AttackAge=>arena?Mathf.Max(0,arena.Now-began):0;
        public override void DelayRunInkMotion(float seconds) { if (Attacking) began += seconds; }
        public float BodyAge=>ArtId=="B3"?Mathf.Max(0,AttackAge-chargeStopAge):AttackAge;
        public bool Charging=>Attacking&&ArtId=="B3"&&AttackAge>=Settings.windup&&!chargeStopped;
        public float ChargeStopAge=>chargeStopAge;
        public float ChargeTravelled {get;private set;}
        public bool Carrying=>carried;
        public Bounds DamageBounds {get;private set;}
        public bool DamageActive {get;private set;}
        public int HitsDelivered {get;private set;}
        public int AttacksStarted {get;private set;}
        public float ReadyAt=>ready;
        public Vector2 LockedGround {get;private set;}
        public Vector2 ProjectilePosition=>ordnance?ordnance.LatestPosition(this):Vector2.zero;
        public bool ProjectileFlying=>ordnance&&ordnance.HasFlying(this);
        public float ImpactAt {get;internal set;}=float.NegativeInfinity;
        public Camera PresentationCamera {get;set;}
        public bool OverridesPlayerHitReaction=>DedicatedReaction;
        internal bool DedicatedReaction;
        public bool DeathHandled {get;private set;}
        protected override bool CancelsAttackOnIncomingHit=>ArtId!="H3";
        protected override float HitStaggerSeconds=>.3f;
        Rect room;Func<Vector2,bool> contains;
        float began,ready,chargeStopAge,regionMin,regionMax,regionY,carryOffset,carryOldLock,carryUntil;Rect[] doorZones;
        bool entered,wasAggressive,launched,chargeStopped,chargeBegun;int direction;
        CombatActor carried;
        Chapter2NormalOrdnance ordnance;System.Random random;
        readonly HashSet<long> struck=new HashSet<long>();
        readonly Dictionary<int,int> sprayCounts=new Dictionary<int,int>();
        readonly Dictionary<int,float> sprayReady=new Dictionary<int,float>();
        // 열쇠 운반자로 뽑히면 켠다(Chapter2GimmickHost.BindCarrier).
        public bool FollowsMovementOverride {get;set;}
        public static Chapter2NormalEnemy Create(Component parent,CombatArena owner,CombatEnemySpawn request,string species,FieldMouseDefinition original,StaticArenaSurfaceIndex surfaces)
        {
            var go=new GameObject("Chapter2 "+Chapter2MonsterArt.ArtId(species));go.transform.SetParent(parent.transform,false);
            var e=go.AddComponent<Chapter2NormalEnemy>();e.ArtId=Chapter2MonsterArt.ArtId(species);e.Settings=C2ApprovedNormalBank.Load().Find(e.ArtId);
            var definition=JsonUtility.FromJson<FieldMouseDefinition>(JsonUtility.ToJson(original));
            definition.maxHP=e.Settings.hp;definition.walkSpeed=e.Settings.moveSpeed;definition.stagger=.3f;definition.hitStaggerImmune=false;
            definition.recovery=e.Settings.cooldown;definition.senseX=e.Settings.startRange;definition.startGap=e.Settings.startRange;
            definition.hurtSize=new[]{e.Settings.hurt.Size.x,e.Settings.hurt.Size.y};definition.hurtOffset=new[]{e.Settings.hurt.FacingOffset.x,e.Settings.hurt.FacingOffset.y};
            e.SurfaceIndex=surfaces;e.InitializeSpecies(owner,request.Position,definition);e.EnablePursuitFor(e.ArtId);e.spawn=request.Position;e.profile=request.Profile;
            if(e.Alert!=null)e.Alert.Receives=()=>e.InDetectionArea(owner.Player);
            e.random=new System.Random(unchecked((int)(request.Position.x*719+request.Position.y*3571)+e.ArtId.GetHashCode()));
            var world=parent.GetComponent<Chapter2World>();e.PresentationCamera=world?world.sceneCamera:Camera.main;
            e.ordnance=Chapter2NormalOrdnance.Get(owner);
            var child=new GameObject("Approved chapter2 body");child.transform.SetParent(go.transform,false);e.visual=child.AddComponent<SpriteRenderer>();
            e.visual.sharedMaterial=owner.spriteMaterial;e.visual.sortingOrder=request.SortingOrder;
            e.Presentation=go.AddComponent<Chapter2NormalPresentation>();e.Presentation.Bind(e);
            Chapter1EnemyRole.Mark(e,Chapter1EnemyRole.Normal);e.ResetEnemy();e.aggressive=request.Aggressive;return e;
        }
        // doors: 출입구 안쪽 접근 구역. 같은 바닥 높이의 구역은 이동 영역에서 스폰 반대쪽으로 잘라 낸다.
        // detectsIn: 발견 영역(소유 방). 플레이어가 이 안에 있을 때만 직접 발견하거나 연쇄 경보를 받는다. 생략하면 제한 없음.
        public void BindEncounter(Rect bounds,Func<Vector2,bool> containsTarget,Rect[] doors=null,Func<Vector2,bool> detectsIn=null){room=bounds;contains=containsTarget;doorZones=doors;detectionArea=detectsIn;FindRegion();}
        Func<Vector2,bool> detectionArea;
        public bool InDetectionArea(CombatActor target)=>TargetEligible(target)&&(detectionArea==null||detectionArea(target.Position));
        void FindRegion()
        {
            regionY=spawn.y;regionMin=spawn.x-size.x/2;regionMax=spawn.x+size.x/2;
            if(!ordnance||!ordnance.TryFloor(spawn+Vector2.up*.12f,out var floor,out var point))return;
            regionY=point.y;var b=floor.GetComponent<Collider2D>().bounds;regionMin=b.min.x;regionMax=b.max.x;
            bool changed=true;var all=FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None);
            while(changed){changed=false;foreach(var s in all){var c=s.GetComponent<Collider2D>();if(!c||Mathf.Abs(c.bounds.max.y-regionY)>.02f)continue;var x=c.bounds;if(x.min.x>regionMax+.02f||x.max.x<regionMin-.02f)continue;float l=Mathf.Min(regionMin,x.min.x),r=Mathf.Max(regionMax,x.max.x);if(l!=regionMin||r!=regionMax){regionMin=l;regionMax=r;changed=true;}}}
            if(room.width>0){regionMin=Mathf.Max(regionMin,room.xMin);regionMax=Mathf.Min(regionMax,room.xMax);}
            if(doorZones!=null)foreach(var d in doorZones)
            {
                if(regionY<d.yMin-.6f||regionY>d.yMin+.6f||d.xMax<=regionMin||d.xMin>=regionMax)continue;
                if(d.center.x<spawn.x)regionMin=Mathf.Max(regionMin,d.xMax);else regionMax=Mathf.Min(regionMax,d.xMin);
            }
        }
        public void SetEngaged(bool value)
        {
            aggressive=value;
            if(value&&!wasAggressive){entered=true;ready=Mathf.Max(ready,arena.Now+Mathf.Lerp(.3f,.8f,(float)random.NextDouble()));}
            if(!value&&wasAggressive){CancelAttack();entered=false;}wasAggressive=value;
        }
        // 플레이어가 소유 방에 새로 들어오면 공격 중이 아닌 개체만 첫 공격 대기를 한 번 다시 뽑는다.
        public void RollEntryDelay(){if(!aggressive||Attacking||Dead||Settings==null)return;ready=Mathf.Max(ready,arena.Now+Mathf.Lerp(.3f,.8f,(float)random.NextDouble()));}
        public override float Telegraph=>Attacking?Mathf.Clamp01(AttackAge/Settings.windup):-1;
        public override void ResetEnemy()
        {
            if(Settings==null)return;ReleaseCarry();base.ResetEnemy();
            Attacking=DamageActive=launched=chargeStopped=DeathHandled=entered=wasAggressive=false;
            ImpactAt=float.NegativeInfinity;struck.Clear();sprayCounts.Clear();sprayReady.Clear();HitsDelivered=AttacksStarted=0;
            ready=arena.Now;chargeStopAge=Settings.windup+Settings.chargeSeconds;ChargeTravelled=0;
            if(ordnance)ordnance.ClearOwner(this);FindRegion();if(Presentation)Presentation.ResetPose();
        }
        public override void CancelAttack()
        {
            base.CancelAttack();ReleaseCarry();Attacking=DamageActive=false;struck.Clear();
            if(arena&&Settings!=null)ready=arena.Now+Settings.cooldown;
        }
        protected override void OnAcceptedHit(CombatActor source,int hitDirection)
        {
            if(Dead){CancelAttack();if(ArtId=="H3"&&ordnance)ordnance.ClearOwner(this);DeathHandled=true;return;}
            staggerUntil=Mathf.Max(staggerUntil,arena.Now+.3f);if(ArtId!="H3")CancelAttack();
        }
        protected override void BeginAttackExecution()
        {
            // The shared command host records execution IDs; this species owns eligibility
            // and its clock, rather than the chapter-one brain's observed attack request.
            began=arena.Now;Attacking=true;launched=chargeStopped=chargeBegun=false;direction=facing;
            chargeStopAge=Settings.windup+Settings.chargeSeconds;ChargeTravelled=0;ImpactAt=float.NegativeInfinity;
            struck.Clear();sprayCounts.Clear();sprayReady.Clear();AttacksStarted++;
        }
        public bool TargetEligible(CombatActor target)=>target&&!target.Dead&&!target.Hidden&&(contains==null||contains(target.Position));
        // 발견(「!」·추적 시작)은 이 적이 화면 안에 있을 때만 한다. 2챕터 일반 적은 방과 관계없이 교전하므로(contains 가 보스방만 뺀다)
        // 화면 조건이 없으면 맵의 모든 적이 시작하자마자 발견해 길 찾기를 돌리고, 화면 밖 포기·귀가·재발견을 반복했다(2026-11-13 렉).
        // 1챕터 적처럼 화면 안 재발견으로 추적을 잇는다(EnemyPursuit.StepReturn 의 detected 와 같은 뜻). 공격 시작 조건은 그대로다.
        // 발견은 소유 방 안에서만 하며(InDetectionArea), 1스테이지와 같은 시야(감지 거리·높이, 벽과 발판에 가리지 않음)도 필요하다.
        // 발견 전의 근접 이동형은 다가가거나 경로를 찾지 않는다.
        public bool Detects(CombatActor target)=>InDetectionArea(target)&&EnemyPursuit.InView(arena,Center,size)&&Visible(target,true);
        public bool StartEligible(CombatActor target)
        {
            if(!TargetEligible(target)||Mathf.Abs(target.Position.x-Position.x)>Settings.startRange)return false;
            if(!Settings.Ranged&&(Mathf.Abs(target.Position.y-Position.y)>2||arena.Occluded(Center,target.Center,true)))return false;
            if(!VisibleStart())return false;
            return !Settings.Ranged||TryAim(target,out _,out _,out _,out _);
        }
        public bool VisibleStart()
        {
            var camera=PresentationCamera;if(!camera)return false;
            float ahead=ArtId=="M3"?4:ArtId=="B3"?2.8f:Settings.Ranged?Settings.launchX:2.1f;
            var cue=Position+new Vector2((FacingTowardTarget(arena.Player.Position.x))*ahead,Settings.Ranged?Settings.launchY:1.5f);
            var a=camera.WorldToViewportPoint(cue);var b=camera.WorldToViewportPoint(Settings.hurt.At(Position,facing).center);
            return a.z>0&&a.x>=0&&a.x<=1&&a.y>=0&&a.y<=1&&b.z>0&&b.x>=0&&b.x<=1&&b.y>=0&&b.y<=1;
        }
        public bool TryAim(CombatActor target,out ArenaSurface floor,out Vector2 point,out Vector2 launch,out C2Arc arc)
        {
            launch=Position+new Vector2(facing*Settings.launchX,Settings.launchY);arc=default;point=default;floor=null;
            return ordnance.TryFloor(target.Position+Vector2.up*.02f,out floor,out point)&&C2Arc.TryCreate(launch,point,Settings.projectileSpeed,Settings.apex,out arc,Settings.minimumFlightSeconds);
        }
        float SafeSpeed(float desired,float dt)
        {
            if(Mathf.Abs(desired)<.001f)return 0;
            float x=Position.x+desired*dt,edge=x+offset.x*facing+Mathf.Sign(desired)*size.x/2;
            if(edge<regionMin+.01f||edge>regionMax-.01f||Mathf.Abs(Position.y-regionY)>.15f)return 0;
            return arena.FindFloor(new Vector2(edge,Position.y+.08f),.2f).HasValue?desired:0;
        }
        void StopCharge(float atAge){if(chargeStopped)return;chargeStopped=true;chargeStopAge=atAge;ReleaseCarry();DamageActive=false;velocity=Vector2.zero;}
        void ReleaseCarry()
        {
            if(!carried)return;
            if(carried.moveLockUntil<=carryUntil+.00001f)carried.moveLockUntil=Mathf.Max(carryOldLock,arena?arena.Now:0);
            carried=null;
        }
        void CarryBy()
        {
            if(!carried)return;if(carried.Dead){ReleaseCarry();return;}
            var next=new Vector2(Position.x+carryOffset,carried.Position.y);
            if(carried.Blocked(next,carried.facing)){StopCharge(AttackAge);return;}
            if(carried.moveLockUntil>carryUntil+.00001f)carryOldLock=Mathf.Max(carryOldLock,carried.moveLockUntil);
            carryUntil=arena.Now+.03f;
            carried.Position=next;carried.VelocityX=0;carried.moveLockUntil=Mathf.Max(carried.moveLockUntil,carryUntil);Physics2D.SyncTransforms();
        }
        public override void Step(float dt)
        {
            if(dt<=0||Settings==null)return;DamageActive=false;
            if(Dead){if(!DeathHandled){if(ArtId=="H3")ordnance.ClearOwner(this);DeathHandled=true;}ReleaseCarry();return;}
            // 열쇠 운반자(stage-gimmicks.md): 공격하지 않고 운반자 경로(MovementOverride)로만 움직인다.
            if(FollowsMovementOverride&&MovementOverride!=null){if(Attacking)CancelAttack();base.Step(dt);return;}
            if(!aggressive){if(Attacking)CancelAttack();entered=wasAggressive=false;return;}
            if(!entered){entered=true;wasAggressive=true;ready=Mathf.Max(ready,arena.Now+Mathf.Lerp(.3f,.8f,(float)random.NextDouble()));}
            bool hurt=arena.Now<stunUntil||arena.Now<staggerUntil||arena.Now<rootUntil;
            if(hurt&&ArtId!="H3"){if(Attacking)CancelAttack();PhysicsStep(dt,0,false);return;}
            if(!Attacking)
            {
                var target=arena.Player;
                // Melee walkers (enemy_pursuit.json) keep chasing once engaged: across floors, without the spawn region.
                // Off screen they give up and walk home; re-detection on screen resumes the hunt.
                // Discovery needs the target in this enemy's own room and in sight; walls and other rooms hide it.
                bool detected=Detects(target);
                if(Pursuit!=null&&Pursuit.StepReturn(spawn,true,detected,hurt,Settings.moveSpeed,dt,Alert,out float home,out _))
                {if(home!=0)SyncFacing(home<0?-1:1);PhysicsStep(dt,home,false);return;}
                if(Alert!=null)
                {
                    // Discovery shows "!" and a reaction pause; the first spotter alerts neighbours in the same room.
                    if(detected)Alert.Spot(arena.Now);
                    Alert.Tick(arena.Now);
                    if(Alert.Reacting(arena.Now)){if(target)SyncFacing(FacingTowardTarget(target.Position.x));PhysicsStep(dt,0,false);return;}
                }
                if(Pursuit!=null&&!Pursuit.Hunting&&(Alert!=null?Alert.Ready(arena.Now):detected))Pursuit.Begin();
                bool hunting=Hunting&&target&&!target.Dead;
                // An undiscovering walker stands still: no approach and no route search toward an unseen target.
                if(!hunting&&(!TargetEligible(target)||Pursuit!=null)){PhysicsStep(dt,0,false);return;}
                if(arena.Now>=ready&&!hurt&&(!hunting||!Pursuit.Busy)&&TargetEligible(target)&&StartEligible(target))
                {SyncFacing(FacingTowardTarget(target.Position.x));SubmitIntent(new EnemyIntent(EnemyIntentKind.BeginAttack,facing,0));}
                else if(hunting)
                {
                    bool hold=target.Hidden||Mathf.Abs(target.Position.x-Position.x)<=Settings.startRange&&Mathf.Abs(target.Position.y-Position.y)<=2;
                    float v=hurt?0:Pursuit.Step(target,target.Position,target.grounded,Settings.moveSpeed,hold,dt);
                    if(v!=0)SyncFacing(v<0?-1:1);
                    PhysicsStep(dt,v,false);return;
                }
                else
                {
                    float speed=0;if(!hurt&&Mathf.Abs(target.Position.x-Position.x)>Settings.startRange){SyncFacing(FacingTowardTarget(target.Position.x));speed=SafeSpeed(facing*Settings.moveSpeed,dt);}
                    PhysicsStep(dt,speed,false);return;
                }
            }
            float a=AttackAge,b=a+dt;
            if(!TargetEligible(arena.Player)){CancelAttack();return;}
            if(ArtId=="B3")StepCharge(a,b,dt);else PhysicsStep(dt,0,false);
            if(!Attacking)return;
            if(ArtId=="K4"&&a<Settings.windup)SyncFacing(FacingTowardTarget(arena.Player.Position.x));
            if(ArtId=="K4"&&a+.00001f>=Settings.windup&&a<Settings.windup+Settings.active-.00001f)
            {
                DamageBounds=Settings.attack.At(Position,facing);DamageActive=true;
                foreach(var target in C2NormalHit.Targets(arena))if(Overlaps(target,DamageBounds)&&!arena.Occluded(Center,target.Center,false))
                {
                    sprayCounts.TryGetValue(target.ActorId,out int count);sprayReady.TryGetValue(target.ActorId,out float next);
                    if(count<Settings.maxHits&&arena.Now+.00001f>=next&&Hit(target,Settings.damage,Settings.playerLock,count,false,true))
                    {sprayCounts[target.ActorId]=count+1;sprayReady[target.ActorId]=arena.Now+Settings.hitInterval;}
                }
            }
            if(ArtId=="M3")
            {
                if(a<Settings.windup)SyncFacing(FacingTowardTarget(arena.Player.Position.x));
                for(int i=0;i<3;i++)if(a+.00001f>=Settings.hitWindowStart[i]&&a<Settings.hitWindowEnd[i]-.00001f){Melee(Settings.attack,Settings.damage,Settings.playerLock,i,true);}
            }
            if(Settings.Ranged&&!launched&&b>=Settings.windup)
            {
                SyncFacing(FacingTowardTarget(arena.Player.Position.x));
                if(!TryAim(arena.Player,out var floor,out var point,out var launch,out var arc)){CancelAttack();return;}
                launched=true;LockedGround=point;ordnance.Launch(this,Settings,launch,point,floor,arc,began+Settings.windup,AttackHitOrigin(0,CombatHitKind.Projectile));
            }
            float end=ArtId=="B3"?chargeStopAge+Settings.slamWindup+Settings.slamActive+Settings.recovery:Settings.windup+Settings.active+Settings.recovery;
            if(Settings.Ranged)end=Settings.windup+Settings.recovery;
            if(b>=end){Attacking=false;DamageActive=false;ReleaseCarry();FinishSpeciesCommand();ready=began+end+Settings.cooldown;}
        }
        void StepCharge(float a,float b,float dt)
        {
            float from=Mathf.Clamp(a-Settings.windup,0,Settings.chargeSeconds),to=Mathf.Clamp(b-Settings.windup,0,Settings.chargeSeconds);
            float delta=!chargeStopped?(to-from)*Settings.chargeSpeed:0;
            if(delta>0&&!chargeBegun){SyncFacing(FacingTowardTarget(arena.Player.Position.x));direction=facing;chargeBegun=true;}
            var old=Position;float speed=delta>0?SafeSpeed(direction*delta/dt,dt):0;
            if(delta>0&&carried&&carried.Blocked(new Vector2(Position.x+direction*delta+carryOffset,carried.Position.y),carried.facing))speed=0;
            PhysicsStep(dt,speed,false);ChargeTravelled+=Mathf.Abs(Position.x-old.x);
            if(delta>0)
            {
                var before=Settings.charge.At(old,direction);var after=Settings.charge.At(Position,direction);DamageBounds=after;DamageActive=!chargeStopped;
                foreach(var p in C2NormalHit.Targets(arena))
                {
                    bool crossed=Overlaps(p,after)||Overlaps(p,before);
                    if(!crossed&&Mathf.Abs(Position.x-old.x)>0)
                        foreach(var c in Physics2D.BoxCastAll(before.center,before.size,0,Vector2.right*direction,Mathf.Abs(Position.x-old.x)))if(c.collider.GetComponentInParent<CombatActor>()==p){crossed=true;break;}
                    long key=HitKey(-1,p);
                    if(!chargeStopped&&!struck.Contains(key)&&crossed&&!arena.Occluded(Center,p.Center,false)&&Hit(p,Settings.chargeDamage,0,0,false,false,true))
                    {
                        struck.Add(key);
                        if(p==arena.Player){carried=p;carryOffset=p.Position.x-Position.x;carryOldLock=p.moveLockUntil;carryUntil=0;}
                    }
                }
                CarryBy();
                if(!chargeStopped&&(speed==0||Mathf.Abs(Position.x-old.x)<delta-.001f))StopCharge(Mathf.Max(Settings.windup,a)+Mathf.Abs(Position.x-old.x)/Settings.chargeSpeed);
            }
            if(!chargeStopped&&b>=Settings.windup+Settings.chargeSeconds)StopCharge(Settings.windup+Settings.chargeSeconds);
            if(chargeStopped){float impact=chargeStopAge+Settings.slamWindup;if(a+.00001f>=impact&&a<impact+Settings.slamActive-.00001f){}if(a+.00001f>=impact&&a<impact+Settings.slamActive-.00001f)Melee(Settings.attack,Settings.damage,Settings.playerLock,0,false);}
        }
        static bool Overlaps(CombatActor target,Bounds b)=>target&&!target.Dead&&!target.Hidden&&target.hurtCollider&&target.hurtCollider.bounds.Intersects(b);
        static long HitKey(int hit,CombatActor target)=>((long)hit<<32)|(uint)target.ActorId;
        void Melee(C2PixelBox box,float damage,float moveLock,int hit,bool small)
        {
            DamageBounds=box.At(Position,facing);DamageActive=true;
            foreach(var target in C2NormalHit.Targets(arena))
            {
                long key=HitKey(hit,target);
                if(struck.Contains(key)||!Overlaps(target,DamageBounds)||arena.Occluded(Center,target.Center,false))continue;
                // A parried hit is consumed too, so one swing is countered once.
                int parries=target.ParryCount;
                if(Hit(target,damage,moveLock,hit,false,small)||target.ParryCount!=parries)struck.Add(key);
            }
        }
        bool Hit(CombatActor target,float damage,float moveLock,int number,bool projectile,bool small,bool noKnock=false)
        {
            bool slam=ArtId=="B3"&&!noKnock;
            bool accepted=C2NormalHit.Apply(this,target,damage,facing,moveLock,small?Settings.pushDistance:0,small?Settings.pushSeconds:0,small||noKnock||slam,AttackHitOrigin(number,projectile?CombatHitKind.Projectile:CombatHitKind.Melee),projectile);
            var policy=arena.PlayerHitReaction;
            if(accepted&&slam&&policy!=null&&policy.enabled&&!target.IsForcedMoving&&!target.Dead&&arena.Now>=target.knockUntil)
            {
                int away=target.Center.x>=Center.x?1:-1;
                float distance=policy.meleeAttackBoxExit?policy.Distance(target.hurtCollider.bounds,DamageBounds,away):policy.minimumDistance;
                target.SetKnockMotion(arena.Now+policy.seconds,away*distance/policy.seconds);
            }
            if(accepted)HitsDelivered++;return accepted;
        }
        internal void CountOrdnanceHit()=>HitsDelivered++;
        void OnDisable(){ReleaseCarry();if(ArtId=="H3"&&ordnance)ordnance.ClearOwner(this);}
        void OnDrawGizmos(){if(arena&&arena.showBoxes&&DamageActive){Gizmos.color=Color.magenta;Gizmos.DrawWireCube(DamageBounds.center,DamageBounds.size);}}
    }
}
