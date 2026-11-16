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
    public class AuthoredSkillWindowTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); foreach (var window in Resources.FindObjectsOfTypeAll<AuthoredSkillWindow>().Where(value => value.name == "AuthoredSkillTestWindow")) window.Close(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        AuthoredSkillWindow Open() { var window = ScriptableObject.CreateInstance<AuthoredSkillWindow>(); window.name = "AuthoredSkillTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); return window; }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator EveryExistingActionSelectsWhitelistedFieldsAndReadonlySpatialSampling()
        {
            var window = Open(); yield return null; var root = window.rootVisualElement; byte[] before = File.ReadAllBytes(Path.Combine(repository, AuthoredSkillSource.RelativePath));
            foreach (string id in AuthoredSkillDefinition.SupportedIds) {
                root.Q<DropdownField>("action").value = id; Assert.That(root.Q<Label>("state").text, Does.Not.Contain("무효"), id); Assert.That(root.Q<ActionGeometryPreview>().Model.Authored.Id, Is.EqualTo(id));
            }
            root.Q<DropdownField>("action").value = "whip_default"; root.Q<FloatField>("hit-0-damage").value = 21; root.Q<FloatField>("hit-0-root").value = 1.5f;
            var preview = root.Q<ActionGeometryPreview>(); root.Q<Slider>("geometry-time").value = .25f; Assert.That(preview.CurrentSample.HitActive, Is.True); root.Q<Slider>("geometry-time").value = 0; Assert.That(preview.CurrentSample.HitActive, Is.False);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null; Assert.That(window.CompileCandidate().Single(value => value.Id == "whip_default").Execution.Hits[0].Root, Is.EqualTo(1.2f));
            root.Q<DropdownField>("action").value = "shield_default"; Assert.That(root.Q<FloatField>("effect-duration").enabledSelf, Is.False); Assert.That(root.Q<FloatField>("effect-parryWindow").enabledSelf, Is.True);
            root.Q<FloatField>("effect-parryWindow").value = .3f; Assert.That(window.CompileCandidate().Single(value => value.Id == "shield_default").Execution.Effect.ParryWindow, Is.EqualTo(.3f));
            root.Q<DropdownField>("action").value = "rapier_follow_up"; root.Q<FloatField>("freeze-1").value = .05f; Assert.That(root.Q<Button>("saveSource").enabledSelf, Is.False); Assert.That(preview.Model, Is.Null);
            Assert.That(File.ReadAllBytes(Path.Combine(repository, AuthoredSkillSource.RelativePath)), Is.EqualTo(before));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator NextStartPublicationPinsCurrentCastAndInvalidResetPreservesState()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("hit-0-damage").value = 21;
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<AuthoredSkillWindow>().Single(value => value.name == "AuthoredSkillTestWindow"); var arena = Object.FindAnyObjectByType<CombatArena>(); window.SetRuntime(arena);
            var floor = new GameObject("AuthoredWindowFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try { using (var driver = arena.AcquireManualSimulation()) {
                driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); } int form = Array.FindIndex(arena.catalog.forms, value => value.id == "whip"); Assert.That(arena.Player.TryReconfigure(0, form, 0, out var error), Is.True, error);
                arena.Player.Position = new Vector2(0, 1000); arena.Player.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var running = arena.Player.currentCast; Assert.That(running, Is.Not.Null); var original = running.Definition.Authored;
                Click(window, "applyNext"); Assert.That(running.Definition.Authored, Is.SameAs(original)); Assert.That(arena.GetAuthoredSkill("whip_default").Execution.Hits[0].Damage, Is.EqualTo(21));
                float now = arena.Now; window.rootVisualElement.Q<FloatField>("end").value = 0; Assert.That(window.ApplyCandidate(true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.currentCast, Is.SameAs(running));
            } }
            finally { Object.Destroy(floor); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator SavedWholeSourceSurvivesGenerationFailureAndRetry()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("hit-0-damage").value = 21; var expected = window.CompileCandidate();
            Assert.Throws<System.ComponentModel.Win32Exception>(() => window.SavePermanentSource("deviant-deliberately-missing-python")); Assert.That(window.rootVisualElement.Q<Label>("status").text, Does.Contain("원본 저장 완료, 생성 실패"));
            Assert.That(AuthoredSkillSource.Load(repository, out _).Select(value => value.ContentHash), Is.EqualTo(expected.Select(value => value.ContentHash))); window.SavePermanentSource(); Assert.DoesNotThrow(() => AuthoredSkillSource.ValidateGenerated(repository, BasicMeleeSourceTests.Generated(repository)));
        }
        static void Click(AuthoredSkillWindow window, string name) { var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True); using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); } }
    }
}
