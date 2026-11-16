using System;
using System.Collections.Generic;

namespace Deviant.World.Contracts
{
    public readonly struct ChunkPlacement
    {
        public int Index { get; }
        public int ModuleIndex { get; }
        public float WorldX { get; }
        public float SourceX { get; }
        public ChunkPlacement(int index, int moduleIndex, float worldX, float sourceX)
        {
            if (index < 0 || moduleIndex < 0 || !Finite(worldX) || !Finite(sourceX)) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index; ModuleIndex = moduleIndex; WorldX = worldX; SourceX = sourceX;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    // Immutable resolved view plan; not chapter content or mutable world state.
    public sealed class StreamingWindow
    {
        public IReadOnlyList<ChunkPlacement> Placements { get; }
        public StreamingWindow(ChunkPlacement[] placements)
        {
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            var indices = new HashSet<int>();
            foreach (var placement in placements)
                if (!indices.Add(placement.Index)) throw new ArgumentException("Duplicate physical chunk index.", nameof(placements));
            Placements = Array.AsReadOnly((ChunkPlacement[])placements.Clone());
        }
        public bool Contains(int index)
        {
            foreach (var placement in Placements) if (placement.Index == index) return true;
            return false;
        }
    }
}
