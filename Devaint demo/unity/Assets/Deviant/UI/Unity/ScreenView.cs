using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.UI.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deviant.UI.Unity
{
    // Layout/device mapping is injected. One router should coordinate the views
    // of one EventSystem. This component never edits game state or engine time.
    public sealed class ScreenView : MonoBehaviour, IScreenView
    {
        GameObject panel, rememberedSelection;
        CanvasGroup group;
        EventSystem events;
        Selectable firstFocus;
        Button back;
        Guid binding;
        bool focused, disposed, wired, focusedInteractable, focusedRaycasts, priorInteractable, priorRaycasts;
        public event Action<ScreenIntent> Input;
        public Guid BindingId => binding;
        public bool IsFocused => focused && binding != Guid.Empty;
        public bool IsAvailable => !disposed && this && enabled && panel && group && events && events.isActiveAndEnabled && firstFocus &&
            (binding == Guid.Empty ? gameObject.activeInHierarchy || transform.IsChildOf(panel.transform) : isActiveAndEnabled && panel.activeInHierarchy);
        public bool IsFocusReady => IsAvailable && !events.alreadySelecting;
        public void Configure(GameObject panel, CanvasGroup group, EventSystem events, Selectable firstFocus, Button back,
            bool focusedInteractable, bool focusedRaycasts)
        {
            if (binding != Guid.Empty) throw new InvalidOperationException("Unbind the screen before replacing its layout.");
            if (!panel || !group || group.gameObject != panel || !events || !firstFocus || !firstFocus.transform.IsChildOf(panel.transform))
                throw new ArgumentException("A panel CanvasGroup, EventSystem and initial selectable inside the panel are required.");
            if (back && !back.transform.IsChildOf(panel.transform)) throw new ArgumentException("The Back button must belong to this panel.");
            Unwire(); this.panel = panel; this.group = group; this.events = events; this.firstFocus = firstFocus; this.back = back;
            this.focusedInteractable = focusedInteractable; this.focusedRaycasts = focusedRaycasts;
            if (back) { back.onClick.AddListener(Back); wired = true; }
        }
        public IScreenFocusReturn CaptureFocusReturn()
        {
            if (!IsAvailable) throw new InvalidOperationException("Screen focus source is unavailable.");
            return new FocusReturn(events, events.currentSelectedGameObject);
        }
        public bool TryBind(Guid bindingId, out string error)
        {
            error = null;
            if (bindingId == Guid.Empty || binding != Guid.Empty || !IsAvailable) { error = "Screen view is unavailable or already bound."; return false; }
            rememberedSelection = null;
            priorInteractable = group.interactable; priorRaycasts = group.blocksRaycasts; binding = bindingId; focused = false;
            try
            {
                group.interactable = false; group.blocksRaycasts = false; panel.SetActive(true);
                if (binding != bindingId || !IsAvailable) { error = "Screen closed during binding."; Release(bindingId, false); return false; }
                return true;
            }
            catch { Release(bindingId, false); throw; }
        }
        public bool TrySetFocus(Guid bindingId, bool value)
        {
            if (binding == Guid.Empty || binding != bindingId || !IsFocusReady) return false;
            if (focused == value) return true;
            if (!value)
            {
                focused = false;
                var selected = events.currentSelectedGameObject;
                if (Belongs(selected)) { rememberedSelection = selected; events.SetSelectedGameObject(null); }
                if (binding != bindingId) return false;
                if (group) { group.interactable = false; group.blocksRaycasts = false; }
            }
            else
            {
                focused = true; group.interactable = focusedInteractable; group.blocksRaycasts = focusedRaycasts;
                var selected = SelectableNow(rememberedSelection) && Belongs(rememberedSelection) ? rememberedSelection : firstFocus.gameObject;
                // The configured first selectable can be disabled by the screen's own state (a departure card that is not
                // picked once two are); focus the first selectable that is usable instead of failing the whole screen.
                if (!SelectableNow(selected)) selected = FirstSelectableNow();
                if (!selected) return false;
                events.SetSelectedGameObject(selected);
            }
            return binding == bindingId && IsAvailable;
        }
        bool Belongs(GameObject target) => target && panel && target.transform.IsChildOf(panel.transform);
        GameObject FirstSelectableNow()
        {
            foreach (var candidate in panel.GetComponentsInChildren<Selectable>())
                if (SelectableNow(candidate.gameObject)) return candidate.gameObject;
            return null;
        }
        static bool SelectableNow(GameObject target)
        {
            if (!target || !target.activeInHierarchy) return false;
            var selectable = target.GetComponent<Selectable>(); return !selectable || selectable.isActiveAndEnabled && selectable.IsInteractable();
        }
        public void Unbind(Guid bindingId) { Release(bindingId, false); }
        void Release(Guid expected, bool notify)
        {
            if (binding == Guid.Empty || expected != binding) return;
            var previous = binding;
            bool clearFocus = events && Belongs(events.currentSelectedGameObject);
            binding = Guid.Empty; focused = false; rememberedSelection = null;
            var errors = new List<Exception>();
            Attempt(() => { if (clearFocus && events && !events.alreadySelecting) events.SetSelectedGameObject(null); }, errors);
            Attempt(() => { if (panel) panel.SetActive(false); }, errors);
            Attempt(() => { if (group) { group.interactable = priorInteractable; group.blocksRaycasts = priorRaycasts; } }, errors);
            if (notify) Attempt(() => Input?.Invoke(new ScreenIntent(previous, ScreenIntentKind.ViewClosed)), errors);
            if (errors.Count > 0) throw new AggregateException("Screen view cleanup failed.", errors);
        }
        static void Attempt(Action action, List<Exception> errors) { try { action(); } catch (Exception exception) { errors.Add(exception); } }
        sealed class FocusReturn : IScreenFocusReturn
        {
            readonly EventSystem events;
            readonly GameObject selected;
            bool returned;
            public FocusReturn(EventSystem events, GameObject selected) { this.events = events; this.selected = selected; }
            public void Restore()
            {
                if (returned) return; returned = true;
                if (!events || !events.isActiveAndEnabled) return;
                if (events.alreadySelecting) events.StartCoroutine(AfterSelection()); else Apply();
            }
            IEnumerator AfterSelection()
            {
                while (events && events.alreadySelecting) yield return null;
                Apply();
            }
            void Apply()
            {
                if (!events || !events.isActiveAndEnabled) return;
                var current = events.currentSelectedGameObject;
                if (!current || !current.activeInHierarchy) events.SetSelectedGameObject(SelectableNow(selected) ? selected : null);
            }
        }
        void Back() { if (binding != Guid.Empty && focused && isActiveAndEnabled) Input?.Invoke(new ScreenIntent(binding, ScreenIntentKind.Back)); }
        void OnDisable() { if (binding != Guid.Empty) Release(binding, true); }
        void OnDestroy() { disposed = true; if (binding != Guid.Empty) Release(binding, true); Unwire(); }
        void Unwire() { if (wired && back) back.onClick.RemoveListener(Back); wired = false; }
    }
}
