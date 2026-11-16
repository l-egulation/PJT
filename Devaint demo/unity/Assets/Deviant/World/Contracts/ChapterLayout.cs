using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.World.Contracts
{
    public readonly struct LayoutPoint
    {
        public float X { get; }
        public float Y { get; }
        public LayoutPoint(float x, float y) { LayoutValues.Finite(x); LayoutValues.Finite(y); X = x; Y = y; }
        public LayoutPoint Add(LayoutPoint other) => new LayoutPoint(X + other.X, Y + other.Y);
    }
    public readonly struct LayoutRect
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => X + Width;
        public float Top => Y + Height;
        public LayoutRect(float x, float y, float width, float height)
        {
            LayoutValues.Finite(x); LayoutValues.Finite(y); LayoutValues.Finite(width); LayoutValues.Finite(height);
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            LayoutValues.Finite(x + width); LayoutValues.Finite(y + height);
            X = x; Y = y; Width = width; Height = height;
        }
        public LayoutRect Translate(LayoutPoint at) => new LayoutRect(X + at.X, Y + at.Y, Width, Height);
        public bool Overlaps(LayoutRect other) => X < other.Right && Right > other.X && Y < other.Top && Top > other.Y;
    }
    public enum LayoutOriginKind { Authored, CapturedGeneration }
    public sealed class LayoutOrigin
    {
        public LayoutOriginKind Kind { get; }
        public string ProviderId { get; }
        public string ProviderVersion { get; }
        public string ContentVersion { get; }
        public long? Seed { get; }
        public LayoutOrigin(LayoutOriginKind kind, string providerId, string providerVersion, string contentVersion, long? seed = null)
        {
            if (kind != LayoutOriginKind.Authored && kind != LayoutOriginKind.CapturedGeneration) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind; ProviderId = LayoutValues.Id(providerId); ProviderVersion = LayoutValues.Id(providerVersion);
            ContentVersion = LayoutValues.Id(contentVersion); Seed = seed;
        }
    }
    public sealed class LayoutModule
    {
        public string Id { get; }
        public string ContentId { get; }
        public LayoutPoint Position { get; }
        public LayoutModule(string id, string contentId, LayoutPoint position)
        { Id = LayoutValues.Id(id); ContentId = LayoutValues.Id(contentId); Position = position; }
    }
    public enum LayoutAnchorKind { Traversal, Binding }
    public sealed class LayoutAnchor
    {
        public string Id { get; }
        public string ModuleId { get; }
        public LayoutPoint LocalPosition { get; }
        public LayoutAnchorKind Kind { get; }
        public LayoutAnchor(string id, string moduleId, LayoutPoint localPosition, LayoutAnchorKind kind = LayoutAnchorKind.Traversal)
        {
            if (kind != LayoutAnchorKind.Traversal && kind != LayoutAnchorKind.Binding) throw new ArgumentOutOfRangeException(nameof(kind));
            Id = LayoutValues.Id(id); ModuleId = LayoutValues.Id(moduleId); LocalPosition = localPosition; Kind = kind;
        }
    }
    public enum LayoutTraversal { Walk, Jump, Dash }
    public sealed class LayoutConnection
    {
        public string Id { get; }
        public string From { get; }
        public string To { get; }
        public LayoutTraversal Traversal { get; }
        // Connections describe bidirectional exploration; they do not lock exits.
        public LayoutConnection(string id, string from, string to, LayoutTraversal traversal = LayoutTraversal.Walk)
        {
            if (traversal != LayoutTraversal.Walk && traversal != LayoutTraversal.Jump && traversal != LayoutTraversal.Dash) throw new ArgumentOutOfRangeException(nameof(traversal));
            Id = LayoutValues.Id(id); From = LayoutValues.Id(from); To = LayoutValues.Id(to); Traversal = traversal;
        }
    }
    public sealed class LayoutPlacement
    {
        public string Id { get; }
        public string ContentId { get; }
        public string ModuleId { get; }
        public LayoutPoint LocalPosition { get; }
        public LayoutPlacement(string id, string contentId, string moduleId, LayoutPoint localPosition)
        { Id = LayoutValues.Id(id); ContentId = LayoutValues.Id(contentId); ModuleId = LayoutValues.Id(moduleId); LocalPosition = localPosition; }
    }
    public sealed class ChapterLayout
    {
        public string Id { get; }
        public string Version { get; }
        public LayoutOrigin Origin { get; }
        public string EntryAnchorId { get; }
        public IReadOnlyList<LayoutModule> Modules { get; }
        public IReadOnlyList<LayoutAnchor> Anchors { get; }
        public IReadOnlyList<LayoutConnection> Connections { get; }
        public IReadOnlyList<LayoutPlacement> Placements { get; }
        public IReadOnlyList<string> RequiredAnchors { get; }
        public IReadOnlyList<LayoutRect> Hazards { get; }
        public LayoutRepetition Repetition { get; }
        public IReadOnlyList<LayoutWitness> Witnesses { get; }
        public string ContentHash { get; }
        public ChapterLayout(string id, string version, LayoutOrigin origin, string entryAnchorId,
            LayoutModule[] modules, LayoutAnchor[] anchors, LayoutConnection[] connections,
            LayoutPlacement[] placements = null, string[] requiredAnchors = null, LayoutRect[] hazards = null,
            LayoutRepetition repetition = null, LayoutWitness[] witnesses = null)
        {
            Id = LayoutValues.Id(id); Version = LayoutValues.Id(version); Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            EntryAnchorId = LayoutValues.Id(entryAnchorId);
            Modules = LayoutValues.Freeze(modules, m => m.Id); Anchors = LayoutValues.Freeze(anchors, a => a.Id);
            Connections = LayoutValues.Freeze(connections, c => c.Id);
            Placements = LayoutValues.Freeze(placements ?? Array.Empty<LayoutPlacement>(), p => p.Id);
            RequiredAnchors = Array.AsReadOnly((requiredAnchors ?? Array.Empty<string>()).Select(LayoutValues.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            if (hazards != null && hazards.Any(r => r.Width <= 0 || r.Height <= 0)) throw new ArgumentException("Hazards require nonempty bounds.", nameof(hazards));
            Hazards = Array.AsReadOnly((hazards ?? Array.Empty<LayoutRect>()).OrderBy(r => r.X).ThenBy(r => r.Y).ThenBy(r => r.Width).ThenBy(r => r.Height).ToArray());
            Repetition = repetition;
            Witnesses = LayoutValues.Freeze(witnesses ?? Array.Empty<LayoutWitness>(), witness => witness.Id);
            ContentHash = LayoutValues.Hash(w => {
                w.Write(1); w.Write(Id); w.Write(Version); w.Write((int)Origin.Kind); w.Write(Origin.ProviderId);
                w.Write(Origin.ProviderVersion); w.Write(Origin.ContentVersion); w.Write(Origin.Seed.HasValue);
                if (Origin.Seed.HasValue) w.Write(Origin.Seed.Value); w.Write(EntryAnchorId);
                w.Write(Modules.Count); foreach (var m in Modules) { w.Write(m.Id); w.Write(m.ContentId); LayoutValues.Point(w, m.Position); }
                w.Write(Anchors.Count); foreach (var a in Anchors) { w.Write(a.Id); w.Write(a.ModuleId); LayoutValues.Point(w, a.LocalPosition); w.Write((int)a.Kind); }
                w.Write(Connections.Count); foreach (var c in Connections) { w.Write(c.Id); w.Write(c.From); w.Write(c.To); w.Write((int)c.Traversal); }
                w.Write(Placements.Count); foreach (var p in Placements) { w.Write(p.Id); w.Write(p.ContentId); w.Write(p.ModuleId); LayoutValues.Point(w, p.LocalPosition); }
                w.Write(RequiredAnchors.Count); foreach (var a in RequiredAnchors) w.Write(a);
                w.Write(Hazards.Count); foreach (var r in Hazards) LayoutValues.Rect(w, r);
                if (Repetition != null || Witnesses.Count != 0)
                {
                    w.Write("movement-proof-v1"); w.Write(Repetition != null);
                    if (Repetition != null)
                    {
                        w.Write(Repetition.Width); w.Write(Repetition.ModulePrefix); w.Write(Repetition.TemplateIds.Count);
                        foreach (var template in Repetition.TemplateIds) w.Write(template);
                    }
                    w.Write(Witnesses.Count);
                    foreach (var witness in Witnesses)
                    {
                        w.Write(witness.Id); LayoutValues.Point(w, witness.Start); w.Write(witness.Facing);
                        LayoutValues.Rect(w, witness.FinishRegion); w.Write(witness.RequireGrounded);
                        w.Write((int)witness.Kind); LayoutValues.Point(w, witness.Destination);
                    }
                }
            });
        }
        public bool TryResolveModule(string moduleId, out LayoutModule module)
        {
            if (Repetition == null)
            {
                module = Modules.FirstOrDefault(candidate => candidate.Id == moduleId);
                return module != null;
            }
            module = null;
            if (!Repetition.TryIndex(moduleId, out int index)) return false;
            string template = Repetition.TemplateIds[index % Repetition.TemplateIds.Count];
            var source = Modules.FirstOrDefault(candidate => candidate.Id == template);
            if (source == null) return false;
            module = new LayoutModule(moduleId, source.ContentId, new LayoutPoint(index * Repetition.Width, source.Position.Y));
            return true;
        }
        public string GeometryModuleId(LayoutModule resolved)
        {
            if (Repetition == null) return resolved.Id;
            if (!Repetition.TryIndex(resolved.Id, out int index)) throw new ArgumentException("Invalid repeated terrain address.");
            return Repetition.TemplateIds[index % Repetition.TemplateIds.Count];
        }
    }
    internal static class LayoutValues
    {
        public static string Id(string value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A stable ID/version is required.");
        public static void Finite(float value) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
        public static IReadOnlyList<T> Freeze<T>(T[] values, Func<T, string> key) where T : class
        {
            if (values == null || values.Any(v => v == null)) throw new ArgumentException("A non-null collection is required.");
            return Array.AsReadOnly(values.OrderBy(key, StringComparer.Ordinal).ToArray());
        }
        public static void Point(BinaryWriter w, LayoutPoint p) { w.Write(p.X); w.Write(p.Y); }
        public static void Rect(BinaryWriter w, LayoutRect r) { w.Write(r.X); w.Write(r.Y); w.Write(r.Width); w.Write(r.Height); }
        public static string Hash(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
