using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
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
    // Plays the first visit with real key presses: hold Esc to skip the playable opening, then the player must stand at
    // the well and walk at once (no hidden screen or opening lock left holding the lobby).
    public sealed class StartingVillageFirstVisitInputTests
    {
        [UnityTest] public IEnumerator AfterTheOpeningThePlayerWalksImmediately()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                StartingVillageWorld village = null;
                for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(village && village.Ready, Is.True);
                yield return null; yield return null;
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                // The GUI may have checked the real editor profile before the memory store was installed: check again.
                gui.GetType().GetField("storyChecked", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gui, false);
                yield return null; yield return null;
                var opening = gui.GetType().GetField("openingActive", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That((bool)opening.GetValue(gui), Is.True, "the first visit walks into the village first");
                // Holding Esc skips the opening to the awakening at the well; no still scene follows.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                for (float held = Time.realtimeSinceStartup; Time.realtimeSinceStartup - held < 1.2f;) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
                Assert.That((bool)opening.GetValue(gui), Is.False, "holding Esc ends the playable opening");
                Assert.That((bool)gui.GetType().GetField("storyActive", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gui), Is.False, "the still scenes are not shown");
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("opening"), Is.True, "a skipped opening counts as seen");
                Assert.That(Mathf.Abs(village.World.arena.Player.Position.x - village.Find(StartingVillageWorld.ReturnWell).Position.x), Is.LessThan(.01f), "control starts at the well");
                yield return null;
                var player = village.World.arena.Player; float startX = player.Position.x;
                var controls = village.World.arena.Controls.Current;
                string hubOpen = (gui.GetType().GetField("hubOpen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gui) as GameObject)?.name;
                bool paused = (bool)gui.GetType().GetField("hubPaused", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gui);
                Assert.That(hubOpen, Is.Null, "no village screen is left open");
                Assert.That(paused, Is.False);
                Assert.That(controls.SimulationPaused, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                float heldAt = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - heldAt < .6f) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(player.Position.x - startX, Is.GreaterThan(.5f), "D walks right right after the opening");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                Chapter1ProfileHost.UseStore(null);
            }
            yield return new ExitPlayMode();
        }
    }
}
