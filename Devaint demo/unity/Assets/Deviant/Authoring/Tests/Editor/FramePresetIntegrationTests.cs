using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class FramePresetIntegrationTests
    {
        struct Metrics { public int firstHit, hits; public float health, impact, end; public string version; }
        [UnityTest] public IEnumerator GameReloadsTheSavedFrameExperimentInAFreshRuntime() => Replay("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabReloadsTheSavedFrameExperimentInAFreshRuntime() => Replay("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            CloseWindows();
        }
        static void CloseWindows()
        { foreach (var w in Resources.FindObjectsOfTypeAll<FrameTimingWindow>().Where(w => w.name == "FramePresetTestWindow")) w.Close(); }
        static FrameTimingWindow Window(string repository)
        {
            var w = ScriptableObject.CreateInstance<FrameTimingWindow>(); w.name = "FramePresetTestWindow"; w.Show(); w.CreateGUI(); w.LoadRepository(repository); return w;
        }
        static GameObject Bind(CombatArena arena)
        {
            arena.characterAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var floor = new GameObject("FramePresetFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); return floor;
        }
        static Metrics Run(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            p.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0); p.Position = Vector2.zero; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(3, 0); Physics2D.SyncTransforms();
            var result = new Metrics { firstHit = -1 };
            driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var cast = p.currentCast;
            Assert.That(cast, Is.Not.Null); result.impact = cast.ImpactTime; result.end = cast.MotionDuration; result.version = cast.FrameTimingVersion;
            for (int tick = 1; tick < 30; tick++) { driver.Step(.02f); if (result.firstHit < 0 && arena.enemies[0].ReceivedHitCount > 0) result.firstHit = tick; }
            result.health = arena.enemies[0].health; result.hits = arena.enemies[0].ReceivedHitCount; return result;
        }
        static IEnumerator Replay(string scene)
        {
            string repository = FramePresetSourceTests.CopyRepository(), path = Path.Combine(repository, "experiment.json");
            GameObject floor = null;
            try
            {
                var candidate = FramePresetSourceTests.Candidate(repository); string fileHash = FramePresetSource.Save(path, null, repository, candidate);
                string actionPath = Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"), motionPath = Path.Combine(repository, AttackMotionSource.RelativePath);
                byte[] actionBefore = File.ReadAllBytes(actionPath), motionBefore = File.ReadAllBytes(motionPath);
                LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
                var firstArena = Object.FindAnyObjectByType<CombatArena>(); floor = Bind(firstArena); Metrics before;
                using (var driver = firstArena.AcquireManualSimulation()) {
                    Assert.That(firstArena.TryApplySwordFrameTiming(candidate, false, out var error), Is.True, error); before = Run(firstArena, driver);
                    Assert.That(before.version, Is.EqualTo(candidate.ContentHash)); Assert.That(before.impact, Is.EqualTo(.23f).Within(.000001f)); Assert.That(before.end, Is.EqualTo(.43f).Within(.000001f)); Assert.That(before.health, Is.EqualTo(90)); Assert.That(before.hits, Is.EqualTo(1));
                }
                Object.Destroy(floor); floor = null; yield return new ExitPlayMode();
                // Close/reopen Play Mode and construct a new arena/window. No in-memory
                // candidate is supplied to the second run; only the saved file is read.
                LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
                var arena = Object.FindAnyObjectByType<CombatArena>(); floor = Bind(arena);
                using (var driver = arena.AcquireManualSimulation()) {
                    var window = Window(repository); string fixedVersion = arena.BasicSwordFirst.ContentHash; float now = arena.Now;
                    window.LoadPreset(path); Assert.That(arena.FrameSwordTiming, Is.Null); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(fixedVersion)); Assert.That(arena.Player.currentCast, Is.Null);
                    Assert.That(window.rootVisualElement.Q<Label>("state").text, Does.Contain("실험 파일과 동일")); Click(window, "applyNext"); var after = Run(arena, driver);
                    Assert.That((after.firstHit, after.hits, after.health, after.impact, after.end, after.version), Is.EqualTo((before.firstHit, before.hits, before.health, before.impact, before.end, before.version)));
                    Assert.That(FramePresetSource.Load(path, repository, out var reloadedHash).ContentHash, Is.EqualTo(candidate.ContentHash)); Assert.That(reloadedHash, Is.EqualTo(fileHash));
                    string bad = Path.Combine(repository, "bad.json"); File.WriteAllText(bad, "{}"); string display = window.rootVisualElement.Q<Label>("result").text; string binding = arena.FrameSwordTiming.ContentHash;
                    Assert.Catch<ArgumentException>(() => window.LoadPreset(bad)); Assert.That(window.rootVisualElement.Q<Label>("result").text, Is.EqualTo(display)); Assert.That(arena.FrameSwordTiming.ContentHash, Is.EqualTo(binding));
                    Assert.That(File.ReadAllBytes(actionPath), Is.EqualTo(actionBefore)); Assert.That(File.ReadAllBytes(motionPath), Is.EqualTo(motionBefore));
                    var source = BasicSwordSource.Load(actionPath, out var hash); source.hit.damage += 1; BasicSwordSource.Save(repository, hash, source);
                    Assert.Catch<ArgumentException>(() => window.LoadPreset(path)); Assert.That(window.rootVisualElement.Q<Label>("result").text, Is.EqualTo(display)); Assert.That(arena.FrameSwordTiming.ContentHash, Is.EqualTo(binding));
                    string stale = Path.Combine(repository, "stale.json"); Assert.Catch<ArgumentException>(() => window.SavePreset(stale, null)); Assert.That(File.Exists(stale), Is.False); window.Close();
                }
            }
            finally { if (floor) Object.Destroy(floor); CloseWindows(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowTracksSavedEditsUndoPlayAndExternalFileConflictsSeparately()
        {
            string repository = FramePresetSourceTests.CopyRepository(), path = Path.Combine(repository, "experiment.json");
            try
            {
                var window = Window(repository); yield return null; var root = window.rootVisualElement;
                Assert.That(root.Q<Button>("importPreset"), Is.Not.Null); Assert.That(root.Q<Button>("exportPreset").enabledSelf, Is.True);
                root.Q<FloatField>("frame-sword-first-ready").value += .03f; string hash = window.SavePreset(path, null); byte[] saved = File.ReadAllBytes(path);
                Assert.That(root.Q<Label>("state").text, Does.Contain("실험 파일과 동일")); root.Q<FloatField>("frame-sword-first-ready").value += .03f;
                Assert.That(root.Q<Label>("state").text, Does.Contain("실험 파일 미저장")); Assert.That(File.ReadAllBytes(path), Is.EqualTo(saved)); window.Close();
                window = Window(repository); window.LoadPreset(path); yield return null; root = window.rootVisualElement;
                Assert.That(root.Q<Label>("result").text, Does.Contain("230.00").And.Contain("430.00"));
                root.Q<FloatField>("frame-sword-first-ready").value += .03f; yield return null; Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
                Assert.That(root.Q<Label>("state").text, Does.Contain("실험 파일과 동일")); Undo.PerformRedo(); yield return null; Assert.That(root.Q<Label>("state").text, Does.Contain("실험 파일 미저장"));
                LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
                window = Resources.FindObjectsOfTypeAll<FrameTimingWindow>().Single(w => w.name == "FramePresetTestWindow"); root = window.rootVisualElement;
                Assert.That(root.Q<Label>("preset").text, Does.Contain(path)); Assert.That(root.Q<Label>("result").text, Does.Contain("260.00")); Assert.That(Object.FindAnyObjectByType<CombatArena>().FrameSwordTiming, Is.Null);
                File.AppendAllText(path, "\n"); string external = File.ReadAllText(path); Assert.Throws<IOException>(() => window.SavePreset(path, hash)); Assert.That(File.ReadAllText(path), Is.EqualTo(external));
                Click(window, "reload"); Assert.That(root.Q<Label>("preset").text, Does.Contain("선택 없음")); Assert.That(root.Q<Label>("result").text, Does.Contain("200.00")); Assert.That(File.ReadAllText(path), Is.EqualTo(external));
            }
            finally { CloseWindows(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator LoadedSourceVersionAndBaselineFollowTheDraftWhenImportIsUndone()
        {
            string repository = FramePresetSourceTests.CopyRepository();
            try
            {
                var oldCandidate = FramePresetSourceTests.Candidate(repository); var window = Window(repository); yield return null;
                window.rootVisualElement.Q<FloatField>("frame-sword-first-ready").value += .03f; yield return null;
                string sourcePath = Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"); var source = BasicSwordSource.Load(sourcePath, out var hash);
                source.hit.impact = .25f; source.hit.duration = .5f; BasicSwordSource.Save(repository, hash, source);
                var next = FramePresetSourceTests.Candidate(repository); string path = Path.Combine(repository, "new-source-experiment.json"); FramePresetSource.Save(path, null, repository, next);
                window.LoadPreset(path); yield return null; Assert.That(window.rootVisualElement.Q<Label>("source").text, Does.Contain(next.SourceAction.ContentHash));
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
                Assert.That(window.rootVisualElement.Q<Label>("source").text, Does.Contain(oldCandidate.SourceAction.ContentHash).And.Not.Contain(next.SourceAction.ContentHash));
                Assert.That(window.rootVisualElement.Q<Label>("result").text, Does.Contain("접촉 200.00"));
            }
            finally { CloseWindows(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        }
        static void Click(FrameTimingWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
