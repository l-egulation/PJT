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
    // One DOM adapter for existing canonical fields. It transports values into
    // the same closed C# compiler; it never executes policy strings or time code.
    public static class AuthoredSkillSource
    {
        public const string RelativePath = BasicMeleeSource.RelativePath;
        public const int MaximumFileBytes = BasicMeleeSource.MaximumFileBytes;
        public static AuthoredSkillDefinition[] Load(string repository, out string hash)
            => Compile(BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(repository, RelativePath), out hash, MaximumFileBytes)));
        public static string Save(string repository, string expectedHash, IReadOnlyList<AuthoredSkillDefinition> candidates)
        {
            string path = Path.Combine(repository, RelativePath); var root = BasicMeleeSource.Parse(SourceFiles.Read(path, out var currentHash, MaximumFileBytes));
            if (currentHash != expectedHash) throw new IOException("Source changed outside this window. Reload before saving; no file was overwritten.");
            var original = Compile(root);
            if (candidates == null || candidates.Count == 0 || candidates.Count > original.Length) throw new ArgumentException("A nonempty unique subset of whole authored candidates is required.");
            var selected = new Dictionary<string, AuthoredSkillDefinition>(StringComparer.Ordinal);
            foreach (var candidate in candidates) {
                if (candidate == null || selected.ContainsKey(candidate.Id)) throw new ArgumentException("Every selected existing action ID must be unique.");
                var previous = original.SingleOrDefault(value => value.Id == candidate.Id);
                if (previous == null || previous.SourceVersion != candidate.SourceVersion || previous.Recompile(candidate.ToNumericData()).ContentHash != candidate.ContentHash) throw new ArgumentException("Authored fixed policy/resource metadata changed. Reload before saving.");
                SpecialFrameNumericEdits.RequireAuthored(repository, previous, candidate);
                selected.Add(candidate.Id, candidate);
            }
            foreach (var form in Items(root, "forms")) foreach (var node in Items(form, "specials")) {
                string id = Text(node, "id"); if (!selected.TryGetValue(id, out var definition)) continue;
                var data = definition.ToNumericData(); Set(Member(node, "activation_time"), data.activation); Set(Member(node, "motion_end"), data.end);
                var hits = Items(node, "hits");
                for (int index = 0; index < hits.Length; index++) {
                    var hit = hits[index]; var next = data.hits[index]; Set(Member(hit, "time"), next.time); Set(Member(hit, "active"), next.active); Set(Member(hit, "damage"), next.damage);
                    var shape = Member(hit, "shape"); if (next.box != null) SaveBox(shape, next.box);
                    var control = Member(hit, "control"); SetOptional(control, "root", next.root); SetOptional(control, "stun", next.stun); SetOptional(control, "push_distance", next.push); SetOptional(control, "push_seconds", next.pushSeconds);
                }
                var move = Member(node, "movement"); if (data.movement != null) {
                    var next = data.movement; SetOptional(move, "start", next.start); SetOptional(move, "duration", next.duration); SetOptional(move, "speed", next.speed); SetOptional(move, "wall_recovery", next.wallRecovery);
                    SetOptional(move, "target_gap", next.gap); SetOptional(move, "recast_window", next.recastWindow); SetOptional(move, "target_radius", next.targetRadius); SetOptional(move, "primary_end_damage", next.endDamage);
                    SetOptional(move, "distance", next.distance); SetOptional(move, "max_distance", next.distance);
                }
                var effect = Member(node, "independent_effect"); if (data.effect != null) {
                    var next = data.effect; SetOptional(effect, "duration", next.duration); SetOptional(effect, "hp", next.hp); SetOptional(effect, "taunt_radius", next.radius);
                    SetOptional(effect, "window", next.parryWindow); SetOptional(effect, "melee_reflect_base", next.parryDamage);
                    var position = Optional(effect, "spawn_offset"); if (position != null) { var pair = Array(position); Set(pair[0], next.x); Set(pair[1], next.y); }
                    var shape = Optional(effect, "shape"); if (shape != null && next.box != null) SaveBox(shape, next.box);
                    SetOptional(effect, "schedule_at", data.activation);
                    if (Optional(effect, "fire_at") != null) Set(Member(effect, "fire_at"), data.hits[data.hits.Length - 1].time);
                    var times = Optional(effect, "fire_times"); if (times != null) { var values = Array(times); for (int index = 0; index < values.Length; index++) Set(values[index], data.hits[index + 1].time); }
                    if (Optional(effect, "end_at") != null && data.hits.Length != 0) Set(Member(effect, "end_at"), data.hits.Max(hit => hit.time + hit.active));
                    if (Optional(effect, "telegraph_seconds") != null && data.hits.Length != 0) Set(Member(effect, "telegraph_seconds"), data.hits[data.hits.Length - 1].time - data.activation);
                }
                var spans = Array(Member(node, "invulnerability")); for (int index = 0; index < spans.Length; index++) { var pair = Array(spans[index]); Set(pair[0], data.invulStart[index]); Set(pair[1], data.invulEnd[index]); }
                var freeze = Optional(node, "vertical_freeze"); if (freeze != null) { var pair = Array(freeze); Set(pair[0], data.verticalFreeze[0]); Set(pair[1], data.verticalFreeze[1]); }
            }
            Compile(root); return SourceFiles.Save(path, expectedHash, BasicMeleeSource.Serialize(root), MaximumFileBytes);
        }
        public static void ValidateGenerated(string repository, Catalog generated)
        {
            if (generated?.forms == null) throw new ArgumentException("Generated combat catalog is required.");
            foreach (var source in Load(repository, out _)) {
                var matches = generated.forms.SelectMany(form => form.specials ?? System.Array.Empty<SkillData>()).Where(value => value != null && value.id == source.Id).ToArray();
                if (matches.Length != 1 || !AuthoredSkillDefinition.TryCreate(source.FormId, matches[0], out var compiled, out _) || compiled.ContentHash != source.ContentHash) throw new ArgumentException("Generated authored skill differs from canonical source: " + source.Id + ". Export the combat catalog before building.");
            }
        }
        static AuthoredSkillDefinition[] Compile(XElement root)
        {
            var result = new Dictionary<string, AuthoredSkillDefinition>(StringComparer.Ordinal);
            foreach (var form in Items(root, "forms")) foreach (var node in Items(form, "specials")) {
                string id = Text(node, "id"); if (!AuthoredSkillDefinition.Supports(id)) continue;
                var data = Transport(node);
                if (result.ContainsKey(id)) throw new ArgumentException("Duplicate canonical authored action: " + id);
                if (!AuthoredSkillDefinition.TryCreate(Text(form, "id"), data, out var compiled, out var error)) throw new ArgumentException("Invalid canonical authored action: " + id + ". " + error);
                result.Add(id, compiled);
            }
            if (result.Count != AuthoredSkillDefinition.SupportedIds.Count) throw new ArgumentException("Every supported canonical authored action is required.");
            return AuthoredSkillDefinition.SupportedIds.Select(id => result[id]).ToArray();
        }
        static SkillData Transport(XElement node)
        {
            if (Optional(node, "action_tracks") != null || Optional(node, "charge") != null) throw new ArgumentException("Authored generic actions do not replace finite/charge definitions.");
            var hits = Items(node, "hits"); var spans = Array(Member(node, "invulnerability"));
            var data = new SkillData { id = Text(node, "id"), name = Text(node, "name"), slot = Text(node, "slot"), description = Text(node, "rules"), activation = Number(node, "activation_time"), end = Number(node, "motion_end"),
                cooldown = Number(node, "cooldown"), capacity = Optional(node, "charges") is XElement charges ? (int)Number(charges, "capacity") : 1,
                inputSpeed = Number(node, "input_move_speed"), facing = Text(node, "facing"), vertical = Text(node, "vertical"), hits = new HitData[hits.Length], invulStart = new float[spans.Length], invulEnd = new float[spans.Length] };
            for (int index = 0; index < hits.Length; index++) { var hit = hits[index]; var control = Member(hit, "control"); data.hits[index] = new HitData { time = Number(hit, "time"), active = Number(hit, "active"), damage = Number(hit, "damage"), anchor = Text(hit, "anchor"), targets = Text(hit, "targets"),
                    root = Number(control, "root", 0), stun = Number(control, "stun", 0), push = Number(control, "push_distance", 0), pushSeconds = Number(control, "push_seconds", .1f), pushWithCaster = Boolean(control, "push_with_caster"), box = Shape(Member(hit, "shape")) }; }
            for (int index = 0; index < spans.Length; index++) { var pair = Array(spans[index]); data.invulStart[index] = BasicMeleeSource.Number(pair[0]); data.invulEnd[index] = BasicMeleeSource.Number(pair[1]); }
            var freeze = Optional(node, "vertical_freeze"); data.verticalFreeze = freeze == null ? System.Array.Empty<float>() : Array(freeze).Select(BasicMeleeSource.Number).ToArray();
            var move = Member(node, "movement"); if (!Null(move)) data.movement = new MoveData { type = Text(move, "type", "dash"), wallPolicy = Text(move, "wall_policy", "end"),
                start = Number(move, "start", 0), duration = Number(move, "duration", 0), distance = Number(move, "distance", Number(move, "max_distance", 0)), speed = Number(move, "speed", 0), wallRecovery = Number(move, "wall_recovery", 0),
                gap = Number(move, "target_gap", .25f), recastWindow = Number(move, "recast_window", 0), targetRadius = Number(move, "target_radius", 0), primaryPush = Boolean(move, "primary_push"), endDamage = Number(move, "primary_end_damage", 0) };
            var effect = Member(node, "independent_effect"); if (!Null(effect)) {
                var position = Optional(effect, "right_spawn") ?? Optional(effect, "spawn_offset"); var xy = position == null ? null : Array(position);
                data.effect = new EffectData { type = Text(effect, "type"), duration = Number(effect, "duration", Number(effect, "lifetime", 0)), hp = Number(effect, "hp", Number(effect, "shield_hp", 0)), reduction = Number(effect, "damage_reduction", 0),
                    speed = Number(effect, "speed", 0), distance = Number(effect, "distance", 0), radius = Number(effect, "taunt_radius", 0), x = xy == null ? 0 : BasicMeleeSource.Number(xy[0]), y = xy == null ? 0 : BasicMeleeSource.Number(xy[1]),
                    parryWindow = Number(effect, "window", 0), parryDamage = Number(effect, "melee_reflect_base", 0), box = Optional(effect, "shape") is XElement shape ? Shape(shape) : null };
            }
            return data;
        }
        static BoxData Shape(XElement shape)
        {
            if (Null(shape)) return null; if (Text(shape, "kind") != "box") throw new ArgumentException("The existing authored shape must be a box.");
            var size = Array(Member(shape, "size")); var right = Array(Member(shape, "right_offset")); var left = Array(Member(shape, "left_offset"));
            if (size.Length != 2 || right.Length != 2 || left.Length != 2 || BasicMeleeSource.Number(left[0]) != -BasicMeleeSource.Number(right[0]) || BasicMeleeSource.Number(left[1]) != BasicMeleeSource.Number(right[1])) throw new ArgumentException("Existing authored left geometry must mirror right.");
            return new BoxData { width = BasicMeleeSource.Number(size[0]), height = BasicMeleeSource.Number(size[1]), x = BasicMeleeSource.Number(right[0]), y = BasicMeleeSource.Number(right[1]) };
        }
        static void SaveBox(XElement shape, BoxData box)
        {
            var size = Array(Member(shape, "size")); var right = Array(Member(shape, "right_offset")); var left = Array(Member(shape, "left_offset"));
            Set(size[0], box.width); Set(size[1], box.height); Set(right[0], box.x); Set(right[1], box.y); Set(left[0], -box.x); Set(left[1], box.y);
            SetOptional(shape, "reach_from_body_front", box.x + box.width / 2 - .65625f); SetOptional(shape, "radius_from_root", box.width / 2); SetOptional(shape, "bottom", box.y - box.height / 2);
        }
        static XElement Member(XElement node, string name) => BasicMeleeSource.Member(node, name);
        static XElement Optional(XElement node, string name) => BasicMeleeSource.Optional(node, name);
        static XElement[] Array(XElement node) => BasicMeleeSource.Items(node);
        static XElement[] Items(XElement node, string name) => Array(Member(node, name));
        static bool Null(XElement node) => (string)node.Attribute("type") == "null";
        static string Text(XElement node, string name) => BasicMeleeSource.String(Member(node, name));
        static string Text(XElement node, string name, string fallback) => Optional(node, name) is XElement value ? BasicMeleeSource.String(value) : fallback;
        static float Number(XElement node, string name) => BasicMeleeSource.Number(Member(node, name));
        static float Number(XElement node, string name, float fallback) => Optional(node, name) is XElement value ? BasicMeleeSource.Number(value) : fallback;
        static bool Boolean(XElement node, string name) => Optional(node, name)?.Value == "true";
        static void Set(XElement node, float value) { if (!BasicMeleeSource.Number(node).Equals(value)) node.Value = value.ToString("R", CultureInfo.InvariantCulture); }
        static void SetOptional(XElement node, string name, float value) { var member = Optional(node, name); if (member != null) Set(member, value); }
    }
}
