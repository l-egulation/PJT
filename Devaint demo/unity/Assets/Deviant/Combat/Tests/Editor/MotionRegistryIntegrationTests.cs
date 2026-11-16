using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class MotionRegistryIntegrationTests
    {
        [Test] public void RestoringOmittedLauncherFrameRestoresItsNativeMuzzleAnchor()
        {
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var original = new MotionSourceRegistry(media.motionRegistry, combat);
            var native = media.sets.Single(set => set.form == "launcher").Find("attack_single");
            var expectedX = (float[])native.muzzleX.Clone(); var expectedY = (float[])native.muzzleY.Clone();
            string removedFrame = native.frames[0];
            media.motionSprites = original.Palettes.SelectMany(palette => palette.Frames).Distinct()
                .ToDictionary(frame => frame, _ => (Sprite)null);
            var edited = original.ToDocument(); var motion = edited.motions.Single(value => value.id == "launcher/attack_single");
            motion.steps = motion.steps.Where(step => step.frame != removedFrame).ToArray();
            Assert.That(motion.steps.Length, Is.GreaterThan(0));
            media.sets = MotionRegistryMedia.Project(media, new MotionSourceRegistry(edited, combat));
            CollectionAssert.DoesNotContain(media.sets.Single(set => set.form == "launcher").Find("attack_single").frames, removedFrame);
            media.sets = MotionRegistryMedia.Project(media, original);
            var restored = media.sets.Single(set => set.form == "launcher").Find("attack_single");
            CollectionAssert.AreEqual(native.frames, restored.frames);
            CollectionAssert.AreEqual(expectedX, restored.muzzleX);
            CollectionAssert.AreEqual(expectedY, restored.muzzleY);
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator ChapterOneMovementSkillsDisplayImportedHeldPosesWhileTravelling()
        {
            EditorSceneManager.OpenScene("Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity");
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(arena, Is.Not.Null);
            using (var driver = arena.AcquireManualSimulation())
            foreach (int direction in new[] { 1, -1 })
            foreach (var spec in new[] {
                (form: "base_sword", clip: "attack_dash_thrust", sample: .25f, held: 4, impact: .15f, end: .5333333f),
                (form: "hammer", clip: "attack_rush_push", sample: .4f, held: 7, impact: .2f, end: .85f) })
            {
                Setup(arena, driver, spec.form, 2, true);
                foreach (var enemy in arena.enemies) enemy.Position = new Vector2(40, 1000);
                var player = arena.Player; player.SyncFacing(direction); Physics2D.SyncTransforms();
                typeof(CombatPlayer).GetMethod("BeginSpecial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(player, new object[] { default(CombatCommandKey), -1f });
                var cast = player.currentCast; Assert.That(cast, Is.Not.Null);
                Assert.That(cast.ImpactTime, Is.EqualTo(spec.impact).Within(.0001f));
                Assert.That(cast.MotionDuration, Is.EqualTo(spec.end).Within(.0001f));
                for (int tick = 0; tick < Mathf.RoundToInt(spec.sample * 100); tick++) driver.Step(.01f);
                arena.Presentation.Draw(0);
                Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
                Assert.That(arena.Presentation.CurrentBodyClipName, Is.EqualTo(spec.clip));
                var clip = arena.characterAnimations.sets.Single(set => set.form == spec.form).Find(spec.clip);
                Assert.That(player.visual.sprite, Is.SameAs(clip.sprites[spec.held]));
                Assert.That(player.visual.flipX, Is.EqualTo(direction < 0));
                Assert.That(player.Position.x * direction, Is.GreaterThan(1));
                // Exercise the existing collision deadline, without changing damage/movement definitions.
                Setup(arena, driver, spec.form, 2, true);
                foreach (var enemy in arena.enemies) enemy.Position = new Vector2(40, 1000);
                player.SyncFacing(direction);
                typeof(CombatPlayer).GetMethod("BeginSpecial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(player, new object[] { default(CombatCommandKey), -1f });
                cast = player.currentCast;
                for (int tick = 0; tick < Mathf.RoundToInt((spec.impact + .02f) * 100); tick++) driver.Step(.01f);
                player.wallHit = true; cast.AfterMovement(); player.wallHit = false;
                float actualEnd = cast.EndTime - cast.StartedAt;
                Assert.That(actualEnd, Is.LessThan(spec.end));
                Assert.That(cast.MotionDuration, Is.EqualTo(spec.end).Within(.0001f));
                for (int tick = 0; tick < 9; tick++) driver.Step(.01f);
                arena.Presentation.Draw(0);
                float recovery = spec.form == "base_sword" ? .25f : .35f;
                float expectedAge = spec.end - recovery + cast.Age - (actualEnd - recovery);
                Assert.That(player.visual.sprite, Is.SameAs(clip.At(expectedAge, false)), "Collision must enter the captured recovery instead of retaining the moving pose.");
            }
        }
        [UnityTest] public IEnumerator GameUsesSharedEditsOnlyForNewExecutions() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesSharedEditsOnlyForNewExecutions() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form, int special, bool maintenance = false)
        {
            driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            if (maintenance)
            {
                player.MaintenanceSite = () => true;
                player.SetWeaponSlotReady(0, true);
                Assert.That(player.TrySetChargeStamp(0, false, out var stampError), Is.True, stampError);
            }
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, value => value.id == form), special, out var error), Is.True, error);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = false; player.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var initial = arena.MotionRegistry.ToDocument();
            Assert.That(arena.TryGetBasicSwordMotion(out var initialFirst, out var initialError), Is.True, initialError);
            var texture = new Texture2D(2, 1); var sprites = new[] { Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32), Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32) };
            foreach (var set in arena.characterAnimations.sets)
                foreach (var clip in set.clips) clip.sprites = clip.frames.Select((_, index) => sprites[index % 2]).ToArray();
            foreach (var palette in arena.MotionRegistry.Palettes)
                for (int index = 0; index < palette.Frames.Count; index++) arena.characterAnimations.motionSprites[palette.Frames[index]] = sprites[index % 2];
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    foreach (var action in new[] { (form: "rapier", special: 0, basic: true), (form: "rapier", special: 0, basic: false),
                        (form: "base_sword", special: 2, basic: false), (form: "whip", special: 2, basic: false), (form: "launcher", special: 2, basic: true) })
                    {
                        Setup(arena, driver, action.form, action.special);
                        Assert.That(arena.TryApplyMotionRegistry(initial, false, out var error), Is.True, error);
                        Assert.That(arena.TryApplyBasicSwordMotion(initialFirst.ToDocument(), false, out error), Is.True, error);
                        var buttons = action.basic ? CombatButtons.Basic : CombatButtons.Special;
                        driver.Step(.02f, new CombatInput(0, buttons, true)); var cast = player.currentCast; Assert.That(cast, Is.Not.Null, action.ToString());
                        string version = arena.Presentation.CurrentAttackMotionVersion, bindingVersion = arena.Presentation.CurrentMotionBindingVersion;
                        var expected = new MotionActionKey(cast.Definition.Id, action.form == "launcher" ? MotionActionKind.Conversion : action.basic ? MotionActionKind.Basic : MotionActionKind.Special,
                            action.form == "launcher" ? action.special : -1);
                        var bound = arena.MotionRegistry.Find(expected); Assert.That(bound, Is.Not.Null); string definition = cast.DefinitionVersion; float impact = cast.ImpactTime, end = cast.MotionDuration;
                        var edited = arena.MotionRegistry.ToDocument(); var motion = edited.motions.Single(value => value.id == bound.MotionId);
                        if (motion.delegatedSource != null)
                        {
                            var firstEdit = initialFirst.ToDocument(); firstEdit.steps[0].seconds += .5f;
                            Assert.That(arena.TryApplyBasicSwordMotion(firstEdit, false, out error), Is.True, error);
                        }
                        else
                        {
                            motion.steps[0].seconds += .5f;
                            Assert.That(arena.TryApplyMotionRegistry(edited, false, out error), Is.True, error);
                        }
                        Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version)); Assert.That(arena.Presentation.CurrentMotionBindingVersion, Is.EqualTo(bindingVersion));
                        Assert.That(arena.MotionBindingHash, Is.Not.EqualTo(bindingVersion));
                        Assert.That(cast.DefinitionVersion, Is.EqualTo(definition)); Assert.That(cast.ImpactTime, Is.EqualTo(impact)); Assert.That(cast.MotionDuration, Is.EqualTo(end));
                        if (motion.delegatedSource == null)
                        {
                            edited.motions.Single(value => value.id == bound.MotionId).steps[0].seconds = 99;
                            Assert.That(arena.MotionRegistry.Motion(bound.MotionId).Seconds[0], Is.LessThan(2), "Apply does not retain mutable editor input.");
                        }
                        arena.Presentation.Draw(0); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
                        string current = arena.Presentation.CurrentAttackMotionVersion; arena.characterAnimations.sets = null;
                        arena.Presentation.Draw(0); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(current), "A started body never resolves mutable media again.");
                        // Restore supplied media explicitly; the old execution remains pinned.
                        var restored = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
                        restored.motionSprites = arena.characterAnimations.motionSprites;
                        foreach (var set in restored.sets) foreach (var clip in set.clips) clip.sprites = clip.frames.Select((_, index) => sprites[index % 2]).ToArray();
                        arena.characterAnimations = restored;
                        if (motion.delegatedSource != null)
                        {
                            var firstEdit = initialFirst.ToDocument(); firstEdit.steps[0].seconds += .5f;
                            Assert.That(arena.TryApplyBasicSwordMotion(firstEdit, false, out error), Is.True, error);
                        }
                        Setup(arena, driver, action.form, action.special); driver.Step(.02f, new CombatInput(0, buttons, true));
                        Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.EqualTo(version)); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
                        var invalid = arena.MotionRegistry.ToDocument(); invalid.actions = invalid.actions.Skip(1).ToArray(); float now = arena.Now; var running = player.currentCast;
                        Assert.That(arena.TryApplyMotionRegistry(invalid, true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.currentCast, Is.SameAs(running));
                        string retainedRegistry = arena.MotionRegistry.ContentHash, retainedBinding = arena.MotionBindingHash;
                        string retainedBody = arena.Presentation.CurrentAttackMotionVersion, retainedStartedBinding = arena.Presentation.CurrentMotionBindingVersion;
                        invalid = arena.MotionRegistry.ToDocument(); invalid.palettes.Single(value => value.id == FixedAttackMotion.FirstSwordId).frames = new[] { "invented-delegated-frame" };
                        Assert.That(arena.TryApplyMotionRegistry(invalid, true, out error), Is.False, "Delegated media palette is checked against the actual immutable supplied binding.");
                        Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.currentCast, Is.SameAs(running));
                        Assert.That(arena.MotionRegistry.ContentHash, Is.EqualTo(retainedRegistry)); Assert.That(arena.MotionBindingHash, Is.EqualTo(retainedBinding));
                        Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(retainedBody)); Assert.That(arena.Presentation.CurrentMotionBindingVersion, Is.EqualTo(retainedStartedBinding));
                    }
                    Setup(arena, driver, "rapier", 0); driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                    var paletteCast = player.currentCast; arena.Presentation.Draw(0);
                    string paletteGraph = arena.MotionRegistry.ContentHash, paletteBinding = arena.MotionBindingHash;
                    string paletteBody = arena.Presentation.CurrentAttackMotionVersion, paletteStartedBinding = arena.Presentation.CurrentMotionBindingVersion;
                    float paletteNow = arena.Now; var shown = player.visual.sprite;
                    var suppliedClip = arena.characterAnimations.sets.Single(value => value.form == "rapier").Find("attack1");
                    var suppliedFrames = suppliedClip.frames; var suppliedSeconds = suppliedClip.seconds; var suppliedIds = suppliedClip.stepIds; var suppliedSprites = suppliedClip.sprites;
                    string missing = suppliedFrames[0]; var paletteSprite = arena.characterAnimations.motionSprites[missing];
                    try
                    {
                        suppliedClip.frames = suppliedFrames.Skip(1).ToArray(); suppliedClip.seconds = suppliedSeconds.Skip(1).ToArray();
                        suppliedClip.stepIds = suppliedIds.Skip(1).ToArray(); suppliedClip.sprites = suppliedSprites.Skip(1).ToArray();
                        arena.characterAnimations.motionSprites.Remove(missing);
                        Assert.That(arena.TryApplyMotionRegistry(arena.MotionRegistry.ToDocument(), true, out var missingError), Is.False);
                        Assert.That(missingError, Is.Not.Null.And.Not.Empty); arena.Presentation.Draw(0);
                        Assert.That(arena.Now, Is.EqualTo(paletteNow)); Assert.That(player.currentCast, Is.SameAs(paletteCast));
                        Assert.That(arena.MotionRegistry.ContentHash, Is.EqualTo(paletteGraph)); Assert.That(arena.MotionBindingHash, Is.EqualTo(paletteBinding));
                        Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(paletteBody)); Assert.That(arena.Presentation.CurrentMotionBindingVersion, Is.EqualTo(paletteStartedBinding));
                        Assert.That(player.visual.sprite, Is.SameAs(shown));
                    }
                    finally
                    {
                        suppliedClip.frames = suppliedFrames; suppliedClip.seconds = suppliedSeconds; suppliedClip.stepIds = suppliedIds; suppliedClip.sprites = suppliedSprites;
                        arena.characterAnimations.motionSprites[missing] = paletteSprite;
                    }
                    Assert.That(arena.TryApplyMotionRegistry(initial, true, out var restoredError), Is.True, restoredError);
                    Assert.That(arena.TryApplyBasicSwordMotion(initialFirst.ToDocument(), false, out restoredError), Is.True, restoredError);
                    Assert.That(player.currentCast, Is.Null); Assert.That(arena.Presentation.CurrentMotionBindingVersion, Is.Null);
                }
            }
            finally { foreach (var sprite in sprites) Object.Destroy(sprite); Object.Destroy(texture); }
        }
    }
}
