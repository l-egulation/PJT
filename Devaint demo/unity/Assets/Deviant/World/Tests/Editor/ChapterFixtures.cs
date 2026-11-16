using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.World.Contracts;

namespace Deviant.World.Tests
{
    // Isolated fixture content. It defines no product chapter, NPC, loot or save rule.
    public static class ChapterFixtures
    {
        public static LayoutBodyProfile Body(float speed = 5) => new LayoutBodyProfile(.9375f, 2, .1875f, 1, speed, "fixture-movement-v1");
        public static ChapterLayout Good(LayoutOriginKind kind = LayoutOriginKind.Authored)
        {
            return new ChapterLayout("fixture-chapter", "1", new LayoutOrigin(kind, "fixture-provider", "1", "fixture-content-v1", kind == LayoutOriginKind.CapturedGeneration ? 17 : (long?)null), "entry",
                new[] { new LayoutModule("west", "floor-8", new LayoutPoint(0, 0)), new LayoutModule("east", "floor-8", new LayoutPoint(8, 0)) },
                new[] { new LayoutAnchor("entry", "west", new LayoutPoint(1, 0)), new LayoutAnchor("west-exit", "west", new LayoutPoint(7, 0)),
                    new LayoutAnchor("east-entry", "east", new LayoutPoint(1, 0)), new LayoutAnchor("goal", "east", new LayoutPoint(7, 0)),
                    new LayoutAnchor("focus", "east", new LayoutPoint(4, 1), LayoutAnchorKind.Binding) },
                new[] { new LayoutConnection("west-path", "entry", "west-exit"), new LayoutConnection("seam", "west-exit", "east-entry"), new LayoutConnection("east-path", "east-entry", "goal") },
                new[] { new LayoutPlacement("marker-west", "marker", "west", new LayoutPoint(3, 0)), new LayoutPlacement("marker-east", "marker", "east", new LayoutPoint(3, 0)) },
                new[] { "goal", "focus" });
        }
        public static ChapterLayout Copy(ChapterLayout original, LayoutModule[] modules = null, LayoutAnchor[] anchors = null,
            LayoutConnection[] connections = null, LayoutPlacement[] placements = null, string[] required = null, LayoutRect[] hazards = null, string entry = null)
            => new ChapterLayout(original.Id, original.Version, original.Origin, entry ?? original.EntryAnchorId, modules ?? original.Modules.ToArray(), anchors ?? original.Anchors.ToArray(),
                connections ?? original.Connections.ToArray(), placements ?? original.Placements.ToArray(), required ?? original.RequiredAnchors.ToArray(), hazards ?? original.Hazards.ToArray());
        public sealed class CapturedProvider : IChapterLayoutProvider
        {
            public ChapterLayout Result = Good(LayoutOriginKind.CapturedGeneration);
            public int Calls;
            public bool Fail, Throw;
            public LayoutSupply Provide(LayoutRequest request)
            {
                Calls++;
                if (Throw) throw new InvalidOperationException("provider failure");
                return Fail ? LayoutSupply.Failure("captured result unavailable") : LayoutSupply.Success(Result);
            }
        }
        public sealed class Catalog : IChapterModuleCatalog
        {
            public ModuleGeometry Geometry = new ModuleGeometry(new[] { new LayoutRect(0, -1, 8, 1) });
            public bool MissingGeometry, MissingMarker;
            public bool TryGetGeometry(string id, out ModuleGeometry geometry, out string error)
            { geometry = id == "floor-8" && !MissingGeometry ? Geometry : null; error = geometry == null ? "missing fixture geometry" : null; return geometry != null; }
            public bool HasPlacementContent(string id) => id == "marker" && !MissingMarker;
        }
        public sealed class Factory : IChapterViewFactory
        {
            public readonly List<View> Created = new List<View>();
            public readonly Catalog Catalog;
            public int Attempts, FailPrepareAt = -1, FailActivateAt = -1;
            public Action OnActivate, OnDispose;
            public bool ThrowDispose;
            public Factory(Catalog catalog) { Catalog = catalog; }
            public IChapterView PrepareModule(LayoutModule module, string hash)
            {
                if (Catalog.Geometry.ContentHash != hash) throw new InvalidOperationException("fixture geometry changed");
                return Prepare(module.Id);
            }
            public IChapterView PreparePlacement(LayoutPlacement placement, LayoutPoint resolvedPosition) => Prepare(placement.Id);
            View Prepare(string id)
            {
                if (++Attempts == FailPrepareAt) throw new InvalidOperationException("prepare failure");
                var view = new View(this, id, Attempts); Created.Add(view); return view;
            }
            public sealed class View : IChapterView
            {
                readonly Factory factory;
                readonly int ordinal;
                public string Id { get; }
                public bool Active, Disposed;
                public int DisposalCount;
                public View(Factory factory, string id, int ordinal) { this.factory = factory; Id = id; this.ordinal = ordinal; }
                public void Activate()
                {
                    factory.OnActivate?.Invoke();
                    if (ordinal == factory.FailActivateAt) throw new InvalidOperationException("activation failure");
                    Active = true;
                }
                public void Dispose()
                {
                    if (Disposed) return;
                    Disposed = true; Active = false; DisposalCount++; factory.OnDispose?.Invoke();
                    if (factory.ThrowDispose) throw new InvalidOperationException("dispose failure");
                }
            }
        }
    }
}
