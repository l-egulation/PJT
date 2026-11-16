using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;

namespace Deviant.CombatLab
{
    public readonly struct ExperimentHitCorrelation
    {
        public CombatAcceptedHit Hit { get; }
        public string SourceActorKey { get; }
        public string TargetActorKey { get; }
        public int AdmissionTick { get; }
        public float AdmittedAt { get; }
        public bool HasStart { get; }
        public CombatCommandOutcome Start { get; }
        public float InputToStart => AdmissionTick >= 0 && HasStart ? Start.Now - AdmittedAt : float.NaN;
        public float StartToHit => HasStart ? Hit.Now - Start.Now : float.NaN;
        internal ExperimentHitCorrelation(CombatAcceptedHit hit, string source, string target, int admissionTick, float admittedAt,
            bool hasStart, CombatCommandOutcome start)
        { Hit = hit; SourceActorKey = source; TargetActorKey = target; AdmissionTick = admissionTick; AdmittedAt = admittedAt; HasStart = hasStart; Start = start; }
    }

    public readonly struct ExperimentCommandObservation
    {
        public CombatCommandOutcome Outcome { get; }
        public string ActorKey { get; }
        public int ActorOrdinal { get; }
        public int EventOrdinal { get; }
        public int CommandOrdinal { get; }
        public int ExecutionOrdinal { get; }
        public int AdmissionTick { get; }
        internal ExperimentCommandObservation(CombatCommandOutcome outcome, string actorKey, int actorOrdinal, int eventOrdinal, int commandOrdinal, int executionOrdinal, int admissionTick)
        { Outcome = outcome; ActorKey = actorKey; ActorOrdinal = actorOrdinal; EventOrdinal = eventOrdinal; CommandOrdinal = commandOrdinal; ExecutionOrdinal = executionOrdinal; AdmissionTick = admissionTick; }
    }
    public readonly struct ExperimentHitIdentityObservation
    {
        public CombatAcceptedHit Hit { get; }
        public int SourceActorOrdinal { get; }
        public int EffectiveSourceActorOrdinal { get; }
        public int TargetActorOrdinal { get; }
        public int ReflectorActorOrdinal { get; }
        public int CommandOrdinal { get; }
        public int ExecutionOrdinal { get; }
        public int EffectOrdinal { get; }
        public int ReflectorCommandOrdinal { get; }
        public int ReflectorExecutionOrdinal { get; }
        internal ExperimentHitIdentityObservation(CombatAcceptedHit hit, int source, int effectiveSource, int target, int reflector,
            int command, int execution, int effect, int reflectorCommand, int reflectorExecution)
        {
            Hit = hit; SourceActorOrdinal = source; EffectiveSourceActorOrdinal = effectiveSource; TargetActorOrdinal = target; ReflectorActorOrdinal = reflector;
            CommandOrdinal = command; ExecutionOrdinal = execution; EffectOrdinal = effect; ReflectorCommandOrdinal = reflectorCommand; ReflectorExecutionOrdinal = reflectorExecution;
        }
    }


