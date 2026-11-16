using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Deviant.Combat;
using Deviant.Definitions;

namespace Deviant.Authoring
{
    public static class BasicMeleeSource
    {
        public const string RelativePath = "data/combat/melee_weapon_design.json";
        public const int MaximumFileBytes = 1024 * 1024;
        const string FirstSwordSource = "actions/base_sword_basic_0.json";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static BasicMeleeEdit[] Load(string repository, out string hash)
        {
            var root = Parse(SourceFiles.Read(Path.Combine(repository, RelativePath), out hash, MaximumFileBytes));
            var hits = ReadHits(root);
            BasicSwordSource.Load(Path.Combine(repository, "data/combat", FirstSwordSource), out _);
            return hits;
        }

        public static BasicMeleeDefinition[] Compile(IReadOnlyList<BasicMeleeEdit> hits)
        {
            if (hits == null || hits.Count == 0) throw new ArgumentException("A complete remaining-basic candidate is required.");
            var result = new BasicMeleeDefinition[hits.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit == null || string.IsNullOrWhiteSpace(hit.formId) || hit.hitIndex < 0 || hit.Id == BasicSwordSource.ActionId || !ids.Add(hit.Id))
                    throw new ArgumentException("Only uniquely identified, unmigrated melee basics can be edited here. Use Basic Sword First Hit for the first sword hit.");
                if (!BasicMeleeDefinition.TryCreate(hit.Id, hit.hit?.ToData(), out var definition, out var error))
                    throw new ArgumentException(hit.Id + ": " + error);
                if (hit.rootSymmetric && definition.OffsetX != 0)
                    throw new ArgumentException(hit.Id + ": the existing root-symmetric box keeps X at zero; edit its width instead.");
                for (int previous = 0; previous < i; previous++)
                    if (hits[previous].formId == hit.formId && hits[previous].hit.active != hit.hit.active)
                        throw new ArgumentException(hit.formId + ": active_seconds is shared by all unmigrated hits of this form.");
                result[i] = definition;
            }
            return result;
        }

        public static string Save(string repository, string expectedHash, IReadOnlyList<BasicMeleeEdit> hits)
        {
            var definitions = Compile(hits);
            string path = Path.Combine(repository, RelativePath);
            var text = SourceFiles.Read(path, out var currentHash, MaximumFileBytes);
            if (currentHash != expectedHash) throw new IOException("Source changed outside this window. Reload before saving; no file was overwritten.");
            var root = Parse(text);
            var original = ReadHits(root);
            BasicSwordSource.Load(Path.Combine(repository, "data/combat", FirstSwordSource), out _);
            if (original.Length != definitions.Length) throw new ArgumentException("The complete remaining-basic candidate must match the source hit list.");
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < hits.Count; i++) byId.Add(hits[i].Id, i);
            foreach (var hit in original)
                if (!byId.TryGetValue(hit.Id, out int index) || hits[index].rootSymmetric != hit.rootSymmetric)
                    throw new ArgumentException("The candidate identities and geometry mapping must match the existing source.");
            foreach (var hit in original)
            {
                var next = definitions[byId[hit.Id]];
                if ((hit.hit.impact != next.Impact || hit.hit.active != next.Active || hit.hit.duration != next.Duration) && MeleeFrameSource.HasFrameOverride(repository, hit.Id))
                    throw new ArgumentException(hit.Id + ": active FrameTime owns hit/end markers. Shared active_seconds edits affecting this action require an explicit fixed-mode return.");
            }

