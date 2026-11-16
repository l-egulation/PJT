using System.Collections;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The play GUI is the only chapter inventory UI, with development tools gated independently.
    // EnterPlayMode reloads the domain, so the player-build flag is forced only after entering and before the run loads.
    public sealed class Chapter1PlayerBuildGateTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            DevFeatures.ForceForTests(null);
            Chapter1ProfileHost.UseStore(null);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        static ForestCombatWorld ReadyWorld()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            return world && world.Ready && world.GetComponent<Chapter1RunBinding>() &&
                world.arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>() ? world : null;
        }

        static IEnumerator LoadRun(Chapter1RunHost host)
        {
            host.StartNewRun();
            yield return null;
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            // The GUI binding installs one frame after the inventory host.
            yield return null; yield return null;
        }

        [UnityTest] public IEnumerator PlayGuiRemainsActiveWithAndWithoutDevFeatures()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            yield return null;
            var host = Chapter1RunHost.Instance;
            Assert.That(host, Is.Not.Null);

            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            DevFeatures.ForceForTests(true);
            // Editor/development configuration also uses only the play GUI.
            yield return LoadRun(host);
            var world = ReadyWorld();
            Assert.That(world, Is.Not.Null);
            CheckPlayGui(world);
            Assert.That(world.arena.Player.GetComponent<CombatKeyboardInput>().ResetKeyEnabled, Is.True, "the editor keeps the F5 test reset");

            DevFeatures.ForceForTests(false);
            yield return LoadRun(host);
            world = ReadyWorld();
            Assert.That(world, Is.Not.Null, "a player build still starts the run");
            CheckPlayGui(world);
            Assert.That(world.arena.Player.GetComponent<CombatKeyboardInput>().ResetKeyEnabled, Is.False, "no F5 reset in player builds");
            Assert.That(world.diagnostics.showDebugUI, Is.False, "debug HUD and F1 panel stay hidden");
            CheckPortalKeysWork(world);
            yield return new ExitPlayMode();
        }

        static void CheckPlayGui(ForestCombatWorld world)
        {
            var binding = world.arena.GetComponent("InventoryGameBinding");
            Assert.That(binding, Is.Not.Null);
            var views = binding.GetComponentsInChildren<Deviant.Inventory.Unity.InventoryPanelView>(true);
            Assert.That(views, Has.Length.EqualTo(1), "no alternate work inventory is installed");
            Assert.That(views[0].GetType().Name, Is.EqualTo("TemporaryGameGui"));
            Assert.That(views[0].gameObject.activeInHierarchy, Is.True, "the chapter play GUI stays active");
            Assert.That(GameObject.Find("GUI mode switch"), Is.Null, "no mode switch in any build configuration");
            Assert.That(world.ExternalPresentation, Is.True, "legacy presentation stays suppressed");
        }

        static void CheckPortalKeysWork(ForestCombatWorld world)
        {
            foreach (var enemy in world.arena.enemies) if (enemy) enemy.aggressive = false;
            var portal = world.GetComponent<Chapter1ManualCheckpoint>();
            var p = world.arena.Player;
            var bindings = CombatKeyboardBindings.For(p);
            // The workshop artisan has taught the portal (C/V are off in a run before that).
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            Chapter1ProfileHost.Service.MarkStorySeen(Chapter1ManualCheckpoint.PortalTaughtStory);
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                using (var driver = world.arena.AcquireManualSimulation())
                {
                    driver.Reset(); for (int i = 0; i < 120 && !p.grounded; i++) driver.Step(1f / 120);
                    Assert.That(p.grounded, Is.True);
                    void PressKeys(params Key[] keys)
                    { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); portal.HandleKeyboard(); }
                    PressKeys(); PressKeys(bindings.CheckpointInstall); PressKeys();
                    Assert.That(p.CheckpointBusy, Is.True, "C installs the portal in player builds too");
                    Assert.That(p.PortalPhase, Is.EqualTo(CheckpointPhase.Installing));
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }
    }
}
