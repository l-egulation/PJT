using System;
using System.Collections.Generic;
using System.Globalization;
using Deviant.Combat;

namespace Deviant.Definitions
{
    // Immutable melee basics from the existing catalog. Non-melee policies keep
    // their own runners; authoring publishes a replacement without editing inputs.
    public sealed class BasicMeleeCatalog
    {
        readonly Dictionary<string, BasicMeleeDefinition> actions;
        public IReadOnlyList<BasicMeleeDefinition> Actions { get; }
        BasicMeleeCatalog(Dictionary<string, BasicMeleeDefinition> actions)
        {
            this.actions = actions;
            var ordered = new List<BasicMeleeDefinition>(actions.Values);
            ordered.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            Actions = ordered.AsReadOnly();
        }
        public BasicMeleeDefinition Find(string actionId) => actionId != null && actions.TryGetValue(actionId, out var action) ? action : null;
        public BasicMeleeDefinition Find(string formId, int hitIndex)
        {
            if (string.IsNullOrWhiteSpace(formId) || hitIndex < 0) return null;
            return actions.TryGetValue(formId + "_basic_" + hitIndex.ToString(CultureInfo.InvariantCulture), out var action) ? action : null;
        }
        public bool TryReplace(IReadOnlyList<BasicMeleeDefinition> replacements, out BasicMeleeCatalog catalog, out string error)
        {
            catalog = null; error = null;
            if (replacements == null || replacements.Count == 0)
            { error = "At least one compiled melee basic replacement is required."; return false; }
            for (int i = 0; i < replacements.Count; i++)
            {
                var action = replacements[i];
                if (action == null || !actions.ContainsKey(action.Id))
                { error = "Every replacement must identify an existing melee basic hit."; return false; }
                for (int previous = 0; previous < i; previous++)
                    if (replacements[previous].Id == action.Id)
                    { error = "Duplicate melee basic replacement: " + action.Id + "."; return false; }
            }
            var next = new Dictionary<string, BasicMeleeDefinition>(actions, StringComparer.Ordinal);
            for (int i = 0; i < replacements.Count; i++) next[replacements[i].Id] = replacements[i];
            catalog = new BasicMeleeCatalog(next);
            return true;
        }
        public static bool TryCreate(Catalog source, out BasicMeleeCatalog catalog, out string error)
        {
            catalog = null; error = null;
            if (source?.forms == null) { error = "A combat form catalog is required."; return false; }
            var actions = new Dictionary<string, BasicMeleeDefinition>(StringComparer.Ordinal);
            var formIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var form in source.forms)
            {
                if (form == null || string.IsNullOrWhiteSpace(form.id) || !formIds.Add(form.id) || form.basics == null)
                { error = "Forms require unique identities and a basic attack list."; return false; }
                for (int index = 0; index < form.basics.Length; index++)
                {
                    var data = form.basics[index];
                    if (data == null) { error = "Basic attack is missing: " + form.id + "/" + index + "."; return false; }
                    if (!string.IsNullOrEmpty(data.kind)) continue;
                    string id = form.id + "_basic_" + index.ToString(CultureInfo.InvariantCulture);
                    if (!BasicMeleeDefinition.TryCreate(id, data, out var action, out var actionError))
                    { error = id + ": " + actionError; return false; }
                    actions.Add(id, action);
                }
            }
            catalog = new BasicMeleeCatalog(actions); return true;
        }
    }
}
