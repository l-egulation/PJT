using System.Collections;
using System.IO;
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
    // Review frames of the opening's cutscenes: the bars coming in as the bystanders' talk starts, and the erasure
    // swallowing the two bystanders, frame by frame. Captures go to outputs/hub/capture/cutscene-*/NN.png.
    public sealed class StartingVillageCutsceneCaptureTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        Keyboard keyboard;

        static string Bubble(MonoBehaviour gui) => (string)gui.GetType().GetProperty("BubbleShowing").GetValue(gui);
        static string Lesson(MonoBehaviour gui) => (string)gui.GetType().GetProperty("LessonShowing").GetValue(gui);
        static float Bars(MonoBehaviour gui) => (float)gui.GetType().GetField("sceneBars", Private).GetValue(gui);

        IEnumerator Press(Key key, float seconds)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < seconds;) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            for (float at = Time.realtimeSinceStartup; !done() && Time.realtimeSinceStartup - at < seconds;) yield return null;
        }

        static bool Notebook(MonoBehaviour gui) => (bool)gui.GetType().GetProperty("NotebookShowing").GetValue(gui);

        // Closes a guide ghost once it has shown its move (Space).
        IEnumerator CloseGhost(MonoBehaviour gui, StartingVillageWorld village)
        {
            yield return WaitFor(() => village.GhostShownOnce, 6);
            yield return WaitFor(() => false, .9f); yield return Press(Key.Space, .05f);
        }

        // Turns the notebook, closes every guide ghost and reads every line until the given line is up.
        IEnumerator Until(MonoBehaviour gui, StartingVillageWorld village, string line, float seconds)
        {
            for (float at = Time.realtimeSinceStartup; Bubble(gui) != line && Time.realtimeSinceStartup - at < seconds;)
            {
                if (Notebook(gui)) { yield return WaitFor(() => false, .5f); yield return Press(Key.Space, .05f); continue; }
                if (Lesson(gui) != null) { yield return CloseGhost(gui, village); continue; }
                string shown = Bubble(gui);
                if (shown != null) { yield return WaitFor(() => false, shown.Length / 30f + .3f); if (Bubble(gui) == shown && shown != line) yield return Press(Key.F, .05f); continue; }
                yield return null;
            }
            Assert.That(Bubble(gui), Is.EqualTo(line));
        }

        // Frames every `step` seconds for `seconds`.
        static IEnumerator Frames(MonoBehaviour gui, StartingVillageWorld village, string folder, float seconds, float step)
        {
            Directory.CreateDirectory("../outputs/hub/capture/" + folder);
            float start = Time.realtimeSinceStartup, next = start;
            for (int n = 0; Time.realtimeSinceStartup - start < seconds;)
            {
                if (Time.realtimeSinceStartup >= next) { StartingVillageCaptureTests.Capture(gui, village, $"{folder}/{n++:00}"); next += step; }
                yield return null;
            }
        }

        [UnityTest] public IEnumerator CaptureTheBystandersSwallowedForReview()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                StartingVillageWorld village = null;
                for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(village && village.Ready, Is.True);
                yield return null; yield return null;
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                gui.GetType().GetField("storyChecked", Private).SetValue(gui, false);
                yield return null; yield return null;
                Assert.That((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), Is.True);
                var player = village.World.arena.Player;
                yield return Until(gui, village, "저 마을이구나.", 12);
                yield return WaitFor(() => false, 1); yield return Press(Key.F, .05f);
                foreach (var line in new[] { "아직은 멀쩡해 보이는데.", "해 지기 전엔 닿겠다." })
                { yield return Until(gui, village, line, 4); yield return WaitFor(() => false, 1); yield return Press(Key.F, .05f); }
                // The road has no lessons now (2026-11-10): the strip of moves goes up and the road is the player's.
                yield return WaitFor(() => (bool)gui.GetType().GetProperty("ControlsShowing").GetValue(gui) && player.grounded, 2);
                Assert.That(Bars(gui), Is.LessThan(1), "no bars while the road is the player's");

                // The bars come in as the hero walks up to the bystanders.
                player.Position = new Vector2(-98.5f, player.Position.y); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                for (int i = 0; i < 4; i++) { yield return WaitFor(() => Lesson(gui) != null, .5f); if (Lesson(gui) == null) break; yield return CloseGhost(gui, village); }
                yield return Frames(gui, village, "cutscene-bars-in", .8f, .1f);
                yield return WaitFor(() => Bars(gui) > 55, 1);
                Assert.That(Bars(gui), Is.GreaterThan(55), "the bars are in by the bystanders' talk");
                yield return Until(gui, village, "나 들으라고 하는 소리 같은데... 서두르자.", 20);
                yield return WaitFor(() => false, 1.2f); yield return Press(Key.F, .05f);
                yield return Frames(gui, village, "cutscene-bars-out", .8f, .1f);
                yield return WaitFor(() => Bars(gui) < 5, 1);
                Assert.That(Bars(gui), Is.LessThan(5), "the bars leave with the controls");

                // The erasure swallows the bystanders.
                player.Position = new Vector2(-82.5f, player.Position.y); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                yield return Until(gui, village, "어.. 색이..!", 8);
                yield return WaitFor(() => false, .6f); yield return Press(Key.F, .05f);
                yield return Frames(gui, village, "cutscene-swallow", 1.5f, .15f);
                // Freshly erased cells let go of dust that drifts on with the front.
                yield return WaitFor(() => village.FrontFlakesAlive > 0, 4);
                Assert.That(village.FrontFlakesAlive, Is.GreaterThan(0), "dust leaves the erased cells");
                yield return Frames(gui, village, "cutscene-flakes", 1.2f, .1f);
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
