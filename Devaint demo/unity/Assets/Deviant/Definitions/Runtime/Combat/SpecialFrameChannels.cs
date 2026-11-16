using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Definitions
{
    // Read-only authoring channel inventory. No data-driven runtime operation,
    // endpoint callback or policy string is executed through these names.
    public static class SpecialFrameChannels
    {
        public static IReadOnlyList<string> Required(ActionSpecialFrameDefinition action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return action.NumericAuthored != null ? ActionSpecialFrameCompiler.MarkerIds(action.NumericAuthored) :
                Array.AsReadOnly(action.NumericFinite.ToDocument().timeline.markers.Select(marker => marker.id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
        public static string Epoch(string group, string field)
        {
            if (group == "hit" && (field == "root" || field == "stun" || field == "pushSeconds")) return "accepted target hit";
            if (group == "movement" && field == "wallRecovery") return "actual wall or primary boss movement end";
            if (group == "movement" && field == "recastWindow") return "accepted marked-target hit";
            if (group == "effect" && field == "duration") return "actual resource-committed activation (smoke retains existing action activation offset)";
            return "existing fixed policy; no implicit body-weight scaling";
        }
    }
}
