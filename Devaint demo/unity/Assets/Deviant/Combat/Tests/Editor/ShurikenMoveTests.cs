using Deviant.Combat.Contracts;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class ShurikenMoveTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        int Index { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="shuriken"); } }
        RangedData Shot { get { return arena.catalog.forms[Index].specials[2].ranged; } }
        CombatActor Target { get { return arena.enemies[0]; } }
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            yield return new WaitForFixedUpdate(); P.Reconfigure(0,Index,2); P.Position=Vector2.zero; P.axis=0;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            Target.Position=new Vector2(4,0); Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float until=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<until) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        GameObject Wall(Vector2 centre,Vector2 size)
        {
            var go=new GameObject("ShurikenReturnTestWall"); go.transform.position=centre;
            go.AddComponent<BoxCollider2D>().size=size; go.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms(); return go;
        }
        void Visit()
        { Assert.That(P.TryShurikenVisit(Target,P.Weapon,Shot.returnDelay,Shot.behindGap),Is.True); Assert.That(P.ShurikenReturnPending(P.Weapon),Is.True); }
        [UnityTest] public IEnumerator HitDealsTwentyFourTeleportsBehindAndReturnsToImpactTimeOrigin()
        {
            arena.FirePlayerRanged(P,Shot,24,1); var projectile=arena.playerShots[0];
            P.Position=new Vector2(.5f,0); Physics2D.SyncTransforms();
            projectile.Step(.3f);
            Assert.That(Target.health,Is.EqualTo(76)); Assert.That(arena.playerShots.Count,Is.Zero);
            Assert.That(Target.hurtCollider.bounds.min.x-P.hurtCollider.bounds.max.x,Is.EqualTo(.25f).Within(.01f));
            Assert.That(P.ShurikenReturnRemaining(P.weapons[0]),Is.EqualTo(5).Within(.001f));
            P.Position=new Vector2(8,0); Physics2D.SyncTransforms();
            yield return Advance(4.9f); Assert.That(P.Position.x,Is.EqualTo(8).Within(.01f));
            yield return Advance(.16f); Assert.That(P.Position.x,Is.EqualTo(.5f).Within(.02f));
            Assert.That(Target.health,Is.EqualTo(76),"arrival/return never add damage");
        }
        [UnityTest] public IEnumerator LethalHitDoesNotTeleportOrScheduleReturn()
        {
            Target.health=20; arena.FirePlayerRanged(P,Shot,24,1); arena.playerShots[0].Step(.3f);
            Assert.That(Target.health,Is.Zero); Assert.That(P.Position.x,Is.EqualTo(0));
            Assert.That(P.ShurikenReturnPending(P.Weapon),Is.False); yield return null;
        }
        [UnityTest] public IEnumerator BlockedBehindKeepsDamageButDoesNotMoveOrRefundCast()
        {
            Target.SyncFacing(-1); var wall=Wall(new Vector2(6.05f,1),new Vector2(1,3));
            P.QueueTestInput(special:true); yield return Advance(.5f);
            Assert.That(Target.health,Is.EqualTo(76)); Assert.That(P.Position.x,Is.EqualTo(0).Within(.01f));
            Assert.That(P.ShurikenReturnPending(P.Weapon),Is.False); Assert.That(P.Weapon.Cooldown.Available,Is.Zero);
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator WeaponSwitchKeepsTimerAndReturnCancelsDashButNotIndependentEffects()
        {
            using (var driver = arena.AcquireManualSimulation())
            {
                Visit(); var source=P.Weapon; P.selected=1; P.Position=new Vector2(8,0); Physics2D.SyncTransforms();
                BombProjectile.Spawn(arena,P,new Vector2(30,15),Vector2.zero,new BombData { radius=.1f,lifetime=20,gravity=0 },1);
                // The dash observation must precede the five-second return deadline.
                for (int i=0;i<245;i++) driver.Step(.02f);
                Assert.That(P.ShurikenReturnPending(source),Is.True);
                driver.Step(.02f,new CombatInput(0,CombatButtons.Dash)); driver.Step(.02f);
                Assert.That(P.Dashing,Is.True);
                for (int i=0;i<6;i++) driver.Step(.02f);
                Assert.That(P.selected,Is.EqualTo(1)); Assert.That(P.Position.x,Is.EqualTo(0).Within(.03f));
                Assert.That(P.Dashing,Is.False); Assert.That(P.ShurikenReturnPending(source),Is.False); Assert.That(arena.bombs.Count,Is.EqualTo(1));
            }
            yield break;
        }
        [UnityTest] public IEnumerator ObstructedOriginFindsNearbyPoseWithinOneUnit()
        {
            Visit(); P.Position=new Vector2(8,0); var wall=Wall(new Vector2(0,1),new Vector2(.1f,2));
            yield return Advance(5.04f);
            Assert.That(Vector2.Distance(P.Position,Vector2.zero),Is.LessThanOrEqualTo(1.001f));
            Assert.That(P.Blocked(P.Position,P.facing),Is.False); Assert.That(P.ShurikenReturnPending(P.weapons[0]),Is.False);
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator NoNearbyOriginUsesOriginalTargetsCurrentFront()
        {
            Visit(); P.Position=new Vector2(10.5f,0); Target.Position=new Vector2(6,0);
            var wall=Wall(new Vector2(0,1),new Vector2(5,4));
            yield return Advance(5.04f);
            Assert.That(P.facing,Is.EqualTo(-Target.facing));
            Assert.That(P.hurtCollider.bounds.min.x-Target.hurtCollider.bounds.max.x,Is.EqualTo(.25f).Within(.02f));
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator DeadTargetWithBlockedOriginKeepsCurrentPositionAndConsumesReturn()
        {
            Visit(); P.Position=new Vector2(8,0); Target.health=0; var wall=Wall(new Vector2(0,1),new Vector2(5,4));
            yield return Advance(5.04f);
            Assert.That(P.Position.x,Is.EqualTo(8).Within(.01f)); Assert.That(P.ShurikenReturnPending(P.weapons[0]),Is.False);
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator BlockedTargetFrontAlsoKeepsCurrentPosition()
        {
            Visit(); P.Position=new Vector2(10.5f,0);
            var originWall=Wall(new Vector2(0,1),new Vector2(5,4));
            var frontWall=Wall(new Vector2(6.2f,1),new Vector2(2,3));
            yield return Advance(5.04f);
            Assert.That(P.Position.x,Is.EqualTo(10.5f).Within(.01f));
            Object.Destroy(originWall); Object.Destroy(frontWall); yield return null;
        }
        [UnityTest] public IEnumerator PendingReturnBlocksOnlySourceMovementSkillAndResetOrDeathClearsIt()
        {
            Visit(); var source=P.Weapon; source.Cooldown.Reset(1,2);
            P.QueueTestInput(special:true); yield return Advance(.06f);
            Assert.That(P.currentCast,Is.Null); Assert.That(arena.PlayerShotCount,Is.Zero); Assert.That(source.Cooldown.Available,Is.EqualTo(1));
            P.selected=1; P.Reconfigure(1,0,0); P.QueueTestInput(basic:true); yield return Advance(.06f);
            Assert.That(P.currentCast,Is.Not.Null); Assert.That(P.ShurikenReturnPending(source),Is.True);
            arena.ResetTest(); Assert.That(P.ShurikenReturnPending(source),Is.False);
            P.selected=0; P.Position=Vector2.zero; Target.Position=new Vector2(4,0); Physics2D.SyncTransforms();
            Visit(); P.health=0; P.Die(); Assert.That(P.ShurikenReturnPending(source),Is.False);
        }
    }
}
