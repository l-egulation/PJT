using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class ShurikenArenaTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        int Index { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="shuriken"); } }
        RangedData Shot { get { return arena.catalog.forms[Index].basics[0].ranged; } }
        SkillData Large { get { return arena.catalog.forms[Index].specials[0]; } }
        SkillData Range { get { return arena.catalog.forms[Index].specials[1]; } }
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            yield return new WaitForFixedUpdate();
            P.Reconfigure(0,Index,0); P.Position=Vector2.zero; P.axis=0;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float until=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<until) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        GameObject Surface(Vector2 center,Vector2 size,bool oneWay)
        {
            var go=new GameObject("ShurikenTestSurface"); go.transform.position=center;
            go.AddComponent<BoxCollider2D>().size=size; go.AddComponent<ArenaSurface>().oneWay=oneWay;
            Physics2D.SyncTransforms(); return go;
        }
        [UnityTest] public IEnumerator TwoPressesReleaseTwoShotsWithoutLunge()
        {
            Assert.That(Index,Is.GreaterThanOrEqualTo(0)); Assert.That(P.Form.basics.Length,Is.EqualTo(2));
            Assert.That(P.Form.basics[0].damage,Is.EqualTo(11)); Assert.That(P.Form.basics[1].damage,Is.EqualTo(12));
            foreach(var special in P.Form.specials) Assert.That(special.unavailable,Is.False);
            P.QueueTestInput(basic:true); yield return Advance(.14f);
            Assert.That(arena.PlayerShotCount,Is.EqualTo(1));
            P.QueueTestInput(basic:true); yield return Advance(.28f);
            Assert.That(arena.PlayerShotCount,Is.EqualTo(2));
            yield return Advance(.5f); Assert.That(arena.PlayerShotCount,Is.EqualTo(2),"no held/automatic third throw");
            Assert.That(P.Position.x,Is.EqualTo(0).Within(.001f),"no automatic lunge");
        }
        [UnityTest] public IEnumerator MuzzleIsMirroredAndRangeStartsAtMuzzle()
        {
            for(int facing=-1;facing<=1;facing+=2)
            {
                P.SyncFacing(facing); Physics2D.SyncTransforms();
                arena.FirePlayerRanged(P,Shot,6,facing);
                var shot=arena.playerShots[0];
                Assert.That(shot.Position,Is.EqualTo(new Vector2(facing*.85625f,1)));
                shot.Step(.1f); Assert.That(shot.Position.x,Is.EqualTo(facing*(.85625f+2.2f)).Within(.001f));
                shot.Step(1); Assert.That(shot.Position.x,Is.EqualTo(facing*(.85625f+6)).Within(.001f));
                Assert.That(arena.playerShots.Count,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator OverlapChoosesNearestActorNotLowestIdAndNeverDuplicatesDamage()
        {
            var far=arena.enemies[0]; var near=arena.enemies[1];
            far.Position=new Vector2(1.4f,1)-far.offset;
            near.Position=new Vector2(.9f,1)-near.offset;
            var duplicate=new GameObject("ShurikenDuplicateHurt"); duplicate.transform.SetParent(near.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=near.size; box.offset=near.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=near;
            Physics2D.SyncTransforms(); arena.FirePlayerRanged(P,Shot,6,1);
            Assert.That(near.health,Is.EqualTo(54)); Assert.That(far.health,Is.EqualTo(100));
            Assert.That(arena.playerShots.Count,Is.Zero); yield return null;
        }
        [UnityTest] public IEnumerator OnlyFirstEnemyOnFlightPathIsHit()
        {
            var first=arena.enemies[0]; var next=arena.enemies[1];
            first.Position=new Vector2(3,0); next.Position=new Vector2(6,0); Physics2D.SyncTransforms();
            P.QueueTestInput(basic:true); yield return Advance(.5f);
            Assert.That(first.health,Is.EqualTo(87)); Assert.That(next.health,Is.EqualTo(60));
        }
        [UnityTest] public IEnumerator SolidBetweenBodyAndMuzzlePreventsSpawnBehindWall()
        {
            var wall=Surface(new Vector2(.6f,1),new Vector2(.1f,3),false);
            var e=arena.enemies[0]; e.Position=new Vector2(2,0); Physics2D.SyncTransforms();
            arena.FirePlayerRanged(P,Shot,6,1);
            Assert.That(arena.playerShots.Count,Is.Zero); Assert.That(e.health,Is.EqualTo(100));
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator FastFlightStopsAtSolidButPassesOneWayPlatform()
        {
            var e=arena.enemies[0]; e.Position=new Vector2(4,0); Physics2D.SyncTransforms();
            var wall=Surface(new Vector2(2,1),new Vector2(.1f,3),false);
            arena.FirePlayerRanged(P,Shot,6,1); arena.playerShots[0].Step(.2f);
            Assert.That(e.health,Is.EqualTo(100)); Assert.That(arena.playerShots.Count,Is.Zero);
            Object.Destroy(wall); yield return null;
            var platform=Surface(new Vector2(2,1),new Vector2(.1f,3),true);
            arena.FirePlayerRanged(P,Shot,6,1); arena.playerShots[0].Step(.2f);
            Assert.That(e.health,Is.EqualTo(94)); Assert.That(arena.playerShots.Count,Is.Zero);
            Object.Destroy(platform); yield return null;
        }
        [UnityTest] public IEnumerator GroundMovementAndAirGravityRemainActiveDuringThrow()
        {
            P.axis=1; float start=P.Position.x; P.QueueTestInput(basic:true); yield return Advance(.2f);
            Assert.That(P.Position.x-start,Is.EqualTo(1).Within(.12f));
            P.CancelAction(); P.Position=new Vector2(0,10); P.grounded=false; P.velocity=Vector2.zero;
            P.QueueTestInput(basic:true); yield return Advance(.2f);
            Assert.That(P.Position.x,Is.GreaterThan(.8f)); Assert.That(P.Position.y,Is.LessThan(10));
        }
        [UnityTest] public IEnumerator CancelBeforeReleasePreventsShotAndReleasedShotKeepsBasicDamageSnapshot()
        {
            P.QueueTestInput(basic:true); yield return Advance(.04f); P.QueueTestInput(dash:true); yield return Advance(.14f);
            Assert.That(arena.PlayerShotCount,Is.Zero);
            arena.ResetTest(); P.Position=Vector2.zero; P.axis=0; P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            Physics2D.SyncTransforms(); P.QueueTestInput(basic:true); yield return Advance(.14f);
            Assert.That(arena.playerShots.Count,Is.EqualTo(1)); var shot=arena.playerShots[0];
            Assert.That(shot.damage,Is.EqualTo(15.6f).Within(.001f));
            P.CancelAction(); P.Weapon.StatSlots[0]=0; P.selected=1;
            var e=arena.enemies[0]; e.Position=shot.Position-e.offset; Physics2D.SyncTransforms();
            shot.SweepTo(shot.Position); Assert.That(e.health,Is.EqualTo(84.4f).Within(.001f));
            arena.ResetTest(); Assert.That(arena.playerShots.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator LargePiercesEachEnemyOnceWithoutBasicStagger()
        {
            var first=arena.enemies[0]; var second=arena.enemies[1];
            first.Position=new Vector2(2.5f,0); second.Position=new Vector2(5,0);
            var duplicate=new GameObject("LargeShurikenDuplicate"); duplicate.transform.SetParent(first.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=first.size; box.offset=first.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=first; Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.8f);
            Assert.That(first.health,Is.EqualTo(76)); Assert.That(second.health,Is.EqualTo(36));
            Assert.That(first.staggerUntil,Is.LessThanOrEqualTo(0)); Assert.That(first.knockUntil,Is.LessThanOrEqualTo(0));
            Assert.That(arena.PlayerShotCount,Is.EqualTo(1)); Assert.That(arena.playerShots.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator LargeMirrorsMuzzleAndHasEightUnitRangeWithoutRehit()
        {
            var e=arena.enemies[0];
            e.Position=new Vector2(1.4f,1)-e.offset; Physics2D.SyncTransforms();
            arena.FirePlayerRanged(P,Large.ranged,24,1); var shot=arena.playerShots[0];
            Assert.That(shot.Position,Is.EqualTo(new Vector2(1.10625f,1)));
            Assert.That(e.health,Is.EqualTo(76));
            shot.SweepTo(shot.Position); Assert.That(e.health,Is.EqualTo(76));
            shot.Step(1); Assert.That(shot.Position.x,Is.EqualTo(9.10625f).Within(.001f));
            Assert.That(arena.playerShots.Count,Is.Zero);
            e.Position=new Vector2(40,0); P.SyncFacing(-1); Physics2D.SyncTransforms();
            arena.FirePlayerRanged(P,Large.ranged,24,-1); shot=arena.playerShots[0];
            Assert.That(shot.Position,Is.EqualTo(new Vector2(-1.10625f,1)));
            Assert.That(shot.velocity.x,Is.EqualTo(-18));
            shot.Step(1); Assert.That(shot.Position.x,Is.EqualTo(-9.10625f).Within(.001f));
            yield return null;
        }
        [UnityTest] public IEnumerator LargeLocksDirectionAtWindupAndKeepsGroundMovement()
        {
            P.axis=1; P.QueueTestInput(special:true); yield return Advance(.08f);
            Assert.That(P.Position.x,Is.GreaterThan(.2f));
            P.axis=-1; yield return Advance(.16f);
            Assert.That(arena.playerShots.Count,Is.EqualTo(1));
            Assert.That(arena.playerShots[0].velocity.x,Is.EqualTo(18),"opposite movement cannot reverse this cast");
            Assert.That(P.Position.x,Is.LessThan(0));
            Assert.That(P.Weapon.Cooldown.Available,Is.Zero);
            Assert.That(P.Weapon.Cooldown.NextReady,Is.EqualTo(arena.Now-.04f+5).Within(.05f));
        }
        [UnityTest] public IEnumerator LargeCancelsBeforeReleaseWithoutCostAndKeepsSpecialSnapshotAfterRelease()
        {
            P.QueueTestInput(special:true); yield return Advance(.08f);
            P.QueueTestInput(dash:true); yield return Advance(.16f);
            Assert.That(arena.PlayerShotCount,Is.Zero); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
            arena.ResetTest(); P.Position=Vector2.zero; P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            P.Weapon.StatSlots[2]=3; P.Weapon.StatSlots[3]=4; P.Reconfigure(0,Index,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.24f);
            Assert.That(P.currentCast.EndTime-arena.Now,Is.EqualTo(.36f).Within(.04f),"basic speed does not shorten special");
            Assert.That(P.Weapon.Cooldown.Period,Is.EqualTo(4.5f).Within(.001f));
            var shot=arena.playerShots[0]; Assert.That(shot.damage,Is.EqualTo(28.8f).Within(.001f));
            P.CancelAction(); P.Weapon.StatSlots[1]=0; P.selected=1;
            var e=arena.enemies[0]; e.Position=shot.Position-e.offset; Physics2D.SyncTransforms();
            shot.SweepTo(shot.Position); Assert.That(e.health,Is.EqualTo(71.2f).Within(.001f));
            arena.ResetTest(); Assert.That(arena.playerShots.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator LargeStopsAtSolidAfterPiercingAndPassesOneWayInAir()
        {
            var first=arena.enemies[0]; var behind=arena.enemies[1];
            first.Position=new Vector2(1.8f,0); behind.Position=new Vector2(5,0);
            var wall=Surface(new Vector2(3,1),new Vector2(.1f,3),false); Physics2D.SyncTransforms();
            arena.FirePlayerRanged(P,Large.ranged,24,1); arena.playerShots[0].Step(.4f);
            Assert.That(first.health,Is.EqualTo(76)); Assert.That(behind.health,Is.EqualTo(60));
            Assert.That(arena.playerShots.Count,Is.Zero); Object.Destroy(wall); yield return null;
            first.Position=new Vector2(40,0); behind.Position=new Vector2(50,0);
            P.Position=new Vector2(0,10); P.grounded=false; P.velocity=Vector2.zero; P.axis=1;
            var platform=Surface(new Vector2(3,10),new Vector2(.1f,8),true);
            P.QueueTestInput(special:true); yield return Advance(.24f);
            Assert.That(P.Position.x,Is.GreaterThan(1)); Assert.That(P.Position.y,Is.LessThan(10));
            var shot=arena.playerShots[0]; shot.Step(.2f); Assert.That(arena.playerShots.Count,Is.EqualTo(1));
            Object.Destroy(platform); yield return null;
        }
        [UnityTest] public IEnumerator RangeBlastDealsFullDamageAcrossRadiusThreeOnceWithoutSelfDamage()
        {
            Vector2 origin=new Vector2(0,10);
            P.Position=origin-P.offset;
            var first=arena.enemies[0]; var edge=arena.enemies[1]; var outside=arena.enemies[2];
            first.Position=origin-first.offset; edge.Position=origin+Vector2.right*2.8f-edge.offset;
            outside.Position=origin+Vector2.right*7.5f-outside.offset;
            var duplicate=new GameObject("BlastDuplicate"); duplicate.transform.SetParent(first.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=first.size; box.offset=first.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=first; Physics2D.SyncTransforms();
            var shot=PlayerProjectile.Spawn(arena,P,origin,Vector2.right*18,Range.ranged,24);
            shot.SweepTo(origin); shot.SweepTo(origin); shot.Detonate();
            Assert.That(first.health,Is.EqualTo(76)); Assert.That(edge.health,Is.EqualTo(36));
            Assert.That(outside.health,Is.EqualTo(1000)); Assert.That(P.health,Is.EqualTo(100));
            Assert.That(first.staggerUntil,Is.LessThanOrEqualTo(0)); Assert.That(first.knockUntil,Is.LessThanOrEqualTo(0));
            Assert.That(arena.playerShots.Count,Is.Zero); Assert.That(arena.bombBursts.Count,Is.EqualTo(1));
            Assert.That(arena.bombBursts[0].radius,Is.EqualTo(3)); yield return null;
        }
        [UnityTest] public IEnumerator RangeExplosionCannotDamageThroughSolidWall()
        {
            Vector2 origin=new Vector2(0,10);
            var first=arena.enemies[0]; var hidden=arena.enemies[1];
            first.Position=origin-first.offset; hidden.Position=origin+Vector2.right*3.5f-hidden.offset;
            var wall=Surface(new Vector2(2,10),new Vector2(.1f,8),false); Physics2D.SyncTransforms();
            PlayerProjectile.Spawn(arena,P,origin,Vector2.right*18,Range.ranged,24).SweepTo(origin);
            Assert.That(first.health,Is.EqualTo(76)); Assert.That(hidden.health,Is.EqualTo(60));
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator RangeMuzzleBlockedByWallExplodesOnNearSideWithNoDirectHit()
        {
            var near=arena.enemies[0]; var hidden=arena.enemies[1];
            near.Position=new Vector2(0,1)-near.offset; hidden.Position=new Vector2(3,1)-hidden.offset;
            var wall=Surface(new Vector2(1.4f,1),new Vector2(.1f,4),false); Physics2D.SyncTransforms();
            arena.FirePlayerRanged(P,Range.ranged,24,1);
            Assert.That(arena.playerShots.Count,Is.Zero); Assert.That(arena.bombBursts.Count,Is.EqualTo(1));
            Assert.That(arena.bombBursts[0].transform.position.x,Is.LessThan(wall.GetComponent<Collider2D>().bounds.min.x));
            Assert.That(near.health,Is.EqualTo(76)); Assert.That(hidden.health,Is.EqualTo(60));
            Assert.That(P.health,Is.EqualTo(100)); Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator RangePassesOneWayPlatformAndExpiresSilentlyAtEightUnits()
        {
            P.Position=new Vector2(0,20); Physics2D.SyncTransforms();
            var platform=Surface(new Vector2(4,20),new Vector2(.1f,8),true);
            arena.FirePlayerRanged(P,Range.ranged,24,1); var shot=arena.playerShots[0];
            shot.Step(.2f); Assert.That(arena.playerShots.Count,Is.EqualTo(1));
            Assert.That(shot.Position.x,Is.EqualTo(4.70625f).Within(.001f));
            shot.Step(1); Assert.That(shot.Position.x,Is.EqualTo(9.10625f).Within(.001f));
            Assert.That(arena.playerShots.Count,Is.Zero); Assert.That(arena.bombBursts.Count,Is.Zero);
            Object.Destroy(platform); yield return null;
        }
        [UnityTest] public IEnumerator RangeSpecialCostSnapshotAndCancelUseSpecialStatsOnly()
        {
            P.Reconfigure(0,Index,1); P.QueueTestInput(special:true); yield return Advance(.08f);
            P.QueueTestInput(dash:true); yield return Advance(.16f);
            Assert.That(arena.PlayerShotCount,Is.Zero); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
            arena.ResetTest(); P.Position=Vector2.zero;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2; P.Weapon.StatSlots[2]=3; P.Weapon.StatSlots[3]=4;
            P.Reconfigure(0,Index,1); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.24f);
            Assert.That(P.Weapon.Cooldown.Period,Is.EqualTo(6.3f).Within(.001f));
            Assert.That(P.currentCast.EndTime-arena.Now,Is.EqualTo(.36f).Within(.04f));
            var shot=arena.playerShots[0]; Assert.That(shot.damage,Is.EqualTo(28.8f).Within(.001f));
            P.CancelAction(); P.Weapon.StatSlots[1]=0; P.selected=1;
            var e=arena.enemies[0]; e.Position=shot.Position-e.offset; Physics2D.SyncTransforms();
            shot.SweepTo(shot.Position); Assert.That(e.health,Is.EqualTo(71.2f).Within(.001f));
            Assert.That(arena.bombBursts.Count,Is.EqualTo(1));
            arena.ResetTest(); Assert.That(arena.playerShots.Count,Is.Zero); Assert.That(arena.bombBursts.Count,Is.Zero);
        }
    }
}
