using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class FrameMeleeApplicationTests
    {
        [UnityTest] public IEnumerator GamePublishesFrameActionAndMotionTogether() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabPublishesFrameActionAndMotionTogether() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var w in Resources.FindObjectsOfTypeAll<FrameTimingWindow>().Where(w => w.name == "FrameApplyTestWindow")) w.Close();
        }
        static FrameTimeline Edited(FrameTimeline original, float extra)
        {
            var frames = original.Frames.ToArray(); frames[0] = new TimelineFrame(frames[0].Id, frames[0].Seconds + extra); return new FrameTimeline(frames, original.Markers);
        }
        static void Bind(CombatArena arena, Sprite[] sprites)
        {
            arena.characterAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var set = arena.characterAnimations.sets.Single(s => s.form == "base_sword"); set.Find("attack1").sprites = sprites.Take(5).ToArray(); set.Effect("attack1").sprites = sprites.Take(6).ToArray();
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            p.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0); p.Position = Vector2.zero; p.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(3, 0); Physics2D.SyncTransforms();
        }
        static void Tick(CombatArena arena, CombatArena.ManualSimulation driver, int count)
        { for (int i = 0; i < count; i++) { driver.Step(.02f); arena.Presentation.Draw(0); } }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player;
            var floor = new GameObject("FrameApplyFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>();
            var texture = new Texture2D(6, 1); var sprites = Enumerable.Range(0, 6).Select(i => Sprite.Create(texture, new Rect(i, 0, 1, 1), Vector2.zero, 32)).ToArray();
            try
            {
                Bind(arena, sprites);
                using (var driver = arena.AcquireManualSimulation())
                {
                    Setup(arena, driver); var sourceAction = arena.BasicSwordFirst; Assert.That(arena.TryGetBasicSwordMotion(out var sourceMotion, out var error), Is.True, error);
                    var sourceCatalog = JsonUtility.ToJson(arena.characterAnimations); var projection = MeleeFrameCompiler.ProjectFirst(sourceAction, sourceMotion, 2.5f);
                    var candidate = MeleeFrameCompiler.CompileFirst(sourceAction, sourceMotion, Edited(projection, .03f), 2.5f);
                    var numericVersion = SwordDashVersion.Capture(arena);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var old = p.currentCast; string oldMotion = arena.Presentation.CurrentAttackMotionVersion;
                    Assert.That(arena.TryApplySwordFrameTiming(candidate, false, out error), Is.True, error);
                    Assert.That(arena.GetMeleeFrame(candidate.Id, true), Is.SameAs(candidate));
                    Assert.That(old.FrameTimingVersion, Is.Null); Assert.That(old.ImpactTime, Is.EqualTo(.2f)); Assert.That(old.MotionDuration, Is.EqualTo(.4f)); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(oldMotion));
                    float now = arena.Now;
                    Assert.That(arena.TryApplyBasicSwordFirst(sourceAction.ToData(), true, out error), Is.False); Assert.That(arena.TryApplyBasicSwordMotion(sourceMotion.ToDocument(), true, out error), Is.False);
                    Assert.That(SwordDashVersion.Capture(arena).FrameTiming, Is.SameAs(candidate));
                    if (arena.CompositionProfile == CombatCompositionProfile.Lab) Assert.Throws<InvalidOperationException>(() => SwordDashComparison.Run(arena, numericVersion, numericVersion));
                    Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(p.currentCast, Is.SameAs(old));
                    Tick(arena, driver, 30); Assert.That(arena.enemies[0].health, Is.EqualTo(90)); Assert.That(JsonUtility.ToJson(arena.characterAnimations), Is.EqualTo(sourceCatalog));

                    Setup(arena, driver); driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var framed = p.currentCast;
                    Assert.That(framed.FrameTiming, Is.SameAs(candidate));
                    Assert.That(framed.FrameTimingVersion, Is.EqualTo(candidate.ContentHash)); Assert.That(framed.ImpactTime, Is.EqualTo(.23f).Within(.000001f)); Assert.That(framed.MotionDuration, Is.EqualTo(.43f).Within(.000001f));
                    Tick(arena, driver, 4); Assert.That(p.visual.sprite, Is.SameAs(sprites[0]), "Direct frame time uses the extended first frame.");
                    Tick(arena, driver, 6); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero); Assert.That(arena.Presentation.LastEffectHandle, Is.Not.Null, "Explicit shifted Cue marker must emit on the common action clock.");
                    Tick(arena, driver, 1); Assert.That(arena.enemies[0].health, Is.EqualTo(90)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                    Assert.That(arena.Presentation.LastEffectHandle.Occurrence.ExecutionId, Is.EqualTo(framed.ExecutionId));
                    var nextCandidate = MeleeFrameCompiler.CompileFirst(sourceAction, sourceMotion, Edited(projection, .06f), 2.5f);
                    Assert.That(arena.TryApplySwordFrameTiming(nextCandidate, false, out error), Is.True, error); Assert.That(framed.FrameTimingVersion, Is.EqualTo(candidate.ContentHash)); Assert.That(framed.ImpactTime, Is.EqualTo(.23f).Within(.000001f));
                    Assert.That(arena.TryRestoreSwordFixedTiming(false, out error), Is.True, error); Assert.That(arena.FrameSwordTiming, Is.Null); Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(sourceAction.ContentHash)); Assert.That(framed.FrameTimingVersion, Is.EqualTo(candidate.ContentHash));
                    Tick(arena, driver, 10); Assert.That(p.currentCast, Is.Null);
                    Tick(arena, driver, 20); driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                    Assert.That(p.currentCast.FrameTimingVersion, Is.Null); Assert.That(p.currentCast.ImpactTime, Is.EqualTo(.2f));

                    Setup(arena, driver); p.Weapon.StatSlots[0] = 3;
                    Assert.That(arena.TryApplySwordFrameTiming(candidate, true, out error), Is.True, error); Assert.That(p.currentCast, Is.Null);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); framed = p.currentCast;
                    Assert.That(framed.ImpactTime, Is.EqualTo(.23f / p.Weapon.Speed).Within(.000001f)); Assert.That(framed.MotionDuration, Is.EqualTo(.43f / p.Weapon.Speed).Within(.000001f));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Jump)); Tick(arena, driver, 30); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero);
                    Assert.That(arena.FrameSwordTiming, Is.SameAs(candidate), "Actor reset/cancellation does not silently remove the editor override.");
                    Assert.That(arena.TryRestoreSwordFixedTiming(true, out error), Is.True, error);
                    Setup(arena, driver); Assert.That(arena.TryApplySwordFrameTiming(candidate, false, out error), Is.True, error);
                    var savedArt = arena.characterAnimations; arena.characterAnimations = null;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); framed = p.currentCast; Tick(arena, driver, 24);
                    Assert.That(framed.ImpactTime, Is.EqualTo(.23f).Within(.000001f)); Assert.That(arena.enemies[0].health, Is.EqualTo(90)); Assert.That(arena.Presentation.LastAttackMotionError, Is.Not.Null);
                    arena.characterAnimations = savedArt; Assert.That(arena.TryRestoreSwordFixedTiming(true, out error), Is.True, error);
                    var changedAction = sourceAction.ToData(); changedAction.damage = 20; Assert.That(arena.TryApplyBasicSwordFirst(changedAction, false, out error), Is.True, error);
                    now = arena.Now; Assert.That(arena.TryApplySwordFrameTiming(candidate, true, out error), Is.False); Assert.That(arena.FrameSwordTiming, Is.Null); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.BasicSwordFirst.Damage, Is.EqualTo(20));
                    Assert.That(arena.TryApplyBasicSwordFirst(sourceAction.ToData(), false, out error), Is.True, error);
                    var changedMotion = sourceMotion.ToDocument(); changedMotion.steps[0].seconds += .01f; Assert.That(arena.TryApplyBasicSwordMotion(changedMotion, false, out error), Is.True, error);
                    Assert.That(arena.TryApplySwordFrameTiming(candidate, true, out error), Is.False); Assert.That(arena.FrameSwordTiming, Is.Null);
                    Assert.That(arena.TryApplyBasicSwordMotion(sourceMotion.ToDocument(), false, out error), Is.True, error);
                }
            }
            finally { arena.Presentation.Reset(); Object.Destroy(floor); foreach (var sprite in sprites) Object.Destroy(sprite); Object.Destroy(texture); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowAppliesAndRestoresExplicitlyWithoutWritingEitherSource()
        {
            var root = new DirectoryInfo(Application.dataPath); while (root != null && !File.Exists(Path.Combine(root.FullName, AttackMotionSource.RelativePath))) root = root.Parent;
            string repository = Path.Combine(Path.GetTempPath(), "deviant-frame-apply-" + Guid.NewGuid().ToString("N"));
            foreach (string relative in new[] { AttackMotionSource.RelativePath, "data/combat/actions/base_sword_basic_0.json", MotionRegistrySource.CombatPath }) { string destination = Path.Combine(repository, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(Path.Combine(root.FullName, relative), destination); }
            try
            {
                LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
                var arena = Object.FindAnyObjectByType<CombatArena>(); arena.characterAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
                var window = ScriptableObject.CreateInstance<FrameTimingWindow>(); window.name = "FrameApplyTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); yield return null;
                var view = window.rootVisualElement; string before = File.ReadAllText(Path.Combine(repository, AttackMotionSource.RelativePath)); string action = File.ReadAllText(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"));
                view.Q<FloatField>("frame-sword-first-ready").value += .03f; Click(window, "applyNext"); Assert.That(arena.FrameSwordTiming, Is.Not.Null); Assert.That(arena.BasicSwordFirst.Impact, Is.EqualTo(.23f).Within(.000001f));
                Click(window, "applyReset"); Assert.That(arena.Player.currentCast, Is.Null); Click(window, "restoreFixed"); Assert.That(arena.FrameSwordTiming, Is.Null); Assert.That(arena.BasicSwordFirst.Impact, Is.EqualTo(.2f));
                view.Q<FloatField>("frame-sword-first-ready").value = 0; Assert.That(view.Q<Button>("applyNext").enabledSelf, Is.False); Assert.That(view.Q<Button>("applyReset").enabledSelf, Is.False);
                Assert.That(File.ReadAllText(Path.Combine(repository, AttackMotionSource.RelativePath)), Is.EqualTo(before)); Assert.That(File.ReadAllText(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json")), Is.EqualTo(action)); window.Close();
            }
            finally { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        }
        static void Click(FrameTimingWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
