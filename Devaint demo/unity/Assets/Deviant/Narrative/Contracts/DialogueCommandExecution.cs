using System;
using Deviant.Definitions;

namespace Deviant.Narrative.Contracts
{
    public enum DialogueCommandStatus { Succeeded, Rejected, Cancelled, Failed }
    public sealed class DialogueCommandResult
    {
        public DialogueCommandStatus Status { get; }
        public string Reason { get; }
        public DialogueCommandResult(DialogueCommandStatus status, string reason = null)
        {
            if (!Enum.IsDefined(typeof(DialogueCommandStatus), status) || status != DialogueCommandStatus.Succeeded && string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A defined command status and non-success reason are required.");
            Status = status; Reason = reason;
        }
    }
    public sealed class DialogueCommandContext
    {
        public Guid SessionId { get; }
        public Guid WorldSessionId { get; }
        public string PlacementId { get; }
        public DialogueDefinition Definition { get; }
        public DialogueCommandContext(Guid sessionId, Guid worldSessionId, string placementId, DialogueDefinition definition)
        {
            if (sessionId == Guid.Empty || worldSessionId == Guid.Empty || string.IsNullOrWhiteSpace(placementId) || definition == null)
                throw new ArgumentException("A live dialogue/chapter identity, placement and starting definition are required.");
            SessionId = sessionId; WorldSessionId = worldSessionId; PlacementId = placementId; Definition = definition;
        }
    }
    public sealed class DialogueCommandRequest
    {
        public DialogueToken Token { get; }
        public string ChoiceId { get; }
        public string CommandId { get; }
        // Session + line identifies the one choice commit at this acyclic node.
        // Revision authenticates the input; a refresh does not create a new effect.
        public DialogueCommandRequest(DialogueToken token, string choiceId, string commandId)
        {
            if (token.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(token.LineId) || token.Revision < 0 || string.IsNullOrWhiteSpace(choiceId) || string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("A displayed token, choice and registered command are required.");
            Token = token; ChoiceId = choiceId; CommandId = commandId;
        }
    }
    public sealed class DialogueCommandReceipt
    {
        public DialogueCommandRequest Request { get; }
        public DialogueCommandResult Result { get; }
        public DialogueCommandReceipt(DialogueCommandRequest request, DialogueCommandResult result)
        { Request = request ?? throw new ArgumentNullException(nameof(request)); Result = result ?? throw new ArgumentNullException(nameof(result)); }
    }
    public interface IDialogueCommandBindings
    {
        // Validation must not execute commands or acquire product state.
        bool Validate(DialogueDefinition definition, out string error);
        IDialogueCommandScope OpenScope(DialogueCommandContext context);
    }
    public interface IDialogueCommandScope : IDisposable
    {
        bool IsActive { get; }
        // Synchronous. Rejected guarantees no change and may be retried.
        // Terminal results are idempotent per session/line; Dispose clears only
        // execution records and never rolls back an already committed command.
        DialogueCommandResult Execute(DialogueCommandRequest request);
    }
}
