using System;
using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class BasicSwordHitFeedbackTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation simulation;
        Chapter1MonsterPresentation[] targets;
        Chapter1MonsterPresentation first,second;
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
            var world=Object.FindFirstObjectByType<ForestCombatWorld>();world.enablePlaytestWorkbench=false;world.enablePlaytestBasicSpeeds=false;
            yield return new EnterPlayMode();yield return null;yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>();arena.Player.UseTestInput=true;
            targets=Object.FindObjectsByType<Chapter1MonsterPresentation>(FindObjectsSortMode.None);
            first=targets.First(p=>p.species=="01_field_mouse");second=targets.First(p=>p!=first&&p.species=="01_field_mouse");
            foreach(var e in arena.enemies)e.aggressive=false;
            var floor=new GameObject("Sword contact test floor");floor.AddComponent<ArenaSurface>();floor.transform.position=new Vector3(-500,-.5f,0);floor.AddComponent<BoxCollider2D>().size=new Vector2(50,1);
            simulation=arena.AcquireManualSimulation();
            Place();
        }
        void Place(int facing=1,string form="base_sword",bool multiple=false)
        {
            arena.Player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id==form),0);
            arena.Player.spawn=new Vector2(-500,.003f);first.actor.spawn=new Vector2(-500+1.9f*facing,.003f);
            if(multiple)second.actor.spawn=first.actor.spawn+Vector2.right*.2f;
            simulation.Reset();arena.Player.SyncFacing(facing);first.actor.SyncFacing(-facing);
            foreach(var p in targets){p.actor.aggressive=false;p.actor.health=100;}
            Physics2D.SyncTransforms();Advance(.1f);
        }
        void Advance(float seconds,bool basic=false)
        {
            for(int i=0;i<Mathf.RoundToInt(seconds*120);i++)
            {
                simulation.Step(1f/120,new CombatInput(pressed:i==0&&basic?CombatButtons.Basic:CombatButtons.None));
                arena.Presentation.Draw(1f/120);foreach(var p in targets)p.Draw(1f/120);
            }
        }
        [UnityTearDown] public IEnumerator Teardown(){simulation?.Dispose();if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator ActualComboAndMultipleTargetsUseActionDirectionNotTargetCount()
        {
            Place(multiple:true);Advance(.22f,true);
            foreach(var p in new[]{first,second})
            {
                Assert.That(p.actor.health,Is.EqualTo(87));Assert.That(p.SwordFeedback.Emissions,Is.EqualTo(1));
                Assert.That(p.SwordFeedback.LastFlipY,Is.False);Assert.That(p.SwordFeedback.LastFlipX,Is.False);
                Assert.That(p.actor.GetComponentInChildren<ParticleSystem>().particleCount,Is.EqualTo(12));
                var contact=p.actor.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Basic sword A contact");
                Assert.That(contact.transform.parent,Is.SameAs(p.actor.transform),"contact pool stays under its target, not the scene root");
                Assert.That(contact.transform.position,Is.EqualTo(p.SwordFeedback.LastPosition));
            }
            Advance(.12f);Advance(.28f,true);
            foreach(var p in new[]{first,second})
            {Assert.That(p.actor.health,Is.EqualTo(74));Assert.That(p.SwordFeedback.Emissions,Is.EqualTo(2));Assert.That(p.SwordFeedback.LastFlipY,Is.True);}
            yield return null;
        }
        [UnityTest] public IEnumerator FirstMissThenSecondHitStillUsesDownwardSlash()
        {
            first.actor.Position=new Vector2(-495,.003f);Physics2D.SyncTransforms();Advance(.34f,true);
            Assert.That(first.SwordFeedback==null||first.SwordFeedback.Emissions==0,Is.True);
            first.actor.Position=new Vector2(-498.1f,.003f);Physics2D.SyncTransforms();Advance(.28f,true);
            Assert.That(first.SwordFeedback.Emissions,Is.EqualTo(1));Assert.That(first.SwordFeedback.LastFlipY,Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator SwordInkParticlesSpreadOutsideContactAndExpireQuickly()
        {
            Advance(.22f,true);var particles=first.actor.GetComponentInChildren<ParticleSystem>();
            var values=new ParticleSystem.Particle[64];int count=particles.GetParticles(values);
            Assert.That(count,Is.EqualTo(12));var origin=values[0].position;
            for(int i=0;i<count;i++)
            {
                Assert.That(values[i].startColor.r,Is.LessThanOrEqualTo(57));
                Assert.That(values[i].startColor.g,Is.LessThanOrEqualTo(57));
                Assert.That(values[i].startColor.b,Is.LessThanOrEqualTo(57));
                Assert.That(values[i].startLifetime,Is.InRange(.16f,.28f));
            }
            particles.Simulate(.18f,true,false,false);count=particles.GetParticles(values);
            Assert.That(values.Take(count).Max(p=>Vector3.Distance(p.position,origin)),Is.GreaterThan(1.1f));
            particles.Simulate(.3f,true,false,false);Assert.That(particles.particleCount,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator SwordParticlesExitFarEdgeAndOnlyTravelAwayFromAttackerBothDirections()
        {
            foreach(int facing in new[]{1,-1})
            {
                Place(facing);var bounds=first.actor.hurtCollider.bounds;
                float edge=facing>0?bounds.max.x:bounds.min.x;
                Advance(.22f,true);
                var values=new ParticleSystem.Particle[64];var particles=first.actor.GetComponentInChildren<ParticleSystem>();
                int count=particles.GetParticles(values);Assert.That(count,Is.EqualTo(12));
                for(int i=0;i<count;i++)
                {
                    Assert.That(values[i].position.x,Is.EqualTo(edge+facing*.0625f).Within(.01f));
                    Assert.That(values[i].velocity.x*facing,Is.GreaterThan(0),"No particles return toward the attacker.");
                    Assert.That(Mathf.Abs(values[i].velocity.y),Is.LessThanOrEqualTo(Mathf.Abs(values[i].velocity.x)*Mathf.Tan(65*Mathf.Deg2Rad)+.001f),"Forward cone is at most +/-65 degrees.");
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LeftWorldPositionPauseDuplicateDrawDisableAndResetAreStable()
        {
            Place(-1);Advance(.22f,true);var fx=first.SwordFeedback;
            Assert.That(fx.LastFlipX,Is.True);var position=fx.LastPosition;
            for(int i=0;i<10;i++)first.Draw(0);
            Assert.That(fx.Emissions,Is.EqualTo(1));Assert.That(fx.ActiveCount,Is.EqualTo(1));
            using(var scope=arena.Controls.OpenScope("sword pause test"))using(var pause=scope.PauseSimulation())
            {float now=arena.Now;yield return new WaitForSecondsRealtime(.3f);first.Draw(.3f);Assert.That(arena.Now,Is.EqualTo(now));Assert.That(fx.ActiveCount,Is.EqualTo(1));}
            Advance(.1f);Assert.That(fx.LastPosition,Is.EqualTo(position));
            first.enabled=false;Assert.That(fx.ActiveCount,Is.Zero);first.enabled=true;first.Draw(0);Assert.That(fx.Emissions,Is.Zero);
            Advance(.4f);Advance(.22f,true);Assert.That(fx.ActiveCount,Is.EqualTo(1));
            simulation.Reset();Assert.That(fx.ActiveCount,Is.Zero);Assert.That(fx.Emissions,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator OtherWeaponsAndDirectDamageKeepGenericFeedback()
        {
            Place(form:"dagger");first.actor.Position=new Vector2(-499,.003f);Physics2D.SyncTransforms();Advance(.3f,true);
            Assert.That(first.actor.health,Is.LessThan(100));Assert.That(first.SwordFeedback.Emissions,Is.Zero);
            simulation.Reset();Advance(.1f);first.actor.Receive(1,arena.Player,1,true,null);first.Draw(0);
            Assert.That(first.SwordFeedback.Emissions,Is.Zero);Assert.That(first.actor.GetComponentInChildren<ParticleSystem>().particleCount,Is.EqualTo(18));
            yield return null;
        }
        [UnityTest] public IEnumerator DelayedDrawAfterWeaponSwapKeepsAcceptedSwordIdentity()
        {
            arena.Player.Reconfigure(1,Array.FindIndex(arena.catalog.forms,f=>f.id=="dagger"),0);Place();
            // Swap is admitted after the existing .4 s attack finishes; defer drawing
            // until then to prove the hit retains the old action identity.
            for(int i=0;i<51;i++)simulation.Step(1f/120,new CombatInput(pressed:i==0?CombatButtons.Basic:CombatButtons.None));
            Assert.That(first.actor.health,Is.EqualTo(87));
            simulation.Step(1f/120,new CombatInput(pressed:CombatButtons.Swap));
            Assert.That(arena.Player.Form.id,Is.EqualTo("dagger"));
            first.Draw(0);
            Assert.That(first.SwordFeedback.Emissions,Is.EqualTo(1));Assert.That(first.SwordFeedback.LastFlipY,Is.False);
            yield return null;
        }
        [Test] public void EmittedOriginKeepsIdentityThroughEffectAndReflectionCopies()
        {
            var origin=new CombatHitOrigin(default,default,Guid.NewGuid(),0,null,Guid.Empty,CombatHitKind.Melee,
                actionId:"base_sword_basic_1",isBasicAction:true,sourceFacing:-1);
            Assert.That(BasicSwordHitFeedback.Matches(origin),Is.True);
            var copy=origin.WithEffect(Guid.NewGuid(),CombatHitKind.Projectile).ReflectedBy(default);
            Assert.That(copy.ActionId,Is.EqualTo(origin.ActionId));Assert.That(copy.SourceFacing,Is.EqualTo(-1));Assert.That(copy.IsBasicAction,Is.True);
            Assert.That(BasicSwordHitFeedback.Matches(copy),Is.False);
        }
    }
}
