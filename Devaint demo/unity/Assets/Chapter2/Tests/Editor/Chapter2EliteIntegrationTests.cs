using System;
using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Chapter2.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2EliteIntegrationTests
    {
        [UnityTearDown] public IEnumerator LeavePlayModeAfterEachTest(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [SetUp] public void OpenExistingGameScene()
        {
            if(Application.isPlaying)return;
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter2Tools.ScenePath);
            var world=Object.FindAnyObjectByType<Chapter2World>();world.seed=17;world.family="current";
        }
        static IEnumerator WaitForWorld()
        {
            float deadline=Time.realtimeSinceStartup+45;
            while(Time.realtimeSinceStartup<deadline)
            {
                var w=Object.FindAnyObjectByType<Chapter2World>();
                if(w&&w.Ready){yield return null;yield return null;yield break;}
                yield return null;
            }
            Assert.Fail("Main Chapter2 scene did not initialize");
        }
        static void Step(CombatArena.ManualSimulation sim,float seconds)
        {
            for(int i=0;i<Mathf.RoundToInt(seconds*120);i++){Physics2D.SyncTransforms();sim.Step(1f/120);}
        }
        [UnityTest] public IEnumerator MainScenePursuitRewardsPauseDeathAndReset()
        {
            yield return new EnterPlayMode();yield return WaitForWorld();
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var e=w.Elite;var p=a.Player;
            Assert.That(w.gameObject.scene.path,Is.EqualTo(Chapter2Tools.ScenePath));
            Assert.That(w.Actors["E:elite"],Is.SameAs(e));Assert.That(e.maxHealth,Is.EqualTo(270));
            Assert.That(w.Layout.rooms.Length,Is.EqualTo(16));Assert.That(w.Boss,Is.Not.Null);
            Assert.That(e.visual.sprite,Is.Not.Null);Assert.That(e.GetComponent<FieldMouseEnemy>(),Is.Null);
            Assert.That(e.visual.transform.position.y,Is.EqualTo(e.Position.y).Within(.01f),"approved art uses its authored foot pivot");
            Assert.That(e.visual.sprite.pivot.y,Is.EqualTo(15).Within(.01f));
            Assert.That(w.ElitePursuing,Is.False);Assert.That(a.enemies.Contains(e),Is.False);
            using(var sim=a.AcquireManualSimulation())
            {
                p.maxHealth=p.health=1000;
                w.WarpToRoom("M");Step(sim,.1f);Assert.That(w.ElitePursuing,Is.False,"no remote ambush before visiting E");
                w.WarpToRoom("F01");var normal=a.enemies.First();w.WarpToRoom("E");
                Assert.That(normal.aggressive,Is.True);Assert.That(w.ElitePursuing,Is.True);
                e.BeginPattern(ElitePattern.Rock);Step(sim,.15f);w.WarpToRoom("F02");
                Assert.That(e.Pattern,Is.EqualTo(ElitePattern.Rock),"room transition must not cancel elite attack");
                Assert.That(e.aggressive&&e.hurtCollider.enabled&&e.bodyCollider.enabled,Is.True);
                Assert.That(a.enemies,Does.Contain(e));
                var link=w.Layout.connections.First();var point=link.points[link.points.Length/2];
                p.Position=point.Vector;Physics2D.SyncTransforms();sim.Step(1f/120);
                Assert.That(w.ElitePursuing&&a.enemies.Contains(e),Is.True,"pursuit also survives corridor positions");
                // Boss entry despawns the elite; RoseBlueIntegrationTests covers that separate lifecycle.
                w.WarpToRoom("M");Assert.That(w.TryOpenMaintenance(),Is.True);
                float now=a.Now,age=e.Age;Assert.Throws<InvalidOperationException>(()=>sim.Step(1f/120));
                Assert.That(a.Now,Is.EqualTo(now));Assert.That(e.Age,Is.EqualTo(age));w.CloseMaintenance();
                w.WarpToRoom("S");p.Position=p.spawn;p.velocity=Vector2.zero;p.SyncFacing(1);Step(sim,.15f);
                Assert.That(e.TryArrival(out var destination,out var direction),Is.True,"safe arrival in another authored room");
                for(int i=0;i<850&&e.Pattern!=ElitePattern.Teleport;i++)sim.Step(1f/120);
                Assert.That(e.Pattern,Is.EqualTo(ElitePattern.Teleport),"The elite selects teleport on its own across rooms.");
                Step(sim,e.Settings.teleportCast+.02f);
                Assert.That(Vector2.Distance(e.Position,destination),Is.LessThan(.05f));
                float hp=p.health;Step(sim,e.Settings.punchPrepare+.03f);Assert.That(hp-p.health,Is.EqualTo(20));Assert.That(e.Flying,Is.True);
                sim.Step(1f/120,new CombatInput(1,CombatButtons.Dash));Assert.That(e.Flying,Is.False);
                w.WarpToRoom("M");int kills=w.Kills;int clears=0;w.Rooms.Cleared+=id=>{if(id=="E")clears++;};
                e.health=0;Step(sim,.03f);
                Assert.That(w.Rooms.IsCleared("E"),Is.True);Assert.That(w.Rooms.Remaining("E"),Is.Zero);
                Assert.That(w.Kills,Is.EqualTo(kills+1));Assert.That(clears,Is.EqualTo(1));
                int pickups=Object.FindObjectsByType<ForestCombatV2.Chapter1RewardPickup>(FindObjectsSortMode.None).Length;
                Step(sim,.1f);Assert.That(w.Kills,Is.EqualTo(kills+1));Assert.That(clears,Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<ForestCombatV2.Chapter1RewardPickup>(FindObjectsSortMode.None).Length,Is.EqualTo(pickups));
                Assert.That(w.ElitePursuing,Is.False);Assert.That(e.FragmentCount,Is.Zero);
                a.ResetTest();Assert.That(e.health,Is.EqualTo(270));Assert.That(w.ElitePursuing,Is.False);
                Assert.That(w.Rooms.IsCleared("E"),Is.False);Assert.That(w.Kills,Is.Zero);
                w.WarpToRoom("E");Assert.That(w.ElitePursuing,Is.True);p.health=0;sim.Step(1f/120);
                Assert.That(w.ElitePursuing,Is.False);Assert.That(e.aggressive,Is.False);Assert.That(e.FragmentCount,Is.Zero);
                a.ResetTest();Assert.That(w.ElitePursuing,Is.False);Assert.That(e.Position,Is.EqualTo(e.spawn));
            }
            yield return new ExitPlayMode();
        }
        // Normal walkers now hunt the player across rooms (data/combat/enemy_pursuit.json); a hit would cancel the
        // 3-second portal install, so this portal-flow check keeps them at their spawns.
        static void StepWithoutNormals(CombatArena.ManualSimulation sim,float seconds)
        {
            for(int i=0;i<Mathf.RoundToInt(seconds*120);i++)
            {
                foreach(var normal in Object.FindObjectsByType<Chapter2NormalEnemy>(FindObjectsSortMode.None))if(!normal.Dead)normal.ResetEnemy();
                Physics2D.SyncTransforms();sim.Step(1f/120);
            }
        }
        [UnityTest] public IEnumerator StagePortalInstallsAndReturnsAcrossRooms()
        {
            yield return new EnterPlayMode();yield return WaitForWorld();
            var world=Object.FindAnyObjectByType<Chapter2World>();var player=world.arena.Player;
            using(var sim=world.arena.AcquireManualSimulation())
            {
                StepWithoutNormals(sim,.2f);
                Assert.That(player.TryInstallPortal(0),Is.True,player.CheckpointMessage);
                StepWithoutNormals(sim,3f);Assert.That(player.HasPortal(0),Is.True);
                var installed=player.PortalPosition(0);
                world.WarpToRoom("M");StepWithoutNormals(sim,.25f);
                Assert.That(player.TryInstallPortal(1),Is.True,player.CheckpointMessage);
                StepWithoutNormals(sim,3f);Assert.That(player.PortalsLinked,Is.True);
                Assert.That(player.TryTravelThroughPortal(),Is.True,player.CheckpointMessage);
                StepWithoutNormals(sim,3f);
                Assert.That(Vector2.Distance(player.Position,installed),Is.LessThan(.1f));
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator PunchKnockbackCoversSixUnitsIn150msAndStops()
        {
            yield return new EnterPlayMode();yield return WaitForWorld();
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var e=w.Elite;var p=a.Player;
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("E");w.WarpToRoom("S");p.maxHealth=p.health=10000;Step(sim,.15f);
                Assert.That(e.Settings.knockDistance,Is.EqualTo(6));Assert.That(e.Settings.knockSeconds,Is.EqualTo(.15f));
                float start=p.Position.x;e.StartFlight(1);Step(sim,.15f);
                Assert.That(p.Position.x-start,Is.EqualTo(6).Within(.08f),"fast punch retains the six-unit travel distance");
                Assert.That(e.Flying,Is.False);float finish=p.Position.x;Step(sim,.1f);
                Assert.That(p.Position.x,Is.EqualTo(finish).Within(.01f),"no slow residual sliding after the impact burst");
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator MainEliteKeepsApprovedTimingsAndExplicitDisplacement()
        {
            yield return new EnterPlayMode();yield return WaitForWorld();
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var e=w.Elite;var p=a.Player;
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("E");p.maxHealth=p.health=1000;
                var room=w.Layout.Room("E");e.Position=new Vector2(room.x+room.w/2,e.Position.y);
                Assert.That(e.Settings.punchPrepare,Is.EqualTo(.25f));
                Assert.That(e.Settings.comboTimes,Is.EqualTo(new[]{1f,1.5f,2.5f}));
                Assert.That(e.Settings.rockForm,Is.EqualTo(1.45f));Assert.That(e.Settings.rockFire,Is.EqualTo(1.5f));
                e.BeginPattern(ElitePattern.Rock);float x=e.Position.x;
                Assert.That(e.Receive(1,p,1,true,null),Is.True);Assert.That(e.health,Is.LessThan(270));
                Assert.That(e.Pattern,Is.EqualTo(ElitePattern.Rock));Assert.That(e.staggerUntil,Is.LessThanOrEqualTo(a.Now));
                Assert.That(e.knockUntil,Is.LessThanOrEqualTo(a.Now));
                e.Receive(1,p,1,false,new HitData{push=1,pushSeconds=.2f});Step(sim,.22f);
                Assert.That(e.Position.x,Is.GreaterThan(x+.8f));
                p.Position=new Vector2(e.Position.x-4,p.Position.y);Step(sim,.1f);Assert.That(e.Direction,Is.EqualTo(-1));
                p.Position=new Vector2(e.Position.x+4,p.Position.y);for(int n=0;n<600&&e.Age<e.Settings.rockForm+.01f;n++)Step(sim,1f/120);Assert.That(e.Direction,Is.EqualTo(1));
                Assert.That(e.RockLocked,Is.True,"rock lock: age="+e.Age+" state="+e.Pattern+" stunUntil="+e.stunUntil+" now="+a.Now);var aim=e.RockAim;var at=e.RockPosition;
                p.Position=new Vector2(e.Position.x-4,p.Position.y);Step(sim,.02f);
                Assert.That(e.Direction,Is.EqualTo(1));Assert.That(e.RockAim,Is.EqualTo(aim));Assert.That(e.RockPosition,Is.EqualTo(at));
                Step(sim,.05f);Assert.That(e.Pattern,Is.EqualTo(ElitePattern.Rest));Assert.That(e.FragmentCount,Is.GreaterThan(0));
            }
            yield return new ExitPlayMode();
        }
    }
}
