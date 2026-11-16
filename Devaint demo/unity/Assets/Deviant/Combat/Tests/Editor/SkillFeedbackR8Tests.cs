using Deviant.Combat.Contracts;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class SkillFeedbackR8Tests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        [UnitySetUp] public IEnumerator Setup()
        {
            CombatTestScene.Open();
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true;
            foreach(var enemy in arena.enemies) enemy.Position=new Vector2(38,0);
            yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator Teardown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float end=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<end) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        void Equip(string id,int special)
        { P.Reconfigure(0,System.Array.FindIndex(arena.catalog.forms,f=>f.id==id),special); }
        [UnityTest] public IEnumerator WhipRepressTwoSecondsAfterHitStillApproaches()
        {
            Equip("whip",2); arena.enemies[0].Position=new Vector2(1,0); Physics2D.SyncTransforms();
            float started=arena.Now;
            P.QueueTestInput(special:true); yield return Advance(2.2f);
            float elapsed=arena.Now-started;
            Assert.That(elapsed,Is.GreaterThanOrEqualTo(2.2f));
            // The Editor can execute multiple fixed ticks before resuming this coroutine.
            // Check the three-second window against observed simulation time, allowing
            // one tick for the hit's interval-crossing evaluation, not a render-frame guess.
            Assert.That(P.ApproachRemaining,Is.EqualTo(3+P.Special.hits[0].time-elapsed).Within(Time.fixedDeltaTime+.001f),"elapsed="+elapsed);
            Assert.That(P.ApproachRemaining,Is.GreaterThan(0)); Assert.That(P.Position.x,Is.EqualTo(-4));
            P.QueueTestInput(special:true); yield return Advance(.4f);
            Assert.That(P.Position.x,Is.GreaterThan(-1.1f)); Assert.That(arena.enemies[0].health,Is.EqualTo(85));
        }
        [UnityTest] public IEnumerator MovementFindsEnemyAboveAndBehindAndArrivesInItsFacingFront()
        {
            Equip("rapier",2); P.Position=new Vector2(0,4);
            var target=arena.enemies[0]; target.Position=new Vector2(-3,7); target.SyncFacing(1); Physics2D.SyncTransforms();
            Assert.That(arena.NearestInCircle(P,7),Is.SameAs(target));
            P.QueueTestInput(special:true); float timeout=arena.Now+.3f;
            while(Mathf.Abs(P.Position.x)<.01f && arena.Now<timeout) yield return new WaitForFixedUpdate();
            Assert.That(P.Position.x,Is.GreaterThan(target.Position.x));
            Assert.That(P.Position.y,Is.EqualTo(target.Position.y).Within(.4f),"compare at blink, before both actors continue falling");
            Assert.That(P.facing,Is.EqualTo(-1)); Assert.That(target.health,Is.EqualTo(65));
            Assert.That(P.currentCast.Moving,Is.False);
        }
        [UnityTest] public IEnumerator MovementFindsEnemyBelowAndFacingLeft()
        {
            Equip("rapier",2); P.Position=new Vector2(0,5);
            var target=arena.enemies[0]; target.Position=new Vector2(-2,0); target.SyncFacing(-1); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.2f);
            Assert.That(target.hurtCollider.bounds.min.x-P.bodyCollider.bounds.max.x,Is.EqualTo(.25f).Within(.03f));
            Assert.That(P.Position.x,Is.LessThan(target.Position.x)); Assert.That(P.facing,Is.EqualTo(1));
            Assert.That(target.health,Is.EqualTo(65));
        }
        [UnityTest] public IEnumerator FollowUpFindsBehindTargetTeleportsToFrontAndRetainsFiveHits()
        {
            Equip("rapier",3); P.Position=new Vector2(0,0);
            var target=arena.enemies[0]; target.Position=new Vector2(-2,0); target.SyncFacing(1); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.2f);
            Assert.That(P.Position.x,Is.GreaterThan(target.Position.x)); Assert.That(P.facing,Is.EqualTo(-1)); Assert.That(P.Invulnerable,Is.True);
            yield return Advance(.5f); Assert.That(target.health,Is.EqualTo(50)); Assert.That(P.Invulnerable,Is.False);
        }
        [UnityTest] public IEnumerator FollowUpAlsoFindsVerticalTargetAndDoesNotSpendCooldownWithoutTarget()
        {
            Equip("rapier",3); P.Position=new Vector2(0,0); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.3f);
            Assert.That(P.Weapon.Cooldown.Available,Is.EqualTo(1)); Assert.That(P.Position.x,Is.EqualTo(0));
            var target=arena.enemies[0]; target.Position=new Vector2(0,2); Physics2D.SyncTransforms();
            P.QueueTestInput(special:true); yield return Advance(.2f);
            Assert.That(P.Position.x,Is.GreaterThan(1)); Assert.That(P.Position.y,Is.GreaterThan(.5f)); Assert.That(target.health,Is.LessThan(100));
        }
        [UnityTest] public IEnumerator ShotgunSpreadsFourUnitsAcrossWholeArcRatherThanHorizontalBurst()
        {
            using (var driver = arena.AcquireManualSimulation())
            {
                Equip("launcher",2); P.Position=new Vector2(0,12); P.grounded=false; P.velocity=Vector2.zero; Physics2D.SyncTransforms();
                const float step = .02f;
                float deadline = arena.Now + 1;
                driver.Step(step, new CombatInput(0, CombatButtons.Special));
                while (arena.PlayerShotCount == 0)
                {
                    Assert.That(arena.Now, Is.LessThan(deadline), "Shotgun must release within the action windup.");
                    driver.Step(step);
                }
                float start=arena.Now,releaseY=P.Position.y;
                // Observe the middle of the recoil arc at a fixed simulation time.
                // An Editor coroutine may resume after the entire arc has finished.
                for (int i=0;i<9;i++) driver.Step(step);
                Assert.That(P.Position.x,Is.InRange(-3,-2)); Assert.That(P.Position.y,Is.GreaterThan(releaseY+.4f));
                for (int i=0;i<8;i++) driver.Step(step);
                Assert.That(P.Position.x,Is.EqualTo(-4).Within(.03f)); Assert.That(P.Position.y,Is.LessThan(releaseY));
                Assert.That(arena.Now-start,Is.GreaterThan(.3f));
            }
            yield break;
        }
    }
}
