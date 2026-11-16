using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;

namespace Deviant.Definitions
{
    public sealed class RangedActionCatalog
    {
        public static IReadOnlyList<string> SupportedIds { get; } = Array.AsReadOnly(new[] {
            "launcher_basic_0", "launcher_basic_special", "launcher_range_special", "launcher_move_special", "launcher_followup_special", "bomb_basic_0",
            "bomb_basic_special", "bomb_range_special", "bomb_move_special", "bomb_followup_special", "shuriken_basic_0", "shuriken_basic_1",
            "shuriken_basic_special", "shuriken_range_special", "shuriken_move_special", "shuriken_followup_special" });
        readonly Dictionary<string, RangedActionDefinition> entries;
        public IReadOnlyList<RangedActionDefinition> Actions { get; }
        RangedActionCatalog(Dictionary<string, RangedActionDefinition> entries)
        {
            this.entries = entries; ValidateGroups(entries);
            Actions = Array.AsReadOnly(SupportedIds.Select(id => entries[id]).ToArray());
        }
        public RangedActionDefinition Read(string id) => id != null && entries.TryGetValue(id, out var value) ? value : null;
        public RangedActionDefinition Basic(string form, int index) => Read(form + "_basic_" + index);
        public RangedActionSnapshotData[] ToSnapshot() => Actions.Select(action=>action.ToSnapshot()).ToArray();
        public static RangedActionCatalog FromSnapshot(IReadOnlyList<RangedActionSnapshotData> snapshots)
        {
            if(snapshots==null||snapshots.Count!=SupportedIds.Count)throw new ArgumentException("All sixteen complete ranged snapshots are required.");
            var values=new Dictionary<string,RangedActionDefinition>(StringComparer.Ordinal);
            foreach(var snapshot in snapshots){var action=RangedActionDefinition.FromSnapshot(snapshot);if(values.ContainsKey(action.Id))throw new ArgumentException("Duplicate ranged snapshot.");values.Add(action.Id,action);}
            return new RangedActionCatalog(values);
        }
        public bool TryReplace(IReadOnlyList<RangedActionDefinition> candidate, out RangedActionCatalog next, out string error)
        {
            next = null; error = null;
            try
            {
                if (candidate == null || candidate.Count != SupportedIds.Count) throw new ArgumentException("The complete sixteen-action ranged candidate is required.");
                var values = new Dictionary<string, RangedActionDefinition>(StringComparer.Ordinal);
                foreach (var action in candidate)
                {
                    if (action == null || action.IsStartSnapshot || !entries.TryGetValue(action.Id, out var previous) || previous.SourceVersion != action.SourceVersion || values.ContainsKey(action.Id))
                        throw new ArgumentException("A unique complete candidate retaining current ranged identity/resource/policy is required.");
                    values.Add(action.Id, action);
                }
                next = new RangedActionCatalog(values); return true;
            }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        public static bool TryCreate(Catalog catalog, out RangedActionCatalog result, out string error)
        {
            result = null; error = null;
            try
            {
                if (catalog?.forms == null) throw new ArgumentException("A generated combat catalog is required.");
                var values = new Dictionary<string, RangedActionDefinition>(StringComparer.Ordinal);
                foreach (string formId in new[] { "launcher", "bomb", "shuriken" })
                {
                    var matches = catalog.forms.Where(form => form != null && form.id == formId).ToArray();
                    if (matches.Length != 1) throw new ArgumentException("One approved ranged form is required: " + formId);
                    var form = matches[0];
                    if (form.specials == null || form.specials.Length != 4 || form.basics == null || form.basics.Length != (formId == "shuriken" ? 2 : 1))
                        throw new ArgumentException("The existing ranged basic/special inventory must be retained: " + formId);
                    // Every ranged form, launcher included, has its basics plus one D skill per ink slot.
                    for (int i = 0; i < form.basics.Length; i++) { string id = formId + "_basic_" + i; values.Add(id, new RangedActionDefinition(id, formId, i, form.basics[i], null, form.inputSpeed)); }
                    for (int i = 0; i < 4; i++)
                    {
                        string id = formId + "_" + new[] { "basic", "range", "move", "followup" }[i] + "_special";
                        if (form.specials[i]?.id != id) throw new ArgumentException("Ranged special slot identity changed: " + id);
                        values.Add(id, new RangedActionDefinition(id, formId, i, null, form.specials[i], form.inputSpeed));
                    }
                }
                result = new RangedActionCatalog(values); return true;
            }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        public RangedActionCatalog Edit(string id, RangedActionNumericData numeric)
        {
            var selected = Read(id) ?? throw new ArgumentException("Unknown ranged action: " + id);
            var edits = Actions.ToDictionary(action => action.Id, action => action, StringComparer.Ordinal);
            var changed = selected.Recompile(numeric); edits[id] = changed;
            if (id == "shuriken_basic_0" || id == "shuriken_basic_1")
            {
                string other = id == "shuriken_basic_0" ? "shuriken_basic_1" : "shuriken_basic_0";
                var n = edits[other].ToNumericData(); n.ranged = changed.ToNumericData().ranged; edits[other] = edits[other].Recompile(n);
            }
            var after = changed.ToNumericData().bomb;
            if (id == "bomb_basic_special")
            {
                var basic = edits["bomb_basic_0"].ToNumericData().bomb;
                if(after.speed!=basic.speed||after.gravity!=basic.gravity||after.radius!=basic.radius||after.lifetime!=basic.lifetime||after.spawnHeight!=basic.spawnHeight||after.spawnGap!=basic.spawnGap)
                    throw new ArgumentException("Edit inherited burst ballistics/defaults once under bomb_basic_0.");
            }
            if (id == "bomb_basic_0")
            {
                foreach (string dependent in new[] { "bomb_basic_special", "bomb_range_special", "bomb_followup_special" })
                {
                    var n = edits[dependent].ToNumericData();
                    n.bomb.spawnHeight=after.spawnHeight;n.bomb.spawnGap=after.spawnGap;
                    if (dependent != "bomb_followup_special") n.bomb.lifetime=after.lifetime;
                    if (dependent == "bomb_basic_special") { n.bomb.speed=after.speed;n.bomb.gravity=after.gravity;n.bomb.radius=after.radius; }
                    if (dependent == "bomb_range_special") n.bomb.gravity=after.gravity;
                    edits[dependent]=edits[dependent].Recompile(n);
                }
            }
            if (!TryReplace(Actions.Select(action=>edits[action.Id]).ToArray(),out var result,out var error)) throw new ArgumentException(error);
            return result;
        }
        static void ValidateGroups(Dictionary<string, RangedActionDefinition> actions)
        {
            if (actions.Count != SupportedIds.Count || SupportedIds.Any(id => !actions.ContainsKey(id))) throw new ArgumentException("All sixteen supported actions are required.");
            var a = actions["shuriken_basic_0"].ToNumericData().ranged; var b = actions["shuriken_basic_1"].ToNumericData().ranged;
            if (a.speed != b.speed || a.distance != b.distance || a.radius != b.radius || a.spawnX != b.spawnX || a.spawnHeight != b.spawnHeight ||
                a.homingTurnRate != b.homingTurnRate || a.homingCone != b.homingCone || a.homingMaxAngle != b.homingMaxAngle)
                throw new ArgumentException("The two shuriken basics share one canonical flight/spawn group.");
            var basic = actions["bomb_basic_0"].ToNumericData().bomb; var burst = actions["bomb_basic_special"].ToNumericData().bomb;
            var area = actions["bomb_range_special"].ToNumericData().bomb; var timed = actions["bomb_followup_special"].ToNumericData().bomb;
            if (basic.speed != burst.speed || basic.gravity != burst.gravity || basic.radius != burst.radius || basic.lifetime != burst.lifetime ||
                basic.gravity != area.gravity || basic.lifetime != area.lifetime || basic.spawnHeight != burst.spawnHeight || basic.spawnGap != burst.spawnGap ||
                basic.spawnHeight != area.spawnHeight || basic.spawnGap != area.spawnGap || basic.spawnHeight != timed.spawnHeight || basic.spawnGap != timed.spawnGap)
                throw new ArgumentException("Inherited bomb ballistic/default source groups must remain coherent.");
        }
    }
}
