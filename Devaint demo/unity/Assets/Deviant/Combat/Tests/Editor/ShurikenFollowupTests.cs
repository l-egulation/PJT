using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class ShurikenFollowupTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        int Index { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="shuriken"); } }
        SkillData Skill { get { return arena.catalog.forms[Index].specials[3]; } }
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            yield return new WaitForFixedUpdate();
            P.Reconfigure(0,Index,3); P.Position=Vector2.zero; P.axis=0;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float until=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<until) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        CombatActor WideTarget()
        {
            var e=arena.enemies[0]; e.health=1000; e.size=new Vector2(12,3);
            e.hurtCollider.size=e.size; e.Position=new Vector2(5,8)-e.offset;
            Physics2D.SyncTransforms(); return e;
        }
        PlayerProjectile Spawn(float damage=5)
        {
            var shot=PlayerProjectile.Spawn(arena,P,new Vector2(2.20625f,8),Vector2.right*3,Skill.ranged,damage);
            shot.SweepTo(shot.Position); return shot;
        }
        [UnityTest] public IEnumerator ZeroRecoveryReleasesExactlyOnceAndFreesAction()
        {
            P.QueueTestInput(special:true); yield return Advance(.12f);
            Assert.That(arena.PlayerShotCount,Is.Zero); Assert.That(P.currentCast,Is.Not.Null);
            yield return Advance(.12f);
            Assert.That(arena.PlayerShotCount,Is.EqualTo(1)); Assert.That(P.currentCast,Is.Null);
            Assert.That(P.Weapon.Cooldown.Period,Is.EqualTo(9));
            var shot=arena.playerShots[0]; P.QueueTestInput(basic:true); yield return Advance(.04f);
            Assert.That(P.currentCast,Is.Not.Null); Assert.That(arena.playerShots.Contains(shot),Is.True);
        }
        [UnityTest] public IEnumerator CoarseTwoSecondFlightHitsEightTimesAndEndsAtSixUnits()
        {
            var e=WideTarget(); var shot=Spawn(); float start=shot.Position.x;
            Assert.That(e.health,Is.EqualTo(995));
            shot.Step(2);
            Assert.That(e.health,Is.EqualTo(960),"0,.25,...,1.75; no expiry tick at 2 seconds");
            Assert.That(shot.Position.x-start,Is.EqualTo(6).Within(.0001f));
            Assert.That(arena.playerShots.Count,Is.Zero);
            Assert.That(e.staggerUntil,Is.LessThanOrEqualTo(0)); Assert.That(e.knockUntil,Is.LessThanOrEqualTo(0));
            yield return null;
        }
        [UnityTest] public IEnumerator ReentryAndDuplicateCollidersCannotResetPerTargetClock()
        {
            var e=WideTarget(); var duplicate=new GameObject("FollowupDuplicate"); duplicate.transform.SetParent(e.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=e.size; box.offset=e.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=e; Physics2D.SyncTransforms();
            var shot=Spawn(); Assert.That(e.health,Is.EqualTo(995));
            Vector2 original=e.Position; e.Position=new Vector2(60,8); Physics2D.SyncTransforms(); shot.Step(.1f);
            e.Position=original; Physics2D.SyncTransforms(); shot.Step(.14f); shot.SweepTo(shot.Position);
            Assert.That(e.health,Is.EqualTo(995)); shot.Step(.01f); Assert.That(e.health,Is.EqualTo(990));
            e.Position=new Vector2(60,8); Physics2D.SyncTransforms(); shot.Step(.8f);
            e.Position=original; Physics2D.SyncTransforms(); shot.SweepTo(shot.Position);
            Assert.That(e.health,Is.EqualTo(985),"overdue reentry deals one tick without catch-up burst"); yield return null;
        }
        [UnityTest] public IEnumerator SeparateProjectilesAndSeparateTargetsHaveIndependentClocks()
        {
            var e=WideTarget(); var other=arena.enemies[1]; other.health=1000;
            other.Position=new Vector2(3,8)-other.offset; Physics2D.SyncTransforms();
            var first=Spawn(); first.Step(.1f); var second=Spawn();
            Assert.That(e.health,Is.EqualTo(990)); Assert.That(other.health,Is.EqualTo(990));
            first.Step(.15f); Assert.That(e.health,Is.EqualTo(985));
            second.Step(.24f); Assert.That(e.health,Is.EqualTo(985));
            second.Step(.01f); Assert.That(e.health,Is.EqualTo(980));
            Assert.That(arena.playerShots.Count,Is.EqualTo(2)); yield return null;
        }
        [UnityTest] public IEnumerator GroundClearanceOneWayPassAndSolidRemoval()
        {
            arena.FirePlayerRanged(P,Skill.ranged,5,1); Assert.That(arena.playerShots.Count,Is.EqualTo(1));
            var shot=arena.playerShots[0]; Assert.That(shot.Position.y-Skill.ranged.radius,Is.EqualTo(.1f).Within(.0001f));
            var platform=new GameObject("FollowupSurface"); platform.transform.position=new Vector2(4,1.6f);
            platform.AddComponent<BoxCollider2D>().size=new Vector2(.1f,4);
            var surface=platform.AddComponent<ArenaSurface>(); surface.oneWay=true; Physics2D.SyncTransforms();
            shot.Step(.2f); Assert.That(arena.playerShots.Count,Is.EqualTo(1));
            surface.oneWay=false; shot.Step(.02f); Assert.That(arena.playerShots.Count,Is.Zero);
            Object.Destroy(platform); yield return null;
        }
        [UnityTest] public IEnumerator SnapshotSurvivesSwapAndResetClearsIndependentShot()
        {
            P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2; P.Weapon.StatSlots[2]=3; P.Weapon.StatSlots[3]=4;
            P.Reconfigure(0,Index,3); P.QueueTestInput(special:true); yield return Advance(.12f);
            Assert.That(arena.PlayerShotCount,Is.Zero); yield return Advance(.12f);
            var shot=arena.playerShots[0]; Assert.That(shot.damage,Is.EqualTo(6).Within(.001f));
            Assert.That(P.Weapon.Cooldown.Period,Is.EqualTo(8.1f).Within(.001f));
            Assert.That(shot.Definition.HitInterval,Is.EqualTo(.25f)); Assert.That(shot.velocity.magnitude,Is.EqualTo(3));
            P.CancelAction(); P.Weapon.StatSlots[1]=0; P.selected=1;
            Assert.That(arena.playerShots.Contains(shot),Is.True); Assert.That(shot.damage,Is.EqualTo(6).Within(.001f));
            arena.ResetTest(); Assert.That(arena.playerShots.Count,Is.Zero);
        }
    }
}
