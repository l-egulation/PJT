using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Control;
using Deviant.Control.Contracts;
using NUnit.Framework;
using Deviant.UI.Presentation;
using Deviant.UI.Unity;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ControlBindingTests
    {
        [UnityTest] public IEnumerator GameUsesSharedOwnerRequests() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", true);
        [UnityTest] public IEnumerator LabUsesSharedOwnerRequests() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", false);
        [UnityTest] public IEnumerator HudCloseDisableAndRebindPreserveOtherOwners()
        {
            var enter = Open("Assets/Deviant/Game/Scenes/deviant.unity");
            while (enter.MoveNext()) yield return enter.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var hud = Hud(arena);
            Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(hud.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            foreach (string field in new[] { "pauseButton", "escapeButton", "bagButton", "closeButton" })
            {
                var button = Button(hud, field);
                Assert.That(button.interactable, Is.True);
                Assert.That(button.targetGraphic.raycastTarget, Is.True);
            }
            using (var dialogue = arena.Controls.OpenScope("test dialogue"))
            {
                // Engine baseline is fixed at binding; independent time owners use leases.
                ((IFeedbackTimeScope)dialogue).RequestSlow(.35f,100);
                var input = dialogue.BlockInput(InputChannels.Combat | InputChannels.Interaction);
                Click(hud, "pauseButton"); Assert.That(Time.timeScale, Is.Zero);
                Click(hud, "closeButton"); Assert.That(Time.timeScale, Is.EqualTo(.35f));
                Assert.That(arena.Player.InputBlocked, Is.True); Assert.That(input.IsActive, Is.True);
                var pause = dialogue.PauseSimulation(); Click(hud, "escapeButton"); Click(hud, "closeButton");
                Assert.That(Time.timeScale, Is.Zero, "The external owner still pauses.");
                hud.enabled = false;
                Assert.That(arena.Player.InputBlocked, Is.True); Assert.That(Time.timeScale, Is.Zero);
                pause.Dispose(); Assert.That(Time.timeScale, Is.EqualTo(.35f));
                hud.enabled = true; Click(hud, "bagButton"); Assert.That(Time.timeScale, Is.EqualTo(.35f));
                Assert.That(arena.Player.InputBlocked, Is.True);
                // Rebinding removes old HUD requests but never external controls.
                var events = EventSystem.current;
                var back = events.GetComponent<InputSystemUIInputModule>().cancel.action;
                var originalRouter = (ScreenRouter)typeof(ScreenInputBinding).GetField("router", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(hud.GetComponent<ScreenInputBinding>());
                using (var replacement = new ControlSession())
                using (var replacementRouter = new ScreenRouter(replacement))
                {
                    hud.GetType().GetMethod("BindControls").Invoke(hud, new object[] { replacement, replacementRouter, back, events });
                    Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Combat | InputChannels.Interaction));
                    hud.GetType().GetMethod("UnbindControls").Invoke(hud, new object[] { arena.Controls, originalRouter });
                    hud.SendMessage("OpenBag"); Assert.That(replacement.Current.Blocks(InputChannels.Combat), Is.True, "Stale unbind cannot close the new binding.");
                    hud.enabled = false; Assert.That(replacement.Current.Blocked, Is.EqualTo(InputChannels.None));
                    hud.SendMessage("TogglePause"); Assert.That(replacement.Current.SimulationPaused, Is.False, "Disabled views ignore late input.");
                    hud.GetType().GetMethod("BindControls").Invoke(hud, new object[] { arena.Controls, originalRouter, back, events });
                }
                input.Dispose(); Assert.That(arena.Player.InputBlocked, Is.False);
                hud.enabled = true; hud.SendMessage("TogglePause"); Assert.That(Time.timeScale, Is.Zero);
                hud.enabled = false; Assert.That(Time.timeScale, Is.EqualTo(.35f)); Assert.That(arena.Player.InputBlocked, Is.False);
            }
        }
        [UnityTest] public IEnumerator EnginePauseHasOneExplicitWriterAndSessionDestructionRestoresTime()
        {
            var enter = Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            while (enter.MoveNext()) yield return enter.Current;
            var arena = new GameObject("ControlLifetimeFixture").AddComponent<CombatArena>();
            arena.enabled = false; // Do not initialize a second scene/player; exercise actual OnDestroy.
            Time.timeScale = .6f;
            var binding = arena.Controls.BindEngineTime();
            var owner = arena.Controls.OpenScope("scene owner"); var pause = owner.PauseSimulation();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => arena.Controls.BindEngineTime());
            using (var other = new CombatControlBinding(arena)) Assert.Throws<InvalidOperationException>(() => other.BindEngineTime());
            Assert.That(Time.timeScale, Is.Zero);
            arena.enabled = false;
            // Destruction, not hiding an individual view, is the session boundary.
            Object.Destroy(arena.gameObject); yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(.6f)); Assert.That(owner.IsActive || pause.IsActive, Is.False);
            owner.Dispose(); pause.Dispose(); binding.Dispose(); Assert.That(Time.timeScale, Is.EqualTo(.6f));
            // The old adapter cannot release a new composition's engine binding.
            using (var next = new CombatControlBinding(null)) using (var nextTime = next.BindEngineTime()) using (var nextOwner = next.OpenScope("next"))
            {
                nextOwner.PauseSimulation(); binding.Dispose(); Assert.That(Time.timeScale, Is.Zero);
            }
            Assert.That(Time.timeScale, Is.EqualTo(.6f));
        }
        [UnityTest] public IEnumerator HudPointerRestrictionReachesTheCommonKeyboardAdapter()
        {
            var enter = Open("Assets/Deviant/Game/Scenes/deviant.unity");
            while (enter.MoveNext()) yield return enter.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var hud = Hud(arena);
            p.UseTestInput = false; p.CancelAction();
            var settings = InputSystem.settings; var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            Mouse mouse = null; Keyboard keyboard = null;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)Button(hud, "bagButton").transform;
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect, point), Is.True);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update(); mouse.MakeCurrent(); keyboard.MakeCurrent();
                hud.SendMessage("Update");
                p.GetComponent<CombatKeyboardInput>().SendMessage("Update"); arena.SendMessage("FixedUpdate");
                Assert.That(p.InputBlocked, Is.True); Assert.That(p.currentCast, Is.Null);
                // This fixture isolates capture/ingress, not a completed Bag
                // click. Drag outside before release so the real EventSystem
                // must cancel the physical click instead of opening inventory.
                var outside = new Vector2(-1000, -1000);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = outside }.WithButton(MouseButton.Left)); InputSystem.Update();
                hud.SendMessage("Update");
                Assert.That(p.InputBlocked, Is.True, "The HUD owns the held pointer even after it leaves the button.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = outside });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                hud.SendMessage("Update"); p.GetComponent<CombatKeyboardInput>().SendMessage("Update"); arena.SendMessage("FixedUpdate");
                Assert.That(p.InputBlocked, Is.True); Assert.That(p.currentCast, Is.Null, "The pointer-release frame stays consumed.");
                int releasedAt = Time.frameCount;
                while (Time.frameCount == releasedAt) yield return null;
                hud.SendMessage("Update");
                var dialog = (GameObject)hud.GetType().GetField("dialog").GetValue(hud);
                Assert.That(dialog.activeSelf, Is.False, "A drag release outside is not an inventory request.");
                Assert.That(p.InputBlocked, Is.False); Assert.That(p.currentCast, Is.Null, "The blocked edge cannot replay after pointer release.");
            }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Time.timeScale = 1;
        }
        static IEnumerator Exercise(string scene, bool globalTime)
        {
            // EditMode runner must see EnterPlayMode at the top iterator level.
            var enter = Open(scene);
            while (enter.MoveNext()) yield return enter.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player;
            float timeScale = Time.timeScale;
            using (var driver = arena.AcquireManualSimulation()) using (var a = arena.Controls.OpenScope("A")) using (var b = arena.Controls.OpenScope("B"))
            {
                driver.Reset();
                p.SubmitInput(new CombatInput(1, CombatButtons.Basic, true));
                var ai = a.BlockInput(InputChannels.Combat); var bi = b.BlockInput(InputChannels.Combat);
                Assert.That(p.axis, Is.Zero); Assert.That(p.specialHeld, Is.False);
                p.SubmitInput(new CombatInput(1, CombatButtons.Special, true)); ai.Dispose();
                Assert.That(p.InputBlocked, Is.True); driver.Step(.02f, new CombatInput(1, CombatButtons.Basic));
                Assert.That(p.currentCast, Is.Null); bi.Dispose(); Assert.That(p.InputBlocked, Is.False);
                // Submit during a pause that has no input restriction: still no buffered presses on resume.
                var ap = a.PauseSimulation(); var bp = b.PauseSimulation();
                float clock = arena.Now; long tick = arena.SimulationTickCount;
                p.SubmitInput(new CombatInput(1, CombatButtons.Basic, true));
                Assert.Throws<InvalidOperationException>(() => driver.Step(.02f, new CombatInput(1, CombatButtons.Basic)));
                Assert.That(arena.Now, Is.EqualTo(clock)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick));
                Assert.That(arena.SimulationFaulted, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(globalTime ? 0 : timeScale));
                ap.Dispose(); Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                driver.Reset(); Assert.That(arena.Controls.Current.SimulationPaused, Is.True, "Combat reset cannot release UI/dialogue scopes.");
                bp.Dispose(); Assert.That(Time.timeScale, Is.EqualTo(timeScale));
                Assert.That(p.axis, Is.Zero); Assert.That(p.specialHeld, Is.False); driver.Step(.02f); Assert.That(p.currentCast, Is.Null);
                var restriction = a.BlockInput(InputChannels.Interaction | InputChannels.Dialogue | InputChannels.Navigation);
                Assert.That(p.InputBlocked, Is.False, "Other input channels do not disable combat.");
                p.SubmitInput(new CombatInput(0, CombatButtons.Basic)); restriction.Dispose();
                // A channel unrelated to combat must not discard an accepted combat press.
                p.Step(.02f); Assert.That(p.currentCast, Is.Not.Null);
                p.CancelAction(); p.InputBlocked = true;
                var modal = b.BlockInput(InputChannels.Combat); modal.Dispose();
                Assert.That(p.InputBlocked, Is.True, "Legacy test block is independent from scoped requests.");
                p.InputBlocked = false;
            }
            using (var owner = arena.Controls.OpenScope("automatic pause"))
            {
                var pause = owner.PauseSimulation(); float clock = arena.Now;
                yield return new WaitForSecondsRealtime(.1f); Assert.That(arena.Now, Is.EqualTo(clock));
                pause.Dispose(); float deadline = Time.realtimeSinceStartup + 3;
                while (arena.Now <= clock && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(arena.Now, Is.GreaterThan(clock), "Automatic simulation resumes only after the final pause is released.");
            }
        }
        static IEnumerator Open(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True, "The test runner must execute the Play Mode entry instruction.");
            CombatArena arena; float deadline = Time.realtimeSinceStartup + 5;
            while (!(arena = Object.FindAnyObjectByType<CombatArena>()) || !arena.Player)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Wait for the composition's actual player initialization.");
                yield return null;
            }
            arena.Player.UseTestInput = true;
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
        }
        static UnityEngine.UI.Button Button(MonoBehaviour hud, string field) => (UnityEngine.UI.Button)hud.GetType().GetField(field).GetValue(hud);
        static void Click(MonoBehaviour hud, string field) => Button(hud, field).onClick.Invoke();
        static MonoBehaviour Hud(CombatArena arena)
        {
            var composition = arena.GetComponent("GameComposition");
            return (MonoBehaviour)composition.GetType().GetField("hud").GetValue(composition);
        }
    }
}
