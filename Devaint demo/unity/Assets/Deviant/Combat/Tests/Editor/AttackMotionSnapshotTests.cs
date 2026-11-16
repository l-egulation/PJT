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
    public class AttackMotionSnapshotTests
    {
        [Test] public void ExistingAttackCatalogMetadataCompilesWithoutLoadingSprites()
        {
            var animations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            foreach (var set in animations.sets)
            {
                var form = Array.Find(combat.forms, f => f.id == (set.gameplayForm ?? set.form)); Assert.That(form, Is.Not.Null);
                for (int i = 0; i < form.basics.Length; i++)
                {
                    var clip = set.Find("attack" + (i + 1)); if (clip == null) continue;
                    var hit = form.basics[i];
                    var timing = new Deviant.Definitions.AttackMotionTiming(set.form + "/" + clip.name, clip.frames, clip.seconds, clip.contact, clip.sourceImpact, clip.fxStart, hit.impact, hit.duration);
                    Assert.That(timing.SourceTime(hit.impact), Is.EqualTo(timing.SourceImpact)); Assert.That(timing.FrameAt(0), Is.InRange(0, clip.frames.Length - 1));
                }
            }
        }
        [UnityTest] public IEnumerator GameKeepsStartingBodyAndCueMotion() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabKeepsStartingBodyAndCueMotion() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static CharacterAnimationCatalog Catalog(Sprite[] sprites)
        {
            var idle = new CharacterClip { name = "idle", frames = new[] { "idle" }, seconds = new[] { 1f }, sprites = new[] { sprites[0] } };
            var attack = new CharacterClip { name = "attack1", frames = new[] { "a", "b" }, seconds = new[] { .1f, .3f }, sourceImpact = .2f, fxStart = .1f, sprites = (Sprite[])sprites.Clone() };
            var effect = new CharacterEffect { attack = "attack1", frames = new[] { "fx-a", "fx-b" }, seconds = new[] { .08f, .12f }, x = new[] { .3f, .8f }, y = new[] { 1f, 1.2f }, sprites = (Sprite[])sprites.Clone() };
            return new CharacterAnimationCatalog { sets = new[] { new CharacterAnimationSet { form = "base_sword", clips = new[] { idle, attack }, effects = new[] { effect } } } };
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            arena.Player.selected = 0; Assert.That(arena.Player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var error), Is.True, error);
            arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); arena.enemies[0].Position = new Vector2(3, 0); Physics2D.SyncTransforms();
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var original = arena.characterAnimations;
            var texture = new Texture2D(2, 1); var sprites = new[] { Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32), Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32) };
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Setup(arena, driver); var catalog = Catalog(sprites); arena.characterAnimations = catalog;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var cast = player.currentCast;
                    Assert.That(cast, Is.Not.Null); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.Null.And.Not.Empty, "Capture occurs at action start, before first draw.");
                    var version = arena.Presentation.CurrentAttackMotionVersion; var definitionVersion = cast.DefinitionVersion;
                    var clip = catalog.sets[0].clips[1]; var effect = catalog.sets[0].effects[0];
                    clip.frames[0] = "edited"; clip.seconds[0] = 1; clip.sourceImpact = .8f; clip.fxStart = .9f;
                    clip.sprites[0] = sprites[1]; clip.sprites[1] = sprites[0];
                    effect.frames[0] = "edited-effect"; effect.seconds[0] = 1; effect.x[0] = 99; effect.y[0] = 99; effect.flipX = true; effect.sprites[0] = sprites[1];
                    arena.CaptureCharacterAttack(cast);
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version), "Duplicate start notification cannot recompile an active snapshot.");
                    arena.Presentation.Draw(0); Assert.That(player.visual.sprite, Is.SameAs(sprites[0]));
                    Vector2 cueOrigin = default;
                    for (int tick = 0; tick < 6; tick++)
                    {
                        driver.Step(.02f); var beforeCue = arena.Presentation.LastEffectHandle; var position = player.Position; arena.Presentation.Draw(0);
                        if (beforeCue == null && arena.Presentation.LastEffectHandle != null) cueOrigin = position;
                    }
                    Assert.That(player.visual.sprite, Is.SameAs(sprites[1])); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version));
                    var cue = arena.Presentation.LastEffectHandle; Assert.That(cue, Is.Not.Null); Assert.That(cue.Occurrence.ExecutionId, Is.EqualTo(cast.ExecutionId));
                    var renderer = GameObject.Find("BasicAttackEffect_base_sword_attack1").GetComponent<SpriteRenderer>();
                    Assert.That(renderer.sprite, Is.SameAs(sprites[0])); Assert.That(renderer.flipX, Is.False); Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(cueOrigin.x + .3f, cueOrigin.y + 1, 0)));
                    Assert.That(cast.DefinitionVersion, Is.EqualTo(definitionVersion)); Assert.That(cast.ImpactTime, Is.EqualTo(.2f)); Assert.That(cast.Definition.End, Is.EqualTo(.4f));
                    var clips = catalog.sets[0].clips; var effects = catalog.sets[0].effects;
                    catalog.sets[0].clips = null; catalog.sets[0].effects = null; arena.Presentation.Draw(0);
                    Assert.That(player.visual.sprite, Is.SameAs(sprites[1]), "The started attack no longer queries mutable clip/effect lists.");
                    catalog.sets[0].clips = clips; catalog.sets[0].effects = effects;
                    arena.characterAnimations = null; driver.Step(.02f); arena.Presentation.Draw(0); Assert.That(player.visual.sprite, Is.SameAs(sprites[1]), "A started binding survives removal of the mutable catalog.");
                    arena.characterAnimations = catalog;
                    for (int tick = 0; tick < 40; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(87));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); arena.Presentation.Draw(0);
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.EqualTo(version)); Assert.That(player.visual.sprite, Is.SameAs(sprites[1]));
                    Assert.That(player.currentCast.DefinitionVersion, Is.EqualTo(definitionVersion));

                    Setup(arena, driver); catalog = Catalog(sprites); catalog.sets[0].clips[1].sprites = new[] { sprites[0] }; arena.characterAnimations = catalog;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); Assert.That(player.currentCast, Is.Not.Null);
                    Assert.That(arena.Presentation.LastAttackMotionError, Does.Contain("sprite count")); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Null);
                    for (int tick = 0; tick < 15; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(87), "Invalid presentation does not reject or retime the hit."); Assert.That(arena.Presentation.ActiveEffects, Is.Zero);

                    Setup(arena, driver); arena.characterAnimations = null; driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                    Assert.That(arena.Presentation.LastAttackMotionError, Is.Not.Null); arena.characterAnimations = Catalog(sprites);
                    arena.CaptureCharacterAttack(player.currentCast); arena.Presentation.Draw(0);
                    Assert.That(player.visual.sprite, Is.Null, "A binding missing at start is not silently adopted mid-attack.");
                    for (int tick = 0; tick < 15; tick++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(87)); Assert.That(arena.Presentation.LastEffectHandle, Is.Null);
                    driver.Reset(); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Null); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
                }
            }
            finally
            {
                if (arena) { arena.Presentation.Reset(); arena.characterAnimations = original; }
                foreach (var sprite in sprites) if (sprite) Object.Destroy(sprite); if (texture) Object.Destroy(texture);
            }
        }
    }
}
