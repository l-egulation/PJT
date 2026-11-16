using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Every uGUI button answers the pointer (playtest 2026-11-05: 「눌리고 있는지 모르겠다」): it rises two pixels while the
    // pointer is over it, sinks one pixel and darkens while held, and gives a small hop when released over it. Moves are
    // whole pixels, so the pixel art stays sharp (no scaling). A watcher adds it to any active button in any scene, so
    // buttons made later by code get it too. Buttons that cannot be pressed do nothing.
    [DisallowMultipleComponent]
    public sealed class ButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public const float Lift = 2, Sink = 1, Hop = 2, PressShade = .82f, EaseSeconds = .06f, HopSeconds = .14f;
        Selectable selectable;
        Graphic shaded;
        bool over, held;
        float offset, hopAt = -1, lastShade = 1;
        Vector2 basePosition, written;
        bool placed;
        RectTransform rect;

        public bool Over => over;
        public bool Held => held;
        public float Offset => offset;

        void Awake()
        {
            rect = transform as RectTransform; selectable = GetComponent<Selectable>();
            // A colour-tint button already shades itself; only plain ones are shaded here.
            if (selectable && selectable.transition != Selectable.Transition.ColorTint) shaded = GetComponent<Graphic>();
        }

        bool Live => selectable && selectable.IsInteractable() && isActiveAndEnabled;
        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = false; held = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && Live) held = true; }
        public void OnPointerUp(PointerEventData e)
        {
            if (held && over && Live) hopAt = Time.unscaledTime;
            held = false;
        }

        void LateUpdate()
        {
            if (!rect) return;
            bool live = Live;
            float target = !live ? 0 : held ? -Sink : over ? Lift : 0;
            float step = Time.unscaledDeltaTime / EaseSeconds * (Lift + Sink);
            offset = Mathf.MoveTowards(offset, target, step);
            float hop = 0;
            if (hopAt >= 0)
            {
                float k = (Time.unscaledTime - hopAt) / HopSeconds;
                if (k >= 1) hopAt = -1; else hop = Hop * Mathf.Sin(k * Mathf.PI);
            }
            // Someone else placed the button since the last frame (or this is the first): that is the new rest position.
            var now = rect.anchoredPosition;
            if (!placed || (now - written).sqrMagnitude > .0001f) { basePosition = now; placed = true; }
            written = basePosition + new Vector2(0, Mathf.Round(offset + hop));
            if ((written - now).sqrMagnitude > .0001f) rect.anchoredPosition = written;
            float shade = held && live ? PressShade : 1;
            if (shaded && shade != lastShade) { shaded.canvasRenderer.SetColor(new Color(shade, shade, shade, shaded.canvasRenderer.GetAlpha())); lastShade = shade; }
        }

        void OnDisable()
        {
            if (rect && placed && (rect.anchoredPosition - written).sqrMagnitude < .0001f) rect.anchoredPosition = basePosition;
            if (shaded && lastShade != 1) shaded.canvasRenderer.SetColor(new Color(1, 1, 1, shaded.canvasRenderer.GetAlpha()));
            lastShade = 1;
            over = held = false; offset = 0; hopAt = -1; placed = false;
        }

        // ---- The watcher: gives every active button the feel, a few times a second.
        sealed class Watcher : MonoBehaviour
        {
            float next;
            void Update()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + .2f;
                foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (!button.GetComponent<ButtonFeel>()) button.gameObject.AddComponent<ButtonFeel>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWatching()
        {
            // Kept across scenes and gone with Play mode (no DontSave flag, which would outlive it in the Editor).
            var go = new GameObject("Button feel") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go); go.AddComponent<Watcher>();
        }

        // Tests and code that build a panel can give its buttons the feel at once.
        public static void Attach(GameObject root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (!button.GetComponent<ButtonFeel>()) button.gameObject.AddComponent<ButtonFeel>();
        }
    }
}
