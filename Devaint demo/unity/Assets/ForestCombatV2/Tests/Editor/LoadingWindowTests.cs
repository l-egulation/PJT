using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The loading window's edge and its place-to-place sweep (opening-playable.md 「로딩」, user decision 2026-11-13).
    public sealed class LoadingWindowTests
    {
        const int B = LoadingWindowCircle.Blocks, Half = B / 2;

        // Every radius the opening passes through keeps the jaggies rule: going from 12 o'clock to 45°, the steps of the
        // window edge, the paper rim and both shade rings only get shorter, each one block lower than the last, and the
        // whole window is the same in all eight eighths.
        [Test]
        public void TheWindowEdgeStepsOnlyShortenTowardsTheDiagonal()
        {
            for (int radius = 2; radius <= ChapterLoadingView.Radius; radius++)
            {
                var cells = LoadingWindowCircle.Draw(radius);
                for (int y = 0; y < B; y++)
                    for (int x = 0; x < B; x++)
                    {
                        byte c = cells[y * B + x];
                        Assert.That(cells[y * B + (B - 1 - x)], Is.EqualTo(c), $"radius {radius}: left-right mirror at {x},{y}");
                        Assert.That(cells[(B - 1 - y) * B + x], Is.EqualTo(c), $"radius {radius}: top-bottom mirror at {x},{y}");
                        Assert.That(cells[x * B + y], Is.EqualTo(c), $"radius {radius}: diagonal mirror at {x},{y}");
                    }
                AssertSteps(cells, radius, c => c != LoadingWindowCircle.Outside && c != LoadingWindowCircle.Rim, "window");
                AssertSteps(cells, radius, c => c == LoadingWindowCircle.Rim, "rim");
                AssertSteps(cells, radius, c => c == LoadingWindowCircle.Shade, "shade");
                if (radius >= 6) AssertSteps(cells, radius, c => c == LoadingWindowCircle.DeepShade, "deep shade");
            }
            foreach (int none in new[] { 0, 1 }) Assert.That(System.Array.TrueForAll(LoadingWindowCircle.Draw(none), c => c == LoadingWindowCircle.Outside), "under one block the window is shut");
        }

        // In the top-right quarter, each column's highest cell of the ring; the columns above the diagonal form the steps.
        static void AssertSteps(byte[] cells, int radius, System.Func<byte, bool> ring, string name)
        {
            var tops = new List<int>();
            for (int x = 0; x < Half; x++)
            {
                int top = -1;
                for (int y = 0; y < Half; y++) if (ring(cells[(Half - 1 - y) * B + Half + x])) top = y;
                if (top < x) break;
                tops.Add(top);
            }
            Assert.That(tops.Count, Is.GreaterThan(0), $"radius {radius}: {name} has an eighth");
            var lengths = new List<int>();
            for (int i = 0; i < tops.Count; i++)
            {
                if (i > 0 && tops[i] == tops[i - 1]) { lengths[lengths.Count - 1]++; continue; }
                if (i > 0) Assert.That(tops[i - 1] - tops[i], Is.EqualTo(1), $"radius {radius}: {name} drops one block at a time ({string.Join(",", tops)})");
                lengths.Add(1);
            }
            for (int i = 1; i < lengths.Count; i++)
                Assert.That(lengths[i], Is.LessThanOrEqualTo(lengths[i - 1]), $"radius {radius}: {name} steps {string.Join(" ", lengths)}");
        }

        // Leaving one place for another, the window opens on the place being left, the seam sweeps in after SeamStart and
        // the place being entered fills the window by SeamEnd, inside the intro before the load; the same place or no
        // known place shows the destination alone.
        [Test]
        public void TheWindowSweepsFromThePlaceBeingLeftToThePlaceEntered()
        {
            Assert.That(ChapterLoadingView.SeamEnd, Is.LessThanOrEqualTo(ChapterLoadingView.OpenSeconds + .5f + .3f), "the sweep ends inside the intro");
            var pairs = new (LoadingPlace to, LoadingPlace from)[]
                { (LoadingPlace.Chapter2, LoadingPlace.Chapter1), (LoadingPlace.Village, LoadingPlace.Chapter1), (LoadingPlace.Village, LoadingPlace.Chapter2), (LoadingPlace.Chapter1, LoadingPlace.Village) };
            foreach (bool colour in new[] { false, true })
                foreach (var (to, from) in pairs)
                {
                    var view = ChapterLoadingView.For(to, from, colour);
                    var root = new GameObject("Loading check", typeof(RectTransform));
                    try
                    {
                        view.Build((RectTransform)root.transform, new Vector2(480, 240), null);
                        Assert.That(view.Missing, Is.Empty, $"{from} -> {to}: art");
                        Assert.That(view.Leaving, Is.EqualTo(from));
                        Assert.That(view.SeamAt(0), Is.EqualTo(ChapterLoadingView.Window), "opens on the place being left");
                        Assert.That(view.SeamAt(ChapterLoadingView.SeamStart), Is.EqualTo(ChapterLoadingView.Window));
                        int mid = view.SeamAt((ChapterLoadingView.SeamStart + ChapterLoadingView.SeamEnd) / 2);
                        Assert.That(mid, Is.InRange(2, ChapterLoadingView.Window - 2));
                        Assert.That(mid % 2, Is.Zero, "the seam keeps the 2 px grid");
                        Assert.That(view.SeamAt(ChapterLoadingView.SeamEnd), Is.Zero, "the place entered fills the window");
                        int last = ChapterLoadingView.Window;
                        for (float t = 0; t < 1.3f; t += 1 / 60f) { int s = view.SeamAt(t); Assert.That(s, Is.LessThanOrEqualTo(last)); last = s; view.Tick(t); }
                        AssertClips(root, from, to);
                    }
                    finally { view.Dispose(); Object.DestroyImmediate(root); }
                }
            foreach (var (to, from) in new (LoadingPlace, LoadingPlace?)[] { (LoadingPlace.Village, LoadingPlace.Village), (LoadingPlace.Chapter2, null) })
            {
                var view = ChapterLoadingView.For(to, from, false);
                Assert.That(view.Leaving, Is.Null);
                Assert.That(view.SeamAt(0), Is.Zero);
            }
            Assert.That(ChapterSceneFade.PlaceOf(Chapter1RunHost.VillageScene), Is.EqualTo(LoadingPlace.Village));
            Assert.That(ChapterSceneFade.PlaceOf(Chapter1RunHost.ChapterScene), Is.EqualTo(LoadingPlace.Chapter1));
            Assert.That(ChapterSceneFade.PlaceOf(ChapterSceneFade.Chapter2Scene), Is.EqualTo(LoadingPlace.Chapter2));
            Assert.That(ChapterSceneFade.PlaceOf(Chapter1RunHost.TitleScene), Is.Null);
        }

        // After the sweep the place being left is hidden and the place entered is uncut.
        static void AssertClips(GameObject root, LoadingPlace from, LoadingPlace to)
        {
            UnityEngine.UI.RectMask2D Find(string name)
            {
                foreach (var clip in root.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true)) if (clip.name == name) return clip;
                return null;
            }
            var leftBack = Find(from + " back"); var enteredBack = Find(to + " back");
            Assert.That(leftBack, Is.Not.Null); Assert.That(enteredBack, Is.Not.Null);
            Assert.That(leftBack.gameObject.activeSelf, Is.False, $"{from} hidden after the sweep");
            Assert.That(enteredBack.gameObject.activeSelf, Is.True);
            Assert.That(enteredBack.padding, Is.EqualTo(Vector4.zero));
        }
    }
}
