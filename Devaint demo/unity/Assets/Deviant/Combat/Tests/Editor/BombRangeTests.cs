using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class BombRangeTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        int Index { get { return System.Array.FindIndex(arena.catalog.forms,f=>f.id=="bomb"); } }
        SkillData Skill { get { return arena.catalog.forms[Index].specials[1]; } }
        [UnitySetUp] public IEnumerator SetUp()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            yield return new WaitForFixedUpdate(); P.Reconfigure(0,Index,1); P.Position=Vector2.zero; P.axis=0;
            for(int i=0;i<arena.enemies.Count;i++) arena.enemies[i].Position=new Vector2(40+i*10,0);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float until=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<until) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        [UnityTest] public IEnumerator WindupSingleReleaseCooldownAndRecovery()
        {
            P.axis=1; P.QueueTestInput(special:true); yield return Advance(.2f);
            Assert.That(arena.bombs.Count,Is.Zero); Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
            yield return Advance(.14f); Assert.That(arena.bombs.Count,Is.EqualTo(1));
            var bomb=arena.bombs[0]; Assert.That(bomb.damage,Is.EqualTo(30));
            Assert.That(bomb.Definition.Radius,Is.EqualTo(.4f)); Assert.That(bomb.Definition.BlastRadius,Is.EqualTo(3));
            Assert.That(P.Weapon.Cooldown.NextReady,Is.EqualTo(arena.Now-.04f+9).Within(.06f));
            Assert.That(P.currentCast,Is.Not.Null); Assert.That(P.Position.x,Is.EqualTo(0).Within(.001f));
            yield return Advance(.62f); Assert.That(P.currentCast,Is.Null);
            Assert.That(arena.bombs.Count+arena.BombExplosionCount,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator CancelBeforeReleaseDoesNotSpendOrEmit()
        {
            P.QueueTestInput(special:true); yield return Advance(.12f); P.QueueTestInput(dash:true);
            yield return Advance(.5f); Assert.That(arena.bombs.Count+arena.BombExplosionCount,Is.Zero);
            Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator RadiusUsesHurtboxEdgeFullDamageOnceAndNoSelfDamage()
        {
            Vector2 center=new Vector2(0,8);
            for(int i=0;i<3;i++)
            {
                var e=arena.enemies[i]; e.size=new Vector2(.2f,.2f); e.hurtCollider.size=e.size; e.health=1000;
                e.Position=center+Vector2.right*(i==0 ? 0 : i==1 ? 3.09f : 3.12f)-e.offset;
            }
            var first=arena.enemies[0]; var duplicate=new GameObject("LargeBombDuplicate"); duplicate.transform.SetParent(first.transform,false);
            var box=duplicate.AddComponent<BoxCollider2D>(); box.size=first.size; box.offset=first.offset; box.isTrigger=true;
            duplicate.AddComponent<HurtRegion>().actor=first; Physics2D.SyncTransforms();
            var bomb=BombProjectile.Spawn(arena,P,center,Vector2.right*8,Skill.bomb,30);
            bomb.SweepTo(center); bomb.SweepTo(center);
            Assert.That(first.health,Is.EqualTo(970)); Assert.That(arena.enemies[1].health,Is.EqualTo(970));
            Assert.That(arena.enemies[2].health,Is.EqualTo(1000)); Assert.That(P.health,Is.EqualTo(100));
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); yield return null;
        }
        [UnityTest] public IEnumerator MuzzleWallContactExplodesInFrontAndOccludesEnemyBehindWall()
        {
            P.Position=new Vector2(12.13f,0); var e=arena.enemies[0]; e.Position=new Vector2(14.5f,0);
            Physics2D.SyncTransforms(); arena.SpawnBomb(P,Skill.bomb,30,1);
            Assert.That(arena.BombExplosionCount,Is.EqualTo(1)); Assert.That(arena.bombs.Count,Is.Zero);
            Assert.That(arena.LastBombExplosion.x,Is.LessThanOrEqualTo(12.41f));
            Assert.That(e.health,Is.EqualTo(100)); Assert.That(P.health,Is.EqualTo(100)); yield return null;
        }
        [UnityTest] public IEnumerator SpecialSnapshotAndReleasedBombSurviveCancelAndSwap()
        {
            P.Weapon.StatSlots[0]=1; P.Weapon.StatSlots[1]=2; P.Weapon.StatSlots[2]=3; P.Weapon.StatSlots[3]=4;
            P.Reconfigure(0,Index,1); P.SyncFacing(-1); P.QueueTestInput(special:true);
            yield return Advance(.2f); Assert.That(arena.bombs.Count,Is.Zero); yield return Advance(.14f);
            var bomb=arena.bombs[0]; Assert.That(bomb.damage,Is.EqualTo(36).Within(.001f));
            Assert.That(P.Weapon.Cooldown.Period,Is.EqualTo(8.1f).Within(.001f));
            Assert.That(bomb.velocity.x,Is.EqualTo(-Mathf.Cos(30*Mathf.Deg2Rad)*8).Within(.001f));
            P.CancelAction(); P.Weapon.StatSlots[1]=0; P.selected=1; yield return Advance(.04f);
            Assert.That(arena.bombs.Contains(bomb),Is.True); Assert.That(bomb.damage,Is.EqualTo(36).Within(.001f));
            arena.ResetTest(); Assert.That(arena.bombs.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator AirInputAndGravityPersistAndDeathClearsBomb()
        {
            P.Position=new Vector2(0,10); P.velocity=Vector2.zero; P.grounded=false; P.axis=1;
            P.QueueTestInput(special:true); yield return Advance(.34f);
            Assert.That(P.Position.x,Is.GreaterThan(1.5f)); Assert.That(P.Position.y,Is.LessThan(10));
            Assert.That(arena.bombs.Count,Is.EqualTo(1)); P.health=0; P.Die();
            Assert.That(P.currentCast,Is.Null); Assert.That(arena.bombs.Count,Is.Zero);
        }
    }
}
