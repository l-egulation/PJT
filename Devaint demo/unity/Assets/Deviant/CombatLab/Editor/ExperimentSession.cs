using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.CombatLab
{
    public readonly struct ExperimentActorObservation
    {
        public string Key { get; }
        public ActorReadSnapshot State { get; }
        public int AcceptedHits { get; }
        public float HealthLost { get; }
        internal ExperimentActorObservation(string key, ActorReadSnapshot state, ActorReadSnapshot previous)
        { Key = key; State = state; AcceptedHits = state.Vitals.ReceivedHits - previous.Vitals.ReceivedHits; HealthLost = previous.Vitals.Health - state.Vitals.Health; }
    }

    public sealed class ExperimentTickObservation
    {
        public int Tick { get; }
        public float Now { get; }
        public IReadOnlyList<ExperimentActorObservation> Actors { get; }
        public PlayerWeaponResourcesReadSnapshot Resources => Actors[0].State.Resources;
        public int FirstCommandOutcome { get; }
        public int CommandOutcomeCount { get; }
        internal ExperimentTickObservation(int tick, float now, ExperimentActorObservation[] actors, int firstCommandOutcome, int commandOutcomeCount)
        { Tick = tick; Now = now; Actors = Array.AsReadOnly(actors); FirstCommandOutcome = firstCommandOutcome; CommandOutcomeCount = commandOutcomeCount; }
    }

    public readonly struct ExperimentDiagnosticEvent
    {
        public int Tick { get; }
        public string Message { get; }
        internal ExperimentDiagnosticEvent(int tick, string message) { Tick = tick; Message = message; }
    }

    public sealed class ExperimentReplayFailure
    {
        public int FaultedTick { get; }
        public int CompletedTicks { get; }
        public string Error { get; }
        public string ObservationError { get; }
        public IReadOnlyList<ExperimentCommandObservation> CommandOutcomes { get; }
        public IReadOnlyList<ExperimentHitCorrelation> AcceptedHitFacts { get; }
        public IReadOnlyList<ExperimentHitIdentityObservation> HitIdentities { get; }
        internal ExperimentReplayFailure(int faultedTick, int completedTicks, Exception error, Exception observationError,
            ExperimentCommandObservation[] commands, ExperimentHitCorrelation[] hits, ExperimentHitIdentityObservation[] identities)
        {
            FaultedTick = faultedTick; CompletedTicks = completedTicks; Error = error.ToString(); ObservationError = observationError?.ToString();
            CommandOutcomes = Array.AsReadOnly(commands); AcceptedHitFacts = Array.AsReadOnly(hits);
            HitIdentities = Array.AsReadOnly(identities);
        }
    }

    public sealed class ExperimentReplayResult
    {
        public ExperimentScenario Scenario { get; }
        public ExperimentVersions Versions { get; }
        public IReadOnlyList<ExperimentTickObservation> Ticks { get; }
        public IReadOnlyList<ExperimentDiagnosticEvent> DiagnosticEvents { get; }
        public IReadOnlyList<ExperimentHitCorrelation> AcceptedHitFacts { get; }
        public string HitTraceHash { get; }
        public IReadOnlyList<ExperimentCommandObservation> CommandOutcomes { get; }
        public string ResourceTraceHash { get; }
        public string CommandTraceHash { get; }
        public IReadOnlyList<ExperimentHitIdentityObservation> HitIdentities { get; }
        public string IdentityTraceHash { get; }
        public int AcceptedHits { get; }
        public float HealthLost { get; }
        public int FirstHitTick { get; }
        public float PlayerTravelX { get; }
        // Trace hashes normalize volatile runtime IDs; AcceptedHitFacts retain
        // the actual session/command/execution/effect identities for inspection.
        public string TraceHash { get; }
        internal ExperimentReplayResult(ExperimentScenario scenario, ExperimentVersions versions, ExperimentTickObservation[] ticks, ExperimentDiagnosticEvent[] diagnosticEvents, ExperimentHitCorrelation[] acceptedHits,
            ExperimentCommandObservation[] commandOutcomes, ExperimentHitIdentityObservation[] hitIdentities)
        {
            Scenario = scenario; Versions = versions; Ticks = Array.AsReadOnly(ticks);
            DiagnosticEvents = Array.AsReadOnly(diagnosticEvents);
            AcceptedHitFacts = Array.AsReadOnly(acceptedHits);
            CommandOutcomes = Array.AsReadOnly(commandOutcomes);
            ResourceTraceHash = ComputeResourceTrace(ticks);
            CommandTraceHash = ComputeCommandTrace(commandOutcomes);
            HitIdentities = Array.AsReadOnly(hitIdentities);
            IdentityTraceHash = ComputeIdentityTrace(hitIdentities);
            FirstHitTick = -1;
            foreach (var hit in acceptedHits)
            {
                AcceptedHits++;
                HealthLost += hit.Hit.Before.Health - hit.Hit.After.Health;
                if (FirstHitTick < 0) FirstHitTick = (int)hit.Hit.TickCount;
            }
            PlayerTravelX = ticks[ticks.Length - 1].Actors[0].State.Kinematics.X - scenario.Initial.Actors[0].X;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    writer.Write("lab-observed-trace-v2"); writer.Write(ticks.Length); writer.Write(ResourceTraceHash); writer.Write(CommandTraceHash); writer.Write(IdentityTraceHash);
                    foreach (var tick in ticks)
                    {
                        writer.Write(tick.Tick); writer.Write(tick.Now);
                        foreach (var actor in tick.Actors)
                        {
                            var state = actor.State; var pose = state.Kinematics; var vitals = state.Vitals; var action = state.Action;
                            writer.Write(actor.Key); writer.Write(pose.X); writer.Write(pose.Y); writer.Write(pose.VelocityX); writer.Write(pose.VelocityY);
                            writer.Write(pose.Facing); writer.Write(pose.Grounded); writer.Write(pose.WallHit);
                            writer.Write(vitals.Maximum); writer.Write(vitals.Health); writer.Write(vitals.Shield); writer.Write(vitals.ReceivedHits); writer.Write(vitals.GuardUntil); writer.Write(vitals.GuardReduction);
                            var status = state.Statuses;
                            writer.Write(status.StunUntil); writer.Write(status.RootUntil); writer.Write(status.StaggerUntil); writer.Write(status.InputLockUntil);
                            writer.Write(status.KnockUntil); writer.Write(status.KnockSpeed); writer.Write(status.StealthUntil); writer.Write(status.SmokeUntil);
                            var dash = state.Dash;
                            writer.Write(dash.StartedAt); writer.Write(dash.EndsAt); writer.Write(dash.ReadyAt); writer.Write(state.Now < dash.EndsAt ? dash.Facing : 0);
                            writer.Write(dash.JumpCarryUntil); writer.Write(state.Now < dash.JumpCarryUntil ? dash.JumpCarryFacing : 0);
                            writer.Write(state.Jump.Used); writer.Write(state.Jump.BufferedUntil);
                            writer.Write(actor.AcceptedHits); writer.Write(actor.HealthLost); writer.Write(action.ContentId ?? "");
                            writer.Write(action.DefinitionVersion ?? ""); writer.Write(action.TimelineVersion ?? ""); writer.Write(action.FrameTimingVersion ?? "");
                            writer.Write((int)action.Phase); writer.Write(action.StartedAt); writer.Write(action.Elapsed); writer.Write(action.EndsAt);
                            writer.Write(state.ActiveWeaponContentId ?? "");
                        }
                    }
                    writer.Write(diagnosticEvents.Length);
                    foreach (var observed in diagnosticEvents) { writer.Write(observed.Tick); writer.Write(observed.Message); }
                }
                using (var sha = SHA256.Create()) TraceHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    writer.Write("lab-accepted-hit-trace-v1"); writer.Write(acceptedHits.Length);
                    foreach (var value in acceptedHits)
                    {
                        var hit = value.Hit; var origin = hit.Origin;
                        writer.Write(hit.TickCount); writer.Write(hit.Now); writer.Write(value.SourceActorKey ?? ""); writer.Write(value.TargetActorKey ?? "");
                        writer.Write((int)origin.Command.Kind); writer.Write(origin.HitIndex); writer.Write(origin.HitId ?? ""); writer.Write((int)origin.Kind);
                        writer.Write(hit.RequestedDamage); writer.Write(hit.Damage); writer.Write(hit.Absorbed); writer.Write(hit.Before.Health); writer.Write(hit.After.Health);
                        writer.Write(value.AdmissionTick); writer.Write(value.AdmittedAt); writer.Write(value.HasStart);
                        if (value.HasStart) { writer.Write(value.Start.TickCount); writer.Write(value.Start.Now); }
                    }
                }
                using (var sha = SHA256.Create()) HitTraceHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static string ComputeResourceTrace(IReadOnlyList<ExperimentTickObservation> ticks)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    writer.Write("lab-resource-trace-v1"); writer.Write(ticks.Count);
                    foreach (var tick in ticks) { writer.Write(tick.Tick); writer.Write(tick.Now); WriteResources(writer, tick.Resources); }
                }
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static string ComputeCommandTrace(IReadOnlyList<ExperimentCommandObservation> commands)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    writer.Write("lab-command-trace-v1"); writer.Write(commands.Count);
                    foreach (var value in commands)
                    {
                        var outcome = value.Outcome; var state = outcome.Snapshot;
                        writer.Write(value.EventOrdinal); writer.Write(value.ActorKey ?? ""); writer.Write(value.ActorOrdinal); writer.Write(value.CommandOrdinal); writer.Write(value.ExecutionOrdinal); writer.Write(value.AdmissionTick);
                        writer.Write(outcome.TickCount); writer.Write(outcome.Now); writer.Write((int)outcome.Command.Kind); writer.Write((int)outcome.State); writer.Write((int)outcome.Reason);
                        writer.Write(outcome.Detail != null); if (outcome.Detail != null) writer.Write(outcome.Detail);
                        writer.Write(state.Action.ContentId ?? ""); writer.Write(state.Action.DefinitionVersion ?? ""); writer.Write(state.Action.TimelineVersion ?? ""); writer.Write(state.Action.FrameTimingVersion ?? "");
                        writer.Write((int)state.Action.Phase); writer.Write(state.Action.StartedAt); writer.Write(state.Action.Elapsed); writer.Write(state.Action.EndsAt);
                        writer.Write(state.Kinematics.X); writer.Write(state.Kinematics.Y); writer.Write(state.Vitals.Health); writer.Write(state.Vitals.Shield); WriteResources(writer, state.Resources);
                    }
                }
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static string ComputeIdentityTrace(IReadOnlyList<ExperimentHitIdentityObservation> identities)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    writer.Write("lab-causal-identity-trace-v1"); writer.Write(identities.Count);
                    foreach (var value in identities)
                    {
                        var hit = value.Hit; writer.Write(hit.TickCount); writer.Write(hit.Now);
                        writer.Write(value.SourceActorOrdinal); writer.Write(value.EffectiveSourceActorOrdinal); writer.Write(value.TargetActorOrdinal); writer.Write(value.ReflectorActorOrdinal);
                        writer.Write(value.CommandOrdinal); writer.Write(value.ExecutionOrdinal); writer.Write(value.EffectOrdinal); writer.Write(value.ReflectorCommandOrdinal); writer.Write(value.ReflectorExecutionOrdinal);
                        writer.Write(hit.Origin.HitIndex); writer.Write(hit.Origin.HitId ?? ""); writer.Write((int)hit.Origin.Kind);
                    }
                }
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static void WriteResources(BinaryWriter writer, PlayerWeaponResourcesReadSnapshot resources)
        {
            writer.Write(resources.HasLoadout); if (!resources.HasLoadout) return;
            writer.Write(resources.ActiveSlot); WriteWeapon(writer, resources.Slot0); WriteWeapon(writer, resources.Slot1);
        }
        static void WriteWeapon(BinaryWriter writer, WeaponChargeReadSnapshot weapon)
        {
            // Slot ownership is the stable cross-run relation. The detached read
            // retains the actual instance GUID; comparison never invents one.
            writer.Write(weapon.FormId ?? ""); writer.Write(weapon.SpecialId ?? ""); writer.Write(weapon.Capacity); writer.Write(weapon.Available); writer.Write(weapon.Period); writer.Write(weapon.NextReady);
        }
    }

    // Reuses the actual Lab and common input/simulation; no actor or terrain recreation.
    // Every run restores the prior technical reset configuration, not in-flight gameplay.
    public sealed class ExperimentSession
    {
        static readonly string[] EnemyNames = { "MeleeTester", "RangedTester", "BossDummy" };
        readonly CombatArena arena;
        public ExperimentScenario Scenario { get; private set; }
        public ExperimentReplayResult LastResult { get; private set; }
        public ExperimentReplayResult LastA { get; private set; }
        public ExperimentReplayResult LastB { get; private set; }
        public ExperimentReplayFailure LastFailure { get; private set; }
        public ExperimentSession(CombatArena arena) { this.arena = arena ? arena : throw new ArgumentNullException(nameof(arena)); }

        public ExperimentInitialState CaptureInitial(string id, int? seed = null, string seedProvenance = "")
        {
            ValidateLab();
            var geometry = CaptureGeometry(arena);
            return CaptureConfiguration(id, geometry, seed, seedProvenance);
        }
        ExperimentInitialState CaptureConfiguration(string id, ExperimentGeometry geometry, int? seed = null, string seedProvenance = "")
        {
            var versions = CaptureVersions(arena);
            var actors = new ExperimentActorInitial[4]; actors[0] = CaptureActor(arena.Player, ExperimentInitialState.PlayerKey, 0, -1, arena.Player.spawn, false);
            for (int i = 0; i < 3; i++) actors[i + 1] = CaptureActor(arena.enemies[i], ExperimentInitialState.EnemyKey(i), i + 1, arena.enemies[i].profile, arena.enemies[i].spawn, arena.enemies[i].aggressive);
            var weapons = new ExperimentWeaponInitial[2];
            for (int i = 0; i < weapons.Length; i++)
            {
                var weapon = arena.Player.weapons[i]; var form = arena.catalog.forms[weapon.FormIndex];
                weapons[i] = new ExperimentWeaponInitial(form.id, form.specials[weapon.SpecialIndex].id, weapon.StatSlots);
            }
            return new ExperimentInitialState(id, ExperimentRuntimeProvenanceCapture.Capture(arena), geometry, actors, weapons, arena.Player.selected, versions, seed, seedProvenance);
        }
        ExperimentActorInitial CaptureActor(CombatActor actor, string key, int order, int profile, Vector2 spawn, bool aggressive)
        {
            if (!arena.TryBindActor(actor, out var handle) || !arena.ReadModel.TryReadActor(handle, out var snapshot)) throw new InvalidOperationException("Actor read binding is unavailable.");
            var pose = snapshot.Kinematics; var vitals = snapshot.Vitals;
            return new ExperimentActorInitial(key, order, profile, pose.X, pose.Y, spawn.x, spawn.y, pose.Facing, vitals.Maximum, vitals.Health, vitals.Shield,
                Mathf.Max(0, actor.guardUntil - arena.Now), actor.guardReduction, aggressive, actor.isBoss, actor.size.x, actor.size.y, actor.offset.x, actor.offset.y, snapshot.Statuses.KnockSpeed);
        }
        public static ExperimentVersions CaptureVersions(CombatArena arena)
        {
            if (!arena || !arena.Player || arena.catalog?.forms == null) throw new InvalidOperationException("An initialized Lab is required.");
            if (!arena.TryGetBasicSwordMotion(out var motion, out var error)) throw new InvalidOperationException(error);
            var basics = new List<BasicMeleeDefinition>();
            foreach (var form in arena.catalog.forms)
                for (int i = 0; i < (form.basics?.Length ?? 0); i++)
                {
                    var action = arena.GetBasicMelee(form.id, i);
                    if (action != null) basics.Add(action.Id == FixedAttackMotion.FirstSwordAction ? arena.FrameSwordTiming?.SourceAction ?? action : action);
                }
            if (arena.ActionTrackCount != ActionTrackDefinition.SupportedIds.Count)
                throw new InvalidOperationException("The Lab runtime does not contain the complete supported finite action set.");
            var sourceTracks = arena.catalog.forms.Where(form => form?.specials != null).SelectMany(form => form.specials)
                .Where(skill => skill != null && ActionTrackDefinition.Supports(skill.id)).ToArray();
            if (!sourceTracks.Select(skill => skill.id).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(ActionTrackDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new InvalidOperationException("The current catalog is missing or duplicates a supported finite action source.");
            foreach (var source in sourceTracks)
            {
                if (ActionTrackDocument.IsAbsent(source.actionTracks))
                    throw new InvalidOperationException("The current catalog is missing a complete supported finite action graph.");
                var compiled = new ActionTrackDefinition(source); var current = arena.GetNumericActionTracks(source.id);
                if (current == null || compiled.SourceVersion != current.SourceVersion)
                    throw new InvalidOperationException("The current catalog and applied finite action metadata differ.");
            }
            if (arena.DirectMeleeSpecialCount != DirectMeleeSpecialDefinition.SupportedIds.Count)
                throw new InvalidOperationException("The Lab runtime does not contain the complete supported direct melee special set.");
            var directSources = arena.catalog.forms.Where(form => form?.specials != null)
                .SelectMany(form => form.specials.Where(skill => skill != null && DirectMeleeSpecialDefinition.SupportedIds.Contains(skill.id))
                    .Select(skill => (FormId: form.id, Skill: skill))).ToArray();
            if (!directSources.Select(source => source.Skill.id).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(DirectMeleeSpecialDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new InvalidOperationException("The current catalog is missing or duplicates a supported direct melee source.");
            foreach (var source in directSources)
            {
                if (!DirectMeleeSpecialDefinition.TryCreate(source.FormId, source.Skill, out var compiled, out var directError))
                    throw new InvalidOperationException(directError);
                var current = arena.GetDirectMeleeSpecial(source.Skill.id);
                if (current == null || current.FormId != source.FormId || compiled.SourceVersion != current.SourceVersion)
                    throw new InvalidOperationException("The current catalog and applied direct melee policy differ.");
            }
            if (arena.ChargedMeleeCount != ChargedMeleeDefinition.SupportedIds.Count)
                throw new InvalidOperationException("The Lab runtime does not contain the complete supported charged melee set.");
            var chargedSources = arena.catalog.forms.Where(form => form?.specials != null)
                .SelectMany(form => form.specials.Where(skill => skill != null && ChargedMeleeDefinition.SupportedIds.Contains(skill.id))
                    .Select(skill => (FormId: form.id, Skill: skill))).ToArray();
            if (!chargedSources.Select(source => source.Skill.id).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(ChargedMeleeDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new InvalidOperationException("The current catalog is missing or duplicates a supported charged melee source.");
            foreach (var source in chargedSources)
            {
                if (!ChargedMeleeDefinition.TryCreate(source.FormId, source.Skill, out var compiled, out var chargedError))
                    throw new InvalidOperationException(chargedError);
                var current = arena.GetChargedMelee(source.Skill.id);
                if (current == null || current.FormId != source.FormId || compiled.SourceVersion != current.SourceVersion)
                    throw new InvalidOperationException("The current catalog and applied charged melee policy differ.");
            }
            if (arena.AuthoredSkillCount != AuthoredSkillDefinition.SupportedIds.Count)
                throw new InvalidOperationException("The Lab runtime does not contain the complete authored skill set.");
            var authoredSources = arena.catalog.forms.Where(form => form?.specials != null)
                .SelectMany(form => form.specials.Where(skill => skill != null && AuthoredSkillDefinition.SupportedIds.Contains(skill.id))
                    .Select(skill => (FormId: form.id, Skill: skill))).ToArray();
            if (!authoredSources.Select(source => source.Skill.id).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(AuthoredSkillDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new InvalidOperationException("The current catalog is missing or duplicates a supported authored skill source.");
            foreach (var source in authoredSources)
            {
                var compiled = new AuthoredSkillDefinition(source.FormId, source.Skill); var current = arena.GetNumericAuthoredSkill(source.Skill.id);
                if (current == null || current.FormId != source.FormId || compiled.SourceVersion != current.SourceVersion)
                    throw new InvalidOperationException("The current catalog and applied authored skill policy differ.");
            }
            if (!RangedActionCatalog.TryCreate(arena.catalog, out var rangedSource, out var rangedError)) throw new InvalidOperationException(rangedError);
            if (!rangedSource.TryReplace(arena.RangedActions, out var ranged, out rangedError)) throw new InvalidOperationException(rangedError);
            var registry = arena.MotionRegistry ?? throw new InvalidOperationException("The complete generated motion registry is required for a reusable scenario.");
            arena.ValidateMotionRegistry(registry);
            return new ExperimentVersions(HashJson(arena.catalog), HashMedia(arena), arena.Movement, basics, motion, arena.FrameSwordTiming,
                ActionTrackDefinition.SupportedIds.Select(arena.GetNumericActionTracks), DirectMeleeSpecialDefinition.SupportedIds.Select(arena.GetDirectMeleeSpecial),
                ChargedMeleeDefinition.SupportedIds.Select(arena.GetChargedMelee), AuthoredSkillDefinition.SupportedIds.Select(arena.GetNumericAuthoredSkill), ranged, registry,
                arena.MeleeFrameState ?? throw new InvalidOperationException("Complete 20-action frame timing state is required for scenario capture."),
                arena.SpecialFrames ?? throw new InvalidOperationException("Complete 23-action special frame timing state is required for scenario capture."));
        }
        public void Select(ExperimentScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            ValidateInitial(scenario.Initial, scenario.Initial.Versions);
            Scenario = scenario; // Whole-document validation precedes publication; no Reset or mutation.
            LastResult = LastA = LastB = null;
        }
        public ExperimentReplayResult RecordManual(ExperimentInitialState initial, float stepSeconds, CombatInput[] inputs)
        {
            // The common recording contract validates every sample before any reset.
            var candidate = new ExperimentScenario(initial, new CombatInputRecording(initial?.Id, stepSeconds, inputs));
            var result = Execute(candidate, initial.Versions, null);
            Scenario = result.Scenario; LastResult = result; LastA = LastB = null;
            return result;
        }
        public ExperimentReplayResult Replay()
        {
            var selected = Scenario ?? throw new InvalidOperationException("Capture and record, or load a complete Lab scenario first.");
            var result = Execute(selected, selected.Initial.Versions, null); LastResult = result; return result;
        }
        public (ExperimentReplayResult A, ExperimentReplayResult B) Compare(ExperimentVersions a, ExperimentVersions b)
        {
            if (a == null || b == null) throw new ArgumentNullException("Both complete A and B definition versions are required.");
            var selected = Scenario ?? throw new InvalidOperationException("Select a complete Lab scenario first.");
            var first = Execute(selected, a, b); // A and B share one lease and one validated initial state/input recording.
            LastResult = first; return (LastA, LastB);
        }
        ExperimentReplayResult Execute(ExperimentScenario scenario, ExperimentVersions a, ExperimentVersions b)
        {
            ValidateAvailable(); ValidateInitial(scenario.Initial, a);
            if (b != null) ValidateInitial(scenario.Initial, b);
            var prior = CaptureConfiguration("lab/session-restore", CaptureGeometry(arena));
            ValidateInitial(prior, prior.Versions);
            ExperimentReplayResult first = null, second = null;
            using (var driver = arena.AcquireManualSimulation())
            {
                try
                {
                    first = Run(driver, scenario, a);
                    if (b != null) second = Run(driver, scenario, b);
                }
                finally { ApplyInitial(driver, prior, prior.Versions); }
            }
            // An exception in a tick, read, B, or cleanup leaves all prior published results intact.
            if (b != null) { LastA = first; LastB = second; }
            return first;
        }
        ExperimentReplayResult Run(CombatArena.ManualSimulation driver, ExperimentScenario scenario, ExperimentVersions versions)
        {
            ApplyInitial(driver, scenario.Initial, versions);
            ICombatReadModel model = arena.ReadModel;
            var handles = new CombatActorHandle[4]; var previous = new ActorReadSnapshot[4];
            for (int i = 0; i < handles.Length; i++)
            {
                CombatActor actor = i == 0 ? (CombatActor)arena.Player : arena.enemies[i - 1];
                if (!arena.TryBindActor(actor, out handles[i]) || !model.TryReadActor(handles[i], out previous[i])) throw new InvalidOperationException("Reset actor could not bind to the combat read model.");
            }
            var recorder = new CombatInputRecorder(scenario.Initial.Id, scenario.Recording.StepSeconds, scenario.Recording.TickCount);
            var observer = new ExperimentReplayObserver(arena, handles, scenario.Initial.Actors.Select(actor => actor.Key).ToArray());
            var ticks = new ExperimentTickObservation[scenario.Recording.TickCount];
            var diagnosticEvents = new List<ExperimentDiagnosticEvent>(); int activeTick = 0;
            Application.LogCallback capture = (message, stack, type) => {
                if (type == LogType.Log && message.StartsWith("[CombatV2] ", StringComparison.Ordinal)) diagnosticEvents.Add(new ExperimentDiagnosticEvent(activeTick, message));
            };
            Application.logMessageReceived += capture;
            try
            {
                for (int tick = 0; tick < ticks.Length; tick++)
                {
                    activeTick = tick; var input = scenario.Recording.Inputs[tick];
                    long firstSequence = arena.Player.NextInputSequence; float admittedAt = arena.Now;
                    int firstOutcome = observer.OutcomeCount;
                    try { driver.Step(scenario.Recording.StepSeconds, input); }
                    catch (Exception error)
                    {
                        Exception observationError = null;
                        try { observer.Observe(tick, admittedAt, firstSequence, arena.Player.NextInputSequence); }
                        catch (Exception observed) { observationError = observed; }
                        LastFailure = new ExperimentReplayFailure(tick, recorder.Count, error, observationError, observer.CompleteOutcomes(), observer.Complete(), observer.CompleteHitIdentities());
                        throw; // Preserve the actual fault; observation never retries or turns it into a successful tick.
                    }
                    recorder.Record(input); // Only a successfully returned common tick is eligible for publication.
                    observer.Observe(tick, admittedAt, firstSequence, arena.Player.NextInputSequence);
                    var actors = new ExperimentActorObservation[4];
                    for (int i = 0; i < actors.Length; i++)
                    {
                        if (!model.TryReadActor(handles[i], out var current) || current.TickCount != tick + 1) throw new InvalidOperationException("The observed actor session changed during replay.");
                        actors[i] = new ExperimentActorObservation(scenario.Initial.Actors[i].Key, current, previous[i]); previous[i] = current;
                    }
                    ticks[tick] = new ExperimentTickObservation(tick + 1, previous[0].Now, actors, firstOutcome, observer.OutcomeCount - firstOutcome);
                }
            }
            finally { Application.logMessageReceived -= capture; }
            var completed = new ExperimentScenario(scenario.Initial, recorder.Complete());
            var result = new ExperimentReplayResult(completed, versions, ticks, diagnosticEvents.ToArray(), observer.Complete(), observer.CompleteOutcomes(), observer.CompleteHitIdentities());
            LastFailure = null; return result;
        }
        void ApplyInitial(CombatArena.ManualSimulation driver, ExperimentInitialState initial, ExperimentVersions versions)
        {
            // Every compiler, binding and geometry check has already run before mutation.
            var player = arena.Player;
            for (int i = 0; i < 2; i++)
            {
                var configuration = initial.Weapons[i]; int form = Array.FindIndex(arena.catalog.forms, entry => entry.id == configuration.FormId);
                int special = Array.FindIndex(arena.catalog.forms[form].specials, entry => entry.id == configuration.SpecialId);
                player.weapons[i].Select(form, special); player.weapons[i].StatSlots.CopyFrom(configuration.StatSlots.ToArray());
            }
            player.selected = initial.SelectedWeapon;
            for (int i = 0; i < 4; i++)
            {
                var source = initial.Actors[i]; CombatActor actor = i == 0 ? (CombatActor)player : arena.enemies[i - 1];
                actor.maxHealth = source.Maximum;
                if (i == 0) player.spawn = new Vector2(source.SpawnX, source.SpawnY);
                else { arena.enemies[i - 1].spawn = new Vector2(source.SpawnX, source.SpawnY); arena.enemies[i - 1].aggressive = source.Aggressive; }
            }
            if (!arena.TryApplyCompleteFrameState(versions.Basics, versions.DirectSpecials, versions.Motion, versions.MotionRegistry, versions.Frame, versions.MeleeFrames.ToDocument(),
                    versions.ActionTracks, versions.AuthoredSkills, versions.SpecialFrames.ToDocument(), false, out var error) ||
                !arena.TryApplyChargedMeleeDefinitions(versions.ChargedSpecials, false, out error) ||
                !arena.TryApplyRangedActions(versions.RangedActions.Actions, false, out error) ||
                versions.Frame != null && arena.FrameSwordTiming?.ContentHash != versions.Frame.ContentHash ||
                !arena.TryApplyMovementAndReset(versions.Movement.ToData(), out error)) throw new InvalidOperationException(error);
            driver.Reset();
            if (CaptureGeometry(arena).ContentHash != initial.Geometry.ContentHash) throw new InvalidOperationException("Lab geometry changed during reset.");
            for (int i = 0; i < 4; i++)
            {
                var source = initial.Actors[i]; CombatActor actor = i == 0 ? (CombatActor)player : arena.enemies[i - 1];
                actor.health = source.Health; actor.shield = source.Shield; actor.guardUntil = source.GuardSeconds; actor.guardReduction = source.GuardReduction;
                actor.knockSpeed = source.InactiveKnockSpeed; // Reset clears the deadline, but the owner intentionally retains this observable inactive value.
                actor.SyncFacing(source.Facing); actor.Position = new Vector2(source.X, source.Y); // Facing resolution cannot alter the captured placement.
                actor.wallHit = false; actor.grounded = false;
            }
            Physics2D.SyncTransforms();
        }
        public void ValidateInitial(ExperimentInitialState initial, ExperimentVersions versions)
        {
            ValidateLab();
            if (initial == null || versions == null) throw new ArgumentNullException("A complete initial state and definition version are required.");
            var provenance = ExperimentRuntimeProvenanceCapture.Capture(arena);
            if (!initial.Provenance.Matches(provenance))
                throw new InvalidOperationException("The scenario requires the same loaded Editor gameplay modules and consumed Physics2D query settings; nothing was reset. Cross-platform determinism is not promised.");
            if (CaptureGeometry(arena).ContentHash != initial.Geometry.ContentHash) throw new InvalidOperationException("The actual resolved Lab terrain differs from the scenario; nothing was reset.");
            if (HashJson(arena.catalog) != initial.Versions.CatalogHash || versions.CatalogHash != initial.Versions.CatalogHash || HashMedia(arena) != initial.Versions.MediaHash || versions.MediaHash != initial.Versions.MediaHash)
                throw new InvalidOperationException("The scenario requires the same catalog and non-overridden media content; nothing was reset.");
            var current = CaptureVersions(arena);
            if (!versions.Basics.Select(action => action.Id).SequenceEqual(current.Basics.Select(action => action.Id))) throw new InvalidOperationException("The captured basic action set differs from current content.");
            if (!versions.Motion.Frames.SequenceEqual(current.Motion.Frames) || !versions.Motion.StepIds.SequenceEqual(current.Motion.StepIds)) throw new InvalidOperationException("The captured sword art binding differs from current content.");
            if (!versions.ActionTracks.Select(action => action.Id).SequenceEqual(current.ActionTracks.Select(action => action.Id)))
                throw new InvalidOperationException("The captured finite action set differs from current supported content.");
            foreach (var track in versions.ActionTracks)
                if (current.ActionTracks.Single(action => action.Id == track.Id).SourceVersion != track.SourceVersion) throw new InvalidOperationException("Sword special metadata differs from the scenario.");
            if (!versions.DirectSpecials.Select(action => action.Id).SequenceEqual(current.DirectSpecials.Select(action => action.Id)))
                throw new InvalidOperationException("The captured direct melee inventory differs from current supported content.");
            foreach (var action in versions.DirectSpecials)
            {
                var expected = current.DirectSpecials.Single(candidate => candidate.Id == action.Id);
                if (expected.FormId != action.FormId || expected.SourceVersion != action.SourceVersion)
                    throw new InvalidOperationException("Direct melee source policy differs from the scenario; nothing was reset.");
                action.Compile();
            }
            if (!versions.ChargedSpecials.Select(action => action.Id).SequenceEqual(current.ChargedSpecials.Select(action => action.Id)))
                throw new InvalidOperationException("The captured charged melee inventory differs from current supported content.");
            foreach (var action in versions.ChargedSpecials)
            {
                var expected = current.ChargedSpecials.Single(candidate => candidate.Id == action.Id);
                if (expected.FormId != action.FormId || expected.SourceVersion != action.SourceVersion)
                    throw new InvalidOperationException("Charged melee source policy differs from the scenario; nothing was reset.");
            }
            if (!versions.AuthoredSkills.Select(action => action.Id).SequenceEqual(current.AuthoredSkills.Select(action => action.Id)))
                throw new InvalidOperationException("The captured authored skill inventory differs from current supported content.");
            foreach (var action in versions.AuthoredSkills)
            {
                var expected = current.AuthoredSkills.Single(candidate => candidate.Id == action.Id);
                if (expected.FormId != action.FormId || expected.SourceVersion != action.SourceVersion)
                    throw new InvalidOperationException("Authored skill source policy differs from the scenario; nothing was reset.");
            }
            if (!current.RangedActions.TryReplace(versions.RangedActions.Actions, out _, out var rangedError)) throw new InvalidOperationException(rangedError);
            arena.ValidateMotionRegistry(versions.MotionRegistry);
            if (!versions.MotionRegistry.Palettes.Select(palette => palette.Id).SequenceEqual(current.MotionRegistry.Palettes.Select(palette => palette.Id)))
                throw new InvalidOperationException("The captured motion media inventory differs from current supplied content.");
            foreach (var palette in versions.MotionRegistry.Palettes)
                if (!palette.Frames.SequenceEqual(current.MotionRegistry.Palette(palette.Id).Frames)) throw new InvalidOperationException("Captured motion palette references differ from current supplied media.");
            if (versions.MotionRegistry.Motion(FixedAttackMotion.FirstSwordId)?.DelegatedSource != FixedBodyMotion.FirstSourcePath)
                throw new InvalidOperationException("First-sword timing must remain delegated to its separately captured canonical source.");
            var validatedFrames = new MeleeFrameCatalog(versions.MeleeFrames.ToDocument(), arena.catalog, versions.MotionRegistry, versions.Motion, versions.Frame, versions.Basics, versions.DirectSpecials);
            if (validatedFrames.ContentHash != versions.MeleeFrames.ContentHash) throw new InvalidOperationException("Captured complete melee frame state differs from its current policy/media context; nothing was reset.");
            var validatedSpecial = new ActionSpecialFrameCatalog(versions.SpecialFrames.ToDocument(), arena.catalog, versions.MotionRegistry, versions.Motion, versions.ActionTracks, versions.AuthoredSkills);
            if (validatedSpecial.ContentHash != versions.SpecialFrames.ContentHash) throw new InvalidOperationException("Captured complete special frame state differs from its numeric/policy/media context; nothing was reset.");
            for (int i = 0; i < initial.Weapons.Count; i++)
            {
                var weapon = initial.Weapons[i]; var matches = arena.catalog.forms.Where(form => form.id == weapon.FormId).ToArray();
                if (matches.Length != 1 || matches[0].specials == null || matches[0].specials.Count(special => special?.id == weapon.SpecialId) != 1) throw new InvalidOperationException("A captured weapon or special no longer exists in the catalog.");
            }
            for (int i = 0; i < initial.Actors.Count; i++)
            {
                var expected = initial.Actors[i]; CombatActor actor = i == 0 ? (CombatActor)arena.Player : arena.enemies[i - 1];
                if (expected.IsBoss != actor.isBoss || expected.Width != actor.size.x || expected.Height != actor.size.y || expected.OffsetX != actor.offset.x || expected.OffsetY != actor.offset.y)
                    throw new InvalidOperationException("The Lab actor fixture body or profile changed; nothing was reset.");
            }
            // Validate scaled motion for either configured weapon, before a reset can be observed.
            foreach (var weapon in initial.Weapons)
            {
                float speed = 1 + weapon.StatSlots.Count(value => value == 3) * .1f;
                foreach (var frame in versions.MeleeFrames.Actions) { frame.CompileForStart(speed); frame.CompileMotion(speed); }
            }
        }
        void ValidateAvailable()
        {
            ValidateLab();
            if (arena.ManualSimulationActive) throw new InvalidOperationException("Release the existing manual simulation owner (including Lab execution controls) before recording or replaying.");
            if (arena.Player.InputBlocked) throw new InvalidOperationException("Release the existing input block before recording or replaying; this tool never clears another owner's block.");
            if (arena.Controls.Current.SimulationPaused) throw new InvalidOperationException("Release the existing simulation pause before recording or replaying.");
        }
        void ValidateLab()
        {
            if (!Application.isPlaying || !arena || !arena.isActiveAndEnabled || !arena.Player || arena.CompositionProfile != CombatCompositionProfile.Lab || arena.gameObject.scene.path != ExperimentGeometry.LabScene ||
                UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 || arena.enemies.Count != 3)
                throw new InvalidOperationException("This scenario tool supports only the initialized, single existing CombatV2Arena Lab, not a general game world.");
            if (!arena.Player.isActiveAndEnabled || !arena.Player.isPlayer || arena.Player.isDecoy || arena.Player.name != "CombatV2Player")
                throw new InvalidOperationException("The explicit Lab player fixture binding changed.");
            foreach (var actor in UnityEngine.Object.FindObjectsByType<CombatActor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (actor != arena.Player && !arena.enemies.Contains(actor as TestEnemy) && !arena.decoys.Contains(actor))
                    throw new InvalidOperationException("Unregistered actors cannot participate in an explicit Lab scenario.");
            for (int i = 0; i < 3; i++)
                if (!arena.enemies[i] || arena.enemies[i].arena != arena || arena.enemies[i].name != EnemyNames[i] || arena.enemies[i].profile != i || arena.enemies[i].isPlayer || arena.enemies[i].isDecoy || !arena.enemies[i].isActiveAndEnabled)
                    throw new InvalidOperationException("Explicit Lab enemy fixture keys, profiles and order must match; Unity object IDs are not reusable keys.");
            foreach (var actor in new CombatActor[] { arena.Player, arena.enemies[0], arena.enemies[1], arena.enemies[2] })
            {
                var body = actor.GetComponent<Rigidbody2D>();
                var hurt = actor.hurtCollider ? actor.hurtCollider.transform : null;
                var region = actor.hurtCollider ? actor.hurtCollider.GetComponent<HurtRegion>() : null;
                var expectedOffset = new Vector2(actor.offset.x * actor.facing, actor.offset.y);
                if (!actor.gameObject.activeInHierarchy || !actor.bodyCollider || !actor.bodyCollider.enabled || !actor.hurtCollider || !actor.hurtCollider.enabled ||
                    actor.transform.lossyScale != Vector3.one || actor.transform.rotation != Quaternion.identity || actor.transform.position.z != 0 ||
                    actor.bodyCollider.size != actor.size || actor.hurtCollider.size != actor.size || actor.bodyCollider.isTrigger || !actor.hurtCollider.isTrigger ||
                    actor.bodyCollider.edgeRadius != 0 || actor.hurtCollider.edgeRadius != 0 || actor.bodyCollider.usedByEffector || actor.hurtCollider.usedByEffector ||
                    actor.bodyCollider.compositeOperation != Collider2D.CompositeOperation.None || actor.hurtCollider.compositeOperation != Collider2D.CompositeOperation.None)
                    throw new InvalidOperationException("Unsupported Lab actor body binding; nothing was reset.");
                if (actor.transform.parent || actor.bodyCollider.transform != actor.transform || !hurt || hurt.parent != actor.transform ||
                    hurt.localPosition != Vector3.zero || hurt.localRotation != Quaternion.identity || hurt.localScale != Vector3.one || !hurt.gameObject.activeInHierarchy ||
                    actor.bodyCollider.offset != expectedOffset || actor.hurtCollider.offset != expectedOffset ||
                    !region || region.actor != actor || actor.GetComponents<Collider2D>().Length != 1 || hurt.GetComponents<Collider2D>().Length != 1 ||
                    actor.gameObject.layer != 0 || hurt.gameObject.layer != 0 || actor.bodyCollider.autoTiling || actor.hurtCollider.autoTiling ||
                    !body || body.bodyType != RigidbodyType2D.Kinematic || !body.simulated || body.gravityScale != 0 || body.constraints != RigidbodyConstraints2D.FreezeRotation ||
                    actor.bodyCollider.attachedRigidbody != body || actor.hurtCollider.attachedRigidbody != body)
                    throw new InvalidOperationException("The native Lab body/hurtbox binding differs from its supported fixture; nothing was reset.");
            }
        }
        public static ExperimentGeometry CaptureGeometry(CombatArena arena)
        {
            if (!arena) throw new ArgumentNullException(nameof(arena));
            var boxes = new List<ExperimentBox>();
            var surfaces = UnityEngine.Object.FindObjectsByType<ArenaSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var surface in surfaces)
            {
                if (surface.gameObject.scene != arena.gameObject.scene || !surface.isActiveAndEnabled) throw new InvalidOperationException("Inactive or foreign Lab surfaces are unsupported.");
                var colliders = surface.GetComponents<Collider2D>();
                if (colliders.Length != 1 || !(colliders[0] is BoxCollider2D box)) throw new InvalidOperationException("Only one static axis-aligned BoxCollider2D per Lab surface is supported.");
                boxes.Add(CaptureBox("surface/" + surface.name, box, surface.oneWay, false));
            }
            foreach (var pit in arena.pits)
            {
                if (!pit || !pit.GetComponent<ArenaPit>() || pit.gameObject.scene != arena.gameObject.scene) throw new InvalidOperationException("The explicit Lab pit binding is invalid.");
                boxes.Add(CaptureBox("pit/" + pit.name, pit, false, true));
            }
            var actualPits = UnityEngine.Object.FindObjectsByType<ArenaPit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (actualPits.Length != arena.pits.Count || actualPits.Any(pit => !arena.pits.Any(box => box && box.gameObject == pit.gameObject))) throw new InvalidOperationException("Every Lab pit must have an explicit fixture binding.");
            return new ExperimentGeometry(boxes);
        }
        static ExperimentBox CaptureBox(string key, BoxCollider2D box, bool oneWay, bool pit)
        {
            var transform = box.transform; var matrix = transform.localToWorldMatrix; var body = box.attachedRigidbody;
            if (!box.enabled || !box.gameObject.activeInHierarchy || box.isTrigger != pit || box.usedByEffector || box.compositeOperation != Collider2D.CompositeOperation.None || box.edgeRadius != 0 || box.autoTiling ||
                body && (body.bodyType != RigidbodyType2D.Static || !body.simulated) || transform.GetComponentInParent<Animator>() || transform.GetComponentInParent<Animation>() ||
                Mathf.Abs(matrix.m01) > .000001f || Mathf.Abs(matrix.m10) > .000001f || Mathf.Abs(matrix.m02) > .000001f || Mathf.Abs(matrix.m12) > .000001f ||
                Mathf.Abs(matrix.m20) > .000001f || Mathf.Abs(matrix.m21) > .000001f || matrix.m00 <= 0 || matrix.m11 <= 0 || Mathf.Abs(matrix.m22 - 1) > .000001f || matrix.m23 != 0)
                throw new InvalidOperationException("Unsupported Lab geometry: only enabled, unanimated static axis-aligned boxes without effectors, composites, rounded edges or tiling may be captured.");
            Vector3 center = transform.TransformPoint(box.offset);
            return new ExperimentBox(key, center.x, center.y, box.size.x * matrix.m00, box.size.y * matrix.m11, oneWay, pit, box.gameObject.layer);
        }
        static string HashJson(object value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(value)))).Replace("-", "").ToLowerInvariant();
        }
        static string HashMedia(CombatArena arena)
        {
            if (arena.characterAnimations == null) throw new InvalidOperationException("The Lab requires its actual motion/media bindings.");
            var copy = JsonUtility.FromJson<CharacterAnimationCatalog>(JsonUtility.ToJson(arena.characterAnimations));
            // Attack timing/bindings are fully versioned by MotionRegistry, the
            // delegated first-sword Motion/Frame and the complete MeleeFrames.
            // Fingerprint only unaffected media, including actual FX bindings.
            if (copy.sets == null || !copy.sets.Any(set => set?.form == "base_sword" && set.clips != null && set.clips.Any(clip => clip?.name == "attack1")))
                throw new InvalidOperationException("First sword media is missing.");
            foreach (var set in copy.sets)
                if (set?.clips != null) set.clips = set.clips.Where(clip => clip?.name == null || !clip.name.StartsWith("attack", StringComparison.Ordinal)).ToArray();
            copy.motionRegistry = null;
            copy.motionSources = null;
            copy.meleeFrames = null;
            copy.specialFrames = null;
            return HashJson(copy);
        }
    }
}
