using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // The helper's ink soaking into the hero (docs/design/story/opening/opening-playable.md part 3, user choice C 2026-11-13):
    // ink floods the paper-tone body from the chest along noisy paths through its own pixels, so it follows the body like
    // ink in paper fibres, until the hero is an ink silhouette; then the ink drains toward the face, the tips first, the
    // colour coming back behind it, and the yellow eyes open last. Pixel arrays are Texture2D rows from the bottom.
    public static class OpeningInkSeep
    {
        public enum Look { Paper, Ink, Colour }

        public const float FloodStart = .05f, FloodSeconds = .35f, DrainStart = .5f, DrainSeconds = .85f;
        // The drain front has passed the eyes (the last pixels) by then.
        public const float Seconds = DrainStart + DrainSeconds;
        public static readonly Color32 Ink = new Color32(21, 18, 16, 255);

        const float FloodEdge = .035f, DrainBand = .08f, EyesLast = 1.12f, FaceRows = 80;

        // Per opaque pixel: when the flood reaches it (0 at the chest .. 1 at the farthest tip) and when the drain does
        // (0 at the farthest tip .. 1 at the face, the eyes after everything).
        public sealed class Maps { public float[] Flood, Drain; public int Width, Height; }

        // heart: the chest in image px from the top-left, where the helper sinks in.
        public static Maps Build(Color32[] colour, int w, int h, Vector2 heart)
        {
            int n = w * h; var opaque = new bool[n];
            for (int i = 0; i < n; i++) opaque[i] = colour[i].a >= 128;
            int seed = Nearest(opaque, w, h, heart.x, heart.y);
            var maps = new Maps { Width = w, Height = h, Flood = Normalise(opaque, w, h, Walk(opaque, w, h, seed, 2, 9), seed) };
            // The face: the dark pixels of the head.
            float sx = 0, sy = 0; int count = 0;
            for (int i = 0; i < n; i++)
            {
                int top = h - 1 - i / w; var c = colour[i];
                if (opaque[i] && top < FaceRows && c.r + c.g + c.b < 120) { sx += i % w; sy += top; count++; }
            }
            int face = Nearest(opaque, w, h, count > 0 ? sx / count : w / 2f, count > 0 ? sy / count : h / 2f);
            var toFace = Normalise(opaque, w, h, Walk(opaque, w, h, face, 3, 6), face);
            maps.Drain = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (!opaque[i]) continue; var c = colour[i];
                bool eye = c.r > 150 && c.g > 100 && c.b < 90 && h - 1 - i / w < FaceRows;
                maps.Drain[i] = eye ? EyesLast : 1 - toFace[i];
            }
            return maps;
        }

        // What pixel i (x, rows from the top) shows u seconds after the helper's ink reached the chest.
        public static Look At(Maps maps, int i, int x, int top, float u)
        {
            float flooded = Mathf.Max(-1, EaseOut((u - FloodStart) / FloodSeconds, 1.3f) * 1.08f - .04f) - maps.Flood[i];
            if (flooded < -FloodEdge) return Look.Paper;
            // The leading edge breaks into a fibrous dither.
            if (flooded < 0) return Bayer(x, top) < (flooded + FloodEdge) / FloodEdge * .8f ? Look.Ink : Look.Paper;
            float d = Mathf.Clamp01((u - DrainStart) / DrainSeconds) * 1.3f - .05f - maps.Drain[i];
            if (d > DrainBand) return Look.Colour;
            if (d > 0) return Bayer(x, top) < d / DrainBand ? Look.Colour : Look.Ink;
            return Look.Ink;
        }

        static float EaseOut(float u, float power) => 1 - Mathf.Pow(1 - Mathf.Clamp01(u), power);

        static readonly float[] BayerMatrix = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Bayer(int x, int y) => (BayerMatrix[(y & 3) * 4 + (x & 3)] + .5f) / 16;

        // A fixed 0..1 value per integer point.
        public static float Hash01(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296f;
            }
        }

        static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi, sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float a = Hash01(xi, yi), b = Hash01(xi + 1, yi), c = Hash01(xi, yi + 1), d = Hash01(xi + 1, yi + 1);
            return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
        }

        static int Nearest(bool[] opaque, int w, int h, float x, float top)
        {
            int best = -1; float bestDistance = float.MaxValue;
            for (int i = 0; i < opaque.Length; i++)
            {
                if (!opaque[i]) continue;
                float dx = i % w - x, dy = h - 1 - i / w - top, d = dx * dx + dy * dy;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        // Shortest noisy walk (8 neighbours) from one pixel through the opaque pixels.
        static float[] Walk(bool[] opaque, int w, int h, int from, float noise, float scale)
        {
            int n = w * h; var distance = new float[n];
            for (int i = 0; i < n; i++) distance[i] = float.PositiveInfinity;
            if (from < 0) return distance;
            var cost = new float[n];
            for (int i = 0; i < n; i++) cost[i] = 1 + noise * Noise(i % w / scale, (h - 1 - i / w) / scale);
            var heap = new MinHeap(); distance[from] = 0; heap.Push(0, from);
            while (heap.Count > 0)
            {
                var (k, i) = heap.Pop();
                if (k > distance[i]) continue;
                int x = i % w, y = i / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx; if (!opaque[j]) continue;
                        float next = k + cost[j] * (dx != 0 && dy != 0 ? 1.414f : 1);
                        if (next < distance[j]) { distance[j] = next; heap.Push(next, j); }
                    }
            }
            return distance;
        }

        // 0..1 over the reachable pixels; a pixel the walk cannot reach (a detached speck) goes by straight distance.
        static float[] Normalise(bool[] opaque, int w, int h, float[] distance, int from)
        {
            float max = 0;
            for (int i = 0; i < distance.Length; i++) if (opaque[i] && !float.IsInfinity(distance[i])) max = Mathf.Max(max, distance[i]);
            var result = new float[distance.Length];
            int fx = from < 0 ? w / 2 : from % w, fy = from < 0 ? h / 2 : from / w;
            for (int i = 0; i < distance.Length; i++)
            {
                if (!opaque[i]) continue;
                result[i] = !float.IsInfinity(distance[i]) && max > 0 ? distance[i] / max
                    : Mathf.Min(1, Vector2.Distance(new Vector2(i % w, i / w), new Vector2(fx, fy)) / 60);
            }
            return result;
        }

        sealed class MinHeap
        {
            readonly List<(float key, int value)> items = new List<(float, int)>();
            public int Count => items.Count;

            public void Push(float key, int value)
            {
                items.Add((key, value));
                for (int j = items.Count - 1; j > 0;)
                {
                    int p = (j - 1) / 2; if (items[p].key <= items[j].key) break;
                    (items[p], items[j]) = (items[j], items[p]); j = p;
                }
            }

            public (float key, int value) Pop()
            {
                var top = items[0]; var last = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
                if (items.Count > 0)
                {
                    items[0] = last;
                    for (int j = 0; ;)
                    {
                        int l = 2 * j + 1, r = l + 1, m = j;
                        if (l < items.Count && items[l].key < items[m].key) m = l;
                        if (r < items.Count && items[r].key < items[m].key) m = r;
                        if (m == j) break;
                        (items[m], items[j]) = (items[j], items[m]); j = m;
                    }
                }
                return top;
            }
        }
    }
}
