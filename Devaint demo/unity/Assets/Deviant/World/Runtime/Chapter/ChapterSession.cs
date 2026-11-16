using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.World.Contracts;

namespace Deviant.World
{
    public enum PlacementPresence { Present, Retired }
    public sealed class ChapterState
    {
        readonly Dictionary<string, PlacementPresence> presence;
        internal ChapterState(ChapterLayout layout) { presence = layout.Placements.ToDictionary(p => p.Id, p => PlacementPresence.Present, StringComparer.Ordinal); }
        public PlacementPresence ReadPresence(string id) => presence[id];
        public IReadOnlyDictionary<string, PlacementPresence> Snapshot() => new System.Collections.ObjectModel.ReadOnlyDictionary<string, PlacementPresence>(new Dictionary<string, PlacementPresence>(presence));
        internal void Retire(string id) { presence[id] = PlacementPresence.Retired; }
    }
    public sealed class ChapterSession : IChapterAnchors, IDisposable
    {
        sealed class Loaded
        {
            public IChapterView module;
            public readonly Dictionary<string, IChapterView> placements = new Dictionary<string, IChapterView>(StringComparer.Ordinal);
        }
        readonly IChapterViewFactory factory;
        readonly IChapterTraversalProof traversalProof;
        readonly Dictionary<string, Loaded> loaded = new Dictionary<string, Loaded>(StringComparer.Ordinal);
        ValidatedChapterLayout validated;
        bool busy;
        public Guid Id { get; } = Guid.NewGuid();
        public ChapterLayout Layout => validated.Layout;
        public LayoutBodyProfile Profile => validated.Profile;
        public string ResolvedLayoutHash => validated.ResolvedHash;
        public LayoutProofResult MovementProof => validated.MovementProof;
        public ChapterState State { get; }
        public bool IsActive { get; private set; } = true;
        public int LoadedModuleCount => loaded.Count;
        public bool IsModuleLoaded(string moduleId) => IsActive && loaded.ContainsKey(moduleId);
        public string LastCleanupError { get; private set; }
        internal ChapterSession(ValidatedChapterLayout validated, IChapterViewFactory factory, IChapterTraversalProof traversalProof)
        { this.validated = validated; this.factory = factory; this.traversalProof = traversalProof; State = new ChapterState(Layout); }
        public bool TryGetAnchor(Guid expectedSession, string anchorId, out LayoutPoint position, out LayoutAnchorKind kind)
        {
            position = default; kind = default;
            if (!IsActive || expectedSession != Id) return false;
            var anchor = Layout.Anchors.FirstOrDefault(a => a.Id == anchorId);
            if (anchor == null) return false;
            var module = Layout.Modules.First(m => m.Id == anchor.ModuleId);
            position = module.Position.Add(anchor.LocalPosition); kind = anchor.Kind; return true;
        }
        public bool TryLoadModule(string moduleId, out string error)
        {
            error = null;
            if (!Available(out error)) return false;
            if (!Layout.TryResolveModule(moduleId, out var module)) { error = "Unknown module: " + moduleId; return false; }
            if (loaded.ContainsKey(moduleId)) return true;
            var pending = new Loaded(); busy = true;
            try
            {
                pending.module = factory.PrepareModule(module, validated.GeometryHashes[Layout.GeometryModuleId(module)]) ?? throw new InvalidOperationException("Module factory returned no view.");
                foreach (var placement in Layout.Placements.Where(p => p.ModuleId == module.Id && State.ReadPresence(p.Id) == PlacementPresence.Present))
                    pending.placements.Add(placement.Id, factory.PreparePlacement(placement, module.Position.Add(placement.LocalPosition)) ?? throw new InvalidOperationException("Placement factory returned no view."));
                pending.module.Activate(); foreach (var view in pending.placements.Values) view.Activate();
                loaded.Add(moduleId, pending); return true;
            }
            catch (Exception exception)
            {
                Release(pending); error = "Module load failed: " + exception.Message;
                if (LastCleanupError != null) error += " / cleanup: " + LastCleanupError;
                return false;
            }
            finally { busy = false; }
        }
        public bool TryUnloadModule(string moduleId, out string error)
        {
            error = null;
            if (!Available(out error)) return false;
            if (!Layout.TryResolveModule(moduleId, out _)) { error = "Unknown module: " + moduleId; return false; }
            if (!loaded.TryGetValue(moduleId, out var views)) return true;
            busy = true;
            try { loaded.Remove(moduleId); Release(views); error = LastCleanupError; return error == null; }
            finally { busy = false; }
        }
        // A view reports a fact to its original session. No HP, loot, pickup,
        // respawn or permanent-save policy is inferred from this generic marker.
        public bool TryRetirePlacement(Guid expectedSession, string placementId, out string error)
        {
            error = null;
            if (!Available(out error)) return false;
            if (expectedSession != Id) { error = "Stale chapter session."; return false; }
            var placement = Layout.Placements.FirstOrDefault(p => p.Id == placementId);
            if (placement == null) { error = "Unknown placement: " + placementId; return false; }
            busy = true;
            try
            {
                State.Retire(placementId);
                if (loaded.TryGetValue(placement.ModuleId, out var views) && views.placements.TryGetValue(placementId, out var view))
                { views.placements.Remove(placementId); LastCleanupError = null; DisposeView(view); error = LastCleanupError; }
                return error == null;
            }
            finally { busy = false; }
        }
        public bool TryRevalidate(LayoutBodyProfile profile, IChapterModuleCatalog catalog, out string error)
        {
            if (!TryPrepareRevalidation(profile, catalog, out var candidate, out error)) return false;
            using (candidate) return candidate.TryCommit(out error);
        }
        public bool TryPrepareRevalidation(LayoutBodyProfile profile, IChapterModuleCatalog catalog, out Revalidation candidate, out string error)
        {
            candidate = null;
            if (!Available(out error)) return false;
            busy = true;
            try
            {
                if (!LayoutValidator.TryValidate(Layout, profile, catalog, out var proof, out error, traversalProof)) return false;
                if (Layout.Modules.Any(module => proof.GeometryHashes[module.Id] != validated.GeometryHashes[module.Id]))
                { error = "Module geometry changed; open an explicitly new layout session."; return false; }
                candidate = new Revalidation(this, validated, proof, catalog); return true;
            }
            finally { busy = false; }
        }
        public sealed class Revalidation : IDisposable
        {
            ChapterSession owner;
            readonly ValidatedChapterLayout expected, candidate;
            readonly IChapterModuleCatalog catalog;
            internal Revalidation(ChapterSession owner, ValidatedChapterLayout expected, ValidatedChapterLayout candidate, IChapterModuleCatalog catalog)
            { this.owner = owner; this.expected = expected; this.candidate = candidate; this.catalog = catalog; }
            public bool TryCommit(out string error)
            {
                error = null;
                if (owner == null || !owner.Available(out error) || !ReferenceEquals(owner.validated, expected))
                { error = error ?? "The prepared movement proof is stale."; return false; }
                var target = owner;
                target.busy = true;
                try
                {
                    foreach (var module in candidate.Layout.Modules)
                        if (!catalog.TryGetGeometry(module.ContentId, out var geometry, out error) || geometry.ContentHash != candidate.GeometryHashes[module.Id])
                        { error = error ?? "The prepared layout geometry changed."; return false; }
                    target.validated = candidate; owner = null; return true;
                }
                finally { target.busy = false; }
            }
            public void Dispose() { owner = null; }
        }
        public void Dispose()
        {
            if (!IsActive) return;
            if (busy) throw new InvalidOperationException("Chapter mutations cannot reenter view callbacks.");
            busy = true; IsActive = false; LastCleanupError = null;
            try
            {
                foreach (var views in loaded.Values.ToArray()) Release(views, false);
                loaded.Clear();
            }
            finally { busy = false; }
        }
        bool Available(out string error)
        {
            error = !IsActive ? "Chapter session has ended." : busy ? "Chapter mutations cannot reenter view callbacks." : null;
            return error == null;
        }
        void Release(Loaded views, bool clearError = true)
        {
            if (clearError) LastCleanupError = null;
            foreach (var view in views.placements.Values.Reverse()) DisposeView(view);
            if (views.module != null) DisposeView(views.module);
        }
        void DisposeView(IChapterView view)
        {
            try { view.Dispose(); }
            catch (Exception exception) { LastCleanupError = (LastCleanupError == null ? "" : LastCleanupError + "; ") + exception.Message; }
        }
    }
    public static class WorldAssembler
    {
        public static bool TryOpen(IChapterLayoutProvider provider, LayoutRequest request, LayoutBodyProfile profile,
            IChapterModuleCatalog catalog, IChapterViewFactory factory, out ChapterSession session, out string error, IChapterTraversalProof traversalProof = null)
        {
            session = null; error = null;
            try
            {
                if (provider == null || request == null || factory == null) { error = "Provider, request and view factory are required."; return false; }
                var supplied = provider.Provide(request);
                if (supplied == null || !supplied.Succeeded) { error = supplied?.Error ?? "Provider returned no result."; return false; }
                if (supplied.Layout.Id != request.ChapterId || supplied.Layout.Version != request.LayoutVersion)
                { error = "Provider returned a different chapter/version."; return false; }
                if (!LayoutValidator.TryValidate(supplied.Layout, profile, catalog, out var validated, out error, traversalProof)) return false;
                if (request.ExpectedResolvedHash != null && request.ExpectedResolvedHash != validated.ResolvedHash)
                { error = "Resolved layout geometry does not match the requested snapshot."; return false; }
                session = new ChapterSession(validated, factory, traversalProof); return true;
            }
            catch (Exception exception) { error = "Chapter open failed: " + exception.Message; return false; }
        }
    }
}
