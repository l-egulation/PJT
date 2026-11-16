using System.Collections;
using System.Linq;
using Deviant.Combat;
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
    // The chapter 1 starting room's signpost (user request 2026-11-10): on a run planned in the village (blueprints already
    // chosen), F beside it opens a window that goes through the road's moves one at a time; ←/→ turn the pages, F closes it
    // and the world waits meanwhile. Captures: outputs/approved-hub/captures/chapter1-controls-window-*.png.
    public sealed class Chapter1ControlsSignTests
    {
        const System.Reflection.BindingFlags Hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Keyboard keyboard;

        static T Get<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetProperty(name).GetValue(gui);

        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }

        static ForestCombatWorld Bound(RunSession run)
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var binding = world ? world.GetComponent<Chapter1RunBinding>() : null;
            return world && world.Ready && binding && binding.Run == run ? world : null;
        }

        static void Capture(MonoBehaviour gui, Camera camera, string name)
        {
            // The capture draws the GUI in camera space, where the chapter's paper layers sort above it: lift it as the
            // overlay draws it in play.
            // and at 1:1 (the batch-mode screen is smaller than the 960x540 picture, which shrank the GUI).
            var canvas = gui.GetComponent<Canvas>(); int order = canvas.sortingOrder; canvas.sortingOrder = 32000;
            var scaler = gui.GetComponent<UnityEngine.UI.CanvasScaler>(); float factor = scaler.scaleFactor;
            scaler.scaleFactor = 1; canvas.scaleFactor = 1; Canvas.ForceUpdateCanvases();
            StartingVillageFrontTests.CaptureWithGui(gui, camera, name);
            canvas.sortingOrder = order; scaler.scaleFactor = factor; canvas.scaleFactor = factor;
        }

        [UnityTest] public IEnumerator FAtTheStartingRoomSignOpensAWindowOfTheMovesOneAtATime()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            try
            {
                for (int frame = 0; frame < 600 && !(Object.FindAnyObjectByType<ForestCombatWorld>()?.Ready ?? false); frame++) yield return null;
                // A run as the village starts it: bare-handed, no blueprint is chosen (run-structure.md 런 시작 장비).
                Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                var host = Chapter1RunHost.Ensure();
                host.StartNewRun(Chapter1ProfileHost.Service.PlanUnarmedRun());
                var run = host.Flow.Current; ForestCombatWorld world = null;
                for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < 20 && !(world = Bound(run));) yield return null;
                Assert.That(world, Is.Not.Null, "the planned run loads chapter 1");
                // The run binding applies the departure on its own Update, a frame after the world is ready.
                var offers = world.GetComponent<Chapter1StartingBlueprints>();
                for (int frame = 0; frame < 60 && !offers.Complete; frame++) yield return null;
                Assert.That(offers.Preselected && offers.Complete && offers.SelectedCount == 0, Is.True, "the village departure is bare-handed");
                Chapter1ControlsSign sign = null;
                for (int i = 0; i < 600 && !(sign && sign.Placed); i++) { yield return null; sign = world.GetComponent<Chapter1ControlsSign>(); }
                Assert.That(sign && sign.Placed, Is.True, "the starting room has the signpost");
                Assert.That(sign.SystemPosition.x-sign.Position.x,Is.GreaterThanOrEqualTo(Chapter1ControlsSign.SignSpacing));
                Assert.That(sign.LabelText.text,Is.EqualTo("조작 튜토리얼"));
                Assert.That(sign.SystemLabelText.text,Is.EqualTo("시스템 튜토리얼"));
                var player = world.arena.Player; var room = world.Data.rooms[0];
                Assert.That(room.kind, Is.EqualTo("start"));
                Assert.That(sign.Position.x, Is.InRange(room.x, room.x + room.w), "inside the starting room");
                Assert.That(Mathf.Abs(sign.Position.x - player.spawn.x), Is.LessThan(8), "near where the run starts");
                var art = sign.transform.Find("Controls sign").GetComponent<SpriteRenderer>();
                Assert.That(art.sprite, Is.SameAs(Resources.Load<Sprite>(Chapter1ControlsSign.Art)));

                // The first run's departure card and combat notebook come first.
                MonoBehaviour gui = null;
                for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < 10 && !(gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null));) yield return null;
                Assert.That(gui, Is.Not.Null);
                for (int i = 0; i < 60 && !Get<bool>(gui, "DepartureCardLocked"); i++) yield return null;
                if (Get<bool>(gui, "DepartureCardLocked")) gui.GetType().GetMethod("DismissDepartureCard").Invoke(gui, null);
                var guide = world.GetComponent<Chapter1FirstRunInkGuide>();
                for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < 6 && guide && !guide.NotebookOpen;) yield return null;
                if (guide && guide.NotebookOpen) guide.FinishNotebook();
                if (guide) guide.enabled = false;
                // The first run's automatic attack guide (first-run-guide.md 첫 런 자동 안내) also holds the world.
                SketchRunBinding sketchBinding = null;
                for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < 6 && !(sketchBinding = world.GetComponent<SketchRunBinding>());) yield return null;
                if (sketchBinding && sketchBinding.FirstRunTutorialShowing) sketchBinding.GetComponentInChildren<Deviant.Tutorial.Unity.FirstRunTutorialWindow>(true).Dismiss();
                foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.Position += Vector2.right * 1000; }

                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                PlayerControls.For(player).Source.SetFocused(true);
                keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();

                player.CancelAction(); player.Position = sign.Position + Vector2.up * .05f; player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                for (int i = 0; i < 30 && !(player.grounded && sign.Key.Visible); i++) yield return null;
                Assert.That(sign.Key.Visible, Is.True, "an F keycap over the sign");
                Capture(gui, world.arena.ViewCamera, "chapter1-controls-sign");
                yield return Press(Key.F);
                Assert.That(Get<bool>(gui, "ControlsWindowOpen"), Is.True, "F at the sign opens the window");
                Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True, "the world waits");
                Assert.That(Get<int>(gui, "ControlsWindowPages"), Is.EqualTo(7), "only the seven movement demonstrations");
                var footer = System.Array.Find(gui.GetComponentsInChildren<UnityEngine.UI.Text>(), t => t.text == "1 / 7");
                Assert.That(footer, Is.Not.Null, "the shared tutorial footer shows all pages");
                string[] titles = { "달리기", "점프", "더블 점프", "대시", "대시로 피하기", "벽타기", "벽차기" };
                for (int page = 0; page < titles.Length; page++)
                {
                    Assert.That(Get<int>(gui, "ControlsWindowPage"), Is.EqualTo(page));
                    Assert.That(Get<string>(gui, "ControlsWindowTitle"), Is.EqualTo(titles[page]));
                    for (int i = 0; i < 40; i++) yield return null;
                    Assert.That(Get<Sprite>(gui, "ControlsWindowPicture"), Is.Not.Null, titles[page] + " plays its move");
                    if (titles[page] == "벽타기")
                    {
                        for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < 3 && !Get<bool>(gui, "ControlsWindowRing");) yield return null;
                        Assert.That(Get<bool>(gui, "ControlsWindowRing"), Is.True, "the stamina ring over the climbing hero");
                    }
                    Capture(gui, world.arena.ViewCamera, "chapter1-controls-window-" + (page + 1));
                    yield return Press(Key.RightArrow);
                }
                Assert.That(Get<int>(gui, "ControlsWindowPage"), Is.EqualTo(6), "the last movement page is bounded");
                yield return Press(Key.LeftArrow);
                Assert.That(Get<int>(gui, "ControlsWindowPage"), Is.EqualTo(5), "left returns to the wall-climb demonstration");
                yield return Press(Key.F);
                Assert.That(Get<bool>(gui, "ControlsWindowOpen"), Is.False, "F closes it");
                Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False, "the world goes on");
                player.CancelAction();player.Position=sign.SystemPosition+Vector2.up*.05f;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
                for(int i=0;i<30&&!sign.SystemKey.Visible;i++)yield return null;
                Assert.That(sign.SystemKey.Visible,Is.True);
                yield return Press(Key.F);
                var system=(Deviant.Tutorial.Unity.FirstRunTutorialWindow)gui.GetType().GetField("systemTutorialReplay",Hidden).GetValue(gui);
                Assert.That(system && system.IsOpen,Is.True,"F on the second sign opens system replay");
                Assert.That(system.PageCount,Is.EqualTo(6));
                yield return new WaitForSecondsRealtime(.4f);
                yield return Press(Key.F);
                Assert.That(system.IsOpen,Is.False);Assert.That(world.arena.Controls.Current.SimulationPaused,Is.False);

            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
            yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }
    }
}
