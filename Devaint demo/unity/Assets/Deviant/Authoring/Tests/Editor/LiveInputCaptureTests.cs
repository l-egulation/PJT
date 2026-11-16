using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class LiveInputCaptureTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator LiveRecorderPollsRealAssignedSamplesAndRetainsOnlyItsBoundedWindow()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                using (var recorder = new CombatInputRecorder(arena.Player, 2))
                {
                    Assert.That(recorder.Count, Is.Zero); Assert.That(recorder.IsCapturing, Is.True); Assert.That(recorder.CanRecord, Is.False);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic, true, CombatButtons.Special));
                    Assert.That(recorder.LiveBatches, Is.Empty, "The live recorder only polls completed ingress batches; it does not copy submitted fixture values.");
                    recorder.Poll(); var first = recorder.LiveBatches.Single();
                    Assert.That(first.TickCount, Is.Zero); Assert.That(first.Now, Is.Zero); Assert.That(first.Receipts.Count, Is.EqualTo(2));
                    Assert.That(first.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Completed)); Assert.That(first.ObservedThroughNow, Is.EqualTo(.02f));
                    Assert.That(first.Receipts.All(r => r.Disposition == CombatInputDisposition.Consumed && r.Sample.Session.Equals(arena.Player.InputSession)), Is.True);
                    Assert.That(first.Receipts[0].Sample.Input.Pressed, Is.EqualTo(CombatButtons.Basic));
                    Assert.That(first.Receipts[1].Sample.Input.Released, Is.EqualTo(CombatButtons.Special));
                    Assert.Throws<NotSupportedException>(() => ((IList<CombatInputReceipt>)first.Receipts)[0] = default);
                    Assert.Throws<InvalidOperationException>(() => recorder.Record(default)); Assert.Throws<InvalidOperationException>(() => recorder.Complete());
                    for (int i = 0; i < 4; i++) driver.Step(.02f);
                    recorder.Poll();
                    Assert.That(recorder.LiveBatches.Select(b => b.TickCount), Is.EqualTo(new long[] { 3, 4 }));
                    Assert.That(recorder.LostLiveBatches, Is.EqualTo(3));
                    Assert.That(first.Receipts.Count, Is.EqualTo(2), "Published snapshots do not share reused storage.");
                    recorder.Dispose(); driver.Step(.02f); recorder.Poll();
                    Assert.That(recorder.IsCapturing, Is.False); Assert.That(recorder.LiveBatches.Select(b => b.TickCount), Is.EqualTo(new long[] { 3, 4 }));
                }
            }
        }

        [UnityTest, Category("RequiresGraphics")] public IEnumerator ExistingComparisonWindowActuallyStartsPollsAndStopsGameLiveCapture()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var window = ScriptableObject.CreateInstance<SwordDashComparisonWindow>();
            try
            {
                window.Show(); window.CreateGUI();
                var serialized = new SerializedObject(window); serialized.FindProperty("arena").objectReferenceValue = arena; serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(window.rootVisualElement.Q<Button>("startLiveInputs"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>("stopLiveInputs"), Is.Not.Null);
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); window.StartLiveInputs();
                    driver.Step(.02f, new CombatInput(1, CombatButtons.Dash));
                    window.PollLiveInputs();
                    var batch = window.LiveInputBatches.Single();
                    Assert.That(batch.Session.ActorId, Is.EqualTo(arena.Player.ActorId)); Assert.That(batch.TickCount, Is.Zero);
                    Assert.That(batch.Receipts.Single().Sample.Input.Pressed, Is.EqualTo(CombatButtons.Dash));
                    Assert.That(window.rootVisualElement.Q<Label>("liveInputs").text, Does.Contain("tick 0").And.Contain("Consumed"));
                    window.StopLiveInputs(); driver.Step(.02f); window.PollLiveInputs();
                    Assert.That(window.LiveInputBatches.Count, Is.EqualTo(1));
                    Assert.That(window.rootVisualElement.Q<Button>("stopLiveInputs").enabledSelf, Is.False);
                }
            }
            finally { window.Close(); }
        }
    }
}
