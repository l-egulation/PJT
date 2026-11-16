using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Deviant.World.Contracts
{
    // Repetition addresses terrain views, never combat-map regions or entities.
    public sealed class LayoutRepetition
    {
        public float Width { get; }
        public string ModulePrefix { get; }
        public IReadOnlyList<string> TemplateIds { get; }
        public LayoutRepetition(float width, string modulePrefix, string[] templateIds)
        {
            LayoutValues.Finite(width);
            if (width <= 0 || templateIds == null || templateIds.Length == 0) throw new ArgumentException("An explicit positive repeat width and template order are required.");
            Width = width; ModulePrefix = LayoutValues.Id(modulePrefix);
            TemplateIds = Array.AsReadOnly(templateIds.Select(LayoutValues.Id).ToArray());
            if (TemplateIds.Distinct(StringComparer.Ordinal).Count() != TemplateIds.Count) throw new ArgumentException("Repeat template IDs must be distinct.");
        }
        public string ModuleId(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return ModulePrefix + index.ToString(CultureInfo.InvariantCulture);
        }
        public bool TryIndex(string moduleId, out int index)
        {
            index = -1;
            if (moduleId == null || !moduleId.StartsWith(ModulePrefix, StringComparison.Ordinal)) return false;
            string number = moduleId.Substring(ModulePrefix.Length);
            return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0 &&
                moduleId == ModuleId(index) && !float.IsInfinity(index * Width);
        }
    }
}