    // Experiment-only observer. It uses actual admitted sequence ranges and actual
    // Started receipts, never a cast/HP delta or a diagnostic string as a hit fact.
    sealed class ExperimentReplayObserver
    {
        readonly CombatArena arena;
        readonly CombatActorHandle[] actors;
        readonly string[] keys;
        readonly CombatAcceptedHit[] hitPage = new CombatAcceptedHit[128];
        readonly CombatCommandOutcome[] commandPage = new CombatCommandOutcome[128];
        readonly Dictionary<CombatCommandKey, CombatCommandOutcome> starts = new Dictionary<CombatCommandKey, CombatCommandOutcome>();
        readonly List<Submission> submissions = new List<Submission>();
        readonly List<ExperimentHitCorrelation> hits = new List<ExperimentHitCorrelation>();
        readonly List<ExperimentCommandObservation> outcomes = new List<ExperimentCommandObservation>();
        readonly Dictionary<CombatCommandKey, int> commandOrdinals = new Dictionary<CombatCommandKey, int>();
        readonly Dictionary<Guid, int> executionOrdinals = new Dictionary<Guid, int>();
        readonly Dictionary<CombatActorHandle, int> actorOrdinals = new Dictionary<CombatActorHandle, int>();
        readonly Dictionary<Guid, int> effectOrdinals = new Dictionary<Guid, int>();
        readonly List<ExperimentHitIdentityObservation> hitIdentities = new List<ExperimentHitIdentityObservation>();
        public int OutcomeCount => outcomes.Count;
        CombatOutcomeCursor hitCursor, commandCursor;
        readonly struct Submission
        {
            public readonly long First, Last;
            public readonly int Tick;
            public readonly float Now;
            public Submission(long first, long last, int tick, float now) { First = first; Last = last; Tick = tick; Now = now; }
        }
        public ExperimentReplayObserver(CombatArena arena, CombatActorHandle[] actors, string[] keys)
        {
            this.arena = arena; this.actors = actors; this.keys = keys;
            foreach (var actor in actors) ActorOrdinal(actor); // Fixed fixture order, then actual first-observed spawned identities.
            if (!arena.HitEvents.TryGetCursor(out hitCursor)) throw new InvalidOperationException("Accepted-hit observer cannot bind to the reset session.");
            commandCursor = new CombatOutcomeCursor(hitCursor.Session);
            DrainCommands(false);
        }
        public void Observe(int tick, float admittedAt, long firstSequence, long nextSequence)
        {
            submissions.Add(new Submission(firstSequence, nextSequence, tick, admittedAt));
            DrainCommands(true);
            while (true)
            {
                if (!arena.HitEvents.TryRead(hitCursor, hitPage, out var page) || page.DroppedBefore != 0)
                    throw new InvalidOperationException("Accepted-hit observation lost its session or overflowed; incomplete metrics are not published.");
                hitCursor = page.Next;
                for (int i = 0; i < page.Count; i++)
                {
                    var hit = hitPage[i]; var origin = hit.Origin;
                    if (origin.Source.ActorId <= 0 || hit.Target.ActorId <= 0 || !origin.Source.Session.Equals(hitCursor.Session) || !hit.Target.Session.Equals(hitCursor.Session) ||
                        origin.Reflector.ActorId > 0 && !origin.Reflector.Session.Equals(hitCursor.Session))
                        throw new InvalidOperationException("Accepted-hit identity crossed an explicit actor/session boundary.");
                    bool hasStart = starts.TryGetValue(origin.Command, out var start) && start.ExecutionId == origin.ExecutionId;
                    if (!hasStart) start = default;
                    int inputTick = -1; float inputNow = float.NaN;
                    if (origin.Command.IsValid && origin.Command.Actor.ActorId == hitCursor.Session.ActorId)
                        foreach (var submission in submissions)
                            if (origin.Command.Sequence >= submission.First && origin.Command.Sequence < submission.Last)
                            { inputTick = submission.Tick; inputNow = submission.Now; break; }
                    hits.Add(new ExperimentHitCorrelation(hit, ActorKey(origin.EffectiveSource), ActorKey(hit.Target), inputTick, inputNow, hasStart, start));
                    hitIdentities.Add(new ExperimentHitIdentityObservation(hit, ActorOrdinal(origin.Source), ActorOrdinal(origin.EffectiveSource), ActorOrdinal(hit.Target), ActorOrdinal(origin.Reflector),
                        CommandOrdinal(origin.Command), GuidOrdinal(executionOrdinals, origin.ExecutionId), GuidOrdinal(effectOrdinals, origin.EffectId), CommandOrdinal(origin.ReflectorCommand), GuidOrdinal(executionOrdinals, origin.ReflectorExecutionId)));
                }
                if (page.Count < hitPage.Length) return;
            }
        }
        void DrainCommands(bool retain)
        {
            while (true)
            {
                if (!arena.Commands.TryReadOutcomes(commandCursor, commandPage, out var page)) throw new InvalidOperationException("Command observer lost its reset session.");
                if (retain && page.DroppedBefore != 0) throw new InvalidOperationException("Command observation overflowed; incomplete hit correlation is not published.");
                commandCursor = page.Next;
                if (retain) for (int i = 0; i < page.Count; i++)
                {
                    var outcome = commandPage[i];
                    if (!outcome.Command.Actor.Session.Equals(commandCursor.Session))
                        throw new InvalidOperationException("Command observation crossed an explicit session boundary; no implicit rebind is allowed.");
                    if (outcomes.Count == ExperimentScenarioCommandCapacity)
                        throw new InvalidOperationException("The bounded experiment command observation capacity was exceeded; incomplete comparison is not published.");
                    int commandOrdinal = CommandOrdinal(outcome.Command);
                    int executionOrdinal = GuidOrdinal(executionOrdinals, outcome.ExecutionId);
                    int admissionTick = -1;
                    if (outcome.Command.Actor.ActorId == commandCursor.Session.ActorId)
                        foreach (var submission in submissions)
                            if (outcome.Command.Sequence >= submission.First && outcome.Command.Sequence < submission.Last) { admissionTick = submission.Tick; break; }
                    outcomes.Add(new ExperimentCommandObservation(outcome, ActorKey(outcome.Command.Actor), ActorOrdinal(outcome.Command.Actor), outcomes.Count + 1, commandOrdinal, executionOrdinal, admissionTick));
                    if (outcome.State == CombatCommandState.Started) starts[outcome.Command] = outcome;
                }
                if (page.Count < commandPage.Length) return;
            }
        }
        string ActorKey(CombatActorHandle actor)
        {
            for (int i = 0; i < actors.Length; i++) if (actors[i].Equals(actor)) return keys[i];
            return null; // A spawned decoy has real runtime identity but no saved fixture key.
        }
        public ExperimentHitCorrelation[] Complete() => hits.ToArray();
        const int ExperimentScenarioCommandCapacity = 65536;
        public ExperimentCommandObservation[] CompleteOutcomes() => outcomes.ToArray();
        public ExperimentHitIdentityObservation[] CompleteHitIdentities() => hitIdentities.ToArray();
        int ActorOrdinal(CombatActorHandle actor)
        {
            if (actor.ActorId <= 0)
            {
                if (!actor.Equals(default(CombatActorHandle))) throw new InvalidOperationException("Malformed absent actor identity cannot share the environment/no-actor ordinal.");
                return 0;
            }
            if (!actorOrdinals.TryGetValue(actor, out int ordinal)) { ordinal = actorOrdinals.Count + 1; actorOrdinals.Add(actor, ordinal); }
            return ordinal;
        }
        int CommandOrdinal(CombatCommandKey command)
        {
            if (!command.IsValid) return 0;
            if (!commandOrdinals.TryGetValue(command, out int ordinal)) { ordinal = commandOrdinals.Count + 1; commandOrdinals.Add(command, ordinal); }
            return ordinal;
        }
        static int GuidOrdinal(Dictionary<Guid, int> ordinals, Guid id)
        {
            if (id == Guid.Empty) return 0;
            if (!ordinals.TryGetValue(id, out int ordinal)) { ordinal = ordinals.Count + 1; ordinals.Add(id, ordinal); }
            return ordinal;
        }
    }
}
