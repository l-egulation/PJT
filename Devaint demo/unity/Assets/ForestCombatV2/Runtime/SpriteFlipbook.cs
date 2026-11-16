using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // Numbered presentation frames from Resources (folder/01, 02, ...) played once on real time, 80 ms per frame by
    // default (the art's review timing, not a gameplay timer). Playback never drives rules; callers decide when to start
    // and what stays shown afterwards.
    public sealed class SpriteFlipbook
    {
        public const float DefaultFrameSeconds = .08f;
        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        public readonly Sprite[] Frames;
        readonly float frameSeconds;
        float startedAt = -1;

        public SpriteFlipbook(string folder, float frameSeconds = DefaultFrameSeconds) { Frames = Load(folder); this.frameSeconds = frameSeconds; }

        public static Sprite[] Load(string folder)
        {
            if (cache.TryGetValue(folder, out var frames)) return frames;
            var list = new List<Sprite>();
            for (int i = 1; ; i++) { var sprite = Resources.Load<Sprite>($"{folder}/{i:00}"); if (!sprite) break; list.Add(sprite); }
            if (list.Count == 0) Debug.LogWarning("Flipbook frames are missing: " + folder + " (tools/assets/update_assets.py).");
            return cache[folder] = list.ToArray();
        }

        public bool Ready => Frames.Length > 0;
        public bool Playing => startedAt >= 0 && Time.unscaledTime - startedAt < Frames.Length * frameSeconds;
        public bool Started => startedAt >= 0;
        public void Play() { if (Ready) startedAt = Time.unscaledTime; }
        public void Stop() { startedAt = -1; }
        // Current frame while playing; null before start or after the last frame.
        public Sprite Current
        {
            get
            {
                if (!Playing) return null;
                return Frames[Mathf.Min(Frames.Length - 1, (int)((Time.unscaledTime - startedAt) / frameSeconds))];
            }
        }
    }
}
