using System;

namespace Deviant.Definitions
{
    [Serializable] public sealed class WindowLightData
    {
        public float angleDegrees, length, edgeFeather, beamOpacity, endFade;
    }
    public sealed class WindowLightDefinition
    {
        // XY world: 0 = right, +90 = up, -90 = down, relative to the window anchor.
        public float AngleDegrees { get; }
        public float Length { get; }
        public float EdgeFeather { get; }
        public float BeamOpacity { get; }
        public float EndFade { get; }
        public WindowLightDefinition(WindowLightData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            ValidateAngle(data.angleDegrees); LightingValidation.Positive(data.length, "length");
            LightingValidation.Nonnegative(data.edgeFeather, "edgeFeather"); LightingValidation.Unit(data.beamOpacity, "beamOpacity");
            LightingValidation.Positive(data.endFade, "endFade");
            if (data.endFade > data.length) throw new ArgumentException("Beam end fade must fit inside its length.");
            AngleDegrees = data.angleDegrees; Length = data.length; EdgeFeather = data.edgeFeather;
            BeamOpacity = data.beamOpacity; EndFade = data.endFade;
        }
        public static void ValidateAngle(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value) || value < -180 || value > 180) throw new ArgumentOutOfRangeException(nameof(value), "Use degrees in [-180, 180]."); }
    }
}
