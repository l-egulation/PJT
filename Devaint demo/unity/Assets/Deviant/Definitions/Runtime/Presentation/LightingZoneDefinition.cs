using System;

namespace Deviant.Definitions
{
    [Serializable] public sealed class LightingZoneData
    {
        public string id, profile;
        public float[] bounds;
        public int priority;
        public float blendWidth, exitHysteresis, transitionSeconds;
    }
    public sealed class LightingZoneDefinition
    {
        public string Id { get; }
        public string ProfileId { get; }
        public float Left { get; }
        public float Bottom { get; }
        public float Right { get; }
        public float Top { get; }
        public int Priority { get; }
        public float BlendWidth { get; }
        public float ExitHysteresis { get; }
        public float TransitionSeconds { get; }
        public LightingZoneDefinition(LightingZoneData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Id = LightingValidation.Id(data.id); ProfileId = LightingValidation.Id(data.profile);
            if (data.bounds == null || data.bounds.Length != 4) throw new ArgumentException("Zone bounds are [left, bottom, right, top].");
            foreach (float value in data.bounds) LightingValidation.Finite(value, nameof(data.bounds));
            Left = data.bounds[0]; Bottom = data.bounds[1]; Right = data.bounds[2]; Top = data.bounds[3];
            if (Right <= Left || Top <= Bottom) throw new ArgumentException("Zone bounds need positive width and height.");
            LightingValidation.Positive(data.blendWidth, nameof(data.blendWidth));
            if ((double)data.blendWidth * 2 > (double)Right - Left || (double)data.blendWidth * 2 > (double)Top - Bottom)
                throw new ArgumentException("The inward blend width must fit inside the zone.");
            LightingValidation.Nonnegative(data.exitHysteresis, nameof(data.exitHysteresis));
            LightingValidation.Nonnegative(data.transitionSeconds, nameof(data.transitionSeconds));
            Priority = data.priority; BlendWidth = data.blendWidth; ExitHysteresis = data.exitHysteresis; TransitionSeconds = data.transitionSeconds;
        }
    }
}
