using System;
using System.IO;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    // Presentation-only transition. Product tint, timing and retention are always
    // explicit; completing this definition never requests a scene/run change.
    public sealed class FadeDefinition
    {
        public string Id { get; }
        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public float FromOpacity { get; }
        public float ToOpacity { get; }
        public double Duration { get; }
        public bool HoldAtEnd { get; }
        public string ContentHash { get; }
        public FadeDefinition(string id, float red, float green, float blue, float fromOpacity, float toOpacity, double duration, bool holdAtEnd)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable fade ID is required.", nameof(id));
            Id = id; Red = Unit(red); Green = Unit(green); Blue = Unit(blue); FromOpacity = Unit(fromOpacity); ToOpacity = Unit(toOpacity);
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            Duration = duration; HoldAtEnd = holdAtEnd;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                { writer.Write(1); writer.Write(Id); writer.Write(Red); writer.Write(Green); writer.Write(Blue); writer.Write(FromOpacity); writer.Write(ToOpacity); writer.Write(Duration); writer.Write(HoldAtEnd); }
                using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public float OpacityAt(double elapsed)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (elapsed >= Duration) return ToOpacity;
            return (float)(FromOpacity + (ToOpacity - FromOpacity) * (elapsed / Duration));
        }
        static float Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
