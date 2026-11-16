using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class BasicMeleeAuthoringTests
    {
        string repository;
        [SetUp] public void SetUp() { repository = BasicMeleeSourceTests.CopyRepository(); }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var window in Resources.FindObjectsOfTypeAll<BasicMeleeWindow>().Where(window => window.name == "BasicMeleeTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, candidate => candidate.id == form), 0, out var error), Is.True, error);
            player.Position = Vector2.zero; player.grounded = true; player.velocity = Vector2.zero; player.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(1, 0); Physics2D.SyncTransforms();
        }
        static GameObject Floor()
        {
            var floor = new GameObject("MeleeAuthoringFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); return floor;
        }
        static CombatCast Start(CombatArena arena, int index)
        {
            typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena.Player, new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), index, arena.Now });
            Assert.That(arena.Player.currentCast, Is.Not.Null); return arena.Player.currentCast;
        }
        static void Finish(CombatArena.ManualSimulation driver)
        { for (int tick = 0; tick < 120; tick++) driver.Step(.02f, default); }
        static void AssertHit(CombatArena arena, CombatCast cast, BasicMeleeDefinition definition)
        {
            Assert.That(cast.DefinitionVersion, Is.EqualTo(definition.ContentHash)); Assert.That(cast.ImpactTime, Is.EqualTo(definition.Impact)); Assert.That(cast.MotionDuration, Is.EqualTo(definition.Duration));
            Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.enemies[0].health, Is.EqualTo(100 - definition.Damage).Within(.00001f));
        }

        [UnityTest] public IEnumerator GameAllSixteenApplyAtNextStartAndKeepRunningDefinitions() => AllHits("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabAllSixteenApplyAtNextStartAndKeepRunningDefinitions() => AllHits("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator AllHits(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = Floor();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    var first = arena.BasicSwordFirst; string mutableCatalog = JsonUtility.ToJson(arena.catalog);
                    var hits = BasicMeleeSource.Load(repository, out _);
                    foreach (var hit in hits)
                    {
                        Setup(arena, driver, hit.formId); var original = arena.GetBasicMelee(hit.formId, hit.hitIndex); var running = Start(arena, hit.hitIndex);
                        hit.hit.damage += 3; hit.hit.duration += .12f; hit.hit.impact += .03f;
                        var candidate = BasicMeleeSource.Compile(hits);
                        Assert.That(arena.TryApplyBasicMelee(candidate, false, out var error), Is.True, error);
                        Assert.That(arena.Player.currentCast, Is.SameAs(running)); Assert.That(running.DefinitionVersion, Is.EqualTo(original.ContentHash));
                        Finish(driver); AssertHit(arena, running, original);
                        var edited = arena.GetBasicMelee(hit.formId, hit.hitIndex);
                        Assert.That(edited.ContentHash, Is.Not.EqualTo(original.ContentHash));
                        if (hit.formId == "base_sword") Assert.That(arena.BasicSwordSecond, Is.SameAs(edited));
                        Setup(arena, driver, hit.formId); var next = Start(arena, hit.hitIndex); Finish(driver); AssertHit(arena, next, edited);
                    }
                    Assert.That(arena.BasicSwordFirst, Is.SameAs(first)); Assert.That(JsonUtility.ToJson(arena.catalog), Is.EqualTo(mutableCatalog));
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.Destroy(floor); }
        }

        [UnityTest] public IEnumerator InvalidWholeReplacementNeverResetsOrPublishesAndFirstSwordKeepsItsOwnGuards()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation())
            {
                Setup(arena, driver, "dagger"); var original = arena.GetBasicMelee("dagger", 0); var running = Start(arena, 0); float now = arena.Now;
                var data = original.ToData(); data.damage = 30; BasicMeleeDefinition.TryCreate(original.Id, data, out var edited, out _);
                Assert.That(arena.TryApplyBasicMelee(new[] { edited, null }, true, out _), Is.False);
                Assert.That(arena.TryApplyBasicMelee(new[] { edited, arena.BasicSwordFirst }, true, out var firstError), Is.False); Assert.That(firstError, Does.Contain("Basic Sword First Hit"));
                Assert.That(arena.GetBasicMelee("dagger", 0), Is.SameAs(original)); Assert.That(arena.Player.currentCast, Is.SameAs(running)); Assert.That(arena.Now, Is.EqualTo(now));
                var hits = BasicMeleeSource.Load(repository, out _); hits[0].hit.damage = 23; hits[hits.Length - 1].hit.duration = 0;
                Assert.Throws<ArgumentException>(() => BasicMeleeSource.Compile(hits));
                Assert.That(arena.Player.currentCast, Is.SameAs(running));
                hits = BasicMeleeSource.Load(repository, out _); hits[0].hit.damage = 23;
                Assert.That(arena.TryApplyBasicMelee(BasicMeleeSource.Compile(hits), true, out var error), Is.True, error);
                Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.BasicSwordSecond.Damage, Is.EqualTo(23));
                Assert.That(running.ResourcesActive, Is.False);
                arena.characterAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
                Assert.That(arena.TryGetBasicSwordMotion(out var motion, out error), Is.True, error);
                var first = arena.BasicSwordFirst;
                var frame = MeleeFrameCompiler.CompileFirst(first, motion, MeleeFrameCompiler.ProjectFirst(first, motion, 2.5f), 2.5f);
                Assert.That(arena.TryApplySwordFrameTiming(frame, false, out error), Is.True, error);
                Assert.That(arena.TryApplyBasicMelee(BasicMeleeSource.Compile(hits), false, out error), Is.True, error);
                Assert.That(arena.FrameSwordTiming, Is.SameAs(frame));
                Assert.That(arena.TryApplyBasicMelee(new[] { frame.Action }, true, out error), Is.False);
                Assert.That(arena.TryApplyBasicSwordFirst(first.ToData(), false, out error), Is.False, "The existing first-sword frame guard remains the only numeric path.");
                Assert.That(arena.TryRestoreSwordFixedTiming(false, out error), Is.True, error);
            }
        }

        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowShowsSharedActiveAppliesPinsSavesReloadsAndSeparatesGenerationFailure()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = Floor();
            var window = ScriptableObject.CreateInstance<BasicMeleeWindow>(); window.name = "BasicMeleeTestWindow";
            window.Show(); window.CreateGUI(); window.LoadRepository(repository); yield return null;
            try
            {
                var root = window.rootVisualElement; var selector = root.Q<DropdownField>("hit");
                Assert.That(root.Query<FloatField>().ToList().Count(field => field.GetFirstAncestorOfType<ActionGeometryPreview>() == null), Is.EqualTo(8)); Assert.That(selector.choices.Count, Is.EqualTo(17));
                string source = Path.Combine(repository, BasicMeleeSource.RelativePath); byte[] before = File.ReadAllBytes(source);
                using (var driver = arena.AcquireManualSimulation())
                {
                    Setup(arena, driver, "base_sword"); var original = arena.BasicSwordSecond; var running = Start(arena, 1);
                    root.Q<FloatField>("damage").value = 22; root.Q<FloatField>("duration").value = .75f; root.Q<FloatField>("impact").value = .31f;
                    Click(window, "applyNext"); Assert.That(arena.Player.currentCast, Is.SameAs(running)); Assert.That(running.DefinitionVersion, Is.EqualTo(original.ContentHash));
                    Finish(driver); AssertHit(arena, running, original);
                    Setup(arena, driver, "base_sword"); var next = Start(arena, 1); Finish(driver); AssertHit(arena, next, arena.BasicSwordSecond);
                    Assert.That(File.ReadAllBytes(source), Is.EqualTo(before));
                    Select(selector, "rapier_basic_0"); root.Q<FloatField>("active").value = .07f;
                    foreach (string id in new[] { "rapier_basic_0", "rapier_basic_1", "rapier_basic_2" }) Assert.That(root.Q<Label>("sharedActive").text, Does.Contain(id));
                    Select(selector, "rapier_basic_2"); Assert.That(root.Q<FloatField>("active").value, Is.EqualTo(.07f));
                    root.Q<FloatField>("duration").value = 0; Select(selector, "base_sword_basic_1");
                    Assert.That(root.Q<Button>("save").enabledSelf, Is.False); Assert.That(root.Q<Button>("applyReset").enabledSelf, Is.False);
                    Select(selector, "rapier_basic_2"); root.Q<FloatField>("duration").value = .36f;
                    selector.value = selector.choices[0]; Assert.That(root.Q<Button>("save").enabledSelf, Is.False); Assert.That(root.Q<Button>("firstSword").enabledSelf, Is.True);
                    Select(selector, "base_sword_basic_1"); Assert.That(root.Q<FloatField>("damage").value, Is.EqualTo(22));
                    root.Q<TextField>("python").value = Path.Combine(repository, "missing-python-executable"); Click(window, "save");
                    Assert.That(root.Q<Label>("status").text, Does.Contain("원본 저장 완료, 생성 실패"));
                    Assert.That(BasicMeleeSource.Load(repository, out _).Single(hit => hit.formId == "base_sword").hit.damage, Is.EqualTo(22));
                    root.Q<TextField>("python").value = "python"; Click(window, "save");
                    Assert.That(root.Q<Label>("status").text, Does.Contain("재생성 완료"));
                    Assert.That(BasicMeleeSourceTests.Generated(repository).forms.Single(form => form.id == "base_sword").basics[1].damage, Is.EqualTo(22));
                    root.Q<FloatField>("damage").value = 50; Click(window, "reload"); Assert.That(root.Q<FloatField>("damage").value, Is.EqualTo(22));
                    Setup(arena, driver, "base_sword"); Start(arena, 1); Click(window, "applyReset"); Assert.That(arena.Player.currentCast, Is.Null);
                }
            }
            finally { window.Close(); Object.Destroy(floor); }
        }

        [UnityTest] public IEnumerator GeneratedSaveIsConsumedByNewGameStartup() => GeneratedStartup("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator GeneratedSaveIsConsumedByNewLabStartup() => GeneratedStartup("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator GeneratedStartup(string scene)
        {
            var hits = BasicMeleeSource.Load(repository, out var hash); var second = hits.Single(hit => hit.formId == "base_sword");
            second.hit.damage = 27; second.hit.duration = .71f; second.hit.impact = .29f;
            var dagger = hits.Single(hit => hit.formId == "dagger" && hit.hitIndex == 3); dagger.hit.damage = 18;
            BasicMeleeSource.Save(repository, hash, hits); BasicSwordSource.Regenerate(repository);
            var expected = BasicMeleeSource.Compile(hits);
            string liveAsset = Path.Combine(Application.dataPath, "Deviant/Combat/Resources/MeleeCatalog.json"); byte[] baseline = File.ReadAllBytes(liveAsset);
            try
            {
                File.Copy(Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json"), liveAsset, true);
                AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/MeleeCatalog.json", ImportAssetOptions.ForceSynchronousImport);
                LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
                yield return new EnterPlayMode(); yield return null;
                var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = Floor();
                try
                {
                    foreach (var definition in expected)
                        Assert.That(arena.GetBasicMelee(hits.Single(hit => hit.Id == definition.Id).formId, hits.Single(hit => hit.Id == definition.Id).hitIndex).ContentHash, Is.EqualTo(definition.ContentHash));
                    using (var driver = arena.AcquireManualSimulation())
                        foreach (var hit in new[] { second, dagger })
                        {
                            Setup(arena, driver, hit.formId); var cast = Start(arena, hit.hitIndex); Finish(driver);
                            AssertHit(arena, cast, expected.Single(definition => definition.Id == hit.Id));
                        }
                }
                finally { Object.Destroy(floor); }
                yield return new ExitPlayMode();
            }
            finally
            {
                File.WriteAllBytes(liveAsset, baseline);
                AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/MeleeCatalog.json", ImportAssetOptions.ForceSynchronousImport);
            }
        }
        static void Select(DropdownField selector, string id) => selector.value = selector.choices.Single(choice => choice.EndsWith("— " + id, StringComparison.Ordinal));
        static void Click(EditorWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
