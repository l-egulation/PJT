using System;
using System.Linq;
using Deviant.World.Contracts;
using NUnit.Framework;

namespace Deviant.World.Tests
{
    public class RepeatingLayoutTests
    {
        sealed class Proof : IChapterTraversalProof
        {
            public int Calls;
            public LayoutProofResult LastResult { get; private set; }
            public bool TryValidate(ChapterLayout layout, LayoutBodyProfile profile, IChapterModuleCatalog catalog, out string error)
            {
                Calls++; error = null;
                LastResult = new LayoutProofResult(layout.ContentHash, profile.ContentHash, new[] {
                    new LayoutWitnessResult("fixture-proof", profile.ContentHash, "fixture-input", "fixture-trace", 1, true, null) });
                return true;
            }
        }
        static LayoutBodyProfile Body(float speed = 5) => new LayoutBodyProfile(.9375f, 2, .1875f, 1, speed, "fixture-movement",
            new LayoutMotionProfile(.02f, 20, 20, 66.66667f, 104.1667f, 15, .2f, 1, .15f, 5, .1f, "fixture-motor"));
        static ChapterLayout Layout(LayoutOriginKind kind = LayoutOriginKind.Authored)
            => new ChapterLayout("repeat-fixture", "1", new LayoutOrigin(kind, "fixture-repeat", "1", "fixture-content"), "entry",
                new[] { new LayoutModule("template", "floor-8", default) },
                new[] { new LayoutAnchor("entry", "template", new LayoutPoint(1, 0)) }, Array.Empty<LayoutConnection>(),
                repetition: new LayoutRepetition(8, "strip/", new[] { "template" }),
                witnesses: new[] { new LayoutWitness("flat-compatibility", new LayoutPoint(1, .03f), 1, LayoutWitnessKind.Walk,
                    new LayoutPoint(7, 0), new LayoutRect(6.95f, -.06f, .1f, .12f)) });

        [Test] public void RepetitionFreezesTemplateOrderAndResolvesAbsoluteAddressesWithoutFiniteFakeExtent()
        {
            var layout = Layout();
            Assert.That(layout.TryResolveModule("strip/0", out var first), Is.True);
            Assert.That(layout.TryResolveModule("strip/8", out var repeated), Is.True);
            Assert.That(first.Id, Is.Not.EqualTo(repeated.Id)); Assert.That(first.ContentId, Is.EqualTo(repeated.ContentId));
            Assert.That(first.Position.X, Is.Zero); Assert.That(repeated.Position.X, Is.EqualTo(64));
            Assert.That(layout.TryResolveModule("strip/0008", out _), Is.False);
            Assert.That(layout.TryResolveModule("strip/-1", out _), Is.False);
            Assert.That(layout.TryResolveModule("strip/2147483648", out _), Is.False);
            Assert.That(layout.TryResolveModule("strip/20000000", out var far), Is.True);
            Assert.That(far.Position.X, Is.EqualTo(160000000));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.Repetition.ModuleId(-1));
            var templates = new[] { "template" }; var repeat = new LayoutRepetition(8, "strip/", templates); templates[0] = "edited";
            Assert.That(repeat.TemplateIds[0], Is.EqualTo("template"));
        }
        [Test] public void CommonAssemblerRetainsOneSessionAcrossRepeatedModuleUnloadReloadAndProfileRevalidation()
        {
            var layout = Layout(); var provider = new RepeatingLayoutProvider(layout);
            var catalog = new ChapterFixtures.Catalog(); var factory = new ChapterFixtures.Factory(catalog); var proof = new Proof();
            Assert.That(WorldAssembler.TryOpen(provider, new LayoutRequest(layout.Id, layout.Version), Body(), catalog, factory, out var session, out var error, proof), Is.True, error);
            using (session)
            {
                var id = session.Id; string hash = session.ResolvedLayoutHash;
                Assert.That(session.TryLoadModule("strip/0", out error), Is.True, error);
                Assert.That(session.TryLoadModule("strip/8", out error), Is.True, error);
                Assert.That(session.LoadedModuleCount, Is.EqualTo(2));
                Assert.That(factory.Created.Select(view => view.Id), Is.EqualTo(new[] { "strip/0", "strip/8" }));
                Assert.That(session.TryUnloadModule("strip/0", out error), Is.True, error);
                Assert.That(session.TryLoadModule("strip/0", out error), Is.True, error);
                Assert.That(session.Id, Is.EqualTo(id)); Assert.That(session.Layout, Is.SameAs(layout));
                Assert.That(session.TryRevalidate(Body(4), catalog, out error), Is.True, error);
                Assert.That(session.Id, Is.EqualTo(id)); Assert.That(session.ResolvedLayoutHash, Is.EqualTo(hash));
                Assert.That(session.Profile.WalkSpeed, Is.EqualTo(4)); Assert.That(proof.Calls, Is.EqualTo(2));
                Assert.That(session.MovementProof.ProfileHash, Is.EqualTo(session.Profile.ContentHash));
            }
        }
        [Test] public void AuthoredAndCapturedRepeatedResultsUseCommonStructureChecksBeforeExplicitProofAndAnyView()
        {
            foreach (var kind in new[] { LayoutOriginKind.Authored, LayoutOriginKind.CapturedGeneration })
            {
                var layout = Layout(kind); var catalog = new ChapterFixtures.Catalog(); var proof = new Proof();
                var broken = new ChapterLayout(layout.Id, layout.Version, layout.Origin, "missing-entry", layout.Modules.ToArray(),
                    layout.Anchors.ToArray(), layout.Connections.ToArray(), repetition: layout.Repetition, witnesses: layout.Witnesses.ToArray());
                var factory = new ChapterFixtures.Factory(catalog);
                Assert.That(WorldAssembler.TryOpen(new RepeatingLayoutProvider(broken), new LayoutRequest(layout.Id, layout.Version), Body(), catalog, factory,
                    out var session, out _, proof), Is.False);
                Assert.That(session, Is.Null); Assert.That(proof.Calls, Is.Zero); Assert.That(factory.Created, Is.Empty);
                Assert.That(WorldAssembler.TryOpen(new RepeatingLayoutProvider(layout), new LayoutRequest(layout.Id, layout.Version), Body(), catalog, factory,
                    out session, out _, null), Is.False, "No implicit flat or unvalidated fallback is selected for repeated terrain.");
                Assert.That(factory.Created, Is.Empty);
            }
        }
        [Test] public void OneWayGeometryAndCapturedMotionArePartOfTheirOwnFrozenHashesNotMapRegeneration()
        {
            var solids = new[] { new LayoutRect(0, -1, 8, 1) };
            var solid = new ModuleGeometry(solids); var oneWay = new ModuleGeometry(solids, new[] { new LayoutRect(2, 2, 4, .24f) });
            Assert.That(solid.ContentHash, Is.Not.EqualTo(oneWay.ContentHash));
            Assert.That(oneWay.Solids.Count, Is.EqualTo(1)); Assert.That(oneWay.OneWay.Count, Is.EqualTo(1));
            var layout = Layout(); string map = layout.ContentHash;
            Assert.That(Body(5).ContentHash, Is.Not.EqualTo(Body(4).ContentHash)); Assert.That(layout.ContentHash, Is.EqualTo(map));
            var catalog = new ChapterFixtures.Catalog { Geometry = oneWay };
            Assert.That(LayoutValidator.TryValidate(ChapterFixtures.Good(), ChapterFixtures.Body(), catalog, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("One-way"));
        }
    }
}
