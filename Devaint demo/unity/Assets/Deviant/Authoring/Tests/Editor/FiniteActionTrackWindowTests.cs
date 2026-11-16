using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
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
    public class FiniteActionTrackWindowTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var window in Resources.FindObjectsOfTypeAll<FiniteActionTrackWindow>().Where(value => value.name == "FiniteTrackTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        FiniteActionTrackWindow Open()
        {
            var window = ScriptableObject.CreateInstance<FiniteActionTrackWindow>(); window.name = "FiniteTrackTestWindow";
            window.Show(); window.CreateGUI(); window.LoadRepository(repository); return window;
        }
        static void Click(FiniteActionTrackWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualFieldsEditMarkersGeometryAndUndoWithoutSavingOrFiringScrub()
        {
            var window = Open(); yield return null; var root = window.rootVisualElement;
            byte[] before = File.ReadAllBytes(Path.Combine(repository, FiniteActionTrackSource.RelativePath));
            Assert.That(root.Q<Label>("effective").text, Does.Contain("activation=0.15"));
            root.Q<FloatField>("frame-windup").value += .03f; root.Q<FloatField>("box-thrust-width").value = 2;
            var candidate = window.CompileCandidate()[0]; Assert.That(candidate.Activation, Is.EqualTo(.18f).Within(.000001f)); Assert.That(candidate.BodyEnd, Is.EqualTo(.63f).Within(.000001f));
            var preview = root.Q<ActionGeometryPreview>(); Assert.That(preview.Model.Tracks.ContentHash, Is.EqualTo(candidate.ContentHash));
            root.Q<Slider>("geometry-time").value = .3f; float forward = preview.CurrentSample.Root.x;
            root.Q<Slider>("geometry-time").value = .1f; Assert.That(preview.CurrentSample.Root.x, Is.Zero); Assert.That(forward, Is.GreaterThan(0));
            root.Q<Toggle>("geometry-facing-left").value = true; root.Q<Slider>("geometry-time").value = .3f; Assert.That(preview.CurrentSample.Root.x, Is.EqualTo(-forward).Within(.000001f));
            root.Q<EnumField>("anchor-contact").value = TimelineAnchor.Marker; root.Q<DropdownField>("reference-contact").value = "dash-end";
            Assert.That(root.Q<Label>("state").text, Does.Contain("Marker cycle")); Assert.That(preview.Model, Is.Null); Assert.That(root.Q<Button>("saveSource").enabledSelf, Is.False);
            Click(window, "reload"); Assert.That(window.CompileCandidate()[0].Activation, Is.EqualTo(.15f));
            root.Q<FloatField>("frame-windup").value += .03f; Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Assert.That(window.CompileCandidate()[0].Activation, Is.EqualTo(.15f));
            Assert.That(File.ReadAllBytes(Path.Combine(repository, FiniteActionTrackSource.RelativePath)), Is.EqualTo(before));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator AllFourNumericFieldsShareWholeCandidateUndoAndCompiledPreview()
        {
            var window = Open(); yield return null; var root = window.rootVisualElement; byte[] before = File.ReadAllBytes(Path.Combine(repository, FiniteActionTrackSource.RelativePath));
            var originals = window.CompileCandidate(); var action = root.Q<DropdownField>("action");
            root.Q<FloatField>("damage-thrust").value = 13; root.Q<FloatField>("dash-speed").value = 12; root.Q<FloatField>("wall-recovery").value = .4f;
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Assert.That(window.CompileCandidate()[0].Execution.Movement.WallRecovery, Is.EqualTo(.25f)); root.Q<FloatField>("wall-recovery").value = .4f;
            var preview = root.Q<ActionGeometryPreview>(); root.Q<Slider>("geometry-time").value = .35f; Assert.That(preview.CurrentSample.Root.x, Is.EqualTo(2.4f).Within(.00001f));
            root.Q<Slider>("geometry-time").value = 0; Assert.That(preview.CurrentSample.Root.x, Is.Zero);
            action.value = ActionTrackDefinition.FollowUpId; root.Q<FloatField>("damage-primary").value = 9; root.Q<FloatField>("damage-echo").value = 17;
            action.value = ActionTrackDefinition.WaveId; root.Q<FloatField>("damage-wave").value = 53; root.Q<FloatField>("wave-speed").value = 8; root.Q<FloatField>("wave-distance").value = 2; root.Q<FloatField>("wave-spawn-x").value = 2; root.Q<FloatField>("wave-spawn-y").value = 1.25f;
            root.Q<Slider>("geometry-time").value = .3f; Assert.That(preview.CurrentSample.Hitbox.center.x, Is.EqualTo(2.8f).Within(.00001f)); Assert.That(preview.CurrentSample.Hitbox.center.y, Is.EqualTo(1.25f));
            action.value = ActionTrackDefinition.GuardId; root.Q<FloatField>("guard-hp").value = 50; root.Q<FloatField>("guard-reduction").value = .25f; Assert.That(preview.CurrentSample.HitActive, Is.False); Assert.That(root.Q<FloatField>("damage-guard"), Is.Null);
            var edited = window.CompileCandidate(); foreach (var value in edited) { var original = originals.Single(item => item.Id == value.Id); Assert.That(value.SourceVersion, Is.EqualTo(original.SourceVersion)); Assert.That(value.ContentHash, Is.Not.EqualTo(original.ContentHash)); }
            root.Q<FloatField>("guard-reduction").value = 1.1f; Assert.That(preview.Model, Is.Null); Assert.That(root.Q<Button>("saveSource").enabledSelf, Is.False); Assert.Throws<ArgumentException>(() => window.CompileCandidate());
            root.Q<FloatField>("guard-reduction").value = .25f; Assert.That(window.CompileCandidate().Single(value => value.Id == ActionTrackDefinition.GuardId).Execution.Effect.Hp, Is.EqualTo(50)); Assert.That(File.ReadAllBytes(Path.Combine(repository, FiniteActionTrackSource.RelativePath)), Is.EqualTo(before));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WholeApplyPinsCurrentCastRejectsInvalidResetAndSurvivesPlayMode()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("frame-windup").value += .03f; window.rootVisualElement.Q<FloatField>("damage-thrust").value = 13; window.rootVisualElement.Q<FloatField>("dash-speed").value = 12;
            string expected = window.CompileCandidate()[0].ContentHash;
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<FiniteActionTrackWindow>().Single(value => value.name == "FiniteTrackTestWindow");
            Assert.That(window.CompileCandidate()[0].ContentHash, Is.EqualTo(expected));
            var arena = Object.FindAnyObjectByType<CombatArena>(); window.SetRuntime(arena);
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); var player = arena.Player; player.selected = 0;
                foreach (var enemy in arena.enemies) enemy.aggressive = false;
                int form = Array.FindIndex(arena.catalog.forms, value => value.id == "base_sword");
                Assert.That(player.TryReconfigure(0, form, 2, out var error), Is.True, error);
                driver.Step(.02f, new Deviant.Combat.Contracts.CombatInput(0, Deviant.Combat.Contracts.CombatButtons.Special)); var running = player.currentCast;
                string old = running.DefinitionVersion; Click(window, "applyNext");
                Assert.That(running.DefinitionVersion, Is.EqualTo(old)); Assert.That(arena.GetActionTracks(ActionTrackDefinition.MovementId).ContentHash, Is.EqualTo(expected));
                Assert.That(running.Definition.Hits[0].Damage, Is.EqualTo(40)); Assert.That(running.Definition.Movement.Speed, Is.EqualTo(20)); Assert.That(arena.GetActionTracks(ActionTrackDefinition.MovementId).Execution.Hits[0].Damage, Is.EqualTo(13));
                float now = arena.Now; var position = player.Position;
                window.rootVisualElement.Q<FloatField>("frame-windup").value = 0;
                Assert.That(window.ApplyCandidate(true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.Position, Is.EqualTo(position)); Assert.That(player.currentCast, Is.SameAs(running));
                window.rootVisualElement.Q<FloatField>("frame-windup").value = .15f + .03f; Click(window, "applyReset"); Assert.That(player.currentCast, Is.Null);
            }
            yield return new ExitPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<FiniteActionTrackWindow>().Single(value => value.name == "FiniteTrackTestWindow");
            Assert.That(window.CompileCandidate()[0].ContentHash, Is.EqualTo(expected));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator SavePublishesCanonicalTracksAndGeneratorFailureIsVisibleWithSavedSourcePreserved()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("frame-windup").value += .03f; window.rootVisualElement.Q<FloatField>("damage-thrust").value = 13; window.rootVisualElement.Q<FloatField>("dash-speed").value = 12;
            var candidate = window.CompileCandidate();
            Assert.Throws<System.ComponentModel.Win32Exception>(() => window.SavePermanentSource("deviant-deliberately-missing-python"));
            Assert.That(window.rootVisualElement.Q<Label>("status").text, Does.Contain("원본 저장 완료, 생성 실패"));
            Assert.That(FiniteActionTrackSource.Load(repository, out _).Select(value => value.ContentHash), Is.EqualTo(candidate.Select(value => value.ContentHash)));
            window.SavePermanentSource();
            Assert.DoesNotThrow(() => FiniteActionTrackSource.ValidateGenerated(repository, BasicMeleeSourceTests.Generated(repository)));
            Click(window, "reload"); Assert.That(window.CompileCandidate().Select(value => value.ContentHash), Is.EqualTo(candidate.Select(value => value.ContentHash)));
        }
    }
}
