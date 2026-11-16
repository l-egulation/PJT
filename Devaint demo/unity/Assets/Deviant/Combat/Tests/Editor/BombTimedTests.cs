using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class BombTimedTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        int Index { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="bomb"); } }
        SkillData Skill { get { return arena.catalog.forms[Index].specials[3]; } }
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            yield return new WaitForFixedUpdate(); P.Reconfigure(0,Index,3); P.Position=Vector2.zero; P.axis=0;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float until=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<until) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        BombProjectile Spawn(Vector2 at,Vector2 velocity,BombData data=null)
        { return BombProjectile.Spawn(arena,P,at,velocity,data ?? Skill.bomb,50); }
        GameObject Surface(Vector2 at,Vector2 size,bool oneWay=false)
        {
            var go=new GameObject("TimedSurface"); go.transform.position=at;
            go.AddComponent<BoxCollider2D>().size=size; go.AddComponent<ArenaSurface>().oneWay=oneWay;
            Physics2D.SyncTransforms(); return go;
        }
        [UnityTest] public IEnumerator LandingStopsWithoutDamageThenFullFuseExpiresAtSameLocation()
        {
            var e=arena.enemies[0]; e.Position=Vector2.zero; Physics2D.SyncTransforms();
            var bomb=Spawn(new Vector2(0,.3f),Vector2.down*2); bomb.Step(.1f);
            Assert.That(bomb.TimedLanded,Is.True); Assert.That(bomb.velocity,Is.EqualTo(Vector2.zero));
            Assert.That(e.health,Is.EqualTo(100)); Assert.That(arena.BombExplosionCount,Is.Zero);
            Vector2 location=bomb.Position; float remaining=bomb.TimedFuseRemaining;
            bomb.Step(remaining-.01f); Assert.That(arena.BombExplosionCount,Is.Zero); Assert.That(bomb.Position,Is.EqualTo(location));
            bomb.Step(.02f); Assert.That(e.health,Is.EqualTo(50)); Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
            Assert.That(arena.LastBombExplosion,Is.EqualTo(location)); yield return null;
        }
        [UnityTest] public IEnumerator FlyingThroughEnemyNeverAddsDirectDamageOrStartsFuse()
        {
            var e=arena.enemies[0]; e.Position=new Vector2(2,8)-e.offset; Physics2D.SyncTransforms();
            var bomb=Spawn(new Vector2(0,8),Vector2.right*10); bomb.Step(.3f);
            Assert.That(bomb.Position.x,Is.EqualTo(3).Within(.002f)); Assert.That(bomb.TimedLanded,Is.False);
            Assert.That(e.health,Is.EqualTo(100)); Assert.That(arena.BombExplosionCount,Is.Zero); yield return null;
        }
        [UnityTest] public IEnumerator WallStopsHorizontalMotionButAllowsFallAndLanding()
        {
            var wall=Surface(new Vector2(2,4),new Vector2(.1f,8));
            var bomb=Spawn(new Vector2(0,2),Vector2.right*10); bomb.Step(.3f);
            Assert.That(bomb.velocity.x,Is.Zero); Assert.That(bomb.velocity.y,Is.LessThan(0));
            Assert.That(bomb.Position.x,Is.LessThanOrEqualTo(1.751f)); Assert.That(bomb.TimedLanded,Is.False);
            Assert.That(arena.BombExplosionCount,Is.Zero); bomb.Step(.3f); Assert.That(bomb.TimedLanded,Is.True);
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator CeilingStopsRiseButKeepsForwardVelocity()
        {
            var ceiling=Surface(new Vector2(0,3),new Vector2(20,.2f));
            var bomb=Spawn(new Vector2(0,2),new Vector2(3,10)); bomb.Step(.15f);
            Assert.That(bomb.velocity.x,Is.EqualTo(3)); Assert.That(bomb.velocity.y,Is.LessThanOrEqualTo(0));
            Assert.That(bomb.Position.x,Is.EqualTo(.45f).Within(.002f)); Assert.That(bomb.Position.y,Is.LessThanOrEqualTo(2.701f));
            Assert.That(bomb.TimedLanded,Is.False); Assert.That(arena.BombExplosionCount,Is.Zero);
            Object.Destroy(ceiling); yield return null;
        }
        [UnityTest] public IEnumerator FiveSecondsWithoutGroundExplodesInAir()
        {
            var bomb=Spawn(new Vector2(0,1000),Vector2.zero); bomb.Step(4.99f);
            Assert.That(arena.BombExplosionCount,Is.Zero); Assert.That(bomb.TimedLanded,Is.False);
            bomb.Step(.02f); Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(arena.bombs.Count,Is.Zero);
            Assert.That(arena.LastBombExplosion.y,Is.EqualTo(750).Within(.03f)); yield return null;
        }
        [UnityTest] public IEnumerator LandingNearFlightTimeoutStillReceivesCompleteLandingFuse()
        {
            var data=JsonUtility.FromJson<BombData>(JsonUtility.ToJson(Skill.bomb)); data.gravity=0;
            var bomb=Spawn(new Vector2(0,5.1f),Vector2.down,data); bomb.Step(4.95f);
            Assert.That(bomb.TimedLanded,Is.True); Assert.That(bomb.TimedFuseRemaining,Is.GreaterThan(1.4f));
            bomb.Step(.1f); Assert.That(arena.BombExplosionCount,Is.Zero);
            bomb.Step(1.2f); Assert.That(arena.BombExplosionCount,Is.Zero);
            bomb.Step(.2f); Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); yield return null;
        }
        [UnityTest] public IEnumerator OneWayPlatformOnlyArmsOnDescendingTopContact()
        {
            var platform=Surface(new Vector2(0,3),new Vector2(10,.2f),true);
            var downward=Spawn(new Vector2(0,4),Vector2.down*2); downward.Step(.3f);
            Assert.That(downward.TimedLanded,Is.True); Assert.That(downward.Position.y,Is.EqualTo(3.3f).Within(.01f));
            var upward=Spawn(new Vector2(0,2),Vector2.up*10); upward.Step(.15f);
            Assert.That(upward.TimedLanded,Is.False); upward.Step(.75f); Assert.That(upward.TimedLanded,Is.True);
            Assert.That(arena.BombExplosionCount,Is.Zero); Object.Destroy(platform); yield return null;
        }
        [UnityTest] public IEnumerator MultipleBombsKeepIndependentFuses()
        {
            var first=Spawn(new Vector2(0,.3f),Vector2.down*2); first.Step(.1f);
            var second=Spawn(new Vector2(1,.3f),Vector2.down*2); second.Step(.1f);
            float secondRemaining=second.TimedFuseRemaining; first.Step(1.5f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(arena.bombs.Contains(second),Is.True);
            Assert.That(second.TimedFuseRemaining,Is.EqualTo(secondRemaining));
            second.Step(secondRemaining+.01f); Assert.That(arena.BombExplosionCount,Is.EqualTo(2)); yield return null;
        }
        [UnityTest] public IEnumerator FiftyDamageRadiusThreeUsesEdgeAndDeduplicatesColliders()
        {
            for(int i=0;i<3;i++)
            {
                var e=arena.enemies[i]; e.health=1000; e.size=new Vector2(.2f,.2f); e.hurtCollider.size=e.size;
                e.Position=new Vector2(i==0 ? 0 : i==1 ? 3.09f : 3.12f,.201f)-e.offset;
            }
            var first=arena.enemies[0]; var duplicate=new GameObject("TimedDuplicate"); duplicate.transform.SetParent(first.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=first.size; box.offset=first.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=first; Physics2D.SyncTransforms();
            var bomb=Spawn(new Vector2(0,.3f),Vector2.down*2); bomb.Step(2);
            Assert.That(first.health,Is.EqualTo(950)); Assert.That(arena.enemies[1].health,Is.EqualTo(950));
            Assert.That(arena.enemies[2].health,Is.EqualTo(1000)); Assert.That(P.health,Is.EqualTo(100));
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); yield return null;
        }
        [UnityTest] public IEnumerator SpecialSnapshotSurvivesCancelAndSwapAndResetCleansIt()
        {
            P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2; P.Weapon.StatSlots[2]=3; P.Weapon.StatSlots[3]=4;
            P.Reconfigure(0,Index,3); P.QueueTestInput(special:true); yield return Advance(.12f);
            Assert.That(arena.bombs.Count,Is.Zero); yield return Advance(.14f);
            var bomb=arena.bombs[0]; Assert.That(bomb.damage,Is.EqualTo(60).Within(.001f));
            Assert.That(P.Weapon.Cooldown.Period,Is.EqualTo(9).Within(.001f)); Assert.That(bomb.Definition.FuseSeconds,Is.EqualTo(1.5f));
            P.CancelAction(); P.Weapon.StatSlots[1]=0; P.selected=1; yield return Advance(.04f);
            Assert.That(arena.bombs.Contains(bomb),Is.True); Assert.That(bomb.damage,Is.EqualTo(60).Within(.001f));
            arena.ResetTest(); Assert.That(arena.bombs.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator OwnerDeathDuringWaitRemovesBombWithoutExplosion()
        {
            var bomb=Spawn(new Vector2(0,.3f),Vector2.down*2); bomb.Step(.1f); Assert.That(bomb.TimedLanded,Is.True);
            P.health=0; P.Die(); Assert.That(arena.bombs.Count,Is.Zero);
            Assert.That(arena.BombExplosionCount,Is.Zero); yield return null;
        }
    }
}
