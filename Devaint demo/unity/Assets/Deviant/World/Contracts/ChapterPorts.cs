using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.World.Contracts
{
    public sealed class LayoutRequest
    {
        public string ChapterId { get; }
        public string LayoutVersion { get; }
        public long? Seed { get; }
        public string ExpectedResolvedHash { get; }
        public LayoutRequest(string chapterId, string layoutVersion, long? seed = null, string expectedResolvedHash = null)
        {
            ChapterId = LayoutValues.Id(chapterId); LayoutVersion = LayoutValues.Id(layoutVersion); Seed = seed;
            ExpectedResolvedHash = expectedResolvedHash == null ? null : LayoutValues.Id(expectedResolvedHash);
        }
    }
    public sealed class LayoutSupply
    {
        public ChapterLayout Layout { get; }
        public string Error { get; }
        public bool Succeeded => Layout != null;
        LayoutSupply(ChapterLayout layout, string error) { Layout = layout; Error = error; }
        public static LayoutSupply Success(ChapterLayout layout) => new LayoutSupply(layout ?? throw new ArgumentNullException(nameof(layout)), null);
        public static LayoutSupply Failure(string error) => new LayoutSupply(null, LayoutValues.Id(error));
    }
    public interface IChapterLayoutProvider
    {
        // Synchronous supply of a resolved immutable result, without scene mutation.
        // Failure is explicit; no unreviewed fallback/retry is chosen by the caller.
        LayoutSupply Provide(LayoutRequest request);
    }
    public sealed class LayoutBodyProfile
    {
        public float Width { get; }
        public float Height { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float WalkSpeed { get; }
        public string MovementVersion { get; }
        public LayoutMotionProfile Motion { get; }
        public string ContentHash { get; }
        public LayoutBodyProfile(float width, float height, float offsetX, float offsetY, float walkSpeed, string movementVersion, LayoutMotionProfile motion = null)
        {
            LayoutValues.Finite(width); LayoutValues.Finite(height); LayoutValues.Finite(offsetX); LayoutValues.Finite(offsetY); LayoutValues.Finite(walkSpeed);
            if (width <= 0 || height <= 0 || walkSpeed < 0) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width; Height = height; OffsetX = offsetX; OffsetY = offsetY; WalkSpeed = walkSpeed;
            MovementVersion = LayoutValues.Id(movementVersion); Motion = motion;
            ContentHash = LayoutValues.Hash(w => {
                w.Write(width); w.Write(height); w.Write(offsetX); w.Write(offsetY); w.Write(walkSpeed); w.Write(MovementVersion);
                if (Motion != null) { w.Write("motor-proof-v1"); w.Write(Motion.ContentHash); }
            });
        }
        public LayoutRect At(LayoutPoint foot, int facing)
        {
            if (facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(facing));
            return new LayoutRect(foot.X + OffsetX * facing - Width / 2, foot.Y + OffsetY - Height / 2, Width, Height);
        }
    }
    public sealed class ModuleGeometry
    {
        public IReadOnlyList<LayoutRect> Solids { get; }
        public IReadOnlyList<LayoutRect> OneWay { get; }
        public string ContentHash { get; }
        public ModuleGeometry(LayoutRect[] solids, LayoutRect[] oneWay = null)
        {
            if (solids == null || solids.Length == 0 || solids.Any(r => r.Width <= 0 || r.Height <= 0)) throw new ArgumentException("Supported modules require nonempty solid boxes.", nameof(solids));
            Solids = Array.AsReadOnly(solids.OrderBy(r => r.X).ThenBy(r => r.Y).ThenBy(r => r.Width).ThenBy(r => r.Height).ToArray());
            if (oneWay != null && oneWay.Any(r => r.Width <= 0 || r.Height <= 0)) throw new ArgumentException("One-way surfaces require nonempty bounds.", nameof(oneWay));
            OneWay = Array.AsReadOnly((oneWay ?? Array.Empty<LayoutRect>()).OrderBy(r => r.X).ThenBy(r => r.Y).ThenBy(r => r.Width).ThenBy(r => r.Height).ToArray());
            ContentHash = LayoutValues.Hash(w => {
                w.Write(Solids.Count); foreach (var r in Solids) LayoutValues.Rect(w, r);
                if (OneWay.Count != 0) { w.Write("one-way-v1"); w.Write(OneWay.Count); foreach (var r in OneWay) LayoutValues.Rect(w, r); }
            });
        }
    }
    public interface IChapterModuleCatalog
    {
        // Read authoring geometry; do not instantiate an active world to validate it.
        bool TryGetGeometry(string contentId, out ModuleGeometry geometry, out string error);
        bool HasPlacementContent(string contentId);
    }
    public interface IChapterAnchors
    {
        Guid Id { get; }
        // Coordinates are relative to the chapter root, guarded by session identity.
        bool TryGetAnchor(Guid expectedSession, string anchorId, out LayoutPoint position, out LayoutAnchorKind kind);
    }
    public interface IChapterView : IDisposable
    {
        // Creation prepares an inactive view; activation happens after preparation succeeds.
        void Activate();
        // Dispose must be idempotent and deactivate immediately, with no domain changes.
    }
    public interface IChapterViewFactory
    {
        // Recheck geometry hash on load: edits after validation cannot use stale proof.
        IChapterView PrepareModule(LayoutModule module, string expectedGeometryHash);
        IChapterView PreparePlacement(LayoutPlacement placement, LayoutPoint resolvedPosition);
    }
}
