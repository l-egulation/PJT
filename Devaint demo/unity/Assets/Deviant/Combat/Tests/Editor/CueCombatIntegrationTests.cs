using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CueCombatIntegrationTests
    {
        sealed class Clock : ICueClock { public float Time; public float ReadElapsed() => Time; }
        static PresentationCueDefinition Definition(string id, float first = .2f) => new PresentationCueDefinition(id, new[] { "a", "b" }, new[] { first, .1f }, new[] { 1f, 2f }, new[] { 3f, 4f }, true);
        static Sprite[] Sprites(Texture2D texture) => new[] {
            Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32), Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32) };
        [Test] public void SpriteViewsKeepTheirVersionAnchorMirrorAndHaveNoPhysicsAuthority()
        {
            var texture = new Texture2D(2, 1); var sprites = Sprites(texture);
            try
            {
                using (var factory = new SpriteCueFactory()) using (var player = new CuePlayer(factory)) using (var owner = player.OpenScope("view test"))
                {
                    var definition = Definition("CueSpriteFixture"); factory.Publish(definition, sprites); var clock = new Clock();
                    var handle = owner.Play(definition, new CueOccurrence(Guid.NewGuid(), definition.Id), new CueAnchor(10, 20, -1), clock);
                    var renderer = GameObject.Find(definition.Id).GetComponent<SpriteRenderer>();
                    Assert.That(renderer.sprite, Is.SameAs(sprites[0])); Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(9, 23, 0)));
                    Assert.That(renderer.flipX, Is.True); Assert.That(renderer.transform.localScale.x, Is.EqualTo(-1)); Assert.That(renderer.sortingOrder, Is.EqualTo(18));
                    Assert.That(renderer.GetComponent<Collider2D>(), Is.Null); Assert.That(renderer.GetComponent<Rigidbody2D>(), Is.Null);
                    var edited = Definition(definition.Id, 1); factory.Publish(edited, new[] { sprites[1], sprites[0] });
                    clock.Time = .2f; player.Tick(); Assert.That(renderer.sprite, Is.SameAs(sprites[1]));
                    Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(8, 24, 0))); Assert.That(handle.DefinitionVersion, Is.EqualTo(definition.ContentHash));
                    var next = owner.Play(edited, new CueOccurrence(Guid.NewGuid(), edited.Id), new CueAnchor(0, 0, 1), new Clock());
                    Assert.That(factory.LiveViewCount, Is.EqualTo(2)); handle.Cancel(); Assert.That(next.Status, Is.EqualTo(CueStatus.Playing));
                    Assert.That(factory.LiveViewCount, Is.EqualTo(1)); player.Reset(); Assert.That(factory.LiveViewCount, Is.Zero);
                    var missing = Definition("CueMissingFixture"); factory.Publish(missing, new Sprite[] { sprites[0], null });
                    using (var scope = player.OpenScope("missing"))
                    {
                        var failure = scope.Play(missing, new CueOccurrence(Guid.NewGuid(), missing.Id), new CueAnchor(0, 0, 1), new Clock());
                        Assert.That(failure.Status, Is.EqualTo(CueStatus.Failed)); Assert.That(failure.Error, Does.Contain("Missing"));
                        Assert.That(GameObject.Find(missing.Id), Is.Null); Assert.That(factory.LiveViewCount, Is.Zero);
                    }
                }
            }
            finally { foreach (var sprite in sprites) Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }
        [Test] public void CueFrameSelectionMatchesExistingEffectIntervals()
        {
            var effect = new CharacterEffect { seconds = new[] { .07f, .13f, .2f } };
            var cue = new PresentationCueDefinition("intervals", new[] { "a", "b", "c" }, effect.seconds, new float[3], new float[3]);
            foreach (float age in new[] { -.1f, 0, .069f, .07f, .199f, .2f, .399f })
                Assert.That(cue.FrameAt(age), Is.EqualTo(effect.FrameIndex(age)), "age=" + age);
        }
        [UnityTest] public IEnumerator GameCueCancelMissingAndDisabledKeepCombatIdentical() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabCueCancelMissingAndDisabledKeepCombatIdentical() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var texture = new Texture2D(2, 1); var sprites = Sprites(texture); var original = arena.characterAnimations;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    float[] baseline = null;
                    foreach (string mode in new[] { "normal", "cancel", "missing", "disabled" })
                    {
                        Setup(arena, driver); var set = Catalog(sprites).sets[0];
                        if (mode == "missing") set.effects[0].sprites[1] = null;
                        arena.characterAnimations = mode == "disabled" ? null : new CharacterAnimationCatalog { sets = new[] { set } };
                        var target = arena.enemies[0]; float health = target.health; int firstHit = -1; bool cancelled = false;
                        for (int tick = 0; tick < 60; tick++)
                        {
                            driver.Step(.02f, new CombatInput(0, tick == 0 ? CombatButtons.Basic : tick == 30 ? CombatButtons.Dash : CombatButtons.None));
                            arena.Presentation.Draw(0);
                            var cue = arena.Presentation.LastEffectHandle;
                            if (mode == "cancel" && cue != null && cue.Status == CueStatus.Playing && !cancelled) { cue.Cancel(); cancelled = true; }
                            if (firstHit < 0 && target.ReceivedHitCount > 0) firstHit = tick;
                        }
                        var values = new[] { health - target.health, (float)firstHit, player.Position.x, player.Position.y, player.DashRemaining, arena.Now };
                        if (baseline == null) { baseline = values; Assert.That(values[0], Is.EqualTo(10)); Assert.That(firstHit, Is.GreaterThan(0)); }
                        else Assert.That(values, Is.EqualTo(baseline), "Cue mode=" + mode);
                        Assert.That(arena.Presentation.ActiveEffects, Is.Zero);
                        if (mode == "cancel") { Assert.That(cancelled, Is.True); Assert.That(arena.Presentation.LastEffectHandle.Status, Is.EqualTo(CueStatus.Cancelled)); }
                        if (mode == "missing") Assert.That(arena.Presentation.LastEffectHandle.Status, Is.EqualTo(CueStatus.Failed));
                        if (mode == "normal") Assert.That(arena.Presentation.LastEffectHandle.Status, Is.EqualTo(CueStatus.Completed));
                    }
                    Setup(arena, driver); arena.characterAnimations = Catalog(sprites);
                    for (int i = 0; i < 8; i++) { driver.Step(.02f, new CombatInput(0, i == 0 ? CombatButtons.Basic : CombatButtons.None)); arena.Presentation.Draw(0); }
                    var independent = arena.Presentation.LastEffectHandle; Assert.That(independent.Status, Is.EqualTo(CueStatus.Playing));
                    player.CancelAction(); driver.Step(.02f); arena.Presentation.Draw(0);
                    Assert.That(independent.Status, Is.EqualTo(CueStatus.Playing), "Cancelling the attack keeps its already-emitted visual until its own end.");
                    for (int i = 0; i < 15; i++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(independent.Status, Is.EqualTo(CueStatus.Completed)); Assert.That(arena.enemies[0].health, Is.EqualTo(100), "The cancelled direct hit remains cancelled.");
                    foreach (int facing in new[] { 1, -1 })
                    {
                        Setup(arena, driver); var catalog = Catalog(sprites); arena.characterAnimations = catalog; player.SyncFacing(facing);
                        for (int i = 0; i < 8; i++) { driver.Step(.02f, new CombatInput(0, i == 0 ? CombatButtons.Basic : CombatButtons.None)); arena.Presentation.Draw(0); }
                        var cast = player.currentCast; var handle = arena.Presentation.LastEffectHandle; Assert.That(handle, Is.Not.Null);
                        Assert.That(handle.Occurrence.ExecutionId, Is.EqualTo(cast.ExecutionId)); Assert.That(handle.Status, Is.EqualTo(CueStatus.Playing));
                        var renderer = GameObject.Find("BasicAttackEffect_base_sword_attack1").GetComponent<SpriteRenderer>();
                        var origin = renderer.transform.position - new Vector3(.3f * facing, 1, 0); string version = handle.DefinitionVersion;
                        arena.Presentation.Draw(0); Assert.That(arena.Presentation.ActiveEffects, Is.EqualTo(1)); Assert.That(arena.Presentation.LastEffectHandle, Is.SameAs(handle));
                        Assert.That(renderer.GetComponent<Collider2D>(), Is.Null); Assert.That(renderer.GetComponent<Rigidbody2D>(), Is.Null);
                        var clip = catalog.sets[0].clips[1]; var effect = catalog.sets[0].effects[0];
                        clip.seconds[0] = 2; clip.sourceImpact = 1; clip.fxStart = 0;
                        effect.seconds[0] = 2; effect.x[0] = 99; effect.flipX = true; effect.sprites[0] = sprites[1];
                        for (int i = 0; i < 3; i++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                        Assert.That(renderer.sprite, Is.SameAs(sprites[1])); Assert.That(renderer.flipX, Is.False);
                        Assert.That(Vector3.Distance(renderer.transform.position, origin + new Vector3(.8f * facing, 1.2f, 0)), Is.LessThan(.001f));
                        Assert.That(handle.DefinitionVersion, Is.EqualTo(version));
                        arena.ResetTest(); Assert.That(handle.Status, Is.EqualTo(CueStatus.Cancelled)); Assert.That(arena.Presentation.ActiveEffects, Is.Zero);
                        Assert.That(GameObject.Find("BasicAttackEffect_base_sword_attack1"), Is.Null);
                    }
                    Setup(arena, driver); arena.characterAnimations = Catalog(sprites);
                    for (int i = 0; i < 8; i++) { driver.Step(.02f, new CombatInput(0, i == 0 ? CombatButtons.Basic : CombatButtons.None)); arena.Presentation.Draw(0); }
                    var onExit = arena.Presentation.LastEffectHandle; Assert.That(onExit.Status, Is.EqualTo(CueStatus.Playing));
                    yield return new ExitPlayMode();
                    Assert.That(onExit.Status, Is.EqualTo(CueStatus.Cancelled), "The common arena's OnDestroy must release active Cue views.");
                }
            }
            finally
            {
                if (arena) { arena.Presentation.Reset(); arena.characterAnimations = original; }
                foreach (var sprite in sprites) if (sprite) { if (Application.isPlaying) Object.Destroy(sprite); else Object.DestroyImmediate(sprite); }
                if (texture) { if (Application.isPlaying) Object.Destroy(texture); else Object.DestroyImmediate(texture); }
            }
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            arena.Player.selected = 0; arena.Player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
            arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); arena.enemies[0].Position = new Vector2(3, 0); Physics2D.SyncTransforms();
        }
        static CharacterAnimationCatalog Catalog(Sprite[] sprites)
        {
            var idle = new CharacterClip { name = "idle", frames = new[] { "idle" }, seconds = new[] { 1f }, sprites = new[] { sprites[0] } };
            var attack = new CharacterClip { name = "attack1", frames = new[] { "pose-a", "pose-b" }, seconds = new[] { .1f, .3f }, sourceImpact = .2f, fxStart = .1f, sprites = (Sprite[])sprites.Clone() };
            var effect = new CharacterEffect { attack = "attack1", frames = new[] { "effect-a", "effect-b" }, seconds = new[] { .08f, .12f }, x = new[] { .3f, .8f }, y = new[] { 1f, 1.2f }, sprites = (Sprite[])sprites.Clone() };
            return new CharacterAnimationCatalog { sets = new[] { new CharacterAnimationSet { form = "base_sword", clips = new[] { idle, attack }, effects = new[] { effect } } } };
        }
    }
}
