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
    public class ChargedMeleeWindowTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); foreach (var window in Resources.FindObjectsOfTypeAll<ChargedMeleeWindow>().Where(value => value.name == "ChargedNumericTestWindow")) window.Close(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        ChargedMeleeWindow Open() { var window = ScriptableObject.CreateInstance<ChargedMeleeWindow>(); window.name = "ChargedNumericTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); return window; }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualFieldsResolveReleaseAndScrubWithoutStartingAChargeOrSaving()
        {
            var window = Open(); yield return null; var root = window.rootVisualElement; byte[] before = File.ReadAllBytes(Path.Combine(repository, ChargedMeleeSource.RelativePath));
            root.Q<FloatField>("minimumDamage").value = 32; root.Q<FloatField>("maximumDamage").value = 66; root.Q<FloatField>("minimum").value = .24f; root.Q<FloatField>("maximum").value = .88f;
            root.Q<FloatField>("baseEnd").value = .65f; root.Q<FloatField>("previewRelease").value = .56f;
            var candidate = window.CompileCandidate()[0]; var expected = candidate.Policy.Resolve(.56f); var preview = root.Q<ActionGeometryPreview>();
            Assert.That(preview.Model.Release.Value.Damage, Is.EqualTo(expected.Damage)); Assert.That(preview.Model.End, Is.EqualTo(expected.MotionEnd));
            root.Q<Slider>("geometry-time").value = expected.ReleaseTime; Assert.That(preview.CurrentSample.HitActive, Is.True); Assert.That(preview.CurrentSample.Root, Is.EqualTo(Vector2.zero));
            root.Q<Slider>("geometry-time").value = 0; Assert.That(preview.CurrentSample.HitActive, Is.False);
            float width = root.Q<FloatField>("width").value; root.Q<FloatField>("width").value += .5f; Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Assert.That(window.CompileCandidate()[0].ToNumericData().box.width, Is.EqualTo(width));
            root.Q<FloatField>("maximum").value = .1f; Assert.That(preview.Model, Is.Null); Assert.That(root.Q<Button>("saveSource").enabledSelf, Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(repository, ChargedMeleeSource.RelativePath)), Is.EqualTo(before));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualApplyPinsHeldChargeAndRejectsInvalidReset()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("maximumDamage").value = 66;
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<ChargedMeleeWindow>().Single(value => value.name == "ChargedNumericTestWindow"); var arena = Object.FindAnyObjectByType<CombatArena>(); window.SetRuntime(arena);
            using (var driver = arena.AcquireManualSimulation()) {
                driver.Reset(); foreach (var target in arena.enemies) target.aggressive = false; int form = Array.FindIndex(arena.catalog.forms, value => value.id == "rapier"); Require(arena.Player.TryReconfigure(0, form, 0, out var error), error);
                arena.Player.Position = new Vector2(0, 1000); arena.Player.velocity = Vector2.zero; arena.Player.grounded = true; arena.Player.SyncFacing(1);
                foreach (var target in arena.enemies) target.Position = new Vector2(40, 1000);
                arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special, true)); var running = arena.Player.currentCast; Assert.That(running, Is.Not.Null, "The nearest-target charge must actually start before testing version pins."); var previous = running.ChargedMelee;
                Click(window, "applyNext"); Assert.That(running.ChargedMelee, Is.SameAs(previous)); Assert.That(arena.GetChargedMelee("rapier_default").Policy.MaximumDamage, Is.EqualTo(66));
                float now = arena.Now; window.rootVisualElement.Q<FloatField>("maximum").value = .1f;
                Assert.That(window.ApplyCandidate(true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.currentCast, Is.SameAs(running));
                window.rootVisualElement.Q<FloatField>("maximum").value = .8f; Click(window, "applyReset"); Assert.That(arena.Player.currentCast, Is.Null);
            }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator SavedCanonicalChargeSurvivesGenerationFailureAndRetry()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("maximumDamage").value = 66; var candidate = window.CompileCandidate();
            Assert.Throws<System.ComponentModel.Win32Exception>(() => window.SavePermanentSource("deviant-deliberately-missing-python")); Assert.That(window.rootVisualElement.Q<Label>("status").text, Does.Contain("원본 저장 완료, 생성 실패"));
            Assert.That(ChargedMeleeSource.Load(repository, out _).Select(value => value.ContentHash), Is.EqualTo(candidate.Select(value => value.ContentHash)));
            window.SavePermanentSource(); Assert.DoesNotThrow(() => ChargedMeleeSource.ValidateGenerated(repository, BasicMeleeSourceTests.Generated(repository)));
        }
        static void Require(bool value, string error) => Assert.That(value, Is.True, error);
        static void Click(ChargedMeleeWindow window, string name) { var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True); using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); } }
    }
}
