using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Narrative;
using Deviant.Narrative.Contracts;
using Deviant.Presentation.Contracts;
using Deviant.Sequences.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using UnityEngine;

namespace Deviant.Integration
{
    public sealed class SequenceDialogueBinding
    {
        public string Id { get; }
        public string PlacementId { get; }
        public SequenceDialogueBinding(string id, string placementId) { Id = Key(id); PlacementId = Key(placementId); }
        internal static string Key(string value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A stable sequence binding key is required.");
    }
    public sealed class SequenceCueBinding
    {
        public string Id { get; }
        public string AnchorId { get; }
        public string CueId { get; }
        public int Facing { get; }
        public SequenceCueBinding(string id, string anchorId, string cueId, int facing)
        {
            Id = SequenceDialogueBinding.Key(id); AnchorId = SequenceDialogueBinding.Key(anchorId);
            CueId = SequenceDialogueBinding.Key(cueId);
            if (facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(facing)); Facing = facing;
        }
    }
    public sealed class SequenceCameraBinding
    {
        public string Id { get; }
        public string AnchorId { get; }
        public CameraPosition Offset { get; }
        public int Priority { get; }
        public SequenceCameraBinding(string id, string anchorId, CameraPosition offset, int priority)
        { Id = SequenceDialogueBinding.Key(id); AnchorId = SequenceDialogueBinding.Key(anchorId); Offset = offset; Priority = priority; }
    }

    public sealed class SequenceFadeBinding
    {
        public string Id { get; }
        public FadeDefinition Definition { get; }
        public int Priority { get; }
        public SequenceFadeBinding(string id, FadeDefinition definition, int priority)
        { Id = SequenceDialogueBinding.Key(id); Definition = definition ?? throw new ArgumentNullException(nameof(definition)); Priority = priority; }
    }

    public sealed class SequenceStageActorBinding
    {
        public string Id { get; }
        public string ViewId { get; }
        public string FromAnchorId { get; }
        public string ToAnchorId { get; }
        public StageMoveDefinition Definition { get; }
        public SequenceStageActorBinding(string id, string viewId, string fromAnchorId, string toAnchorId, StageMoveDefinition definition)
        {
            Id = SequenceDialogueBinding.Key(id); ViewId = SequenceDialogueBinding.Key(viewId);
            FromAnchorId = SequenceDialogueBinding.Key(fromAnchorId); ToAnchorId = SequenceDialogueBinding.Key(toAnchorId);
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }

