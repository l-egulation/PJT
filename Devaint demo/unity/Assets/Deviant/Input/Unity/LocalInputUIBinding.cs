using System;
using System.Collections.Generic;
using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Input.Unity
{
    // Explicit scene binding. Native uGUI keeps its normal navigation/submit and
    // pointer implementation; this component lends it this player's actions.
    public sealed class LocalInputUIBinding : MonoBehaviour
    {
        public bool FixedMenuKeys;
        public ScrollRect KeyboardScrollOverride;
        InputActionAsset fixedAsset; InputActionMap fixedMap;
        InputActionMap FixedMap
        {
            get
            {
                if(fixedAsset)return fixedMap;
                fixedAsset=ScriptableObject.CreateInstance<InputActionAsset>();
                fixedMap=new InputActionMap("Approved menu");fixedAsset.AddActionMap(fixedMap);return fixedMap;
            }
        }
        LocalInput input;
        InputSystemUIInputModule module;
        EventSystem events;
        readonly List<InputActionReference> owned = new List<InputActionReference>();
        InputActionReference oldPoint, oldClick, oldMove, oldSubmit, oldCancel, oldScroll;
        bool oldNavigation, boundFixedMenuKeys;
        GameObject panel, ring;
        GameObject lastSelection;
        ScrollRect[] scrolls = Array.Empty<ScrollRect>();
        public void Bind(LocalInput owner, EventSystem eventSystem)
        {
            if (input == owner && events == eventSystem && boundFixedMenuKeys == FixedMenuKeys) return;
            Unbind();
            if (!owner || !eventSystem) throw new ArgumentNullException(nameof(owner));
            input = owner; events = eventSystem; boundFixedMenuKeys = FixedMenuKeys;
            module = events.GetComponent<InputSystemUIInputModule>() ?? events.gameObject.AddComponent<InputSystemUIInputModule>();
            oldPoint = module.point; oldClick = module.leftClick; oldMove = module.move;
            oldSubmit = module.submit; oldCancel = module.cancel; oldScroll = module.scrollWheel;
            oldNavigation = events.sendNavigationEvents;
            // The source has already enabled UI actions. Native UI therefore
            // treats them as externally owned and will not disable them on exit.
            module.point = Reference(GameAction.Point); module.leftClick = Reference(GameAction.Click);
            var move=FixedMenuKeys?FixedMove():Reference(GameAction.Navigate);
            var submit=FixedMenuKeys?FixedButton("Fixed submit","<Keyboard>/f","<Gamepad>/buttonSouth"):Reference(GameAction.Submit);
            var cancel=FixedMenuKeys?FixedButton("Fixed cancel","<Keyboard>/escape","<Gamepad>/buttonEast"):Reference(GameAction.Cancel);
            module.move=move;module.submit=submit;module.cancel=cancel;module.scrollWheel=Reference(GameAction.Scroll);
            if(fixedAsset)fixedAsset.Enable();
        }
        InputActionReference FixedMove()
        {
            var action=FixedMap.AddAction("Fixed menu move",InputActionType.Value);action.expectedControlType="Vector2";
            action.AddCompositeBinding("2DVector").With("Up","<Keyboard>/upArrow").With("Down","<Keyboard>/downArrow").With("Left","<Keyboard>/leftArrow").With("Right","<Keyboard>/rightArrow");
            action.AddBinding("<Gamepad>/dpad");action.AddBinding("<Gamepad>/leftStick");return OwnFixed(action);
        }
        InputActionReference FixedButton(string name,string keyboard,string pad)
        {var action=FixedMap.AddAction(name,InputActionType.Button);action.AddBinding(keyboard);action.AddBinding(pad);return OwnFixed(action);}
        InputActionReference OwnFixed(InputAction action)
        {var reference=InputActionReference.Create(action);owned.Add(reference);return reference;}
        InputActionReference Reference(GameAction action)
        { var value = InputActionReference.Create(input.Source.GetAction(action)); owned.Add(value); return value; }
        public void SetPanel(GameObject focused)
        {
            if (panel != focused) scrolls = focused ? focused.GetComponentsInChildren<ScrollRect>() : Array.Empty<ScrollRect>();
            panel = focused;
            if (!events) return;
            events.sendNavigationEvents = focused && !input.Rebinding;
            if (!focused && events.currentSelectedGameObject && !events.alreadySelecting) events.SetSelectedGameObject(null);
            if (focused) input.SuppressGameplay();
        }
        void LateUpdate()
        {
            if (!input || !events || !module) return;
            if (!panel || !panel.activeInHierarchy)
            { if (ring) ring.SetActive(false); return; }
            input.SuppressGameplay();
            var selected = events.currentSelectedGameObject;
            var selectable = selected ? selected.GetComponent<Selectable>() : null;
            if (!selected || !selected.transform.IsChildOf(panel.transform) || !selected.activeInHierarchy || !selectable || !selectable.IsInteractable())
            {
                foreach (var candidate in panel.GetComponentsInChildren<Selectable>())
                    if (candidate.isActiveAndEnabled && candidate.IsInteractable())
                    { events.SetSelectedGameObject(candidate.gameObject); selected = candidate.gameObject; break; }
            }
            if (selected != lastSelection) { lastSelection = selected; Reveal(selected); }
            if(FixedMenuKeys&&Keyboard.current!=null&&!(selected&&selected.GetComponent<InputField>()))
            {
                float direction=(Keyboard.current.wKey.isPressed?1:0)-(Keyboard.current.sKey.isPressed?1:0);
                if(direction!=0)
                {
                    ScrollRect nearest=KeyboardScrollOverride&&KeyboardScrollOverride.isActiveAndEnabled?KeyboardScrollOverride:null;float distance=nearest?float.NegativeInfinity:float.PositiveInfinity;
                    foreach(var candidate in panel.GetComponentsInChildren<ScrollRect>())
                    {
                        if(!candidate.isActiveAndEnabled||!candidate.vertical||!candidate.content||!candidate.viewport||candidate.content.rect.height<=candidate.viewport.rect.height)continue;
                        float next=selected?(candidate.transform.position-selected.transform.position).sqrMagnitude:0;
                        if(next<distance){distance=next;nearest=candidate;}
                    }
                    if(nearest)nearest.verticalNormalizedPosition=Mathf.Clamp01(nearest.verticalNormalizedPosition+direction*Time.unscaledDeltaTime*1.2f);
                }
            }
            if (input.UsingGamepad && selected)
            {
                if (!ring) BuildRing();
                ring.transform.SetParent(selected.transform, false);
                var rect = (RectTransform)ring.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-2, -2); rect.offsetMax = new Vector2(2, 2); ring.SetActive(true);
                var scroll = selected.GetComponentInParent<ScrollRect>();
                if (!scroll)
                {
                    float distance = float.PositiveInfinity;
                    foreach (var candidate in scrolls)
                    {
                        if (!candidate || !candidate.isActiveAndEnabled || !candidate.vertical || !candidate.content || !candidate.viewport ||
                            candidate.content.rect.height <= candidate.viewport.rect.height) continue;
                        float next = (candidate.transform.position - selected.transform.position).sqrMagnitude;
                        if (next < distance) { distance = next; scroll = candidate; }
                    }
                }
                if (scroll && scroll.vertical)
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + input.Read(GameAction.MapPan).Y * Time.unscaledDeltaTime);
            }
            else if (ring) ring.SetActive(false);
        }
        static void Reveal(GameObject selected)
        {
            if (!selected) return;
            var scroll = selected.GetComponentInParent<ScrollRect>(); var item = selected.transform as RectTransform;
            if (!scroll || !scroll.viewport || !scroll.content || !item) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, item); var view = scroll.viewport.rect;
            var delta = Vector2.zero;
            if (scroll.vertical) delta.y = bounds.min.y < view.yMin ? view.yMin - bounds.min.y : bounds.max.y > view.yMax ? view.yMax - bounds.max.y : 0;
            if (scroll.horizontal) delta.x = bounds.min.x < view.xMin ? view.xMin - bounds.min.x : bounds.max.x > view.xMax ? view.xMax - bounds.max.x : 0;
            scroll.content.anchoredPosition += delta;
        }
        void BuildRing()
        {
            ring = new GameObject("Gamepad focus", typeof(RectTransform));
            for (int i = 0; i < 4; i++)
            {
                var image = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(ring.transform, false); image.color = new Color32(27, 141, 124, 255); image.raycastTarget = false;
                var r = image.rectTransform;
                r.anchorMin = i == 0 ? Vector2.zero : i == 1 ? Vector2.up : i == 2 ? Vector2.zero : Vector2.right;
                r.anchorMax = i == 0 ? Vector2.right : i == 1 ? Vector2.one : i == 2 ? Vector2.up : Vector2.one;
                r.pivot = i == 0 || i == 2 ? Vector2.zero : Vector2.one;
                r.sizeDelta = i < 2 ? new Vector2(0, 2) : new Vector2(2, 0); r.anchoredPosition = Vector2.zero;
            }
        }
        public void Unbind()
        {
            bool ownsModule = module && owned.Contains(module.move);
            if (module)
            {
                if (owned.Contains(module.point)) module.point = oldPoint;
                if (owned.Contains(module.leftClick)) module.leftClick = oldClick;
                if (owned.Contains(module.move)) module.move = oldMove;
                if (owned.Contains(module.submit)) module.submit = oldSubmit;
                if (owned.Contains(module.cancel)) module.cancel = oldCancel;
                if (owned.Contains(module.scrollWheel)) module.scrollWheel = oldScroll;
            }
            if (events && ownsModule) events.sendNavigationEvents = oldNavigation;
            foreach (var reference in owned) if (reference) { if (Application.isPlaying) Object.Destroy(reference); else Object.DestroyImmediate(reference); }
            if(fixedAsset){fixedAsset.Disable();if(Application.isPlaying)Object.Destroy(fixedAsset);else Object.DestroyImmediate(fixedAsset);}fixedAsset=null;fixedMap=null;
            owned.Clear(); scrolls = Array.Empty<ScrollRect>(); if (ring) Object.Destroy(ring); ring = null; lastSelection = panel = null; input = null; module = null; events = null;
        }
        void OnDisable() => Unbind();
        void OnDestroy() => Unbind();
    }
}
