using System;
using Deviant.Combat;

namespace Deviant.Definitions
{
    [Serializable]
    public sealed class BasicMeleeDocument
    {
        public int schemaVersion;
        public string id;
        public BasicMeleeHitData hit;
    }

    [Serializable]
    public sealed class BasicMeleeHitData
    {
        public float damage, duration, impact, active;
        public BoxData box;

        public BasicData ToData() => new BasicData {
            damage = damage, duration = duration, impact = impact, active = active,
            box = box == null ? null : new BoxData { width = box.width, height = box.height, x = box.x, y = box.y }
        };

        public static BasicMeleeHitData From(BasicMeleeDefinition definition) => new BasicMeleeHitData {
            damage = definition.Damage, duration = definition.Duration, impact = definition.Impact, active = definition.Active,
            box = new BoxData { width = definition.Width, height = definition.Height, x = definition.OffsetX, y = definition.OffsetY }
        };
    }
}
