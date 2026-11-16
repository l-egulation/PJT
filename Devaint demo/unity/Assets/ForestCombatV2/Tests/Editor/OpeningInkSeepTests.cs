using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Look = ForestCombatV2.OpeningInkSeep.Look;

namespace ForestCombatV2.Tests
{
    // The helper's ink soaking into the hero (opening-playable.md part 3, user choice C 2026-11-13), on the ink body's
    // real idle frame: ink floods the sketch from the chest, then drains into the face with the eyes opening last.
    public sealed class OpeningInkSeepTests
    {
        static readonly Vector2 Heart = new Vector2(65, 86);

        static (Color32[] pixels, int w, int h) Frame()
        {
            var sprite = Resources.Load<Sprite>("UnarmedBody/idle/F01");
            Assert.That(sprite, Is.Not.Null, "the ink body's idle frame");
            var rect = sprite.textureRect; int w = (int)rect.width, h = (int)rect.height, stride = sprite.texture.width;
            var all = sprite.texture.GetPixels32(); var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) pixels[y * w + x] = all[((int)rect.y + y) * stride + (int)rect.x + x];
            return (pixels, w, h);
        }

        static bool Eye(Color32 c, int top) => c.r > 150 && c.g > 100 && c.b < 90 && top < 80;

        [Test]
        public void FloodsFromTheChestThenDrainsIntoTheFaceWithTheEyesOpeningLast()
        {
            var (pixels, w, h) = Frame();
            var maps = OpeningInkSeep.Build(pixels, w, h, Heart);
            var opaque = Enumerable.Range(0, w * h).Where(i => pixels[i].a >= 128).ToArray();
            Assert.That(opaque.Length, Is.GreaterThan(500));
            Look At(int i, float u) => OpeningInkSeep.At(maps, i, i % w, h - 1 - i / w, u);
            int Count(float u, Look look) => opaque.Count(i => At(i, u) == look);

            Assert.That(Count(0, Look.Paper), Is.EqualTo(opaque.Length), "still the sketch as the helper lands");

            // The ink starts at the chest: the chest is inked while the feet are still sketch.
            int chest = opaque.OrderBy(i => (i % w - Heart.x) * (i % w - Heart.x) + (h - 1 - i / w - Heart.y) * (h - 1 - i / w - Heart.y)).First();
            int foot = opaque.OrderBy(i => i / w).First();
            float early = OpeningInkSeep.FloodStart + .06f;
            Assert.That(At(chest, early), Is.EqualTo(Look.Ink));
            Assert.That(At(foot, early), Is.EqualTo(Look.Paper));

            Assert.That(Count(OpeningInkSeep.DrainStart, Look.Ink), Is.EqualTo(opaque.Length), "an ink silhouette before it drains");

            // Halfway through the drain the feet have their colour while the eyes are still ink.
            float half = OpeningInkSeep.DrainStart + OpeningInkSeep.DrainSeconds * .5f;
            Assert.That(At(foot, half), Is.EqualTo(Look.Colour));
            var eyes = opaque.Where(i => Eye(pixels[i], h - 1 - i / w)).ToArray();
            Assert.That(eyes.Length, Is.GreaterThan(0));
            Assert.That(eyes.All(i => At(i, half) == Look.Ink));

            // Just before the end only the eyes are left: everything else has its colour.
            float late = OpeningInkSeep.Seconds - .1f;
            Assert.That(opaque.Where(i => !eyes.Contains(i)).All(i => At(i, late) == Look.Colour), "the rest has its colour");
            Assert.That(eyes.All(i => At(i, late) == Look.Ink), "the eyes open last");

            Assert.That(Count(OpeningInkSeep.Seconds, Look.Colour), Is.EqualTo(opaque.Length), "the ink body's own colours at the end");
        }

        [Test]
        public void BuildsTheSameMapsEveryTime()
        {
            var (pixels, w, h) = Frame();
            var a = OpeningInkSeep.Build(pixels, w, h, Heart); var b = OpeningInkSeep.Build(pixels, w, h, Heart);
            Assert.That(a.Flood, Is.EqualTo(b.Flood)); Assert.That(a.Drain, Is.EqualTo(b.Drain));
        }
    }
}