            foreach (var form in Items(Member(root, "forms")))
            {
                string formId = String(Member(form, "id"));
                var basic = Member(form, "basic");
                var damages = Items(Member(basic, "damages"));
                var durations = Items(Member(basic, "motion_durations"));
                var impacts = Items(Member(basic, "local_hit_times"));
                var shapes = Items(Member(basic, "shapes"));
                bool durationChanged = false;
                for (int index = 0; index < damages.Length; index++)
                {
                    string id = formId + "_basic_" + index.ToString(Invariant);
                    if (!byId.TryGetValue(id, out int candidateIndex)) continue; // Mapped first sword remains untouched.
                    var next = definitions[candidateIndex];
                    SetNumber(damages[index], next.Damage);
                    durationChanged |= SetNumber(durations[index], next.Duration);
                    SetNumber(impacts[index], next.Impact);
                    SetNumber(Member(basic, "active_seconds"), next.Active);
                    var shape = shapes[index];
                    var size = Pair(Member(shape, "size"));
                    var right = Pair(Member(shape, "right_offset"));
                    var left = Pair(Member(shape, "left_offset"));
                    bool widthChanged = SetNumber(size[0], next.Width);
                    bool heightChanged = SetNumber(size[1], next.Height);
                    bool xChanged = SetNumber(right[0], next.OffsetX);
                    bool yChanged = SetNumber(right[1], next.OffsetY);
                    if (xChanged) SetNumber(left[0], -next.OffsetX);
                    if (yChanged) SetNumber(left[1], next.OffsetY);
                    // Existing melee_source.py and melee.md define these annotations;
                    // no new shape fields or alternate geometry source is introduced.
                    if (widthChanged || xChanged)
                    {
                        var reach = Optional(shape, "reach_from_body_front");
                        if (reach != null) SetNumber(reach, double.Parse(right[0].Value, Invariant) + double.Parse(size[0].Value, Invariant) / 2 - .65625);
                        var radius = Optional(shape, "radius_from_root");
                        if (radius != null) SetNumber(radius, double.Parse(size[0].Value, Invariant) / 2);
                    }
                    if (heightChanged || yChanged)
                    {
                        var bottom = Optional(shape, "bottom");
                        if (bottom != null) SetNumber(bottom, double.Parse(right[1].Value, Invariant) - double.Parse(size[1].Value, Invariant) / 2);
                    }
                }
                var cycle = Optional(basic, "fastest_cycle");
                if (durationChanged && cycle != null)
                {
                    double total = durations.Sum(duration => double.Parse(duration.Value, Invariant));
                    SetNumber(cycle, total);
                }
            }
            // The DOM retains all original properties, including unknown metadata,
            // specials, null hit_sources and number tokens that were not changed.
            ReadHits(root);
            return SourceFiles.Save(path, expectedHash, Serialize(root), MaximumFileBytes);
        }

        static BasicMeleeEdit[] ReadHits(XElement root)
        {
            RequireType(root, "object");
            var hits = new List<BasicMeleeEdit>();
            var formIds = new HashSet<string>(StringComparer.Ordinal);
            bool firstSwordFound = false;
            foreach (var form in Items(Member(root, "forms")))
            {
                string formId = String(Member(form, "id"));
                if (string.IsNullOrWhiteSpace(formId) || !formIds.Add(formId)) throw new ArgumentException("Source forms require unique identities.");
                string name = String(Member(form, "name"));
                var basic = Member(form, "basic");
                var damages = Items(Member(basic, "damages"));
                var durations = Items(Member(basic, "motion_durations"));
                var impacts = Items(Member(basic, "local_hit_times"));
                var shapes = Items(Member(basic, "shapes"));
                var sources = Optional(basic, "hit_sources");
                var sourceItems = sources == null ? null : Items(sources);
                if (durations.Length != damages.Length || impacts.Length != damages.Length || shapes.Length != damages.Length || sourceItems != null && sourceItems.Length != damages.Length)
                    throw new ArgumentException(formId + ": basic array lengths must match.");
                float active = Number(Member(basic, "active_seconds"));
                for (int index = 0; index < damages.Length; index++)
                {
                    bool mapped = sourceItems != null && Type(sourceItems[index]) != "null";
                    if (mapped)
                    {
                        if (formId != "base_sword" || index != 0 || String(sourceItems[index]) != FirstSwordSource ||
                            new[] { damages[index], durations[index], impacts[index], shapes[index] }.Any(value => Type(value) != "null"))
                            throw new ArgumentException("The existing first-sword hit_sources mapping and null array slots must remain unchanged.");
                        firstSwordFound = true;
                        continue;
                    }
                    if (formId == "base_sword" && index == 0) throw new ArgumentException("The first sword hit must use its existing action source and window.");
                    var shape = shapes[index];
                    if (String(Member(shape, "kind")) != "box") throw new ArgumentException("Only the existing melee basic boxes are supported.");
                    var size = Pair(Member(shape, "size"));
                    var right = Pair(Member(shape, "right_offset"));
                    var left = Pair(Member(shape, "left_offset"));
                    float x = Number(right[0]), y = Number(right[1]);
                    if (Number(left[0]) != -x || Number(left[1]) != y) throw new ArgumentException("The existing melee basic offsets must mirror X and preserve Y.");
                    hits.Add(new BasicMeleeEdit {
                        formId = formId, formName = name, hitIndex = index, rootSymmetric = Optional(shape, "radius_from_root") != null,
                        hit = new BasicMeleeHitData { damage = Number(damages[index]), duration = Number(durations[index]), impact = Number(impacts[index]), active = active,
                            box = new BoxData { width = Number(size[0]), height = Number(size[1]), x = x, y = y } }
                    });
                }
            }
            if (!firstSwordFound) throw new ArgumentException("The existing first-sword source mapping is required.");
            var result = hits.ToArray(); Compile(result); return result;
        }

        internal static XElement Parse(string json)
        {
            try
            {
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
                    return XElement.Load(reader, LoadOptions.PreserveWhitespace);
            }
            catch (Exception error) when (error is XmlException || error is SerializationException)
            { throw new ArgumentException("Invalid melee design JSON: " + error.Message, error); }
        }
        internal static string Serialize(XElement root)
        {
            // XElement retains expanded empty nodes from the JSON reader. Its
            // default writer emits empty text for those nodes, which is not a
            // JSON null/container value. Normalize only structural content;
            // whitespace-only strings and scalar number tokens stay verbatim.
            var document = new XElement(root);
            foreach (var node in document.DescendantsAndSelf())
                if (Type(node) == "null" || Type(node) == "object" || Type(node) == "array")
                {
                    if (!node.HasElements) node.RemoveNodes();
                    else foreach (var text in node.Nodes().OfType<XText>().ToArray()) text.Remove();
                }
            using (var stream = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), false, true, "  ")) document.WriteTo(writer);
                return Encoding.UTF8.GetString(stream.ToArray()).TrimStart('\uFEFF') + "\n";
            }
        }
        static string Type(XElement node) => (string)node.Attribute("type") ?? "string";
        static void RequireType(XElement node, string type)
        { if (node == null || Type(node) != type) throw new ArgumentException("Expected JSON " + type + "."); }
        internal static XElement Optional(XElement parent, string name)
        {
            RequireType(parent, "object");
            var matches = parent.Elements(name).ToArray();
            if (matches.Length > 1) throw new ArgumentException("Duplicate source property: " + name + ".");
            return matches.Length == 0 ? null : matches[0];
        }
        internal static XElement Member(XElement parent, string name) => Optional(parent, name) ?? throw new ArgumentException("Missing source property: " + name + ".");
        internal static XElement[] Items(XElement node) { RequireType(node, "array"); return node.Elements().ToArray(); }
        static XElement[] Pair(XElement node)
        {
            var pair = Items(node);
            if (pair.Length != 2) throw new ArgumentException("An existing two-coordinate source array is required.");
            return pair;
        }
        internal static string String(XElement node) { RequireType(node, "string"); return node.Value; }
        internal static float Number(XElement node)
        {
            RequireType(node, "number");
            if (!float.TryParse(node.Value, NumberStyles.Float, Invariant, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("A finite source number is required.");
            return value;
        }
        static bool SetNumber(XElement node, float value)
        {
            if (Number(node).Equals(value)) return false;
            node.Value = value.ToString("R", Invariant); return true;
        }
        static void SetNumber(XElement node, double value)
        { RequireType(node, "number"); node.Value = value.ToString("R", Invariant); }
    }
}
