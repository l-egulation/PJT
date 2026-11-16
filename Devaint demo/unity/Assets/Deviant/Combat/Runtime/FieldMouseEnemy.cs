using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Deviant.Combat
{
    // Opt-in flat-ground pilot using the shared command and accepted-hit boundaries.
    public class FieldMouseEnemy : TestEnemy
    {
        // A scene-specific brain may finish a command through the same ledger as existing species.
        protected void FinishSpeciesCommand()
        {
            Brain.StopIntoRecovery(arena.Now);
            EndAttackCommand(CombatCommandState.Completed);
        }
        public FieldMouseDefinition Definition { get; private set; }
        public FieldMouseBrain Brain { get; private set; }
        public bool ExternalPresentation { get; private set; }
        // Opt-in scene attacks share sensing, chase, interruption, command IDs and terrain safety.
        public Action<FieldMouseEnemy, float, float> SpeciesAttackStep;
        public Action<FieldMouseEnemy> SpeciesAttackStarting;
        public bool DeliverSpeciesHit(CombatActor target, float damage, int hitNumber)
            => DeliverSpeciesHit(target, damage, hitNumber, facing);
        public bool DeliverSpeciesHit(CombatActor target,float damage,int hitNumber,int direction)
        {
            if(Definition==null||!Definition.noPlayerHitReaction||!target||!target.isPlayer)
                return target.Receive(damage,this,direction,false,null,origin:AttackHitOrigin(hitNumber));
            // Damage only: restore the hit's input lock and knockback (both the base hit and the arena reaction).
            float locked=target.moveLockUntil,knock=target.knockUntil,speed=target.knockSpeed;
            bool accepted=target.Receive(damage,this,direction,false,null,origin:AttackHitOrigin(hitNumber));
            if(accepted&&!target.Dead){target.moveLockUntil=locked;target.SetKnockMotion(knock,speed);}
            return accepted;
        }
        public void FireSpeciesProjectile(Vector2 at, Vector2 speed, float damage, int hitNumber)
            => arena.FireEnemyProjectile(this, at, speed, damage, AttackHitOrigin(hitNumber, CombatHitKind.Projectile));
        public bool DeliverSpeciesProjectileHit(CombatActor target,float damage,int hitNumber)
            => target.Receive(damage,this,facing,false,null,origin:AttackHitOrigin(hitNumber,CombatHitKind.Projectile));
        public float SpeciesAttackAge => (float)(arena.Now - Brain.StartedAt);
        public override void DelayRunInkMotion(float seconds) { base.DelayRunInkMotion(seconds); Brain?.DelayMotion(seconds); }
        public bool SpeciesAttackVisible => !cancelledPresentation && (Brain.Executing || arena.Now < Brain.RecoveryUntil);
        public CombatActor Focus { get; private set; }
        // Scene-owned movement in place of the pilot: no sensing, chasing or attacking. The returned horizontal
        // speed only walks on while the floor continues, unless the actor is airborne (chapter 1 key carrier).
        public Func<FieldMouseEnemy, float, float> MovementOverride;
        // A runner that hits never push (the key carrier, playtest 2026-11-06): knockback is dropped while its override moves it.
        public bool IgnoresHitStagger { get; set; }
        public bool IgnoresKnockback { get; set; }
        // Optional one-step extras the override may set: a controlled vertical speed (wall climbing) and leave to
        // walk off a ledge. Both reset after each step.
        public float? MovementVertical;
        public bool MovementAllowsDrop;
        public StaticArenaSurfaceIndex SurfaceIndex { get; set; }
        // Melee walkers listed in data/combat/enemy_pursuit.json: once they detect a target they keep its position,
        // drop the spawn leash, move faster and route over platforms with jumps, drop-throughs and ledge falls.
        public EnemyPursuit Pursuit { get; private set; }
        // Discovery reaction and "!" chain alert for the receivers listed in enemy_pursuit.json.
        public EnemyAlert Alert { get; private set; }
        public bool Hunting => Pursuit != null && Pursuit.Hunting;
        protected void EnablePursuitFor(string id)
        {
            var settings = EnemyPursuitSettings.Load();
            Pursuit = settings.Applies(id) ? new EnemyPursuit(this, settings, id) : null;
            Alert = settings.AlertsFor(id) ? new EnemyAlert(this, settings, id) : null;
        }
        readonly List<ArenaSurface> pathSurfaces = new List<ArenaSurface>();
        readonly List<Vector2> supportIntervals = new List<Vector2>();
        static readonly Comparison<Vector2> CompareIntervals = (a,b)=>a.x.CompareTo(b.x);
        public const string PilotLimit = "Flat-ground pilot; slopes/steps, ground detours and cross-actor simultaneous events are not implemented/verified.";
        // Authored encounter opt-in; combat definitions and explicitly paused AI are unchanged.
        public bool IdleRoamingEnabled { get; private set; }
        public bool IsIdleRoaming { get; private set; }
        public Vector2 distantIdleRange;
        // Scene activation rule for resting idle monsters (chapter 1: player's room and its passage neighbours).
        // Without it the distance rule applies: farther than distantIdleRange from the player.
        public Func<FieldMouseEnemy, bool> OutsideActiveArea;
        public bool BeyondDistantIdleRange() => distantIdleRange.x > 0 && arena && arena.Player &&
            (Mathf.Abs(Position.x - arena.Player.Position.x) > distantIdleRange.x ||
             Mathf.Abs(Position.y - arena.Player.Position.y) > distantIdleRange.y);
        uint roamSeed, roamRandom;
        float roamRadius, roamSpeedScale, waitMin, waitMax, walkMin, walkMax, roamUntil, roamGoal;
        bool roamWalking;
        public void ConfigureIdleRoaming(int seed,float radius,float speedScale,float minWait,float maxWait,float minWalk,float maxWalk)
        {
            IdleRoamingEnabled=radius>0 && speedScale>0;
            roamSeed=(uint)Math.Max(1,seed);roamRadius=radius;roamSpeedScale=speedScale;
            waitMin=minWait;waitMax=maxWait;walkMin=minWalk;walkMax=maxWalk;
            ResetRoaming();
        }
        void ResetRoaming()
        { roamRandom=roamSeed==0?1:roamSeed;roamWalking=false;IsIdleRoaming=false;roamUntil=0;roamGoal=spawn.x; }
        float RoamRange(float a,float b)
        { roamRandom^=roamRandom<<13;roamRandom^=roamRandom>>17;roamRandom^=roamRandom<<5;return Mathf.Lerp(a,b,(roamRandom&0xffffff)/16777216f); }
        float IdleVelocity(float now,float dt)
        {
            IsIdleRoaming=false;
            if(!IdleRoamingEnabled || seen || detected || Brain.State!=FieldMouseState.Idle ||
                now<rootUntil || now<stunUntil || now<staggerUntil || now<Brain.RecoveryUntil)
            { roamWalking=false;roamUntil=0;return 0; }
            if(roamUntil==0)roamUntil=now+RoamRange(waitMin,waitMax);
            if(now>=roamUntil){
                roamWalking=!roamWalking;
                roamUntil=now+(roamWalking?RoamRange(walkMin,walkMax):RoamRange(waitMin,waitMax));
                if(roamWalking)roamGoal=spawn.x+RoamRange(-roamRadius,roamRadius);
            }
            if(!roamWalking || Mathf.Abs(roamGoal-Position.x)<.04f)return 0;
            float desired=Mathf.Sign(roamGoal-Position.x)*Mathf.Min(Definition.walkSpeed*roamSpeedScale,Mathf.Abs(roamGoal-Position.x)/dt);
            Brain.Look(desired<0?-1:1);SyncFacing(Brain.Facing);IsIdleRoaming=true;return desired;
        }
        Sprite reference, hurt;
        Sprite[] moving, attackingFrames, dying;
        bool seen, detected, targetPath, targetReachable, lastPath, returnPath;
        float nextSense, nextPath, walkClock;
        Vector2 observedTarget, targetWalkGoal, lastWalkGoal;
        public string TrackingStatus => Dead ? "사망" : !aggressive ? "AI 정지" :
            Hunting && !Brain.Executing && arena.Now >= staggerUntil ? "끝까지 추격 · " + Pursuit.Status :
            arena.Now < staggerUntil ? "피격 경직" : Brain.Executing ? "발견 · 공격" :
            Brain.State == FieldMouseState.Return ? "복귀" :
            Brain.State == FieldMouseState.LastKnown ? "시야 상실 · 마지막 위치 확인" :
            Brain.State == FieldMouseState.SearchWait ? (seen ? "추격 포기 대기 · 경로 막힘" : "추격 포기 대기 · 시야 상실") :
            Brain.State == FieldMouseState.Idle ? (seen ? "대기 · 발견했지만 도달 불가" : IsIdleRoaming ? "배회" : "대기 · 대상 안 보임") :
            seen ? "발견 · 추격" : "대상 안 보임";
        double deathAt = double.NaN;
        readonly HashSet<int> struck = new HashSet<int>();
        int ledgerExecution = -1;
        bool cancelledPresentation;
        int incomingFacing;
        public override float Telegraph { get { return Brain != null && Brain.Executing ? Mathf.Clamp01((float)(arena.Now - Brain.StartedAt) / Definition.prepare) : -1; } }
        public void Initialize(CombatArena owner, Vector2 origin)
        {
            Definition = FieldMouseDefinition.Load(); Brain = new FieldMouseBrain(Definition, EnemyPursuitSettings.Load().targetFacingDeadzone);
            Configure(owner, origin, new Vector2(Definition.bodySize[0], Definition.bodySize[1]),
                new Vector2(Definition.bodyOffset[0], Definition.bodyOffset[1]), false);
            spawn = origin; maxHealth = Definition.maxHP;
            reference = Resources.Load<Sprite>("MousePilot/reference");
            hurt = Resources.Load<Sprite>("MousePilot/hurt/01");
            moving = LoadFrames("move", 8); attackingFrames = LoadFrames("attack", 8); dying = LoadFrames("death", 4);
            if (!reference || !hurt) throw new InvalidOperationException("Mouse pilot sprite imports are missing.");
            var child = new GameObject("FieldMouseVisual"); child.transform.SetParent(transform, false);
            visual = child.AddComponent<SpriteRenderer>(); visual.sprite = reference; visual.sortingOrder = 11;
            if (arena.spriteMaterial) visual.sharedMaterial = arena.spriteMaterial;
            ResetEnemy(); Physics2D.SyncTransforms();
            if (!FullSupport(Position, Position)) throw new InvalidOperationException("Mouse initial spawn has no complete flat support.");
        }
        public void InitializeSpecies(CombatArena owner, Vector2 origin, FieldMouseDefinition definition)
        {
            ExternalPresentation = true;
            Definition = definition; Brain = new FieldMouseBrain(definition, EnemyPursuitSettings.Load().targetFacingDeadzone);
            Configure(owner, origin, new Vector2(definition.bodySize[0], definition.bodySize[1]),
                new Vector2(definition.bodyOffset[0], definition.bodyOffset[1]), false);
            spawn = origin; maxHealth = definition.maxHP; EnablePursuitFor(definition.id);
            if (definition.hurtSize != null && definition.hurtSize.Length == 2 &&
                definition.hurtOffset != null && definition.hurtOffset.Length == 2)
                ConfigureHurtbox(new Vector2(definition.hurtSize[0], definition.hurtSize[1]),
                    new Vector2(definition.hurtOffset[0], definition.hurtOffset[1]));
        }
        Sprite[] LoadFrames(string motion, int count)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = Resources.Load<Sprite>("MousePilot/" + motion + "/" + (i + 1).ToString("00"));
                if (!frames[i]) throw new InvalidOperationException("Mouse sprite missing: " + motion + " " + (i + 1));
            }
            return frames;
        }
        protected int FacingTowardTarget(float targetX) => EnemyPursuitSettings.Load().FacingToward(targetX - Position.x, facing);
        protected bool TargetXAligned(float targetX) => EnemyPursuitSettings.Load().TargetAligned(targetX - Position.x);
        public override bool SyncFacing(int direction)
        {
            bool result = base.SyncFacing(direction);
            if (visual) visual.transform.localPosition = Vector3.zero; // Sprite already has its foot pivot.
            return result;
        }
        public override void ResetEnemy()
        {
            if (Brain == null) return;
            base.ResetEnemy(); Brain.Reset(); SyncFacing(1); ResetRoaming(); Pursuit?.Reset(); Alert?.Reset();
            deathAt = double.NaN; nextSense = nextPath = 0; walkClock = 0;
            struck.Clear(); ledgerExecution = -1; Focus = null; seen = detected = false;
            cancelledPresentation = false;
            incomingFacing = 0;
            targetPath=targetReachable=lastPath=returnPath=false;
            targetWalkGoal=lastWalkGoal=spawn;
            if (visual) { visual.color = Color.white; visual.sprite = reference; }
        }
        public override void CancelAttack()
        {
            base.CancelAttack();
            cancelledPresentation = true;
            if (Brain != null && arena)
            {
                if (ExternalPresentation && Definition.chargeDistance > 0 && Brain.Executing) Brain.StopIntoRecovery(arena.Now);
                else Brain.Interrupt(arena.Now);
            }
        }
        protected override float HitStaggerSeconds => IgnoresHitStagger || Definition!=null && Definition.hitStaggerImmune?0:base.HitStaggerSeconds;
        public override bool Immovable => Definition!=null && Definition.immovable;
        protected override void OnAcceptedHit(CombatActor source, int hitDirection)
        {
            base.OnAcceptedHit(source, hitDirection);
            // Ordinary hits keep damage/impact/push, but do not interrupt an armored elite.
            // Explicit stun and lethal damage are still cancelled by the shared damage flow.
            if(!Dead && (IgnoresHitStagger || Definition.hitStaggerImmune))return;
            CancelAttack();
            if (Dead)
            { if (double.IsNaN(deathAt)) deathAt = arena.Now; Brain.Die(); }
            else
            {
                // The pilot's approved stagger also covers accepted special/control hits.
                staggerUntil = Mathf.Max(staggerUntil, arena.Now + Definition.stagger);
                cancelledPresentation = true; Brain.Interrupt(arena.Now);
                if (!seen) incomingFacing = hitDirection < 0 ? 1 : -1;
            }
        }
        protected override void BeginAttackExecution()
        {
            SpeciesAttackStarting?.Invoke(this);
            Brain.BeginAttack(arena.Now);
        }
        protected override bool HasCommandChaseFloor() => true; // SafeVelocity performs the pilot's exact support sweep.
        public bool Visible(CombatActor target, bool initialRange)
        {
            if (!target || target.Dead || target.Hidden) return false;
            Vector2 delta = target.Position - Position;
            float senseX = Definition.senseX * (Pursuit != null ? Pursuit.settings.detectionScale : 1);
            if (Mathf.Abs(FeetHeight(target)-FeetHeight(this)) > Definition.senseY || initialRange && Mathf.Abs(delta.x) > senseX) return false;
            return InSight(target);
        }
        // Sight lines from the body centre to the target's hurt box (centre, top, bottom); walls and platforms block them.
        public bool InSight(CombatActor target)
        {
            if (!target || !target.hurtCollider) return false;
            Bounds b = target.hurtCollider.bounds;
            float x = b.center.x, inset = 1f / 32;
            // Monster sight is blocked by platforms too; attack occlusion keeps its own rules.
            return !arena.Occluded(Center, b.center, true) ||
                !arena.Occluded(Center, new Vector2(x, b.max.y - inset), true) ||
                !arena.Occluded(Center, new Vector2(x, b.min.y + inset), true);
        }
        void Sense()
        {
            using var supportRead = supportProbe.BeginReadBatch();
            CombatActor candidate = Focus;
            if (!candidate || candidate.Dead) candidate = arena.Player;
            // A valid decoy can affect the next target judgement without reaiming an attack.
            if (!Brain.Executing && arena.decoys.Count > 0)
                foreach (var decoy in arena.decoys)
                    if (Visible(decoy, true)) { candidate = decoy; break; }
            if (!candidate || candidate.Dead) candidate = arena.Player;
            Focus = candidate; detected = Visible(candidate, true); seen = Visible(candidate, false);
            if (seen) observedTarget = candidate.Position;
        }
        public bool InsideStartDistance(CombatActor target)
        {
            if (!target || target.Dead) return false;
            Bounds a = bodyCollider.bounds, b = target.bodyCollider.bounds;
            float gap = target.Center.x >= Center.x ? b.min.x - a.max.x : a.min.x - b.max.x;
            // Approved overlap rule: intersecting bodies have zero separation for attack start only.
            // Actual damage still requires the original attack box and its exposure checks.
            return Mathf.Max(0, gap) <= Definition.startGap;
        }
        bool CanStartNow(CombatActor target)
        {
            bool distance = InsideStartDistance(target);
            if (Definition.chargeDistance > 0)
            {
                if (!target || arena.Now < rootUntil) return false;
                float separation = Mathf.Abs(target.Position.x - Position.x);
                int direction = target.Position.x < Position.x ? -1 : 1;
                var destination = Position + Vector2.right * (direction * Definition.chargeDistance);
                distance = separation >= Definition.minimumStartDistance && separation <= Definition.startGap &&
                    (Hunting || Mathf.Abs(destination.x - spawn.x) < Definition.leash) && (Pursuit != null || FlatPath(destination));
            }
            return distance && Mathf.Abs(FeetHeight(target)-FeetHeight(this)) <= Definition.startHeight && Visible(target, false);
        }
        public Bounds AttackBounds
        {
            get { return new Bounds(Position + new Vector2(Definition.attackOffset[0] * facing, Definition.attackOffset[1]),
                                   new Vector3(Definition.attackSize[0], Definition.attackSize[1], 1)); }
        }
        public bool DamageExposed(CombatActor target)
        {
            Bounds a = AttackBounds, b = target.hurtCollider.bounds;
            float left = Mathf.Max(a.min.x, b.min.x), right = Mathf.Min(a.max.x, b.max.x);
            float low = Mathf.Max(a.min.y, b.min.y), high = Mathf.Min(a.max.y, b.max.y);
            if (right <= left || high <= low) return false;
            float inset = Mathf.Min(1f / 32, (high - low) / 4), x = (left + right) / 2;
            for (int i = 0; i < 3; i++)
            {
                float y = i == 0 ? low + inset : i == 1 ? (low + high) / 2 : high - inset;
                if (!arena.Occluded(new Vector2(Center.x, y), new Vector2(x, y))) return true;
            }
            return false;
        }
        // Exact interval coverage for the flat, axis-aligned pilot terrain. No centre-only cliff probe.
        float FeetHeight(CombatActor actor)
        {
            return actor.grounded&&SupportPlane(actor.Position,out float floor,actor.offset.x*actor.facing)?floor:actor.Position.y;
        }
        readonly ArenaSupportProbe supportProbe = new ArenaSupportProbe();
        bool SupportPlane(Vector2 feet,out float height,float centerOffset=float.NaN)
        {
            if(float.IsNaN(centerOffset))centerOffset=offset.x*facing;
            return supportProbe.TryGet(feet,centerOffset,Definition.epsilon,out height);
        }
        public bool FullSupport(Vector2 from, Vector2 to)
        {
            using var supportRead = supportProbe.BeginReadBatch();
            if(!SupportPlane(from,out float floor)||!SupportPlane(to,out float otherFloor)||Mathf.Abs(floor-otherFloor)>Definition.epsilon)return false;
            from.y=to.y=floor;
            float half = size.x / 2, shift = offset.x * facing;
            float left = Mathf.Min(from.x, to.x) + shift - half, right = Mathf.Max(from.x, to.x) + shift + half;
            var intervals = supportIntervals;intervals.Clear();
            var supportArea=new Bounds(new Vector3((left+right)/2,from.y,0),new Vector3(right-left,Definition.epsilon*2,1));
            supportArea.Expand(Definition.epsilon*2);
            foreach (var surface in PathSurfaces(supportArea))
            {
                if (Mathf.Abs(Mathf.DeltaAngle(surface.transform.eulerAngles.z, 0)) > Definition.epsilon) continue;
                var collider = surface.GetComponent<Collider2D>(); if (!collider || !collider.enabled) continue;
                var tiles=surface.GetComponent<Tilemap>();
                if(tiles)
                {
                    var first=tiles.WorldToCell(new Vector3(left,from.y-Definition.epsilon,0));
                    var last=tiles.WorldToCell(new Vector3(right-Definition.epsilon,from.y-Definition.epsilon,0));
                    for(int x=first.x;x<=last.x;x++)
                    {
                        var cell=new Vector3Int(x,first.y,0);
                        if(!tiles.HasTile(cell))continue;
                        Vector3 tileStart=tiles.CellToWorld(cell),tileEnd=tiles.CellToWorld(cell+Vector3Int.one);
                        if(Mathf.Abs(tileEnd.y-from.y)<=Definition.epsilon)intervals.Add(new Vector2(tileStart.x,tileEnd.x));
                    }
                    continue;
                }
                Bounds b = collider.bounds;
                if (Mathf.Abs(b.max.y - from.y) <= Definition.epsilon && b.max.x >= left && b.min.x <= right)
                    intervals.Add(new Vector2(b.min.x, b.max.x));
            }
            intervals.Sort(CompareIntervals);
            float edge = left;
            foreach (var span in intervals)
            { if (span.x > edge + Definition.epsilon) break; edge = Mathf.Max(edge, span.y); if (edge >= right - Definition.epsilon) return true; }
            return false;
        }
        public bool FlatPath(Vector2 destination)
        {
            using var supportRead = supportProbe.BeginReadBatch();
            if (!FullSupport(Position, destination)) return false;
            Vector2 middle = (Position + destination) / 2 + new Vector2(offset.x * facing, offset.y);
            var sweep = new Bounds(middle, new Vector3(Mathf.Abs(destination.x - Position.x) + size.x, size.y, 1));
            // The existing box test uses foot height; tilemaps use the offset sweep.
            // Include both envelopes before applying those unchanged exact tests.
            var candidates=sweep;
            candidates.Encapsulate(new Vector3(sweep.min.x,Position.y,0));
            candidates.Encapsulate(new Vector3(sweep.max.x,Position.y+size.y,0));
            candidates.Expand(Definition.epsilon*2);
            foreach (var surface in PathSurfaces(candidates))
            {
                if (surface.oneWay) continue;
                var collider = surface.GetComponent<Collider2D>(); if (!collider || !collider.enabled) continue;
                var tiles=surface.GetComponent<Tilemap>();
                if(tiles)
                {
                    var first=tiles.WorldToCell(sweep.min+Vector3.one*Definition.epsilon);
                    var last=tiles.WorldToCell(sweep.max-Vector3.one*Definition.epsilon);
                    for(int x=first.x;x<=last.x;x++)for(int y=first.y;y<=last.y;y++)
                        if(tiles.HasTile(new Vector3Int(x,y,0)))return false;
                    continue;
                }
                Bounds b = collider.bounds;
                if (b.max.y > Position.y + Definition.epsilon && b.min.y < Position.y + size.y - Definition.epsilon &&
                    b.max.x > sweep.min.x + Definition.epsilon && b.min.x < sweep.max.x - Definition.epsilon) return false;
            }
            return true;
        }
        List<ArenaSurface> PathSurfaces(Bounds area)
        {
            if(SurfaceIndex!=null)SurfaceIndex.Query(area,pathSurfaces);
            else
            {
                // Non-indexed scenes can stream or move their terrain.
                pathSurfaces.Clear();
                pathSurfaces.AddRange(UnityEngine.Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None));
            }
            return pathSurfaces;
        }
        bool WalkGoal(Vector2 target,out Vector2 goal,out bool reachesTarget)
        {
            goal=Position;reachesTarget=false;
            if(!SupportPlane(Position,out float floor))return false;
            Vector2 projected=new Vector2(target.x,floor);
            bool whole=FlatPath(projected);
            reachesTarget=whole&&SupportPlane(target,out float targetFloor)&&Mathf.Abs(targetFloor-floor)<=Definition.epsilon;
            if(whole)goal=projected;
            else
            {
                float low=0,high=1;
                for(int i=0;i<20;i++)
                {
                    float mid=(low+high)/2;
                    if(FlatPath(new Vector2(Mathf.Lerp(Position.x,target.x,mid),floor)))low=mid;else high=mid;
                }
                Vector2 limit=new Vector2(Mathf.Lerp(Position.x,target.x,low),floor);
                float direction=Mathf.Sign(target.x-Position.x);
                bool cliff=!FullSupport(Position,limit+Vector2.right*(direction*Definition.cliffMargin));
                float margin=cliff?Definition.cliffMargin:Physics2D.defaultContactOffset;
                goal=new Vector2(Position.x+direction*Mathf.Max(0,Mathf.Abs(limit.x-Position.x)-margin),floor);
            }
            return reachesTarget||Mathf.Abs(goal.x-Position.x)>Definition.epsilon;
        }
        float SafeVelocity(float desired, float dt)
        {
            using var supportRead = supportProbe.BeginReadBatch();
            if (desired == 0 || dt <= 0) return 0;
            float delta = desired * dt;
            if (!Brain.Executing && (Brain.State == FieldMouseState.Return || Brain.State == FieldMouseState.LastKnown || Brain.State == FieldMouseState.Chase))
            {
                float goal = Brain.State == FieldMouseState.Return ? spawn.x : Brain.State == FieldMouseState.LastKnown ? lastWalkGoal.x : targetWalkGoal.x;
                delta = Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), Mathf.Abs(goal - Position.x));
            }
            if (!Hunting)
            {
                float boundary = spawn.x + Mathf.Sign(delta) * Definition.leash;
                delta = Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), Mathf.Max(0, (boundary - Position.x) * Mathf.Sign(delta)));
            }
            Vector2 next = Position + Vector2.right * delta;
            if (!FullSupport(Position, next))
            {
                float low = 0, high = 1;
                for (int i = 0; i < 20; i++)
                { float mid = (low + high) / 2; if (FullSupport(Position, Position + Vector2.right * delta * mid)) low = mid; else high = mid; }
                delta = Mathf.Sign(delta) * Mathf.Max(0, Mathf.Abs(delta) * low - Definition.cliffMargin);
            }
            return delta / dt;
        }
        public override void Step(float dt)
        {
            if (MovementOverride == null && (OutsideActiveArea != null || distantIdleRange.x > 0) && arena && arena.Player && !Dead && ReceivedHitCount == 0 && !Hunting &&
                !(Pursuit != null && Pursuit.Returning) &&
                Brain.State == FieldMouseState.Idle && !Brain.Executing && !seen && !detected && arena.decoys.Count == 0 &&
                (OutsideActiveArea != null ? OutsideActiveArea(this) : BeyondDistantIdleRange()))
            {
                IsIdleRoaming = false;
                roamWalking = false;
                velocity = Vector2.zero;
                return;
            }
            try { StepPilot(dt); }
            catch (Exception exception)
            {
                EndAttackCommand(CombatCommandState.Failed, CombatCommandReason.Exception, exception.Message);
                EndChaseCommand(CombatCommandState.Failed, CombatCommandReason.Exception, exception.Message);
                throw;
            }
        }
        void StepPilot(float dt)
        {
            if (Dead) { if (double.IsNaN(deathAt)) deathAt = arena.Now; Brain.Die(); Present(arena.Now, 0); return; }
            float now = arena.Now;
            if (MovementOverride != null)
            {
                IsIdleRoaming=false;roamWalking=false;
                if (Brain.Executing) CancelAttack();
                float want = now < staggerUntil || now < stunUntil ? 0 : MovementOverride(this, dt);
                if (want != 0) SyncFacing(want < 0 ? -1 : 1);
                if (IgnoresKnockback && knockUntil > now) SetKnockMotion(0, 0);
                float? vertical = MovementVertical; bool drop = MovementAllowsDrop;
                MovementVertical = null; MovementAllowsDrop = false;
                if (want != 0 && vertical == null && !drop && grounded && VelocityY <= 0 && !FullSupport(Position, Position + Vector2.right * want * dt)) want = 0;
                Vector2 before = Position;
                PhysicsStep(dt, want, false, vertical);
                EndChaseCommand(CombatCommandState.Completed);
                Present(now + dt, Mathf.Abs(Position.x - before.x));
                return;
            }
            if (!aggressive)
            {
                IsIdleRoaming=false;roamUntil=0;roamWalking=false;
                if (Brain.Executing) CancelAttack();
                PhysicsStep(dt, 0, false);
                EndChaseCommand(CombatCommandState.Completed);
                Present(now + dt, 0);
                return;
            }
            if (now >= nextSense) { Sense(); nextSense = now + Definition.sensePeriod; }
            // Off-screen give-up: walk home at normal speed (teleport when stuck off screen); re-detection on screen resumes.
            if (Pursuit != null && Pursuit.StepReturn(spawn, !Brain.Executing && !Brain.AttackRequested && now >= Brain.RecoveryUntil, detected,
                    now < staggerUntil || now < stunUntil || now < rootUntil, Definition.walkSpeed, dt, Alert, out float home, out bool arrived))
            {
                IsIdleRoaming = false; roamWalking = false;
                if (home != 0) { Brain.Look(home < 0 ? -1 : 1); SyncFacing(Brain.Facing); }
                Vector2 before = Position;
                PhysicsStep(dt, home, false);
                if (arrived) { int face = facing; Brain.Reset(); Brain.Look(face); }
                EndChaseCommand(CombatCommandState.Completed); Present(now + dt, Mathf.Abs(Position.x - before.x));
                return;
            }
            if (Alert != null)
            {
                // Spotting shows "!" and holds still for the reaction time; the first spotter alerts its neighbours.
                if (detected) Alert.Spot(now);
                Alert.Tick(now);
                if (Alert.Reacting(now))
                {
                    IsIdleRoaming = false; roamWalking = false;
                    if (Focus) SyncFacing(FacingTowardTarget(Focus.Position.x));
                    PhysicsStep(dt, 0, false); EndChaseCommand(CombatCommandState.Completed); Present(now + dt, 0);
                    return;
                }
                // An alerted walker without pursuit (the gutter spitter) engages as if it had detected the target.
                if (Pursuit == null && Alert.Ready(now)) detected = true;
            }
            if (Pursuit != null && !Pursuit.Hunting && (Alert != null ? Alert.Ready(now) : detected)) Pursuit.Begin();
            bool hunting = Hunting && Focus && !Focus.Dead;
            if (hunting && !Focus.Hidden) observedTarget = Focus.Position;
            using (supportProbe.BeginReadBatch())
            {
                if (!hunting && now >= nextPath && (seen || detected || Brain.State != FieldMouseState.Idle))
                {
                    targetPath=Focus&&WalkGoal(observedTarget,out targetWalkGoal,out targetReachable);
                    lastPath=WalkGoal(Brain.LastKnown,out lastWalkGoal,out bool unusedLastReachable);
                    returnPath=FlatPath(spawn);
                    nextPath = now + Definition.pathPeriod;
                }
            }
            Brain.StaggerUntil = staggerUntil; Brain.StunUntil = stunUntil; Brain.RootUntil = rootUntil;
            if (Brain.Executing && (now < staggerUntil || now < stunUntil)) CancelAttack();
            MouseObservation o;
            using (supportProbe.BeginReadBatch())
            {
                bool inside = InsideStartDistance(Focus);
                if (Definition.chargeDistance > 0 && Focus) inside = Mathf.Abs(Focus.Position.x - Position.x) <= Definition.startGap;
                bool canAttack = CanStartNow(Focus);
                if (hunting) canAttack = canAttack && !Pursuit.Busy;
                o = new MouseObservation {
                    position = Position, spawn = spawn, targetPosition = observedTarget,
                    detected = detected, visible = seen, pathToTarget = targetPath,
                    canAttack = canAttack, insideStartDistance = inside && Focus && Mathf.Abs(FeetHeight(Focus)-FeetHeight(this))<=Definition.startHeight,
                    atLastKnown = Mathf.Abs(Position.x - Brain.LastKnown.x) <= Definition.epsilon,
                    pathToLastKnown = lastPath, returnPath = returnPath,
                    atSpawn = Mathf.Abs(Position.x-spawn.x)<=Definition.epsilon && FullSupport(Position,spawn),
                    canReengage = detected && Visible(Focus, true) && Mathf.Abs(Position.x - spawn.x) < Definition.leash &&
                        Mathf.Abs(Focus.Position.x - spawn.x) <= Definition.reengageTargetLimit &&
                        (targetReachable || canAttack && now >= Brain.NextAttackAt && now >= Brain.RecoveryUntil)
                };
            }
            if (hunting)
            {
                // Hunting knows the target's position and has no spawn leash; pursuit owns the route.
                o.spawn = Position; o.detected = o.visible = o.pathToTarget = o.pathToLastKnown = o.canReengage = true;
                o.returnPath = true; o.atSpawn = false; o.targetPosition = observedTarget;
            }
            Brain.Tick(now, o, aggressive);
            if (Brain.AttackRequested) SubmitIntent(new EnemyIntent(EnemyIntentKind.BeginAttack, Brain.Facing, 0));
            if (now >= staggerUntil && now >= stunUntil && now >= Brain.RecoveryUntil && !Brain.Executing && incomingFacing != 0)
            { if (!seen) Brain.Look(incomingFacing); incomingFacing = 0; }
            if (now >= Brain.RecoveryUntil && now >= staggerUntil && now >= stunUntil) SyncFacing(Brain.Facing);
            Vector2 previous = Position;
            float desired = Brain.DesiredVelocity;
            bool pursuing = hunting && !Brain.Executing && !Brain.AttackRequested && now >= Brain.RecoveryUntil &&
                now >= staggerUntil && now >= stunUntil;
            if (pursuing)
            {
                desired = Pursuit.Step(Focus, observedTarget, Focus.grounded, Definition.walkSpeed, o.insideStartDistance, dt);
                if (desired != 0) { Brain.Look(desired < 0 ? -1 : 1); SyncFacing(Brain.Facing); }
            }
            float idle=hunting?0:IdleVelocity(now,dt);
            if(desired==0)desired=idle;
            bool charging = Brain.Executing && Definition.chargeDistance > 0;
            if (charging && now < rootUntil)
            { CancelAttack(); charging = false; }
            if (charging)
            {
                double from = Math.Max(0, now - Brain.StartedAt - Definition.prepare);
                double to = Math.Max(0, now + dt - Brain.StartedAt - Definition.prepare);
                desired = facing * (ChargeDistanceAt(to) - ChargeDistanceAt(from)) / dt;
            }
            if (desired != 0 && !charging)
                desired = SubmitIntent(new EnemyIntent(EnemyIntentKind.Chase, desired < 0 ? -1 : 1, Mathf.Abs(desired))).Horizontal;
            // Pursuit walkers make a short charge: it stops at a wall or at an edge with no way back, and runs
            // off an edge whose floor below leads back up. Other species keep the flat-ground cancel rule.
            bool shortCharge = charging && Pursuit != null;
            float safe = pursuing ? desired : shortCharge ? Pursuit.LimitCharge(desired * dt, Definition.walkSpeed, out _) / dt : SafeVelocity(desired, dt);
            if(IsIdleRoaming && Mathf.Abs(safe)<.001f){roamWalking=false;IsIdleRoaming=false;roamUntil=now+RoamRange(waitMin,waitMax);}
            if (charging && !shortCharge && desired != 0 && (Mathf.Abs(safe-desired) > Definition.epsilon || !FlatPath(Position + Vector2.right * safe * dt)))
            { CancelAttack(); safe = 0; }
            PhysicsStep(dt, safe, false);
            if (charging && !shortCharge && wallHit) CancelAttack();
            EndChaseCommand(CombatCommandState.Completed);
            if (!hunting && Mathf.Abs(Position.x - spawn.x) >= Definition.leash)
            { Brain.LeashReached(now); EndAttackCommand(CombatCommandState.Cancelled, CombatCommandReason.Interrupted); }
            if (Brain.Execution != ledgerExecution) { ledgerExecution = Brain.Execution; struck.Clear(); cancelledPresentation = false; }
            if (SpeciesAttackStep != null) SpeciesAttackStep(this, now, dt);
            else if (Brain.ActiveDuring(now, now + dt))
                foreach (var target in arena.TargetsInBox(AttackBounds.center, AttackBounds.size))
                {
                    if (!Brain.ActiveDuring(now, now + dt)) break;
                    if ((target == arena.Player || target.isDecoy) && !target.Dead && !struck.Contains(target.ActorId) && DamageExposed(target))
                    {
                        // A parried hit is consumed too, so one swing is countered once.
                        int parries = target.ParryCount;
                        if (target.Receive(Definition.damage, this, facing, false, null, origin: AttackHitOrigin(0)) || target.ParryCount != parries) struck.Add(target.ActorId);
                    }
                }
            Brain.AdvanceTime(now + dt);
            if (!Brain.Executing && now + dt >= Brain.RecoveryUntil) EndAttackCommand(CombatCommandState.Completed);
            Present(now + dt, Mathf.Abs(Position.x - previous.x));
        }
        void Present(double now, float moved)
        {
            if (ExternalPresentation) return;
            if (!visual) return;
            visual.transform.localPosition = Vector3.zero; visual.transform.localScale = Vector3.one;
            visual.color = Color.white; visual.flipX = facing < 0;
            if (Dead) visual.sprite = dying[Math.Min(3, (int)(Math.Max(0, now - deathAt) / (Definition.deathDuration / 4)))];
            else if (now < staggerUntil) visual.sprite = hurt;
            else if (!cancelledPresentation && (Brain.Executing || now < Brain.RecoveryUntil && double.IsNegativeInfinity(Brain.StartedAt) == false &&
                     Math.Abs(Brain.RecoveryUntil - (Brain.StartedAt + Definition.prepare + Definition.active + Definition.recovery)) < .000001)
                    )
                visual.sprite = attackingFrames[Brain.AttackFrame(now)];
            else if (now < Brain.RecoveryUntil || now < stunUntil) visual.sprite = reference;
            else if (moved > Definition.epsilon)
            { walkClock += moved / Definition.walkSpeed; visual.sprite = moving[(int)(walkClock % Definition.moveCycle / (Definition.moveCycle / 8))]; }
            else visual.sprite = reference;
        }
        float ChargeDistanceAt(double seconds)
        {
            double activeTime = Math.Min(seconds, Definition.active);
            double brakeTime = Math.Min(Math.Max(0, seconds - Definition.active), Definition.brake);
            return (float)(Definition.chargeSpeed * (activeTime + brakeTime -
                (Definition.brake > 0 ? brakeTime * brakeTime / (2 * Definition.brake) : 0)));
        }
        void OnDrawGizmos()
        {
            if (Brain == null || !arena || !arena.showBoxes) return;
            Gizmos.color = Color.red; Gizmos.DrawWireCube(Center, size);
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(spawn, .08f);
            if (Brain.Executing) { Gizmos.color = Color.magenta; Gizmos.DrawWireCube(AttackBounds.center, AttackBounds.size); }
        }
    }
}
