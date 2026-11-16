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
    // The existing melee JSON remains the only canonical file. This adapter
    // transports fixed metadata and selected consumed numbers/tracks; it never
    // resolves markers. Load/save/build use ActionTrackDefinition/FrameTimeline.
    public static class FiniteActionTrackSource
    {
        public const string RelativePath = BasicMeleeSource.RelativePath;
        public const string CatalogPath = "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json";
        public const int MaximumFileBytes = BasicMeleeSource.MaximumFileBytes;
        public static ActionTrackDefinition[] Load(string repository, out string fileHash)
            => Compile(BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(repository, RelativePath), out fileHash, MaximumFileBytes)));
        public static string Save(string repository, string expectedHash, IReadOnlyList<ActionTrackDefinition> candidates)
        {
            string path = Path.Combine(repository, RelativePath);
            var root = BasicMeleeSource.Parse(SourceFiles.Read(path, out var actualHash, MaximumFileBytes));
            if (actualHash != expectedHash) throw new IOException("Source changed outside this window. Reload before saving; no file was overwritten.");
            var original = Compile(root);
            if (candidates == null || candidates.Count == 0 || candidates.Count > original.Length) throw new ArgumentException("A nonempty subset of complete finite action candidates is required.");
            var byId = new Dictionary<string, ActionTrackDefinition>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (candidate == null || byId.ContainsKey(candidate.Id)) throw new ArgumentException("Finite candidates require distinct existing IDs.");
                byId.Add(candidate.Id, candidate);
            }
            var sourceById = original.ToDictionary(source => source.Id, StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (!sourceById.TryGetValue(candidate.Id, out var source) || candidate.SourceVersion != source.SourceVersion || source.Recompile(candidate.ToDocument(), candidate.ToNumericData()).ContentHash != candidate.ContentHash)
                    throw new ArgumentException("Finite action metadata changed since this candidate was read. Reload before saving.");
                if (candidate.SourceTimeMode != ActionTrackSourceTimeMode.CombatTimeFixed) throw new ArgumentException("Derived FrameTime graphs are saved only in the special frame source, not the numeric action source.");
                SpecialFrameNumericEdits.RequireFinite(repository, source, candidate);
            }
            foreach (var node in SpecialNodes(root))
            {
                string id = Text(node, "id");
                if (!byId.TryGetValue(id, out var candidate)) continue;
                SaveNumeric(node, sourceById[id], candidate);
                var replacement = BasicMeleeSource.Parse(JsonUtility.ToJson(candidate.ToDocument(), true));
                replacement.Name = "action_tracks";
                BasicMeleeSource.Member(node, "action_tracks").ReplaceWith(replacement);
            }
            Compile(root); // Validate every canonical finite action before atomic publication.
            return SourceFiles.Save(path, expectedHash, BasicMeleeSource.Serialize(root), MaximumFileBytes);
        }
        public static void ValidateGenerated(string repository, Catalog generated)
        {
            if (generated?.forms == null) throw new ArgumentException("Generated combat catalog is required.");
            var current = Load(repository, out _);
            var compiled = new Dictionary<string, ActionTrackDefinition>(StringComparer.Ordinal);
            foreach (var form in generated.forms)
                if (form?.specials != null) foreach (var source in form.specials)
                {
                    if (source == null || ActionTrackDocument.IsAbsent(source.actionTracks)) continue;
                    if (!ActionTrackDefinition.Supports(source.id) || compiled.ContainsKey(source.id)) throw new ArgumentException("Unexpected or duplicate generated finite action.");
                    compiled.Add(source.id, new ActionTrackDefinition(source));
                }
            if (compiled.Count != current.Length) throw new ArgumentException("Generated finite sources are missing. Export the combat catalog before building.");
            foreach (var source in current)
                if (!compiled.TryGetValue(source.Id, out var value) || source.ContentHash != value.ContentHash)
                    throw new ArgumentException("Generated finite action differs from its canonical source: " + source.Id + ". Export the combat catalog before building.");
        }
        static ActionTrackDefinition[] Compile(XElement root)
        {
            var result = new List<ActionTrackDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in SpecialNodes(root))
            {
                string id = Text(node, "id"); var tracks = BasicMeleeSource.Optional(node, "action_tracks");
                if (!ActionTrackDefinition.Supports(id))
                {
                    if (tracks != null) throw new ArgumentException("Only explicitly migrated existing actions have authored finite tracks.");
                    continue;
                }
                if (!ids.Add(id) || tracks == null) throw new ArgumentException("Missing or duplicate canonical finite action: " + id);
                var transport = Transport(node);
                var document = new XElement(tracks); document.Name = "root";
                transport.actionTracks = JsonUtility.FromJson<ActionTrackDocument>(BasicMeleeSource.Serialize(document));
                result.Add(new ActionTrackDefinition(transport));
            }
            if (result.Count != ActionTrackDefinition.SupportedIds.Count) throw new ArgumentException("Every migrated canonical finite action is required.");
            return ActionTrackDefinition.SupportedIds.Select(id => result.Single(value => value.Id == id)).ToArray();
        }
        static IEnumerable<XElement> SpecialNodes(XElement root)
        {
            foreach (var form in BasicMeleeSource.Items(BasicMeleeSource.Member(root, "forms")))
                foreach (var skill in BasicMeleeSource.Items(BasicMeleeSource.Member(form, "specials"))) yield return skill;
        }
        static SkillData Transport(XElement source)
        {
            var hits = BasicMeleeSource.Items(BasicMeleeSource.Member(source, "hits"));
            var result = new SkillData {
                id = Text(source, "id"), name = Text(source, "name"), slot = Text(source, "slot"), description = Text(source, "rules"),
                vertical = Text(source, "vertical"), facing = Text(source, "facing"), cooldown = Number(source, "cooldown"),
                activation = Number(source, "activation_time"), end = Number(source, "motion_end"), inputSpeed = Number(source, "input_move_speed"),
                invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(), hits = new HitData[hits.Length], capacity = 1
            };
            var invulnerability = BasicMeleeSource.Items(BasicMeleeSource.Member(source, "invulnerability"));
            if (invulnerability.Length != 0) throw new ArgumentException("These approved finite actions have no invulnerability track.");
            for (int index = 0; index < hits.Length; index++)
            {
                var hit = hits[index]; var control = BasicMeleeSource.Member(hit, "control");
                result.hits[index] = new HitData {
                    time = Number(hit, "time"), active = Number(hit, "active"), damage = Number(hit, "damage"), anchor = Text(hit, "anchor"), targets = Text(hit, "targets"),
                    box = Shape(BasicMeleeSource.Member(hit, "shape")), root = Number(control, "root", 0), stun = Number(control, "stun", 0),
                    push = Number(control, "push_distance", 0), pushSeconds = Number(control, "push_seconds", .1f), pushWithCaster = Boolean(control, "push_with_caster")
                };
            }
            var movement = BasicMeleeSource.Member(source, "movement");
            if ((string)movement.Attribute("type") != "null") result.movement = new MoveData {
                type = Text(movement, "type", "dash"), wallPolicy = Text(movement, "wall_policy", "end"), start = Number(movement, "start", 0), duration = Number(movement, "duration", 0),
                distance = Number(movement, "distance", Number(movement, "max_distance", 0)), speed = Number(movement, "speed", 0), wallRecovery = Number(movement, "wall_recovery", 0),
                gap = Number(movement, "target_gap", .25f), primaryPush = Boolean(movement, "primary_push"), endDamage = Number(movement, "primary_end_damage", 0),
                recastWindow = Number(movement, "recast_window", 0), targetRadius = Number(movement, "target_radius", 0)
            };
            var effect = BasicMeleeSource.Member(source, "independent_effect");
            if ((string)effect.Attribute("type") != "null")
            {
                var position = BasicMeleeSource.Optional(effect, "right_spawn") ?? BasicMeleeSource.Optional(effect, "spawn_offset");
                var xy = position == null ? null : BasicMeleeSource.Items(position);
                result.effect = new EffectData {
                    type = Text(effect, "type"), duration = Number(effect, "duration", Number(effect, "lifetime", 0)),
                    hp = Number(effect, "hp", Number(effect, "shield_hp", 0)), reduction = Number(effect, "damage_reduction", 0),
                    speed = Number(effect, "speed", 0), distance = Number(effect, "distance", 0), radius = Number(effect, "taunt_radius", 0),
                    x = xy == null ? 0 : BasicMeleeSource.Number(xy[0]), y = xy == null ? 0 : BasicMeleeSource.Number(xy[1])
                };
            }
            return result;
        }
        static void SaveNumeric(XElement node, ActionTrackDefinition original, ActionTrackDefinition candidate)
        {
            var numeric = candidate.ToNumericData(); var hits = BasicMeleeSource.Items(BasicMeleeSource.Member(node, "hits"));
            for (int index = 0; index < hits.Length; index++) Set(BasicMeleeSource.Member(hits[index], "damage"), numeric.hits[index].damage);
            if (candidate.Id == ActionTrackDefinition.MovementId) {
                var move = BasicMeleeSource.Member(node, "movement"); Set(BasicMeleeSource.Member(move, "speed"), numeric.dashSpeed); Set(BasicMeleeSource.Member(move, "wall_recovery"), numeric.wallRecovery);
            }
            if (candidate.Id == ActionTrackDefinition.WaveId) {
                var effect = BasicMeleeSource.Member(node, "independent_effect"); Set(BasicMeleeSource.Member(effect, "speed"), numeric.waveSpeed); Set(BasicMeleeSource.Member(effect, "distance"), numeric.waveDistance);
                var prior = original.ToNumericData();
                if (numeric.waveSpawnX != prior.waveSpawnX || numeric.waveSpawnY != prior.waveSpawnY) {
                    var right = BasicMeleeSource.Items(BasicMeleeSource.Member(effect, "right_spawn")); var left = BasicMeleeSource.Items(BasicMeleeSource.Member(effect, "left_spawn"));
                    if (right.Length != 2 || left.Length != 2) throw new ArgumentException("Existing wave spawn positions require two mirrored coordinates.");
                    Set(right[0], numeric.waveSpawnX); Set(right[1], numeric.waveSpawnY); Set(left[0], -numeric.waveSpawnX); Set(left[1], numeric.waveSpawnY);
                }
            }
            if (candidate.Id == ActionTrackDefinition.GuardId) {
                var effect = BasicMeleeSource.Member(node, "independent_effect"); Set(BasicMeleeSource.Member(effect, "shield_hp"), numeric.guardHp); Set(BasicMeleeSource.Member(effect, "damage_reduction"), numeric.guardReduction);
            }
        }
        static void Set(XElement node, float value)
        {
            if (!BasicMeleeSource.Number(node).Equals(value)) node.Value = value.ToString("R", CultureInfo.InvariantCulture);
        }
        static BoxData Shape(XElement shape)
        {
            var size = BasicMeleeSource.Items(BasicMeleeSource.Member(shape, "size"));
            var offset = BasicMeleeSource.Items(BasicMeleeSource.Member(shape, "right_offset"));
            if (size.Length != 2 || offset.Length != 2) throw new ArgumentException("A two-coordinate box is required.");
            return new BoxData { width = BasicMeleeSource.Number(size[0]), height = BasicMeleeSource.Number(size[1]), x = BasicMeleeSource.Number(offset[0]), y = BasicMeleeSource.Number(offset[1]) };
        }
        static string Text(XElement source, string member) => BasicMeleeSource.String(BasicMeleeSource.Member(source, member));
        static string Text(XElement source, string member, string fallback) => BasicMeleeSource.Optional(source, member) is XElement node ? BasicMeleeSource.String(node) : fallback;
        static float Number(XElement source, string member) => BasicMeleeSource.Number(BasicMeleeSource.Member(source, member));
        static float Number(XElement source, string member, float fallback) => BasicMeleeSource.Optional(source, member) is XElement node ? BasicMeleeSource.Number(node) : fallback;
        static bool Boolean(XElement source, string member) => BasicMeleeSource.Optional(source, member)?.Value == "true";
    }
}
