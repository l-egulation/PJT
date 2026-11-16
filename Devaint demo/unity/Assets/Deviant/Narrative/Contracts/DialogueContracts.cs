using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;

namespace Deviant.Narrative.Contracts
{
    public enum DialogueStatus { Active, Completed, Cancelled, Failed }
    public enum DialogueEndReason { None, Completed, PlayerCancelled, TargetLost, EligibilityLost, ViewClosed, HostDisabled, SceneChanged, RunEnded, OwnerDisposed, ContextFailed, PresentationFailed, CommandFailed, CommandCancelled }
    public readonly struct DialogueToken
    {
        public Guid SessionId { get; }
        public string LineId { get; }
        public long Revision { get; }
        public DialogueToken(Guid sessionId, string lineId, long revision) { SessionId = sessionId; LineId = lineId; Revision = revision; }
    }
    public sealed class DialogueChoiceSnapshot
    {
        public string Id { get; }
        public string TextKey { get; }
        public bool IsAvailable { get; }
        public DialogueChoiceSnapshot(string id, string textKey, bool isAvailable)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(textKey)) throw new ArgumentException("Choice identity and text key are required.");
            Id = id; TextKey = textKey; IsAvailable = isAvailable;
        }
    }
    public sealed class DialogueSnapshot
    {
        public DialogueToken Token { get; }
        public Guid WorldSessionId { get; }
        public string PlacementId { get; }
        public string DefinitionVersion { get; }
        public string SpeakerKey { get; }
        public string TextKey { get; }
        public DialogueStatus Status { get; }
        public DialogueEndReason EndReason { get; }
        public string Error { get; }
        public string CleanupError { get; }
        public DialogueCommandReceipt LastCommand { get; }
        public bool AllowPlayerCancel { get; }
        public IReadOnlyList<DialogueChoiceSnapshot> Choices { get; }
        public DialogueSnapshot(DialogueToken token, Guid worldSession, string placementId, string version, string speakerKey, string textKey,
            DialogueStatus status, DialogueEndReason reason, string error, bool allowPlayerCancel, DialogueChoiceSnapshot[] choices = null,
            DialogueCommandReceipt lastCommand = null, string cleanupError = null)
        {
            var copy = choices == null ? Array.Empty<DialogueChoiceSnapshot>() : (DialogueChoiceSnapshot[])choices.Clone();
            if (copy.Any(c => c == null) || copy.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Choice snapshots require unique non-null entries.", nameof(choices));
            Token = token; WorldSessionId = worldSession; PlacementId = placementId; DefinitionVersion = version; SpeakerKey = speakerKey; TextKey = textKey;
            Status = status; EndReason = reason; Error = error; AllowPlayerCancel = allowPlayerCancel; Choices = Array.AsReadOnly(copy);
            LastCommand = lastCommand; CleanupError = cleanupError;
        }
    }
    public enum DialogueTargetState { Available, Missing, Ineligible }
    public interface IDialogueContext
    {
        DialogueTargetState CheckTarget();
        bool Evaluate(string conditionId);
    }
    public interface IDialogueCommands
    {
        DialogueSnapshot Read();
        bool TryAdvance(DialogueToken expected, out string error);
        bool TrySelectChoice(DialogueToken expected, string choiceId, out string error);
        bool TryCancel(DialogueToken expected, out string error);
        bool End(Guid expectedSession, DialogueEndReason reason, out string error);
        void Poll();
    }
    public sealed class NpcTarget
    {
        public Guid WorldSessionId { get; }
        public string PlacementId { get; }
        public NpcDefinition Definition { get; }
        public float Distance { get; }
        public bool InCombat { get; }
        public NpcTarget(Guid worldSessionId, string placementId, NpcDefinition definition, float distance, bool inCombat)
        {
            if (worldSessionId == Guid.Empty || string.IsNullOrWhiteSpace(placementId) || definition == null || float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0)
                throw new ArgumentException("A live target identity, definition and finite nonnegative measured distance are required.");
            WorldSessionId = worldSessionId; PlacementId = placementId; Definition = definition; Distance = distance; InCombat = inCombat;
        }
    }
    public sealed class InteractionPolicy
    {
        public float MaxDistance { get; }
        public bool AllowInCombat { get; }
        public bool RequireEligibilityWhileTalking { get; }
        public bool AllowPlayerCancel { get; }
        public InteractionPolicy(float maxDistance, bool allowInCombat, bool requireEligibilityWhileTalking, bool allowPlayerCancel)
        {
            if (float.IsNaN(maxDistance) || float.IsInfinity(maxDistance) || maxDistance < 0) throw new ArgumentOutOfRangeException(nameof(maxDistance));
            MaxDistance = maxDistance; AllowInCombat = allowInCombat; RequireEligibilityWhileTalking = requireEligibilityWhileTalking; AllowPlayerCancel = allowPlayerCancel;
        }
    }
    public interface IInteractionTargets
    {
        IReadOnlyList<NpcTarget> ReadTargets();
        bool TryRead(Guid worldSession, string placementId, out NpcTarget target);
    }
    public interface INarrativeConditions
    {
        bool Evaluate(string conditionId, Guid worldSession, string placementId);
    }
}
