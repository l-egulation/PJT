using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class DirectMeleeSpecialMotionTests
    {
        [UnityTest] public IEnumerator GameDirectSpecialsCaptureBodyWithoutEmittingBasicCues() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabDirectSpecialsCaptureBodyWithoutEmittingBasicCues() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static CharacterAnimationCatalog Catalog(string form, float impact, Sprite[] sprites)
        {
            var clip = new CharacterClip { name = "attack1", frames = new[] { "a", "b" }, seconds = new[] { .1f, .9f }, sourceImpact = impact, fxStart = 0, sprites = (Sprite[])sprites.Clone() };
            var effect = new CharacterEffect { attack = "attack1", frames = new[] { "fx" }, seconds = new[] { .1f }, x = new[] { 0f }, y = new[] { 1f }, sprites = new[] { sprites[0] } };
            return new CharacterAnimationCatalog { sets = new[] { new CharacterAnimationSet { form = form, clips = new[] { clip }, effects = new[] { effect } } } };
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form, int slot)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear(); p.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), slot);
            p.Position = Vector2.zero; p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena)
        {
            typeof(CombatPlayer).GetMethod("BeginSpecial", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena.Player, new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), -1f }); return arena.Player.currentCast;
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var original = arena.characterAnimations;
            var texture = new Texture2D(2, 1); var sprites = new[] { Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32), Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32) };
            var floor = new GameObject("DirectSpecialMotionFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (var action in new[] { (form:"base_sword",slot:0,id:"base_sword_default"), (form:"rapier",slot:1,id:"rapier_range"), (form:"greatsword",slot:1,id:"greatsword_range") })
                {
                    var definition = arena.GetDirectMeleeSpecial(action.id); Setup(arena, driver, action.form, action.slot); var catalog = Catalog(action.form, definition.Impact, sprites); arena.characterAnimations = catalog;
                    var cast = Start(arena); string version = arena.Presentation.CurrentAttackMotionVersion; Assert.That(version, Is.Not.Null.And.Not.Empty);
                    var clip = catalog.sets[0].clips[0]; clip.frames[0] = "edited"; clip.seconds[0] = 2; clip.sourceImpact = 1; clip.sprites[0] = sprites[1]; catalog.sets[0].effects[0].sprites[0] = sprites[1];
                    arena.CaptureCharacterAttack(cast); arena.Presentation.Draw(0);
                    Assert.That(p.visual.sprite, Is.SameAs(sprites[0])); Assert.That(arena.Presentation.CurrentForm, Is.EqualTo(action.form)); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version)); Assert.That(arena.Presentation.LastEffectHandle, Is.Null); Assert.That(arena.Presentation.ActiveEffects, Is.Zero);
                    arena.characterAnimations = null; driver.Step(.02f); arena.Presentation.Draw(0); Assert.That(p.visual.sprite, Is.SameAs(sprites[0]));
                    for (int tick = 0; tick < 80; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(100 - definition.Damage)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.Presentation.LastEffectHandle, Is.Null);
                    // Next execution adopts the new art metadata, with no change
                    // to the fixed special timing or basic-Cue policy.
                    driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false; arena.characterAnimations = catalog; Start(arena); arena.Presentation.Draw(0);
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.EqualTo(version)); Assert.That(p.visual.sprite, Is.SameAs(sprites[1])); Assert.That(arena.Presentation.LastEffectHandle, Is.Null);

                    Setup(arena, driver, action.form, action.slot); catalog = Catalog(action.form, definition.Impact, sprites); catalog.sets[0].clips[0].sprites = new[] { sprites[0] }; arena.characterAnimations = catalog;
                    cast = Start(arena); Assert.That(arena.Presentation.LastAttackMotionError, Does.Contain("sprite count")); catalog.sets[0].clips[0].sprites = sprites; arena.CaptureCharacterAttack(cast); arena.Presentation.Draw(0); Assert.That(p.visual.sprite, Is.Null);
                    for (int tick = 0; tick < 80; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(100 - definition.Damage)); Assert.That(arena.Presentation.LastEffectHandle, Is.Null);
                    if (action.form != "base_sword")
                    {
                        Setup(arena, driver, action.form, action.slot); arena.characterAnimations = Catalog("base_sword", definition.Impact, sprites); Start(arena); arena.Presentation.Draw(0);
                        Assert.That(p.visual.sprite, Is.SameAs(sprites[0])); Assert.That(arena.Presentation.CurrentForm, Is.EqualTo("base_sword")); Assert.That(arena.Presentation.LastEffectHandle, Is.Null);
                    }
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
