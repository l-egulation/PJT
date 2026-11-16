using System;
using System.Collections;
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
    public class DirectMeleeSpecialWindowTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var window in Resources.FindObjectsOfTypeAll<DirectMeleeSpecialWindow>().Where(value => value.name == "DirectNumericTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        DirectMeleeSpecialWindow Open()
        {
            var window = ScriptableObject.CreateInstance<DirectMeleeSpecialWindow>(); window.name = "DirectNumericTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); return window;
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator FieldsUndoSpatialSelectionAndInvalidGraphNeverMutateSource()
        {
            var window = Open(); yield return null; var root = window.rootVisualElement; byte[] before = File.ReadAllBytes(Path.Combine(repository, DirectMeleeSpecialSource.RelativePath));
            root.Q<FloatField>("damage").value = 42; root.Q<FloatField>("impact").value = .35f; root.Q<FloatField>("duration").value = .85f;
            var candidate = window.CompileCandidate()[0]; var preview = root.Q<ActionGeometryPreview>();
            Assert.That(candidate.Damage, Is.EqualTo(42)); Assert.That(preview.Model.Direct.ContentHash, Is.EqualTo(candidate.ContentHash));
            root.Q<Slider>("geometry-time").value = candidate.Impact; Assert.That(preview.CurrentSample.HitActive, Is.True); Assert.That(preview.CurrentSample.Root, Is.EqualTo(Vector2.zero));
            root.Q<Slider>("geometry-time").value = 0; Assert.That(preview.CurrentSample.HitActive, Is.False);
            root.Q<DropdownField>("action").value = "greatsword_range"; Assert.That(root.Q<FloatField>("offsetX").enabledSelf, Is.False);
            root.Q<FloatField>("width").value = 9; Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null; Assert.That(window.CompileCandidate()[2].ToBasicData().box.width, Is.EqualTo(10));
            root.Q<FloatField>("active").value = -1; Assert.That(preview.Model, Is.Null); Assert.That(root.Q<Button>("saveSource").enabledSelf, Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(repository, DirectMeleeSpecialSource.RelativePath)), Is.EqualTo(before));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualApplyButtonsPinCurrentVersionAndPreserveInvalidReset()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("damage").value = 42;
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<DirectMeleeSpecialWindow>().Single(value => value.name == "DirectNumericTestWindow"); var arena = Object.FindAnyObjectByType<CombatArena>(); window.SetRuntime(arena);
            using (var driver = arena.AcquireManualSimulation()) {
                driver.Reset(); foreach (var target in arena.enemies) target.aggressive = false; int form = Array.FindIndex(arena.catalog.forms, value => value.id == "base_sword");
                Assert.That(arena.Player.TryReconfigure(0, form, 0, out var error), Is.True, error); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var running = arena.Player.currentCast;
                string before = running.DefinitionVersion; Click(window, "applyNext"); Assert.That(running.DefinitionVersion, Is.EqualTo(before)); Assert.That(arena.GetDirectMeleeSpecial("base_sword_default").Damage, Is.EqualTo(42));
                float now = arena.Now; window.rootVisualElement.Q<FloatField>("active").value = -1;
                Assert.That(window.ApplyCandidate(true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.currentCast, Is.SameAs(running));
                window.rootVisualElement.Q<FloatField>("active").value = .1f; Click(window, "applyReset"); Assert.That(arena.Player.currentCast, Is.Null);
            }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator SourceSaveFailureIsVisibleAndRetryUsesSavedCanonicalNumericData()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("damage").value = 42; var expected = window.CompileCandidate();
            Assert.Throws<System.ComponentModel.Win32Exception>(() => window.SavePermanentSource("deviant-deliberately-missing-python"));
            Assert.That(window.rootVisualElement.Q<Label>("status").text, Does.Contain("원본 저장 완료, 생성 실패"));
            Assert.That(DirectMeleeSpecialSource.Load(repository, out _).Select(value => value.ContentHash), Is.EqualTo(expected.Select(value => value.ContentHash)));
            window.SavePermanentSource(); Assert.DoesNotThrow(() => DirectMeleeSpecialSource.ValidateGenerated(repository, BasicMeleeSourceTests.Generated(repository)));
        }
        static void Click(DirectMeleeSpecialWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
