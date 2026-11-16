using System;
using System.Collections;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class GenericAttackMotionTests
    {
        [UnityTest] public IEnumerator GamePinsRemainingAttackMediaBeforeTheFirstDraw() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabPinsRemainingAttackMediaBeforeTheFirstDraw() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static CharacterAnimationCatalog Catalog(string form, Sprite first, Sprite second)
        {
            var clip = new CharacterClip { name = "attack1", frames = new[] { "first", "second" }, seconds = new[] { .1f, .9f }, sourceImpact = .2f, fxStart = -1, sprites = new[] { first, second } };
            return new CharacterAnimationCatalog { sets = new[] { new CharacterAnimationSet { form = form, clips = new[] { clip } } } };
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var original = arena.characterAnimations;
            var texture = new Texture2D(2, 1); var first = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32); var second = Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (var action in new[] { (form:"base_sword", special:1, basic:false), (form:"whip", special:2, basic:false), (form:"shield", special:1, basic:false), (form:"bomb", special:0, basic:true), (form:"shuriken", special:0, basic:true), (form:"launcher", special:2, basic:true) })
                {
                    driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
                    p.selected = 0; p.InputBlocked = false; foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
                    Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == action.form), action.special, out var error), Is.True, error);
                    p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = false; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
                    var catalog = Catalog(action.form, first, second); arena.characterAnimations = catalog;
                    driver.Step(.02f, new CombatInput(0, action.basic ? CombatButtons.Basic : CombatButtons.Special)); var cast = p.currentCast; Assert.That(cast, Is.Not.Null, action.form);
                    var clip = catalog.sets[0].clips[0]; clip.frames[0] = "edited"; clip.seconds[0] = 2; clip.sourceImpact = .9f; clip.sprites[0] = second;
                    arena.Presentation.Draw(0); Assert.That(p.visual.sprite, Is.SameAs(first), action.form + " running media must retain its start binding before first Draw.");
                    arena.characterAnimations = null; driver.Step(.02f); arena.Presentation.Draw(0); Assert.That(p.visual.sprite, Is.SameAs(first), action.form + " started body survives catalog removal.");
                    Assert.That(arena.Presentation.LastEffectHandle, Is.Null, "No special or converted basic acquires another basic's Cue.");
                    driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false; arena.characterAnimations = catalog;
                    p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = false; Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, action.basic ? CombatButtons.Basic : CombatButtons.Special)); arena.Presentation.Draw(0); Assert.That(p.visual.sprite, Is.SameAs(second), action.form + " next execution adopts edited media.");
                }
            }
            finally { arena.Presentation.Reset(); arena.characterAnimations = original; Object.Destroy(first); Object.Destroy(second); Object.Destroy(texture); }
        }
    }
}
