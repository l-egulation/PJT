using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deviant.Replay.Unity
{
    public sealed class ReplayUiTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public static readonly HashSet<ReplayUiTarget> Active = new HashSet<ReplayUiTarget>();
        public static event Action<ReplayUiTarget, int> Observed;
        public string Id { get; private set; }
        public bool IsScreen { get; private set; }
        public long OpenVersion { get; private set; }
        public void Configure(string id, bool screen = false) { Id = id; IsScreen = screen; }
        void OnEnable() { Active.Add(this); OpenVersion++; Emit(4); }
        void OnDisable() { Emit(5); Active.Remove(this); }
        public void OnPointerEnter(PointerEventData e) => Emit(0);
        public void OnPointerExit(PointerEventData e) => Emit(1);
        public void OnSelect(BaseEventData e) => Emit(2);
        public void OnDeselect(BaseEventData e) => Emit(3);
        public void Activated() => Emit(6);
        void Emit(int kind)
        {
            // Recording failures must never escape into the UI's action/selection callback.
            try { Observed?.Invoke(this, kind); } catch (Exception) { /* recorder owns its failure state */ }
        }
        public static ReplayUiTarget Attach(GameObject target, string id, bool screen = false)
        {
            var marker = target.GetComponent<ReplayUiTarget>() ?? target.AddComponent<ReplayUiTarget>(); marker.Configure(id, screen); return marker;
        }
        public static void RegisterTree(Transform root)
        {
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable.GetComponent<ReplayUiTarget>()) continue;
                // Structural fallback is deliberately build-scoped, never a translated object name.
                var path = new Stack<int>(); var p = selectable.transform;
                while (p && p != root) { path.Push(p.GetSiblingIndex()); p = p.parent; }
                var marker = Attach(selectable.gameObject, "ui.structural." + string.Join(".", path));
                if (selectable is Button button) button.onClick.AddListener(marker.Activated);
            }
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                if (scroll.GetComponent<ReplayUiTarget>()) continue;
                var path = new Stack<int>(); var p = scroll.transform;
                while (p && p != root) { path.Push(p.GetSiblingIndex()); p = p.parent; }
                Attach(scroll.gameObject, "ui.scroll." + string.Join(".", path));
            }
        }
    }
}
