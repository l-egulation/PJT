using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Game
{
    // Main-owned bindings select original resource PNG bytes. Acquisition layers retain their shared source canvas.
    public sealed class VillageInkAssets : IDisposable
    {
        [Serializable] public sealed class Entry
        {
            public string id, resource;
            public int width, height;
            public int[] bounds;
            public bool crop;
        }
        [Serializable] public sealed class Frame { public string color; public int width, height; public float[] pour; }
        [Serializable] sealed class Data { public Entry[] images; public Frame[] frames; }
        public static readonly string[] Colors = { "red", "yellow", "green", "blue", "purple" };
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Frame> frames = new Dictionary<string, Frame>();

        public VillageInkAssets()
        {
            var json = Resources.Load<TextAsset>("VillageInk/Bindings");
            if (!json) throw new InvalidOperationException("VillageInk/Bindings is missing; prepare the village ink assets.");
            var data = JsonUtility.FromJson<Data>(json.text);
            foreach (var e in data.images) entries.Add(e.id, e);
            foreach (var f in data.frames) frames.Add(f.color, f);
        }
        public Frame GetFrame(int piece) => frames[Colors[Mathf.Clamp(piece - 1, 0, 4)]];
        public Sprite Get(string id)
        {
            if (sprites.TryGetValue(id, out var cached)) return cached;
            if (!entries.TryGetValue(id, out var e)) throw new InvalidOperationException("Village ink binding missing: " + id);
            var texture = Resources.Load<Texture2D>(e.resource);
            if (!texture) throw new InvalidOperationException("Village ink texture missing: " + e.resource);
            float sx = texture.width / (float)e.width, sy = texture.height / (float)e.height;
            var rect = e.crop
                ? new Rect(e.bounds[0] * sx, (e.height - e.bounds[3]) * sy,
                    (e.bounds[2] - e.bounds[0]) * sx, (e.bounds[3] - e.bounds[1]) * sy)
                : new Rect(0, 0, texture.width, texture.height);
            var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "VillageInk/" + id; sprite.hideFlags = HideFlags.DontSave;
            sprites.Add(id, sprite); return sprite;
        }
        public void Dispose()
        {
            foreach (var sprite in sprites.Values) if (sprite) UnityEngine.Object.Destroy(sprite);
            sprites.Clear();
        }
    }
}
