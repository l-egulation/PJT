using System.Collections;
using System.Linq;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1SpeciesBehaviorTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation simulation;
        Chapter1MonsterPresentation[] presentations;

        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>();
            Assert.That(arena,Is.Not.Null);
            arena.Player.UseTestInput=true;
            foreach(var enemy in arena.enemies) enemy.aggressive=false;
            simulation=arena.AcquireManualSimulation();
            presentations=Object.FindObjectsByType<Chapter1MonsterPresentation>(FindObjectsSortMode.None);
            var floor=new GameObject("species behavior isolated ground");
            floor.AddComponent<ArenaSurface>();floor.transform.position=new Vector3(-500,-.5f,0);
            floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);
            Physics2D.SyncTransforms();
            // This fixture adds terrain after the scene's static geometry was indexed.
            foreach(var index in arena.enemies.OfType<FieldMouseEnemy>().Select(e=>e.SurfaceIndex).Where(i=>i!=null).Distinct())
                index.Rebuild(Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None));
        }
        [UnityTearDown] public IEnumerator Teardown()
        { simulation?.Dispose(); simulation=null; if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        void Advance(float seconds)
        {
            int count=Mathf.RoundToInt(seconds/.01f);
            for(int i=0;i<count;i++)
            { simulation.Step(.01f);foreach(var p in presentations)p.Draw(.01f); }
        }
        FieldMouseEnemy Place(string species,float gap=0)
        {
            foreach(var enemy in arena.enemies) { enemy.aggressive=false;enemy.ResetEnemy(); }
            arena.Player.ResetPlayer();
            var actor=(FieldMouseEnemy)presentations.First(p=>p.species==species).actor;
            actor.spawn=new Vector2(-500,.003f);actor.ResetEnemy();actor.SyncFacing(1);
            arena.Player.SyncFacing(1);
            arena.Player.Position=new Vector2(actor.Position.x+actor.offset.x+actor.size.x/2+gap+
                arena.Player.size.x/2-arena.Player.offset.x,.003f);
            arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();Advance(.03f);
            return actor;
        }
        [UnityTest] public IEnumerator AllSixUseApprovedVitalsBodiesAndDistinctAttacksWithoutProjectiles()
        {
            var ids=new[]{"01_field_mouse","02_mole","05_boar","07_scarecrow","08_hay_bale","10_water_bucket"};
            var hp=new[]{30f,40,60,40,80,60};var damage=new[]{10f,15,20,12,25,18};
            for(int i=0;i<ids.Length;i++)
            {
                var actor=Place(ids[i],.3f);var d=actor.Definition;
                Assert.That(actor.health,Is.EqualTo(hp[i]),ids[i]);
                Assert.That(actor.bodyCollider.size,Is.EqualTo(new Vector2(d.bodySize[0],d.bodySize[1])));
                if(d.chargeDistance>0)
                { arena.Player.Position=new Vector2(actor.Position.x+4,actor.Position.y);Physics2D.SyncTransforms(); }
                float original=arena.Player.health;actor.aggressive=true;Advance(.02f);
                Assert.That(actor.Brain.Execution,Is.EqualTo(1),ids[i]+" starts species attack");
                Advance(d.prepare-.04f);
                Assert.That(arena.Player.health,Is.EqualTo(original),ids[i]+" no damage during preparation");
                Advance(d.active+.08f);
                Assert.That(arena.Player.health,Is.EqualTo(original-damage[i]).Within(.001f),ids[i]+" single approved hit");
                Assert.That(arena.projectiles.Count,Is.Zero,"No temporary ranged AI remains");
                actor.aggressive=false;
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BoarCompletesFiveUnitsIncludingBrakeAndDoesNotAttackInsideMinimumDistance()
        {
            var actor=Place("05_boar");
            arena.Player.Position=new Vector2(-496,.003f);Physics2D.SyncTransforms();
            float x=actor.Position.x;actor.aggressive=true;Advance(.02f);
            Assert.That(actor.Brain.Executing,Is.True);
            Advance(1.41f);
            Assert.That(actor.Position.x-x,Is.EqualTo(5).Within(.005f));
            Assert.That(actor.Brain.State,Is.EqualTo(FieldMouseState.Recovery));
            float stopped=actor.Position.x;Advance(.4f);
            Assert.That(actor.Position.x,Is.EqualTo(stopped).Within(.001f));
            actor=Place("05_boar");arena.Player.Position=new Vector2(-498,.003f);
            Physics2D.SyncTransforms();actor.aggressive=true;Advance(.8f);
            Assert.That(actor.Brain.Execution,Is.Zero);Assert.That(actor.Position.x,Is.EqualTo(-500).Within(.001f));
            yield return null;
        }
        [UnityTest] public IEnumerator BoarRootAndHitsCancelChargeWithoutResumingOrResettingCooldown()
        {
            foreach(bool hit in new[]{false,true})
            {
                var actor=Place("05_boar");arena.Player.Position=new Vector2(-496,.003f);
                Physics2D.SyncTransforms();actor.aggressive=true;Advance(.8f);
                Assert.That(actor.Brain.Executing,Is.True);
                double deadline=actor.Brain.NextAttackAt;
                if(hit) actor.Receive(1,arena.Player,-1,false,null);
                else actor.rootUntil=arena.Now+.2f;
                Advance(.02f);float stopped=actor.Position.x;Advance(.4f);
                Assert.That(actor.Position.x,Is.EqualTo(stopped).Within(.001f));
                Assert.That(actor.Brain.Executing,Is.False);
                Assert.That(actor.Brain.NextAttackAt,Is.EqualTo(deadline));
                Assert.That(actor.Brain.RecoveryUntil,Is.GreaterThan(arena.Now));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator HayHoldsItsThirdFrameAndCancelledAttackCannotDealPendingDamage()
        {
            var actor=Place("08_hay_bale",.3f);actor.aggressive=true;Advance(.3f);
            var presentation=presentations.First(p=>p.actor==actor);
            Assert.That(presentation.State,Is.EqualTo("attack"));Assert.That(presentation.Frame,Is.EqualTo(2));
            Advance(.7f);Assert.That(presentation.Frame,Is.EqualTo(2));
            float health=arena.Player.health;actor.Receive(1,arena.Player,-1,false,null);
            Advance(.7f);Assert.That(arena.Player.health,Is.EqualTo(health));
            Assert.That(presentation.State,Is.Not.EqualTo("attack"));
            yield return null;
        }
    }
}
