using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    // Numeric authoring for the three already-migrated direct policies. The
    // existing melee JSON remains the one source; policy/resources are readonly.
    public static class DirectMeleeSpecialSource
    {
        public const string RelativePath = BasicMeleeSource.RelativePath;
        public const int MaximumFileBytes = BasicMeleeSource.MaximumFileBytes;
        public static DirectMeleeSpecialDefinition[] Load(string repository, out string fileHash)
        {
            ValidateInitialReference(repository);
            return Compile(BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(repository, RelativePath), out fileHash, MaximumFileBytes)));
        }
        public static string Save(string repository, string expectedHash, IReadOnlyList<DirectMeleeSpecialDefinition> candidates)
        {
            string path = Path.Combine(repository, RelativePath);
            var root = BasicMeleeSource.Parse(SourceFiles.Read(path, out var actualHash, MaximumFileBytes));
            if (actualHash != expectedHash) throw new IOException("Source changed outside this window. Reload before saving; no file was overwritten.");
            ValidateInitialReference(repository); var original = Compile(root);
            if (candidates == null || candidates.Count == 0 || candidates.Count > original.Length) throw new ArgumentException("A nonempty subset of complete direct numeric candidates is required.");
            var byId = new Dictionary<string, DirectMeleeSpecialDefinition>(StringComparer.Ordinal);
            foreach (var candidate in candidates) {
                if (candidate == null || byId.ContainsKey(candidate.Id)) throw new ArgumentException("Direct numeric candidates require unique existing IDs.");
                var source = original.SingleOrDefault(value => value.Id == candidate.Id);
                if (source == null || source.SourceVersion != candidate.SourceVersion || source.Recompile(candidate.ToBasicData()).ContentHash != candidate.ContentHash)
                    throw new ArgumentException("Direct action metadata changed since this candidate was read. Reload before saving.");
                if ((source.Impact != candidate.Impact || source.Active != candidate.Active || source.Duration != candidate.Duration) && MeleeFrameSource.HasFrameOverride(repository, source.Id))
                    throw new ArgumentException(source.Id + ": active FrameTime timing is read-only in the numeric source; edit the action markers or explicitly return fixed mode.");
                byId.Add(candidate.Id, candidate);
            }
            foreach (var form in BasicMeleeSource.Items(BasicMeleeSource.Member(root, "forms")))
                foreach (var node in BasicMeleeSource.Items(BasicMeleeSource.Member(form, "specials")))
                {
                    string id = Text(node, "id"); if (!byId.TryGetValue(id, out var value)) continue;
                    Set(BasicMeleeSource.Member(node, "activation_time"), value.Impact); Set(BasicMeleeSource.Member(node, "motion_end"), value.Duration);
                    var hit = BasicMeleeSource.Items(BasicMeleeSource.Member(node, "hits")).Single();
                    Set(BasicMeleeSource.Member(hit, "time"), value.Impact); Set(BasicMeleeSource.Member(hit, "active"), value.Active); Set(BasicMeleeSource.Member(hit, "damage"), value.Damage);
                    var box = value.ToBasicData().box; var shape = BasicMeleeSource.Member(hit, "shape");
                    var size = Pair(shape, "size"); var right = Pair(shape, "right_offset"); var left = Pair(shape, "left_offset");
                    Set(size[0], box.width); Set(size[1], box.height); Set(right[0], box.x); Set(right[1], box.y); Set(left[0], -box.x); Set(left[1], box.y);
                    var reach = BasicMeleeSource.Optional(shape, "reach_from_body_front"); if (reach != null) Set(reach, box.x + box.width / 2 - .65625f);
                    var radius = BasicMeleeSource.Optional(shape, "radius_from_root"); if (radius != null) Set(radius, box.width / 2);
                    var bottom = BasicMeleeSource.Optional(shape, "bottom"); if (bottom != null) Set(bottom, box.y - box.height / 2);
                }
            Compile(root); return SourceFiles.Save(path, expectedHash, BasicMeleeSource.Serialize(root), MaximumFileBytes);
        }
        public static void ValidateGenerated(string repository, Catalog generated)
        {
            if (generated?.forms == null) throw new ArgumentException("Generated combat catalog is required.");
            foreach (var source in Load(repository, out _))
            {
                var matches = generated.forms.SelectMany(form => form.specials ?? Array.Empty<SkillData>()).Where(value => value != null && value.id == source.Id).ToArray();
                if (matches.Length != 1 || !DirectMeleeSpecialDefinition.TryCreate(source.FormId, matches[0], out var compiled, out var error) || compiled.ContentHash != source.ContentHash)
                    throw new ArgumentException("Generated direct action differs from its canonical source: " + source.Id + ". Export the combat catalog before building.");
            }
        }
        static DirectMeleeSpecialDefinition[] Compile(XElement root)
        {
            var values = new Dictionary<string, DirectMeleeSpecialDefinition>(StringComparer.Ordinal);
            foreach (var form in BasicMeleeSource.Items(BasicMeleeSource.Member(root, "forms")))
                foreach (var node in BasicMeleeSource.Items(BasicMeleeSource.Member(form, "specials")))
                {
                    string id = Text(node, "id"); if (!DirectMeleeSpecialDefinition.SupportedIds.Contains(id)) continue;
                    string formId = Text(form, "id"); var source = Transport(node);
                    if (values.ContainsKey(id) || !DirectMeleeSpecialDefinition.TryCreate(formId, source, out var compiled, out var error))
                        throw new ArgumentException("Invalid or duplicate canonical direct special: " + id);
                    values.Add(id, compiled);
                }
            if (values.Count != DirectMeleeSpecialDefinition.SupportedIds.Count) throw new ArgumentException("Every supported direct action is required.");
            return DirectMeleeSpecialDefinition.SupportedIds.Select(id => values[id]).ToArray();
        }
        static SkillData Transport(XElement node)
        {
            var hits = BasicMeleeSource.Items(BasicMeleeSource.Member(node, "hits"));
            if (hits.Length != 1 || BasicMeleeSource.Optional(node, "action_tracks") != null || !Null(node, "movement") || !Null(node, "independent_effect") ||
                BasicMeleeSource.Items(BasicMeleeSource.Member(node, "invulnerability")).Length != 0 || BasicMeleeSource.Optional(node, "charge") != null || BasicMeleeSource.Optional(node, "charges") != null)
                throw new ArgumentException("The direct source requires its existing single-hit policy with no charge, movement, effect or defense tracks.");
            var hit = hits[0]; var control = BasicMeleeSource.Member(hit, "control");
            if (control.HasElements) throw new ArgumentException("These three existing direct actions have no control effect.");
            var shape = BasicMeleeSource.Member(hit, "shape"); if (Text(shape, "kind") != "box") throw new ArgumentException("An existing direct box is required.");
            var size = Pair(shape, "size"); var right = Pair(shape, "right_offset"); var left = Pair(shape, "left_offset");
            float x = BasicMeleeSource.Number(right[0]), y = BasicMeleeSource.Number(right[1]);
            if (BasicMeleeSource.Number(left[0]) != -x || BasicMeleeSource.Number(left[1]) != y) throw new ArgumentException("Direct left geometry must remain the existing mirrored right box.");
            return new SkillData { id = Text(node, "id"), name = Text(node, "name"), slot = Text(node, "slot"), description = Text(node, "rules"),
                cooldown = Number(node, "cooldown"), capacity = 1, activation = Number(node, "activation_time"), end = Number(node, "motion_end"),
                inputSpeed = Number(node, "input_move_speed"), facing = Text(node, "facing"), vertical = Text(node, "vertical"), invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                hits = new[] { new HitData { time = Number(hit, "time"), active = Number(hit, "active"), damage = Number(hit, "damage"), anchor = Text(hit, "anchor"), targets = Text(hit, "targets"), pushSeconds = .1f,
                    box = new BoxData { width = BasicMeleeSource.Number(size[0]), height = BasicMeleeSource.Number(size[1]), x = x, y = y } } } };
        }
        static void ValidateInitialReference(string repository)
        {
            var reference = BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(repository, "data/combat/basic_sword_special.json"), out _, MaximumFileBytes));
            if (Text(reference, "numeric_source") != "melee_weapon_design.json#base_sword_default" || Text(reference, "numeric_values_role") != "initial_approved_regression_provenance")
                throw new ArgumentException("The initial sword special reference must explicitly identify its one active canonical numeric source.");
        }
        static string Text(XElement node, string name) => BasicMeleeSource.String(BasicMeleeSource.Member(node, name));
        static float Number(XElement node, string name) => BasicMeleeSource.Number(BasicMeleeSource.Member(node, name));
        static bool Null(XElement node, string name) => (string)BasicMeleeSource.Member(node, name).Attribute("type") == "null";
        static XElement[] Pair(XElement node, string name) { var values = BasicMeleeSource.Items(BasicMeleeSource.Member(node, name)); return values.Length == 2 ? values : throw new ArgumentException("An existing two-coordinate array is required."); }
        static void Set(XElement node, float value) { if (!BasicMeleeSource.Number(node).Equals(value)) node.Value = value.ToString("R", CultureInfo.InvariantCulture); }
    }
}
