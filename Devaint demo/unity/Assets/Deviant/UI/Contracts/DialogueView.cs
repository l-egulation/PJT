using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Narrative.Contracts;

namespace Deviant.UI.Contracts
{
    public enum DialogueIntentKind { Advance, Cancel, ViewClosed, SelectChoice }
    public readonly struct DialogueIntent
    {
        public Guid BindingId { get; }
        public DialogueToken Token { get; }
        public DialogueIntentKind Kind { get; }
        public string ChoiceId { get; }
        public DialogueIntent(Guid binding, DialogueToken token, DialogueIntentKind kind, string choiceId = null)
        { BindingId = binding; Token = token; Kind = kind; ChoiceId = choiceId; }
    }
    public sealed class DialogueChoiceDisplay
    {
        public string Id { get; }
        public string Text { get; }
        public bool IsAvailable { get; }
        public DialogueChoiceDisplay(string id, string text, bool isAvailable)
        {
            if (string.IsNullOrWhiteSpace(id) || text == null) throw new ArgumentException("Choice identity and resolved text are required.");
            Id = id; Text = text; IsAvailable = isAvailable;
        }
    }
    public sealed class DialogueDisplay
    {
        public Guid BindingId { get; }
        public DialogueToken Token { get; }
        public string Speaker { get; }
        public string Body { get; }
        public bool AllowCancel { get; }
        public IReadOnlyList<DialogueChoiceDisplay> Choices { get; }
        public bool CanAdvance => Choices.Count == 0;
        public DialogueDisplay(Guid binding, DialogueToken token, string speaker, string body, bool allowCancel, DialogueChoiceDisplay[] choices = null)
        {
            var copy = choices == null ? Array.Empty<DialogueChoiceDisplay>() : (DialogueChoiceDisplay[])choices.Clone();
            if (copy.Any(c => c == null) || copy.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Choice displays require unique non-null entries.", nameof(choices));
            BindingId = binding; Token = token; Speaker = speaker; Body = body; AllowCancel = allowCancel; Choices = Array.AsReadOnly(copy);
        }
    }
    public interface IDialogueText
    {
        bool TryRead(string key, out string text);
    }
    public interface IDialogueView
    {
        event Action<DialogueIntent> Input;
        bool TryBind(Guid bindingId, out string error);
        bool TryApply(DialogueDisplay display);
        void Unbind(Guid bindingId);
    }
}