    // Explicit game/lab adapter. Its maps are copied at composition time. It
    // neither selects product policies nor writes world/story/combat state.
    public sealed class ChapterSequenceBindings : ISequenceBindings
    {
        readonly ChapterWorldHost world;
        readonly Guid session;
        readonly IInteractionTargets targets;
        readonly INarrativeConditions conditions;
        readonly InteractionPolicy interactionPolicy;
        readonly IDialogueView view;
        readonly IDialogueText text;
        readonly IControlRequests controls;
        readonly DialoguePresentationPolicy presentationPolicy;
        readonly ICuePlayer cues;
        readonly ICueCatalog cueCatalog;
        readonly ICameraRequests cameras;
        readonly Dictionary<string, SequenceDialogueBinding> dialogues;
        readonly Dictionary<string, SequenceCueBinding> cueBindings;
        readonly Dictionary<string, SequenceCameraBinding> cameraBindings;
        readonly IFadeRequests fades;
        readonly Dictionary<string, SequenceFadeBinding> fadeBindings;
        readonly Dictionary<string, SequenceStageActorBinding> stageActorBindings;
        readonly Dictionary<string, IStageActorRequests> stageViews;
        readonly ISequenceSkipEndpoint skipEndpoint;
        readonly IDialogueCommandBindings commandBindings;
        public ChapterSequenceBindings(ChapterWorldHost world, Guid session, IInteractionTargets targets, INarrativeConditions conditions,
            InteractionPolicy interactionPolicy, IDialogueView view, IDialogueText text, IControlRequests controls, DialoguePresentationPolicy presentationPolicy,
            ICuePlayer cues, ICueCatalog cueCatalog, ICameraRequests cameras, SequenceDialogueBinding[] dialogues, SequenceCueBinding[] cueBindings,
            SequenceCameraBinding[] cameraBindings, ISequenceSkipEndpoint skipEndpoint = null, IFadeRequests fades = null, SequenceFadeBinding[] fadeBindings = null,
            SequenceStageActorBinding[] stageActorBindings = null, IDictionary<string, IStageActorRequests> stageViews = null, IDialogueCommandBindings commandBindings = null)
        {
            if (!world || session == Guid.Empty) throw new ArgumentException("A chapter host and its expected session are required.");
            this.world = world; this.session = session;
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets)); this.conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            this.interactionPolicy = interactionPolicy ?? throw new ArgumentNullException(nameof(interactionPolicy));
            this.view = view ?? throw new ArgumentNullException(nameof(view)); this.text = text ?? throw new ArgumentNullException(nameof(text));
            this.controls = controls ?? throw new ArgumentNullException(nameof(controls)); this.presentationPolicy = presentationPolicy ?? throw new ArgumentNullException(nameof(presentationPolicy));
            this.cues = cues ?? throw new ArgumentNullException(nameof(cues)); this.cueCatalog = cueCatalog ?? throw new ArgumentNullException(nameof(cueCatalog));
            this.cameras = cameras ?? throw new ArgumentNullException(nameof(cameras));
            this.dialogues = Copy(dialogues, b => b.Id); this.cueBindings = Copy(cueBindings, b => b.Id); this.cameraBindings = Copy(cameraBindings, b => b.Id);
            this.skipEndpoint = skipEndpoint; this.fades = fades; this.commandBindings = commandBindings;
            this.fadeBindings = Copy(fadeBindings ?? Array.Empty<SequenceFadeBinding>(), b => b.Id);
            this.stageActorBindings = Copy(stageActorBindings ?? Array.Empty<SequenceStageActorBinding>(), b => b.Id);
            this.stageViews = stageViews == null ? new Dictionary<string, IStageActorRequests>(StringComparer.Ordinal) : new Dictionary<string, IStageActorRequests>(stageViews, StringComparer.Ordinal);
            if (this.stageViews.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value == null)) throw new ArgumentException("Stage view IDs and providers are required.", nameof(stageViews));
        }
        static Dictionary<string, T> Copy<T>(T[] source, Func<T, string> key) where T : class
        {
            if (source == null || source.Any(b => b == null)) throw new ArgumentException("Non-null binding lists are required.");
            return source.ToDictionary(key, StringComparer.Ordinal);
        }
        bool IsActive => world && world.Session != null && world.Session.Id == session && world.Session.IsActive;
        public bool Validate(SequenceDefinition definition, out string error)
        {
            error = null;
            try
            {
                if (definition == null || !IsActive) throw new InvalidOperationException("Sequence chapter session is unavailable.");
                foreach (var anchor in definition.RequiredAnchors) ReadAnchor(anchor);
                CaptureCues(definition);
                foreach (var group in definition.Steps.Where(s => s.Kind == SequenceStepKind.ParallelAll))
                {
                    if (group.Children.Count(c => c.Kind == SequenceStepKind.Dialogue) > 1)
                        throw new InvalidOperationException("This sequence binding has one dialogue view; parallel dialogue steps require distinct views.");
                    if (group.Children.Count(c => c.Kind == SequenceStepKind.Fade) > 1)
                        throw new InvalidOperationException("This sequence binding has one fade channel; concurrent fades require distinct channels.");
                }
                foreach (var step in definition.Leaves)
                {
                    if (step.Kind == SequenceStepKind.Dialogue)
                    {
                        if (!dialogues.TryGetValue(step.BindingId, out var binding) || !targets.TryRead(session, binding.PlacementId, out var target))
                            throw new InvalidOperationException("Sequence NPC binding is unavailable: " + step.BindingId);
                        if (!DialogueSession.ValidateCommands(target.Definition.Dialogue, commandBindings, out error)) throw new InvalidOperationException(error ?? "Sequence dialogue commands are unavailable.");
                    }
                    else if (step.Kind == SequenceStepKind.Cue)
                    {
                        if (!cueBindings.TryGetValue(step.BindingId, out var binding)) throw new InvalidOperationException("Missing sequence Cue binding: " + step.BindingId);
                        RequireAnchor(definition, binding.AnchorId);
                    }
                    else if (step.Kind == SequenceStepKind.Camera)
                    {
                        if (!cameraBindings.TryGetValue(step.BindingId, out var binding)) throw new InvalidOperationException("Missing sequence camera binding: " + step.BindingId);
                        RequireAnchor(definition, binding.AnchorId);
                    }
                    else if (step.Kind == SequenceStepKind.Fade)
                    {
                        if (!fadeBindings.ContainsKey(step.BindingId)) throw new InvalidOperationException("Missing sequence fade binding: " + step.BindingId);
                        if (fades == null || !fades.IsAvailable || fades.SessionId == Guid.Empty) throw new InvalidOperationException("Sequence fade output is unavailable.");
                    }
                    else if (step.Kind == SequenceStepKind.StageMove)
                    {
                        if (!stageActorBindings.TryGetValue(step.BindingId, out var binding)) throw new InvalidOperationException("Missing sequence stage actor binding: " + step.BindingId);
                        RequireAnchor(definition, binding.FromAnchorId); RequireAnchor(definition, binding.ToAnchorId);
                        var actor = StageView(binding.ViewId);
                        if (!actor.IsAvailable || actor.SessionId == Guid.Empty || actor.IsOwned) throw new InvalidOperationException("Stage view is unavailable or already owned: " + binding.ViewId);
                    }
                }
                var views = definition.Leaves.Where(s => s.Kind == SequenceStepKind.StageMove).Select(s => stageActorBindings[s.BindingId].ViewId).Distinct(StringComparer.Ordinal).ToArray();
                if (views.Select(v => StageView(v).SessionId).Distinct().Count() != views.Length) throw new InvalidOperationException("Stage view IDs must resolve to distinct output sessions.");
                foreach (var group in definition.Steps.Where(s => s.Kind == SequenceStepKind.ParallelAll))
                {
                    var actors = group.Children.Where(s => s.Kind == SequenceStepKind.StageMove).Select(s => stageActorBindings[s.BindingId].ViewId).ToArray();
                    if (actors.Distinct(StringComparer.Ordinal).Count() != actors.Length) throw new InvalidOperationException("A stage view can run only one move per parallel group.");
                }
                if (definition.Skip != null && (skipEndpoint == null || !skipEndpoint.Validate(definition.Skip.EndpointId, out error)))
                    throw new InvalidOperationException(error ?? "Sequence skip endpoint is unavailable.");
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        ICueCatalogSnapshot CaptureCues(SequenceDefinition definition)
        {
            var ids = new List<string>();
            foreach (var step in definition.Leaves.Where(s => s.Kind == SequenceStepKind.Cue))
            {
                if (!cueBindings.TryGetValue(step.BindingId, out var binding)) throw new InvalidOperationException("Missing sequence Cue binding: " + step.BindingId);
                RequireAnchor(definition, binding.AnchorId); ids.Add(binding.CueId);
            }
            var captured = cueCatalog.Capture(ids) ?? throw new InvalidOperationException("Sequence Cue catalog returned no version.");
            if (captured.Version == Guid.Empty) throw new InvalidOperationException("Sequence Cue catalog version is missing.");
            foreach (var id in ids)
            {
                var cue = captured.Read(id);
                if (!(cue is PresentationCueDefinition) && !(cue is AudioCueDefinition) || cue.Id != id)
                    throw new InvalidOperationException("Sequence Cue catalog must resolve the requested Sprite or Audio definition: " + id);
                if (cue.Loop) throw new InvalidOperationException("Sequence Cue steps require a finite one-shot Cue.");
            }
            return captured;
        }
        IStageActorRequests StageView(string id)
        {
            if (!stageViews.TryGetValue(id, out var view)) throw new InvalidOperationException("Missing stage view: " + id);
            return view;
        }
        static void RequireAnchor(SequenceDefinition definition, string anchor)
        {
            if (!definition.RequiredAnchors.Contains(anchor)) throw new InvalidOperationException("Sequence must declare its binding anchor: " + anchor);
        }
        Vector2 ReadAnchor(string anchor)
        {
            if (!IsActive || !world.TryGetWorldAnchor(session, anchor, out var point, out _)) throw new InvalidOperationException("Sequence anchor is missing or belongs to an ended chapter: " + anchor);
            return point;
        }
        public ISequenceBindingScope OpenScope(Guid executionId, SequenceDefinition definition)
        {
            if (executionId == Guid.Empty || definition == null || !IsActive) throw new InvalidOperationException("A live sequence execution, definition and chapter are required.");
            return new Scope(this, executionId, definition);
        }
        sealed class Scope : ISequenceBindingScope
        {
            readonly ChapterSequenceBindings owner;
            readonly Guid execution;
            readonly List<ISequenceOperation> operations = new List<ISequenceOperation>();
            readonly Dictionary<string, NpcTarget> selectedTargets = new Dictionary<string, NpcTarget>(StringComparer.Ordinal);
            readonly ICueCatalogSnapshot selectedCues;
            readonly Guid? cueSession;
            ICueScope cueScope;
            ICameraScope cameraScope;
            IFadeScope fadeScope;
            readonly Guid? fadeSession;
            readonly Dictionary<string, Guid> stageSessions = new Dictionary<string, Guid>(StringComparer.Ordinal);
            readonly Dictionary<string, IStageActorScope> actorScopes = new Dictionary<string, IStageActorScope>(StringComparer.Ordinal);
            bool closed, skipStarted;
            int ordinal;
            public Scope(ChapterSequenceBindings owner, Guid execution, SequenceDefinition definition)
            {
                this.owner = owner; this.execution = execution;
                selectedCues = owner.CaptureCues(definition);
                if (definition.Leaves.Any(s => s.Kind == SequenceStepKind.Cue))
                {
                    if (owner.cues.SessionId == Guid.Empty) throw new InvalidOperationException("Sequence Cue player session is unavailable.");
                    cueSession = owner.cues.SessionId;
                }
                if (definition.Leaves.Any(s => s.Kind == SequenceStepKind.Fade))
                {
                    if (owner.fades == null || !owner.fades.IsAvailable || owner.fades.SessionId == Guid.Empty) throw new InvalidOperationException("Sequence fade output is unavailable.");
                    fadeSession = owner.fades.SessionId;
                }
                foreach (var step in definition.Leaves.Where(s => s.Kind == SequenceStepKind.StageMove))
                {
                    var id = owner.stageActorBindings[step.BindingId].ViewId;
                    if (stageSessions.ContainsKey(id)) continue;
                    var actor = owner.StageView(id);
                    if (!actor.IsAvailable || actor.SessionId == Guid.Empty || actor.IsOwned) throw new InvalidOperationException("Stage view is unavailable or owned: " + id);
                    stageSessions.Add(id, actor.SessionId);
                }
                foreach (var step in definition.Leaves.Where(s => s.Kind == SequenceStepKind.Dialogue))
                {
                    if (selectedTargets.ContainsKey(step.BindingId)) continue;
                    var binding = owner.dialogues[step.BindingId];
                    if (!owner.targets.TryRead(owner.session, binding.PlacementId, out var target)) throw new InvalidOperationException("Sequence NPC binding is unavailable: " + step.BindingId);
                    selectedTargets.Add(step.BindingId, target);
                }
            }
            public bool IsActive => !closed && owner.IsActive && (!fadeSession.HasValue || owner.fades.IsAvailable && owner.fades.SessionId == fadeSession.Value)
                && (!cueSession.HasValue || owner.cues.SessionId == cueSession.Value)
                && stageSessions.All(p => owner.StageView(p.Key).IsAvailable && owner.StageView(p.Key).SessionId == p.Value)
                && actorScopes.Values.All(s => s.IsActive);
            public ISequenceOperation Begin(SequenceStep step)
            {
                Check(); ISequenceOperation operation;
                switch (step.Kind)
                {
                    case SequenceStepKind.Dialogue:
                        operation = new DialogueOperation(owner, selectedTargets[step.BindingId]); break;
                    case SequenceStepKind.Cue:
                        var cue = owner.cueBindings[step.BindingId]; var point = owner.ReadAnchor(cue.AnchorId);
                        if (cueScope == null) cueScope = owner.cues.OpenScope("sequence " + execution + " catalog " + selectedCues.Version, selectedCues);
                        var clock = new StepClock();
                        var selectedCue = selectedCues.Read(cue.CueId);
                        var handle = cueScope.Play(selectedCue, new CueOccurrence(execution, selectedCue.Id, ordinal++), new CueAnchor(point.x, point.y, cue.Facing), clock);
                        operation = new CueOperation(handle, clock); break;
                    case SequenceStepKind.Camera:
                        var camera = owner.cameraBindings[step.BindingId]; owner.ReadAnchor(camera.AnchorId);
                        if (cameraScope == null) cameraScope = owner.cameras.OpenScope("sequence " + execution);
                        var source = new ChapterCameraSource(owner.world, owner.session, camera.AnchorId, new Vector3(camera.Offset.X, camera.Offset.Y, camera.Offset.Z));
                        operation = new CameraOperation(cameraScope.Request(camera.Priority, source), source, step.Duration); break;
                    case SequenceStepKind.Fade:
                        if (!fadeSession.HasValue) throw new InvalidOperationException("Fade was not declared in this sequence scope.");
                        var fade = owner.fadeBindings[step.BindingId];
                        if (fadeScope == null) fadeScope = owner.fades.OpenScope("sequence " + execution);
                        Check();
                        var playback = fadeScope.Play(fade.Definition, fade.Priority);
                        if (playback == null || playback.SessionId != fadeSession.Value) throw new InvalidOperationException("Sequence fade handle belongs to a missing or stale output session.");
                        operation = new FadeOperation(playback, fade.Definition.HoldAtEnd); break;
                    case SequenceStepKind.StageMove:
                        var stage = owner.stageActorBindings[step.BindingId];
                        if (!stageSessions.ContainsKey(stage.ViewId)) throw new InvalidOperationException("Stage view was not declared in this scope.");
                        if (!actorScopes.TryGetValue(stage.ViewId, out var actorScope))
                        { actorScope = owner.StageView(stage.ViewId).OpenScope("sequence " + execution); actorScopes.Add(stage.ViewId, actorScope); }
                        Check();
                        var from = owner.ReadAnchor(stage.FromAnchorId); var to = owner.ReadAnchor(stage.ToAnchorId);
                        var motion = actorScope.Play(stage.Definition, new StagePoint(from.x, from.y), new StagePoint(to.x, to.y));
                        if (motion == null || motion.SessionId != stageSessions[stage.ViewId]) throw new InvalidOperationException("Stage move returned a missing or stale handle.");
                        operation = new StageOperation(motion); break;
                    default: throw new ArgumentException("Only bound leaf steps are accepted; groups and waits are owned by SequenceRunner.", nameof(step));
                }
                operations.Add(operation); return operation;
            }
            public ISequenceOperation BeginSkip(SequenceSkipDefinition skip)
            {
                Check();
                if (skipStarted || skip == null || owner.skipEndpoint == null) throw new InvalidOperationException("Skip endpoint is unavailable or already started.");
                skipStarted = true;
                var operation = owner.skipEndpoint.Begin(execution, skip.EndpointId) ?? throw new InvalidOperationException("Skip endpoint returned no operation.");
                operations.Add(operation); return operation;
            }
            void Check() { if (!IsActive) throw new ObjectDisposedException(nameof(ISequenceBindingScope)); }
            public void Dispose()
            {
                if (closed) return; closed = true; var errors = new List<Exception>();
                foreach (var operation in operations) Release(operation, errors);
                operations.Clear(); Release(cueScope, errors); Release(cameraScope, errors); Release(fadeScope, errors);
                foreach (var scope in actorScopes.Values) Release(scope, errors); actorScopes.Clear();
                if (errors.Count > 0) throw new AggregateException("Sequence binding cleanup failed.", errors);
            }
            static void Release(IDisposable value, List<Exception> errors) { try { value?.Dispose(); } catch (Exception exception) { errors.Add(exception); } }
        }
        sealed class DialogueOperation : ISequenceOperation
        {
            readonly InteractionService interactions;
            readonly IDialogueCommands dialogue;
            DialoguePresenter presenter;
            bool disposed;
            public SequenceOperationStatus Status { get; private set; } = SequenceOperationStatus.Running;
            public string Error { get; private set; }
            public DialogueOperation(ChapterSequenceBindings owner, NpcTarget selected)
            {
                interactions = new InteractionService(owner.targets, owner.conditions, owner.interactionPolicy, owner.commandBindings);
                try
                {
                    if (!interactions.TryStart(selected, out dialogue, out var error))
                        throw new InvalidOperationException("Sequence dialogue could not start: " + error);
                    presenter = new DialoguePresenter(dialogue, owner.view, owner.text, owner.controls, owner.presentationPolicy);
                    Poll(0);
                }
                catch { presenter?.Dispose(); interactions.Dispose(); throw; }
            }
            public void Poll(double elapsed)
            {
                if (disposed || Status != SequenceOperationStatus.Running) return;
                presenter.Refresh(); var snapshot = dialogue.Read();
                if (presenter.CleanupError != null) { Status = SequenceOperationStatus.Failed; Error = presenter.CleanupError; return; }
                switch (snapshot.Status)
                {
                    case DialogueStatus.Completed: Status = SequenceOperationStatus.Completed; break;
                    case DialogueStatus.Cancelled: Status = SequenceOperationStatus.Cancelled; Error = snapshot.EndReason.ToString(); break;
                    case DialogueStatus.Failed: Status = SequenceOperationStatus.Failed; Error = snapshot.Error ?? presenter.LastError ?? snapshot.EndReason.ToString(); break;
                }
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                if (Status == SequenceOperationStatus.Running) Status = SequenceOperationStatus.Cancelled;
                try { presenter?.Dispose(); } finally { interactions.Dispose(); }
                if (presenter?.CleanupError != null) throw new InvalidOperationException("Dialogue cleanup failed: " + presenter.CleanupError);
            }
        }
        sealed class StepClock : ICueClock { public float Time; public float ReadElapsed() => Time; }
        sealed class CueOperation : ISequenceOperation
        {
            readonly ICueHandle handle;
            readonly StepClock clock;
            bool disposed;
            public SequenceOperationStatus Status => disposed && handle.Status != CueStatus.Completed && handle.Status != CueStatus.Failed ? SequenceOperationStatus.Cancelled :
                handle.Status == CueStatus.Completed ? SequenceOperationStatus.Completed : handle.Status == CueStatus.Cancelled ? SequenceOperationStatus.Cancelled :
                handle.Status == CueStatus.Failed ? SequenceOperationStatus.Failed : SequenceOperationStatus.Running;
            public string Error => handle.Error;
            public CueOperation(ICueHandle handle, StepClock clock) { this.handle = handle; this.clock = clock; }
            public void Poll(double elapsed)
            {
                if (disposed || Status != SequenceOperationStatus.Running) return;
                if (elapsed > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(elapsed));
                clock.Time = (float)elapsed; handle.Poll();
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                string previous = handle.Error; handle.Dispose();
                if (handle.Error != previous) throw new InvalidOperationException(handle.Error);
            }
        }
        sealed class StageOperation : ISequenceOperation
        {
            readonly IStageActorHandle handle;
            bool disposed;
            public SequenceOperationStatus Status => handle.Status == StageMoveStatus.Completed ? SequenceOperationStatus.Completed :
                handle.Status == StageMoveStatus.Cancelled ? SequenceOperationStatus.Cancelled : handle.Status == StageMoveStatus.Failed ? SequenceOperationStatus.Failed : SequenceOperationStatus.Running;
            public string Error => handle.Error;
            public StageOperation(IStageActorHandle handle) { this.handle = handle; }
            public void Poll(double elapsed) { if (!disposed && Status == SequenceOperationStatus.Running) handle.Poll(elapsed); }
            public void Dispose() { if (disposed) return; disposed = true; if (handle.Status != StageMoveStatus.Completed) handle.Dispose(); }
        }
        sealed class FadeOperation : ISequenceOperation
        {
            readonly IFadeHandle handle;
            readonly bool holdAtEnd;
            bool disposed;
            public SequenceOperationStatus Status => handle.Status == FadeStatus.Completed ? SequenceOperationStatus.Completed :
                handle.Status == FadeStatus.Cancelled ? SequenceOperationStatus.Cancelled : handle.Status == FadeStatus.Failed ? SequenceOperationStatus.Failed : SequenceOperationStatus.Running;
            public string Error => handle.Error;
            public FadeOperation(IFadeHandle handle, bool holdAtEnd) { this.handle = handle; this.holdAtEnd = holdAtEnd; }
            public void Poll(double elapsed) { if (!disposed && Status == SequenceOperationStatus.Running) handle.Poll(elapsed); }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                // Retained output belongs to the enclosing binding scope after
                // playback completes. The next fade replaces it; scope end returns it.
                if (handle.Status != FadeStatus.Completed || !holdAtEnd) handle.Dispose();
            }
        }
        sealed class CameraOperation : ISequenceOperation
        {
            readonly ICameraHandle handle;
            readonly ICameraPositionSource source;
            readonly double duration;
            bool disposed;
            public SequenceOperationStatus Status => handle.Status == CameraRequestStatus.Completed ? SequenceOperationStatus.Completed :
                handle.Status == CameraRequestStatus.Failed ? SequenceOperationStatus.Failed : handle.Status == CameraRequestStatus.Cancelled ? SequenceOperationStatus.Cancelled : SequenceOperationStatus.Running;
            public string Error => handle.Error;
            public CameraOperation(ICameraHandle handle, ICameraPositionSource source, double duration) { this.handle = handle; this.source = source; this.duration = duration; }
            public void Poll(double elapsed)
            {
                if (disposed || Status != SequenceOperationStatus.Running) return;
                source.Read(); // Detect a lost required anchor even while another framing wins.
                if (elapsed >= duration) handle.Complete();
            }
            public void Dispose() { if (disposed) return; disposed = true; handle.Dispose(); }
        }
    }
}
