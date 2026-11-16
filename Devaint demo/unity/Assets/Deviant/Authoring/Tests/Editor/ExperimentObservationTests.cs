using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using Deviant.CombatLab.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class ExperimentObservationTests
    {
        CombatArena arena;
        string directory;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "deviant-experiment-observations-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; arena = Object.FindAnyObjectByType<CombatArena>();
            int sword = Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword");
            foreach (var weapon in arena.Player.weapons) { weapon.Select(sword, 0); weapon.StatSlots.Clear(); }
            arena.Player.selected = 0; arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1);
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            var failures = new List<Exception>();
            foreach (var window in Resources.FindObjectsOfTypeAll<ExperimentScenarioWindow>().Where(window => window.name.StartsWith("ObservationTest")))
                try { window.Close(); } catch (Exception error) { failures.Add(error); }
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            if (failures.Count != 0) throw new AggregateException(failures);
        }
        static CombatInput[] SpecialThenCooldown(int ticks = 100)
        {
            var input = new CombatInput[ticks]; input[0] = new CombatInput(pressed: CombatButtons.Special); input[1] = new CombatInput(released: CombatButtons.Special);
            input[50] = new CombatInput(pressed: CombatButtons.Special); return input;
        }
        ExperimentSession Record(CombatInput[] inputs)
        {
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/actual-command-resources"); session.RecordManual(initial, .02f, inputs); return session;
        }
        [Test] public void ReplayRetainsEveryCommandStateReasonAndBothActualResourceOwners()
        {
            var session = Record(SpecialThenCooldown()); var first = session.LastResult; var replay = session.Replay();
            Assert.That(first.CommandOutcomes.Any(value => value.Outcome.State == CombatCommandState.Started && value.Outcome.Command.Kind == CombatCommandKind.Special), Is.True);
            Assert.That(first.CommandOutcomes.Any(value => value.Outcome.State == CombatCommandState.Completed && value.Outcome.Command.Kind == CombatCommandKind.Special), Is.True);
            Assert.That(first.CommandOutcomes.Any(value => value.Outcome.State == CombatCommandState.Rejected && value.Outcome.Reason == CombatCommandReason.Cooldown), Is.True);
            Assert.That(first.CommandOutcomes.Select(value => value.EventOrdinal), Is.EqualTo(Enumerable.Range(1, first.CommandOutcomes.Count)));
            var started = first.CommandOutcomes.First(value => value.Outcome.State == CombatCommandState.Started && value.Outcome.Command.Kind == CombatCommandKind.Special);
            var completed = first.CommandOutcomes.First(value => value.Outcome.State == CombatCommandState.Completed && value.Outcome.Command.Equals(started.Outcome.Command));
            Assert.That(completed.CommandOrdinal, Is.EqualTo(started.CommandOrdinal)); Assert.That(completed.ExecutionOrdinal, Is.EqualTo(started.ExecutionOrdinal));
            Assert.That(completed.Outcome.ExecutionId, Is.EqualTo(started.Outcome.ExecutionId)); Assert.That(started.ActorKey, Is.EqualTo(ExperimentInitialState.PlayerKey));
            Assert.That(first.Ticks.Any(tick => tick.Resources.Slot0.Available == 0 && tick.Resources.Slot0.NextReady > tick.Now), Is.True);
            Assert.That(first.Ticks.All(tick => tick.Resources.Slot1.Available == tick.Resources.Slot1.Capacity), Is.True);
            Assert.That(first.Ticks.Sum(tick => tick.CommandOutcomeCount), Is.EqualTo(first.CommandOutcomes.Count));
            Assert.That(replay.CommandTraceHash, Is.EqualTo(first.CommandTraceHash)); Assert.That(replay.ResourceTraceHash, Is.EqualTo(first.ResourceTraceHash)); Assert.That(replay.TraceHash, Is.EqualTo(first.TraceHash));
            Assert.That(replay.HitTraceHash, Is.EqualTo(first.HitTraceHash)); Assert.That(first.CommandOutcomes.All(value => value.Outcome.EventSequence > 0), Is.True);
            Assert.That(first.Ticks.Last().Resources.Slot0.InstanceId, Is.EqualTo(arena.Player.weapons[0].InstanceId)); Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [Test] public void CancellationAndNoChargeBeforeActivationAreObservedRatherThanInferredFromNoHits()
        {
            var inputs = new CombatInput[40]; inputs[0] = new CombatInput(pressed: CombatButtons.Special); inputs[1] = new CombatInput(pressed: CombatButtons.Dash);
            var result = Record(inputs).LastResult;
            Assert.That(result.AcceptedHits, Is.Zero);
            var cancel = result.CommandOutcomes.Single(value => value.Outcome.State == CombatCommandState.Cancelled && value.Outcome.Command.Kind == CombatCommandKind.Special);
            Assert.That(cancel.Outcome.Reason, Is.EqualTo(CombatCommandReason.Interrupted));
            Assert.That(result.CommandOutcomes.Any(value => value.Outcome.State == CombatCommandState.Started && value.Outcome.Command.Kind == CombatCommandKind.Dash), Is.True);
            Assert.That(result.Ticks.All(tick => tick.Resources.Slot0.Available == tick.Resources.Slot0.Capacity), Is.True);
            Assert.That(result.Ticks.All(tick => tick.Resources.Slot0.NextReady == 0), Is.True);
        }
        [Test] public void DifferentCommandOutcomesAreVisibleEvenWhenNeitherRunHits()
        {
            arena.enemies[0].Position = new Vector2(35, 0); Physics2D.SyncTransforms();
            var firstInputs = new CombatInput[40]; firstInputs[0] = new CombatInput(pressed: CombatButtons.Special); firstInputs[1] = new CombatInput(pressed: CombatButtons.Dash);
            var cancelled = Record(firstInputs).LastResult;
            var secondInputs = new CombatInput[40]; secondInputs[0] = new CombatInput(pressed: CombatButtons.Special); var completed = Record(secondInputs).LastResult;
            Assert.That(cancelled.AcceptedHits, Is.Zero); Assert.That(completed.AcceptedHits, Is.Zero);
            Assert.That(cancelled.CommandTraceHash, Is.Not.EqualTo(completed.CommandTraceHash)); Assert.That(cancelled.ResourceTraceHash, Is.Not.EqualTo(completed.ResourceTraceHash));
            Assert.That(cancelled.TraceHash, Is.Not.EqualTo(completed.TraceHash));
        }
        sealed class DecoyHitQuery : IActorPlacementQuery
        {
            readonly CombatArena arena;
            readonly bool replace;
            readonly UnityActorSurfaceQuery native = new UnityActorSurfaceQuery(_ => false);
            int lastTick = -1;
            public DecoyHitQuery(CombatArena arena, bool replace = true) { this.arena = arena; this.replace = replace; }
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request)
            {
                int tick = (int)arena.SimulationTickCount;
                if (tick != lastTick && tick < 2)
                {
                    lastTick = tick;
                    if (tick == 0 || replace) arena.SpawnDecoy(arena.Player, new Vector2(10 + tick, 0), new EffectData { hp = 100, duration = 10 });
                    Assert.That(arena.decoys[0].Receive(0, arena.enemies[0], 1, false, null), Is.True);
                }
                return native.BoxCast(request);
            }
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) => native.OverlapBox(request);
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) => native.Raycast(request);
        }
        [Test] public void SequentialReplacementDecoysRetainDistinctRawTargetsAndDeterministicActorRelations()
        {
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/actual-spawned-hit-identities");
            var field = typeof(CombatActor).GetField("surfaces", BindingFlags.Instance | BindingFlags.NonPublic); var original = field.GetValue(arena.Player);
            try
            {
                field.SetValue(arena.Player, new DecoyHitQuery(arena)); var first = session.RecordManual(initial, .02f, new CombatInput[3]);
                Assert.That(first.AcceptedHitFacts.Count, Is.EqualTo(2), "Both zero-damage hits were actually accepted.");
                Assert.That(first.AcceptedHitFacts.All(value => value.TargetActorKey == null && value.Hit.Damage == 0), Is.True);
                Assert.That(first.AcceptedHitFacts[0].Hit.Target.Equals(first.AcceptedHitFacts[1].Hit.Target), Is.False);
                Assert.That(first.HitIdentities[0].TargetActorOrdinal, Is.Not.EqualTo(first.HitIdentities[1].TargetActorOrdinal));
                Assert.That(first.HitIdentities.All(value => value.TargetActorOrdinal > initial.Actors.Count), Is.True);
                Assert.That(first.HitIdentities[0].SourceActorOrdinal, Is.EqualTo(first.HitIdentities[1].SourceActorOrdinal));
                foreach (var actor in Object.FindObjectsByType<CombatActor>(FindObjectsSortMode.None).Where(actor => actor.isDecoy)) Object.DestroyImmediate(actor.gameObject);
                arena.decoys.Clear(); // Native Destroy is deferred; the next synchronous experiment must not see stale unregistered technical views.
                field.SetValue(arena.Player, new DecoyHitQuery(arena)); var replay = session.Replay();
                Assert.That(replay.IdentityTraceHash, Is.EqualTo(first.IdentityTraceHash));
                Assert.That(replay.HitIdentities.Select(value => value.TargetActorOrdinal), Is.EqualTo(first.HitIdentities.Select(value => value.TargetActorOrdinal)));
                Assert.That(replay.AcceptedHitFacts[0].Hit.Target.Equals(first.AcceptedHitFacts[0].Hit.Target), Is.False, "Runtime IDs differ while the actual relationship remains comparable.");
                foreach (var actor in Object.FindObjectsByType<CombatActor>(FindObjectsSortMode.None).Where(actor => actor.isDecoy)) Object.DestroyImmediate(actor.gameObject);
                arena.decoys.Clear(); field.SetValue(arena.Player, new DecoyHitQuery(arena, false)); var sameTarget = session.Replay();
                Assert.That(sameTarget.AcceptedHitFacts.Count, Is.EqualTo(2)); Assert.That(sameTarget.HitTraceHash, Is.EqualTo(first.HitTraceHash), "The existing hit-value trace keeps its old meaning.");
                Assert.That(sameTarget.IdentityTraceHash, Is.Not.EqualTo(first.IdentityTraceHash), "Two accepted facts on one target must not compare as two successive spawned targets.");
                Assert.That(sameTarget.HitIdentities[0].TargetActorOrdinal, Is.EqualTo(sameTarget.HitIdentities[1].TargetActorOrdinal));
            }
            finally { field.SetValue(arena.Player, original); }
        }
        sealed class DamageThenFailQuery : IActorPlacementQuery
        {
            readonly CombatArena arena;
            bool fired;
            public DamageThenFailQuery(CombatArena arena) { this.arena = arena; }
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request)
            {
                if (!fired) { fired = true; arena.enemies[0].Receive(0, arena.Player, 1, false, null); }
                throw new InvalidOperationException("actual Native query failed after committed accepted hit");
            }
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) => Array.Empty<ActorSurfaceBounds>();
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) => Array.Empty<ActorRayContact>();
        }
        [Test] public void FaultedAttemptRetainsPartialActualFactsWithoutPublishingAnotherSuccessfulTickOrReplacingOldResult()
        {
            var session = Record(SpecialThenCooldown()); var prior = session.LastResult; var selected = session.Scenario;
            var field = typeof(CombatActor).GetField("surfaces", BindingFlags.Instance | BindingFlags.NonPublic); var query = field.GetValue(arena.Player);
            field.SetValue(arena.Player, new DamageThenFailQuery(arena));
            try
            {
                var failure = Assert.Throws<InvalidOperationException>(() => session.Replay()); Assert.That(failure.Message, Does.Contain("actual Native query failed"));
                Assert.That(session.LastResult, Is.SameAs(prior)); Assert.That(session.Scenario, Is.SameAs(selected));
                Assert.That(session.LastFailure, Is.Not.Null); Assert.That(session.LastFailure.FaultedTick, Is.Zero); Assert.That(session.LastFailure.CompletedTicks, Is.Zero);
                Assert.That(session.LastFailure.AcceptedHitFacts.Count, Is.EqualTo(1)); Assert.That(session.LastFailure.AcceptedHitFacts[0].Hit.Damage, Is.Zero);
                Assert.That(session.LastFailure.CommandOutcomes.Any(value => value.Outcome.State == CombatCommandState.Started && value.Outcome.Command.Kind == CombatCommandKind.Special), Is.True);
                Assert.That(session.LastFailure.CommandOutcomes.All(value => value.Outcome.EventSequence > 0 && value.Outcome.Command.IsValid), Is.True);
                Assert.That(session.LastFailure.Error, Does.Contain("actual Native query failed")); Assert.That(session.LastFailure.ObservationError, Is.Null);
                Assert.That(arena.ManualSimulationActive, Is.False); Assert.That(arena.SimulationFaulted, Is.False, "The existing finally reset still restores a usable arena.");
            }
            finally { field.SetValue(arena.Player, query); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualWindowShowsResourceAndOutcomeTracesAndHonestFailedAttempt()
        {
            var window = ScriptableObject.CreateInstance<ExperimentScenarioWindow>(); window.name = "ObservationTestWindow"; window.Show(); window.CreateGUI(); yield return null;
            window.SetManualInputs(.02f, SpecialThenCooldown()); Click(window, "captureInitial"); Click(window, "recordManual");
            Assert.That(window.rootVisualElement.Q<Label>("results").text, Does.Contain("Resource trace").And.Contain("command trace").And.Contain("Actual outcomes").And.Contain("Final charges"));
            var prior = window.LastResult;
            var field = typeof(CombatActor).GetField("surfaces", BindingFlags.Instance | BindingFlags.NonPublic); var query = field.GetValue(arena.Player);
            field.SetValue(arena.Player, new DamageThenFailQuery(arena));
            try
            {
                Click(window, "replay"); Assert.That(window.LastResult, Is.SameAs(prior)); Assert.That(window.LastFailure, Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Label>("results").text, Does.Contain("FAILED attempt at tick 0").And.Contain("completed ticks 0").And.Contain("Prior successful recording/result retained"));
                Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally { field.SetValue(arena.Player, query); }
        }
        static void Click(ExperimentScenarioWindow window, string id)
        {
            var button = window.rootVisualElement.Q<Button>(id); Assert.That(button, Is.Not.Null);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
