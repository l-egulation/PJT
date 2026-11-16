using System;

namespace Deviant.Definitions
{
    public enum HangingConnectorStyle { Chain, Rope }
    [Serializable] public sealed class HangingConnectorData
    {
        public string style;
        public int segments;
        public float linkPitch, chainWidth, ropeWidth, linkOverlap, maxBend, lagScale, response;
    }
    // Presentation tuning only. No chain link mass, physics joints or combat forces.
    public sealed class HangingConnectorDefinition
    {
        public HangingConnectorStyle Style { get; }
        public int Segments { get; }
        public float LinkPitch { get; }
        public float ChainWidth { get; }
        public float RopeWidth { get; }
        public float LinkOverlap { get; }
        public float MaxBend { get; }
        public float LagScale { get; }
        public float Response { get; }
        public HangingConnectorDefinition(HangingConnectorData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            switch (data.style)
            {
                case "chain": Style = HangingConnectorStyle.Chain; break;
                case "rope": Style = HangingConnectorStyle.Rope; break;
                default: throw new ArgumentException("Connector style must be chain or rope.");
            }
            if (data.segments < 4 || data.segments > 64) throw new ArgumentOutOfRangeException("segments");
            LightingValidation.Positive(data.linkPitch, "linkPitch"); LightingValidation.Positive(data.chainWidth, "chainWidth");
            LightingValidation.Positive(data.ropeWidth, "ropeWidth"); LightingValidation.Nonnegative(data.maxBend, "maxBend");
            LightingValidation.Nonnegative(data.lagScale, "lagScale"); LightingValidation.Positive(data.response, "response");
            LightingValidation.Positive(data.linkOverlap, "linkOverlap");
            if (data.linkOverlap < 1 || data.linkOverlap > 2) throw new ArgumentOutOfRangeException("linkOverlap");
            Segments = data.segments; LinkPitch = data.linkPitch; ChainWidth = data.chainWidth;
            RopeWidth = data.ropeWidth; LinkOverlap = data.linkOverlap; MaxBend = data.maxBend; LagScale = data.lagScale; Response = data.response;
        }
    }
}
