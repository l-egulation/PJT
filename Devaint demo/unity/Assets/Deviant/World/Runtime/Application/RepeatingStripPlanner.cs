using System;
using Deviant.World.Contracts;

namespace Deviant.World.Streaming
{
    // Compatibility policy for the existing forest prototype. A future chapter
    // provider supplies resolved module placements instead of adopting this loop.
    public sealed class RepeatingStripPlanner
    {
        readonly float width;
        public float ModuleWidth => width;
        readonly int moduleCount, behind, ahead;
        int cachedIndex = -1;
        StreamingWindow cached;
        public RepeatingStripPlanner(float width, int moduleCount, int behind, int ahead)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (moduleCount <= 0 || behind < 0 || ahead < 0 || (long)behind + ahead + 1 > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(moduleCount));
            if (float.IsInfinity(width * moduleCount)) throw new ArgumentOutOfRangeException(nameof(width));
            this.width = width; this.moduleCount = moduleCount; this.behind = behind; this.ahead = ahead;
        }
        public StreamingWindow Plan(float targetX)
        {
            if (float.IsNaN(targetX) || float.IsInfinity(targetX)) throw new ArgumentOutOfRangeException(nameof(targetX));
            // Match the existing float division before floor; no negative chunks.
            double current = Math.Max(0, Math.Floor(targetX / width));
            if (current > int.MaxValue - ahead) throw new ArgumentOutOfRangeException(nameof(targetX));
            int index = (int)current;
            if (index == cachedIndex) return cached;
            int first = Math.Max(0, index - behind), last = index + ahead;
            var placements = new ChunkPlacement[last - first + 1];
            for (int i = 0; i < placements.Length; i++) placements[i] = Place(first + i);
            var window = new StreamingWindow(placements);
            cachedIndex = index; cached = window;
            return window;
        }
        public ChunkPlacement Place(int index)
        {
            if (index < 0 || float.IsInfinity(index * width)) throw new ArgumentOutOfRangeException(nameof(index));
            int module = index % moduleCount;
            return new ChunkPlacement(index, module, index * width, module * width);
        }
    }
}
