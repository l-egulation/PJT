using System;
using System.Collections;
using System.Reflection;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SwordComboMotionTests
    {
        [UnityTest] public IEnumerator GameCapturesSecondBodyAndCueBeforeDrawing() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabCapturesSecondBodyAndCueBeforeDrawing() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static CharacterAnimationCatalog Catalog(Sprite[] sprites)
        {
            var idle = new CharacterClip { name = "idle", frames = new[] { "idle" }, seconds = new[] { 1f }, sprites = new[] { sprites[0] } };
            var first = new CharacterClip { name = "attack1", frames = new[] { "first-a", "first-b" }, seconds = new[] { .1f, .3f }, sourceImpact = .2f, fxStart = -1, sprites = (Sprite[])sprites.Clone() };
            var second = new CharacterClip { name = "attack2", frames = new[] { "second-a", "second-b" }, seconds = new[] { .1f, .5f }, sourceImpact = .2f, fxStart = .1f, sprites = (Sprite[])sprites.Clone() };
            var effect = new CharacterEffect { attack = "attack2", scale = 1.25f, frames = new[] { "second-fx-a", "second-fx-b" }, seconds = new[] { .08f, .12f }, x = new[] { .3f, .8f }, y = new[] { 1f, 1.2f }, sprites = (Sprite[])sprites.Clone() };
            return new CharacterAnimationCatalog { sets = new[] { new CharacterAnimationSet { form = "base_sword", clips = new[] { idle, first, second }, effects = new[] { effect } } } };
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var error), Is.True, error);
            p.Position = Vector2.zero; p.velocity = Vector2.zero; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(3, 0); Physics2D.SyncTransforms();
        }
        static CombatCast StartSecond(CombatArena arena)
        {
            typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena.Player, new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), 1, -1f });
            return arena.Player.currentCast;
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var original = arena.characterAnimations;
            var texture = new Texture2D(2, 1); var sprites = new[] { Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32), Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32) };
            var floor = new GameObject("SwordMotionFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Setup(arena, driver); var catalog = Catalog(sprites); arena.characterAnimations = catalog;
                    for (int i = 0; i < 5; i++) p.Weapon.StatSlots[i] = 3;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var first = p.currentCast; string firstVersion = arena.Presentation.CurrentAttackMotionVersion;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                    for (int tick = 0; tick < 100 && p.currentCast == first; tick++) driver.Step(.02f);
                    var second = p.currentCast; Assert.That(second, Is.Not.Null); Assert.That(second.Definition.Id, Is.EqualTo("base_sword_basic_1"));
                    Assert.That(second.StartedAt, Is.EqualTo(first.EndTime)); Assert.That(second.StartedAt / .02f % 1, Is.GreaterThan(.01f), "High-speed queued completion starts inside a tick.");
                    string version = arena.Presentation.CurrentAttackMotionVersion; Assert.That(version, Is.Not.Null.And.Not.EqualTo(firstVersion), "Second-hit capture happens before its first draw.");
                    var clip = catalog.sets[0].clips[2]; var effect = catalog.sets[0].effects[0];
                    clip.frames[0] = "edited"; clip.seconds[0] = 1; clip.sourceImpact = .8f; clip.fxStart = .9f; clip.sprites[0] = sprites[1]; clip.sprites[1] = sprites[0];
                    effect.frames[0] = "edited-fx"; effect.seconds[0] = 1; effect.x[0] = effect.y[0] = 99; effect.flipX = true; effect.sprites[0] = sprites[1];
                    effect.scale = 2;
                    arena.CaptureCharacterAttack(second); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version));
                    arena.Presentation.Draw(0); Assert.That(p.visual.sprite, Is.SameAs(sprites[0])); Assert.That(arena.Presentation.CurrentClip, Is.EqualTo("attack2"));
                    Vector2 origin = default;
                    for (int tick = 0; tick < 5; tick++)
                    {
                        driver.Step(.02f); bool emitted = arena.Presentation.LastEffectHandle != null; var position = p.Position; arena.Presentation.Draw(0);
                        if (!emitted && arena.Presentation.LastEffectHandle != null) origin = position;
                    }
                    var cue = arena.Presentation.LastEffectHandle; Assert.That(cue, Is.Not.Null); Assert.That(cue.Occurrence.ExecutionId, Is.EqualTo(second.ExecutionId));
                    var renderer = GameObject.Find("BasicAttackEffect_base_sword_attack2").GetComponent<SpriteRenderer>();
                    Assert.That(renderer.transform.localScale, Is.EqualTo(new Vector3(1.25f,1.25f,1)), "The running cue keeps its captured media scale.");
                    Assert.That(renderer.sprite, Is.SameAs(sprites[0])); Assert.That(renderer.flipX, Is.False); Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(origin.x + .3f, origin.y + 1, 0)));
                    Assert.That(p.visual.sprite, Is.SameAs(sprites[1]));
                    arena.characterAnimations = null; driver.Step(.02f); arena.Presentation.Draw(0);
                    Assert.That(p.visual.sprite, Is.SameAs(sprites[1])); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version));
                    for (int tick = 0; tick < 40; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(74)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2));
                    arena.characterAnimations = catalog; StartSecond(arena); arena.Presentation.Draw(0);
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.EqualTo(version)); Assert.That(p.visual.sprite, Is.SameAs(sprites[1]), "The next execution sees new art metadata.");

                    Setup(arena, driver); catalog = Catalog(sprites); catalog.sets[0].clips[2].sprites = new[] { sprites[0] }; arena.characterAnimations = catalog;
                    second = StartSecond(arena); Assert.That(arena.Presentation.LastAttackMotionError, Does.Contain("sprite count")); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Null);
                    catalog.sets[0].clips[2].sprites = sprites; arena.CaptureCharacterAttack(second); arena.Presentation.Draw(0); Assert.That(p.visual.sprite, Is.Null, "The first failure remains captured.");
                    for (int tick = 0; tick < 40; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(87)); Assert.That(arena.Presentation.LastEffectHandle, Is.Null);

                    Setup(arena, driver); arena.characterAnimations = null; second = StartSecond(arena);
                    Assert.That(arena.Presentation.LastAttackMotionError, Is.Not.Null); arena.characterAnimations = Catalog(sprites); arena.CaptureCharacterAttack(second);
                    for (int tick = 0; tick < 40; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(87)); Assert.That(arena.Presentation.LastEffectHandle, Is.Null);
                    driver.Reset(); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Null); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
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
