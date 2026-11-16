using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1HitFeedbackPoseTests
    {
        [TestCase("01_field_mouse",1)] [TestCase("01_field_mouse",-1)]
        [TestCase("02_mole",1)] [TestCase("02_mole",-1)]
        [TestCase("07_scarecrow",1)] [TestCase("07_scarecrow",-1)]
        [TestCase("08_hay_bale",1)] [TestCase("08_hay_bale",-1)]
        [TestCase("10_water_bucket",1)] [TestCase("10_water_bucket",-1)]
        public void SupportFootStaysAttachedToMovingRootWithoutChangingSpriteOrCollider(string species,int facing)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<Chapter1MonsterCatalog>("Assets/ForestCombatV2/Data/Chapter1MonstersV4.asset");
            Assert.That(catalog,Is.Not.Null);
            var idle=catalog.Find(species,"idle").frames[0];var hurt=catalog.Find(species,"hurt").frames[0];
            var root=new GameObject("foot anchor test");var child=new GameObject("visual");child.transform.SetParent(root.transform,false);
            var collider=root.AddComponent<BoxCollider2D>();var size=collider.size;
            var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=hurt;renderer.flipX=facing<0;
            try
            {
                Assert.That(Chapter1HitFeedback.TryAnchors(species,out var idlePixel,out var hurtPixel),Is.True);
                foreach(var rootX in new[]{0f,-.25f,.75f})foreach(var age in new[]{0f,.02f,.05f,.1f,.21f,.26f,.299f})foreach(var hitDirection in new[]{-1,1})
                {
                    root.transform.position=new Vector3(rootX,2,0);
                    Chapter1HitFeedback.ApplyPose(renderer,idle,species,age,hitDirection);
                    var expected=root.transform.TransformPoint(Chapter1HitFeedback.FootLocal(idle,idlePixel,renderer.flipX));
                    var actual=renderer.transform.TransformPoint(Chapter1HitFeedback.FootLocal(hurt,hurtPixel,renderer.flipX));
                    Assert.That(Vector3.Distance(expected,actual),Is.LessThan(.00001f));
                    Assert.That(renderer.sprite==hurt,Is.True);Assert.That(collider.size,Is.EqualTo(size));
                    Assert.That(root.transform.position,Is.EqualTo(new Vector3(rootX,2,0)));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void BoarKeepsOriginalPoseAndFlashMaterialIsIncluded()
        {
            Assert.That(Chapter1HitFeedback.TryAnchors("05_boar",out _,out _),Is.False);
            var material=Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
            Assert.That(material,Is.Not.Null);Assert.That(material.shader.name,Is.EqualTo("ForestCombatV2/UnlitHit"));
        }
    }

    public sealed class Chapter1HitFeedbackIntegrationTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation playback;
        Chapter1MonsterPresentation[] presentations;
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>();Assert.That(arena,Is.Not.Null);
            // This fixture retains the standalone legacy particle/flash path.
            // The shared VFX/debris path has its own approved-art integration tests.
            var shared=arena.GetComponent<HitFeedbackBridge>();if(shared)shared.enabled=false;
            var world=Object.FindFirstObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            arena.Player.UseTestInput=true;
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            playback=arena.AcquireManualSimulation();
            presentations=Object.FindObjectsByType<Chapter1MonsterPresentation>(FindObjectsSortMode.None);
            Assert.That(presentations.Select(p=>p.species).Distinct().Count(),Is.EqualTo(6));
            var floor=new GameObject("hit feedback isolated flat ground");floor.AddComponent<ArenaSurface>();
            floor.transform.position=new Vector3(-500,-.5f,0);floor.AddComponent<BoxCollider2D>().size=new Vector2(50,1);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            playback?.Dispose();playback=null;if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }
        void Advance(float seconds)
        {
            int ticks=Mathf.RoundToInt(seconds/.01f);
            for(int i=0;i<ticks;i++) { playback.Step(.01f);foreach(var presentation in presentations)presentation.Draw(.01f); }
        }
        void Place(Chapter1MonsterPresentation presentation)
        {
            foreach(var enemy in arena.enemies) { enemy.ResetEnemy();enemy.aggressive=false; }
            var actor=presentation.actor;actor.Position=new Vector2(-500,.003f);actor.velocity=Vector2.zero;actor.SyncFacing(1);
            arena.Player.Position=new Vector2(-494,.003f);arena.Player.velocity=Vector2.zero;
            Physics2D.SyncTransforms();presentation.Draw(0);Advance(.03f);
        }
        [UnityTest] public IEnumerator SixSpeciesUseExistingDamageAndKnockbackWithWorldParticlesAndFlash()
        {
            foreach(var presentation in presentations.GroupBy(p=>p.species).Select(g=>g.First()))
            {
                Place(presentation);var actor=presentation.actor;float health=actor.health,x=actor.Position.x;
                var size=actor.bodyCollider.size;var hurtSize=actor.hurtCollider.size;
                Assert.That(actor.Receive(1,arena.Player,-1,true,null),Is.True);presentation.Draw(0);
                Assert.That(presentation.State,Is.EqualTo("hurt"));
                Assert.That(actor.health,Is.EqualTo(health-1));Assert.That(actor.Position.x,Is.EqualTo(x));
                var flash=actor.visual.transform.Find("White hit flash").GetComponent<SpriteRenderer>();
                var particles=actor.GetComponentInChildren<ParticleSystem>();
                Assert.That(flash.enabled,Is.True);Assert.That(flash.sprite,Is.SameAs(actor.visual.sprite));
                Assert.That(particles.particleCount,Is.EqualTo(18));
                Assert.That(particles.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.World));
                var before=new ParticleSystem.Particle[128];particles.GetParticles(before);
                if(presentation.species=="01_field_mouse")
                    Assert.That(before[0].position.y-actor.Position.y,Is.InRange(.45f,.85f),"Impact must touch the mouse body, not the transparent sprite padding.");
                Advance(.1f);
                Assert.That(x-actor.Position.x,Is.EqualTo(arena.EnemyHitReaction.BasicPushDistance).Within(.003f),presentation.species);
                Assert.That(actor.bodyCollider.size,Is.EqualTo(size));Assert.That(actor.hurtCollider.size,Is.EqualTo(hurtSize));
                Assert.That(flash.enabled,Is.False);Assert.That(presentation.State,Is.EqualTo("hurt"));
                var after=new ParticleSystem.Particle[128];particles.GetParticles(after);
                Assert.That(after[0].position,Is.EqualTo(before[0].position),"World particles must not follow the moved actor root.");
                float stopped=actor.Position.x;Advance(.1f);Assert.That(actor.Position.x,Is.EqualTo(stopped).Within(.001f));
                if(presentation.species=="05_boar")
                { Assert.That(actor.visual.transform.localPosition,Is.EqualTo(Vector3.zero));Assert.That(actor.visual.transform.localRotation,Is.EqualTo(Quaternion.identity)); }
                actor.shield=10;health=actor.health;actor.Receive(1,arena.Player,-1,true,null);presentation.Draw(0);
                Assert.That(actor.health,Is.EqualTo(health),"Absorbed valid hits still produce feedback.");
                Assert.That(flash.enabled,Is.True);Assert.That(particles.particleCount,Is.EqualTo(36));
                presentation.Draw(0);Assert.That(particles.particleCount,Is.EqualTo(36),"Drawing twice must not replay a hit.");
                Advance(.31f);Assert.That(presentation.State,Is.Not.EqualTo("hurt"));
                actor.Receive(10000,arena.Player,-1,false,null);presentation.Draw(0);
                Assert.That(presentation.State,Is.EqualTo("death"));Assert.That(flash.enabled,Is.False);
                arena.ResetTest();
                Assert.That(particles.particleCount,Is.Zero);Assert.That(flash.enabled,Is.False);
                Assert.That(actor.visual.transform.localRotation,Is.EqualTo(Quaternion.identity));
                Assert.That(actor.visual.transform.localScale,Is.EqualTo(Vector3.one));
                foreach(var enemy in arena.enemies)enemy.aggressive=false;
            }
            yield return null;
        }
        [UnityTest] public IEnumerator NativeParticlesMoveExpireAndPauseWithTheExistingControlOwner()
        {
            var presentation=presentations.First(p=>p.species=="02_mole");Place(presentation);var actor=presentation.actor;
            actor.Receive(1,arena.Player,-1,true,null);presentation.Draw(0);
            var particles=actor.GetComponentInChildren<ParticleSystem>();
            var values=new ParticleSystem.Particle[128];particles.GetParticles(values);var emitted=values[0].position;
            float now=arena.Now;
            using(var scope=arena.Controls.OpenScope("hit feedback pause verification"))
            {
                using(var pause=scope.PauseSimulation())
                {
                    presentation.Draw(0);Assert.That(particles.isPaused,Is.True);
                    float particleTime=particles.time;
                    yield return new WaitForSecondsRealtime(.08f);
                    Assert.That(arena.Now,Is.EqualTo(now));Assert.That(particles.time,Is.EqualTo(particleTime));
                    Assert.That(particles.particleCount,Is.EqualTo(18));
                }
                presentation.Draw(0);Assert.That(particles.isPlaying,Is.True);
                yield return new WaitForSecondsRealtime(.04f);
                Assert.That(particles.GetParticles(values),Is.GreaterThan(0));
                Assert.That(Vector3.Distance(values[0].position,emitted),Is.GreaterThan(.001f));
                yield return new WaitForSecondsRealtime(.45f);
                Assert.That(particles.particleCount,Is.Zero);
                Assert.That(arena.Now,Is.EqualTo(now),"Native presentation time must not advance the manual combat owner.");
            }
        }
        [UnityTest] public IEnumerator DisablingPresentationClearsEffectsWithoutReplayingOldHitsOrChangingMovement()
        {
            var presentation=presentations.First(p=>p.species=="02_mole");Place(presentation);var actor=presentation.actor;
            actor.Receive(1,arena.Player,-1,true,null);presentation.Draw(0);
            var particles=actor.GetComponentInChildren<ParticleSystem>();Assert.That(particles.particleCount,Is.EqualTo(18));
            presentation.enabled=false;Assert.That(particles.particleCount,Is.Zero);
            float x=actor.Position.x;Advance(.1f);Assert.That(x-actor.Position.x,Is.EqualTo(.25f).Within(.003f));
            presentation.enabled=true;presentation.Draw(0);Assert.That(particles.particleCount,Is.Zero);
            yield return null;
        }
    }
}
