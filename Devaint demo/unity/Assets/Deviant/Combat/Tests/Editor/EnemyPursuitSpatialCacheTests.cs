using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    // The pursuit terrain/graph spatial caches must answer exactly like the full scans they replaced, id-order tie
    // breaks included.
    public class EnemyPursuitSpatialCacheTests
    {
        static EnemyPursuitSettings Settings() => new EnemyPursuitSettings
        {
            schema = "enemy-pursuit-v1", appliesTo = new[] { "K4" }, chaseSpeedScale = 1.6f, jumpHeight = 3.2f, jumpAirSpeed = 6,
            jumpWindupSeconds = .12f, maxDropHeight = 16, replanSeconds = .25f, detectionScale = 1, dropThrough = true, walkOffLedges = true,
        };
        // Full scans exactly as before the caches.
        static bool SolidScan(PursuitTerrain t, float x0, float x1, float y0, float y1)
        {
            foreach (var b in t.boxes)
                if (!b.oneWay && b.x0 < x1 - 1e-4f && b.x1 > x0 + 1e-4f && b.y0 < y1 - 1e-4f && b.y1 > y0 + 1e-4f) return true;
            return false;
        }
        static float CeilingScan(PursuitTerrain t, float x0, float x1, float y)
        {
            float c = float.PositiveInfinity;
            foreach (var b in t.boxes)
                if (!b.oneWay && b.x0 < x1 - 1e-4f && b.x1 > x0 + 1e-4f && b.y0 >= y - 1e-3f) c = Mathf.Min(c, b.y0);
            return c;
        }
        static PursuitSegment AtScan(PursuitGraph g, float hw, float x, float feetY, float tol = .08f)
        {
            PursuitSegment best = null; float bestD = float.PositiveInfinity;
            foreach (var s in g.segments)
            {
                if (Mathf.Abs(s.y - feetY) > tol || x + hw < s.x0 - .05f || x - hw > s.x1 + .05f) continue;
                float d = Mathf.Max(0, Mathf.Max(s.c0 - x, x - s.c1));
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }
        static PursuitSegment BelowScan(PursuitGraph g, float hw, float x, float y)
        {
            PursuitSegment best = null;
            foreach (var s in g.segments)
                if (s.y <= y + .05f && x + hw > s.x0 + .05f && x - hw < s.x1 - .05f && s.Standable && (best == null || s.y > best.y)) best = s;
            return best;
        }
        static float Snap(System.Random r, float min, float max, float step) => min + step * r.Next(0, (int)((max - min) / step) + 1);

        static PursuitTerrain RandomTerrain(int seed)
        {
            var r = new System.Random(seed); var t = new PursuitTerrain();
            // Wide ground and walls (some cross many cells), stacked decks, duplicated tops for tie breaks, thin one-ways.
            t.boxes.Add(new PursuitTerrain.Box { x0 = -40, x1 = 240, y0 = -6, y1 = 0 });
            t.boxes.Add(new PursuitTerrain.Box { x0 = -42, x1 = -40, y0 = -6, y1 = 200 });
            t.boxes.Add(new PursuitTerrain.Box { x0 = -3000, x1 = 3000, y0 = -3000, y1 = -2990 });
            for (int i = 0; i < 160; i++)
            {
                float x = Snap(r, -30, 220, .125f), y = Snap(r, 0, 120, .25f), w = Snap(r, .5f, 30, .125f);
                bool oneWay = r.NextDouble() < .35;
                float h = oneWay ? .125f : Snap(r, .25f, 6, .25f);
                t.boxes.Add(new PursuitTerrain.Box { x0 = x, x1 = x + w, y0 = y - h, y1 = y, oneWay = oneWay, noDrop = r.NextDouble() < .1 });
                if (r.NextDouble() < .15) t.boxes.Add(new PursuitTerrain.Box { x0 = x + w + Snap(r, 1, 6, .5f), x1 = x + w + Snap(r, 7, 14, .5f), y0 = y - h, y1 = y, oneWay = oneWay });
            }
            return t;
        }

        [TestCase(1)] [TestCase(7)] [TestCase(42)] [TestCase(1234)]
        public void CachedTerrainAndGraphLookupsEqualTheFullScans(int seed)
        {
            var t = RandomTerrain(seed); var s = Settings();
            const float hw = .5f;
            var g = t.Graph(hw, 1.3f, 6, s.jumpHeight, s);
            Assert.That(g.segments.Count, Is.GreaterThan(50));
            var r = new System.Random(seed * 31 + 5);
            var probesX = new List<float>(); var probesY = new List<float>();
            foreach (var seg in g.segments)
            {
                // Exact edges, stand-range ends and just outside them.
                foreach (float x in new[] { seg.x0, seg.x1, seg.c0, seg.c1, seg.x0 - hw - .05f, seg.x1 + hw + .05f, seg.x0 + hw - .05f, (seg.x0 + seg.x1) / 2 })
                { probesX.Add(x); probesY.Add(seg.y); }
            }
            for (int i = 0; i < 4000; i++) { probesX.Add((float)(r.NextDouble() * 280 - 40)); probesY.Add((float)(r.NextDouble() * 130 - 5)); }
            for (int i = 0; i < probesX.Count; i++)
            {
                float x = probesX[i], y = probesY[i];
                foreach (float dy in new[] { 0f, .05f, -.08f, .3f })
                {
                    Assert.That(g.At(x, y + dy), Is.SameAs(AtScan(g, hw, x, y + dy)), $"At({x},{y + dy})");
                    Assert.That(g.At(x, y + dy, .3f), Is.SameAs(AtScan(g, hw, x, y + dy, .3f)), $"At wide({x},{y + dy})");
                    Assert.That(g.Below(x, y + dy), Is.SameAs(BelowScan(g, hw, x, y + dy)), $"Below({x},{y + dy})");
                }
                float w = (float)(r.NextDouble() * 3), h = (float)(r.NextDouble() * 3);
                Assert.That(t.SolidIn(x - w, x + w, y, y + h), Is.EqualTo(SolidScan(t, x - w, x + w, y, y + h)), $"SolidIn({x},{y})");
                Assert.That(t.SolidIn(x, x, y, y), Is.EqualTo(SolidScan(t, x, x, y, y)), $"SolidIn point({x},{y})");
                Assert.That(t.CeilingAbove(x - hw, x + hw, y), Is.EqualTo(CeilingScan(t, x - hw, x + hw, y)), $"CeilingAbove({x},{y})");
            }
        }

        [Test] public void EqualCandidatesKeepTheLowestSegmentId()
        {
            // Two separate decks at the same height on either side of a gap, and two overlapping equal tops.
            var t = new PursuitTerrain();
            t.boxes.Add(new PursuitTerrain.Box { x0 = 0, x1 = 10, y0 = -1, y1 = 0 });
            t.boxes.Add(new PursuitTerrain.Box { x0 = 12, x1 = 22, y0 = -1, y1 = 0 });
            t.boxes.Add(new PursuitTerrain.Box { x0 = 4, x1 = 18, y0 = 5 - .125f, y1 = 5, oneWay = true });
            t.boxes.Add(new PursuitTerrain.Box { x0 = 4, x1 = 18, y0 = 5 - .125f, y1 = 5, oneWay = true, noDrop = true });
            var s = Settings(); var g = t.Graph(.5f, 1.3f, 6, s.jumpHeight, s);
            for (float x = -1; x <= 23; x += .0625f)
                foreach (float y in new[] { 0f, 5f, 2.5f, 7f })
                {
                    Assert.That(g.At(x, y), Is.SameAs(AtScan(g, .5f, x, y)));
                    Assert.That(g.Below(x, y), Is.SameAs(BelowScan(g, .5f, x, y)));
                }
        }

        [Test] public void BoxesAddedAfterAQueryAreIndexed()
        {
            var t = new PursuitTerrain();
            t.boxes.Add(new PursuitTerrain.Box { x0 = 0, x1 = 10, y0 = -1, y1 = 0 });
            Assert.That(t.SolidIn(20, 21, -.5f, .5f), Is.False);
            t.boxes.Add(new PursuitTerrain.Box { x0 = 20, x1 = 30, y0 = -1, y1 = 0 });
            Assert.That(t.SolidIn(20, 21, -.5f, .5f), Is.True);
            Assert.That(t.CeilingAbove(24, 25, -2), Is.EqualTo(-1));
        }
    }
}
