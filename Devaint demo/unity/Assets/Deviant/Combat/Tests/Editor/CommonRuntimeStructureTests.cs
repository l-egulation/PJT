using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CommonRuntimeStructureTests
    {
        [Test] public void RetiredCombatTypesAreNotCompiled()
        {
            foreach(string name in new[] { "Deviant.World.InkPlayerController", "VillageCombatant", "Deviant.World.InkHeroCombat", "Deviant.World.InkMonsterController", "Deviant.Weapons.ConfirmedWeaponTest" })
                Assert.That(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType(name)!=null), Is.False, name);
        }
        [Test] public void ExistingScenePathsOnlyLaunchCommonGame()
        {
            foreach(string path in new[] {
                "Assets/Deviant/World/Scenes/TwilightInkMotionTest.unity",
                "Assets/Deviant/World/Scenes/TwilightInkMonsterTest.unity",
                "Assets/Deviant/World/Scenes/TwilightInkHUDTest.unity",
                "Assets/Deviant/World/Scenes/TwilightInkHUDLayoutTest.unity",
                "Assets/Deviant/Weapons/Scenes/FourWeaponsForestTest.unity" })
            {
                var scene=LegacySceneFixture.Open(path);
                var behaviours=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                Assert.That(behaviours.Length, Is.EqualTo(1), path);
                Assert.That(behaviours[0], Is.Not.Null, "Missing script in "+path);
                Assert.That(behaviours[0].GetType().Name, Is.EqualTo("GameEntry"));
            }
        }
        [Test] public void MovedPrefabsAndDataKeepTheirScriptTypes()
        {
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Deviant" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(b => b != null), Is.True, path);
            }
            foreach (var expected in new[] {
                new[] { "Assets/Deviant/World/Data/ForestIntegration.asset", "Deviant.World.InkForestData" },
                new[] { "Assets/Deviant/World/Monsters/Data/eye.asset", "Deviant.World.InkMonsterData" },
                new[] { "Assets/Deviant/World/Monsters/Data/boar.asset", "Deviant.World.InkMonsterData" },
                new[] { "Assets/Deviant/Weapons/Data/ConfirmedWeapons.asset", "Deviant.Weapons.ConfirmedWeaponLibrary" } })
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(expected[0]);
                Assert.That(data, Is.Not.Null, expected[0]);
                Assert.That(data.GetType().FullName, Is.EqualTo(expected[1]), expected[0]);
            }
        }
        [Test] public void CommonSceneHasNoMissingScriptsAndOneSimulation()
        {
            var scene=LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            var behaviours=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Assert.That(behaviours.All(b=>b!=null), Is.True);
            Assert.That(behaviours.Count(b=>b is CombatArena), Is.EqualTo(1));
            Assert.That(behaviours.Count(b=>b.GetType().Name=="GameComposition"), Is.EqualTo(1));
        }
        [UnityTearDown] public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator UnconfiguredArenaDoesNotConstructAWorldOrPlayer()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("UnconfiguredCombat").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null; yield return new WaitForFixedUpdate();
            var arena = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(arena.enabled, Is.True);
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Unconfigured));
            Assert.That(arena.Player, Is.Null);
            Assert.That(arena.enemies, Is.Empty); Assert.That(arena.pits, Is.Empty);
            Assert.That(Object.FindObjectsByType<CombatActor>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest] public IEnumerator ExplicitInitializationUsesOnlyTheSuppliedEnvironmentAndActorRequests()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("ExplicitCombat").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var camera = new GameObject("SuppliedCamera").AddComponent<Camera>();
            var content = CombatContentLoader.Load(null, false);
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Profile = CombatCompositionProfile.Game; content.Camera = camera;
            content.PlayerSpawn = new Vector2(100, 1000); content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSortingOrder = 35; content.SecondaryFormIndex = 1;
            var enemySpawn = new Vector2(110, 1000);
            content.Enemies = new[] { new CombatEnemySpawn("RequestedEnemy", 1, enemySpawn, new Vector2(1.3f, 1.2f), new Vector2(.45f, 1.25f), 60, false, false, 34) };
            var order = new List<string>();
            content.PrepareEnvironment = owner => {
                Assert.That(owner, Is.SameAs(arena)); Assert.That(owner.Player, Is.Null);
                var floor = new GameObject("SuppliedFloor"); floor.transform.position = new Vector2(105, 999.5f);
                floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(30, 1);
                order.Add("environment");
            };
            content.BindPlayerInput = player => {
                Assert.That(player, Is.SameAs(arena.Player)); Assert.That(player.Position, Is.EqualTo(content.PlayerSpawn));
                Assert.That(arena.enemies, Is.Empty);
                player.gameObject.AddComponent<CombatKeyboardInput>().Player = player;
                order.Add("input");
            };
            Assert.That(arena.Initialize(content), Is.True);
            Assert.That(order, Is.EqualTo(new[] { "environment", "input" }));
            Assert.That(arena.SimulationTickCount, Is.Zero);
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Game));
            Assert.That(arena.ViewCamera, Is.SameAs(camera));
            Assert.That(arena.Player.Position, Is.EqualTo(content.PlayerSpawn));
            Assert.That(arena.Player.visual.sortingOrder, Is.EqualTo(35));
            Assert.That(arena.Player.GetComponent<CombatKeyboardInput>().Player, Is.SameAs(arena.Player));
            Assert.That(arena.enemies.Count, Is.EqualTo(1));
            var enemy = arena.enemies[0];
            Assert.That(enemy.Position, Is.EqualTo(enemySpawn)); Assert.That(enemy.spawn, Is.EqualTo(enemySpawn));
            Assert.That(enemy.profile, Is.EqualTo(1)); Assert.That(enemy.health, Is.EqualTo(60));
            Assert.That(enemy.visual.sortingOrder, Is.EqualTo(34)); Assert.That(enemy.aggressive, Is.False);
            Assert.That(Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(arena.pits, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => arena.Initialize(content));
            Assert.That(order.Count, Is.EqualTo(2)); Assert.That(arena.enemies.Count, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator InvalidContentCannotCreateActorsOrInvokeSceneConstruction()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("InvalidCombat").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var content = CombatContentLoader.Load(null, false);
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Profile = CombatCompositionProfile.Game; content.Camera = new GameObject("SuppliedCamera").AddComponent<Camera>();
            content.Locomotion = null; content.Enemies = Array.Empty<CombatEnemySpawn>();
            int callbacks = 0; content.PrepareEnvironment = _ => callbacks++; content.BindPlayerInput = _ => callbacks++;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".+"));
            Assert.That(arena.Initialize(content), Is.False);
            Assert.That(arena.enabled, Is.False); Assert.That(arena.Player, Is.Null);
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Unconfigured));
            Assert.That(callbacks, Is.Zero); Assert.That(arena.enemies, Is.Empty);
            Assert.That(Object.FindObjectsByType<CombatActor>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest] public IEnumerator LabRootConstructsItsSandboxAndKeepsTheSharedCombatAndInput()
        {
            var scene = LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).All(behaviour => behaviour != null), Is.True);
            var configured = Object.FindAnyObjectByType<CombatArena>(); configured.loadCharacterArt = false; configured.enabled = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Lab));
            Assert.That(arena.SimulationTickCount, Is.Zero);
            Assert.That(arena.Player.Position, Is.EqualTo(new Vector2(-4, 0)));
            Assert.That(arena.Player.GetComponent<CombatKeyboardInput>().Player, Is.SameAs(arena.Player));
            Assert.That(arena.enemies.Select(enemy => enemy.spawn), Is.EqualTo(new[] { new Vector2(3, 0), new Vector2(9, 0), new Vector2(31, 0) }));
            Assert.That(arena.enemies.All(enemy => !enemy.aggressive), Is.True);
            var surfaces = Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None);
            Assert.That(surfaces.Length, Is.EqualTo(7)); Assert.That(surfaces.Count(surface => surface.oneWay), Is.EqualTo(1));
            var wall = surfaces.Single(surface => surface.name == "Wall");
            Assert.That(wall.transform.position, Is.EqualTo(new Vector3(13, 1.5f, 0)));
            Assert.That(wall.GetComponent<BoxCollider2D>().size, Is.EqualTo(new Vector2(.4f, 3)));
            Assert.That(arena.pits.Count, Is.EqualTo(1)); Assert.That(arena.pits[0].GetComponent<ArenaPit>(), Is.Not.Null);
            Assert.That(arena.pits[0].size, Is.EqualTo(new Vector2(70, 3)));
            Assert.That(arena.ViewCamera.orthographicSize, Is.EqualTo(6));
            arena.enabled = true;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Step(.02f, new Deviant.Combat.Contracts.CombatInput(1));
                Assert.That(arena.SimulationTickCount, Is.EqualTo(1)); Assert.That(arena.Player.Position.x, Is.GreaterThan(-4));
                driver.Reset(); Assert.That(arena.Player.Position, Is.EqualTo(new Vector2(-4, 0)));
                Assert.That(arena.enemies.Select(enemy => enemy.Position), Is.EqualTo(new[] { new Vector2(3, 0), new Vector2(9, 0), new Vector2(31, 0) }));
            }
        }

        [UnityTest] public IEnumerator RemovingTheLabRootDoesNotInstallAnAutomaticSandboxFallback()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            var configured = Object.FindAnyObjectByType<CombatArena>();
            Object.DestroyImmediate(configured.GetComponent("LabComposition"));
            yield return new EnterPlayMode(); yield return null; yield return new WaitForFixedUpdate();
            var arena = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Unconfigured));
            Assert.That(arena.Player, Is.Null); Assert.That(arena.enemies, Is.Empty); Assert.That(arena.pits, Is.Empty);
            Assert.That(Object.FindObjectsByType<CombatActor>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest] public IEnumerator GameTechnicalKeysUseTheExplicitDiagnosticsAndRespectCombatControl() => ExerciseDiagnostics("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabTechnicalKeysUseTheExplicitDiagnosticsAndRespectCombatControl() => ExerciseDiagnostics("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static IEnumerator ExerciseDiagnostics(string path)
        {
            LegacySceneFixture.Open(path); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var diagnostics = (MonoBehaviour)arena.GetComponent("CombatDiagnostics");
            var panel = diagnostics.GetType().GetField("showPanel");
            bool initialPanel = (bool)panel.GetValue(diagnostics), initialBoxes = arena.showBoxes;
            var settings = InputSystem.settings; var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard = null;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                void Poll(params Key[] keys)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); keyboard.MakeCurrent();
                    diagnostics.SendMessage("Update");
                }
                Poll(Key.F1, Key.F2);
                Assert.That((bool)panel.GetValue(diagnostics), Is.EqualTo(!initialPanel)); Assert.That(arena.showBoxes, Is.EqualTo(!initialBoxes));
                using (var driver = arena.AcquireManualSimulation())
                {
                    Poll(); Poll(Key.F1, Key.F2);
                    Assert.That((bool)panel.GetValue(diagnostics), Is.EqualTo(!initialPanel)); Assert.That(arena.showBoxes, Is.EqualTo(!initialBoxes));
                }
                using (var scope = arena.Controls.OpenScope("diagnostics block")) using (scope.BlockInput(Deviant.Control.Contracts.InputChannels.Combat))
                {
                    Poll(); Poll(Key.F1, Key.F2);
                    Assert.That((bool)panel.GetValue(diagnostics), Is.EqualTo(!initialPanel)); Assert.That(arena.showBoxes, Is.EqualTo(!initialBoxes));
                }
                using (var scope = arena.Controls.OpenScope("diagnostics pause")) using (scope.PauseSimulation())
                {
                    Poll(); Poll(Key.F1, Key.F2);
                    Assert.That((bool)panel.GetValue(diagnostics), Is.EqualTo(!initialPanel)); Assert.That(arena.showBoxes, Is.EqualTo(!initialBoxes));
                }
                Poll(); Poll(Key.F1, Key.F2);
                Assert.That((bool)panel.GetValue(diagnostics), Is.EqualTo(initialPanel)); Assert.That(arena.showBoxes, Is.EqualTo(initialBoxes));
            }
            finally
            {
                panel.SetValue(diagnostics, initialPanel); arena.showBoxes = initialBoxes;
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }
    }
}
