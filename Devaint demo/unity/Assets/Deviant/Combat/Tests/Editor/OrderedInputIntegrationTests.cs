using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Control.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class OrderedInputIntegrationTests
    {
        [UnityTest] public IEnumerator GameKeyboardKeepsIndividualEdgesBeforeOneSharedTick() => KeyboardIngress("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabKeyboardKeepsIndividualEdgesBeforeOneSharedTick() => KeyboardIngress("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator GameResetAndManualReplayUseTheSameSessionIngress() => ResetAndManual("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabResetAndManualReplayUseTheSameSessionIngress() => ResetAndManual("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator GamePreservesRepeatedKeyboardEventsBeforeOnePoll() => RepeatedEvents("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabPreservesRepeatedKeyboardEventsBeforeOnePoll() => RepeatedEvents("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static IEnumerator RepeatedEvents(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            AssertRepeatedEvents();
        }
        // Captured callbacks must be created after Unity has reloaded the domain.
        static void AssertRepeatedEvents()
        {
            var arena = Object.FindAnyObjectByType<CombatArena>(); arena.ResetTest(); Prepare(arena);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var eventTimes = new List<double>();
            Action<InputEventPtr, InputDevice> observe = (input, device) => { if (device == keyboard && input.IsA<StateEvent>()) eventTimes.Add(input.time); };
            InputSystem.onEvent += observe;
            try
            {
                keyboard.MakeCurrent();
                using (var capture = arena.Player.BeginInputCapture(32))
                {
                    double time = Time.realtimeSinceStartupAsDouble;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A), time - .003);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(), time - .002);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A), time - .001);
                    InputSystem.Update(); arena.Player.GetComponent<CombatKeyboardInput>().SendMessage("Update");
                    Assert.That(arena.Player.currentCast, Is.Null);
                    arena.SendMessage("FixedUpdate");
                    var receipts = Drain(capture).SelectMany(batch => batch.Receipts).ToArray();
                    Assert.That(receipts.Select(value => value.Sample.Input.Pressed), Is.EqualTo(new[] { CombatButtons.Weapon1Basic, CombatButtons.None, CombatButtons.Weapon1Basic }));
                    Assert.That(receipts[1].Sample.Input.Released, Is.EqualTo(CombatButtons.Weapon1Basic));
                    Assert.That(receipts.Select(value => value.Sample.Timestamp), Is.EqualTo(eventTimes));
                    Assert.That(arena.Player.currentCast, Is.Not.Null);
                    arena.SendMessage("FixedUpdate"); Assert.That(Drain(capture).SelectMany(batch => batch.Receipts), Is.Empty);
                    arena.ResetTest(); Prepare(arena); Drain(capture); eventTimes.Clear();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    arena.Player.GetComponent<CombatKeyboardInput>().SendMessage("Update"); arena.SendMessage("FixedUpdate"); Drain(capture); eventTimes.Clear();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.A));
                    InputSystem.Update(); arena.Player.GetComponent<CombatKeyboardInput>().SendMessage("Update"); arena.SendMessage("FixedUpdate");
                    receipts = Drain(capture).SelectMany(batch => batch.Receipts).ToArray();
                    Assert.That(receipts.Select(value => value.Sample.Input.Pressed), Is.EqualTo(new[] { CombatButtons.Weapon1Special, CombatButtons.Weapon1Basic }));
                    Assert.That(receipts.Select(value => value.Sample.Timestamp), Is.EqualTo(eventTimes));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    arena.ResetTest(); Prepare(arena); Drain(capture);
                    arena.Player.GetComponent<CombatKeyboardInput>().SendMessage("Update"); arena.SendMessage("FixedUpdate");
                    Assert.That(Drain(capture).SelectMany(batch => batch.Receipts).All(value => value.Sample.Input.Pressed == CombatButtons.None && value.Sample.Input.Released == CombatButtons.None), Is.True);
                    var adapter = arena.Player.GetComponent<CombatKeyboardInput>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
                    adapter.enabled = false; adapter.enabled = true; adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
                    Assert.That(Drain(capture).SelectMany(batch => batch.Receipts).All(value => value.Sample.Input.Pressed == CombatButtons.None), Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); Drain(capture);
                    using (var scope = arena.Controls.OpenScope("event-capture-block"))
                    {
                        var block = scope.BlockInput(InputChannels.Combat);
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
                        block.Dispose(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
                        Assert.That(arena.Player.currentCast, Is.Null);
                        Assert.That(Drain(capture).SelectMany(batch => batch.Receipts).All(value => value.Sample.Input.Pressed == CombatButtons.None), Is.True);
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); Drain(capture);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
                    using (var scope = arena.Controls.OpenScope("intervening-event-block")) { using (scope.BlockInput(InputChannels.Combat)) { } }
                    adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
                    Assert.That(arena.Player.currentCast, Is.Null);
                    Assert.That(Drain(capture).SelectMany(batch => batch.Receipts).All(value => value.Sample.Input.Pressed == CombatButtons.None), Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); Drain(capture);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
                    using (arena.AcquireManualSimulation()) { }
                    adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
                    Assert.That(arena.Player.currentCast, Is.Null);
                    Drain(capture); arena.Player.health = 0;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F5)); InputSystem.Update(); adapter.SendMessage("Update");
                    Assert.That(arena.Player.Dead, Is.False, "F5 still resets the dead test profile.");
                    Prepare(arena); Drain(capture); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); Drain(capture);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F5)); InputSystem.Update(); arena.Player.health = 42;
                    using (arena.AcquireManualSimulation()) { }
                    adapter.SendMessage("Update"); Assert.That(arena.Player.health, Is.EqualTo(42), "A pre-lease queued F5 must not reset the new owner boundary.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); Drain(capture);
                    Poll(adapter, keyboard, Key.RightArrow); arena.SendMessage("FixedUpdate");
                    Assert.That(arena.Player.axis, Is.EqualTo(1)); Assert.That(arena.Player.currentCast, Is.Null, "D is movement, not a special attack."); Drain(capture);
                    Poll(adapter, keyboard, Key.LeftArrow); arena.SendMessage("FixedUpdate"); Assert.That(arena.Player.axis, Is.EqualTo(-1)); Drain(capture);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
                    adapter.SendMessage("OnApplicationFocus", false); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate");
                    Assert.That(arena.Player.currentCast, Is.Null); Drain(capture);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); adapter.SendMessage("Update"); arena.SendMessage("FixedUpdate"); Drain(capture);
                    for (int i = 0; i < CombatInputIngress.DefaultCapacity - 1; i++) arena.Player.SubmitInput(default(CombatInput));
                    long sequence = arena.Player.NextInputSequence;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.RightArrow)); InputSystem.Update();
                    var update = typeof(CombatKeyboardInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() => update.Invoke(adapter, null));
                    Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(arena.Player.NextInputSequence, Is.EqualTo(sequence)); Assert.That(arena.Player.CurrentInput.Axis, Is.Zero);
                    arena.Player.ClearInputState(); Drain(capture);
                    arena.ResetTest(); Prepare(arena);
                    arena.Player.Reconfigure(1, Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword"), 0);
                    Poll(adapter, keyboard); arena.SendMessage("FixedUpdate"); Drain(capture);
                    Poll(adapter, keyboard, Key.S); arena.SendMessage("FixedUpdate");
                    Assert.That(arena.Player.selected, Is.EqualTo(1)); Assert.That(arena.Player.currentCast, Is.Not.Null);
                    arena.ResetTest(); Prepare(arena); Poll(adapter, keyboard); arena.SendMessage("FixedUpdate"); Drain(capture);
                    var defaultKey = adapter.Bindings.Weapon2Basic;
                    try
                    {
                        adapter.Bindings.Weapon2Basic = Key.T;
                        Poll(adapter, keyboard, Key.S); arena.SendMessage("FixedUpdate");
                        Assert.That(arena.Player.currentCast, Is.Null, "The old key no longer dispatches the rebound action.");
                        Poll(adapter, keyboard, Key.T); arena.SendMessage("FixedUpdate");
                        Assert.That(arena.Player.selected, Is.EqualTo(1)); Assert.That(arena.Player.currentCast, Is.Not.Null);
                    }
                    finally { adapter.Bindings.Weapon2Basic = defaultKey; }
                }
            }
            finally { InputSystem.onEvent -= observe; InputSystem.RemoveDevice(keyboard); }
        }
        [UnityTest] public IEnumerator FailedNativeActionKeepsConsumedThenDeadDiscardReceiptOrder()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            AssertFailedNativeAction();
        }
        // Captured callbacks must be created after Unity has reloaded the domain.
        static void AssertFailedNativeAction()
        {
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var target = new GameObject("OrderedInputFailureTarget").AddComponent<ArenaTickSnapshotProbe>();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); Prepare(arena); player.Position = new Vector2(0, 1000);
                    target.Configure(arena, new Vector2(1.25f, 1000), new Vector2(1, 2), Vector2.up, false);
                    var source = new SkillData { id = "ordered-input-failure", vertical = "normal", activation = 0, end = 2,
                        invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                        hits = new[] { new HitData { time = 0, active = 1, damage = 1, anchor = "actor", targets = "all",
                            box = new BoxData { x = 1.5f, y = 1, width = 4, height = 2 } } } };
                    var cast = CombatCast.Create(arena, player, source, player.Weapon, true); cast.BeforeMovement(.125f);
                    Physics2D.SyncTransforms(); var failure = new InvalidOperationException("ordered input later action failure");
                    CombatInputAdmission discarded = default;
                    target.OnCancel = () =>
                    {
                        player.health = 0; player.Die();
                        discarded = player.SubmitInput(Manual(player, new CombatInput(0, CombatButtons.Basic)));
                        throw failure;
                    };
                    using (var capture = player.BeginInputCapture(8))
                    {
                        Assert.That(Assert.Throws<InvalidOperationException>(() => driver.Step(.125f)), Is.SameAs(failure));
                        Assert.That(discarded.Status, Is.EqualTo(CombatInputAdmissionStatus.Discarded));
                        var batches = Drain(capture); Assert.That(batches.Count, Is.EqualTo(2));
                        Assert.That(batches[0].Kind, Is.EqualTo(CombatInputBatchKind.Consumption));
                        Assert.That(batches[0].TickOutcome, Is.EqualTo(CombatInputTickOutcome.Faulted));
                        Assert.That(batches[0].Receipts.Single().Disposition, Is.EqualTo(CombatInputDisposition.Consumed));
                        Assert.That(batches[1].Kind, Is.EqualTo(CombatInputBatchKind.Discard));
                        Assert.That(batches[1].Receipts.Single().DiscardReason, Is.EqualTo(CombatInputDiscardReason.Dead));
                        Assert.That(batches[1].Receipts.Single().Sample.Sequence, Is.GreaterThan(batches[0].Receipts.Single().Sample.Sequence));
                        Assert.That(arena.SimulationFaulted, Is.True); Assert.That(arena.Now, Is.Zero);
                    }
                    target.OnCancel = null; driver.Reset(); driver.Step(.02f); Assert.That(player.currentCast, Is.Null);
                }
            }
            finally { target.OnCancel = null; Object.Destroy(target.gameObject); }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static void Prepare(CombatArena arena)
        {
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            var player = arena.Player; player.UseTestInput = false; player.selected = 0;
            player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword"), 0);
            player.Position = Vector2.zero; player.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static void Poll(CombatKeyboardInput adapter, Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); keyboard.MakeCurrent();
            adapter.SendMessage("Update");
        }
        static List<CombatInputBatch> Drain(ICombatInputCapture capture)
        {
            var batches = new List<CombatInputBatch>();
            while (capture.TryRead(out var batch)) batches.Add(batch);
            return batches;
        }
        static IEnumerator KeyboardIngress(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            arena.ResetTest(); Prepare(arena);
            var adapter = player.GetComponent<CombatKeyboardInput>();
            var settings = InputSystem.settings; var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard = null; float timeScale = Time.timeScale;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                using (var capture = player.BeginInputCapture(64))
                {
                    long tick = arena.SimulationTickCount; float now = arena.Now; double before = Time.realtimeSinceStartupAsDouble;
                    var outcomeValues = new CombatCommandOutcome[32];
                    var outcomeCursor = new CombatOutcomeCursor(player.InputSession);
                    Assert.That(arena.Commands.TryReadOutcomes(outcomeCursor, outcomeValues, out var outcomePage), Is.True);
                    outcomeCursor = outcomePage.Next;
                    Poll(adapter, keyboard, Key.A, Key.RightArrow);
                    Poll(adapter, keyboard);
                    Poll(adapter, keyboard, Key.A, Key.Q, Key.LeftShift, Key.RightShift, Key.LeftArrow);
                    Assert.That(player.currentCast, Is.Null); Assert.That(player.Dashing, Is.False);
                    Assert.That(capture.TryRead(out _), Is.False);
                    Assert.That(arena.Commands.TryReadOutcomes(outcomeCursor, outcomeValues, out outcomePage), Is.True);
                    Assert.That(outcomePage.Count, Is.Zero, "Keyboard admission alone must not emit an actual action start.");
                    arena.SendMessage("FixedUpdate");
                    var batches = Drain(capture); Assert.That(batches.Count, Is.EqualTo(1)); var batch = batches[0];
                    Assert.That(batch.Kind, Is.EqualTo(CombatInputBatchKind.Consumption));
                    Assert.That(batch.TickCount, Is.EqualTo(tick)); Assert.That(batch.Now, Is.EqualTo(now));
                    Assert.That(batch.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Completed)); Assert.That(batch.ObservedThroughNow, Is.EqualTo(arena.Now));
                    Assert.That(batch.Receipts.Count, Is.EqualTo(6));
                    Assert.That(batch.Receipts.Select(r => r.Sample.Input.Pressed), Is.EqualTo(new[] { CombatButtons.Weapon1Basic, CombatButtons.None, CombatButtons.Weapon1Basic, CombatButtons.Weapon1Special, CombatButtons.Dash, CombatButtons.Dash }));
                    Assert.That(batch.Receipts[1].Sample.Input.Released, Is.EqualTo(CombatButtons.Weapon1Basic));
                    for (int i = 0; i < batch.Receipts.Count; i++)
                    {
                        var receipt = batch.Receipts[i]; var sample = receipt.Sample;
                        Assert.That(receipt.Disposition, Is.EqualTo(CombatInputDisposition.Consumed));
                        Assert.That(sample.Session, Is.EqualTo(player.InputSession)); Assert.That(sample.DeviceId, Is.EqualTo(keyboard.deviceId));
                        Assert.That(sample.Source, Is.EqualTo(CombatInputSource.Keyboard)); Assert.That(sample.TimeDomain, Is.EqualTo(CombatInputTimeDomain.Realtime));
                        Assert.That(sample.Timestamp, Is.InRange(before, Time.realtimeSinceStartupAsDouble));
                        if (i > 0) { Assert.That(sample.Sequence, Is.GreaterThan(batch.Receipts[i - 1].Sample.Sequence)); Assert.That(sample.Timestamp, Is.GreaterThanOrEqualTo(batch.Receipts[i - 1].Sample.Timestamp)); }
                    }
                    Assert.That(batch.Input.Pressed, Is.EqualTo(CombatButtons.Weapon1Basic | CombatButtons.Weapon1Special | CombatButtons.Dash));
                    Assert.That(batch.Input.Released, Is.EqualTo(CombatButtons.Weapon1Basic)); Assert.That(batch.Input.Axis, Is.EqualTo(-1)); Assert.That(batch.Input.IsSpecialHeld(0), Is.True);
                    Assert.That(player.Dashing, Is.True, "Current bit-union dash priority remains unchanged; earlier basic edges do not execute chronologically.");
                    Assert.That(player.currentCast, Is.Null); Assert.That(player.axis, Is.EqualTo(-1)); Assert.That(player.specialHeld, Is.True);
                    Assert.That(arena.Commands.TryReadOutcomes(outcomeCursor, outcomeValues, out outcomePage), Is.True);
                    var actionOutcomes = outcomeValues.Take(outcomePage.Count).ToArray();
                    var dashStart = actionOutcomes.Single(o => o.State == CombatCommandState.Started);
                    Assert.That(dashStart.Command.Equals(CombatCommandKey.For(batch.Receipts[4].Sample)), Is.True);
                    Assert.That(actionOutcomes.Count(o => o.State == CombatCommandState.Rejected), Is.EqualTo(4));
                    Assert.That(actionOutcomes.All(o => o.State == CombatCommandState.Started || o.Reason == CombatCommandReason.Priority), Is.True);
                    arena.SendMessage("FixedUpdate"); var next = Drain(capture).Single();
                    Assert.That(next.Receipts, Is.Empty); Assert.That(next.Input.Pressed, Is.EqualTo(CombatButtons.None)); Assert.That(next.Input.Released, Is.EqualTo(CombatButtons.None));
                    Poll(adapter, keyboard); arena.SendMessage("FixedUpdate"); var released = Drain(capture).Single();
                    Assert.That(released.Receipts.Select(r => r.Sample.Input.Released), Is.EqualTo(new[] { CombatButtons.Weapon1Basic, CombatButtons.Weapon1Special, CombatButtons.Dash, CombatButtons.Dash }));
                    Assert.That(released.Input.IsSpecialHeld(0), Is.False); Assert.That(released.Input.Axis, Is.Zero);
                    Poll(adapter, keyboard, Key.DownArrow, Key.Space); Poll(adapter, keyboard); arena.SendMessage("FixedUpdate");
                    var drop = Drain(capture).Single();
                    Assert.That(drop.Receipts[0].Sample.Input.Pressed, Is.EqualTo(CombatButtons.Drop));
                    Assert.That(drop.Receipts[1].Sample.Input.Released, Is.EqualTo(CombatButtons.Drop), "Space release keeps the sampled press mapping even after Down is released.");

                    arena.ResetTest(); player.ClearInputState(); Poll(adapter, keyboard); Drain(capture);
                    Poll(adapter, keyboard, Key.A);
                    using (var scope = arena.Controls.OpenScope("ordered-input-block-test"))
                    {
                        var block = scope.BlockInput(InputChannels.Combat);
                        Poll(adapter, keyboard); Poll(adapter, keyboard, Key.A);
                        Assert.That(player.CurrentInput, Is.EqualTo(default(CombatInput)));
                        block.Dispose(); arena.SendMessage("FixedUpdate"); Assert.That(player.currentCast, Is.Null);
                    }
                    var blocked = Drain(capture).SelectMany(b => b.Receipts).ToArray();
                    Assert.That(blocked.Length, Is.GreaterThanOrEqualTo(3));
                    Assert.That(blocked.All(r => r.Disposition == CombatInputDisposition.Discarded), Is.True);
                    Assert.That(blocked.Any(r => r.DiscardReason == CombatInputDiscardReason.Blocked), Is.True);

                    using (var scope = arena.Controls.OpenScope("ordered-input-pause-test"))
                    {
                        var pause = scope.PauseSimulation(); long pausedTick = arena.SimulationTickCount;
                        Poll(adapter, keyboard); Poll(adapter, keyboard, Key.A); arena.SendMessage("FixedUpdate");
                        Assert.That(arena.SimulationTickCount, Is.EqualTo(pausedTick));
                        pause.Dispose(); arena.SendMessage("FixedUpdate"); Assert.That(player.currentCast, Is.Null);
                    }
                    Assert.That(Drain(capture).SelectMany(b => b.Receipts).All(r => r.Disposition == CombatInputDisposition.Discarded), Is.True);
                    Time.timeScale = 0; Poll(adapter, keyboard); Poll(adapter, keyboard, Key.A);
                    Time.timeScale = timeScale; arena.SendMessage("FixedUpdate"); Assert.That(player.currentCast, Is.Null);
                    Assert.That(Drain(capture).SelectMany(b => b.Receipts).Any(r => r.DiscardReason == CombatInputDiscardReason.Paused), Is.True);
                }
            }
            finally
            {
                Time.timeScale = timeScale;
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        static CombatInputSample Manual(CombatPlayer player, CombatInput input, CombatInputSession? session = null, long? sequence = null)
            => new CombatInputSample(session ?? player.InputSession, sequence ?? player.NextInputSequence, input,
                CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, player.arena.Now);
        static IEnumerator ResetAndManual(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            using (var capture = player.BeginInputCapture(64))
            {
                driver.Reset(); Prepare(arena);
                var original = player.InputSession;
                var old = Manual(player, new CombatInput(-1, CombatButtons.Basic, true));
                Assert.That(player.SubmitInput(old).Status, Is.EqualTo(CombatInputAdmissionStatus.Admitted));
                Assert.That(player.currentCast, Is.Null, "Admission is not action success.");
                arena.ResetTest();
                Assert.That(player.InputSession.Generation, Is.GreaterThan(original.Generation)); Assert.That(player.InputSession.Epoch, Is.EqualTo(original.Epoch));
                Assert.That(player.axis, Is.EqualTo(-1)); Assert.That(player.specialHeld, Is.True); Assert.That(player.CurrentInput.Pressed, Is.EqualTo(CombatButtons.None));
                long sequence = player.NextInputSequence;
                Assert.That(player.SubmitInput(Manual(player, default, original, sequence)).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSession));
                var foreign = new CombatInputSession(player.ActorId + 1000, player.InputSession.Generation, player.InputSession.Epoch);
                Assert.That(player.SubmitInput(Manual(player, new CombatInput(1, CombatButtons.Dash), foreign, sequence)).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSession));
                Assert.That(player.NextInputSequence, Is.EqualTo(sequence)); Assert.That(player.axis, Is.EqualTo(-1));
                driver.Reset();
                Assert.That(player.InputSession.Epoch, Is.EqualTo(original.Epoch + 1)); Assert.That(player.axis, Is.Zero); Assert.That(player.specialHeld, Is.False);
                Assert.That(player.SubmitInput(old).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSession));
                Drain(capture);

                driver.Step(.02f, new CombatInput(1, CombatButtons.Basic, true, CombatButtons.Special));
                var first = Drain(capture).Single();
                Assert.That(first.TickCount, Is.Zero); Assert.That(first.Now, Is.Zero); Assert.That(first.Session, Is.EqualTo(player.InputSession));
                Assert.That(first.Receipts.Count, Is.EqualTo(2)); Assert.That(first.Receipts[0].Sample.Input.Pressed, Is.EqualTo(CombatButtons.Basic));
                Assert.That(first.Receipts[1].Sample.Input.Released, Is.EqualTo(CombatButtons.Special));
                Assert.That(first.Receipts.All(r => r.Sample.Source == CombatInputSource.Simulation && r.Sample.DeviceId == 0 && r.Sample.TimeDomain == CombatInputTimeDomain.Simulation && r.Sample.Timestamp == 0), Is.True);
                Assert.That(player.currentCast && player.currentCast.isBasic, Is.True);
                Assert.That(player.SubmitInput(first.Receipts[0].Sample).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSequence));
                Assert.That(first.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Completed)); Assert.That(first.ObservedThroughNow, Is.EqualTo(.02f));
                driver.Step(.02f); var second = Drain(capture).Single(); Assert.That(second.Receipts.Count, Is.EqualTo(1));
                Assert.That(second.Receipts[0].Sample.Input, Is.EqualTo(default(CombatInput))); Assert.That(second.Now, Is.EqualTo(.02f));
                Assert.That(second.Receipts[0].Sample.Timestamp, Is.EqualTo((double).02f)); Assert.That(second.TickCount, Is.EqualTo(1));

                var stale = Manual(player, new CombatInput(1, CombatButtons.Special, true)); player.SubmitInput(stale);
                player.health = 0; player.Die(); var death = Drain(capture).SelectMany(b => b.Receipts).ToArray();
                Assert.That(death.Any(r => r.Sample.Sequence == stale.Sequence && r.DiscardReason == CombatInputDiscardReason.Dead), Is.True);
                Assert.That(player.SubmitInput(Manual(player, new CombatInput(1, CombatButtons.Basic))).Status, Is.EqualTo(CombatInputAdmissionStatus.Discarded));
                driver.Reset(); driver.Step(.02f); Assert.That(player.currentCast, Is.Null);
            }
        }
    }
}
