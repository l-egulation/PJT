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
    public class FrameTimingPreviewTests
    {
        string repository;
        [SetUp] public void SetUp()
        {
            var original = new DirectoryInfo(Application.dataPath);
            while (original != null && !File.Exists(Path.Combine(original.FullName, AttackMotionSource.RelativePath))) original = original.Parent;
            Assert.That(original, Is.Not.Null); repository = Path.Combine(Path.GetTempPath(), "deviant-frame-preview-" + Guid.NewGuid().ToString("N"));
            foreach (string path in new[] { AttackMotionSource.RelativePath, "data/combat/actions/base_sword_basic_0.json", MotionRegistrySource.CombatPath }) {
                string destination = Path.Combine(repository, path); Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(Path.Combine(original.FullName, path), destination);
            }
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var window in Resources.FindObjectsOfTypeAll<FrameTimingWindow>().Where(w => w.name == "FrameTimingTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        FrameTimingWindow Open()
        {
            var window = ScriptableObject.CreateInstance<FrameTimingWindow>(); window.name = "FrameTimingTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); return window;
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator UndoingSourceLoadsCannotRemoveTheInitialFrameModel()
        {
            var window = Open(); yield return null; var root = window.rootVisualElement;
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Undo.PerformUndo(); yield return null;
            Assert.That(root.Q<FloatField>("frame-sword-first-ready"), Is.Not.Null);
            Assert.That(root.Q<Label>("state").text, Does.Not.Contain("미리보기 불가"));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator PreviewMovesAnchorsRejectsCyclesAndSupportsUndoWithoutSaving()
        {
            var window = Open(); yield return null; var root = window.rootVisualElement;
            string motion = File.ReadAllText(Path.Combine(repository, AttackMotionSource.RelativePath)); string action = File.ReadAllText(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"));
            Assert.That(root.Q<Label>("result").text, Does.Contain("200.00").And.Contain("400.00"));
            var first = root.Q<FloatField>("frame-sword-first-ready"); float baseline = first.value; first.value += .03f; yield return null;
            Assert.That(root.Q<Label>("result").text, Does.Contain("230.00").And.Contain("430.00")); Assert.That(root.Q<Label>("state").text, Does.Contain("미리보기 편집"));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null; Assert.That(root.Q<FloatField>("frame-sword-first-ready").value, Is.EqualTo(baseline));
            Undo.PerformRedo(); yield return null; Assert.That(root.Q<Label>("result").text, Does.Contain("230.00"));
            root.Q<EnumField>("anchor-contact").value = TimelineAnchor.Marker;
            root.Q<DropdownField>("reference-contact").value = FrameTimingProjection.ActionEnd;
            root.Q<EnumField>("anchor-action-end").value = TimelineAnchor.Marker;
            root.Q<DropdownField>("reference-action-end").value = FrameTimingProjection.Contact;
            Assert.That(root.Q<Label>("state").text, Does.Contain("Marker cycle")); Assert.That(root.Q<Label>("result").text, Is.Empty);
            ClickReload(window); Assert.That(root.Q<Label>("state").text, Does.Contain("가져온 시간과 동일"));
            root.Q<FloatField>("frame-sword-first-ready").value = 0; Assert.That(root.Q<Label>("state").text, Does.Contain("미리보기 불가"));
            Assert.That(File.ReadAllText(Path.Combine(repository, AttackMotionSource.RelativePath)), Is.EqualTo(motion)); Assert.That(File.ReadAllText(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json")), Is.EqualTo(action));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator PreviewDraftSurvivesPlayModeAndCannotChangeCombatOrSources()
        {
            var window = Open(); yield return null; window.rootVisualElement.Q<FloatField>("frame-sword-first-ready").value += .03f;
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<FrameTimingWindow>().Single(w => w.name == "FrameTimingTestWindow");
            Assert.That(window.rootVisualElement.Q<Label>("result").text, Does.Contain("230.00").And.Contain("430.00"));
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation())
            {
                var position = arena.Player.Position; float health = arena.Player.health, now = arena.Now; string version = arena.BasicSwordFirst.ContentHash;
                string motion = File.ReadAllText(Path.Combine(repository, AttackMotionSource.RelativePath));
                window.rootVisualElement.Q<FloatField>("frame-sword-first-ready").value += .03f; yield return null;
                Assert.That(window.rootVisualElement.Q<Label>("result").text, Does.Contain("260.00").And.Contain("460.00"));
                Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Player.health, Is.EqualTo(health)); Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(version));
                Assert.That(File.ReadAllText(Path.Combine(repository, AttackMotionSource.RelativePath)), Is.EqualTo(motion));
            }
            yield return new ExitPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<FrameTimingWindow>().Single(w => w.name == "FrameTimingTestWindow");
            Assert.That(window.rootVisualElement.Q<Label>("result").text, Does.Contain("260.00").And.Contain("460.00"));
        }
        static void ClickReload(FrameTimingWindow window)
        {
            var button = window.rootVisualElement.Q<Button>("reload");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
