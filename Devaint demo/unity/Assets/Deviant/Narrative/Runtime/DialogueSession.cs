using System;
using System.Linq;
using Deviant.Definitions;
using Deviant.Narrative.Contracts;

namespace Deviant.Narrative
{
    public sealed class DialogueSession : IDialogueCommands, IDisposable
    {
        readonly NpcTarget target;
        readonly IDialogueContext context;
        readonly bool allowCancel;
        readonly Guid id = Guid.NewGuid();
        DialogueLine line;
        DialogueChoiceSnapshot[] choices;
        long revision;
        bool busy;
        DialogueSnapshot snapshot;
        IDialogueCommandScope commands;
        DialogueCommandReceipt lastCommand;
        string cleanupError;
        DialogueSession(NpcTarget target, IDialogueContext context, bool allowCancel)
        {
            this.target = target; this.context = context; this.allowCancel = allowCancel;
            line = target.Definition.Dialogue.Find(target.Definition.Dialogue.EntryId); choices = ReadChoices(line);
            Publish(DialogueStatus.Active, DialogueEndReason.None, null);
        }
        public static bool TryStart(NpcTarget target, IDialogueContext context, bool allowPlayerCancel, out DialogueSession session, out string error,
            IDialogueCommandBindings commandBindings = null)
        {
            session = null; error = null; DialogueSession candidate = null;
            if (target == null || context == null) { error = "Target and dialogue context are required."; return false; }
            try
            {
                var entry = target.Definition.Dialogue.Find(target.Definition.Dialogue.EntryId);
                if (context.CheckTarget() != DialogueTargetState.Available || entry.ConditionId != null && !context.Evaluate(entry.ConditionId))
                { error = "Dialogue entry is unavailable."; return false; }
                if (!ValidateCommands(target.Definition.Dialogue, commandBindings, out error)) return false;
                candidate = new DialogueSession(target, context, allowPlayerCancel);
                if (target.Definition.Dialogue.CommandIds.Count > 0)
                {
                    candidate.commands = commandBindings.OpenScope(new DialogueCommandContext(candidate.id, target.WorldSessionId, target.PlacementId, target.Definition.Dialogue));
                    if (candidate.commands == null || !candidate.commands.IsActive) throw new InvalidOperationException("Dialogue command scope is unavailable.");
                }
                session = candidate; return true;
            }
            catch (Exception exception)
            {
                error = "Dialogue start failed: " + exception.Message;
                if (candidate != null)
                {
                    candidate.End(candidate.id, DialogueEndReason.CommandFailed, out _);
                    if (candidate.cleanupError != null) error += "; " + candidate.cleanupError;
                }
                return false;
            }
        }
        public static bool ValidateCommands(DialogueDefinition definition, IDialogueCommandBindings bindings, out string error)
        {
            error = null;
            if (definition == null) { error = "A dialogue definition is required."; return false; }
            if (definition.CommandIds.Count == 0) return true;
            if (bindings == null) { error = "Dialogue requires explicit command bindings."; return false; }
            return bindings.Validate(definition, out error);
        }
        public DialogueSnapshot Read() => snapshot;
        public bool TryAdvance(DialogueToken expected, out string error) => TryProgress(expected, null, false, out error);
        public bool TrySelectChoice(DialogueToken expected, string choiceId, out string error) => TryProgress(expected, choiceId, true, out error);
        bool TryProgress(DialogueToken expected, string choiceId, bool selecting, out string error)
        {
            if (!Accept(expected, out error)) return false;
            var choice = selecting ? line.Choices.FirstOrDefault(c => c.Id == choiceId) : null;
            if (selecting && choice == null) { error = "The displayed line does not contain this choice."; return false; }
            if (!selecting && line.Choices.Count > 0) { error = "This line requires an explicit choice."; return false; }
            busy = true;
            try
            {
                if (!Available()) { error = snapshot.EndReason.ToString(); return false; }
                if (line.ConditionId != null && !context.Evaluate(line.ConditionId)) { error = "Current line condition no longer holds."; return false; }
                if (choice != null && choice.ConditionId != null && !context.Evaluate(choice.ConditionId)) { error = "Choice condition no longer holds."; return false; }
                var nextId = selecting ? choice.NextId : line.NextId;
                var next = nextId == null ? null : target.Definition.Dialogue.Find(nextId);
                if (next != null && next.ConditionId != null && !context.Evaluate(next.ConditionId)) { error = "Next line condition does not hold."; return false; }
                // Check revision capacity before any external commit. Conditions
                // authorize the transition before the command; destination options
                // are sampled after success so they can observe its result.
                if (revision == long.MaxValue) throw new InvalidOperationException("Dialogue revision exhausted.");
                if (choice != null && !ExecuteCommand(expected, choice, out error)) return false;
                var nextChoices = next == null ? Array.Empty<DialogueChoiceSnapshot>() : ReadChoices(next);
                NextRevision(); choices = nextChoices;
                if (next == null) Publish(DialogueStatus.Completed, DialogueEndReason.Completed, null);
                else { line = next; Publish(DialogueStatus.Active, DialogueEndReason.None, null); }
                return true;
            }
            catch (Exception exception) { Finish(DialogueEndReason.ContextFailed, exception.Message); error = snapshot.Error; return false; }
            finally { busy = false; }
        }
        bool ExecuteCommand(DialogueToken token, DialogueChoice choice, out string error)
        {
            error = null;
            if (choice.CommandId == null) return true;
            var request = new DialogueCommandRequest(token, choice.Id, choice.CommandId);
            try
            {
                var result = commands?.Execute(request) ?? throw new InvalidOperationException("Dialogue command returned no result.");
                lastCommand = new DialogueCommandReceipt(request, result);
                if (result.Status == DialogueCommandStatus.Succeeded) return true;
                error = result.Reason;
                if (result.Status == DialogueCommandStatus.Rejected) Publish(DialogueStatus.Active, DialogueEndReason.None, null);
                else Finish(result.Status == DialogueCommandStatus.Cancelled ? DialogueEndReason.CommandCancelled : DialogueEndReason.CommandFailed, error);
                return false;
            }
            catch (Exception exception)
            {
                error = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
                lastCommand = new DialogueCommandReceipt(request, new DialogueCommandResult(DialogueCommandStatus.Failed, error));
                Finish(DialogueEndReason.CommandFailed, error); return false;
            }
        }
        public bool TryCancel(DialogueToken expected, out string error)
        {
            if (!Accept(expected, out error)) return false;
            if (!allowCancel) { error = "Player cancellation is disabled by this session policy."; return false; }
            busy = true;
            try { Finish(DialogueEndReason.PlayerCancelled, null); return true; }
            finally { busy = false; }
        }
        public bool End(Guid expectedSession, DialogueEndReason reason, out string error)
        {
            error = null;
            if (busy || expectedSession != id || snapshot.Status != DialogueStatus.Active) { error = "Stale, ended or reentrant dialogue request."; return false; }
            if (!Enum.IsDefined(typeof(DialogueEndReason), reason) || reason == DialogueEndReason.None || reason == DialogueEndReason.Completed)
            { error = "A lifecycle cancellation/failure reason is required; only advance completes dialogue."; return false; }
            if (reason == DialogueEndReason.PlayerCancelled && !allowCancel) { error = "Player cancellation is disabled."; return false; }
            busy = true;
            try { Finish(reason, null); return true; }
            finally { busy = false; }
        }
        public void Poll()
        {
            if (busy || snapshot.Status != DialogueStatus.Active) return;
            busy = true;
            try
            {
                if (!Available() || line.Choices.Count == 0) return;
                var refreshed = ReadChoices(line);
                if (choices.Select(c => c.IsAvailable).SequenceEqual(refreshed.Select(c => c.IsAvailable))) return;
                NextRevision(); choices = refreshed; Publish(DialogueStatus.Active, DialogueEndReason.None, null);
            }
            catch (Exception exception) { Finish(DialogueEndReason.ContextFailed, exception.Message); }
            finally { busy = false; }
        }
        DialogueChoiceSnapshot[] ReadChoices(DialogueLine current) => current.Choices.Select(c =>
            new DialogueChoiceSnapshot(c.Id, c.TextKey, c.ConditionId == null || context.Evaluate(c.ConditionId))).ToArray();
        void NextRevision()
        {
            if (revision == long.MaxValue) throw new InvalidOperationException("Dialogue revision exhausted.");
            revision++;
        }
        bool Available()
        {
            try
            {
                if (commands != null && !commands.IsActive) { Finish(DialogueEndReason.CommandFailed, "Dialogue command scope ended."); return false; }
            }
            catch (Exception exception) { Finish(DialogueEndReason.CommandFailed, exception.Message); return false; }
            var state = context.CheckTarget();
            if (state == DialogueTargetState.Available) return true;
            Finish(state == DialogueTargetState.Missing ? DialogueEndReason.TargetLost : DialogueEndReason.EligibilityLost, null); return false;
        }
        bool Accept(DialogueToken expected, out string error)
        {
            error = null;
            if (busy || snapshot.Status != DialogueStatus.Active || expected.SessionId != id || expected.LineId != line.Id || expected.Revision != revision)
            { error = "Stale, ended or reentrant dialogue input."; return false; }
            return true;
        }
        void Finish(DialogueEndReason reason, string error)
        {
            if (snapshot.Status != DialogueStatus.Active) return;
            if (revision < long.MaxValue) revision++;
            bool failed = reason == DialogueEndReason.ContextFailed || reason == DialogueEndReason.PresentationFailed || reason == DialogueEndReason.CommandFailed;
            Publish(failed ? DialogueStatus.Failed : DialogueStatus.Cancelled, reason, error);
        }
        void Publish(DialogueStatus status, DialogueEndReason reason, string error)
        {
            if (status != DialogueStatus.Active && commands != null)
            {
                var owned = commands; commands = null;
                try { owned.Dispose(); }
                catch (Exception exception) { cleanupError = "Dialogue command cleanup failed: " + exception.Message; }
            }
            snapshot = new DialogueSnapshot(new DialogueToken(id, line.Id, revision), target.WorldSessionId, target.PlacementId, target.Definition.Dialogue.ContentHash,
                line.SpeakerKey, line.TextKey, status, reason, error, allowCancel, status == DialogueStatus.Active ? choices : null, lastCommand, cleanupError);
        }
        public void Dispose() { if (busy) throw new InvalidOperationException("Dialogue mutations cannot reenter context callbacks."); End(id, DialogueEndReason.OwnerDisposed, out _); }
    }
}
