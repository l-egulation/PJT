using System.Diagnostics;

namespace ForestCombatV2
{
    // A random chapter is composed under the loading screen (ChapterSceneFade). Composing it in one frame froze the hero
    // running in the loading window for 2.4 s into chapter 1 and 1.5 s into chapter 2 (profiled 2026-11-16), so the long
    // builds go step by step and start a new frame once the current one has spent its share. A single step still runs
    // whole: only the gaps between steps become frame boundaries.
    public sealed class FrameSlice
    {
        // Build time per frame. With the loading window's own drawing the screen keeps about 30 frames a second, smooth
        // for the hero's run cycle and the sliding place; a smaller share would add frames and lengthen the load.
        public const double DefaultMilliseconds = 20;
        readonly Stopwatch watch = Stopwatch.StartNew();
        readonly double milliseconds;
        public FrameSlice(double milliseconds = DefaultMilliseconds) { this.milliseconds = milliseconds; }
        // This frame's share is spent: the caller yields one frame, then calls Restart.
        public bool Due => watch.Elapsed.TotalMilliseconds >= milliseconds;
        public void Restart() => watch.Restart();
    }

    // A presentation on a ForestCombatWorld's object that builds over frames once the world is Composed. The world
    // answers Ready only after every enabled one has Settled (built, or given up after an error it logged), so the
    // loading screen stays up over the whole build and the run starts on the finished picture.
    public interface IStagedWorldPresentation
    {
        bool Settled { get; }
    }
}
