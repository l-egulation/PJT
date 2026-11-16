using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class CombatArenaTests
    {
        CombatArena arena;
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode();
            yield return null;
            arena = Object.FindFirstObjectByType<CombatArena>();
            Assert.That(arena, Is.Not.Null);
            arena.Player.UseTestInput=true;
            yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator AdvanceCombat(float seconds)
        {
            float until = arena.Now + seconds;
            float timeout = Time.realtimeSinceStartup + 15;
            while (arena.Now < until)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(timeout), "Combat clock stopped advancing");
                yield return new WaitForFixedUpdate();
            }
        }
        void Configure(int form, int special)
        {
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            Assert.That(arena.Player.TryReconfigure(0, form, special, out var error), Is.True, error);
        }
        [UnityTest] public IEnumerator CatalogAndGeometry()
        {
            Assert.That(arena.catalog.forms.Length, Is.EqualTo(10));
            foreach (var f in arena.catalog.forms) Assert.That(f.specials.Length, Is.EqualTo(4));
            Assert.That(arena.Player.hurtCollider.size, Is.EqualTo(new Vector2(.9375f,2)));
            Assert.That(arena.Player.health, Is.EqualTo(100));
            yield return null;
        }
        [UnityTest] public IEnumerator BasicHitDoesNotRepeatAcrossPhysicsFrames()
        {
            arena.Player.Position = Vector2.zero;
            arena.enemies[0].Position = new Vector2(1.5f,0);
            Physics2D.SyncTransforms();
            arena.Player.QueueTestInput(basic:true);
            yield return AdvanceCombat(.35f);
            Assert.That(arena.enemies[0].health, Is.EqualTo(90).Within(.001f));
            yield return AdvanceCombat(.2f);
            Assert.That(arena.enemies[0].health, Is.EqualTo(90).Within(.001f));
        }
        [UnityTest] public IEnumerator TestWeaponSpecsChangeActualHitDamageAndBasicAttackTiming()
        {
            Assert.That(arena.ApplyTestWeaponTuning(0,"2","3","2","1.25",out var error),Is.True,error);
            arena.Player.Position=Vector2.zero;arena.Player.SyncFacing(1);
            arena.enemies[0].Position=new Vector2(1.5f,0);Physics2D.SyncTransforms();
            arena.Player.QueueTestInput(basic:true);
            yield return AdvanceCombat(.14f);
            Assert.That(arena.enemies[0].health,Is.EqualTo(80).Within(.001f),"2x damage lands by the 2x speed impact time");
            yield return AdvanceCombat(.14f);
            Assert.That(arena.Player.currentCast,Is.Null,"2x speed finishes the .4 second basic motion in .2 seconds");
            Assert.That(arena.enemies[0].health,Is.EqualTo(80).Within(.001f),"speed never duplicates the hit");
        }
        [UnityTest] public IEnumerator JumpHeightMatchesThreeUnits()
        {
            arena.Player.Position = new Vector2(-4,0);
            arena.Player.QueueTestInput(jump:true);
            float start = arena.Now, top = 0;
            while (arena.Now < start + .55f) { yield return new WaitForFixedUpdate(); top = Mathf.Max(top,arena.Player.Position.y); }
            Assert.That(top, Is.EqualTo(3).Within(.08f));
        }
        [UnityTest] public IEnumerator ResetClearsEffectsButPreservesLoadout()
        {
            Configure(3,2);
            arena.Player.QueueTestInput(special:true);
            yield return AdvanceCombat(.15f);
            arena.ResetTest();
            Assert.That(arena.Player.Weapon.FormIndex, Is.EqualTo(3));
            Assert.That(arena.Player.Weapon.Cooldown.Available, Is.EqualTo(3));
            Assert.That(arena.casts.Count, Is.EqualTo(0));
            Assert.That(arena.Player.health, Is.EqualTo(100));
        }
        [UnityTest] public IEnumerator GroundComboHasTwoShortStepsAndStopsBetweenThem()
        {
            var p=arena.Player; p.axis=1;
            float x=p.Position.x;
            p.QueueTestInput(basic:true);
            yield return AdvanceCombat(.12f);
            Assert.That(p.Position.x,Is.EqualTo(x).Within(.02f),"no continuous forward slide during windup");
            p.QueueTestInput(basic:true);
            yield return AdvanceCombat(.20f);
            Assert.That(p.Position.x-x,Is.EqualTo(.65f).Within(.03f));
            float first=p.Position.x;
            yield return AdvanceCombat(.14f);
            Assert.That(p.Position.x,Is.EqualTo(first).Within(.02f),"stop between the two impacts");
            yield return AdvanceCombat(.22f);
            Assert.That(p.Position.x-x,Is.EqualTo(1.3f).Within(.04f));
        }
        [UnityTest] public IEnumerator AirAttackKeepsNormalHorizontalMovementEvenWithHammer()
        {
            var p=arena.Player; Configure(4,0);
            p.Position=new Vector2(-4,3); p.velocity=Vector2.zero; p.grounded=false; p.axis=1;
            Physics2D.SyncTransforms(); float x=p.Position.x, at=arena.Now;
            p.QueueTestInput(basic:true);
            yield return AdvanceCombat(.2f);
            Assert.That(p.Position.x-x,Is.EqualTo((arena.Now-at)*arena.Movement.RunSpeed).Within(.03f));
            Assert.That(p.Position.y,Is.LessThan(3),"attack must not freeze vertical physics");
        }
        [UnityTest] public IEnumerator BasicAttackFollowsDirectionInputAndTurnsAtWall()
        {
            var p=arena.Player; p.axis=1; p.QueueTestInput(basic:true);
            yield return AdvanceCombat(.06f);
            p.axis=-1; yield return AdvanceCombat(.04f); yield return null;
            Assert.That(p.facing,Is.EqualTo(-1)); Assert.That(p.visual.flipX,Is.True);
            p.CancelAction(); p.axis=0; p.Position=new Vector2(12.5f,0); p.SyncFacing(-1);
            Physics2D.SyncTransforms();
            Assert.That(p.SyncFacing(1),Is.True); Physics2D.SyncTransforms();
            Assert.That(p.facing,Is.EqualTo(1)); Assert.That(p.bodyCollider.bounds.max.x,Is.LessThanOrEqualTo(12.801f));
        }
        [UnityTest] public IEnumerator SecondJumpAddsTheSameThreeUnits()
        {
            var p=arena.Player; p.QueueTestInput(jump:true);
            yield return AdvanceCombat(.30f);
            float initial=p.Position.y; p.QueueTestInput(jump:true);
            float end=arena.Now+.34f, top=initial;
            while(arena.Now<end) { yield return new WaitForFixedUpdate(); top=Mathf.Max(top,p.Position.y); }
            Assert.That(top-initial,Is.EqualTo(3).Within(.08f));
        }
        [UnityTest] public IEnumerator FallRestoresTheFloorAtThreeSecondsBeforeFall()
        {
            var p=arena.Player;
            yield return AdvanceCombat(.5f);
            string trace="position="+p.Position+" velocity="+p.velocity+" ground="+p.grounded+" probe="+p.ProbeGround();
            foreach(var hit in Physics2D.RaycastAll(new Vector2(p.Center.x,p.Position.y+.04f),Vector2.down,.1f)) trace+=" / "+hit.collider.name+" normal="+hit.normal+" y="+hit.point.y;
            Assert.That(p.ProbeSafeFloor(out float floor),Is.True,trace);
            p.Position=new Vector2(-1,0); Physics2D.SyncTransforms();
            yield return AdvanceCombat(1);
            p.Position=new Vector2(5,0); Physics2D.SyncTransforms();
            yield return AdvanceCombat(2.1f);
            float cutoff=arena.Now-3;
            p.Fall();
            Assert.That(p.Position.x,Is.EqualTo(-1).Within(.01f));
            Assert.That(p.LastRestoreTime,Is.LessThanOrEqualTo(cutoff));
            Assert.That(p.LastRestoreTime,Is.GreaterThan(cutoff-.041f));
            Assert.That(p.health,Is.EqualTo(90));
        }
        [UnityTest] public IEnumerator AirborneCutoffUsesThePreviousSafeFloor()
        {
            var p=arena.Player; p.Position=new Vector2(-2,0); Physics2D.SyncTransforms();
            yield return AdvanceCombat(.4f);
            p.QueueTestInput(jump:true); yield return AdvanceCombat(.2f);
            p.Position=new Vector2(5,0); p.velocity=Vector2.zero; Physics2D.SyncTransforms();
            yield return AdvanceCombat(2.9f);
            p.Fall();
            Assert.That(p.Position.x,Is.EqualTo(-2).Within(.01f));
            Assert.That(p.LastRestoreTime,Is.GreaterThanOrEqualTo(0),"must use recorded ground rather than entry fallback");
        }
        [UnityTest] public IEnumerator APartiallyUnsupportedLedgeIsNotASafeRestorePoint()
        {
            var p=arena.Player; p.Position=new Vector2(19.6f,0); Physics2D.SyncTransforms();
            Assert.That(p.ProbeSafeFloor(out float height),Is.False);
            yield return null;
        }
        [UnityTest, Category("RequiresArt")] public IEnumerator SuppliedSwordAndWhipFramesLoadWithRegisteredPivots()
        {
            foreach(var set in arena.characterAnimations.sets)
                foreach(var clip in set.clips)
                    foreach(var sprite in clip.sprites) { Assert.That(sprite,Is.Not.Null); Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32)); }
            var sword=arena.characterAnimations.sets[0].Find("idle").sprites[0];
            var whip=arena.characterAnimations.sets[1].Find("idle").sprites[0];
            Assert.That(arena.characterAnimations.sets.Length,Is.EqualTo(11));
            Assert.That(Vector2.Distance(sword.pivot,new Vector2(80,23)),Is.LessThan(.001f));
            Assert.That(Vector2.Distance(whip.pivot,new Vector2(66,16)),Is.LessThan(.001f));
            yield return null;
        }
        int BombIndex { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="bomb"); } }
        BombData BombSettings { get { return arena.catalog.forms[BombIndex].basics[0].bomb; } }
        [UnityTest] public IEnumerator BombContactDamagesEachTargetOnceIncludingDuplicateHurtboxes()
        {
            var first=arena.enemies[0]; first.Position=new Vector2(2,0);
            var second=arena.enemies[1]; second.Position=new Vector2(1.8f,0);
            var duplicate=new GameObject("DuplicateHurtRegion"); duplicate.transform.SetParent(first.transform,false);
            var collider=duplicate.AddComponent<BoxCollider2D>(); collider.size=first.size; collider.offset=first.offset; collider.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=first; Physics2D.SyncTransforms();
            BombProjectile.Spawn(arena,arena.Player,new Vector2(1,1),Vector2.right*10,BombSettings,18);
            yield return AdvanceCombat(.12f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
            Assert.That(first.health,Is.EqualTo(82).Within(.001f)); Assert.That(second.health,Is.EqualTo(42).Within(.001f));
            Assert.That(arena.Player.health,Is.EqualTo(100));
            yield return AdvanceCombat(.4f);
            Assert.That(first.health,Is.EqualTo(82).Within(.001f)); Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator BombFloorImpactExplodesImmediatelyWithoutABounce()
        {
            BombProjectile.Spawn(arena,arena.Player,new Vector2(-4,1),Vector2.down*10,BombSettings,18);
            yield return AdvanceCombat(.12f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(arena.bombs.Count,Is.EqualTo(0));
            Assert.That(arena.LastBombExplosion.y,Is.EqualTo(.2f).Within(.03f));
        }
        [UnityTest] public IEnumerator FastBombCannotTunnelThroughWallOrDamageEnemyBehindIt()
        {
            arena.enemies[0].Position=new Vector2(14.5f,0); Physics2D.SyncTransforms();
            BombProjectile.Spawn(arena,arena.Player,new Vector2(10,1),Vector2.right*200,BombSettings,18);
            yield return AdvanceCombat(.04f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(arena.LastBombExplosion.x,Is.LessThanOrEqualTo(12.61f));
            Assert.That(arena.enemies[0].health,Is.EqualTo(100));
        }
        [UnityTest] public IEnumerator BombMuzzleSweepCannotSpawnOnTheOtherSideOfWall()
        {
            arena.Player.Position=new Vector2(12.13f,0); Physics2D.SyncTransforms();
            arena.SpawnBomb(arena.Player,BombSettings,18,1);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(arena.LastBombExplosion.x,Is.LessThanOrEqualTo(12.61f));
            yield return null;
        }
        [UnityTest] public IEnumerator BombCancelledBeforeReleaseDoesNotLaunch()
        {
            Configure(BombIndex,0); arena.Player.QueueTestInput(basic:true);
            yield return AdvanceCombat(.1f); Assert.That(arena.bombs.Count,Is.EqualTo(0));
            arena.Player.QueueTestInput(dash:true); yield return AdvanceCombat(.2f);
            Assert.That(arena.bombs.Count,Is.EqualTo(0)); Assert.That(arena.BombExplosionCount,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator ReleasedBombKeepsSnapshotAfterCancelAndWeaponChange()
        {
            var p=arena.Player; Configure(BombIndex,0); p.Weapon.StatSlots[0]=1; p.QueueTestInput(basic:true);
            yield return AdvanceCombat(.24f); Assert.That(arena.bombs.Count,Is.EqualTo(1));
            var bomb=arena.bombs[0];
            p.Weapon.StatSlots[0]=0; p.CancelAction(); p.selected=1;
            arena.enemies[0].Position=new Vector2(bomb.Position.x+1,0); Physics2D.SyncTransforms();
            yield return AdvanceCombat(.12f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(arena.enemies[0].health,Is.EqualTo(78.4f).Within(.001f));
        }
        [UnityTest] public IEnumerator BombBasicSpecialUsesApprovedTripleThrowWindup()
        {
            var p=arena.Player; Configure(BombIndex,0); p.QueueTestInput(special:true);
            yield return AdvanceCombat(.1f);
            Assert.That(p.currentCast,Is.Not.Null); Assert.That(p.currentCast.Definition.Id,Is.EqualTo("bomb_basic_special"));
            Assert.That(p.Weapon.Cooldown.Available,Is.EqualTo(1));
            Assert.That(arena.bombs.Count,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator BombResetRemovesFlyingBombAndFutureExplosion()
        {
            Configure(BombIndex,0); arena.Player.QueueTestInput(basic:true);
            yield return AdvanceCombat(.24f); Assert.That(arena.bombs.Count,Is.EqualTo(1));
            arena.ResetTest(); yield return AdvanceCombat(.8f);
            Assert.That(arena.bombs.Count,Is.EqualTo(0)); Assert.That(arena.bombBursts.Count,Is.EqualTo(0)); Assert.That(arena.BombExplosionCount,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator BombPassesUpThroughOneWayPlatformAndExplodesWhenLanding()
        {
            var b=BombProjectile.Spawn(arena,arena.Player,new Vector2(-8,2.3f),Vector2.up*10,BombSettings,18);
            yield return AdvanceCombat(.1f); Assert.That(arena.BombExplosionCount,Is.EqualTo(0));
            b.Position=new Vector2(-8,4); b.velocity=Vector2.down*10;
            yield return AdvanceCombat(.12f); Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
            Assert.That(arena.LastBombExplosion.y,Is.GreaterThan(2.99f));
        }
        [UnityTest] public IEnumerator BombTrajectoryMatchesThirtyDegreesAndProjectileGravity()
        {
            arena.Player.Position=new Vector2(-4,8); Physics2D.SyncTransforms();
            var bomb=arena.SpawnBomb(arena.Player,BombSettings,18,1); Vector2 start=bomb.Position; float at=arena.Now;
            yield return AdvanceCombat(.12f); float t=arena.Now-at;
            Assert.That(bomb.Position.x-start.x,Is.EqualTo(Mathf.Cos(30*Mathf.Deg2Rad)*10*t).Within(.005f));
            Assert.That(bomb.Position.y-start.y,Is.EqualTo(5*t-10*t*t).Within(.005f));
            Assert.That(arena.BombExplosionCount,Is.EqualTo(0),"bomb must not explode before a collision");
        }
        [UnityTest] public IEnumerator QueuedTimedSpecialTakesPriorityOverQueuedBasicThrow()
        {
            var p=arena.Player; Configure(BombIndex,3); p.QueueTestInput(basic:true);
            yield return AdvanceCombat(.1f); p.QueueTestInput(basic:true,special:true);
            yield return AdvanceCombat(1.2f);
            Assert.That(arena.BombExplosionCount+arena.bombs.Count,Is.EqualTo(2));
            Assert.That(arena.bombs.Exists(b=>b.Definition.FuseSeconds>0),Is.True);
        }
    }
}
