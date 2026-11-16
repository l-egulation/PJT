using System;
using System.Collections.Generic;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Narrative;
using Deviant.Narrative.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using UnityEngine;

namespace Deviant.Integration
{
    // An explicit game/lab composition endpoint. No NPC content, key mapping,
    // distance, combat allowance or pause policy is selected by this component.
    public sealed class NpcDialogueHost : MonoBehaviour
    {
        InteractionService interactions;
        IControlRequests controls;
        IDialogueView view;
        IDialogueText text;
        DialoguePresentationPolicy presentationPolicy;
        DialoguePresenter presenter;
        DialogueSourceBinding sourceBinding;
        IDialogueCommandBindings commandBindings;
        public DialogueSourceBinding SourceBinding => sourceBinding;
        public Guid BindingId { get; private set; }
        public IDialogueCommands Current => interactions?.Current;
        public string LastError { get; private set; }
        public void Configure(IInteractionTargets targets, INarrativeConditions conditions, InteractionPolicy interactionPolicy,
            IDialogueView view, IDialogueText text, IControlRequests controls, DialoguePresentationPolicy presentationPolicy,
            IDialogueCommandBindings commandBindings = null, DialogueSourceBinding sourceBinding = null)
        {
            Close(DialogueEndReason.OwnerDisposed);
            this.view = view ?? throw new ArgumentNullException(nameof(view)); this.text = text ?? throw new ArgumentNullException(nameof(text));
            this.controls = controls ?? throw new ArgumentNullException(nameof(controls)); this.presentationPolicy = presentationPolicy ?? throw new ArgumentNullException(nameof(presentationPolicy));
            if (sourceBinding != null && !DialogueSession.ValidateCommands(sourceBinding.Source.Definition, commandBindings, out var error))
                throw new ArgumentException(error, nameof(sourceBinding));
            this.sourceBinding = sourceBinding; this.commandBindings = commandBindings;
            interactions = new InteractionService(sourceBinding == null ? targets : new DialogueSourceTargets(targets, () => this.sourceBinding), conditions, interactionPolicy, commandBindings);
            BindingId = Guid.NewGuid();
        }
        public bool TryApplySource(CompiledDialogueSource candidate, out string error)
        {
            error = null;
            if (interactions == null || sourceBinding == null || candidate == null) { error = "An explicitly configured source binding and complete candidate are required."; return false; }
            if (candidate.Definition.Id != sourceBinding.Source.Definition.Id) { error = "A candidate must retain the configured stable dialogue identity."; return false; }
            try
            {
                if (!DialogueSession.ValidateCommands(candidate.Definition, commandBindings, out error)) return false;
                sourceBinding = new DialogueSourceBinding(sourceBinding.NpcId, candidate);
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        public bool TryApplySource(TextAsset source, TextAsset references, out string error)
        {
            try { return TryApplySource(DialogueSourceJson.Compile(source, references), out error); }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        public IReadOnlyList<NpcTarget> Query(out string error)
        {
            error = null;
            if (interactions == null || controls.Current.Blocks(InputChannels.Interaction)) { error = "Interaction input is unavailable."; return Array.Empty<NpcTarget>(); }
            return interactions.Query(out error);
        }
        public bool TryInteract(NpcTarget selected, out string error)
        {
            error = null;
            if (!isActiveAndEnabled || interactions == null || controls.Current.Blocks(InputChannels.Interaction)) { error = "Interaction input is unavailable."; return false; }
            var startingSource = sourceBinding;
            if (!interactions.TryStart(selected, out var session, out error)) return false;
            presenter?.Dispose(); presenter = null;
            try
            {
                var sessionText = startingSource != null && selected.Definition.Id == startingSource.NpcId
                    ? new DialogueSourceText(startingSource.Source.References) : text;
                presenter = new DialoguePresenter(session, view, sessionText, controls, presentationPolicy);
                if (!presenter.IsActive) { error = presenter.LastError ?? "Dialogue presentation ended during binding."; return false; }
                return true;
            }
            catch (Exception exception)
            {
                session.End(session.Read().Token.SessionId, DialogueEndReason.PresentationFailed, out _);
                LastError = error = exception.Message; return false;
            }
        }
        void Update() { Refresh(); }
        public void Refresh()
        {
            interactions?.Poll(); presenter?.Refresh();
            if (presenter != null && !presenter.IsActive) { LastError = presenter.LastError; presenter.Dispose(); presenter = null; }
        }
        public bool TryClose(Guid expectedBinding, DialogueEndReason reason, out string error)
        {
            error = null;
            if (expectedBinding == Guid.Empty || expectedBinding != BindingId) { error = "Dialogue binding is missing or stale."; return false; }
            Close(reason); return true;
        }
        public void Close(DialogueEndReason reason)
        {
            BindingId = Guid.Empty;
            var session = Current;
            if (session != null && session.Read().Status == DialogueStatus.Active) session.End(session.Read().Token.SessionId, reason, out _);
            presenter?.Dispose(); presenter = null; interactions?.Dispose(); interactions = null; sourceBinding = null; commandBindings = null;
        }
        void OnDisable() { Close(DialogueEndReason.HostDisabled); }
        void OnDestroy() { Close(DialogueEndReason.SceneChanged); }
    }
}
