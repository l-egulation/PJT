using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class MeleeBasicMotionIntegrationTests
    {
        [UnityTest] public IEnumerator GameEachMeleeHitCapturesItsOwnBodyAndCueAtStart() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabEachMeleeHitCapturesItsOwnBodyAndCueAtStart() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static CharacterAnimationCatalog Catalog(string form, int hit, BasicData source, Sprite[] sprites)
        {
            string state = "attack" + (hit + 1);
            var clip = new CharacterClip { name = state, frames = new[] { state + "-a", state + "-b" }, seconds = new[] { source.duration * .25f, source.duration * .75f }, sourceImpact = source.impact, fxStart = 0, sprites = (Sprite[])sprites.Clone() };
            var effect = new CharacterEffect { attack = state, frames = new[] { state + "-fx-a", state + "-fx-b" }, seconds = new[] { .04f, .06f }, x = new[] { .3f, .8f }, y = new[] { 1f, 1.2f }, sprites = (Sprite[])sprites.Clone() };
            return new CharacterAnimationCatalog { sets = new[] { new CharacterAnimationSet { form = form, clips = new[] { clip }, effects = new[] { effect } } } };
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            p.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), 0);
            p.Position = Vector2.zero; p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, int hit)
        {
            typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena.Player, new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), hit, -1f });
            return arena.Player.currentCast;
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var original = arena.characterAnimations;
            var texture = new Texture2D(2, 1); var sprites = new[] { Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32), Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32) };
            var floor = new GameObject("MeleeFormsMotionFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    int tested = 0;
                    foreach (var form in arena.catalog.forms.Where(f => f.basics.Any(b => string.IsNullOrEmpty(b.kind)))) for (int hit = 0; hit < form.basics.Length; hit++)
                    {
                        tested++; var source = form.basics[hit]; Setup(arena, driver, form.id); var catalog = Catalog(form.id, hit, source, sprites); arena.characterAnimations = catalog;
                        var cast = Start(arena, hit); string version = arena.Presentation.CurrentAttackMotionVersion; Assert.That(version, Is.Not.Null.And.Not.Empty, form.id + "/" + hit);
                        var clip = catalog.sets[0].clips[0]; var effect = catalog.sets[0].effects[0];
                        clip.frames[0] = "edited"; clip.seconds[0] = 99; clip.sourceImpact = 90; clip.fxStart = 88; clip.sprites[0] = sprites[1];
                        effect.frames[0] = "edited-fx"; effect.seconds[0] = 99; effect.x[0] = effect.y[0] = 99; effect.flipX = true; effect.sprites[0] = sprites[1];
                        arena.CaptureCharacterAttack(cast); arena.Presentation.Draw(0);
                        Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version)); Assert.That(arena.Presentation.CurrentForm, Is.EqualTo(form.id)); Assert.That(arena.Presentation.CurrentClip, Is.EqualTo("attack" + (hit + 1))); Assert.That(p.visual.sprite, Is.SameAs(sprites[0]));
                        if (form.id == "shield") Assert.That(arena.Presentation.LastEffectHandle, Is.Null, "Reviewed shield contact replaces the generic cue");
                        else
                        {
                            var cue = arena.Presentation.LastEffectHandle; Assert.That(cue, Is.Not.Null); Assert.That(cue.Occurrence.ExecutionId, Is.EqualTo(cast.ExecutionId));
                            var renderer = GameObject.Find("BasicAttackEffect_" + form.id + "_attack" + (hit + 1)).GetComponent<SpriteRenderer>();
                            Assert.That(renderer.sprite, Is.SameAs(sprites[0])); Assert.That(renderer.flipX, Is.False); Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(.3f, 1, 0)));
                        }
                        arena.characterAnimations = null; driver.Step(.02f); arena.Presentation.Draw(0);
                        Assert.That(p.visual.sprite, Is.SameAs(sprites[0])); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version));
                        for (int tick = 0; tick < 60; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                        Assert.That(arena.enemies[0].health, Is.EqualTo(100 - source.damage)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                        arena.characterAnimations = catalog; Start(arena, hit); arena.Presentation.Draw(0);
                        Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.EqualTo(version)); Assert.That(p.visual.sprite, Is.SameAs(sprites[1]));

                        Setup(arena, driver, form.id); catalog = Catalog(form.id, hit, source, sprites); catalog.sets[0].clips[0].sprites = new[] { sprites[0] }; arena.characterAnimations = catalog;
                        cast = Start(arena, hit); Assert.That(arena.Presentation.LastAttackMotionError, Does.Contain("sprite count"));
                        catalog.sets[0].clips[0].sprites = sprites; arena.CaptureCharacterAttack(cast); arena.Presentation.Draw(0);
                        Assert.That(p.visual.sprite, Is.Null); Assert.That(arena.Presentation.CurrentForm, Is.EqualTo(form.id));
                        for (int tick = 0; tick < 60; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                        Assert.That(arena.enemies[0].health, Is.EqualTo(100 - source.damage)); Assert.That(arena.Presentation.LastEffectHandle, Is.Null);
                        if (form.id != "base_sword")
                        {
                            Setup(arena, driver, form.id); catalog = Catalog("base_sword", 0, source, sprites); arena.characterAnimations = catalog;
                            Start(arena, hit); arena.Presentation.Draw(0);
                            Assert.That(p.visual.sprite, Is.SameAs(sprites[0]), "Existing body fallback remains available: " + form.id);
                            Assert.That(arena.Presentation.CurrentForm, Is.EqualTo("base_sword")); Assert.That(arena.Presentation.LastEffectHandle, Is.Null, "Fallback art cannot emit sword effects for another weapon.");
                            catalog.sets[0].clips[0].sprites[0] = sprites[1]; arena.characterAnimations = null; arena.Presentation.Draw(0);
                            Assert.That(p.visual.sprite, Is.SameAs(sprites[0]), "The fallback choice is captured at start too.");
                        }
                    }
                    Assert.That(tested, Is.EqualTo(17));
                }
            }
            finally
            {
                if (arena) { arena.Presentation.Reset(); arena.characterAnimations = original; }
                Object.Destroy(floor); foreach (var sprite in sprites) Object.Destroy(sprite); Object.Destroy(texture);
            }
        }
    }
}
