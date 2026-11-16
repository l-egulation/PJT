using System;
using System.Collections.Generic;

namespace Deviant.Definitions
{
    [Serializable] public sealed class LightingCatalogData
    {
        public int schema;
        public LightingProfileData[] profiles;
        public LightEmitterData[] emitters;
        public PendulumData pendulum;
        public HangingConnectorData suspension;
        public WindowLightData windowBeam;
        public LightingZoneData[] zones;
    }
    [Serializable] public sealed class LightingProfileData { public string id; public float[] color; public float intensity; }
    [Serializable] public sealed class LightEmitterData
    {
        public string id;
        public float[] color;
        public float intensity, radius, normalDistance, shadowStrength, flickerAmplitude, flickerPeriod;
        public float shadowSoftness, shadowFalloff, lightFalloff;
    }
    [Serializable] public sealed class PendulumData
    {
        public float length, gravity, damping, maxAngleDegrees, maxAngularSpeed, bodyImpulseScale, attackImpulse;
    }

    public readonly struct LightColor
    {
        public readonly float R, G, B;
        public LightColor(float r, float g, float b)
        {
            LightingValidation.Nonnegative(r, nameof(r)); LightingValidation.Nonnegative(g, nameof(g)); LightingValidation.Nonnegative(b, nameof(b));
            R = r; G = g; B = b;
        }
        public static LightColor Lerp(LightColor a, LightColor b, float t)
            => new LightColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }
    public sealed class LightingProfile
    {
        public string Id { get; }
        public LightColor Color { get; }
        public float Intensity { get; }
        public LightingProfile(LightingProfileData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Id = LightingValidation.Id(data.id); Color = LightingValidation.Color(data.color);
            LightingValidation.Nonnegative(data.intensity, "intensity"); Intensity = data.intensity;
        }
    }
    public sealed class LightEmitterDefinition
    {
        public string Id { get; }
        public LightColor Color { get; }
        public float Intensity { get; }
        public float Radius { get; }
        public float NormalDistance { get; }
        public float ShadowStrength { get; }
        public float ShadowSoftness { get; }
        public float ShadowFalloff { get; }
        public float LightFalloff { get; }
        public float FlickerAmplitude { get; }
        public float FlickerPeriod { get; }
        public LightEmitterDefinition(LightEmitterData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Id = LightingValidation.Id(data.id); Color = LightingValidation.Color(data.color);
            LightingValidation.Nonnegative(data.intensity, "intensity"); LightingValidation.Positive(data.radius, "radius");
            LightingValidation.Positive(data.normalDistance, "normalDistance"); LightingValidation.Unit(data.shadowStrength, "shadowStrength");
            LightingValidation.Unit(data.shadowSoftness, "shadowSoftness"); LightingValidation.Unit(data.shadowFalloff, "shadowFalloff"); LightingValidation.Unit(data.lightFalloff, "lightFalloff");
            LightingValidation.Unit(data.flickerAmplitude, "flickerAmplitude"); LightingValidation.Positive(data.flickerPeriod, "flickerPeriod");
            Intensity = data.intensity; Radius = data.radius; NormalDistance = data.normalDistance;
            ShadowStrength = data.shadowStrength; FlickerAmplitude = data.flickerAmplitude; FlickerPeriod = data.flickerPeriod;
            ShadowSoftness = data.shadowSoftness; ShadowFalloff = data.shadowFalloff; LightFalloff = data.lightFalloff;
        }
        // Stateless, seeded decoration. Never consumes the combat/global random stream.
        public float IntensityAt(double seconds, uint seed)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            double phase = (seed % 4096) * (Math.PI * 2 / 4096);
            return Intensity * (1 + FlickerAmplitude * (float)Math.Sin(seconds * Math.PI * 2 / FlickerPeriod + phase));
        }
    }
    public sealed class PendulumDefinition
    {
        public float Length { get; }
        public float Gravity { get; }
        public float Damping { get; }
        public float MaxAngle { get; }
        public float MaxAngularSpeed { get; }
        public float BodyImpulseScale { get; }
        public float AttackImpulse { get; }
        public PendulumDefinition(PendulumData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            LightingValidation.Positive(data.length, "length"); LightingValidation.Positive(data.gravity, "gravity");
            LightingValidation.Nonnegative(data.damping, "damping"); LightingValidation.Positive(data.maxAngularSpeed, "maxAngularSpeed");
            LightingValidation.Positive(data.maxAngleDegrees, "maxAngleDegrees");
            if (data.maxAngleDegrees >= 90) throw new ArgumentOutOfRangeException("maxAngleDegrees");
            LightingValidation.Nonnegative(data.bodyImpulseScale, "bodyImpulseScale"); LightingValidation.Nonnegative(data.attackImpulse, "attackImpulse");
            Length = data.length; Gravity = data.gravity; Damping = data.damping; MaxAngle = data.maxAngleDegrees * (float)Math.PI / 180;
            MaxAngularSpeed = data.maxAngularSpeed; BodyImpulseScale = data.bodyImpulseScale; AttackImpulse = data.attackImpulse;
        }
    }
    public sealed class LightingCatalog
    {
        readonly Dictionary<string, LightingProfile> profiles = new Dictionary<string, LightingProfile>(StringComparer.Ordinal);
        readonly Dictionary<string, LightEmitterDefinition> emitters = new Dictionary<string, LightEmitterDefinition>(StringComparer.Ordinal);
        public PendulumDefinition Pendulum { get; }
        public HangingConnectorDefinition Suspension { get; }
        public WindowLightDefinition WindowBeam { get; }
        public IReadOnlyList<LightingZoneDefinition> Zones { get; }
        public LightingCatalog(LightingCatalogData data)
        {
            if (data == null || data.schema != 5 || data.profiles == null || data.profiles.Length == 0 || data.emitters == null || data.emitters.Length == 0 || data.zones == null)
                throw new ArgumentException("A complete schema 5 lighting catalog is required, including an explicit zones array.");
            foreach (var input in data.profiles) { var definition = new LightingProfile(input); profiles.Add(definition.Id, definition); }
            foreach (var input in data.emitters) { var definition = new LightEmitterDefinition(input); emitters.Add(definition.Id, definition); }
            Pendulum = new PendulumDefinition(data.pendulum);
            Suspension = new HangingConnectorDefinition(data.suspension);
            WindowBeam = new WindowLightDefinition(data.windowBeam);
            var zones = new LightingZoneDefinition[data.zones.Length]; var zoneIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = new LightingZoneDefinition(data.zones[i]);
                if (!zoneIds.Add(zone.Id) || !profiles.ContainsKey(zone.ProfileId)) throw new ArgumentException("Zone IDs must be unique and reference an existing lighting profile.");
                zones[i] = zone;
            }
            Zones = Array.AsReadOnly(zones);
        }
        public LightingProfile Profile(string id) => profiles[id];
        public LightEmitterDefinition Emitter(string id) => emitters[id];
    }
    public static class LightingValidation
    {
        public static string Id(string value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A stable lighting ID is required.");
        public static void Nonnegative(float value, string name)
        { Finite(value, name); if (value < 0) throw new ArgumentOutOfRangeException(name); }
        public static void Finite(float value, string name)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        public static void Positive(float value, string name) { Nonnegative(value, name); if (value == 0) throw new ArgumentOutOfRangeException(name); }
        public static void Unit(float value, string name) { Nonnegative(value, name); if (value > 1) throw new ArgumentOutOfRangeException(name); }
        public static LightColor Color(float[] value)
        { if (value == null || value.Length != 3) throw new ArgumentException("An RGB lighting color is required."); return new LightColor(value[0], value[1], value[2]); }
    }
}
