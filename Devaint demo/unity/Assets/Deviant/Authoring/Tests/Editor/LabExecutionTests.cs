using System;
using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using Deviant.Control.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class LabExecutionTests
    {
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<LabExecutionWindow>()) window.Close();
            if (Application.isPlaying) yield return new ExitPlayMode();
            Time.timeScale = 1;
        }

        [UnityTest] public IEnumerator RunPauseSingleTickAndSlowMotionAdvanceRealClockWithoutChangingStepOrGlobalTime()
        {
            var open = OpenLab(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            float originalTimeScale = Time.timeScale, step = Time.fixedDeltaTime;
            Time.timeScale = .63f;
            try
            {
                using (var controller = new LabExecutionController(arena))
                {
                    controller.Reset(); Place(arena);
                    Assert.That(controller.IsPaused, Is.True);
                    Assert.That(arena.ManualSimulationActive, Is.True);
                    arena.SendMessage("FixedUpdate");
                    Assert.That(Player(controller).TickCount, Is.Zero);
                    Assert.That(controller.AdvanceElapsed(10), Is.Zero);
                    Assert.That(controller.BacklogSeconds, Is.Zero, "Paused wall time is not accrued.");
                    Assert.That(controller.SingleTick(new CombatInput(1)), Is.True);
                    var first = Player(controller);
                    Assert.That(first.TickCount, Is.EqualTo(1));
                    Assert.That(first.Now, Is.EqualTo(step));
                    Assert.That(first.Kinematics.X, Is.GreaterThan(0));
                    Assert.That(controller.IsPaused, Is.True);
                    controller.Reset(); Place(arena);
                    controller.Run(); controller.SetContinuousInput(1, false);
                    Assert.That(controller.AdvanceElapsed((double)step * 4), Is.EqualTo(4));
                    var normal = Player(controller);
                    controller.Pause();
                    Assert.That(normal.TickCount, Is.EqualTo(4));
                    Assert.That(normal.Now, Is.EqualTo(step * 4).Within(.000001f));
                    controller.Reset(); Place(arena);
                    controller.SlowFactor = .25f;
                    controller.Run(); controller.SetContinuousInput(1, false);
                    Assert.That(controller.AdvanceElapsed((double)step * 4), Is.EqualTo(1));
                    var slow = Player(controller);
                    Assert.That(slow.TickCount, Is.EqualTo(1));
                    Assert.That(slow.Now, Is.EqualTo(step));
                    Assert.That(slow.Kinematics.X, Is.EqualTo(first.Kinematics.X).Within(.000001f));
                    Assert.That(slow.Kinematics.X, Is.LessThan(normal.Kinematics.X));
                    controller.Pause();
                    controller.Reset(); Place(arena);
                    controller.SlowFactor = 1; controller.Run();
                    Assert.That(controller.AdvanceElapsed((double)step * (LabExecutionController.MaximumTicksPerUpdate + 7)),
                        Is.EqualTo(LabExecutionController.MaximumTicksPerUpdate));
                    Assert.That(controller.BacklogSeconds, Is.EqualTo((double)step * 7).Within(.000000001));
                    Assert.That(controller.AdvanceElapsed(0), Is.EqualTo(7));
                    Assert.That(controller.BacklogSeconds, Is.EqualTo(0).Within(.000000001));
                    controller.Pause(); controller.Reset(); Place(arena);
                    controller.SlowFactor = .2f;
                    Assert.That(controller.SingleTick(new CombatInput(0, CombatButtons.Basic)), Is.True);
                    var cast = arena.Player.currentCast;
                    Assert.That(cast, Is.Not.Null);
                    float impact = cast.ImpactTime, duration = cast.MotionDuration;
                    var read = Player(controller);
                    Assert.That(read.Action.DefinitionVersion, Is.EqualTo(cast.DefinitionVersion));
                    Assert.That(read.Action.ExecutionId, Is.EqualTo(cast.ExecutionId));
                    controller.Reset(); Place(arena); controller.SlowFactor = 1;
                    controller.SingleTick(new CombatInput(0, CombatButtons.Basic));
                    Assert.That(arena.Player.currentCast.ImpactTime, Is.EqualTo(impact));
                    Assert.That(arena.Player.currentCast.MotionDuration, Is.EqualTo(duration));
                    Assert.That(Time.fixedDeltaTime, Is.EqualTo(step));
                    Assert.That(Time.timeScale, Is.EqualTo(.63f));
                }
                Assert.That(arena.ManualSimulationActive, Is.False);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(.63f));
            }
            finally { Time.timeScale = originalTimeScale; }
        }

        [UnityTest] public IEnumerator NestedExternalRequestsBlockSteppingAndSurviveControllerPauseResetAndDispose()
        {
            var open = OpenLab(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var external = arena.Controls.OpenScope("external dialogue fixture"))
            {
                var input = external.BlockInput(InputChannels.Combat | InputChannels.Interaction);
                var externalPause = external.PauseSimulation();
                var controller = new LabExecutionController(arena);
                try
                {
                    controller.Reset(); Place(arena);
                    var before = Player(controller);
                    Assert.That(controller.SingleTick(new CombatInput(1, CombatButtons.Basic, true)), Is.False);
                    Assert.That(Player(controller).TickCount, Is.EqualTo(before.TickCount));
                    Assert.That(Player(controller).Now, Is.EqualTo(before.Now));
                    Assert.That(controller.IsPaused, Is.True);
                    Assert.That(externalPause.IsActive && input.IsActive, Is.True);
                    controller.Run();
                    Assert.That(controller.AdvanceElapsed(20), Is.Zero);
                    Assert.That(controller.BacklogSeconds, Is.Zero);
                    controller.Pause(); controller.Reset();
                    externalPause.Dispose();
                    Assert.That(controller.SingleTick(new CombatInput(1, CombatButtons.Basic, true)), Is.True);
                    Assert.That(Player(controller).TickCount, Is.EqualTo(1));
                    Assert.That(arena.Player.currentCast, Is.Null, "The existing combat input restriction is never bypassed.");
                    input.Dispose();
                    controller.SingleTick();
                    Assert.That(arena.Player.currentCast, Is.Null, "A blocked input edge must not replay after unblocking.");
                    Assert.That(arena.Player.CurrentInput.Pressed, Is.EqualTo(CombatButtons.None));
                    controller.Reset(); Place(arena); controller.Run();
                    controller.SetContinuousInput(1, true);
                    Assert.That(controller.QueueButtons(CombatButtons.Basic), Is.True);
                    Assert.That(arena.Player.CurrentInput.Pressed, Is.EqualTo(CombatButtons.Basic));
                    using (external.PauseSimulation()) { }
                    controller.AdvanceElapsed(Time.fixedDeltaTime);
                    Assert.That(arena.Player.currentCast, Is.Null, "A whole nested pause between editor callbacks still clears admitted edges.");
                    Assert.That(controller.Axis, Is.Zero); Assert.That(controller.SpecialHeld, Is.False);
                    controller.QueueButtons(CombatButtons.Basic);
                    using (external.BlockInput(InputChannels.Combat)) { }
                    controller.AdvanceElapsed(Time.fixedDeltaTime);
                    Assert.That(arena.Player.currentCast, Is.Null, "A nested input block also leaves no local replay queue.");
                    using (var capture = arena.Player.BeginInputCapture(8))
                    {
                        long sequence = arena.Player.NextInputSequence;
                        controller.QueueButtons(CombatButtons.Basic);
                        controller.AdvanceElapsed(Time.fixedDeltaTime);
                        Assert.That(arena.Player.currentCast, Is.Not.Null);
                        bool consumed = false;
                        while (capture.TryRead(out var batch))
                            consumed |= batch.Receipts.Any(receipt => receipt.Sample.Sequence == sequence &&
                                receipt.Disposition == CombatInputDisposition.Consumed && receipt.Sample.Input.Pressed == CombatButtons.Basic);
                        Assert.That(consumed, Is.True, "StepPending consumes the original admitted receipt instead of replacing it before the tick.");
                    }
                    controller.Pause();
                    var remainingInput = external.BlockInput(InputChannels.Combat);
                    var remainingPause = external.PauseSimulation();
                    controller.Dispose();
                    var callback = typeof(LabExecutionController).GetMethod("OnEditorUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var capturedCallback = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), controller, callback);
                    Assert.DoesNotThrow(() => capturedCallback(), "A delegate captured before unsubscription must ignore its disposed controller.");
                    Assert.That(arena.ManualSimulationActive, Is.False);
                    Assert.That(remainingInput.IsActive && remainingPause.IsActive, Is.True);
                    Assert.That(arena.Controls.Current.Blocks(InputChannels.Combat), Is.True);
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                    remainingPause.Dispose(); remainingInput.Dispose();
                    using (var next = new LabExecutionController(arena))
                    {
                        next.Reset(); Place(arena); next.SingleTick();
                        Assert.That(arena.Player.currentCast, Is.Null);
                        arena.enabled = false;
                        Assert.That(next.IsDisposed, Is.True);
                        Assert.That(arena.ManualSimulationActive, Is.False);
                        arena.enabled = true;
                    }
                }
                finally { controller.Dispose(); }
            }
        }

        [UnityTest] public IEnumerator DisableInsideDamageAndResetCallbacksReleasesLeaseOnlyAfterTheNativeOperationReturns()
        {
            var open = OpenLab(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var external = arena.Controls.OpenScope("disable during tick external input"))
            {
                var externalInput = external.BlockInput(InputChannels.Interaction);
                using (var controller = new LabExecutionController(arena))
                {
                    controller.Reset(); Place(arena);
                    controller.SingleTick(new CombatInput(0, CombatButtons.Basic));
                    bool disabledDuringTick = false, leaseHeldInsideCallback = false;
                    long entryTick = arena.SimulationTickCount;
                    Application.LogCallback disable = (message, stack, type) =>
                    {
                        if (disabledDuringTick || !message.Contains(arena.enemies[0].name + " damage=")) return;
                        disabledDuringTick = true;
                        arena.enabled = false;
                        leaseHeldInsideCallback = arena.ManualSimulationActive;
                        Assert.That(controller.IsDisposed, Is.True);
                    };
                    Application.logMessageReceived += disable;
                    try
                    {
                        for (int i = 0; i < 30 && !disabledDuringTick; i++)
                        {
                            entryTick = arena.SimulationTickCount;
                            Assert.That(controller.SingleTick(), Is.True);
                        }
                    }
                    finally { Application.logMessageReceived -= disable; }
                    Assert.That(disabledDuringTick, Is.True, "The callback must run from an actual target damage operation inside Step.");
                    Assert.That(leaseHeldInsideCallback, Is.True, "The native manual lease guard still owns the executing tick.");
                    Assert.That(arena.SimulationTickCount, Is.EqualTo(entryTick + 1));
                    Assert.That(arena.enemies[0].health, Is.EqualTo(90));
                    Assert.That(arena.SimulationFaulted, Is.False);
                    Assert.That(arena.ManualSimulationActive, Is.False);
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                    Assert.That(externalInput.IsActive, Is.True);
                    Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Interaction));
                }
                arena.enabled = true;
                using (var controller = new LabExecutionController(arena))
                {
                    bool heldInsideReset = false;
                    Action disable = () => { arena.enabled = false; heldInsideReset = arena.ManualSimulationActive; };
                    arena.ResetCompleted += disable;
                    try { controller.Reset(); }
                    finally { arena.ResetCompleted -= disable; }
                    Assert.That(heldInsideReset, Is.True);
                    Assert.That(controller.IsDisposed, Is.True);
                    Assert.That(arena.ManualSimulationActive, Is.False);
                    Assert.That(arena.SimulationFaulted, Is.False);
                    Assert.That(externalInput.IsActive, Is.True);
                }
                arena.enabled = true;
            }
        }

        [UnityTest] public IEnumerator ManualLeaseIsExclusiveAndSingleTickRestoresOwnPauseAfterSimulationFailure()
        {
            var open = OpenLab(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var owner = arena.AcquireManualSimulation())
            {
                Assert.Throws<InvalidOperationException>(() => new LabExecutionController(arena));
                Assert.That(arena.ManualSimulationActive, Is.True);
                owner.Reset(); owner.Step(Time.fixedDeltaTime);
            }
            using (var controller = new LabExecutionController(arena))
            {
                Assert.Throws<InvalidOperationException>(() => new LabExecutionController(arena));
                controller.Reset(); Place(arena);
                Action failedReset = () => throw new InvalidOperationException("Lab execution reset fixture failure");
                arena.ResetCompleted += failedReset;
                try { Assert.Throws<InvalidOperationException>(() => controller.Reset()); }
                finally { arena.ResetCompleted -= failedReset; }
                Assert.That(arena.SimulationFaulted, Is.True);
                Assert.Throws<InvalidOperationException>(() => controller.SingleTick(new CombatInput(0, CombatButtons.Basic)));
                Assert.That(controller.IsPaused, Is.True, "The controller's own request is restored even when Step throws.");
                Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                Assert.That(arena.ManualSimulationActive, Is.True);
                controller.Reset();
                Assert.That(arena.SimulationFaulted, Is.False);
                controller.SingleTick();
                Assert.That(Player(controller).TickCount, Is.EqualTo(1));
            }
            Assert.That(arena.ManualSimulationActive, Is.False);
        }

        [UnityTest] public IEnumerator GameCompositionIsRejectedAndPlayModeExitDisposesTheLabLease()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var game = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(game.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Game));
            Assert.Throws<InvalidOperationException>(() => new LabExecutionController(game));
            Assert.That(game.ManualSimulationActive, Is.False);
            yield return new ExitPlayMode();
            var open = OpenLab(); while (open.MoveNext()) yield return open.Current;
            var controller = new LabExecutionController(Object.FindAnyObjectByType<CombatArena>());
            Assert.That(controller.IsPaused, Is.True);
            yield return new ExitPlayMode();
            Assert.That(controller.IsDisposed, Is.True);
            controller.Dispose();
        }

        [UnityTest, Category("RequiresGraphics")] public IEnumerator RealWindowControlsActualTicksMetricsRebindAndDisableCleanup()
        {
            var open = OpenLab(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var window = ScriptableObject.CreateInstance<LabExecutionWindow>();
            var root = window.rootVisualElement;
            try
            {
                window.Show(); window.CreateGUI();
                root.Q<UnityEditor.UIElements.ObjectField>("runtime").value = arena;
                Click(window, "bind");
                Assert.That(window.Controller, Is.Not.Null);
                Assert.That(window.Controller.IsPaused, Is.True);
                Click(window, "reset"); Place(arena);
                Click(window, "step");
                Assert.That(Player(window.Controller).TickCount, Is.EqualTo(1));
                Assert.That(root.Q<Label>("metrics").text, Does.Contain("tick 1").And.Contain("Player HP"));
                Click(window, "basic");
                Assert.That(arena.Player.currentCast, Is.Not.Null);
                Assert.That(root.Q<Label>("metrics").text, Does.Contain(arena.Player.currentCast.DefinitionVersion));
                root.Q<Slider>("slowFactor").value = .25f;
                Assert.That(window.Controller.SlowFactor, Is.EqualTo(.25f));
                int originalMask = arena.ViewCamera.cullingMask;
                root.Q<Toggle>("visualOutput").value = false;
                Assert.That(arena.ViewCamera.cullingMask, Is.Zero);
                Click(window, "run");
                float deadline = Time.realtimeSinceStartup + 3;
                while (Player(window.Controller).TickCount < 3)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                    yield return null;
                }
                Click(window, "pause");
                long pausedTick = Player(window.Controller).TickCount;
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(Player(window.Controller).TickCount, Is.EqualTo(pausedTick));
                using (var other = arena.Controls.OpenScope("window external owner"))
                {
                    var pause = other.PauseSimulation(); var input = other.BlockInput(InputChannels.Combat);
                    Click(window, "step");
                    Assert.That(Player(window.Controller).TickCount, Is.EqualTo(pausedTick));
                    Assert.That(root.Q<Label>("status").text, Does.Contain("다른 소유자"));
                    root.Q<UnityEditor.UIElements.ObjectField>("runtime").value = null;
                    Assert.That(window.Controller, Is.Null);
                    Assert.That(arena.ManualSimulationActive, Is.False);
                    Assert.That(arena.ViewCamera.cullingMask, Is.EqualTo(originalMask));
                    Assert.That(input.IsActive && pause.IsActive, Is.True);
                    window.BindRuntime(arena); window.BindRuntime(arena);
                    Assert.That(arena.ManualSimulationActive, Is.True);
                    root.Q<Toggle>("visualOutput").value = false;
                    window.Close();
                    Assert.That(arena.ManualSimulationActive, Is.False);
                    Assert.That(arena.ViewCamera.cullingMask, Is.EqualTo(originalMask));
                    Assert.That(input.IsActive && pause.IsActive, Is.True);
                }
            }
            finally { if (window) window.Close(); }
        }

        [UnityTest] public IEnumerator VisualSuppressionLeavesRendererAndCueLifetimesAndDamageIdentical()
        {
            var open = OpenLab(); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var texture = new Texture2D(6, 1);
            var sprites = Enumerable.Range(0, 6).Select(i => Sprite.Create(texture, new Rect(i, 0, 1, 1), Vector2.zero, 32)).ToArray();
            try
            {
                arena.characterAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
                var set = arena.characterAnimations.sets.Single(s => s.form == "base_sword");
                set.Find("attack1").sprites = sprites.Take(5).ToArray();
                set.Effect("attack1").sprites = sprites;
                using (var controller = new LabExecutionController(arena))
                {
                    int originalMask = arena.ViewCamera.cullingMask;
                    var visible = Attack(arena, controller);
                    Assert.That(visible.Hits, Is.EqualTo(1));
                    Assert.That(visible.Health, Is.EqualTo(90));
                    Assert.That(visible.CueSeen, Is.True);
                    Assert.That(arena.Presentation.LastEffectHandle.Status, Is.EqualTo(Deviant.Presentation.Contracts.CueStatus.Completed));
                    Assert.That(arena.Presentation.ActiveEffects, Is.Zero);
                    bool playerRenderer = arena.Player.visual.enabled;
                    controller.SetVisualOutput(false);
                    var suppressed = Attack(arena, controller);
                    Assert.That(arena.ViewCamera.cullingMask, Is.Zero);
                    Assert.That(arena.Player.visual.enabled, Is.EqualTo(playerRenderer));
                    Assert.That(suppressed, Is.EqualTo(visible), "Suppressing output cannot remove cue updates or change gameplay results.");
                    Assert.That(arena.Presentation.LastEffectHandle.Status, Is.EqualTo(Deviant.Presentation.Contracts.CueStatus.Completed));
                    Assert.That(arena.Presentation.ActiveEffects, Is.Zero);
                    Assert.That(arena.Presentation.LastEffectError, Is.Null);
                    var spawned = new GameObject("SpawnedWhileOutputSuppressed").AddComponent<SpriteRenderer>();
                    Assert.That(spawned.enabled, Is.True);
                    Assert.That((arena.ViewCamera.cullingMask & (1 << spawned.gameObject.layer)), Is.Zero);
                    Object.Destroy(spawned.gameObject);
                    controller.SetVisualOutput(true);
                    Assert.That(arena.ViewCamera.cullingMask, Is.EqualTo(originalMask));
                    controller.SetVisualOutput(false);
                    arena.ViewCamera.cullingMask = 1 << 4;
                    controller.SetVisualOutput(true);
                    Assert.That(arena.ViewCamera.cullingMask, Is.EqualTo(1 << 4), "A later camera writer owns its value.");
                    arena.ViewCamera.cullingMask = originalMask;
                    controller.SetVisualOutput(false);
                }
                Assert.That(arena.ViewCamera.cullingMask, Is.Not.Zero);
            }
            finally
            {
                arena.Presentation.Reset();
                foreach (var sprite in sprites) Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        static (float Health, int Hits, long Ticks, float Now, float X, bool CueSeen) Attack(CombatArena arena, LabExecutionController controller)
        {
            controller.Reset(); Place(arena);
            bool cue = false;
            for (int i = 0; i < 30; i++)
            {
                controller.SingleTick(new CombatInput(0, i == 0 ? CombatButtons.Basic : CombatButtons.None));
                arena.Presentation.Draw(0);
                cue |= arena.Presentation.LastEffectHandle != null;
            }
            Assert.That(controller.TryReadTarget(0, out var target), Is.True);
            var player = Player(controller);
            return (target.Vitals.Health, target.Vitals.ReceivedHits, player.TickCount, player.Now, player.Kinematics.X, cue);
        }

        static void Place(CombatArena arena)
        {
            var player = arena.Player; player.selected = 0;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
            player.Position = Vector2.zero; player.SyncFacing(1);
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            arena.enemies[0].Position = new Vector2(3, 0);
            Physics2D.SyncTransforms();
        }

        static ActorReadSnapshot Player(LabExecutionController controller)
        {
            Assert.That(controller.TryReadPlayer(out var player), Is.True);
            return player;
        }

        static void Click(LabExecutionWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name);
            Assert.That(button.enabledSelf, Is.True, name);
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = button; button.SendEvent(click); }
        }

        static IEnumerator OpenLab()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode();
            CombatArena arena; float deadline = Time.realtimeSinceStartup + 5;
            while (!(arena = Object.FindAnyObjectByType<CombatArena>()) || !arena.Player)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
        }
    }
}
