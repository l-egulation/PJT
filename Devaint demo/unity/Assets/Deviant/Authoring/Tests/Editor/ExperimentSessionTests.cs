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
using Deviant.Control.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class ExperimentSessionTests
    {
        CombatArena arena;
        string directory;
        Catalog CatalogContext => arena.catalog;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "deviant-lab-scenario-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; arena = Object.FindAnyObjectByType<CombatArena>();
            arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); arena.Player.health = 84;
            arena.Player.selected = 0; int sword = Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword");
            foreach (var weapon in arena.Player.weapons) { weapon.Select(sword, 0); weapon.StatSlots.Clear(); }
            for (int i = 0; i < arena.enemies.Count; i++) { arena.enemies[i].aggressive = false; arena.enemies[i].Position = new Vector2(i == 0 ? 3 : 35, 0); }
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            var failures = new List<Exception>();
            foreach (var window in Resources.FindObjectsOfTypeAll<ExperimentScenarioWindow>().Where(window => window.name.StartsWith("ScenarioTest")))
                try { window.Close(); } catch (Exception error) { failures.Add(error); }
            if (Application.isPlaying) yield return new ExitPlayMode();
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch (Exception error) { failures.Add(error); }
            if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException("Scenario fixture cleanup failed after leaving Play Mode.", failures);
        }
        static CombatInput[] Inputs(int count = 67)
        {
            var inputs = new CombatInput[count]; inputs[0] = new CombatInput(0, CombatButtons.Basic);
            if (count > 1) inputs[1] = new CombatInput(released: CombatButtons.Basic);
            if (count > 30) inputs[30] = new CombatInput(0, CombatButtons.Dash);
            return inputs;
        }
        ExperimentSession Record(out ExperimentInitialState initial)
        {
            var session = new ExperimentSession(arena); initial = session.CaptureInitial("lab/reusable-test", 123, "explicit fixture provenance only");
            session.RecordManual(initial, .02f, Inputs()); return session;
        }
        [Test] public void FreshCaptureValidatesImmediatelyAndAcrossEquivalentWholeStatePublicationWithoutMutation()
        {
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/frame-media-baseline");
            float now = arena.Now; var position = arena.Player.Position; float health = arena.Player.health; var running = arena.Player.currentCast;
            int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try
            {
                session.ValidateInitial(initial, initial.Versions);
                var versions = initial.Versions;
                Assert.That(arena.TryApplyMeleeFrameState(versions.Basics, versions.DirectSpecials, versions.Motion, versions.MotionRegistry, versions.Frame, versions.MeleeFrames.ToDocument(), false, out var error), Is.True, error);
                session.ValidateInitial(initial, versions);
                Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(versions.ContentHash));
                Assert.That(resets, Is.Zero); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.Position, Is.EqualTo(position));
                Assert.That(arena.Player.health, Is.EqualTo(health)); Assert.That(arena.Player.currentCast, Is.SameAs(running)); Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally { arena.ResetCompleted -= count; }
        }
        [Test] public void FreshReloadReplaysTwiceWithSameObservedStateAndHitOrderWithoutRecreatingTheLab()
        {
            var session = Record(out var initial); var first = session.LastResult; string path = Path.Combine(directory, "fresh.json");
            ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext);
            var actorObjects = new[] { arena.Player.gameObject }.Concat(arena.enemies.Select(enemy => enemy.gameObject)).ToArray();
            var surfaceObjects = Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None).OrderBy(surface => surface.name).ToArray();
            arena.Player.Position = new Vector2(8, 1); arena.Player.health = 43; arena.Player.selected = 1; arena.Player.weapons[1].StatSlots[0] = 4;
            var replay = new ExperimentSession(arena); replay.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _));
            var a = replay.Replay(); var b = replay.Replay();
            Assert.That(a.TraceHash, Is.EqualTo(first.TraceHash)); Assert.That(b.TraceHash, Is.EqualTo(a.TraceHash));
            Assert.That(a.Ticks.SelectMany(tick => tick.Actors).Select(actor => (actor.Key, actor.AcceptedHits, actor.State.Vitals.Health)),
                Is.EqualTo(b.Ticks.SelectMany(tick => tick.Actors).Select(actor => (actor.Key, actor.AcceptedHits, actor.State.Vitals.Health))));
            Assert.That(a.DiagnosticEvents, Is.Not.Empty);
            Assert.That(a.DiagnosticEvents.Select(observed => (observed.Tick, observed.Message)), Is.EqualTo(b.DiagnosticEvents.Select(observed => (observed.Tick, observed.Message))));
            Assert.That(a.AcceptedHits, Is.GreaterThan(0)); Assert.That(a.HealthLost, Is.EqualTo(10)); Assert.That(a.Ticks.Count, Is.EqualTo(67));
            Assert.That(a.HitTraceHash, Is.EqualTo(first.HitTraceHash)); Assert.That(b.HitTraceHash, Is.EqualTo(a.HitTraceHash));
            Assert.That(a.AcceptedHitFacts.Count, Is.EqualTo(a.AcceptedHits));
            var hit = a.AcceptedHitFacts.First(); Assert.That(hit.HasStart, Is.True); Assert.That(hit.AdmissionTick, Is.Zero);
            Assert.That(hit.Start.ExecutionId, Is.EqualTo(hit.Hit.Origin.ExecutionId)); Assert.That(hit.Start.Command.Equals(hit.Hit.Origin.Command), Is.True);
            Assert.That(hit.StartToHit, Is.GreaterThanOrEqualTo(0)); Assert.That(hit.InputToStart, Is.Zero);
            Assert.That(hit.Hit.TickCount, Is.EqualTo(a.FirstHitTick));
            Assert.That(arena.Player.health, Is.EqualTo(43)); Assert.That(arena.Player.Position, Is.EqualTo(new Vector2(8, 1)));
            Assert.That(arena.Player.selected, Is.EqualTo(1)); Assert.That(arena.Player.weapons[1].StatSlots[0], Is.EqualTo(4));
            Assert.That(new[] { arena.Player.gameObject }.Concat(arena.enemies.Select(enemy => enemy.gameObject)), Is.EqualTo(actorObjects));
            Assert.That(Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None).OrderBy(surface => surface.name), Is.EqualTo(surfaceObjects));
            Assert.That(ExperimentSession.CaptureGeometry(arena).ContentHash, Is.EqualTo(initial.Geometry.ContentHash));
            Assert.That(arena.ManualSimulationActive, Is.False); Assert.That(arena.SimulationFaulted, Is.False);
        }
        [Test] public void ChangedActionGeometryIsALegitimateABResultAndAcceptedZeroDamageHitsStillCount()
        {
            var session = Record(out var initial); var a = initial.Versions; var data = a.FixedSword.ToData(); data.box.width = .2f; data.box.x = .3f;
            Assert.That(BasicMeleeDefinition.TryCreate(a.FixedSword.Id, data, out var narrow, out var error), Is.True, error);
            var b = ReplaceFirst(a, narrow, CatalogContext); var pair = session.Compare(a, b);
            Assert.That(pair.A.AcceptedHits, Is.GreaterThan(0)); Assert.That(pair.B.AcceptedHits, Is.Zero); Assert.That(pair.A.HealthLost, Is.EqualTo(10)); Assert.That(pair.B.HealthLost, Is.Zero);
            Assert.That(pair.A.Scenario.Recording.ContentHash, Is.EqualTo(pair.B.Scenario.Recording.ContentHash));
            Assert.That(pair.A.Scenario.Initial.Geometry.ContentHash, Is.EqualTo(pair.B.Scenario.Initial.Geometry.ContentHash));
            data = a.FixedSword.ToData(); data.damage = 0;
            Assert.That(BasicMeleeDefinition.TryCreate(a.FixedSword.Id, data, out var zero, out error), Is.True, error);
            pair = session.Compare(a, ReplaceFirst(a, zero, CatalogContext));
            Assert.That(pair.B.HealthLost, Is.Zero); Assert.That(pair.B.AcceptedHits, Is.EqualTo(pair.A.AcceptedHits)); Assert.That(pair.B.FirstHitTick, Is.EqualTo(pair.A.FirstHitTick));
            Assert.That(pair.B.AcceptedHitFacts.All(hit => hit.Hit.Damage == 0 && hit.Hit.After.Health == hit.Hit.Before.Health), Is.True);
            Assert.That(pair.B.AcceptedHitFacts.All(hit => hit.HasStart && hit.Start.ExecutionId == hit.Hit.Origin.ExecutionId), Is.True);
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(a.ContentHash)); Assert.That(arena.ManualSimulationActive, Is.False);
        }
        static ExperimentVersions ReplaceFirst(ExperimentVersions source, BasicMeleeDefinition first, Catalog catalog) => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics.Select(action => action.Id == first.Id ? first : action), source.Motion, null, source.ActionTracks, source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry,
            new MeleeFrameCatalog(source.MeleeFrames.ToSourceDocument(), catalog, source.MotionRegistry, source.Motion, null, source.Basics.Select(action => action.Id == first.Id ? first : action), source.DirectSpecials), source.SpecialFrames);
        [TestCase("terrain")] [TestCase("rotation")] [TestCase("rounded")] [TestCase("dynamic")] [TestCase("content")] [TestCase("fixture")]
        [TestCase("hurt-transform")] [TestCase("hurt-offset")] [TestCase("hurt-region")] [TestCase("actor-body")]
        public void MismatchedTerrainContentOrUnsupportedGeometryRejectBeforeAnyResetAndRetainOldSelection(string change)
        {
            var session = Record(out _); var selected = session.Scenario; var previousResult = session.LastResult; int resets = 0; Action count = () => resets++;
            var wall = Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None).Single(surface => surface.name == "Wall");
            string originalName = arena.enemies[0].name; float originalSpeed = arena.catalog.forms[0].inputSpeed;
            Rigidbody2D body = null;
            try
            {
                if (change == "terrain") wall.transform.position += Vector3.right;
                if (change == "rotation") wall.transform.rotation = Quaternion.Euler(0, 0, 15);
                if (change == "rounded") wall.GetComponent<BoxCollider2D>().edgeRadius = .1f;
                if (change == "dynamic") body = wall.gameObject.AddComponent<Rigidbody2D>();
                if (change == "content") arena.catalog.forms[0].inputSpeed += .1f;
                if (change == "fixture") arena.enemies[0].name = "NotAnExplicitFixtureKey";
                if (change == "hurt-transform") arena.enemies[0].hurtCollider.transform.localPosition = Vector3.right;
                if (change == "hurt-offset") arena.enemies[0].hurtCollider.offset += Vector2.right;
                if (change == "hurt-region") arena.enemies[0].hurtCollider.GetComponent<HurtRegion>().actor = arena.enemies[1];
                if (change == "actor-body") arena.enemies[0].GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
                arena.Player.Position = new Vector2(7, 0); arena.Player.health = 42; Physics2D.SyncTransforms(); arena.ResetCompleted += count;
                Assert.Throws<InvalidOperationException>(() => session.Select(selected)); Assert.Throws<InvalidOperationException>(() => session.Replay());
                Assert.That(resets, Is.Zero); Assert.That(session.Scenario, Is.SameAs(selected)); Assert.That(session.LastResult, Is.SameAs(previousResult));
                Assert.That(arena.Player.Position, Is.EqualTo(new Vector2(7, 0))); Assert.That(arena.Player.health, Is.EqualTo(42)); Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally
            {
                arena.ResetCompleted -= count; arena.enemies[0].name = originalName; arena.catalog.forms[0].inputSpeed = originalSpeed;
                if (body) Object.DestroyImmediate(body);
            }
        }
        [Test] public void ExistingManualInputBlockAndPauseOwnersAreNeverReleasedOrOverridden()
        {
            var session = Record(out _); int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try
            {
                using (var manual = arena.AcquireManualSimulation())
                { Assert.Throws<InvalidOperationException>(() => session.Replay()); Assert.That(arena.ManualSimulationActive, Is.True); manual.Step(.02f); }
                using (var owner = arena.Controls.OpenScope("external scenario blocker"))
                {
                    using (var block = owner.BlockInput(InputChannels.Combat))
                    { Assert.Throws<InvalidOperationException>(() => session.Replay()); Assert.That(block.IsActive, Is.True); Assert.That(arena.Player.InputBlocked, Is.True); }
                    using (var pause = owner.PauseSimulation())
                    { Assert.Throws<InvalidOperationException>(() => session.Replay()); Assert.That(pause.IsActive, Is.True); Assert.That(arena.Controls.Current.SimulationPaused, Is.True); }
                }
                Assert.That(resets, Is.Zero); Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally { arena.ResetCompleted -= count; }
        }
        sealed class FaultQuery : IActorPlacementQuery
        {
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) => throw new InvalidOperationException("actual common tick fault");
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) => Array.Empty<ActorSurfaceBounds>();
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) => Array.Empty<ActorRayContact>();
        }
        [Test] public void FaultedTickCannotPublishRecordingAndCleanupRestoresConfigurationAndItsLease()
        {
            var session = Record(out var initial); var prior = session.Scenario; var last = session.LastResult;
            var query = typeof(CombatActor).GetField("surfaces", BindingFlags.Instance | BindingFlags.NonPublic); var original = query.GetValue(arena.Player);
            query.SetValue(arena.Player, new FaultQuery());
            try
            {
                var failure = Assert.Throws<InvalidOperationException>(() => session.RecordManual(initial, .02f, Inputs(9)));
                Assert.That(failure.Message, Does.Contain("actual common tick fault")); Assert.That(session.Scenario, Is.SameAs(prior)); Assert.That(session.LastResult, Is.SameAs(last));
                Assert.That(arena.SimulationTickCount, Is.Zero); Assert.That(arena.SimulationFaulted, Is.False); Assert.That(arena.ManualSimulationActive, Is.False);
                Assert.That(arena.Player.health, Is.EqualTo(84)); Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.casts, Is.Empty);
            }
            finally { query.SetValue(arena.Player, original); }
        }
        [Test] public void FailureEnteringBDoesNotPublishPartialPairAndRestoresOriginalDefinitions()
        {
            var session = Record(out var initial); var last = session.LastResult; int resets = 0;
            Action failOnce = () => { if (++resets == 3) throw new InvalidOperationException("B initialization failure"); }; arena.ResetCompleted += failOnce;
            try { Assert.Throws<InvalidOperationException>(() => session.Compare(initial.Versions, initial.Versions)); }
            finally { arena.ResetCompleted -= failOnce; }
            Assert.That(session.LastA, Is.Null); Assert.That(session.LastB, Is.Null); Assert.That(session.LastResult, Is.SameAs(last));
            Assert.That(resets, Is.GreaterThan(3)); Assert.That(arena.ManualSimulationActive, Is.False); Assert.That(arena.SimulationFaulted, Is.False);
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(initial.Versions.ContentHash));
        }
        [Test] public void SavedFrameSpecialTracksMovementAndEquipmentRestoreInAFreshSession()
        {
            Assert.That(arena.TryGetBasicSwordMotion(out var motion, out var error), Is.True, error);
            var fixedSword = arena.FrameSwordTiming?.SourceAction ?? arena.BasicSwordFirst;
            float inputSpeed = arena.catalog.forms.Single(form => form.id == "base_sword").inputSpeed;
            var frame = MeleeFrameCompiler.CompileFirst(fixedSword, motion, MeleeFrameCompiler.ProjectFirst(fixedSword, motion, inputSpeed), inputSpeed);
            Assert.That(arena.TryApplySwordFrameTiming(frame, false, out error), Is.True, error);
            var track = arena.GetActionTracks(ActionTrackDefinition.FollowUpId); var trackDocument = track.ToDocument(); trackDocument.hits[0].box.width += .2f;
            var modified = track.Recompile(trackDocument);
            Assert.That(arena.TryApplyActionTracks(new[] { arena.GetActionTracks(ActionTrackDefinition.MovementId), modified }, false, out error), Is.True, error);
            var movement = arena.Movement.ToData(); movement.dashSpeed = 8;
            Assert.That(arena.TryApplyMovementAndReset(movement, out error), Is.True, error);
            arena.Player.weapons[0].StatSlots[0] = 3; arena.Player.selected = 1; arena.Player.SyncFacing(-1);
            var session = Record(out var captured); string path = Path.Combine(directory, "all-versions.json");
            ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext); string expected = session.LastResult.TraceHash;
            Assert.That(arena.TryRestoreSwordFixedTiming(false, out error), Is.True, error);
            var prior = ExperimentSession.CaptureVersions(arena);
            var loaded = new ExperimentSession(arena); loaded.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _)); var replayed = loaded.Replay();
            Assert.That(replayed.TraceHash, Is.EqualTo(expected)); Assert.That(replayed.Versions.Frame.ContentHash, Is.EqualTo(frame.ContentHash));
            Assert.That(replayed.Versions.ActionTracks.Single(action => action.Id == modified.Id).ContentHash, Is.EqualTo(modified.ContentHash));
            Assert.That(replayed.Scenario.Initial.Weapons[0].StatSlots[0], Is.EqualTo(3)); Assert.That(captured.SelectedWeapon, Is.EqualTo(1));
            Assert.That(arena.FrameSwordTiming, Is.Null); Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(prior.ContentHash));
        }
        void SelectTrackedSpecial(string id)
        {
            int form = Array.FindIndex(arena.catalog.forms, candidate => candidate.specials != null && candidate.specials.Any(skill => skill.id == id));
            Assert.That(form, Is.GreaterThanOrEqualTo(0));
            int special = Array.FindIndex(arena.catalog.forms[form].specials, skill => skill.id == id);
            arena.Player.weapons[0].Select(form, special); arena.Player.selected = 0; arena.Player.health = 84;
            arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1);
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            arena.enemies[0].Position = new Vector2(5, 0); Physics2D.SyncTransforms();
        }
        static CombatInput[] SpecialInputs(int count = 65, int swapTick = -1)
        {
            var inputs = new CombatInput[count]; inputs[0] = new CombatInput(0, CombatButtons.Special);
            inputs[1] = new CombatInput(released: CombatButtons.Special);
            if (swapTick >= 0) inputs[swapTick] = new CombatInput(0, CombatButtons.Swap);
            return inputs;
        }
        static ExperimentVersions ReplaceTrack(ExperimentVersions source, ActionTrackDefinition replacement) => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks.Select(action => action.Id == replacement.Id ? replacement : action), source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames,
            ExperimentScenarioTests.RebuildSpecialFrames(source, finite: source.ActionTracks.Select(action => action.Id == replacement.Id ? replacement : action)));
        [Test] public void CompleteSnapshotsReplayWaveVersionABAndRestoreBothEffectAndDefenseDefinitions()
        {
            SelectTrackedSpecial(ActionTrackDefinition.WaveId);
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/full-wave-graphs");
            var a = initial.Versions; Assert.That(a.ActionTracks.Count, Is.EqualTo(ActionTrackDefinition.SupportedIds.Count));
            var wave = a.ActionTracks.Single(action => action.Id == ActionTrackDefinition.WaveId); var document = wave.ToDocument();
            document.timeline.frames[0].seconds += .1f; document.hits[0].box.height = .25f;
            var shortWave = wave.Recompile(document); var b = ReplaceTrack(a, shortWave);
            session.RecordManual(initial, .02f, SpecialInputs());
            Assert.That(session.LastResult.HealthLost, Is.EqualTo(40)); Assert.That(session.LastResult.AcceptedHits, Is.EqualTo(1));
            string path = Path.Combine(directory, "all-wave-graphs.json"); ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext);
            var guard = a.ActionTracks.Single(action => action.Id == ActionTrackDefinition.GuardId); document = guard.ToDocument();
            document.timeline.markers.Single(marker => marker.id == "guard-end").offset = .5f; var shortGuard = guard.Recompile(document);
            Assert.That(arena.TryApplyActionTracks(new[] { shortWave, shortGuard }, false, out var error), Is.True, error);
            var prior = ExperimentSession.CaptureVersions(arena); var fresh = new ExperimentSession(arena);
            fresh.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _)); var replayed = fresh.Replay();
            Assert.That(replayed.TraceHash, Is.EqualTo(session.LastResult.TraceHash));
            var pair = fresh.Compare(a, b);
            Assert.That(pair.A.AcceptedHits, Is.EqualTo(1)); Assert.That(pair.A.HealthLost, Is.EqualTo(40));
            Assert.That(pair.B.AcceptedHits, Is.EqualTo(1)); Assert.That(pair.B.HealthLost, Is.EqualTo(40));
            Assert.That(pair.B.FirstHitTick, Is.GreaterThan(pair.A.FirstHitTick)); Assert.That(pair.B.TraceHash, Is.Not.EqualTo(pair.A.TraceHash));
            Assert.That(pair.A.Scenario.Initial.ContentHash, Is.EqualTo(pair.B.Scenario.Initial.ContentHash));
            Assert.That(pair.A.Scenario.Recording.ContentHash, Is.EqualTo(pair.B.Scenario.Recording.ContentHash));
            foreach (var action in prior.ActionTracks) Assert.That(arena.GetActionTracks(action.Id).ContentHash, Is.EqualTo(action.ContentHash));
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(prior.ContentHash)); Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [Test] public void CompleteSnapshotsReplayGuardDurationABAndPinCapturedDefinitionsAfterCurrentEdits()
        {
            SelectTrackedSpecial(ActionTrackDefinition.GuardId);
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/full-defense-graphs"); var a = initial.Versions;
            var guard = a.ActionTracks.Single(action => action.Id == ActionTrackDefinition.GuardId); var document = guard.ToDocument();
            document.timeline.markers.Single(marker => marker.id == "guard-end").offset = .5f; var shortGuard = guard.Recompile(document); var b = ReplaceTrack(a, shortGuard);
            var recorded = session.RecordManual(initial, .02f, SpecialInputs());
            Assert.That(recorded.Ticks.Last().Actors[0].State.Vitals.Shield, Is.EqualTo(30));
            var pair = session.Compare(a, b);
            Assert.That(pair.A.Ticks.Last().Actors[0].State.Vitals.Shield, Is.EqualTo(30)); Assert.That(pair.B.Ticks.Last().Actors[0].State.Vitals.Shield, Is.Zero);
            Assert.That(pair.A.Ticks.Last().Actors[0].State.Vitals.GuardUntil, Is.GreaterThan(3));
            Assert.That(pair.B.Ticks.Last().Actors[0].State.Vitals.GuardUntil, Is.LessThan(1));
            Assert.That(pair.A.Ticks.SelectMany(tick => tick.Actors).Where(actor => actor.State.Action.ContentId == guard.Id).Select(actor => actor.State.Action.DefinitionVersion).Distinct(), Is.EqualTo(new[] { guard.ContentHash }));
            Assert.That(arena.TryApplyActionTracks(new[] { shortGuard }, false, out var error), Is.True, error);
            var replayed = session.Replay(); Assert.That(replayed.TraceHash, Is.EqualTo(recorded.TraceHash));
            Assert.That(arena.GetActionTracks(guard.Id).ContentHash, Is.EqualTo(shortGuard.ContentHash));
            Assert.That(session.Scenario.Initial.Versions.ActionTracks.Single(action => action.Id == guard.Id).ContentHash, Is.EqualTo(guard.ContentHash));
            Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [TestCase(ActionTrackDefinition.WaveId)] [TestCase(ActionTrackDefinition.GuardId)]
        public void MissingCurrentCatalogTrackSourceRejectsCaptureAndReplayBeforeMutation(string id)
        {
            var session = Record(out _); var previous = session.Scenario; var last = session.LastResult;
            var source = arena.catalog.forms.SelectMany(form => form.specials).Single(skill => skill.id == id); var document = source.actionTracks;
            int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try
            {
                source.actionTracks = null; arena.Player.Position = new Vector2(7, 0); arena.Player.health = 42;
                Assert.Throws<InvalidOperationException>(() => session.CaptureInitial("lab/invalid-current-source"));
                Assert.Throws<InvalidOperationException>(() => session.Select(previous)); Assert.Throws<InvalidOperationException>(() => session.Replay());
                Assert.That(resets, Is.Zero); Assert.That(arena.Player.Position, Is.EqualTo(new Vector2(7, 0))); Assert.That(arena.Player.health, Is.EqualTo(42));
                Assert.That(session.Scenario, Is.SameAs(previous)); Assert.That(session.LastResult, Is.SameAs(last)); Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally { source.actionTracks = document; arena.ResetCompleted -= count; }
        }
        [TestCase("base_sword_default")] [TestCase("rapier_range")] [TestCase("greatsword_range")]
        public void SavedDirectSpecialSnapshotsReplayCapturedDamageAndRestoreEveryPriorFamily(string id)
        {
            SelectTrackedSpecial(id); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/full-direct/" + id); var a = initial.Versions;
            Assert.That(a.DirectSpecials.Select(action => action.Id), Is.EqualTo(DirectMeleeSpecialDefinition.SupportedIds.OrderBy(value => value, StringComparer.Ordinal)));
            var action = a.DirectSpecials.Single(candidate => candidate.Id == id); var values = action.ToBasicData(); values.damage += 7;
            var edited = action.Recompile(values);
            var direct = a.DirectSpecials.Select(candidate => candidate.Id == id ? edited : candidate).ToArray();
            var b = new ExperimentVersions(a.CatalogHash, a.MediaHash, a.Movement, a.Basics, a.Motion, a.Frame, a.ActionTracks, direct, a.ChargedSpecials, a.AuthoredSkills, a.RangedActions, a.MotionRegistry, ExperimentScenarioTests.RebuildFrames(a, direct: direct), a.SpecialFrames);
            int count = Math.Max(100, (int)Math.Ceiling(action.Duration / .02f) + 5); var recorded = session.RecordManual(initial, .02f, SpecialInputs(count));
            Assert.That(recorded.AcceptedHits, Is.EqualTo(1)); Assert.That(recorded.HealthLost, Is.EqualTo(action.Damage).Within(.00001f));
            string path = Path.Combine(directory, id + ".json"); ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext);
            var currentDirect = a.DirectSpecials.Select(candidate => {
                var numeric = candidate.ToBasicData(); numeric.damage += 2; return candidate.Recompile(numeric);
            }).ToArray();
            Assert.That(arena.TryApplyDirectMeleeSpecials(currentDirect, false, out var error), Is.True, error);
            var prior = ExperimentSession.CaptureVersions(arena); var loaded = new ExperimentSession(arena);
            loaded.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _)); var replay = loaded.Replay();
            Assert.That(replay.TraceHash, Is.EqualTo(recorded.TraceHash)); Assert.That(replay.Versions.DirectSpecials.Single(candidate => candidate.Id == id).ContentHash, Is.EqualTo(action.ContentHash));
            var pair = loaded.Compare(a, b);
            Assert.That(pair.A.AcceptedHits, Is.EqualTo(1)); Assert.That(pair.B.AcceptedHits, Is.EqualTo(1));
            Assert.That(pair.A.HealthLost, Is.EqualTo(action.Damage).Within(.00001f)); Assert.That(pair.B.HealthLost, Is.EqualTo(action.Damage + 7).Within(.00001f));
            Assert.That(pair.A.Scenario.Recording.ContentHash, Is.EqualTo(pair.B.Scenario.Recording.ContentHash));
            Assert.That(pair.A.Ticks.SelectMany(tick => tick.Actors).Where(actor => actor.State.Action.ContentId == id).Select(actor => actor.State.Action.DefinitionVersion).Distinct(), Is.EqualTo(new[] { action.ContentHash }));
            Assert.That(pair.B.Ticks.SelectMany(tick => tick.Actors).Where(actor => actor.State.Action.ContentId == id).Select(actor => actor.State.Action.DefinitionVersion).Distinct(), Is.EqualTo(new[] { edited.ContentHash }));
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(prior.ContentHash));
            foreach (var candidate in currentDirect) Assert.That(arena.GetDirectMeleeSpecial(candidate.Id).ContentHash, Is.EqualTo(candidate.ContentHash));
            foreach (var candidate in prior.ActionTracks) Assert.That(arena.GetActionTracks(candidate.Id).ContentHash, Is.EqualTo(candidate.ContentHash));
            Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [TestCase("base_sword_default")] [TestCase("rapier_range")] [TestCase("greatsword_range")]
        public void MissingCurrentDirectSourceRejectsCaptureLoadAndReplayBeforeReset(string id)
        {
            var session = Record(out _); var selected = session.Scenario; var result = session.LastResult;
            var form = arena.catalog.forms.Single(candidate => candidate.specials.Any(skill => skill.id == id)); var prior = form.specials;
            int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try
            {
                form.specials = prior.Where(skill => skill.id != id).ToArray(); arena.Player.Position = new Vector2(7, 0); arena.Player.health = 42;
                Assert.Throws<InvalidOperationException>(() => session.CaptureInitial("lab/missing-direct-source"));
                Assert.Throws<InvalidOperationException>(() => session.Select(selected)); Assert.Throws<InvalidOperationException>(() => session.Replay());
                Assert.That(resets, Is.Zero); Assert.That(arena.Player.Position, Is.EqualTo(new Vector2(7, 0))); Assert.That(arena.Player.health, Is.EqualTo(42));
                Assert.That(session.Scenario, Is.SameAs(selected)); Assert.That(session.LastResult, Is.SameAs(result)); Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally { form.specials = prior; arena.ResetCompleted -= count; }
        }
        [Test] public void CapturedDirectPolicyMismatchRejectsBeforeAnyMutation()
        {
            var session = Record(out var initial); var source = initial.Versions; var action = source.DirectSpecials[0]; var values = action.ToData(); values.cooldown += 1;
            Assert.That(DirectMeleeSpecialDefinition.TryCreate(action.FormId, values, out var foreign, out var error), Is.True, error);
            var direct = source.DirectSpecials.Select(candidate => candidate.Id == action.Id ? foreign : candidate).ToArray();
            var bad = new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks, direct, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, ExperimentScenarioTests.RebuildFrames(source, direct: direct), source.SpecialFrames);
            int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try { Assert.Throws<InvalidOperationException>(() => session.Compare(source, bad)); Assert.That(resets, Is.Zero); Assert.That(arena.ManualSimulationActive, Is.False); }
            finally { arena.ResetCompleted -= count; }
        }
        static CombatInput[] ChargedInputs(bool maximum, int ticks = 180)
        {
            var inputs = new CombatInput[ticks]; inputs[0] = new CombatInput(0, CombatButtons.Special, maximum);
            if (maximum)
            {
                for (int tick = 1; tick < ticks - 1; tick++) inputs[tick] = new CombatInput(specialHeld: true);
                inputs[ticks - 1] = new CombatInput(released: CombatButtons.Special);
            }
            else inputs[1] = new CombatInput(released: CombatButtons.Special);
            return inputs;
        }
        [TestCase("rapier_default", false)] [TestCase("rapier_default", true)]
        [TestCase("greatsword_default", false)] [TestCase("greatsword_default", true)]
        public void SavedChargedSnapshotsReplayMinMaxInputsAndRestoreEveryDefinitionFamily(string id, bool maximum)
        {
            SelectTrackedSpecial(id); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/full-charge/" + id + "/" + maximum); var a = initial.Versions;
            Assert.That(a.ChargedSpecials.Select(action => action.Id), Is.EqualTo(ChargedMeleeDefinition.SupportedIds.OrderBy(value => value, StringComparer.Ordinal)));
            var action = a.ChargedSpecials.Single(candidate => candidate.Id == id); var numeric = action.ToNumericData(); numeric.minimumDamage += 5; numeric.maximumDamage += 9;
            var edited = action.Recompile(numeric);
            var b = new ExperimentVersions(a.CatalogHash, a.MediaHash, a.Movement, a.Basics, a.Motion, a.Frame, a.ActionTracks, a.DirectSpecials, a.ChargedSpecials.Select(candidate => candidate.Id == id ? edited : candidate), a.AuthoredSkills, a.RangedActions, a.MotionRegistry, a.MeleeFrames, a.SpecialFrames);
            var recorded = session.RecordManual(initial, .02f, ChargedInputs(maximum)); float damage = maximum ? action.Policy.MaximumDamage : action.Policy.MinimumDamage;
            Assert.That(recorded.AcceptedHits, Is.EqualTo(1)); Assert.That(recorded.HealthLost, Is.EqualTo(damage).Within(.00001f));
            Assert.That(recorded.AcceptedHitFacts.Single().HasStart, Is.True); Assert.That(recorded.AcceptedHitFacts.Single().AdmissionTick, Is.Zero);
            Assert.That(recorded.Scenario.Recording.Inputs[0].SpecialHeld, Is.EqualTo(maximum));
            Assert.That(recorded.Scenario.Recording.Inputs[maximum ? recorded.Ticks.Count - 1 : 1].Released, Is.EqualTo(CombatButtons.Special));
            string path = Path.Combine(directory, id + "-" + maximum + ".json"); ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext);
            var currentCharged = a.ChargedSpecials.Select(candidate => { var values = candidate.ToNumericData(); values.minimumDamage += 2; values.maximumDamage += 3; return candidate.Recompile(values); }).ToArray();
            Assert.That(arena.TryApplyChargedMeleeDefinitions(currentCharged, false, out var error), Is.True, error);
            var prior = ExperimentSession.CaptureVersions(arena); var loaded = new ExperimentSession(arena); loaded.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _));
            var replay = loaded.Replay(); Assert.That(replay.TraceHash, Is.EqualTo(recorded.TraceHash)); Assert.That(replay.HitTraceHash, Is.EqualTo(recorded.HitTraceHash));
            var pair = loaded.Compare(a, b);
            Assert.That(pair.A.AcceptedHits, Is.EqualTo(1)); Assert.That(pair.B.AcceptedHits, Is.EqualTo(1));
            Assert.That(pair.A.HealthLost, Is.EqualTo(damage).Within(.00001f)); Assert.That(pair.B.HealthLost, Is.EqualTo(damage + (maximum ? 9 : 5)).Within(.00001f));
            Assert.That(pair.A.Scenario.Recording.ContentHash, Is.EqualTo(pair.B.Scenario.Recording.ContentHash));
            Assert.That(pair.A.Ticks.SelectMany(tick => tick.Actors).Where(actor => actor.State.Action.ContentId == id).Select(actor => actor.State.Action.DefinitionVersion).Distinct(), Is.EqualTo(new[] { action.ContentHash }));
            Assert.That(pair.B.Ticks.SelectMany(tick => tick.Actors).Where(actor => actor.State.Action.ContentId == id).Select(actor => actor.State.Action.DefinitionVersion).Distinct(), Is.EqualTo(new[] { edited.ContentHash }));
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(prior.ContentHash));
            foreach (var candidate in currentCharged) Assert.That(arena.GetChargedMelee(candidate.Id).ContentHash, Is.EqualTo(candidate.ContentHash));
            foreach (var candidate in prior.DirectSpecials) Assert.That(arena.GetDirectMeleeSpecial(candidate.Id).ContentHash, Is.EqualTo(candidate.ContentHash));
            foreach (var candidate in prior.ActionTracks) Assert.That(arena.GetActionTracks(candidate.Id).ContentHash, Is.EqualTo(candidate.ContentHash));
            Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [TestCase("rapier_default")] [TestCase("greatsword_default")]
        public void MissingCurrentChargedSourceRejectsCaptureSelectionAndReplayBeforeReset(string id)
        {
            var session = Record(out _); var selected = session.Scenario; var result = session.LastResult;
            var form = arena.catalog.forms.Single(candidate => candidate.specials.Any(skill => skill.id == id)); var source = form.specials;
            int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try
            {
                form.specials = source.Where(skill => skill.id != id).ToArray(); arena.Player.Position = new Vector2(7, 0); arena.Player.health = 42;
                Assert.Throws<InvalidOperationException>(() => session.CaptureInitial("lab/missing-charged-source"));
                Assert.Throws<InvalidOperationException>(() => session.Select(selected)); Assert.Throws<InvalidOperationException>(() => session.Replay());
                Assert.That(resets, Is.Zero); Assert.That(arena.Player.Position, Is.EqualTo(new Vector2(7, 0))); Assert.That(arena.Player.health, Is.EqualTo(42));
                Assert.That(session.Scenario, Is.SameAs(selected)); Assert.That(session.LastResult, Is.SameAs(result)); Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally { form.specials = source; arena.ResetCompleted -= count; }
        }
        [Test] public void CapturedChargedPolicyMismatchRejectsBeforeAnyMutation()
        {
            var session = Record(out var initial); var source = initial.Versions; var action = source.ChargedSpecials[0]; var values = action.ToData(); values.cooldown += 1;
            Assert.That(ChargedMeleeDefinition.TryCreate(action.FormId, values, out var foreign, out var error), Is.True, error);
            var bad = new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks, source.DirectSpecials, source.ChargedSpecials.Select(candidate => candidate.Id == action.Id ? foreign : candidate), source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames);
            int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try { Assert.Throws<InvalidOperationException>(() => session.Compare(source, bad)); Assert.That(resets, Is.Zero); Assert.That(arena.ManualSimulationActive, Is.False); }
            finally { arena.ResetCompleted -= count; }
        }
        static ExperimentVersions ReplaceComplete(ExperimentVersions source, IReadOnlyList<AuthoredSkillDefinition> authored = null, RangedActionCatalog ranged = null, MotionSourceRegistry registry = null)
            => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks,
                source.DirectSpecials, source.ChargedSpecials, authored ?? source.AuthoredSkills, ranged ?? source.RangedActions, registry ?? source.MotionRegistry,
                ExperimentScenarioTests.RebuildFrames(source, registry: registry ?? source.MotionRegistry), ExperimentScenarioTests.RebuildSpecialFrames(source, authored: authored ?? source.AuthoredSkills, registry: registry ?? source.MotionRegistry));
        [TestCase("whip_default")] [TestCase("hammer_default")]
        public void SavedCompleteAuthoredSkillReplaysCapturedVersionAndRestoresEverySourceFamily(string id)
        {
            SelectTrackedSpecial(id); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
            arena.enemies[0].knockSpeed = -2.75f; // Preserve an exact signed inactive owner baseline, not only the default zero.
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/complete-authored/" + id); var a = initial.Versions;
            Assert.That(initial.Actors[1].InactiveKnockSpeed, Is.EqualTo(-2.75f));
            var action = a.AuthoredSkills.Single(candidate => candidate.Id == id); var values = action.ToNumericData(); values.hits[0].damage += 7;
            var edited = action.Recompile(values); var b = ReplaceComplete(a, a.AuthoredSkills.Select(candidate => candidate.Id == id ? edited : candidate).ToArray());
            var recorded = session.RecordManual(initial, .02f, SpecialInputs(150));
            Assert.That(recorded.AcceptedHits, Is.EqualTo(1)); Assert.That(recorded.HealthLost, Is.EqualTo(action.Execution.Hits[0].Damage).Within(.00001f));
            string path = Path.Combine(directory, id + "-full.json"); ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext);
            Assert.That(arena.TryApplyAuthoredSkills(new[] { edited }, false, out var error), Is.True, error); var prior = ExperimentSession.CaptureVersions(arena);
            var loaded = new ExperimentSession(arena); loaded.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _)); var replay = loaded.Replay();
            Assert.That(replay.TraceHash, Is.EqualTo(recorded.TraceHash)); Assert.That(replay.HitTraceHash, Is.EqualTo(recorded.HitTraceHash));
            var pair = loaded.Compare(a, b); Assert.That(pair.B.HealthLost, Is.EqualTo(pair.A.HealthLost + 7).Within(.00001f));
            Assert.That(pair.A.AcceptedHitFacts.Single().HasStart, Is.True); Assert.That(pair.B.AcceptedHitFacts.Single().HasStart, Is.True);
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(prior.ContentHash)); Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [TestCase("launcher_basic_0")] [TestCase("launcher_range_special")] [TestCase("shuriken_basic_special")]
        public void SavedCompleteRangedCatalogReplaysBasicAndSpecialsWithoutRecapturingCurrentValues(string id)
        {
            var action = arena.GetRangedAction(id); Assert.That(action, Is.Not.Null);
            int form = Array.FindIndex(arena.catalog.forms, entry => entry.id == action.FormId);
            arena.Player.weapons[0].Select(form, action.IsBasic ? 0 : action.Selection);
            arena.Player.selected = 0; arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(3, 0); foreach (var enemy in arena.enemies) enemy.aggressive = false; Physics2D.SyncTransforms();
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/complete-ranged/" + id); var a = initial.Versions;
            var values = a.RangedActions.Read(id).ToNumericData(); for (int hit = 0; hit < values.damages.Length; hit++) values.damages[hit] += 3;
            var edited = a.RangedActions.Edit(id, values); var b = ReplaceComplete(a, ranged: edited);
            var inputs = new CombatInput[120]; var button = action.IsBasic ? CombatButtons.Basic : CombatButtons.Special;
            inputs[0] = new CombatInput(pressed: button); inputs[1] = new CombatInput(released: button);
            var recorded = session.RecordManual(initial, .02f, inputs); Assert.That(recorded.AcceptedHits, Is.EqualTo(1));
            string path = Path.Combine(directory, id + "-ranged.json"); ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext);
            Assert.That(arena.TryApplyRangedActions(edited.Actions, false, out var error), Is.True, error); var prior = ExperimentSession.CaptureVersions(arena);
            var loaded = new ExperimentSession(arena); loaded.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _)); var replay = loaded.Replay();
            Assert.That(replay.TraceHash, Is.EqualTo(recorded.TraceHash)); Assert.That(replay.HitTraceHash, Is.EqualTo(recorded.HitTraceHash));
            var pair = loaded.Compare(a, b); Assert.That(pair.A.AcceptedHits, Is.EqualTo(1)); Assert.That(pair.B.AcceptedHits, Is.EqualTo(1));
            Assert.That(pair.B.HealthLost, Is.EqualTo(pair.A.HealthLost + 3).Within(.00001f)); Assert.That(pair.B.AcceptedHitFacts.Single().HasStart, Is.True);
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(prior.ContentHash)); Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [Test] public void SavedMotionRegistryRestoresFullCapturedBindingAndChangesOnlyMediaWeights()
        {
            SelectTrackedSpecial("rapier_range"); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/complete-motion-registry"); var a = initial.Versions;
            var document = a.MotionRegistry.ToDocument(); var body = document.motions.Single(motion => motion.id == "rapier/attack1");
            body.steps[0].seconds += .07f; var changed = new MotionSourceRegistry(document, CatalogContext); var b = ReplaceComplete(a, registry: changed);
            var recorded = session.RecordManual(initial, .02f, SpecialInputs(100)); string path = Path.Combine(directory, "registry.json");
            ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext);
            Assert.That(arena.TryApplyMotionRegistry(changed.ToDocument(), false, out var error), Is.True, error); var prior = ExperimentSession.CaptureVersions(arena);
            var loaded = new ExperimentSession(arena); loaded.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _)); var replay = loaded.Replay();
            Assert.That(replay.Versions.MotionBindingHash, Is.EqualTo(a.MotionBindingHash)); Assert.That(replay.TraceHash, Is.EqualTo(recorded.TraceHash)); Assert.That(replay.HitTraceHash, Is.EqualTo(recorded.HitTraceHash));
            var pair = loaded.Compare(a, b); Assert.That(pair.A.HealthLost, Is.EqualTo(pair.B.HealthLost)); Assert.That(pair.A.FirstHitTick, Is.EqualTo(pair.B.FirstHitTick));
            Assert.That(pair.A.Versions.MotionBindingHash, Is.Not.EqualTo(pair.B.Versions.MotionBindingHash));
            Assert.That(arena.MotionBindingHash, Is.EqualTo(prior.MotionBindingHash)); Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(prior.ContentHash));
        }
        [TestCase("whip_default")] [TestCase("launcher_basic_0")]
        public void MissingCurrentNewFamilySourceRejectsBeforeResetAndRetainsPublishedResults(string id)
        {
            var session = Record(out _); var selected = session.Scenario; var last = session.LastResult;
            var form = arena.catalog.forms.Single(entry => entry.id == (id == "whip_default" ? "whip" : "launcher"));
            var specials = form.specials; var basics = form.basics; int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try
            {
                if (id == "whip_default") form.specials = specials.Where(skill => skill.id != id).ToArray(); else form.basics = Array.Empty<BasicData>();
                Assert.Throws<InvalidOperationException>(() => session.CaptureInitial("lab/missing-complete-source"));
                Assert.Throws<InvalidOperationException>(() => session.Select(selected)); Assert.Throws<InvalidOperationException>(() => session.Replay());
                Assert.That(resets, Is.Zero); Assert.That(session.Scenario, Is.SameAs(selected)); Assert.That(session.LastResult, Is.SameAs(last)); Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally { form.specials = specials; form.basics = basics; arena.ResetCompleted -= count; }
        }
        [Test] public void ChangedNativeMotionMediaBindingRejectsBeforeAnyReset()
        {
            var session = Record(out _); var prior = session.Scenario; var set = arena.characterAnimations.sets.Single(value => value.form == "rapier");
            var clip = set.clips.Single(value => value.name == "attack1"); var frames = clip.frames; int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            var last = session.LastResult; float now = arena.Now; var position = arena.Player.Position; float health = arena.Player.health;
            var running = arena.Player.currentCast; string versions = arena.MeleeFrameVersion, binding = arena.MotionBindingHash;
            try
            {
                clip.frames = (string[])frames.Clone(); clip.frames[0] = "unknown/rebound-body-frame";
                var selectFailure = Assert.Catch(() => session.Select(prior)); var replayFailure = Assert.Catch(() => session.Replay());
                Assert.That(selectFailure is ArgumentException || selectFailure is InvalidOperationException, Is.True);
                Assert.That(replayFailure is ArgumentException || replayFailure is InvalidOperationException, Is.True);
                Assert.That(resets, Is.Zero); Assert.That(arena.ManualSimulationActive, Is.False);
                Assert.That(session.Scenario, Is.SameAs(prior)); Assert.That(session.LastResult, Is.SameAs(last));
                Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Player.health, Is.EqualTo(health));
                Assert.That(arena.Player.currentCast, Is.SameAs(running)); Assert.That(arena.MeleeFrameVersion, Is.EqualTo(versions)); Assert.That(arena.MotionBindingHash, Is.EqualTo(binding));
            }
            finally { clip.frames = frames; arena.ResetCompleted -= count; }
        }
        [TestCase(ActionTrackDefinition.WaveId)] [TestCase(ActionTrackDefinition.GuardId)]
        [TestCase("base_sword_default")] [TestCase("rapier_range")] [TestCase("greatsword_range")]
        [TestCase("rapier_default")] [TestCase("greatsword_default")]
        [TestCase("whip_default")] [TestCase("launcher_basic_0")]
        public void MissingGraphCannotReplaceWindowSelection(string id)
        {
            var session = Record(out _); var selected = session.Scenario;
            var window = ScriptableObject.CreateInstance<ExperimentScenarioWindow>(); window.name = "ScenarioTestCompleteTracks";
            window.Show(); window.CreateGUI();
            string path = Path.Combine(directory, "complete-window.json"); ExperimentScenarioSource.Save(path, null, selected, CatalogContext);
            window.LoadScenario(path); var retained = window.SelectedScenario; int resets = 0; Action count = () => resets++;
            arena.ResetCompleted += count;
            try
            {
                string json = ExperimentScenarioSource.Serialize(selected);
                string collection = AuthoredSkillDefinition.SupportedIds.Contains(id) ? "authoredSkills" : RangedActionCatalog.SupportedIds.Contains(id) ? "rangedActions" :
                    ChargedMeleeDefinition.SupportedIds.Contains(id) ? "chargedSpecials" : DirectMeleeSpecialDefinition.SupportedIds.Contains(id) ? "directSpecials" : "actionTracks";
                File.WriteAllText(path, RemoveTrackGraph(json, id, collection));
                Assert.Catch<ArgumentException>(() => window.LoadScenario(path)); Assert.That(window.SelectedScenario, Is.SameAs(retained));
                Assert.That(resets, Is.Zero); Assert.That(arena.ManualSimulationActive, Is.False);
            }
            finally { arena.ResetCompleted -= count; window.Close(); }
        }
        [Test] public void ObsoleteScenarioSchemasCannotReplaceWindowSelection()
        {
            var session = Record(out _); var selected = session.Scenario;
            var window = ScriptableObject.CreateInstance<ExperimentScenarioWindow>(); window.name = "ScenarioTestObsoleteSchemas";
            window.Show(); window.CreateGUI(); string path = Path.Combine(directory, "obsolete-window.json");
            ExperimentScenarioSource.Save(path, null, selected, CatalogContext); window.LoadScenario(path); var retained = window.SelectedScenario;
            string json = ExperimentScenarioSource.Serialize(selected), current = "\"schemaVersion\": " + ExperimentScenarioSource.SchemaVersion;
            int field = json.IndexOf(current, StringComparison.Ordinal); Assert.That(field, Is.GreaterThanOrEqualTo(0));
            float now = arena.Now; var position = arena.Player.Position; float health = arena.Player.health;
            int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            try
            {
                for (int schema = 1; schema < ExperimentScenarioSource.SchemaVersion; schema++)
                {
                    string obsolete = json.Remove(field, current.Length).Insert(field, "\"schemaVersion\": " + schema);
                    Assert.That(obsolete, Is.Not.EqualTo(json)); File.WriteAllText(path, obsolete);
                    Assert.Throws<ArgumentException>(() => window.LoadScenario(path)); Assert.That(window.SelectedScenario, Is.SameAs(retained));
                    Assert.That(resets, Is.Zero); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.Position, Is.EqualTo(position));
                    Assert.That(arena.Player.health, Is.EqualTo(health)); Assert.That(arena.ManualSimulationActive, Is.False);
                }
            }
            finally { arena.ResetCompleted -= count; window.Close(); }
        }
        static string RemoveTrackGraph(string json, string id, string collection = "actionTracks")
        {
            int list = json.IndexOf("\"" + collection + "\": [", StringComparison.Ordinal);
            int identity = json.IndexOf("\"id\": \"" + id + "\"", list, StringComparison.Ordinal);
            int start = json.LastIndexOf('{', identity), end = -1, depth = 0; bool quoted = false, escaped = false;
            Assert.That(list, Is.GreaterThanOrEqualTo(0)); Assert.That(identity, Is.GreaterThan(list));
            for (int index = start; index < json.Length; index++)
            {
                char value = json[index];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (value == '\\') escaped = true;
                    else if (value == '"') quoted = false;
                    continue;
                }
                if (value == '"') quoted = true;
                else if (value == '{') depth++;
                else if (value == '}' && --depth == 0) { end = index + 1; break; }
            }
            Assert.That(end, Is.GreaterThan(start));
            int next = end; while (char.IsWhiteSpace(json[next])) next++;
            if (json[next] == ',') return json.Remove(start, next - start + 1);
            int previous = start - 1; while (char.IsWhiteSpace(json[previous])) previous--;
            return json.Remove(json[previous] == ',' ? previous : start, end - (json[previous] == ',' ? previous : start));
        }
        [TestCase(ActionTrackDefinition.FollowUpId)] [TestCase("whip_default")]
        public void SavedSpecialFrameVersionsReplayAndCompareWithoutRebasingToCurrentLocalMode(string id)
        {
            SelectTrackedSpecial(id); arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/special-frame-state/" + id); var fixedVersion = initial.Versions;
            var frameVersion = ExperimentScenarioTests.WithSpecialFrames(fixedVersion, ExperimentScenarioTests.ShiftSpecialFrames(fixedVersion, id, .125f));
            var inputs = SpecialInputs(150); var recorded = session.RecordManual(initial, .02f, inputs);
            Assert.That(recorded.AcceptedHits, Is.GreaterThan(0));
            string path = Path.Combine(directory, id + "-frame-state.json"); ExperimentScenarioSource.Save(path, null, session.Scenario, CatalogContext);
            Assert.That(arena.TryApplySpecialFrames(new[] { frameVersion.SpecialFrames.Find(id) }, false, out var error), Is.True, error);
            var current = ExperimentSession.CaptureVersions(arena); Assert.That(current.MediaHash, Is.EqualTo(fixedVersion.MediaHash));
            var fresh = new ExperimentSession(arena); fresh.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _));
            var replayed = fresh.Replay(); Assert.That(replayed.TraceHash, Is.EqualTo(recorded.TraceHash)); Assert.That(replayed.HitTraceHash, Is.EqualTo(recorded.HitTraceHash));
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(current.ContentHash));
            var pair = fresh.Compare(fixedVersion, frameVersion);
            Assert.That(pair.A.FirstHitTick, Is.GreaterThanOrEqualTo(0)); Assert.That(pair.B.FirstHitTick, Is.GreaterThan(pair.A.FirstHitTick));
            Assert.That(pair.B.AcceptedHits, Is.EqualTo(pair.A.AcceptedHits)); Assert.That(pair.B.HealthLost, Is.EqualTo(pair.A.HealthLost));
            Assert.That(pair.B.TraceHash, Is.Not.EqualTo(pair.A.TraceHash)); Assert.That(arena.GetSpecialFrame(id).UsesFrameTime, Is.True);
            Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(current.ContentHash)); Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [UnityTest] public IEnumerator SavedScenarioFreshPlayModeUsesExplicitFixtureKeysRatherThanRuntimeActorIds()
        {
            var recorded = Record(out var captured); string path = Path.Combine(directory, "fresh-runtime.json"), trace = recorded.LastResult.TraceHash;
            ExperimentScenarioSource.Save(path, null, recorded.Scenario, CatalogContext);
            yield return new ExitPlayMode();
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; arena = Object.FindAnyObjectByType<CombatArena>();
            var fresh = new ExperimentSession(arena); fresh.Select(ExperimentScenarioSource.Load(path, CatalogContext, out _)); var replay = fresh.Replay();
            Assert.That(replay.TraceHash, Is.EqualTo(trace)); Assert.That(replay.Scenario.Initial.Actors[0].Key, Is.EqualTo(ExperimentInitialState.PlayerKey));
            Assert.That(replay.Scenario.Initial.Actors.Skip(1).Select(actor => actor.Key), Is.EqualTo(captured.Actors.Skip(1).Select(actor => actor.Key)));
            Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualWindowCapturesRecordsSavesFreshLoadsAndReplaysWithoutMutatingOnInvalidLoad()
        {
            var window = ScriptableObject.CreateInstance<ExperimentScenarioWindow>(); window.name = "ScenarioTestWindow"; window.Show(); window.CreateGUI(); yield return null;
            window.SetManualInputs(.02f, Inputs(53)); Click(window, "captureInitial"); Click(window, "recordManual");
            Assert.That(window.rootVisualElement.Q<Label>("results").text, Does.Contain("53 successful ticks").And.Contain("accepted hits"));
            Assert.That(window.rootVisualElement.Q<Label>("initialSummary").text, Does.Contain("All " + ActionTrackDefinition.SupportedIds.Count + " supported finite actions"));
            foreach (string id in ActionTrackDefinition.SupportedIds) Assert.That(window.rootVisualElement.Q<Label>("initialSummary").text, Does.Contain(id));
            foreach (string id in DirectMeleeSpecialDefinition.SupportedIds) Assert.That(window.rootVisualElement.Q<Label>("initialSummary").text, Does.Contain(id));
            foreach (string id in ChargedMeleeDefinition.SupportedIds) Assert.That(window.rootVisualElement.Q<Label>("initialSummary").text, Does.Contain(id));
            Assert.That(window.rootVisualElement.Q<Label>("initialSummary").text, Does.Contain("All " + AuthoredSkillDefinition.SupportedIds.Count + " authored skills").And.Contain("all " + RangedActionCatalog.SupportedIds.Count + " ranged actions").And.Contain("Full motion registry:"));
            string trace = window.LastResult.TraceHash, path = Path.Combine(directory, "window.json"); string hash = window.SaveScenario(path, null);
            Click(window, "captureA"); Click(window, "captureB"); Click(window, "compare");
            Assert.That(window.rootVisualElement.Q<Label>("status").text, Does.Contain("A/B complete")); window.Close();
            window = ScriptableObject.CreateInstance<ExperimentScenarioWindow>(); window.name = "ScenarioTestFreshWindow"; window.Show(); window.CreateGUI(); yield return null;
            window.LoadScenario(path); var selected = window.SelectedScenario; Click(window, "replay"); Assert.That(window.LastResult.TraceHash, Is.EqualTo(trace));
            File.WriteAllText(path, "external editor text"); Assert.Throws<IOException>(() => window.SaveScenario(path, hash));
            Assert.Throws<ArgumentException>(() => window.LoadScenario(path)); Assert.That(window.SelectedScenario, Is.SameAs(selected));
            Assert.That(File.ReadAllText(path), Is.EqualTo("external editor text")); Assert.That(arena.ManualSimulationActive, Is.False);
        }
        static void Click(ExperimentScenarioWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button, Is.Not.Null, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
