using Deviant.Combat.Contracts;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class BombSpecialTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        int Index { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="bomb"); } }
        int Released { get { return arena.BombExplosionCount+arena.bombs.Count; } }
        SkillData Skill { get { return arena.catalog.forms[Index].specials[0]; } }
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            yield return new WaitForFixedUpdate(); P.Reconfigure(0,Index,0);
            P.Position=Vector2.zero; P.axis=0;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float until=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<until) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        [UnityTest] public IEnumerator ReleasesThreeInOrderAndSpendsOneCooldownAtFirstRelease()
        {
            P.QueueTestInput(special:true); yield return Advance(.12f);
            Assert.That(Released,Is.Zero); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
            var cast=P.currentCast; yield return Advance(.12f);
            Assert.That(Released,Is.EqualTo(1)); Assert.That(P.Weapon.Cooldown.Available,Is.Zero);
            float ready=P.Weapon.Cooldown.NextReady;
            Assert.That(ready,Is.EqualTo(arena.Now-.04f+8).Within(.06f));
            yield return Advance(.15f); Assert.That(Released,Is.EqualTo(2));
            yield return Advance(.15f); Assert.That(Released,Is.EqualTo(3));
            Assert.That(P.Weapon.Cooldown.NextReady,Is.EqualTo(ready)); Assert.That(P.currentCast,Is.SameAs(cast));
            yield return Advance(.42f); Assert.That(P.currentCast,Is.Null); Assert.That(Released,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator DashBeforeFirstReleaseCostsNothingAndReleasesNothing()
        {
            P.QueueTestInput(special:true); yield return Advance(.1f); P.QueueTestInput(dash:true);
            yield return Advance(.55f); Assert.That(Released,Is.Zero); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator DashBetweenReleasesStopsRemainingButPreservesFirstBombAndCooldown()
        {
            P.QueueTestInput(special:true); yield return Advance(.24f);
            var bomb=arena.bombs[0]; float ready=P.Weapon.Cooldown.NextReady;
            P.QueueTestInput(dash:true); yield return Advance(.08f);
            Assert.That(arena.bombs.Contains(bomb),Is.True); Assert.That(P.Dashing,Is.True);
            yield return Advance(.5f); Assert.That(Released,Is.EqualTo(1)); Assert.That(P.Weapon.Cooldown.NextReady,Is.EqualTo(ready));
        }
        [UnityTest] public IEnumerator EachBombDealsTwelveOnceWithDuplicateCollidersAndNoSelfDamage()
        {
            var e=arena.enemies[0]; e.Position=new Vector2(2,0); e.health=100;
            var duplicate=new GameObject("BombSpecialDuplicate"); duplicate.transform.SetParent(e.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=e.size; box.offset=e.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=e; Physics2D.SyncTransforms();
            for(int i=0;i<3;i++)
            {
                var bomb=BombProjectile.Spawn(arena,P,e.Center,Vector2.right*10,Skill.bomb,12);
                bomb.SweepTo(bomb.Position); bomb.SweepTo(bomb.Position);
            }
            Assert.That(e.health,Is.EqualTo(64)); Assert.That(arena.BombExplosionCount,Is.EqualTo(3));
            Assert.That(P.health,Is.EqualTo(100)); yield return null;
        }
        [UnityTest] public IEnumerator AllReleasesKeepDirectionAndSpecialSnapshotWithoutBasicHaste()
        {
            P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2; P.Weapon.StatSlots[2]=3; P.Weapon.StatSlots[3]=4;
            P.Reconfigure(0,Index,0); P.SyncFacing(1); P.QueueTestInput(special:true); yield return Advance(.12f);
            Assert.That(Released,Is.Zero); yield return Advance(.12f);
            Assert.That(arena.bombs[0].damage,Is.EqualTo(14.4f).Within(.001f));
            Assert.That(P.Weapon.Cooldown.Period,Is.EqualTo(7.2f).Within(.001f));
            P.Weapon.StatSlots[1]=0; P.axis=-1;
            yield return Advance(.3f); Assert.That(arena.bombs.Count,Is.EqualTo(3));
            foreach(var bomb in arena.bombs)
            {
                Assert.That(bomb.damage,Is.EqualTo(14.4f).Within(.001f));
                Assert.That(bomb.velocity.x,Is.EqualTo(Mathf.Cos(30*Mathf.Deg2Rad)*15).Within(.001f));
            }
            Assert.That(P.Position.x,Is.EqualTo(0).Within(.001f));
        }
        [UnityTest] public IEnumerator AirControlAndGravityPersistDuringBurst()
        {
            using (var driver = arena.AcquireManualSimulation())
            {
                P.Position=new Vector2(0,10); P.grounded=false; P.velocity=Vector2.zero; Physics2D.SyncTransforms();
                // Observe at 0.30s, before the second release at 0.35s.
                // Coroutine resumption can overshoot that boundary in the Editor.
                for (int i=0;i<15;i++)
                    driver.Step(.02f, new CombatInput(1, i==0 ? CombatButtons.Special : CombatButtons.None));
                Assert.That(P.Position.x,Is.GreaterThan(1.3f)); Assert.That(P.Position.y,Is.LessThan(10));
                Assert.That(Released,Is.EqualTo(1));
            }
            yield break;
        }
        [UnityTest] public IEnumerator SwapQueuesUntilRecoveryAndResetClearsIndependentBombs()
        {
            P.QueueTestInput(special:true); yield return Advance(.24f); P.QueueTestInput(swap:true);
            yield return Advance(.3f); Assert.That(P.selected,Is.Zero); Assert.That(Released,Is.EqualTo(3));
            yield return Advance(.42f); Assert.That(P.selected,Is.EqualTo(1));
            // Released effects do not belong to the selected weapon's current action.
            var bomb=arena.SpawnBomb(P,Skill.bomb,12,1); Assert.That(arena.bombs.Contains(bomb),Is.True);
            arena.ResetTest(); Assert.That(arena.bombs.Count,Is.Zero);
        }
    }
}
