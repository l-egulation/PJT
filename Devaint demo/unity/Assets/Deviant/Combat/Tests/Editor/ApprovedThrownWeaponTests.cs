using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Deviant.Combat.Contracts;
using Object=UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    [Category("RequiresArt")]
    public class ApprovedThrownWeaponTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation sim;
        CombatPlayer P=>arena.Player;
        [UnitySetUp] public IEnumerator Setup()
        {
            CombatTestScene.Open(requireArt:true);yield return new EnterPlayMode();yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>();P.UseTestInput=true;sim=arena.AcquireManualSimulation();
        }
        [UnityTearDown] public IEnumerator Teardown(){sim?.Dispose();if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        void Equip(string form)
        {
            sim.Reset();P.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id==form),0);
            foreach(var enemy in arena.enemies)enemy.Position=new Vector2(100,0);
            Advance(.02f);arena.Presentation.Draw(0);
        }
        void Advance(float seconds)
        {
            int count=Mathf.CeilToInt(seconds/.005f);
            for(int i=0;i<count;i++){sim.Step(seconds/count);arena.Presentation.Draw(seconds/count);}
        }
        void Fire(){sim.Step(.001f,new CombatInput(pressed:CombatButtons.Basic));arena.Presentation.Draw(.001f);}
        CharacterAnimationSet Set(string form)=>Array.Find(arena.characterAnimations.sets,s=>s.form==form);

        [UnityTest] public IEnumerator BothApprovedFormsLoadAllMotionsWithoutSwordFallback()
        {
            Assert.That(arena.characterAnimations.sets.Length,Is.EqualTo(13));
            foreach(var form in new[]{"shuriken","bomb"})
            {
                Equip(form);Assert.That(arena.Presentation.CurrentForm,Is.EqualTo(form));
                foreach(var key in new[]{"idle","walk","run","jump_start","jump_rise","jump_apex","fall_start","fall_loop","land","dash","hurt","death","attack1"})
                {
                    var clip=Set(form).Find(key);Assert.That(clip,Is.Not.Null,form+"/"+key);
                    foreach(var sprite in clip.sprites)
                    {
                        Assert.That(sprite,Is.Not.Null);Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
                        Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
                    }
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ShurikenAppearsOnlyOnReleaseAndKeepsFlatNativeShapeAfterCancel()
        {
            Equip("shuriken");Fire();Advance(.08f);Assert.That(arena.PlayerShotCount,Is.Zero);
            Advance(.03f);Assert.That(arena.PlayerShotCount,Is.EqualTo(1));
            var shot=arena.playerShots[0];var art=shot.GetComponentInChildren<ApprovedThrownVisual>();
            Assert.That(art,Is.Not.Null);art.Sample();var sprite=art.GetComponent<SpriteRenderer>().sprite;
            Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(28,6)));
            Assert.That(art.GetComponent<Collider2D>(),Is.Null);Assert.That(shot.Definition.Radius,Is.EqualTo(.2f));
            P.CancelAction();Advance(.02f);Assert.That(shot,Is.Not.Null);Assert.That(art,Is.Not.Null);
            sim.Reset();yield return null;Assert.That(Object.FindObjectsByType<ApprovedThrownVisual>(FindObjectsSortMode.None),Is.Empty);
        }
        [UnityTest] public IEnumerator QueuedPairRestartsAtChainPoseAndStoppingPlaysFinalReturn()
        {
            Equip("shuriken");Fire();Advance(.13f);Fire();Advance(.13f);
            Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("attack2"));
            Advance(.13f);Fire();Advance(.23f);
            Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("attack1_chain"));
            Advance(.25f);Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("recover1"));
            Advance(.32f);Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("idle"));
            Assert.That(arena.PlayerShotCount,Is.EqualTo(3));
            yield return null;
        }
        [UnityTest] public IEnumerator OneClickReturnsWithoutAutomaticallyThrowingSecond()
        {
            Equip("shuriken");Fire();Advance(.27f);
            Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("recover1"));
            Advance(.35f);Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("idle"));
            Assert.That(arena.PlayerShotCount,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator BombUsesApprovedSpriteAtReleaseAndPreservesThirtyDegreeBallistics()
        {
            Equip("bomb");Fire();Advance(.14f);Assert.That(arena.bombs.Count,Is.Zero);
            Advance(.04f);Assert.That(arena.bombs.Count,Is.EqualTo(1));
            var bomb=arena.bombs[0];Assert.That(bomb.GetComponentInChildren<ApprovedThrownVisual>(),Is.Not.Null);
            Assert.That(bomb.velocity.x,Is.EqualTo(10*Mathf.Cos(30*Mathf.Deg2Rad)).Within(.001f));
            Assert.That(bomb.Definition.Gravity,Is.EqualTo(20));Assert.That(bomb.damage,Is.EqualTo(24));
            Assert.That(arena.Presentation.CurrentForm,Is.EqualTo("bomb"));yield return null;
        }
    }
}
