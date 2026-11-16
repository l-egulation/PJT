using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Deviant.World.Contracts;
using NUnit.Framework;

namespace Deviant.World.Tests
{
    public class ChapterLayoutTests
    {
        [Test] public void ChapterRuntimeHasNoUnityCombatUiOrEditorDependency()
        {
            foreach (var assembly in new[] { typeof(ChapterLayout).Assembly, typeof(WorldAssembler).Assembly })
                Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void ResolvedLayoutIsImmutableCanonicalAndCultureIndependent()
        {
            var baseline = ChapterFixtures.Good(); var modules = baseline.Modules.Reverse().ToArray(); var anchors = baseline.Anchors.Reverse().ToArray();
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                var copied = ChapterFixtures.Copy(baseline, modules, anchors, baseline.Connections.Reverse().ToArray());
                modules[0] = new LayoutModule("changed", "other", new LayoutPoint(20, 0));
                Assert.That(copied.ContentHash, Is.EqualTo(baseline.ContentHash));
                Assert.That(copied.Modules.Any(m => m.Id == "changed"), Is.False);
                Assert.Throws<NotSupportedException>(() => ((IList<LayoutModule>)copied.Modules)[0] = modules[0]);
                var shifted = ChapterFixtures.Copy(baseline, modules: new[] { new LayoutModule("west", "floor-8", new LayoutPoint(0, 0)), new LayoutModule("east", "floor-8", new LayoutPoint(9, 0)) });
                Assert.That(shifted.ContentHash, Is.Not.EqualTo(baseline.ContentHash));
                Assert.That(ChapterFixtures.Good(LayoutOriginKind.CapturedGeneration).ContentHash, Is.Not.EqualTo(baseline.ContentHash), "Provenance is part of reproducibility.");
            }
            finally { CultureInfo.CurrentCulture = originalCulture; }
        }
        [Test] public void AuthoredProviderIgnoresSeedAndRequiresExactIdentityAndVersion()
        {
            var layout = ChapterFixtures.Good(); var provider = new AuthoredLayoutProvider(layout);
            Assert.That(provider.Provide(new LayoutRequest(layout.Id, layout.Version, 1)).Layout, Is.SameAs(layout));
            Assert.That(provider.Provide(new LayoutRequest(layout.Id, layout.Version, 999)).Layout, Is.SameAs(layout));
            Assert.That(provider.Provide(new LayoutRequest("unknown", layout.Version)).Succeeded, Is.False);
            Assert.That(provider.Provide(new LayoutRequest(layout.Id, "other")).Succeeded, Is.False);
            Assert.Throws<ArgumentException>(() => new AuthoredLayoutProvider(ChapterFixtures.Good(LayoutOriginKind.CapturedGeneration)));
        }
        [Test] public void InvalidNumericGeometryIsRejectedBeforeItCanBecomeProof()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutPoint(float.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutRect(0, 0, float.PositiveInfinity, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutRect(0, 0, -1, 1));
            Assert.Throws<ArgumentException>(() => new ModuleGeometry(new[] { default(LayoutRect) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutBodyProfile(1, 2, 0, 1, -1, "test"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChapterFixtures.Body().At(default, 0));
            Assert.That(ChapterFixtures.Body(4).ContentHash, Is.Not.EqualTo(ChapterFixtures.Body(5).ContentHash));
        }
        [TestCase("module-duplicate")] [TestCase("anchor-duplicate")] [TestCase("placement-duplicate")]
        [TestCase("cross-duplicate")] [TestCase("entry-missing")] [TestCase("module-missing")]
        [TestCase("link-missing")] [TestCase("binding-link")] [TestCase("required-missing")]
        [TestCase("disconnected")] [TestCase("jump")] [TestCase("dash")]
        public void StructuralAndUnsupportedRoutesCannotCreateAWorld(string fault)
        {
            var layout = ChapterFixtures.Good(); var catalog = new ChapterFixtures.Catalog(); var factory = new ChapterFixtures.Factory(catalog);
            switch (fault)
            {
                case "module-duplicate": layout = ChapterFixtures.Copy(layout, modules: new[] { layout.Modules[0], layout.Modules[0] }); break;
                case "anchor-duplicate": layout = ChapterFixtures.Copy(layout, anchors: layout.Anchors.Concat(new[] { layout.Anchors[0] }).ToArray()); break;
                case "placement-duplicate": layout = ChapterFixtures.Copy(layout, placements: new[] { layout.Placements[0], layout.Placements[0] }); break;
                case "cross-duplicate": layout = ChapterFixtures.Copy(layout, placements: new[] { new LayoutPlacement("west", "marker", "west", default) }); break;
                case "entry-missing": layout = ChapterFixtures.Copy(layout, entry: "missing"); break;
                case "module-missing": layout = ChapterFixtures.Copy(layout, anchors: layout.Anchors.Concat(new[] { new LayoutAnchor("orphan", "missing", default) }).ToArray()); break;
                case "link-missing": layout = ChapterFixtures.Copy(layout, connections: new[] { new LayoutConnection("bad", "entry", "missing") }); break;
                case "binding-link": layout = ChapterFixtures.Copy(layout, connections: new[] { new LayoutConnection("bad", "entry", "focus") }); break;
                case "required-missing": layout = ChapterFixtures.Copy(layout, required: new[] { "missing" }); break;
                case "disconnected": layout = ChapterFixtures.Copy(layout, connections: Array.Empty<LayoutConnection>()); break;
                case "jump": layout = ChapterFixtures.Copy(layout, connections: new[] { new LayoutConnection("jump", "entry", "goal", LayoutTraversal.Jump) }); break;
                case "dash": layout = ChapterFixtures.Copy(layout, connections: new[] { new LayoutConnection("dash", "entry", "goal", LayoutTraversal.Dash) }); break;
            }
            Assert.That(WorldAssembler.TryOpen(new AuthoredLayoutProvider(layout), Request(layout), ChapterFixtures.Body(), catalog, factory, out var session, out var error), Is.False);
            Assert.That(session, Is.Null); Assert.That(error, Is.Not.Empty); Assert.That(factory.Created, Is.Empty);
        }
        [TestCase("start-collision")] [TestCase("gap")] [TestCase("obstruction")] [TestCase("hazard")]
        [TestCase("no-walking")] [TestCase("clearance")] [TestCase("missing-geometry")] [TestCase("missing-marker")]
        public void PhysicalPrerequisitesAreCheckedBeyondGraphConnectivity(string fault)
        {
            var layout = ChapterFixtures.Good(); var catalog = new ChapterFixtures.Catalog(); var profile = ChapterFixtures.Body();
            switch (fault)
            {
                case "start-collision": layout = ChapterFixtures.Copy(layout, anchors: layout.Anchors.Where(a => a.Id != "entry").Concat(new[] { new LayoutAnchor("entry", "west", new LayoutPoint(1, -.1f)) }).ToArray()); break;
                case "gap": layout = ChapterFixtures.Copy(layout, modules: new[] { new LayoutModule("west", "floor-8", default), new LayoutModule("east", "floor-8", new LayoutPoint(9, 0)) }); break;
                case "obstruction": catalog.Geometry = new ModuleGeometry(new[] { new LayoutRect(0, -1, 8, 1), new LayoutRect(4, 0, .2f, 3) }); break;
                case "hazard": layout = ChapterFixtures.Copy(layout, hazards: new[] { new LayoutRect(3.9f, 0, .2f, 1) }); break;
                case "no-walking": profile = ChapterFixtures.Body(0); break;
                case "clearance": catalog.Geometry = new ModuleGeometry(new[] { new LayoutRect(0, -1, 8, 1), new LayoutRect(0, 2.5f, 8, .5f) }); profile = new LayoutBodyProfile(.9375f, 3, .1875f, 1.5f, 5, "tall-fixture"); break;
                case "missing-geometry": catalog.MissingGeometry = true; break;
                case "missing-marker": catalog.MissingMarker = true; break;
            }
            Assert.That(LayoutValidator.TryValidate(layout, profile, catalog, out var validated, out var error), Is.False);
            Assert.That(validated, Is.Null); Assert.That(error, Is.Not.Empty);
        }
        [Test] public void FixedAndCapturedResultsUseTheSameValidatorStateAndAssembler()
        {
            var catalog = new ChapterFixtures.Catalog(); var factory = new ChapterFixtures.Factory(catalog);
            var captured = new ChapterFixtures.CapturedProvider(); var fixedLayout = ChapterFixtures.Good();
            foreach (var provider in new IChapterLayoutProvider[] { new AuthoredLayoutProvider(fixedLayout), captured })
            {
                Assert.That(WorldAssembler.TryOpen(provider, Request(fixedLayout), ChapterFixtures.Body(), catalog, factory, out var session, out var error), Is.True, error);
                using (session)
                {
                    Assert.That(session.TryLoadModule("east", out error), Is.True, error);
                    Assert.That(session.TryLoadModule("west", out error), Is.True, error);
                    Assert.That(session.LoadedModuleCount, Is.EqualTo(2)); Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));
                }
            }
            Assert.That(captured.Calls, Is.EqualTo(1)); Assert.That(factory.Created.All(v => v.Disposed), Is.True);
        }
        [Test] public void ProviderFailureExceptionAndWrongVersionDoNotFallBackOrConstructViews()
        {
            var provider = new ChapterFixtures.CapturedProvider(); var catalog = new ChapterFixtures.Catalog(); var factory = new ChapterFixtures.Factory(catalog);
            provider.Fail = true;
            Assert.That(WorldAssembler.TryOpen(provider, Request(provider.Result), ChapterFixtures.Body(), catalog, factory, out _, out _), Is.False);
            provider.Fail = false; provider.Throw = true;
            Assert.That(WorldAssembler.TryOpen(provider, Request(provider.Result), ChapterFixtures.Body(), catalog, factory, out _, out _), Is.False);
            provider.Throw = false;
            Assert.That(WorldAssembler.TryOpen(provider, new LayoutRequest(provider.Result.Id, "different"), ChapterFixtures.Body(), catalog, factory, out _, out _), Is.False);
            Assert.That(provider.Calls, Is.EqualTo(3), "Each call asks once; no hidden generation retries or fallback."); Assert.That(factory.Created, Is.Empty);
        }
        [Test] public void StreamingDoesNotResetPresenceAndOldSessionCommandsCannotRetireNewPlacements()
        {
            var catalog = new ChapterFixtures.Catalog(); var factory = new ChapterFixtures.Factory(catalog); var session = Open(catalog, factory);
            var oldId = session.Id;
            IChapterAnchors query = session;
            Assert.That(query.TryGetAnchor(oldId, "goal", out var goal, out var kind), Is.True);
            Assert.That(goal.X, Is.EqualTo(15)); Assert.That(goal.Y, Is.Zero); Assert.That(kind, Is.EqualTo(LayoutAnchorKind.Traversal));
            Assert.That(query.TryGetAnchor(oldId, "focus", out _, out kind), Is.True); Assert.That(kind, Is.EqualTo(LayoutAnchorKind.Binding));
            Assert.That(query.TryGetAnchor(Guid.NewGuid(), "goal", out _, out _), Is.False);
            Assert.That(query.TryGetAnchor(oldId, "missing", out _, out _), Is.False);
            Assert.That(session.TryLoadModule("west", out var error), Is.True, error);
            int count = factory.Created.Count; Assert.That(session.TryLoadModule("west", out error), Is.True, error); Assert.That(factory.Created.Count, Is.EqualTo(count));
            var before = session.State.Snapshot();
            Assert.That(session.TryRetirePlacement(Guid.NewGuid(), "marker-west", out _), Is.False);
            Assert.That(session.TryRetirePlacement(session.Id, "marker-west", out error), Is.True, error);
            Assert.That(session.TryRetirePlacement(session.Id, "marker-west", out error), Is.True, error);
            Assert.That(before["marker-west"], Is.EqualTo(PlacementPresence.Present));
            Assert.That(session.TryUnloadModule("west", out error), Is.True, error); Assert.That(session.TryLoadModule("west", out error), Is.True, error);
            Assert.That(factory.Created.Count(v => v.Id == "marker-west"), Is.EqualTo(1), "Reload must not recreate a retired view.");
            Assert.That(session.TryLoadModule("missing", out _), Is.False); Assert.That(session.TryUnloadModule("missing", out _), Is.False);
            session.Dispose(); session.Dispose(); Assert.That(factory.Created.All(v => v.Disposed && v.DisposalCount == 1), Is.True);
            Assert.That(query.TryGetAnchor(oldId, "goal", out _, out _), Is.False);
            using (var next = Open(catalog, factory))
            {
                Assert.That(next.Id, Is.Not.EqualTo(oldId)); Assert.That(next.TryRetirePlacement(oldId, "marker-west", out _), Is.False);
                Assert.That(next.State.ReadPresence("marker-west"), Is.EqualTo(PlacementPresence.Present));
                Assert.That(session.TryLoadModule("west", out _), Is.False); Assert.That(session.TryRetirePlacement(oldId, "marker-east", out _), Is.False);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void FailedLoadReleasesOnlyItsPreparedViewsAndCanRetry(bool activation)
        {
            var catalog = new ChapterFixtures.Catalog(); var factory = new ChapterFixtures.Factory(catalog);
            using (var session = Open(catalog, factory))
            {
                Assert.That(session.TryLoadModule("west", out var error), Is.True, error); var old = factory.Created.ToArray();
                if (activation) factory.FailActivateAt = factory.Attempts + 2; else factory.FailPrepareAt = factory.Attempts + 2;
                Assert.That(session.TryLoadModule("east", out error), Is.False); Assert.That(error, Does.Contain("failure"));
                Assert.That(session.LoadedModuleCount, Is.EqualTo(1)); Assert.That(old.All(v => v.Active && !v.Disposed), Is.True);
                Assert.That(factory.Created.Except(old).All(v => v.Disposed), Is.True);
                Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));
                factory.FailPrepareAt = factory.FailActivateAt = -1;
                Assert.That(session.TryLoadModule("east", out error), Is.True, error);
            }
        }
        [Test] public void RevalidationReusesResolvedLayoutAndNeverAdoptsGeometryEdits()
        {
            var catalog = new ChapterFixtures.Catalog();
            catalog.Geometry = new ModuleGeometry(new[] { new LayoutRect(0, -1, 8, 1), new LayoutRect(0, 2.5f, 8, .5f) });
            var originalGeometry = catalog.Geometry;
            var factory = new ChapterFixtures.Factory(catalog); var provider = new ChapterFixtures.CapturedProvider();
            Assert.That(WorldAssembler.TryOpen(provider, Request(provider.Result), ChapterFixtures.Body(), catalog, factory, out var session, out var error), Is.True, error);
            using (session)
            {
                string frozen = session.ResolvedLayoutHash;
                Assert.That(session.TryLoadModule("west", out error), Is.True, error);
                catalog.Geometry = new ModuleGeometry(new[] { new LayoutRect(0, -1, 8, 1), new LayoutRect(0, 3.5f, 8, .5f) });
                Assert.That(session.TryLoadModule("east", out error), Is.False, "Load rejects stale geometry proof.");
                Assert.That(session.TryRevalidate(ChapterFixtures.Body(4), catalog, out error), Is.False);
                Assert.That(session.TryUnloadModule("west", out error), Is.True, error);
                Assert.That(session.TryRevalidate(ChapterFixtures.Body(4), catalog, out error), Is.False, "Unloading views cannot silently change the resolved map.");
                Assert.That(WorldAssembler.TryOpen(provider, new LayoutRequest(provider.Result.Id, provider.Result.Version, 17, frozen), ChapterFixtures.Body(), catalog, factory, out _, out error), Is.False);
                catalog.Geometry = originalGeometry;
                Assert.That(session.TryRevalidate(ChapterFixtures.Body(4), catalog, out error), Is.True, error);
                var profileHash = session.Profile.ContentHash;
                Assert.That(session.TryRevalidate(new LayoutBodyProfile(.9375f, 3, .1875f, 1.5f, 4, "tall"), catalog, out error), Is.False);
                Assert.That(session.Profile.ContentHash, Is.EqualTo(profileHash)); Assert.That(session.ResolvedLayoutHash, Is.EqualTo(frozen));
                Assert.That(session.Layout, Is.SameAs(provider.Result)); Assert.That(provider.Calls, Is.EqualTo(2), "Only explicit opens ask the provider; movement revalidation never regenerates.");
            }
        }
        [Test] public void ViewCallbacksCannotReenterMutationAndBrokenCleanupStillAttemptsEveryView()
        {
            var catalog = new ChapterFixtures.Catalog(); var factory = new ChapterFixtures.Factory(catalog); var session = Open(catalog, factory);
            factory.OnActivate = () => {
                Assert.That(session.TryLoadModule("east", out _), Is.False); Assert.That(session.TryRetirePlacement(session.Id, "marker-east", out _), Is.False);
                Assert.That(session.TryRevalidate(ChapterFixtures.Body(), catalog, out _), Is.False); Assert.Throws<InvalidOperationException>(() => session.Dispose());
            };
            Assert.That(session.TryLoadModule("west", out var error), Is.True, error);
            Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));
            factory.ThrowDispose = true; session.Dispose(); Assert.That(session.LastCleanupError, Does.Contain("dispose failure"));
            Assert.That(factory.Created.All(v => v.Disposed && v.DisposalCount == 1), Is.True); Assert.That(session.LoadedModuleCount, Is.Zero);
        }
        static LayoutRequest Request(ChapterLayout layout) => new LayoutRequest(layout.Id, layout.Version, layout.Origin.Seed);
        static ChapterSession Open(ChapterFixtures.Catalog catalog, ChapterFixtures.Factory factory)
        {
            var layout = ChapterFixtures.Good();
            Assert.That(WorldAssembler.TryOpen(new AuthoredLayoutProvider(layout), Request(layout), ChapterFixtures.Body(), catalog, factory, out var session, out var error), Is.True, error);
            return session;
        }
    }
}
