using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class ActionGeometryPreviewTests
    {
        readonly List<Object> owned = new List<Object>();
        string repository;
        [SetUp] public void SetUp() { repository = BasicMeleeSourceTests.CopyRepository(); }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(window => window.name == "GeometryPreviewTestWindow")) window.Close();
            foreach (var item in owned) if (item) Object.DestroyImmediate(item);
            owned.Clear();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        static Catalog Forms() => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        static CharacterAnimationCatalog Media() => JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
        static CharacterClip Clip(CharacterAnimationCatalog catalog) => catalog.sets.Single(set => set.form == "base_sword").Find("attack1");
        BasicMeleeDefinition First() => FrameMotionSource.ReadAction(repository);
        FixedAttackMotion Motion() => FrameMotionSource.Load(repository, out _).FixedBinding;
        Sprite MakeSprite(Rect rect, Vector2 normalizedPivot, float ppu = 32)
        {
            var texture = new Texture2D(128, 128); owned.Add(texture);
            var sprite = Sprite.Create(texture, rect, normalizedPivot, ppu); owned.Add(sprite); return sprite;
        }
        static BasicMeleeDefinition Change(BasicMeleeDefinition action, Action<BasicData> change)
        {
            var data = action.ToData(); change(data);
            Assert.That(BasicMeleeDefinition.TryCreate(action.Id, data, out var result, out var error), Is.True, error); return result;
        }
        [Test] public void FacingMirrorsFixedBodyAndNonuniformHitboxWithoutDerivingThemFromArt()
        {
            var action = Change(First(), data => { data.box.width = 5.5f; data.box.height = .75f; data.box.x = 2.25f; data.box.y = 1.75f; });
            var model = ActionGeometryPreviewModel.Compile(action, Forms(), Media(), Motion(), resolveSprite: _ => null);
            var right = model.Sample(action.Impact, 1, 0); var left = model.Sample(action.Impact, -1, 0);
            Assert.That(right.Body.size, Is.EqualTo(new Vector2(.9375f, 2))); Assert.That(left.Body.size, Is.EqualTo(right.Body.size));
            Assert.That(right.Body.center, Is.EqualTo(new Vector2(.1875f, 1))); Assert.That(left.Body.center, Is.EqualTo(new Vector2(-.1875f, 1)));
            Assert.That(right.Hurtbox, Is.EqualTo(right.Body)); Assert.That(left.Hurtbox, Is.EqualTo(left.Body));
            Assert.That(right.Hitbox.size, Is.EqualTo(new Vector2(5.5f, .75f))); Assert.That(left.Hitbox.size, Is.EqualTo(right.Hitbox.size));
            Assert.That(right.Hitbox.center, Is.EqualTo(new Vector2(2.25f, 1.75f))); Assert.That(left.Hitbox.center, Is.EqualTo(new Vector2(-2.25f, 1.75f)));
            Assert.That(right.Hitbox.xMin, Is.EqualTo(-left.Hitbox.xMax)); Assert.That(right.Sprite, Is.Null);
        }
        [Test] public void HitWindowIncludesImpactExcludesActiveEndAndScrubClampsAtCompiledEnd()
        {
            var action = First(); var model = ActionGeometryPreviewModel.Compile(action, Forms(), Media(), Motion(), resolveSprite: _ => null);
            Assert.That(model.Sample(action.Impact - .00001f, 1, 0).HitActive, Is.False);
            Assert.That(model.Sample(action.Impact, 1, 0).HitActive, Is.True);
            Assert.That(model.Sample(action.Impact + action.Active - .00001f, 1, 0).HitActive, Is.True);
            Assert.That(model.Sample(action.Impact + action.Active, 1, 0).HitActive, Is.False);
            Assert.That(model.Sample(action.Duration, 1, 0).HitActive, Is.False);
            Assert.That(model.Sample(99, 1, 0).Seconds, Is.EqualTo(model.Action.End)); Assert.That(model.Sample(-99, 1, 0).Seconds, Is.Zero);
            var zeroImpact = Change(action, data => data.impact = 0);
            var zero = ActionGeometryPreviewModel.Compile(zeroImpact, Forms(), Media(), resolveSprite: _ => null);
            Assert.That(zero.Sample(0, 1, 0).HitActive, Is.True);
        }
        [Test] public void SpriteUsesItsActualSubrectPivotAndPpuAndFacingRatherThanBodyOrWholeTexture()
        {
            var sprite = MakeSprite(new Rect(11, 7, 80, 40), new Vector2(.25f, .125f));
            var media = Media(); var clip = Clip(media); clip.sprites = Enumerable.Repeat(sprite, clip.frames.Length).ToArray();
            var model = ActionGeometryPreviewModel.Compile(First(), Forms(), media, Motion(), resolveSprite: _ => throw new AssertionException("Captured binding must be used."));
            var right = model.Sample(0, 1, 0); var left = model.Sample(0, -1, 0);
            Assert.That(right.SpriteBounds, Is.EqualTo(new Rect(-.625f, -.15625f, 2.5f, 1.25f)));
            Assert.That(left.SpriteBounds, Is.EqualTo(new Rect(-1.875f, -.15625f, 2.5f, 1.25f)));
            Assert.That(right.Body.height, Is.EqualTo(2)); Assert.That(right.SpriteBounds.height, Is.Not.EqualTo(right.Body.height));
            Assert.That(right.SpriteBounds.xMin, Is.EqualTo(-left.SpriteBounds.xMax));
            clip.sprites[0] = null;
            Assert.That(model.Sample(0, 1, 0).Sprite, Is.SameAs(sprite), "The preview owns the captured references, not a mutable catalog array.");
            var non32 = MakeSprite(new Rect(0, 0, 64, 32), Vector2.zero, 16); clip.sprites = Enumerable.Repeat(non32, clip.frames.Length).ToArray();
            var actualPpu = ActionGeometryPreviewModel.Compile(First(), Forms(), media, Motion());
            Assert.That(actualPpu.Sample(0, 1, 0).SpriteBounds.size, Is.EqualTo(new Vector2(4, 2)));
            Assert.That(actualPpu.MediaDiagnostic, Does.Contain("PPU 32가 아닌"));
        }
        [Test] public void EveryExistingBasicUsesSharedCompiledAdvanceAndSelectedHitGeometry()
        {
            var definitions = new[] { First() }.Concat(BasicMeleeSource.Compile(BasicMeleeSource.Load(repository, out _))).ToArray();
            Assert.That(definitions.Length, Is.EqualTo(17));
            var forms = Forms(); var media = Media();
            foreach (var definition in definitions)
            {
                var model = ActionGeometryPreviewModel.Compile(definition, forms, media, definition.Id == First().Id ? Motion() : null, resolveSprite: _ => null);
                string formId = definition.Id.Substring(0, definition.Id.LastIndexOf("_basic_", StringComparison.Ordinal));
                var form = forms.forms.Single(candidate => candidate.id == formId);
                var compiled = MeleeActionDefinition.ForBasic(definition, 1, form.inputSpeed);
                Assert.That(model.Action.ContentHash, Is.EqualTo(compiled.ContentHash), definition.Id);
                Assert.That(model.AdvanceAt(compiled.AdvanceStart, 1), Is.Zero, definition.Id);
                Assert.That(model.AdvanceAt(compiled.AdvanceStart + compiled.AdvanceDuration / 2, 1), Is.EqualTo(compiled.AdvanceDistance / 2).Within(.000001f), definition.Id);
                Assert.That(model.AdvanceAt(compiled.End, 1), Is.EqualTo(compiled.AdvanceDistance).Within(.000001f), definition.Id);
                Assert.That(model.AdvanceAt(compiled.End, -1), Is.EqualTo(-compiled.AdvanceDistance).Within(.000001f), definition.Id);
                Assert.That(model.AdvanceAt(compiled.End, 0), Is.Zero, definition.Id);
                var pose = model.Sample(compiled.Impact, 1, 1);
                Assert.That(pose.HitActive, Is.True, definition.Id); Assert.That(pose.Hitbox.size, Is.EqualTo(new Vector2(definition.Width, definition.Height)), definition.Id);
                Assert.That(pose.Hitbox.center.x - pose.Root.x, Is.EqualTo(definition.OffsetX).Within(.000001f), definition.Id);
            }
        }
        [TestCase(.045f, .165f, 1f)] [TestCase(.2f, .4f, 1.1f)] [TestCase(.5f, .95f, 2f)]
        public void SharedBasicFactoryKeepsExistingFloatEvaluationAndAttackSpeed(float impact, float end, float speed)
        {
            var action = Change(First(), data => { data.impact = impact; data.duration = end; });
            var compiled = MeleeActionDefinition.ForBasic(action, speed, 2.5f);
            var existing = new MeleeActionDefinition(action, speed, .65f, Mathf.Max(0, impact - .04f) / speed, .08f / speed);
            Assert.That(compiled.ContentHash, Is.EqualTo(existing.ContentHash));
            Assert.That(compiled.AdvanceDistance, Is.EqualTo(.65f)); Assert.That(compiled.Impact, Is.EqualTo(impact / speed));
            Assert.That(MeleeActionDefinition.ForBasic(action, speed, 0).AdvanceDistance, Is.Zero);
        }
        [Test] public void AdvanceIsClippedAtActionCompletionAndNeutralAndStationaryRemainAtOrigin()
        {
            var action = Change(First(), data => { data.impact = .01f; data.active = .01f; data.duration = .03f; });
            var model = ActionGeometryPreviewModel.Compile(action, Forms(), Media(), resolveSprite: _ => null);
            Assert.That(model.AdvanceAt(99, 1), Is.EqualTo(.65f / .08f * .03f));
            Assert.That(model.AdvanceAt(99, -1), Is.EqualTo(-.65f / .08f * .03f));
            Assert.That(model.AdvanceAt(99, 0), Is.Zero); Assert.That(model.AdvanceAt(-1, 1), Is.Zero);
        }
        [Test] public void FrameEditsMoveContactActiveEndAndBodyOnTheSameCompiledCandidate()
        {
            var action = First(); var motion = Motion(); var projected = MeleeFrameCompiler.ProjectFirst(action, motion, 2.5f);
            var frames = projected.Frames.Select(frame => new TimelineFrame(frame.Id, frame.Seconds + (frame.Id == projected.Frames[0].Id ? .03f : 0))).ToArray();
            var candidate = MeleeFrameCompiler.CompileFirst(action, motion, new FrameTimeline(frames, projected.Markers), 2.5f);
            var sprite = MakeSprite(new Rect(0, 0, 32, 64), new Vector2(.5f, 0));
            var media = Media(); Clip(media).sprites = Enumerable.Repeat(sprite, motion.Frames.Count).ToArray();
            var model = ActionGeometryPreviewModel.Compile(candidate.Action, Forms(), media, frame: candidate);
            Assert.That(model.Action.Impact, Is.EqualTo(candidate.Timeline.Times[FrameTimingProjection.Contact]));
            Assert.That(model.Action.End, Is.EqualTo(candidate.Timeline.Times[FrameTimingProjection.ActionEnd]));
            Assert.That(model.Action.Impact, Is.EqualTo(action.Impact + .03f).Within(.000001f));
            Assert.That(model.Motion.UsesFrameTime, Is.True);
            Assert.That(model.Sample(action.Impact, 1, 0).HitActive, Is.False);
            Assert.That(model.Sample(model.Action.Impact, 1, 0).HitActive, Is.True);
            Assert.That(model.Sample(model.Action.Impact + model.Action.Active, 1, 0).HitActive, Is.False);
            foreach (float time in new[] { 0, .02f, .2f, .23f, .31f, .43f, .01f })
            {
                int expectedFrame = candidate.CompileMotion(1).FrameAt(time);
                Assert.That(model.Sample(time, -1, -1).FrameIndex, Is.EqualTo(expectedFrame));
                Assert.That(model.Sample(time, -1, -1).Sprite, Is.SameAs(sprite));
            }
            Assert.Throws<ArgumentException>(() => ActionGeometryPreviewModel.Compile(action, Forms(), media, frame: candidate));
        }
        [Test] public void FixedMotionEditsUseRuntimeFrameMappingWithoutMovingContactOrEnd()
        {
            var action = First(); var motion = Motion(); var document = motion.ToDocument(); document.steps[0].seconds += .1f;
            var changed = new FixedAttackMotion(document);
            var preview = ActionGeometryPreviewModel.Compile(action, Forms(), Media(), changed, resolveSprite: _ => null);
            Assert.That(preview.Action.Impact, Is.EqualTo(action.Impact)); Assert.That(preview.Action.End, Is.EqualTo(action.Duration));
            foreach (float time in new[] { 0, .04f, .1f, .19f, .2f, .28f, .4f, .02f })
                Assert.That(preview.Sample(time, 1, 0).FrameIndex, Is.EqualTo(changed.Compile(action.Impact, action.Duration).FrameAt(time)));
        }
        [Test] public void ExistingFallbackBindingIsExplicitAndMissingImagesDoNotInventSpritesOrChangeGeometry()
        {
            var definitions = BasicMeleeSource.Compile(BasicMeleeSource.Load(repository, out _));
            var hammer = definitions.First(action => action.Id.StartsWith("hammer_basic_", StringComparison.Ordinal));
            var media = Media(); media.sets = media.sets.Where(set => set.form != "hammer").ToArray();
            var sprite = MakeSprite(new Rect(0, 0, 64, 64), new Vector2(.5f, 0));
            Clip(media).sprites = Enumerable.Repeat(sprite, Clip(media).frames.Length).ToArray();
            var model = ActionGeometryPreviewModel.Compile(hammer, Forms(), media, resolveSprite: _ => null);
            Assert.That(model.RequestedBinding, Is.EqualTo("hammer/attack1")); Assert.That(model.BoundBinding, Is.EqualTo("base_sword/attack1"));
            Assert.That(model.MediaDiagnostic, Does.Contain("기존 런타임 본체 대체 바인딩").And.Contain("실제 아트 아님"));
            Assert.That(model.Sample(hammer.Impact, 1, 0).Sprite, Is.SameAs(sprite)); Assert.That(model.Sample(hammer.Impact, 1, 0).Hitbox.width, Is.EqualTo(hammer.Width));
            Clip(media).sprites = null;
            var missing = ActionGeometryPreviewModel.Compile(hammer, Forms(), media, resolveSprite: _ => null);
            Assert.That(missing.LoadedSprites, Is.Zero); Assert.That(missing.MediaDiagnostic, Does.Contain("미디어 누락"));
            Assert.That(missing.Sample(hammer.Impact, 1, 0).Sprite, Is.Null); Assert.That(missing.Sample(hammer.Impact, 1, 0).HitActive, Is.True);
            Assert.That(missing.Sample(hammer.Impact, 1, 0).Hitbox, Is.EqualTo(model.Sample(hammer.Impact, 1, 0).Hitbox));
        }
        [Test] public void InvalidCandidateClearsOldGeometryInsteadOfPresentingLastValidAsCurrent()
        {
            var preview = new ActionGeometryPreview(); preview.SetMedia(Forms(), Media(), _ => null);
            preview.SetCandidate(First(), Motion()); Assert.That(preview.Model, Is.Not.Null);
            preview.SetInvalid("active window outside action");
            Assert.That(preview.Model, Is.Null); Assert.That(preview.Q<Slider>("geometry-time").enabledSelf, Is.False);
            Assert.That(preview.Q<Label>("geometry-state").text, Does.Contain("active window outside action").And.Contain("마지막 유효"));
            Assert.That(preview.Q<Label>("geometry-sample").text, Is.Empty);
            preview.SetCandidate(First(), Motion()); Assert.That(preview.Model, Is.Not.Null); Assert.That(preview.Q<Slider>("geometry-time").enabledSelf, Is.True);
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator NumericWindowsRefreshSelectedGeometryAndFrameAndMotionWindowsShareTheSampler()
        {
            var melee = Open<BasicMeleeWindow>(); melee.LoadRepository(repository);
            var sword = Open<BasicSwordWindow>(); sword.LoadRepository(repository);
            var frame = Open<FrameTimingWindow>(); frame.LoadRepository(repository);
            var motion = Open<AttackMotionWindow>(); motion.LoadRepository(repository); yield return null;
            var forms = Forms(); var media = Media();
            var sprite = MakeSprite(new Rect(0, 0, 80, 40), new Vector2(.25f, .125f));
            foreach (var window in new EditorWindow[] { melee, sword, frame, motion })
            {
                var previews = window.rootVisualElement.Query<ActionGeometryPreview>().ToList(); Assert.That(previews.Count, Is.EqualTo(1));
                previews[0].SetMedia(forms, media, _ => sprite, "테스트가 제공한 Sprite — 실제 게임 아트 아님");
            }
            var selector = melee.rootVisualElement.Q<DropdownField>("hit");
            foreach (string choice in selector.choices.ToArray())
            {
                selector.value = choice;
                var preview = melee.rootVisualElement.Q<ActionGeometryPreview>(); Assert.That(preview.Model, Is.Not.Null, choice);
                preview.Q<Slider>("geometry-time").value = preview.Model.Action.Impact;
                Assert.That(preview.CurrentSample.HitActive, Is.True); Assert.That(preview.CurrentSample.Sprite, Is.SameAs(sprite));
            }
            selector.value = selector.choices.First(choice => choice.Contains("base_sword_basic_1"));
            var meleePreview = melee.rootVisualElement.Q<ActionGeometryPreview>();
            melee.rootVisualElement.Q<FloatField>("width").value = 7.25f;
            Assert.That(meleePreview.Model.Sample(meleePreview.Model.Action.Impact, 1, 0).Hitbox.width, Is.EqualTo(7.25f));
            melee.rootVisualElement.Q<FloatField>("duration").value = 0; Assert.That(meleePreview.Model, Is.Null);
            melee.rootVisualElement.Q<FloatField>("duration").value = .6f; Assert.That(meleePreview.Model, Is.Not.Null);
            meleePreview.Q<Slider>("geometry-time").value = meleePreview.Model.Action.End;
            meleePreview.Q<Toggle>("geometry-facing-left").value = true;
            Assert.That(meleePreview.CurrentSample.Facing, Is.EqualTo(-1)); Assert.That(meleePreview.CurrentSample.Root.x, Is.EqualTo(-.65f).Within(.000001f));
            meleePreview.Q<DropdownField>("geometry-input").value = "입력 없음";
            Assert.That(meleePreview.CurrentSample.Root, Is.EqualTo(Vector2.zero));
            Assert.That(meleePreview.CurrentSample.Body.center.x, Is.EqualTo(-.1875f));
            meleePreview.Q<DropdownField>("geometry-input").value = "오른쪽 (+1)";
            Assert.That(meleePreview.CurrentSample.Facing, Is.EqualTo(1)); Assert.That(meleePreview.CurrentSample.Root.x, Is.EqualTo(.65f).Within(.000001f));
            sword.rootVisualElement.Q<FloatField>("height").value = 3.5f;
            Assert.That(sword.rootVisualElement.Q<ActionGeometryPreview>().CurrentSample.Hitbox.height, Is.EqualTo(3.5f));
            var framePreview = frame.rootVisualElement.Q<ActionGeometryPreview>(); float contact = framePreview.Model.Action.Impact;
            frame.rootVisualElement.Q<FloatField>("frame-sword-first-ready").value += .03f;
            Assert.That(framePreview.Model.Action.Impact, Is.EqualTo(contact + .03f).Within(.000001f));
            var motionPreview = motion.rootVisualElement.Q<ActionGeometryPreview>(); string timing = motionPreview.Model.Motion.ContentHash;
            motion.rootVisualElement.Q<FloatField>("seconds-0").value += .03f;
            Assert.That(motionPreview.Model.Motion.ContentHash, Is.Not.EqualTo(timing)); Assert.That(motionPreview.Model.Action.Impact, Is.EqualTo(First().Impact));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator BackwardScrubbingAndFacingCannotStartTickResetDamageSpendOrEmitCues()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var window = Open<FrameTimingWindow>(); window.LoadRepository(repository); yield return null;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                var cast = arena.Player.currentCast; Assert.That(cast, Is.Not.Null);
                var preview = window.rootVisualElement.Q<ActionGeometryPreview>();
                var sprite = MakeSprite(new Rect(0, 0, 80, 40), new Vector2(.25f, .125f));
                preview.SetMedia(arena.catalog, Media(), _ => sprite, "테스트가 제공한 Sprite — 실제 게임 아트 아님");
                float now = arena.Now, age = cast.Age, health = arena.Player.health; var position = arena.Player.Position; var velocity = arena.Player.velocity;
                float[] enemyHealth = arena.enemies.Select(enemy => enemy.health).ToArray(); int[] received = arena.enemies.Select(enemy => enemy.ReceivedHitCount).ToArray();
                int casts = arena.casts.Count, projectiles = arena.projectiles.Count, cues = arena.Presentation.ActiveEffects;
                var charges = arena.Player.weapons.Select(weapon => new Vector4(weapon.Cooldown.Capacity, weapon.Cooldown.Available, weapon.Cooldown.Period, weapon.Cooldown.NextReady)).ToArray();
                string definition = arena.BasicSwordFirst.ContentHash, motionVersion = arena.Presentation.CurrentAttackMotionVersion;
                var frameBinding = arena.FrameSwordTiming; string source = File.ReadAllText(Path.Combine(repository, AttackMotionSource.RelativePath));
                var scrub = preview.Q<Slider>("geometry-time");
                foreach (float t in new[] { preview.Model.Action.End, preview.Model.Action.Impact, 0, preview.Model.Action.Impact + .01f, .01f, preview.Model.Action.End, 0 })
                {
                    scrub.value = t; preview.Q<Toggle>("geometry-facing-left").value = !preview.Q<Toggle>("geometry-facing-left").value;
                    preview.Q<Slider>("geometry-zoom").value = 2; yield return null;
                    Assert.That(preview.CurrentSample.Seconds, Is.EqualTo(t)); Assert.That(preview.CurrentSample.Sprite, Is.SameAs(sprite));
                    Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(cast.Age, Is.EqualTo(age)); Assert.That(arena.Player.currentCast, Is.SameAs(cast));
                    Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Player.velocity, Is.EqualTo(velocity)); Assert.That(arena.Player.health, Is.EqualTo(health));
                    Assert.That(arena.enemies.Select(enemy => enemy.health), Is.EqualTo(enemyHealth)); Assert.That(arena.enemies.Select(enemy => enemy.ReceivedHitCount), Is.EqualTo(received));
                    Assert.That(arena.casts.Count, Is.EqualTo(casts)); Assert.That(arena.projectiles.Count, Is.EqualTo(projectiles)); Assert.That(arena.Presentation.ActiveEffects, Is.EqualTo(cues));
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(motionVersion)); Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(definition)); Assert.That(arena.FrameSwordTiming, Is.SameAs(frameBinding));
                    Assert.That(arena.Player.weapons.Select(weapon => new Vector4(weapon.Cooldown.Capacity, weapon.Cooldown.Available, weapon.Cooldown.Period, weapon.Cooldown.NextReady)), Is.EqualTo(charges)); Assert.That(File.ReadAllText(Path.Combine(repository, AttackMotionSource.RelativePath)), Is.EqualTo(source));
                }
                var end = preview.Model.Sample(preview.Model.Action.End, -1, -1); var beginning = preview.Model.Sample(0, -1, -1);
                Assert.That(end.Root.x, Is.LessThan(beginning.Root.x)); Assert.That(beginning.Root, Is.EqualTo(Vector2.zero));
            }
        }
        T Open<T>() where T : EditorWindow
        {
            var window = ScriptableObject.CreateInstance<T>(); window.name = "GeometryPreviewTestWindow"; window.Show();
            if (window is BasicMeleeWindow melee) melee.CreateGUI();
            else if (window is BasicSwordWindow sword) sword.CreateGUI();
            else if (window is FrameTimingWindow frame) frame.CreateGUI();
            else if (window is AttackMotionWindow motion) motion.CreateGUI();
            return window;
        }
    }
}
