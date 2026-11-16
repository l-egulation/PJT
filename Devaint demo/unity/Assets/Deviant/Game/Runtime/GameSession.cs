using System;
using System.Collections.Generic;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Equipment.Contracts;
using Deviant.Integration;
using Deviant.Narrative.Contracts;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using Deviant.UI.Presentation;
using Deviant.World;

namespace Deviant.Game
{
    public enum GameSessionEndReason { None, PlayerDied, DevelopmentReset, Disabled, Destroyed, CombatUnavailable, InitializationFailed }

    // One current prototype play lifetime. This is not a new Run progression,
    // save, reward, respawn policy, or a traversal-validated forest chapter.
    public sealed class GameSession : IDisposable
    {
        readonly List<DialogueChild> dialogues = new List<DialogueChild>();
        readonly List<SequenceChild> sequences = new List<SequenceChild>();
        readonly SessionControls controls;
        bool executing, releasing, released;
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsActive { get; private set; } = true;
        public bool IsClosed => released;
        public GameSessionEndReason EndReason { get; private set; }
        public string CleanupError { get; private set; }
        public IWeaponLoadout Loadout { get; }
        public IControlRequests Controls => controls;
        public ScreenRouter Screens { get; }
        public PrototypeForestSession Forest { get; }
        public GameSession(IWeaponLoadout loadout, IControlRequests sharedControls, InkForestWorld forest, Action<InkForestChunk> prepareChunk)
        {
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            Loadout.Read(); // Reject an invalid composition owner before acquiring any view/control lease.
            controls = new SessionControls(sharedControls ?? throw new ArgumentNullException(nameof(sharedControls)));
            Screens = new ScreenRouter(controls);
            try { Forest = new PrototypeForestSession(forest, prepareChunk); }
            catch
            {
                try { Screens.Dispose(); } finally { controls.Dispose(); }
                throw;
            }
        }
        public bool TryConfigureDialogue(Guid expected, NpcDialogueHost host, IInteractionTargets targets, INarrativeConditions conditions,
            InteractionPolicy interactionPolicy, Deviant.UI.Contracts.IDialogueView view, Deviant.UI.Contracts.IDialogueText text,
            DialoguePresentationPolicy presentationPolicy, out string error, IDialogueCommandBindings commands = null, DialogueSourceBinding source = null)
        {
            error = null;
            if (!Accept(expected, out error)) return false;
            if (!host || host.BindingId != Guid.Empty || host.Current != null || dialogues.Exists(child => ReferenceEquals(child.Host, host))) { error = "An explicit idle, unregistered dialogue host is required."; return false; }
            executing = true;
            try
            {
                host.Configure(targets, conditions, interactionPolicy, view, text, controls, presentationPolicy, commands, source);
                dialogues.Add(new DialogueChild(host)); return IsActive;
            }
            catch (Exception exception) { error = exception.Message; return false; }
            finally { executing = false; FlushClose(); }
        }
        public bool TryInteract(Guid expected, NpcDialogueHost host, NpcTarget selected, out string error)
        {
            if (!Accept(expected, out error)) return false;
            var child = dialogues.Find(item => ReferenceEquals(item.Host, host));
            if (child == null || !host || child.BindingId != host.BindingId) { error = "Dialogue host is not owned by this session generation."; return false; }
            executing = true;
            try
            {
                bool started = host.TryInteract(selected, out error);
                return started && IsActive;
            }
            finally { executing = false; FlushClose(); }
        }
        public bool TryStartSequence(Guid expected, SequenceHost host, SequenceDefinition definition, ISequenceBindings bindings,
            SequenceControlPolicy policy, ISequenceClock clock, out string error)
        {
            if (!Accept(expected, out error)) return false;
            if (!host) { error = "An explicit sequence host is required."; return false; }
            var child = sequences.Find(item => ReferenceEquals(item.Host, host));
            if (host.Current != null && host.Current.Read().Status == SequenceStatus.Running || child != null && !ReferenceEquals(child.Execution, host.Current))
            { error = "Sequence host is busy or changed outside this session."; return false; }
            executing = true;
            try
            {
                var previous = host.Current;
                bool started = host.TryStart(definition, bindings, controls, policy, clock, out error);
                if (!ReferenceEquals(previous, host.Current))
                {
                    if (child == null) { child = new SequenceChild(host); sequences.Add(child); }
                    child.Execution = host.Current;
                }
                return started && IsActive;
            }
            finally { executing = false; FlushClose(); }
        }
        bool Accept(Guid expected, out string error)
        {
            error = IsActive && expected == Id && !executing ? null : "Game session has ended, is busy, or its token is stale.";
            return error == null;
        }
        public void Close(GameSessionEndReason reason)
        {
            if (!IsActive) return;
            if (reason == GameSessionEndReason.None) throw new ArgumentOutOfRangeException(nameof(reason));
            IsActive = false; EndReason = reason;
            if (!executing) FlushClose();
        }
        void FlushClose()
        {
            if (IsActive || released || releasing || executing) return;
            releasing = true;
            for (int i = sequences.Count - 1; i >= 0; i--)
            {
                var child = sequences[i];
                if (child.Execution != null) Release(child.Execution.Dispose);
            }
            for (int i = dialogues.Count - 1; i >= 0; i--)
            {
                var child = dialogues[i];
                if (child.Host && child.BindingId == child.Host.BindingId)
                    Release(() => child.Host.TryClose(child.BindingId, EndReason == GameSessionEndReason.PlayerDied ? DialogueEndReason.RunEnded : DialogueEndReason.OwnerDisposed, out _));
            }
            sequences.Clear(); dialogues.Clear();
            Release(Screens.Dispose); Release(controls.Dispose); Release(Forest.Dispose);
            releasing = false; released = true;
        }
        void Release(Action close)
        {
            try { close(); }
            catch (Exception exception) { CleanupError = CleanupError == null ? exception.Message : CleanupError + "; " + exception.Message; }
        }
        public void Dispose() => Close(GameSessionEndReason.Destroyed);
        sealed class DialogueChild
        {
            public readonly NpcDialogueHost Host;
            public readonly Guid BindingId;
            public DialogueChild(NpcDialogueHost host) { Host = host; BindingId = host.BindingId; }
        }
        sealed class SequenceChild
        {
            public readonly SequenceHost Host;
            public ISequenceCommands Execution;
            public SequenceChild(SequenceHost host) { Host = host; }
        }
        // Same application control owner, only this session's leases tracked.
        // No ownership of shared controls or foreign simulation/time requests.
        sealed class SessionControls : IControlRequests, IDisposable
        {
            readonly IControlRequests shared;
            readonly List<IControlScope> scopes = new List<IControlScope>();
            bool closed;
            public ControlState Current => shared.Current;
            public SessionControls(IControlRequests shared) { this.shared = shared; }
            public IControlScope OpenScope(string owner)
            {
                if (closed) throw new ObjectDisposedException("Game session controls");
                for (int i = scopes.Count - 1; i >= 0; i--) if (!scopes[i].IsActive) scopes.RemoveAt(i);
                var scope = shared.OpenScope(owner); scopes.Add(scope); return scope;
            }
            public void Dispose()
            {
                if (closed) return; closed = true;
                List<Exception> errors = null;
                for (int i = scopes.Count - 1; i >= 0; i--)
                    try { scopes[i].Dispose(); }
                    catch (Exception exception) { if (errors == null) errors = new List<Exception>(); errors.Add(exception); }
                scopes.Clear();
                if (errors != null) throw new AggregateException("Session control cleanup failed.", errors);
            }
        }
    }
}
