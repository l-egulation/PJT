using UnityEngine;

namespace Deviant.Combat
{
    // Test-branch fixed combat framing (port of the browser lab's 전투 카메라 배율): while the hit-feel
    // effects are on, the orthographic view is always pulled in by `zoom`, with no transition when a
    // fight starts. The chapter world rewrites the base size every LateUpdate (500); this runs after it
    // and only scales the size the frame already chose. Presentation only: camera position, bounds and
    // combat are untouched.
    [DefaultExecutionOrder(750)]
    public sealed class CombatFeelCamera : MonoBehaviour
    {
        CombatArena arena;
        CombatFeelSettings settings;
        float written = -1, baseSize, punch = 1, punchUntil = -1;
        public float CurrentZoom { get; private set; } = 1;
        // A short extra pull-in on top of the fixed zoom (e.g. the hammer range impact), until the simulation clock passes `until`.
        public void Punch(float scale, float until)
        { if (arena && !arena.CombatScreenEffectsAllowed) return; punch = scale; punchUntil = until; }
        float ActivePunch => arena && arena.Now < punchUntil ? punch : 1;
        // Set by rooms built as exactly one camera view (boss rooms), so the fixed zoom never crops their edges.
        public bool HoldBaseFraming { get; set; }
        public void Bind(CombatArena owner, CombatFeelSettings configuration) { arena = owner; settings = configuration; }
        public void Clear() => Restore();
        void OnDisable() => Restore();
        void Restore()
        {
            var view = arena ? arena.ViewCamera : null;
            if (view && written > 0 && Mathf.Approximately(view.orthographicSize, written)) view.orthographicSize = baseSize;
            written = -1; CurrentZoom = 1; punchUntil = -1;
        }
        void LateUpdate()
        {
            var view = arena ? arena.ViewCamera : null;
            if (!view || !view.orthographic || settings == null || settings.combatCamera == null) return;
            float extra = ActivePunch;
            bool on = arena.CombatScreenEffectsAllowed && arena.FeelFeedback && arena.FeelFeedback.EffectsEnabled && (settings.combatCamera.zoom > 0 || extra != 1) && !HoldBaseFraming;
            if (!on) { Restore(); return; }
            // A size we did not write is the frame's base (the world sets it each frame).
            if (written < 0 || !Mathf.Approximately(view.orthographicSize, written)) baseSize = view.orthographicSize;
            CurrentZoom = (1 + Mathf.Max(0, settings.combatCamera.zoom)) * extra;
            float size = baseSize / CurrentZoom;
            if (!Mathf.Approximately(view.orthographicSize, size)) view.orthographicSize = size;
            written = size;
        }
    }
}
