using System;
using Deviant.Control.Contracts;
using Deviant.Narrative.Contracts;
using Deviant.UI.Contracts;

namespace Deviant.UI.Presentation
{
    public sealed class DialoguePresentationPolicy
    {
        public InputChannels BlockedInput { get; }
        public bool PauseSimulation { get; }
        public DialoguePresentationPolicy(InputChannels blockedInput, bool pauseSimulation)
        { BlockedInput = blockedInput; PauseSimulation = pauseSimulation; }
    }
    public sealed class DialoguePresenter : IDisposable
    {
        readonly IDialogueCommands dialogue;
        readonly IDialogueView view;
        readonly IDialogueText text;
        readonly IControlRequests controls;
        readonly Guid session;
        IControlScope scope;
        bool disposed;
        public Guid BindingId { get; } = Guid.NewGuid();
        public bool IsActive => !disposed;
        public string LastError { get; private set; }
        public string CleanupError { get; private set; }
        public DialoguePresenter(IDialogueCommands dialogue, IDialogueView view, IDialogueText text, IControlRequests controls, DialoguePresentationPolicy policy)
        {
            this.dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue)); this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.text = text ?? throw new ArgumentNullException(nameof(text)); this.controls = controls ?? throw new ArgumentNullException(nameof(controls));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var snapshot = dialogue.Read(); session = snapshot.Token.SessionId;
            if (snapshot.Status != DialogueStatus.Active || !view.TryBind(BindingId, out var error)) throw new InvalidOperationException("Dialogue view/session cannot bind.");
            try
            {
                scope = controls.OpenScope("dialogue " + session);
                if (policy.BlockedInput != InputChannels.None) scope.BlockInput(policy.BlockedInput);
                if (policy.PauseSimulation) scope.PauseSimulation();
                view.Input += OnInput; Refresh();
            }
            catch { Release(); throw; }
        }
        public void Refresh()
        {
            if (disposed) return;
            try
            {
                dialogue.Poll(); var snapshot = dialogue.Read();
                if (snapshot.Token.SessionId != session || snapshot.Status != DialogueStatus.Active) { Release(); return; }
                if (!text.TryRead(snapshot.SpeakerKey, out var speaker) || !text.TryRead(snapshot.TextKey, out var body)) throw new InvalidOperationException("Dialogue text binding is missing.");
                var choices = new DialogueChoiceDisplay[snapshot.Choices.Count];
                for (int i = 0; i < choices.Length; i++)
                {
                    var choice = snapshot.Choices[i];
                    if (!text.TryRead(choice.TextKey, out var label)) throw new InvalidOperationException("Dialogue choice text binding is missing.");
                    choices[i] = new DialogueChoiceDisplay(choice.Id, label, choice.IsAvailable);
                }
                if (!view.TryApply(new DialogueDisplay(BindingId, snapshot.Token, speaker, body, snapshot.AllowPlayerCancel, choices))) throw new InvalidOperationException("Dialogue view binding is stale or its layout cannot display the choices.");
            }
            catch (Exception exception) { LastError = exception.Message; dialogue.End(session, DialogueEndReason.PresentationFailed, out _); Release(); }
        }
        void OnInput(DialogueIntent intent)
        {
            if (disposed || intent.BindingId != BindingId || intent.Token.SessionId != session) return;
            if (intent.Kind == DialogueIntentKind.ViewClosed)
            { dialogue.End(session, DialogueEndReason.ViewClosed, out _); Release(); return; }
            if (controls.Current.Blocks(InputChannels.Dialogue)) { LastError = "Dialogue input is restricted by an active owner."; return; }
            string error;
            if (intent.Kind == DialogueIntentKind.Advance) dialogue.TryAdvance(intent.Token, out error);
            else if (intent.Kind == DialogueIntentKind.SelectChoice) dialogue.TrySelectChoice(intent.Token, intent.ChoiceId, out error);
            else if (intent.Kind == DialogueIntentKind.Cancel) dialogue.TryCancel(intent.Token, out error);
            else return;
            LastError = error;
            if (dialogue.Read().Status != DialogueStatus.Active) Release();
            // The next refresh publishes the next token. Repeated clicks on this
            // displayed line retain their old revision and cannot skip a line.
        }
        public void Dispose()
        {
            if (disposed) return;
            if (dialogue.Read().Token.SessionId == session && dialogue.Read().Status == DialogueStatus.Active) dialogue.End(session, DialogueEndReason.OwnerDisposed, out _);
            Release();
        }
        void Release()
        {
            if (disposed) return; disposed = true;
            try
            {
                var snapshot = dialogue.Read();
                if (snapshot.Token.SessionId == session && snapshot.CleanupError != null) RecordCleanup(new InvalidOperationException(snapshot.CleanupError));
            }
            catch (Exception exception) { RecordCleanup(exception); }
            try { view.Input -= OnInput; } catch (Exception exception) { RecordCleanup(exception); }
            try { scope?.Dispose(); } catch (Exception exception) { RecordCleanup(exception); }
            try { view.Unbind(BindingId); } catch (Exception exception) { RecordCleanup(exception); }
        }
        void RecordCleanup(Exception exception)
        {
            LastError = exception.Message;
            CleanupError = CleanupError == null ? exception.Message : CleanupError + "; " + exception.Message;
        }
    }
}
