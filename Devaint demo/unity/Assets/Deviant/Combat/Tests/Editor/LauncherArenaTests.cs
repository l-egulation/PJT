using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class LauncherArenaTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        int Launcher { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="launcher"); } }
        SkillData Skill(int mode) { return arena.catalog.forms[Launcher].specials[mode]; }
        BasicData Ball { get { return arena.catalog.forms[Launcher].basics[0]; } }
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float end=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<end) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        void Equip(int mode) { P.Reconfigure(0,Launcher,mode); }
        // Accepted hits roll the shared damage variance, so check the nominal loss inside its bounds.
        static void Lost(CombatActor enemy,float start,float nominal,string message="")
        {
            var variance=CombatDamageVariance.Settings;
            Assert.That(enemy.health,Is.InRange(start-nominal*variance.maximum-.001f,start-nominal*variance.minimum+.001f),message);
        }
        CombatActor TargetAt(Vector2 center)
        {
            var actor = new GameObject("LauncherRegressionTarget").AddComponent<CombatActor>();
            actor.Configure(arena, center, Vector2.one * .2f, Vector2.zero, false);
            actor.maxHealth = actor.health = 100;
            return actor;
        }
        GameObject WallAt(Vector2 center, Vector2 size)
        {
            var wall = new GameObject("LauncherRegressionWall"); wall.transform.position = center;
            wall.AddComponent<BoxCollider2D>().size = size; wall.AddComponent<ArenaSurface>();
            return wall;
        }
        [UnityTest] public IEnumerator UnlimitedLaserHitsFarEnemiesInBothDirectionsAndStopsAtFirstWall()
        {
            Equip(1);
            foreach (int direction in new[] { 1, -1 })
            {
                P.Position = new Vector2(0, 99); P.SyncFacing(direction);
                var near = TargetAt(new Vector2(direction * 40, 100));
                var far = TargetAt(new Vector2(direction * 400, 100));
                Physics2D.SyncTransforms();
                arena.FirePlayerRanged(P, Skill(1).ranged, 28, direction);
                Lost(near, 100, 28); Lost(far, 100, 28);
                near.health = far.health = 100;
                var wall = WallAt(new Vector2(direction * 50, 100), new Vector2(.2f, 4));
                Physics2D.SyncTransforms();
                arena.FirePlayerRanged(P, Skill(1).ranged, 28, direction);
                Lost(near, 100, 28); Assert.That(far.health, Is.EqualTo(100));
                Assert.That(arena.lasers[arena.lasers.Count-1].transform.lossyScale.x, Is.LessThan(51));
                Object.DestroyImmediate(wall); Object.DestroyImmediate(near.gameObject); Object.DestroyImmediate(far.gameObject);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ChargedImpactUsesContactCenteredSquareAndDamagesEachActorOnce()
        {
            Equip(0);
            var direct = TargetAt(new Vector2(2,100));
            var corner = TargetAt(new Vector2(2.7f,100.8f));
            var outside = TargetAt(new Vector2(3.1f,100.8f));
            var duplicate = new GameObject("DuplicateChargeHurtRegion"); duplicate.transform.SetParent(direct.transform,false);
            duplicate.AddComponent<BoxCollider2D>().size = Vector2.one*.2f;
            duplicate.AddComponent<HurtRegion>().actor = direct;
            Physics2D.SyncTransforms();
            var launch = new Deviant.Definitions.SkillRangedLaunchDefinition(Skill(0).ranged).WithChargeAmount(1);
            Assert.That(launch.Projectile.ImpactBoxSize, Is.EqualTo(2), "charging must not grow the impact square");
            var shot = PlayerProjectile.Spawn(arena,P,new Vector2(0,100),Vector2.right*20,launch.Projectile,40);
            shot.SweepTo(new Vector2(4,100));
            Lost(direct,100,40); Lost(corner,100,40); Assert.That(outside.health,Is.EqualTo(100));
            Assert.That(arena.bombBursts[arena.bombBursts.Count-1].transform.position.x,Is.EqualTo(1.9f).Within(.02f));
            Assert.That(arena.bombBursts[arena.bombBursts.Count-1].boxSize,Is.EqualTo(2));
            shot.SweepTo(new Vector2(4,100)); Lost(direct,100,40);
            yield return null;
        }
        [UnityTest] public IEnumerator ChargedImpactIsBlockedByWallsAndTerrainOrExpiryDoesNotBurst()
        {
            Equip(0);
            var direct = TargetAt(new Vector2(2,100));
            var blocked = TargetAt(new Vector2(2.7f,100.8f));
            WallAt(new Vector2(2.3f,100.5f),new Vector2(.1f,.6f));
            Physics2D.SyncTransforms();
            var shot=PlayerProjectile.Spawn(arena,P,new Vector2(0,100),Vector2.right*20,Skill(0).ranged,20);
            shot.SweepTo(new Vector2(4,100));
            Lost(direct,100,20); Assert.That(blocked.health,Is.EqualTo(100));
            int bursts=arena.bombBursts.Count;
            WallAt(new Vector2(1,110),new Vector2(.2f,2)); Physics2D.SyncTransforms();
            var terrain=PlayerProjectile.Spawn(arena,P,new Vector2(0,110),Vector2.right*20,Skill(0).ranged,20);
            terrain.SweepTo(new Vector2(4,110));
            var expiry=PlayerProjectile.Spawn(arena,P,new Vector2(0,120),Vector2.right*20,Skill(0).ranged,20);
            expiry.Step(1);
            Assert.That(arena.bombBursts.Count,Is.EqualTo(bursts));
            Assert.That(arena.playerShots.Contains(terrain),Is.False); Assert.That(arena.playerShots.Contains(expiry),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator EveryImplementedWeaponEquipsInBothSlotsAndLauncherHasFourInkSkills()
        {
            for(int slot=0;slot<2;slot++) for(int i=0;i<arena.catalog.forms.Length;i++)
            { P.Reconfigure(slot,i,0); Assert.That(P.weapons[slot].FormIndex,Is.EqualTo(i)); }
            string[] ids={"launcher_basic_special","launcher_range_special","launcher_move_special","launcher_followup_special"};
            float[] cooldowns={6,8,7,9};
            for(int mode=0;mode<4;mode++)
            {
                Equip(mode); Assert.That(P.Weapon.SpecialIndex,Is.EqualTo(mode));
                Assert.That(P.Special.id,Is.EqualTo(ids[mode])); Assert.That(P.Special.unavailable,Is.False); Assert.That(P.Special.cooldown,Is.EqualTo(cooldowns[mode]));
            }
            Assert.That(arena.catalog.forms[Launcher].basics.Length,Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator EveryInkKeepsTheBallAsTheBasicAttack()
        {
            for(int mode=0;mode<4;mode++)
            {
                arena.ResetTest(); Equip(mode); P.Position=new Vector2(-6,0);
                arena.enemies[0].Position=new Vector2(-2,0); arena.enemies[1].Position=new Vector2(2,0); Physics2D.SyncTransforms();
                P.QueueTestInput(basic:true); yield return Advance(.5f);
                Lost(arena.enemies[0],100,15,"mode "+mode); Assert.That(arena.enemies[1].health,Is.EqualTo(60),"mode "+mode);
                Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1),"the basic attack spends no skill cooldown");
            }
        }
        [UnityTest] public IEnumerator LaserSkillPiercesTwoEnemiesOnceButStopsAtWall()
        {
            Equip(1); P.Position=Vector2.zero;
            arena.enemies[0].Position=new Vector2(2,0); arena.enemies[1].Position=new Vector2(5,0);
            // Keep the entire 5.3-unit boss hurtbox behind the wall, not straddling it.
            arena.enemies[2].Position=new Vector2(18,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.2f);
            Lost(arena.enemies[0],100,28); Lost(arena.enemies[1],60,28); float once=arena.enemies[0].health;
            Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(0));
            yield return Advance(.6f);
            Assert.That(arena.enemies[0].health,Is.EqualTo(once)); Assert.That(arena.enemies[2].health,Is.EqualTo(1000));
            Assert.That(arena.lasers.Count,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator LaserStopsAtActualNearbyWallAndCannotReachEnemyBehindIt()
        {
            Equip(1); P.Position=new Vector2(10,0); arena.enemies[0].Position=new Vector2(15,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.2f);
            Assert.That(arena.enemies[0].health,Is.EqualTo(100));
            Assert.That(arena.lasers.Count,Is.EqualTo(1));
            var bounds=arena.lasers[0].GetComponent<SpriteRenderer>().bounds;
            Assert.That(bounds.max.x,Is.LessThanOrEqualTo(12.801f));
        }
        [UnityTest] public IEnumerator LaserCancelledDuringWindupNeverDamagesOrSpendsCooldown()
        {
            Equip(1); arena.enemies[0].Position=new Vector2(-1,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.06f); P.QueueTestInput(dash:true); yield return Advance(.3f);
            Assert.That(arena.enemies[0].health,Is.EqualTo(100)); Assert.That(arena.lasers.Count,Is.EqualTo(0));
            Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ShotgunAllFivePelletsCanHitOneEnemyWithoutDuplicateColliderDamage()
        {
            Equip(2); var first=arena.enemies[0]; first.Position=new Vector2(-2,0);
            var duplicate=new GameObject("DuplicateHurtRegion"); duplicate.transform.SetParent(first.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=first.size; box.offset=first.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=first; Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.3f);
            Assert.That(arena.PlayerShotCount,Is.EqualTo(5)); Lost(first,100,50);
        }
        [UnityTest] public IEnumerator GroundShotgunHopsBackSixUnitsAndLandsAgain()
        {
            Equip(2); float start=P.Position.x,floor=P.Position.y; Assert.That(P.grounded,Is.True); P.axis=0;
            P.QueueTestInput(special:true); yield return Advance(.3f);
            Assert.That(P.Position.y,Is.GreaterThan(floor+.05f),"the recoil leaves the ground");
            yield return Advance(.6f);
            Assert.That(P.Position.x,Is.EqualTo(start-6).Within(.05f)); Assert.That(P.grounded,Is.True);
        }
        [UnityTest] public IEnumerator AirShotgunMovesSixUnitsAndKeepsGravity()
        {
            Equip(2); P.Position=new Vector2(0,12); P.grounded=false; P.axis=0; Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.6f);
            // The hop covers 6 units; the existing air carry adds a little after the hop ends.
            Assert.That(P.Position.x,Is.EqualTo(-6).Within(.3f)); Assert.That(P.Position.y,Is.LessThan(12));
            Assert.That(arena.PlayerShotCount,Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator AirShotgunRecoilStopsAtWall()
        {
            Equip(2); P.Position=new Vector2(12.1f,3); P.grounded=false; P.SyncFacing(-1); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.4f);
            Assert.That(P.bodyCollider.bounds.max.x,Is.LessThanOrEqualTo(12.802f));
        }
        [UnityTest] public IEnumerator BasicUsesBasicBonusAndSkillsUseSpecialBonusAndCooldownReduction()
        {
            Equip(1); P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2; P.Weapon.StatSlots[2]=4; P.Reconfigure(0,Launcher,1);
            arena.enemies[0].Position=new Vector2(-1,0); Physics2D.SyncTransforms();
            P.QueueTestInput(basic:true); yield return Advance(.4f);
            Lost(arena.enemies[0],100,18,"ball 15 x basic +20%"); float afterBall=arena.enemies[0].health;
            P.QueueTestInput(special:true); yield return Advance(.25f);
            Lost(arena.enemies[0],afterBall,33.6f,"laser 28 x special +20%");
            Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(0));
            Assert.That(P.Weapon.Cooldown.Period,Is.LessThan(8f),"cooldown reduction applies to launcher skills");
        }
        [UnityTest] public IEnumerator ChargeShotGrowsDamageAndSizeWithHoldTime()
        {
            Equip(0); P.Position=new Vector2(-6,0); arena.enemies[0].Position=new Vector2(-1,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.6f);
            Lost(arena.enemies[0],100,20,"quick release fires the minimum 20");
            // Three equal hold bands (approved 2026-11-06): 0.2-0.4 → 20, 0.4-0.6 → 30, 0.6-0.8 → 40.
            foreach (var (hold, damage, radius) in new[] { (.35f, 20f, .15f), (.5f, 30f, .225f) })
            {
                arena.ResetTest(); Equip(0); P.Position=new Vector2(-6,0); arena.enemies[0].Position=new Vector2(-1,0); Physics2D.SyncTransforms();
                P.specialHeld=true; P.QueueTestInput(special:true); yield return Advance(hold); P.specialHeld=false; yield return Advance(.03f);
                Assert.That(arena.playerShots.Count,Is.EqualTo(1),"released after "+hold);
                Assert.That(arena.playerShots[0].Definition.Radius,Is.EqualTo(radius).Within(.001f),"released after "+hold);
                yield return Advance(.4f);
                Lost(arena.enemies[0],100,damage,"released after "+hold);
            }
            arena.ResetTest(); Equip(0); P.Position=new Vector2(-6,0); arena.enemies[0].Position=new Vector2(-1,0); Physics2D.SyncTransforms();
            P.specialHeld=true; P.QueueTestInput(special:true); yield return Advance(.84f);
            Assert.That(arena.playerShots.Count,Is.EqualTo(1),"full charge fires automatically");
            Assert.That(arena.playerShots[0].Definition.Radius,Is.EqualTo(.3f).Within(.001f));
            P.specialHeld=false; yield return Advance(.4f);
            Lost(arena.enemies[0],100,40,"full charge deals 40");
        }
        [UnityTest] public IEnumerator GrenadeAndFourFragmentsDealAtMostFortyNineWithNoSelfDamage()
        {
            var enemy=arena.enemies[0]; enemy.Position=Vector2.zero; Physics2D.SyncTransforms();
            var g=BombProjectile.Spawn(arena,P,enemy.Center,Vector2.zero,Skill(3).bomb,Skill(3).hits[0].damage);
            g.SweepTo(g.Position);
            Lost(enemy,100,49); float blasted=enemy.health; Assert.That(arena.BombExplosionCount,Is.EqualTo(5));
            Assert.That(P.health,Is.EqualTo(100)); yield return Advance(.2f);
            Assert.That(enemy.health,Is.EqualTo(blasted));
        }
        [UnityTest] public IEnumerator GrenadeFragmentsKeepDamageSnapshotAfterCancelAndSwitch()
        {
            Equip(3); P.Weapon.StatSlots[0]=2; P.Position=new Vector2(0,8); P.grounded=false; Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.24f); Assert.That(arena.bombs.Count,Is.EqualTo(1));
            var grenade=arena.bombs[0]; P.CancelAction(); P.Weapon.StatSlots[0]=0; P.selected=1;
            var enemy=arena.enemies[0]; enemy.Position=grenade.Position-enemy.offset; Physics2D.SyncTransforms(); grenade.SweepTo(grenade.Position);
            Lost(enemy,100,58.8f);
        }
        [UnityTest] public IEnumerator FragmentExpiresWithOneExplosionButUncollidedGrenadeCleansUpSilently()
        {
            var fragment=new BombData { radius=.1f,gravity=0,blastRadius=.6f,lifetime=.6f,explodeOnExpiry=true };
            BombProjectile.Spawn(arena,P,new Vector2(0,20),Vector2.zero,fragment,4);
            yield return Advance(.64f); Assert.That(arena.BombExplosionCount,Is.EqualTo(1));
            arena.ResetTest();
            var grenade=JsonUtility.FromJson<BombData>(JsonUtility.ToJson(Skill(3).bomb)); grenade.gravity=0;
            BombProjectile.Spawn(arena,P,new Vector2(0,20),Vector2.zero,grenade,12);
            yield return Advance(5.1f);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(0)); Assert.That(arena.bombs.Count,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator FastPlayerShotCannotTunnelAndResetClearsAllLauncherEffects()
        {
            var data=Ball.ranged;
            arena.enemies[0].Position=new Vector2(15,0); Physics2D.SyncTransforms();
            PlayerProjectile.Spawn(arena,P,new Vector2(10,1),Vector2.right*200,data,8);
            yield return Advance(.04f); Assert.That(arena.enemies[0].health,Is.EqualTo(100)); Assert.That(arena.playerShots.Count,Is.EqualTo(0));
            Equip(2); P.QueueTestInput(special:true); yield return Advance(.18f);
            arena.ResetTest(); yield return Advance(.8f);
            Assert.That(arena.playerShots.Count,Is.EqualTo(0)); Assert.That(arena.lasers.Count,Is.EqualTo(0)); Assert.That(arena.bombs.Count,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator PointBlankOverlapDoesNotAddAGhostMeleeHitToAnySkill()
        {
            float[] damage={20,28,50,49};
            for(int mode=0;mode<4;mode++)
            {
                arena.ResetTest(); Equip(mode);
                arena.enemies[0].Position=P.Position; Physics2D.SyncTransforms();
                P.QueueTestInput(special:true); yield return Advance(.5f);
                Lost(arena.enemies[0],100,damage[mode],"mode "+mode);
            }
        }
    }
}
