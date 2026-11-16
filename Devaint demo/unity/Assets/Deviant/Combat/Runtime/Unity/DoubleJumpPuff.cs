using UnityEngine;

namespace Deviant.Combat
{
    // The second jump steps on air (user choice 2026-11-05: A 「뭉게 발판」 of four pixelfx drafts): a flat cloud puff
    // kicked out where the feet were, spreading sideways, splitting from the middle and fading in four steps. It stays
    // where it was kicked (the hero rises away from it) and plays once on the arena clock, so it waits while the game is
    // paused. Frames: Resources/DoubleJumpFx/00..12 (180x110, foot pivot, PPU64), 40 ms each.
    public sealed class DoubleJumpPuff : MonoBehaviour
    {
        public const string FramePath = "DoubleJumpFx/";
        public const int FrameCount = 13;
        public const float FrameSeconds = .04f;
        static Sprite[] frames;

        CombatArena arena;
        SpriteRenderer view;
        float started;

        public static Sprite[] Frames()
        {
            if (frames != null && frames.Length == FrameCount && frames[0]) return frames;
            frames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++) frames[i] = Resources.Load<Sprite>(FramePath + i.ToString("00"));
            return frames;
        }

        public static DoubleJumpPuff Spawn(CombatArena arena, Vector2 feet, SpriteRenderer body)
        {
            var art = Frames();
            if (!arena || art[0] == null) return null;
            var node = new GameObject("Double jump puff");
            node.transform.SetParent(arena.transform, true);
            node.transform.position = new Vector3(feet.x, feet.y, 0);
            var puff = node.AddComponent<DoubleJumpPuff>();
            puff.arena = arena; puff.started = arena.Now;
            puff.view = node.AddComponent<SpriteRenderer>();
            if (body) { puff.view.sortingLayerID = body.sortingLayerID; puff.view.sortingOrder = body.sortingOrder - 1; }
            puff.view.sprite = art[0];
            return puff;
        }

        public int Frame => arena ? Mathf.FloorToInt((arena.Now - started) / FrameSeconds) : FrameCount;

        void LateUpdate()
        {
            int frame = Frame;
            // The arena clock goes back on a reset: the puff belongs to the run that made it.
            if (!arena || frame >= FrameCount || arena.Now < started) { Destroy(gameObject); return; }
            view.sprite = Frames()[Mathf.Max(0, frame)];
        }
    }
}
