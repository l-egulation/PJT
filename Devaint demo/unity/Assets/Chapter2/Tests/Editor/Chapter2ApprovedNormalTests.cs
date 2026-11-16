using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Chapter2.Editor;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2ApprovedNormalTests
    {
        [TestCase("R-selected",2.1f,3f,1.5f,2f,.9f)]
        [TestCase("H3",3.4f,3.5f,2.5f,2f,.9f)]
        [TestCase("R-selected",2.1f,3f,1.5f,10f,7.9f/6f)]
        [TestCase("H3",3.4f,3.5f,2.5f,11f,7.6f/6f)]
        public void ApprovedProjectilesRespectMinimumFlightAndKeepPeak(string id,float x,float y,float rise,float targetX,float expectedDuration)
        {
            var config=C2ApprovedNormalBank.Load().Find(id);
            Assert.That(config.minimumFlightSeconds,Is.EqualTo(.9f));
            Assert.That(C2Arc.TryCreate(new Vector2(x,y),new Vector2(targetX,0),config.projectileSpeed,rise,out var arc,config.minimumFlightSeconds),Is.True);
            Assert.That(arc.duration,Is.EqualTo(expectedDuration).Within(.0001f));
            Assert.That(Mathf.Abs(arc.horizontal),Is.LessThanOrEqualTo(config.projectileSpeed));
            Assert.That(arc.Sample(arc.duration).x,Is.EqualTo(targetX).Within(.0001f));
            Assert.That(arc.Sample(arc.duration).y,Is.EqualTo(0).Within(.0001f));
            Assert.That(arc.Sample(arc.vertical/arc.gravity).y,Is.EqualTo(y+rise).Within(.0001f));
        }
        [Test] public void MinimumFlightAlsoAppliesToVerticalTossAndRejectsInvalidValues()
        {
            Assert.That(C2Arc.TryCreate(Vector2.zero,Vector2.zero,6,.1f,out var vertical,.9f),Is.True);
            Assert.That(vertical.duration,Is.EqualTo(.9f));Assert.That(vertical.horizontal,Is.Zero);
            Assert.That(vertical.Sample(vertical.duration).y,Is.EqualTo(0).Within(.0001f));
            Assert.That(C2Arc.TryCreate(Vector2.zero,new Vector2(2,3),6,1.5f,out _,.9f),Is.False);
            foreach(var value in new[]{-.1f,float.NaN,float.PositiveInfinity})
                Assert.That(C2Arc.TryCreate(Vector2.zero,Vector2.right,6,1.5f,out _,value),Is.False);
        }
        [Test] public void ApprovedDataAndOriginalSpriteTimings()
        {
            var bank=C2ApprovedNormalBank.Load();var art=Chapter2MonsterArt.Load();
            int[] hp={54,90,54,54,54},ms={2000,1500,1590,1350,2500};string[] ids={"K4","B3","M3","R-selected","H3"};
            for(int i=0;i<ids.Length;i++)
            {
                var c=bank.Find(ids[i]);Assert.That(c.hp,Is.EqualTo(hp[i]));
                var clip=art.Find(ids[i]).Find("attack");Assert.That(clip.DurationMs,Is.EqualTo(ms[i]),ids[i]);
                foreach(var s in clip.frames){Assert.That(s,Is.Not.Null);Assert.That(s.pixelsPerUnit,Is.EqualTo(32));Assert.That(s.rect.width,Is.EqualTo(512));Assert.That(s.pivot,Is.EqualTo(new Vector2(320,124)));}
                var left=c.attack.At(Vector2.zero,-1);var right=c.attack.At(Vector2.zero,1);
                Assert.That(left.center.x,Is.EqualTo(-right.center.x).Within(.00001));Assert.That(left.size,Is.EqualTo(right.size));
            }
            Assert.That(bank.Find("M3").hitWindowStart,Is.EqualTo(new[]{.57f,.72f,.87f}));
        }
        [Test] public void ArcKeepsHorizontalSpeedApexAndLockedEndpoint()
        {
            foreach(float direction in new[]{-1f,1f})foreach(float speed in new[]{6f,12f})foreach(float rise in new[]{1.5f,2.5f})
            {
                Assert.That(C2Arc.TryCreate(new Vector2(0,3),new Vector2(direction*8,0),speed,rise,out var arc));
                Assert.That(arc.Sample(arc.duration).x,Is.EqualTo(direction*8).Within(.0001));Assert.That(arc.Sample(arc.duration).y,Is.EqualTo(0).Within(.0001));
                Assert.That(Mathf.Abs(arc.horizontal),Is.EqualTo(speed).Within(.0001));
                Assert.That(arc.Sample(arc.vertical/arc.gravity).y,Is.EqualTo(3+rise).Within(.0001));
            }
            Assert.That(C2Arc.TryCreate(Vector2.zero,new Vector2(4,3),12,1.5f,out _),Is.False);
            Assert.That(C2Arc.TryCreate(new Vector2(2,3),new Vector2(2,0),12,1.5f,out var vertical),Is.True);
            Assert.That(vertical.horizontal,Is.Zero);Assert.That(vertical.Sample(vertical.duration).y,Is.EqualTo(0).Within(.0001));
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [SetUp] public void PrepareScene()
        {
            if(Application.isPlaying)return;
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter2Tools.ScenePath);
            var initial=Object.FindAnyObjectByType<Chapter2World>();
            Assert.That(initial,Is.Not.Null,"Chapter2 scene world binding");
            initial.seed=17;initial.family="current";
        }
        [UnityTest] public IEnumerator ApprovedFiveSpeciesInActualChapterTwoScene()
        {
            yield return new EnterPlayMode();
            yield return ExerciseApprovedScene();
            yield return new ExitPlayMode();
        }
        // 발견은 소유 방 안에서만: 다른 방·벽 뒤의 플레이어는 발견하지 않고 다가가거나 경로를 찾지 않는다.
        // 같은 방 동료는 벽 너머라도 연쇄 경보를 받지만 다른 방 소유 몬스터는 가까워도 받지 않는다.
        [UnityTest] public IEnumerator WalkersDiscoverOnlyInTheirOwnRoomAndInSight()
        {
            yield return new EnterPlayMode();
            yield return null;yield return null;
            var world=Object.FindAnyObjectByType<Chapter2World>();Assert.That(world!=null&&world.Ready,Is.True,"actual game scene initialized");
            var arena=world.arena;var p=arena.Player;p.UseTestInput=true;p.maxHealth=10000;p.health=10000;
            foreach(var s in world.GetComponent<Chapter2Terrain>().Surfaces){var collider=s.GetComponent<Collider2D>();if(collider)collider.enabled=false;}
            string Owner(Chapter2NormalEnemy e)=>world.Layout.actors.First(a=>world.Actors.TryGetValue(a.id,out var x)&&x==e).roomId;
            var walkers=world.Actors.Values.OfType<Chapter2NormalEnemy>().Where(e=>e.Pursuit!=null&&e.Alert!=null).ToList();
            var subject=walkers.First(e=>walkers.Any(o=>o!=e&&Owner(o)==Owner(e))&&walkers.Any(o=>Owner(o)!=Owner(e)));
            var mate=walkers.First(o=>o!=subject&&Owner(o)==Owner(subject));
            var stranger=walkers.First(o=>Owner(o)!=Owner(subject));
            var home=world.Layout.Room(Owner(subject));
            var elsewhere=world.Layout.rooms.First(r=>r.id!=home.id&&r.id!=Owner(stranger)&&r.id!="B"&&r.id!="E");
            var at=new Vector2(home.x+home.w/2,home.y+2);
            var created=new List<GameObject>();
            GameObject Box(string name,Vector2 center,Vector2 size){var go=new GameObject(name);go.transform.position=center;go.AddComponent<ArenaSurface>();go.AddComponent<BoxCollider2D>().size=size;created.Add(go);return go;}
            Box("Discovery test floor",new Vector2(at.x,at.y-.5f),new Vector2(home.w-1,1));
            // The wall between subject and mate hides the player from the mate, so the mate can only be alerted.
            Box("Discovery test mate wall",at+new Vector2(-1.5f,1.5f),new Vector2(.2f,3));
            Vector2 playerAt=new Vector2(elsewhere.x+elsewhere.w/2,elsewhere.y+2);
            var focus=new[]{subject,mate,stranger};
            Action<float> hold=dt=>{foreach(var enemy in world.Actors.Values)if(enemy)enemy.aggressive=Array.IndexOf(focus,enemy)>=0;p.Position=playerAt;p.velocity=Vector2.zero;};
            arena.BeforePlayerStep+=hold;arena.PlayerStepCompleted+=hold;
            try
            {
                using(var sim=arena.AcquireManualSimulation())
                {
                    void Place(Chapter2NormalEnemy e,Vector2 feet){e.spawn=feet;e.Position=feet;e.ResetEnemy();}
                    Place(subject,at);Place(mate,at+Vector2.left*3);Place(stranger,at+Vector2.left*5);
                    p.ResetPlayer();p.Position=playerAt;Physics2D.SyncTransforms();
                    // All three walkers stay on screen, so only the room and sight rules decide discovery.
                    world.sceneCamera.transform.position=new Vector3(at.x,at.y+4,-10);
                    Assert.That(focus.All(e=>EnemyPursuit.InView(arena,e.Center,e.size)),Is.True,"test walkers are on screen");
                    Assert.That(subject.InDetectionArea(p),Is.False,"a player in another room is outside the discovery area");
                    for(int i=0;i<120;i++)sim.Step(1f/120);
                    foreach(var e in focus)
                    {
                        Assert.That(e.Hunting||e.Alert.Alerted||e.Alert.Pending,Is.False,e.ArtId+" does not discover a player in another room");
                    }
                    Assert.That(subject.Position.x,Is.EqualTo(at.x).Within(.01f),"an undiscovering walker neither approaches nor routes");
                    // Same room, but a wall between: still hidden.
                    playerAt=at+Vector2.right*4;var wall=Box("Discovery test sight wall",at+new Vector2(2,1.5f),new Vector2(.2f,3));
                    p.Position=playerAt;Physics2D.SyncTransforms();
                    Assert.That(subject.InDetectionArea(p),Is.True);Assert.That(subject.Detects(p),Is.False,"walls hide the player");
                    for(int i=0;i<60;i++)sim.Step(1f/120);
                    Assert.That(subject.Hunting||subject.Alert.Alerted,Is.False,"no discovery through a wall");
                    // In sight: the subject spots, its room mate behind a wall is alerted, the other room's walker is not.
                    wall.GetComponent<Collider2D>().enabled=false;Physics2D.SyncTransforms();
                    Assert.That(subject.Detects(p),Is.True,"same room and clear sight");Assert.That(mate.Detects(p),Is.False,"the mate itself cannot see the player");
                    int guard=0;while(!subject.Hunting&&guard++<240)sim.Step(1f/120);
                    Assert.That(subject.Hunting,Is.True,"discovered player is hunted");
                    guard=0;while(!mate.Alert.Alerted&&guard++<240)sim.Step(1f/120);
                    Assert.That(mate.Alert.Alerted,Is.True,"a chain alert reaches a walker of the same room");
                    Assert.That(stranger.Alert.Alerted||stranger.Alert.Pending||stranger.Hunting,Is.False,"a chain alert does not reach another room's walker");
                }
            }
            finally
            {
                arena.BeforePlayerStep-=hold;arena.PlayerStepCompleted-=hold;
                foreach(var go in created)Object.Destroy(go);
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator ExerciseApprovedScene()
        {
            yield return null;yield return null;
            var world=Object.FindAnyObjectByType<Chapter2World>();
            Assert.That(world,Is.Not.Null,"actual game scene world");
            Assert.That(world.Ready,Is.True,"actual game scene initialized");
            Debug.Log("APPROVED_STAGE world-ready");
            var arena=world.arena;Assert.That(arena,Is.Not.Null,"world arena");
            var p=arena.Player;Assert.That(p,Is.Not.Null,"arena player");
            p.UseTestInput=true;p.maxHealth=10000;p.health=10000;
            var terrain=world.GetComponent<Chapter2Terrain>();Assert.That(terrain,Is.Not.Null,"runtime terrain");
            foreach(var s in terrain.Surfaces){var collider=s.GetComponent<Collider2D>();if(collider)collider.enabled=false;}
            Debug.Log("APPROVED_STAGE terrain-ready");
            var floorGo=new GameObject("Approved normal test ground");var testFloor=floorGo.AddComponent<ArenaSurface>();var floorBox=floorGo.AddComponent<BoxCollider2D>();
            Chapter2NormalEnemy subject=null;bool allow=true;int guard;
            Action<float> suppress=dt=>{foreach(var enemy in world.Actors.Values)if(enemy)enemy.aggressive=enemy==subject&&allow;};arena.BeforePlayerStep+=suppress;arena.PlayerStepCompleted+=suppress;
            var output=Path.GetFullPath("../outputs/approved-normal-tests");Directory.CreateDirectory(output);
            using(var sim=arena.AcquireManualSimulation())
            {
                foreach(string id in new[]{"K4","M3","B3","R-selected","H3"})
                {
                    subject=world.Actors.Values.OfType<Chapter2NormalEnemy>().First(e=>e.ArtId==id);
                    var entry=world.Layout.actors.First(e=>world.Actors.TryGetValue(e.id,out var a)&&a==subject);var room=world.Layout.Room(entry.roomId);
                    world.WarpToRoom(room.id);var at=new Vector2(room.x+3.5f,room.y+2);
                    floorGo.transform.position=new Vector2(room.x+room.w/2,at.y-.5f);floorBox.size=new Vector2(room.w-1,1);floorBox.enabled=true;
                    subject.spawn=at;subject.Position=at;subject.ResetEnemy();subject.BindEncounter(room.Bounds,pos=>room.Bounds.Contains(pos));
                    p.ResetPlayer();p.maxHealth=10000;p.health=10000;p.Position=at+Vector2.right*(id=="R-selected"||id=="H3"?8:3);p.velocity=Vector2.zero;
                    Physics2D.SyncTransforms();world.sceneCamera.transform.position=new Vector3(at.x+5,at.y+4,-10);allow=true;
                    float startHP=p.health;guard=0;
                    world.sceneCamera.transform.position+=Vector3.right*100;
                    Assert.That(subject.StartEligible(p),Is.False,id+" cannot start a new offscreen windup");
                    world.sceneCamera.transform.position-=Vector3.right*100;
                    if(!subject.Settings.Ranged){p.Position+=Vector2.up*2.1f;Assert.That(subject.StartEligible(p),Is.False,id+" start-only height gate");p.Position-=Vector2.up*2.1f;Physics2D.SyncTransforms();}
                    while(subject.AttacksStarted==0&&guard++<180)sim.Step(1f/120);
                    Assert.That(subject.AttacksStarted,Is.EqualTo(1),id+" starts after per-entry delay and visible cue");
                    Assert.That(subject.AttackAge,Is.LessThan(.02f));
                    float began=arena.Now;var hitTimes=new List<float>();float last=p.health;
                    bool seenCarry=false,seenDamage=false;int samples=0;
                    float maxRed=0;bool cueFlash=false,cueShake=false;
                    while(subject.Attacking&&samples++<650)
                    {
                        sim.Step(1f/120);seenCarry|=subject.Carrying;seenDamage|=subject.DamageActive;
                        subject.Presentation.Sample();maxRed=Mathf.Max(maxRed,subject.Presentation.CueRedness);cueFlash|=subject.Presentation.CueFlashing;cueShake|=subject.Presentation.CueOffsetPixels!=0;
                        if(p.health<last){hitTimes.Add(arena.Now-began);last=p.health;}
                        if(samples%12==0){subject.Presentation.Sample();yield return null;Capture(world.sceneCamera,Path.Combine(output,id+"-"+(samples/12).ToString("000")+".png"),subject);}
                    }
                    Assert.That(subject.Attacking,Is.False,id+" completes body without waiting for projectile/hazard expiry");
                    // Melee wind-ups shake, redden and flash like the stage 1 melee monsters; throwers do not.
                    bool melee=id=="K4"||id=="M3"||id=="B3";
                    Assert.That(maxRed>.4f&&cueFlash&&cueShake,Is.EqualTo(melee),$"{id} wind-up cue (red {maxRed:0.00}, flash {cueFlash}, shake {cueShake})");
                    subject.Presentation.Sample();Assert.That(subject.Presentation.CueRedness+(subject.Presentation.CueFlashing?1:0),Is.Zero,id+" cue ends with the wind-up");
                    if(id=="K4")Assert.That(startHP-p.health,Is.EqualTo(30).Within(.001),"spray exactly three ten-damage hits");
                    if(id=="M3")
                    {
                        Assert.That(startHP-p.health,Is.EqualTo(15).Within(.001),"three distinct thrust windows");Assert.That(hitTimes.Count,Is.EqualTo(3));
                        Assert.That(hitTimes[1]-hitTimes[0],Is.EqualTo(.15).Within(.012));Assert.That(hitTimes[2]-hitTimes[1],Is.EqualTo(.15).Within(.012));
                    }
                    if(id=="B3")
                    {
                        Assert.That(seenCarry,Is.True,"charge actually carries player");Assert.That(subject.ChargeTravelled,Is.EqualTo(6).Within(.03));
                        Assert.That(subject.Carrying,Is.False);Assert.That(startHP-p.health,Is.EqualTo(26).Within(.001),"8 contact + 18 slam, not continuous contact damage");
                    }
                    Assert.That(subject.ReadyAt-arena.Now,Is.EqualTo(subject.Settings.cooldown).Within(.02),id+" cooldown starts after body recovery");
                    // A slow released can may still be in flight after body recovery.
                    if(subject.Settings.Ranged){for(int i=0;subject.ProjectileFlying&&i<360;i++)sim.Step(1f/120);Assert.That(startHP-p.health,Is.EqualTo(subject.Settings.damage).Within(.001),id+" direct projectile damage once before first puddle tick");}
                    if(id=="K4"||id=="M3"||id=="B3")Assert.That(seenDamage,Is.True,id+" actual active window");
                    Capture(world.sceneCamera,Path.Combine(output,id+"-body-complete.png"),subject);
                    Debug.Log("APPROVED_NORMAL_PASS "+id+" hits="+subject.HitsDelivered+" playerDamage="+(startHP-p.health));
                    allow=false;subject.aggressive=false;Chapter2NormalOrdnance.Get(arena).Clear();
                }
                // One-way nearest floor wins; other walls and floors are not projectile blockers.
                var manager=Chapter2NormalOrdnance.Get(arena);subject=world.Actors.Values.OfType<Chapter2NormalEnemy>().First(e=>e.ArtId=="H3");allow=false;
                var owner=world.Layout.actors.First(e=>world.Actors.TryGetValue(e.id,out var a)&&a==subject);var home=world.Layout.Room(owner.roomId);world.WarpToRoom(home.id);
                var baseAt=new Vector2(home.x+4,home.y+2);floorGo.transform.position=new Vector2(home.x+home.w/2,baseAt.y-.5f);floorBox.size=new Vector2(home.w-1,1);floorBox.enabled=true;
                subject.spawn=baseAt;subject.ResetEnemy();subject.BindEncounter(home.Bounds,pos=>home.Bounds.Contains(pos));
                p.ResetPlayer();p.maxHealth=10000;p.health=10000;p.Position=baseAt+Vector2.right*3;p.velocity=Vector2.zero;Physics2D.SyncTransforms();
                for(int i=0;i<10;i++)sim.Step(1f/120);
                var puddleDecoy=SpawnDecoy(arena,p.Position);puddleDecoy.grounded=true;
                float before=p.health;manager.AddHazard(subject,subject.Settings,p.Position,arena.Now,default,1);manager.AddHazard(subject,subject.Settings,p.Position,arena.Now,default,1);
                for(int i=0;i<73;i++)sim.Step(1f/120);
                Assert.That(before-p.health,Is.EqualTo(5).Within(.001),"overlapping hazards share one player tick and delay first damage");
                AssertDecoyHits(puddleDecoy,1,3);
                RemoveDecoy(arena,puddleDecoy);
                before=p.health;
                for(int i=0;i<70;i++){p.Position=new Vector2(p.Position.x,baseAt.y+3);p.velocity=Vector2.zero;p.grounded=false;sim.Step(1f/120);}
                Assert.That(p.health,Is.EqualTo(before),"airborne player immune to puddles");
                for(int i=0;i<5;i++)manager.AddHazard(subject,subject.Settings,baseAt+Vector2.right*3,arena.Now,default,1);
                Assert.That(manager.HazardCount,Is.GreaterThanOrEqualTo(5),"no per-caster cap");
                // Non-lethal H3 hit stops movement but keeps the body command alive.
                allow=true;p.Position=baseAt+Vector2.right*7;subject.aggressive=true;world.sceneCamera.transform.position=new Vector3(baseAt.x+5,baseAt.y+4,-10);
                guard=0;while(!subject.Attacking&&guard++<200)sim.Step(1f/120);Assert.That(subject.Attacking,Is.True);
                subject.Receive(1,p,1,true,null,true);Assert.That(subject.Attacking,Is.True,"H3 non-lethal basic hit does not cancel formation");
                float ageBefore=subject.AttackAge;for(int i=0;i<20;i++)sim.Step(1f/120);Assert.That(subject.AttackAge,Is.GreaterThan(ageBefore));
                subject.Receive(99999,p,1,false,null,true);Assert.That(manager.HazardCount,Is.Zero,"H3 death clears every owned hazard");Assert.That(manager.HasFlying(subject),Is.False);
                // A released R can remains an arena-owned shot after body death and ignores unrelated terrain.
                subject=world.Actors.Values.OfType<Chapter2NormalEnemy>().First(e=>e.ArtId=="R-selected");owner=world.Layout.actors.First(e=>world.Actors.TryGetValue(e.id,out var a)&&a==subject);home=world.Layout.Room(owner.roomId);world.WarpToRoom(home.id);
                baseAt=new Vector2(home.x+4,home.y+2);floorGo.transform.position=new Vector2(home.x+home.w/2,baseAt.y-.5f);floorBox.size=new Vector2(home.w-1,1);floorBox.enabled=true;
                subject.spawn=baseAt;subject.ResetEnemy();subject.BindEncounter(home.Bounds,pos=>home.Bounds.Contains(pos));p.Position=baseAt+Vector2.right*8;p.velocity=Vector2.zero;subject.aggressive=true;allow=true;Physics2D.SyncTransforms();world.sceneCamera.transform.position=new Vector3(baseAt.x+5,baseAt.y+4,-10);
                guard=0;while(!subject.ProjectileFlying&&guard++<400)sim.Step(1f/120);Assert.That(subject.ProjectileFlying,Is.True);
                var shieldDecoy=SpawnDecoy(arena,subject.ProjectilePosition-Vector2.up);
                sim.Step(1f/120);
                AssertDecoyHits(shieldDecoy,1,8);
                Assert.That(manager.HasFlying(subject),Is.False,"a can consumed by a decoy does not pass through");
                RemoveDecoy(arena,shieldDecoy);subject.ResetEnemy();subject.aggressive=true;
                guard=0;while(!subject.ProjectileFlying&&guard++<400)sim.Step(1f/120);Assert.That(subject.ProjectileFlying,Is.True);
                subject.Receive(99999,p,1,false,null,true);Assert.That(manager.HasFlying(subject),Is.True,"released can survives body death");
                floorBox.enabled=false;p.Position=baseAt+Vector2.up*5;
                for(int i=0;i<380;i++){p.Position=baseAt+Vector2.up*5;p.velocity=Vector2.zero;sim.Step(1f/120);}
                Assert.That(manager.HasFlying(subject),Is.False,"original three-second lifetime despite lost floor");Assert.That(manager.HazardCount,Is.Zero,"no invented hazard on expiry");
                Debug.Log("APPROVED_NORMAL_POLICY_PASS hazard-shared-tick/airborne/unlimited/death, H3-no-cancel, R-death-persistence/floor-loss-expiry");
                // A carried player reaches a wall before the charger's body: stop and release,
                // then preserve the full 450ms slam warning from that actual stop.
                subject=world.Actors.Values.OfType<Chapter2NormalEnemy>().First(e=>e.ArtId=="B3");
                owner=world.Layout.actors.First(e=>world.Actors.TryGetValue(e.id,out var actor)&&actor==subject);home=world.Layout.Room(owner.roomId);world.WarpToRoom(home.id);
                baseAt=new Vector2(home.x+4,home.y+2);floorGo.transform.position=new Vector2(home.x+home.w/2,baseAt.y-.5f);floorBox.size=new Vector2(home.w-1,1);floorBox.enabled=true;
                subject.spawn=baseAt;subject.ResetEnemy();subject.BindEncounter(home.Bounds,pos=>home.Bounds.Contains(pos));
                p.ResetPlayer();p.maxHealth=10000;p.health=10000;p.Position=baseAt+Vector2.right*3;p.velocity=Vector2.zero;
                var wall=new GameObject("Carried-player-only wall");wall.transform.position=baseAt+new Vector2(5,1.5f);wall.AddComponent<ArenaSurface>();wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,3);
                Physics2D.SyncTransforms();world.sceneCamera.transform.position=new Vector3(baseAt.x+5,baseAt.y+4,-10);allow=true;bool carriedBeforeStop=false;guard=0;
                float externalLock=0;
                while((subject.AttacksStarted==0||subject.Attacking&&subject.ChargeStopAge>=1.099f)&&guard++<500)
                {
                    sim.Step(1f/120);carriedBeforeStop|=subject.Carrying;
                    if(subject.Carrying&&externalLock==0){externalLock=arena.Now+2;p.moveLockUntil=externalLock;}
                }
                Assert.That(carriedBeforeStop,Is.True,"wall scenario actually starts carrying");
                Assert.That(subject.ChargeTravelled,Is.LessThan(5.5f),"carried player's wall stops charge early");
                Assert.That(subject.Carrying,Is.False,"wall stop releases player immediately");
                Assert.That(p.moveLockUntil,Is.GreaterThanOrEqualTo(externalLock),"release preserves movement locks owned by another accepted hit");
                Assert.That(subject.ChargeStopAge,Is.LessThan(1.1f));float stopAge=subject.ChargeStopAge;
                while(subject.AttackAge<stopAge+.44f){sim.Step(1f/120);Assert.That(subject.DamageActive,Is.False,"full slam warning remains after early stop");}
                allow=false;subject.CancelAttack();manager.Clear();
                subject.spawn=baseAt+Vector2.right*3;subject.ResetEnemy();subject.BindEncounter(home.Bounds,pos=>home.Bounds.Contains(pos));
                p.ResetPlayer();p.maxHealth=10000;p.health=10000;p.Position=baseAt+Vector2.right*5.5f;p.velocity=Vector2.zero;
                Physics2D.SyncTransforms();Assert.That(subject.StartEligible(p),Is.False,"melee cannot start through solid terrain");
                p.Position=baseAt+Vector2.right*4;Physics2D.SyncTransforms();allow=true;guard=0;
                while(subject.AttacksStarted==0&&guard++<400)sim.Step(1f/120);
                p.Position=baseAt+Vector2.right*5.5f;p.velocity=Vector2.zero;Physics2D.SyncTransforms();
                while(subject.Attacking&&guard++<900)sim.Step(1f/120);
                Assert.That(subject.AttacksStarted,Is.EqualTo(1));
                Assert.That(p.health,Is.EqualTo(10000),"charge box cannot hit through the wall before the body stops");
                Assert.That(subject.HitsDelivered,Is.Zero);Assert.That(subject.Carrying,Is.False);
                wall.GetComponent<Collider2D>().enabled=false;allow=false;subject.CancelAttack();manager.Clear();
                foreach(string turnId in new[]{"B3","K4","M3"})
                {
                    subject=world.Actors.Values.OfType<Chapter2NormalEnemy>().First(e=>e.ArtId==turnId);
                    owner=world.Layout.actors.First(e=>world.Actors.TryGetValue(e.id,out var actor)&&actor==subject);home=world.Layout.Room(owner.roomId);world.WarpToRoom(home.id);
                    baseAt=new Vector2(home.x+home.w/2,home.y+2);floorGo.transform.position=new Vector2(home.x+home.w/2,baseAt.y-.5f);floorBox.size=new Vector2(home.w-1,1);
                    subject.spawn=baseAt;subject.ResetEnemy();subject.BindEncounter(home.Bounds,pos=>home.Bounds.Contains(pos));
                    p.ResetPlayer();p.Position=baseAt+Vector2.right*3;p.velocity=Vector2.zero;Physics2D.SyncTransforms();world.sceneCamera.transform.position=new Vector3(baseAt.x,baseAt.y+4,-10);allow=true;
                    guard=0;while(!subject.Attacking&&guard++<400)sim.Step(1f/120);Assert.That(subject.Attacking,Is.True);
                    var facingDecoy=turnId=="B3"?null:SpawnDecoy(arena,baseAt-Vector2.right*3);
                    p.Position=baseAt+new Vector2(-3,5);p.velocity=Vector2.zero;Physics2D.SyncTransforms();
                    guard=0;while(subject.AttackAge<subject.Settings.windup+.02f&&guard++<200)sim.Step(1f/120);
                    Assert.That(subject.facing,Is.EqualTo(-1),turnId+" locks the direction at the end of windup");
                    p.Position=baseAt+new Vector2(3,5);p.velocity=Vector2.zero;Physics2D.SyncTransforms();sim.Step(1f/120);
                    Assert.That(subject.facing,Is.EqualTo(-1),turnId+" does not retarget in the active phase");
                    if(turnId!="B3")
                    {
                        guard=0;while(subject.Attacking&&guard++<400){p.Position=new Vector2(p.Position.x,baseAt.y+5);p.velocity=Vector2.zero;sim.Step(1f/120);}
                        Assert.That(facingDecoy,Is.Not.Null);Assert.That(facingDecoy.Dead,Is.False);
                        AssertDecoyHits(facingDecoy,3,turnId=="K4"?18:15);
                        RemoveDecoy(arena,facingDecoy);
                    }
                    subject.CancelAttack();allow=false;manager.Clear();
                }
                Object.Destroy(wall);
                Debug.Log("APPROVED_NORMAL_WALL_PASS carry-stop/release/slam-warning/terrain-occlusion/external-lock/active-facing-lock/decoys");

            }
            arena.BeforePlayerStep-=suppress;arena.PlayerStepCompleted-=suppress;
        }
        static void AssertDecoyHits(CombatActor actor,int hits,float damage)
        {
            Assert.That((bool)actor,Is.True,"decoy fixture must remain valid until its receipt assertions");
            Assert.That(actor.ReceivedHitCount,Is.EqualTo(hits),"decoy is a separate recipient with its own hit limit");
            var variance=CombatDamageVariance.Settings;
            Assert.That(1000-actor.health,Is.InRange(damage*variance.minimum-.001f,damage*variance.maximum+.001f),"decoys retain the common non-player damage variance");
        }
        static CombatActor SpawnDecoy(CombatArena arena,Vector2 at)
        {
            var actor=new GameObject("Approved normal collision decoy").AddComponent<CombatActor>();actor.isDecoy=true;
            actor.Configure(arena,at,new Vector2(.9375f,2),new Vector2(.1875f,1),false);actor.maxHealth=1000;actor.health=1000;actor.expiredAt=arena.Now+60;
            arena.decoys.Add(actor);Physics2D.SyncTransforms();return actor;
        }
        static void RemoveDecoy(CombatArena arena,CombatActor actor){arena.decoys.Remove(actor);Object.DestroyImmediate(actor.gameObject);}
        static void Capture(Camera camera,string path,Chapter2NormalEnemy focus)
        {
            // Keep other actors in the simulation but hide their bodies for this capture.
            var hidden=Object.FindObjectsByType<TestEnemy>(FindObjectsSortMode.None).Where(e=>e!=focus&&e.visual&&e.visual.enabled).Select(e=>e.visual).ToArray();
            foreach(var renderer in hidden)renderer.enabled=false;
            var prior=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var tex=new Texture2D(960,540,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.Destroy(tex);camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);foreach(var renderer in hidden)if(renderer)renderer.enabled=true;
        }
    }
}
