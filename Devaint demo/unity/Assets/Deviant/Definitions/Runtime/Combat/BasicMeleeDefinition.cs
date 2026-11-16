using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Deviant.Combat;

namespace Deviant.Definitions
{
    // A single existing melee hit. Runtime DTOs returned to the legacy runner are
    // fresh copies; neither the authoring DTO nor another execution owns this box.
    public sealed class BasicMeleeDefinition
    {
        public string Id { get; }
        public string ContentHash { get; }
        public float Damage { get; }
        public float Duration { get; }
        public float Impact { get; }
        public float Active { get; }
        public float Width { get; }
        public float Height { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }

        BasicMeleeDefinition(string id, BasicData data)
        {
            Id = id; Damage = data.damage; Duration = data.duration; Impact = data.impact; Active = data.active;
            Width = data.box.width; Height = data.box.height; OffsetX = data.box.x; OffsetY = data.box.y;
            var canonical = new StringBuilder("basic-melee-v1\n").Append(Id).Append('\n');
            foreach (float value in new[] { Damage, Duration, Impact, Active, Width, Height, OffsetX, OffsetY })
                canonical.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            using (var hash = SHA256.Create())
                ContentHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())))
                    .Replace("-", "").ToLowerInvariant();
        }

        public BasicData ToData() => new BasicData {
            damage = Damage, duration = Duration, impact = Impact, active = Active,
            box = new BoxData { width = Width, height = Height, x = OffsetX, y = OffsetY }
        };

        public static bool TryCreate(string id, BasicData data, out BasicMeleeDefinition definition, out string error)
        {
            definition = null; error = null;
            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                error = "A single-line action id is required.";
            // JsonUtility can materialize absent inline DTOs as empty objects.
            // The legacy runner selects behavior by kind, not payload nullability.
            else if (data == null || data.box == null || !string.IsNullOrEmpty(data.kind))
                error = "This compiler requires a legacy melee basic hit with a box; ranged policies are not supported.";
            else if (!Finite(data.damage) || data.damage < 0 || !Finite(data.duration) || data.duration <= 0 ||
                !Finite(data.impact) || data.impact < 0 || !Finite(data.active) || data.active <= 0 ||
                !Finite(data.impact + data.active) || data.impact + data.active > data.duration)
                error = "Damage must be finite and nonnegative; a positive active window must fit inside a positive motion duration.";
            else if (!Finite(data.box.width) || data.box.width <= 0 || !Finite(data.box.height) || data.box.height <= 0 ||
                !Finite(data.box.x) || !Finite(data.box.y))
                error = "Hitbox dimensions must be finite and positive; offsets must be finite.";
            if (error != null) return false;
            definition = new BasicMeleeDefinition(id, data);
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
