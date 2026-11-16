using System;
using System.IO;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    // A presentation-only move. Anchor positions belong to the binding; this
    // definition never authorizes movement of a gameplay/physics actor.
    public sealed class StageMoveDefinition
    {
        public string Id { get; }
        public double Duration { get; }
        public int Facing { get; }
        public float Depth { get; }
        public string ContentHash { get; }
        public StageMoveDefinition(string id, double duration, int facing, float depth)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable stage move ID is required.", nameof(id));
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(facing));
            if (float.IsNaN(depth) || float.IsInfinity(depth)) throw new ArgumentOutOfRangeException(nameof(depth));
            Id = id; Duration = duration; Facing = facing; Depth = depth;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                { writer.Write(1); writer.Write(Id); writer.Write(Duration); writer.Write(Facing); writer.Write(Depth); }
                using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public double ProgressAt(double elapsed)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            return elapsed >= Duration ? 1 : elapsed / Duration;
        }
    }
}
