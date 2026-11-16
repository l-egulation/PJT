using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Deviant.Authoring.Tests
{
    public class LocomotionAuthoringTests
    {
        string directory, source, generated;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "deviant-authoring-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            source = Path.Combine(directory, "player.json"); generated = Path.Combine(directory, "PlayerLocomotion.json");
            File.WriteAllText(source, Resources.Load<TextAsset>("PlayerLocomotion").text);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            SessionState.EraseString("LocomotionTestSourceBeforePlay");
            foreach (var window in Resources.FindObjectsOfTypeAll<LocomotionWindow>().Where(w => w.name == "LocomotionTestWindow")) window.Close();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test] public void SaveReloadAndExportUseTheSameCompiledValues()
        {
            var data = LocomotionSource.Load(source, out var hash);
            data.dashSpeed = 11; data.firstJumpVelocity = 18; data.secondJumpVelocity = 22; data.riseGravity = 72; data.fallGravity = 110;
            var expected = LocomotionSource.Validate(data);
            hash = LocomotionSource.Save(source, hash, data);
            LocomotionSource.Export(source, hash, generated);
            CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(generated));
            var loaded = LocomotionSource.Load(source, out var reloadedHash);
            Assert.That(reloadedHash, Is.EqualTo(hash));
            Assert.That(LocomotionSource.Validate(loaded).ContentHash, Is.EqualTo(expected.ContentHash));
            Assert.That(loaded.firstJumpVelocity, Is.EqualTo(18)); Assert.That(loaded.secondJumpVelocity, Is.EqualTo(22));
            Assert.That(loaded.riseGravity, Is.EqualTo(72)); Assert.That(loaded.fallGravity, Is.EqualTo(110));
            byte[] before = File.ReadAllBytes(source);
            LocomotionSource.Save(source, hash, loaded);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(source), "Repeated save must be deterministic.");
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }

        [Test] public void ExternalChangesArePreservedEvenWhenOnlyWhitespaceChanged()
        {
            var data = LocomotionSource.Load(source, out var hash);
            File.AppendAllText(source, "\n");
            byte[] external = File.ReadAllBytes(source); data.dashSpeed = 12;
            Assert.Throws<IOException>(() => LocomotionSource.Save(source, hash, data));
            Assert.Throws<IOException>(() => LocomotionSource.Export(source, hash, generated));
            CollectionAssert.AreEqual(external, File.ReadAllBytes(source));
            Assert.That(File.Exists(generated), Is.False);
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }

        [Test] public void InvalidEditsCannotChangeSourceOrRecreateDeletedSource()
        {
            var data = LocomotionSource.Load(source, out var hash);
            byte[] before = File.ReadAllBytes(source); data.dashDuration = -1;
            Assert.Throws<ArgumentException>(() => LocomotionSource.Save(source, hash, data));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(source));
            data.dashDuration = .2f;
            File.Delete(source);
            Assert.Throws<IOException>(() => LocomotionSource.Save(source, hash, data));
            Assert.That(File.Exists(source), Is.False);
        }

        [Test] public void FailedExportLeavesSavedSourceAvailableForRetry()
        {
            var data = LocomotionSource.Load(source, out var hash); data.runSpeed = 7;
            hash = LocomotionSource.Save(source, hash, data);
            Assert.Throws<DirectoryNotFoundException>(() => LocomotionSource.Export(source, hash, Path.Combine(directory, "absent", "catalog.json")));
            Assert.That(LocomotionSource.Load(source, out _).runSpeed, Is.EqualTo(7));
            LocomotionSource.Export(source, hash, generated);
            Assert.That(LocomotionSource.Load(generated, out _).runSpeed, Is.EqualTo(7));
        }

        LocomotionWindow Window()
        {
            var window = ScriptableObject.CreateInstance<LocomotionWindow>();
            window.name = "LocomotionTestWindow";
            window.Show(); window.CreateGUI(); window.LoadSource(source, generated);
            return window;
        }
        static void Click(LocomotionWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name);
            Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }

        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowValidationSaveReloadAndUndoPreserveFileIdentity()
        {
            float initialSpeed = LocomotionSource.Load(source, out _).dashSpeed;
            var window = Window(); yield return null;
            Assert.That(window.rootVisualElement.Query<FloatField>().ToList().Count, Is.EqualTo(13), "11 movement fields and the two dash curve fields");
            Assert.That(window.rootVisualElement.Q<Button>("apply").enabledSelf, Is.False);
            Undo.IncrementCurrentGroup();
            var speed = window.rootVisualElement.Q<FloatField>("dashSpeed");
            speed.value = -1;
            Assert.That(window.rootVisualElement.Q<Button>("save").enabledSelf, Is.False);
            speed.value = 11;
            StringAssert.Contains($"{initialSpeed:0.###} → 11", window.rootVisualElement.Q<Label>("diff").text);
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            Click(window, "save"); yield return null;
            Assert.That(LocomotionSource.Load(source, out _).dashSpeed, Is.EqualTo(11));
            Undo.PerformUndo(); yield return null;
            Assert.That(speed.value, Is.EqualTo(initialSpeed));
            Click(window, "save"); yield return null;
            Assert.That(LocomotionSource.Load(source, out _).dashSpeed, Is.EqualTo(initialSpeed), "Undo must not roll back the source's concurrency hash.");
            speed.value = 9;
            Click(window, "reload"); yield return null;
            Assert.That(speed.value, Is.EqualTo(initialSpeed));
            CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(generated));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowDraftSurvivesPlayEntryAndAppliesWithoutSavingSource()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            UnityEngine.Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            var window = Window(); yield return null;
            window.rootVisualElement.Q<FloatField>("dashSpeed").value = 11;
            // Iterator locals are not retained across Unity's domain reload.
            SessionState.SetString("LocomotionTestSourceBeforePlay", Convert.ToBase64String(File.ReadAllBytes(source)));
            yield return new EnterPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<LocomotionWindow>().Single(w => w.name == "LocomotionTestWindow");
            window.CreateGUI();
            Assert.That(window.rootVisualElement.Q<FloatField>("dashSpeed").value, Is.EqualTo(11));
            Click(window, "apply"); yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType<CombatArena>().Movement.DashSpeed, Is.EqualTo(11));
            Assert.That(Convert.ToBase64String(File.ReadAllBytes(source)), Is.EqualTo(SessionState.GetString("LocomotionTestSourceBeforePlay", "")));
            Assert.That(File.Exists(generated), Is.False);
        }
    }
}
