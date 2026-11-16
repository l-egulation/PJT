using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Deviant.Combat;
using Deviant.Definitions;

namespace Deviant.Authoring
{
    // Only the existing two charge policies; no generic timeline interpretation.
    // C# compiles both source and edits. Python transports the unchanged schema.
    public static class ChargedMeleeSource
    {
        public const string RelativePath = BasicMeleeSource.RelativePath;
        public const int MaximumFileBytes = BasicMeleeSource.MaximumFileBytes;
        public static ChargedMeleeDefinition[] Load(string repository, out string fileHash)
            => Compile(BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(repository, RelativePath), out fileHash, MaximumFileBytes)));
        public static string Save(string repository, string expectedHash, IReadOnlyList<ChargedMeleeDefinition> candidates)
        {
            string path = Path.Combine(repository, RelativePath); var root = BasicMeleeSource.Parse(SourceFiles.Read(path, out var hash, MaximumFileBytes));
            if (hash != expectedHash) throw new IOException("Source changed outside this window. Reload before saving; no file was overwritten.");
            var original = Compile(root);
            if (candidates == null || candidates.Count == 0 || candidates.Count > original.Length) throw new ArgumentException("A nonempty subset of complete charged numeric candidates is required.");
            var selected = new Dictionary<string, ChargedMeleeDefinition>(StringComparer.Ordinal);
            foreach (var candidate in candidates) {
                if (candidate == null || selected.ContainsKey(candidate.Id)) throw new ArgumentException("Charged candidates require unique existing IDs.");
                var source = original.SingleOrDefault(value => value.Id == candidate.Id);
                if (source == null || source.SourceVersion != candidate.SourceVersion || source.Recompile(candidate.ToNumericData()).ContentHash != candidate.ContentHash)
                    throw new ArgumentException("Charged identity/resource metadata changed since this candidate was read. Reload before saving.");
                selected.Add(candidate.Id, candidate);
            }
            foreach (var form in Items(root, "forms")) foreach (var node in Items(form, "specials")) {
                string id = Text(node, "id"); if (!selected.TryGetValue(id, out var value)) continue;
                var numeric = value.ToNumericData(); var hit = Items(node, "hits").Single(); var charge = Member(node, "charge");
                var before = original.Single(item => item.Id == id).ToNumericData();
                Set(Member(node, "activation_time"), numeric.minimum); Set(Member(node, "motion_end"), numeric.baseEnd);
                Set(Member(hit, "time"), numeric.minimum); Set(Member(hit, "active"), numeric.active); Set(Member(hit, "damage"), numeric.minimumDamage);
                Set(Member(charge, "min_hold"), numeric.minimum); Set(Member(charge, "max_hold"), numeric.maximum);
                Set(Member(charge, "min_damage"), numeric.minimumDamage); Set(Member(charge, "max_damage"), numeric.maximumDamage); Set(Member(charge, "active"), numeric.active);
                // Read-only annotations follow the same C# policy used at runtime,
                // never a second Python release/end compiler.
                if (numeric.baseEnd != before.baseEnd || numeric.minimum != before.minimum || numeric.active != before.active)
                    Set(Member(charge, "recovery"), numeric.baseEnd - (numeric.minimum + numeric.active));
                if (numeric.baseEnd != before.baseEnd || numeric.minimum != before.minimum || numeric.maximum != before.maximum)
                    Set(Member(charge, "max_motion"), value.Policy.Resolve(value.Policy.Maximum).MotionEnd);
                var shape = Member(hit, "shape"); var size = Pair(shape, "size"); var right = Pair(shape, "right_offset"); var left = Pair(shape, "left_offset"); var box = numeric.box;
                Set(size[0], box.width); Set(size[1], box.height); Set(right[0], box.x); Set(right[1], box.y); Set(left[0], -box.x); Set(left[1], box.y);
                var reach = Optional(shape, "reach_from_body_front"); if (reach != null) Set(reach, box.x + box.width / 2 - .65625f);
                var bottom = Optional(shape, "bottom"); if (bottom != null) Set(bottom, box.y - box.height / 2);
            }
            Compile(root); return SourceFiles.Save(path, expectedHash, BasicMeleeSource.Serialize(root), MaximumFileBytes);
        }
        public static void ValidateGenerated(string repository, Catalog generated)
        {
            if (generated?.forms == null) throw new ArgumentException("Generated combat catalog is required.");
            foreach (var source in Load(repository, out _)) {
                var matches = generated.forms.SelectMany(form => form.specials ?? Array.Empty<SkillData>()).Where(value => value != null && value.id == source.Id).ToArray();
                if (matches.Length != 1 || !ChargedMeleeDefinition.TryCreate(source.FormId, matches[0], out var compiled, out _) || compiled.ContentHash != source.ContentHash)
                    throw new ArgumentException("Generated charged action differs from its canonical source: " + source.Id + ". Export the combat catalog before building.");
            }
        }
        static ChargedMeleeDefinition[] Compile(XElement root)
        {
            var result = new Dictionary<string, ChargedMeleeDefinition>(StringComparer.Ordinal);
            foreach (var form in Items(root, "forms")) foreach (var node in Items(form, "specials")) {
                string id = Text(node, "id"); if (!ChargedMeleeDefinition.SupportedIds.Contains(id)) continue;
                var source = Transport(node);
                if (result.ContainsKey(id) || !ChargedMeleeDefinition.TryCreate(Text(form, "id"), source, out var compiled, out _)) throw new ArgumentException("Invalid or duplicate canonical charged action: " + id);
                result.Add(id, compiled);
            }
            if (result.Count != ChargedMeleeDefinition.SupportedIds.Count) throw new ArgumentException("Every supported charged action is required.");
            return ChargedMeleeDefinition.SupportedIds.Select(id => result[id]).ToArray();
        }
        static SkillData Transport(XElement node)
        {
            var hits = Items(node, "hits");
            if (hits.Length != 1 || !Null(node, "movement") || !Null(node, "independent_effect") || Items(node, "invulnerability").Length != 0 || Optional(node, "action_tracks") != null || Optional(node, "charges") != null)
                throw new ArgumentException("These charged actions retain one area hit with no movement/effect/defense track.");
            var hit = hits[0]; var charge = Member(node, "charge"); var control = Member(hit, "control");
            if (control.HasElements || Number(charge, "active") != Number(hit, "active")) throw new ArgumentException("Charged active timing must agree with its single existing hit and have no control effect.");
            var shape = Member(hit, "shape"); if (Text(shape, "kind") != "box") throw new ArgumentException("The existing charged box is required.");
            var size = Pair(shape, "size"); var right = Pair(shape, "right_offset"); var left = Pair(shape, "left_offset"); float x = BasicMeleeSource.Number(right[0]), y = BasicMeleeSource.Number(right[1]);
            if (BasicMeleeSource.Number(left[0]) != -x || BasicMeleeSource.Number(left[1]) != y) throw new ArgumentException("Charged left geometry must remain mirrored.");
            return new SkillData { id = Text(node, "id"), name = Text(node, "name"), slot = Text(node, "slot"), description = Text(node, "rules"),
                cooldown = Number(node, "cooldown"), capacity = 1, inputSpeed = Number(node, "input_move_speed"), facing = Text(node, "facing"), vertical = Text(node, "vertical"),
                activation = Number(node, "activation_time"), end = Number(node, "motion_end"), chargeMin = Number(charge, "min_hold"), chargeMax = Number(charge, "max_hold"),
                chargeMinDamage = Number(charge, "min_damage"), chargeMaxDamage = Number(charge, "max_damage"),
                chargeHoldUntilRelease = Optional(charge, "hold_until_release")?.Value == "true",
                chargeReleaseWindup = Optional(charge, "release_windup") == null ? 0 : Number(charge, "release_windup"),
                invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                hits = new[] { new HitData { time = Number(hit, "time"), active = Number(hit, "active"), damage = Number(hit, "damage"), anchor = Text(hit, "anchor"), targets = Text(hit, "targets"), pushSeconds = .1f,
                    box = new BoxData { width = BasicMeleeSource.Number(size[0]), height = BasicMeleeSource.Number(size[1]), x = x, y = y } } } };
        }
        static XElement Member(XElement node, string name) => BasicMeleeSource.Member(node, name);
        static XElement Optional(XElement node, string name) => BasicMeleeSource.Optional(node, name);
        static XElement[] Items(XElement node, string name) => BasicMeleeSource.Items(Member(node, name));
        static string Text(XElement node, string name) => BasicMeleeSource.String(Member(node, name));
        static float Number(XElement node, string name) => BasicMeleeSource.Number(Member(node, name));
        static bool Null(XElement node, string name) => (string)Member(node, name).Attribute("type") == "null";
        static XElement[] Pair(XElement node, string name) { var values = Items(node, name); return values.Length == 2 ? values : throw new ArgumentException("An existing two-coordinate array is required."); }
        static void Set(XElement node, float value) { if (!BasicMeleeSource.Number(node).Equals(value)) node.Value = value.ToString("R", CultureInfo.InvariantCulture); }
    }
}
