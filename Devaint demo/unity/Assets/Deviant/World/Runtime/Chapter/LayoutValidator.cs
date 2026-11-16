using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.World.Contracts;

namespace Deviant.World
{
    public sealed class ValidatedChapterLayout
    {
        public ChapterLayout Layout { get; }
        public LayoutBodyProfile Profile { get; }
        public string ResolvedHash { get; }
        public LayoutProofResult MovementProof { get; }
        internal readonly Dictionary<string, string> GeometryHashes;
        internal ValidatedChapterLayout(ChapterLayout layout, LayoutBodyProfile profile, Dictionary<string, string> hashes, LayoutProofResult movementProof = null)
        {
            Layout = layout; Profile = profile; GeometryHashes = hashes;
            MovementProof = movementProof;
            using (var stream = new System.IO.MemoryStream())
            {
                using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(layout.ContentHash);
                    foreach (var module in layout.Modules) { writer.Write(module.Id); writer.Write(hashes[module.Id]); }
                }
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    ResolvedHash = BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
    // All providers share structure/identity/catalog validation. The default
    // geometric proof is conservative level solid-box walking; other supported
    // movement must supply an explicit common-motor proof, never a bypass.
    public static class LayoutValidator
    {
        const float Epsilon = .0001f;
        public static bool TryValidate(ChapterLayout layout, LayoutBodyProfile profile, IChapterModuleCatalog catalog,
            out ValidatedChapterLayout validated, out string error, IChapterTraversalProof traversalProof = null)
        {
            validated = null; error = null;
            try
            {
                if (layout == null || profile == null || catalog == null) return Fail("Layout, body profile and module catalog are required.", out error);
                if (layout.Modules.Count == 0) return Fail("A chapter requires at least one module.", out error);
                if (Duplicate(layout.Modules.Select(m => m.Id)) || Duplicate(layout.Anchors.Select(a => a.Id)) ||
                    Duplicate(layout.Connections.Select(c => c.Id)) || Duplicate(layout.Placements.Select(p => p.Id)) ||
                    Duplicate(layout.Modules.Select(m => m.Id).Concat(layout.Placements.Select(p => p.Id))) || Duplicate(layout.Witnesses.Select(witness => witness.Id)))
                    return Fail("Duplicate module/anchor/connection/placement ID.", out error);
                var modules = layout.Modules.ToDictionary(m => m.Id, StringComparer.Ordinal);
                var anchors = layout.Anchors.ToDictionary(a => a.Id, StringComparer.Ordinal);
                if (layout.Repetition != null)
                {
                    var repeat = layout.Repetition;
                    if (repeat.TemplateIds.Count != layout.Modules.Count || repeat.TemplateIds.Any(id => !modules.ContainsKey(id)) || layout.Placements.Count != 0)
                        return Fail("Repeated terrain requires the complete ordered template set and no implicit repeated entity placements.", out error);
                    for (int index = 0; index < repeat.TemplateIds.Count; index++)
                        if (modules[repeat.TemplateIds[index]].Position.X != index * repeat.Width)
                            return Fail("Repeat template position disagrees with its supplied strip address.", out error);
                    if (traversalProof == null || profile.Motion == null || layout.Witnesses.Count == 0)
                        return Fail("Repeated terrain requires an explicit captured motor profile and technical traversal witnesses.", out error);
                }
                if (!anchors.TryGetValue(layout.EntryAnchorId, out var entry) || entry.Kind != LayoutAnchorKind.Traversal)
                    return Fail("Entry must reference a traversal anchor.", out error);
                if (layout.Anchors.Any(a => !modules.ContainsKey(a.ModuleId)) || layout.Placements.Any(p => !modules.ContainsKey(p.ModuleId)))
                    return Fail("Anchor/placement references an unknown module.", out error);
                foreach (var required in layout.RequiredAnchors) if (!anchors.ContainsKey(required)) return Fail("Required anchor missing: " + required, out error);
                foreach (var connection in layout.Connections)
                {
                    if (!anchors.TryGetValue(connection.From, out var from) || !anchors.TryGetValue(connection.To, out var to) ||
                        from.Kind != LayoutAnchorKind.Traversal || to.Kind != LayoutAnchorKind.Traversal || from.Id == to.Id)
                        return Fail("Connection references invalid traversal anchors: " + connection.Id, out error);
                    if (connection.Traversal != LayoutTraversal.Walk && traversalProof == null) return Fail("Unsupported traversal proof: " + connection.Id + " / " + connection.Traversal, out error);
                }
                if (Math.Abs(profile.OffsetY - profile.Height / 2) > Epsilon) return Fail("Unsupported body origin: feet must align with the body bottom.", out error);
                var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
                var solids = new List<LayoutRect>();
                foreach (var module in layout.Modules)
                {
                    if (!catalog.TryGetGeometry(module.ContentId, out var geometry, out var reason) || geometry == null)
                        return Fail("Module geometry unavailable: " + module.ContentId + " / " + reason, out error);
                    hashes[module.Id] = geometry.ContentHash;
                    if (geometry.OneWay.Count != 0 && traversalProof == null) return Fail("One-way terrain requires an explicit movement proof.", out error);
                    foreach (var box in geometry.Solids) solids.Add(box.Translate(module.Position));
                }
                foreach (var placement in layout.Placements)
                    if (!catalog.HasPlacementContent(placement.ContentId)) return Fail("Placement content missing: " + placement.ContentId, out error);
                var reached = new HashSet<string>(StringComparer.Ordinal) { entry.Id }; bool changed;
                do
                {
                    changed = false;
                    foreach (var connection in layout.Connections)
                    {
                        if (reached.Contains(connection.From)) changed |= reached.Add(connection.To);
                        if (reached.Contains(connection.To)) changed |= reached.Add(connection.From);
                    }
                } while (changed);
                foreach (var required in layout.RequiredAnchors)
                    if (anchors[required].Kind == LayoutAnchorKind.Traversal && !reached.Contains(required)) return Fail("Required traversal anchor is disconnected: " + required, out error);
                if (traversalProof != null)
                {
                    if (!traversalProof.TryValidate(layout, profile, catalog, out error)) return false;
                    validated = new ValidatedChapterLayout(layout, profile, hashes, traversalProof.LastResult); return true;
                }
                LayoutPoint Position(LayoutAnchor a) => modules[a.ModuleId].Position.Add(a.LocalPosition);
                foreach (var anchor in layout.Anchors.Where(a => a.Kind == LayoutAnchorKind.Traversal))
                {
                    var at = Position(anchor);
                    if (!Clear(profile.At(at, 1), solids, layout.Hazards) || !Clear(profile.At(at, -1), solids, layout.Hazards))
                        return Fail("Traversal anchor intersects solid/hazard: " + anchor.Id, out error);
                    float left = at.X - Math.Abs(profile.OffsetX) - profile.Width / 2, right = at.X + Math.Abs(profile.OffsetX) + profile.Width / 2;
                    if (!Supported(left, right, at.Y, solids)) return Fail("Traversal anchor lacks full level support: " + anchor.Id, out error);
                }
                foreach (var connection in layout.Connections)
                {
                    var a = Position(anchors[connection.From]); var b = Position(anchors[connection.To]);
                    if (profile.WalkSpeed <= 0 || Math.Abs(a.Y - b.Y) > Epsilon) return Fail("Unsupported level walking connection: " + connection.Id, out error);
                    float left = Math.Min(a.X, b.X) - Math.Abs(profile.OffsetX) - profile.Width / 2;
                    float right = Math.Max(a.X, b.X) + Math.Abs(profile.OffsetX) + profile.Width / 2;
                    var corridor = new LayoutRect(left, a.Y, right - left, profile.Height);
                    if (!Clear(corridor, solids, layout.Hazards) || !Supported(left, right, a.Y, solids))
                        return Fail("Connection is obstructed, hazardous or has a gap: " + connection.Id, out error);
                }
                validated = new ValidatedChapterLayout(layout, profile, hashes); return true;
            }
            catch (Exception exception) { error = "Layout validation failed: " + exception.Message; return false; }
        }
        static bool Duplicate(IEnumerable<string> ids) { var seen = new HashSet<string>(StringComparer.Ordinal); return ids.Any(id => !seen.Add(id)); }
        static bool Fail(string reason, out string error) { error = reason; return false; }
        static bool Clear(LayoutRect body, IEnumerable<LayoutRect> solids, IEnumerable<LayoutRect> hazards)
            => !solids.Any(body.Overlaps) && !hazards.Any(body.Overlaps);
        static bool Supported(float left, float right, float y, IEnumerable<LayoutRect> solids)
        {
            float covered = left;
            foreach (var floor in solids.Where(r => Math.Abs(r.Top - y) <= Epsilon && r.Right > left && r.X < right).OrderBy(r => r.X))
            {
                if (floor.X > covered + Epsilon) return false;
                covered = Math.Max(covered, floor.Right); if (covered >= right - Epsilon) return true;
            }
            return false;
        }
    }
}
