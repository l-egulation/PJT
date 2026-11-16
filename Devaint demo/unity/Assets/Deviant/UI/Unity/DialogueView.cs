using System;
using System.Linq;
using Deviant.UI.Contracts;
using UnityEngine;
using UnityEngine.Events;

namespace Deviant.UI.Unity
{
    // Layout is supplied by a prefab/composition. This component renders only;
    // it never advances dialogue, changes time or releases a global input flag.
    public sealed class DialogueView : MonoBehaviour, IDialogueView
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMPro.TMP_Text speaker, body;
        [SerializeField] UnityEngine.UI.Button advance, cancel;
        [SerializeField] UnityEngine.UI.Button[] choices = Array.Empty<UnityEngine.UI.Button>();
        [SerializeField] TMPro.TMP_Text[] choiceLabels = Array.Empty<TMPro.TMP_Text>();
        UnityAction[] choose;
        UnityAction advanceInput, cancelInput;
        UnityEngine.UI.Button wiredAdvance, wiredCancel;
        UnityEngine.UI.Button[] wiredChoices;
        Guid binding;
        DialogueDisplay displayed;
        bool wired;
        public event Action<DialogueIntent> Input;
        public Guid BindingId => binding;
        public DialogueDisplay Displayed => displayed;
        public void Configure(GameObject panel, TMPro.TMP_Text speaker, TMPro.TMP_Text body, UnityEngine.UI.Button advance, UnityEngine.UI.Button cancel,
            UnityEngine.UI.Button[] choices = null, TMPro.TMP_Text[] choiceLabels = null)
        {
            if (binding != Guid.Empty) throw new InvalidOperationException("Unbind the dialogue view before replacing its layout.");
            var buttons = choices == null ? Array.Empty<UnityEngine.UI.Button>() : (UnityEngine.UI.Button[])choices.Clone();
            var labels = choiceLabels == null ? Array.Empty<TMPro.TMP_Text>() : (TMPro.TMP_Text[])choiceLabels.Clone();
            if (!ValidChoices(panel, advance, cancel, buttons, labels)) throw new ArgumentException("Choice slots require distinct panel buttons and labels inside those buttons.");
            Unwire(); this.panel = panel; this.speaker = speaker; this.body = body; this.advance = advance; this.cancel = cancel;
            this.choices = buttons; this.choiceLabels = labels;
        }
        static bool ValidChoices(GameObject panel, UnityEngine.UI.Button advance, UnityEngine.UI.Button cancel, UnityEngine.UI.Button[] buttons, TMPro.TMP_Text[] labels)
        {
            if (buttons == null || labels == null || buttons.Length != labels.Length || buttons.Distinct().Count() != buttons.Length || labels.Distinct().Count() != labels.Length) return false;
            for (int i = 0; i < buttons.Length; i++)
                if (!panel || !buttons[i] || !labels[i] || buttons[i] == advance || buttons[i] == cancel || !buttons[i].transform.IsChildOf(panel.transform) || !labels[i].transform.IsChildOf(buttons[i].transform)) return false;
            return true;
        }
        bool HasLayout() => panel && speaker && body && advance && cancel && ValidChoices(panel, advance, cancel, choices, choiceLabels);
        void Wire(DialogueDisplay expected)
        {
            // UnityEvent may already be invoking an old listener list. Each
            // callback retains the displayed generation/token instead of reading
            // a replacement session or choice line from mutable view fields.
            wiredAdvance = advance; wiredCancel = cancel; wiredChoices = (UnityEngine.UI.Button[])choices.Clone();
            advanceInput = () => { if (expected.CanAdvance) Emit(expected, DialogueIntentKind.Advance); };
            cancelInput = () => { if (expected.AllowCancel) Emit(expected, DialogueIntentKind.Cancel); };
            choose = new UnityAction[wiredChoices.Length]; wired = true;
            wiredAdvance.onClick.AddListener(advanceInput); wiredCancel.onClick.AddListener(cancelInput);
            for (int i = 0; i < wiredChoices.Length; i++) { int slot = i; choose[i] = () => SelectChoice(expected, slot); wiredChoices[i].onClick.AddListener(choose[i]); }
        }
        public bool TryBind(Guid bindingId, out string error)
        {
            error = null;
            if (!enabled || bindingId == Guid.Empty || binding != Guid.Empty || !HasLayout()) { error = "Dialogue view is disabled, already bound or has missing layout references."; return false; }
            binding = bindingId; return true;
        }
        public bool TryApply(DialogueDisplay display)
        {
            if (!enabled || display == null || binding == Guid.Empty || binding != display.BindingId || !HasLayout() || display.Choices.Count > choices.Length) return false;
            Unwire(); displayed = display; speaker.text = display.Speaker; body.text = display.Body;
            advance.interactable = display.CanAdvance; cancel.interactable = display.AllowCancel;
            for (int i = 0; i < choices.Length; i++)
            {
                bool visible = i < display.Choices.Count;
                if (visible) { choiceLabels[i].text = display.Choices[i].Text; choices[i].interactable = display.Choices[i].IsAvailable; }
                choices[i].gameObject.SetActive(visible);
            }
            panel.SetActive(true);
            if (!isActiveAndEnabled || binding != display.BindingId) return false;
            Wire(display); return true;
        }
        public void Unbind(Guid bindingId)
        {
            if (binding == Guid.Empty || binding != bindingId) return;
            binding = Guid.Empty; displayed = null; Unwire();
            if (panel) panel.SetActive(false);
        }
        bool IsCurrent(DialogueDisplay expected) => isActiveAndEnabled && binding != Guid.Empty && binding == expected.BindingId && displayed != null &&
            displayed.Token.SessionId == expected.Token.SessionId && displayed.Token.LineId == expected.Token.LineId && displayed.Token.Revision == expected.Token.Revision;
        void SelectChoice(DialogueDisplay expected, int slot)
        {
            if (!IsCurrent(expected) || slot >= expected.Choices.Count || !choices[slot] || !choices[slot].isActiveAndEnabled || !expected.Choices[slot].IsAvailable) return;
            Input?.Invoke(new DialogueIntent(expected.BindingId, expected.Token, DialogueIntentKind.SelectChoice, expected.Choices[slot].Id));
        }
        void Emit(DialogueDisplay expected, DialogueIntentKind kind)
        {
            if (IsCurrent(expected)) Input?.Invoke(new DialogueIntent(expected.BindingId, expected.Token, kind));
        }
        void OnDisable()
        {
            Unwire(); var previous = binding;
            try { if (binding != Guid.Empty && displayed != null) Input?.Invoke(new DialogueIntent(binding, displayed.Token, DialogueIntentKind.ViewClosed)); }
            finally { if (binding == previous) { binding = Guid.Empty; displayed = null; } }
        }
        void OnDestroy() { Unwire(); }
        void Unwire()
        {
            if (!wired) return;
            if (wiredAdvance) wiredAdvance.onClick.RemoveListener(advanceInput); if (wiredCancel) wiredCancel.onClick.RemoveListener(cancelInput);
            for (int i = 0; i < wiredChoices.Length; i++) if (wiredChoices[i]) wiredChoices[i].onClick.RemoveListener(choose[i]);
            wiredAdvance = wiredCancel = null; wiredChoices = null; choose = null; advanceInput = cancelInput = null; wired = false;
        }
    }
}
