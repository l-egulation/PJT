using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class BombMissileTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        int Index { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="bomb"); } }
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            yield return new WaitForFixedUpdate(); P.Reconfigure(0,Index,2); P.Position=Vector2.zero; P.axis=0;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float until=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            // Fixed-step accumulation can end a few float bits below the requested time.
            // Do not advance an extra 20 ms frame for that rounding difference.
            while(arena.Now+.000001f<until) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        [UnityTest] public IEnumerator FlightOnlyInvulnerabilityAndEarlyManualPressIsNotBuffered()
        {
            P.QueueTestInput(special:true); yield return Advance(.12f);
            Assert.That(P.Invulnerable,Is.False); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
            yield return Advance(.14f); var cast=P.currentCast;
            Assert.That(cast.MissileFlying,Is.True); Assert.That(P.Invulnerable,Is.True);
            Assert.That(P.Receive(10,arena.enemies[0],1,true,null),Is.False); Assert.That(P.health,Is.EqualTo(100));
            P.QueueTestInput(special:true); yield return Advance(.02f); Assert.That(cast.MissileFlying,Is.True);
            yield return Advance(.22f); Assert.That(cast.MissileFlying,Is.True); Assert.That(arena.BombExplosionCount,Is.Zero);
            P.QueueTestInput(special:true); yield return Advance(.02f);
            Assert.That(cast.MissileFlying,Is.False); Assert.That(P.Invulnerable,Is.False); Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
            Assert.That(P.Receive(10,arena.enemies[0],1,true,null),Is.True); Assert.That(P.health,Is.EqualTo(90));
        }
        [UnityTest] public IEnumerator FlightIgnoresDashSwapJumpAndBasicAndDoesNotQueueThem()
        {
            P.QueueTestInput(special:true); yield return Advance(.26f); var cast=P.currentCast;
            float x=P.Position.x,y=P.Position.y; P.axis=-1;
            P.QueueTestInput(basic:true,dash:true,jump:true,swap:true); yield return Advance(.08f);
            Assert.That(P.currentCast,Is.SameAs(cast)); Assert.That(cast.MissileFlying,Is.True);
            Assert.That(P.Dashing,Is.False); Assert.That(P.selected,Is.Zero); Assert.That(P.facing,Is.EqualTo(1));
            Assert.That(P.Position.x-x,Is.EqualTo(.96f).Within(.03f)); Assert.That(P.Position.y,Is.EqualTo(y).Within(.001f));
            yield return Advance(.12f); P.QueueTestInput(special:true); yield return Advance(.4f);
            Assert.That(P.selected,Is.Zero); Assert.That(P.currentCast,Is.Null); Assert.That(P.Dashing,Is.False);
        }
        [UnityTest] public IEnumerator MaximumDistanceAutomaticallyExplodesExactlyOnce()
        {
            P.QueueTestInput(special:true); yield return Advance(1.1f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
            Assert.That(arena.LastBombExplosion.x,Is.EqualTo(10+.1875f).Within(.002f));
            Assert.That(arena.LastBombExplosion.y,Is.EqualTo(1).Within(.002f));
            yield return Advance(.5f); Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(P.currentCast,Is.Null);
        }
        [UnityTest] public IEnumerator EnemyContactBeforeManualDelayStillExplodesAndDeduplicatesDamage()
        {
            var e=arena.enemies[0]; e.Position=new Vector2(2,0);
            var other=arena.enemies[1]; other.Position=new Vector2(2.2f,0);
            var duplicate=new GameObject("MissileDuplicate"); duplicate.transform.SetParent(e.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=e.size; box.offset=e.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=e; Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.3f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(P.currentCast.MissileFlying,Is.False);
            Assert.That(e.health,Is.EqualTo(76)); Assert.That(other.health,Is.EqualTo(36)); Assert.That(P.health,Is.EqualTo(100));
            yield return Advance(.2f); Assert.That(e.health,Is.EqualTo(76)); Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator WallStopsOriginalBodyAndOccludesExplosion()
        {
            P.Position=new Vector2(12.13f,0); var e=arena.enemies[0]; e.Position=new Vector2(14.5f,0);
            Physics2D.SyncTransforms(); P.QueueTestInput(special:true); yield return Advance(.3f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
            Assert.That(arena.LastBombExplosion.x+P.size.x/2,Is.LessThanOrEqualTo(12.801f));
            Assert.That(e.health,Is.EqualTo(100)); Assert.That(P.Position.x,Is.LessThan(12.15f));
        }
        [UnityTest] public IEnumerator ManualExplosionHopsHalfUnitUpAndPointSixBackDuringRecovery()
        {
            P.QueueTestInput(special:true); yield return Advance(.46f);
            P.QueueTestInput(special:true); yield return Advance(.02f);
            float rootAtExplosion=arena.LastBombExplosion.x-P.offset.x;
            float peak=P.Position.y,until=arena.Now+.26f;
            while(arena.Now<until) { yield return new WaitForFixedUpdate(); peak=Mathf.Max(peak,P.Position.y); }
            Assert.That(peak,Is.EqualTo(.5f).Within(.025f));
            Assert.That(P.Position.x,Is.EqualTo(rootAtExplosion-.6f).Within(.015f));
            Assert.That(P.Position.y,Is.EqualTo(0).Within(.01f)); Assert.That(P.Invulnerable,Is.False);
        }
        [UnityTest] public IEnumerator LeftwardFlightHopsRightAndKeepsOriginalHurtbox()
        {
            Vector2 originalSize=P.size; P.SyncFacing(-1); P.QueueTestInput(special:true); yield return Advance(.46f);
            Assert.That(P.Position.x,Is.LessThan(-2)); Assert.That(P.size,Is.EqualTo(originalSize));
            P.QueueTestInput(special:true); yield return Advance(.28f);
            float rootAtExplosion=arena.LastBombExplosion.x+P.offset.x;
            Assert.That(P.Position.x,Is.EqualTo(rootAtExplosion+.6f).Within(.02f)); Assert.That(P.facing,Is.EqualTo(-1));
        }
        [UnityTest] public IEnumerator SpecialDamageAndCooldownSnapshotIgnoreBasicHaste()
        {
            P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2; P.Weapon.StatSlots[2]=3; P.Weapon.StatSlots[3]=4;
            P.Reconfigure(0,Index,2); P.QueueTestInput(special:true); yield return Advance(.12f);
            Assert.That(P.currentCast.MissileFlying,Is.False); yield return Advance(.14f);
            Assert.That(P.Weapon.Cooldown.Period,Is.EqualTo(9).Within(.001f)); P.Weapon.StatSlots[1]=0;
            var e=arena.enemies[0]; e.Position=P.Center+Vector2.right*2-e.offset; Physics2D.SyncTransforms();
            yield return Advance(.12f); Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
            Assert.That(e.health,Is.EqualTo(71.2f).Within(.001f));
        }
        [UnityTest] public IEnumerator CancelBeforeLaunchAndDeathResetDoNotCreateAnExplosion()
        {
            P.QueueTestInput(special:true); yield return Advance(.1f); P.QueueTestInput(dash:true); yield return Advance(.2f);
            Assert.That(arena.BombExplosionCount,Is.Zero); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
            arena.ResetTest(); P.Position=Vector2.zero; P.QueueTestInput(special:true); yield return Advance(.26f);
            Assert.That(P.Invulnerable,Is.True); Assert.That(P.visual.enabled,Is.False); P.health=0; P.Die();
            Assert.That(P.currentCast,Is.Null); Assert.That(P.Invulnerable,Is.False); Assert.That(P.visual.enabled,Is.True);
            Assert.That(arena.BombExplosionCount,Is.Zero); arena.ResetTest(); Assert.That(P.Invulnerable,Is.False);
        }
        [UnityTest] public IEnumerator AirFlightHoldsLaunchHeightAndOneWayDoesNotStopHorizontalTravel()
        {
            P.Position=new Vector2(0,10); P.grounded=false; P.velocity=Vector2.zero; P.axis=1;
            P.QueueTestInput(special:true); yield return Advance(.26f); float y=P.Position.y;
            var platform=new GameObject("MissileOneWay"); platform.transform.position=P.Center+Vector2.right;
            platform.AddComponent<BoxCollider2D>().size=new Vector2(.1f,5); platform.AddComponent<ArenaSurface>().oneWay=true;
            Physics2D.SyncTransforms(); yield return Advance(.16f);
            Assert.That(P.currentCast.MissileFlying,Is.True); Assert.That(P.Position.y,Is.EqualTo(y).Within(.001f));
            Assert.That(arena.BombExplosionCount,Is.Zero); Object.Destroy(platform);
        }
    }
}
