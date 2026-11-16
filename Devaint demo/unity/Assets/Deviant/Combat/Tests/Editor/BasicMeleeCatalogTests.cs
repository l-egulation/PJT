using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class BasicMeleeCatalogTests
    {
        static Catalog Source() => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        [Test] public void ExistingSevenFormsCompileAllSeventeenHitsAndKeepRangedPoliciesSeparate()
        {
            var source = Source(); Assert.That(BasicMeleeCatalog.TryCreate(source, out var catalog, out var error), Is.True, error);
            Assert.That(catalog.Actions.Count, Is.EqualTo(17)); Assert.That(catalog.Actions.Select(a => a.ContentHash).Distinct().Count(), Is.EqualTo(17));
            foreach (var form in source.forms) for (int i = 0; i < form.basics.Length; i++)
            {
                var hit = form.basics[i]; var compiled = catalog.Find(form.id, i);
                if (!string.IsNullOrEmpty(hit.kind)) { Assert.That(compiled, Is.Null); continue; }
                Assert.That(compiled.Id, Is.EqualTo(form.id + "_basic_" + i)); Assert.That(compiled.Damage, Is.EqualTo(hit.damage));
                Assert.That(compiled.Duration, Is.EqualTo(hit.duration)); Assert.That(compiled.Impact, Is.EqualTo(hit.impact)); Assert.That(compiled.Active, Is.EqualTo(hit.active));
                Assert.That(compiled.Width, Is.EqualTo(hit.box.width)); Assert.That(compiled.Height, Is.EqualTo(hit.box.height)); Assert.That(compiled.OffsetX, Is.EqualTo(hit.box.x)); Assert.That(compiled.OffsetY, Is.EqualTo(hit.box.y));
            }
            Assert.That(catalog.Find(null, 0), Is.Null); Assert.That(catalog.Find("dagger", -1), Is.Null); Assert.That(catalog.Find("dagger", 4), Is.Null);
            Assert.That(typeof(BasicMeleeCatalog).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat"), Is.False);
        }
        [Test] public void PublishedDefinitionsAndLookupDoNotFollowSourceOrReturnedDtoEdits()
        {
            var source = Source(); BasicMeleeCatalog.TryCreate(source, out var catalog, out _); var dagger = catalog.Find("dagger", 3); string version = dagger.ContentHash;
            var form = Array.Find(source.forms, f => f.id == "dagger"); form.basics[3].box.width = 99; form.basics[3].damage = 999; form.basics[3] = null; form.id = "edited";
            var copy = dagger.ToData(); copy.damage = 123; copy.box.width = 456;
            Assert.That(catalog.Find("dagger", 3), Is.SameAs(dagger)); Assert.That(catalog.Find("edited", 3), Is.Null); Assert.That(dagger.ContentHash, Is.EqualTo(version));
            Assert.That(dagger.Damage, Is.EqualTo(17)); Assert.That(dagger.Width, Is.EqualTo(3.0625f));
            Assert.Throws<NotSupportedException>(() => ((IList<BasicMeleeDefinition>)catalog.Actions)[0] = dagger);
        }
        [Test] public void EachHitUsesItsOwnScaledTimelineAndDetachedGeometry()
        {
            var source = Source(); BasicMeleeCatalog.TryCreate(source, out var catalog, out _);
            foreach (var form in source.forms) for (int i = 0; i < form.basics.Length; i++)
            {
                var hit = catalog.Find(form.id, i); if (hit == null) continue;
                foreach (float speed in new[] { 1f, 1.5f, 5f })
                {
                    var timeline = new MeleeActionDefinition(hit, speed, form.inputSpeed > 0 ? .65f : 0, Math.Max(0, hit.Impact - .04f) / speed, .08f / speed);
                    Assert.That(timeline.Impact, Is.EqualTo(hit.Impact / speed)); Assert.That(timeline.End, Is.EqualTo(hit.Duration / speed)); Assert.That(timeline.Active, Is.EqualTo(hit.Active / speed));
                    Assert.That(timeline.AdvanceDistance, Is.EqualTo(form.inputSpeed > 0 ? .65f : 0)); var shape = timeline.CreateHit(); shape.box.width = 999;
                    Assert.That(timeline.CreateHit().box.width, Is.EqualTo(hit.Width)); Assert.That(timeline.SourceVersion, Is.EqualTo(hit.ContentHash));
                }
            }
        }
        [TestCase("null")] [TestCase("forms")] [TestCase("duplicate")] [TestCase("missing-hit")] [TestCase("invalid-timing")] [TestCase("invalid-shape")]
        public void InvalidCatalogNeverPublishesAPartialDefinitionSet(string scenario)
        {
            var source = Source();
            switch (scenario)
            {
                case "null": source = null; break;
                case "forms": source.forms = null; break;
                case "duplicate": source.forms[1].id = source.forms[0].id; break;
                case "missing-hit": source.forms[1].basics[0] = null; break;
                case "invalid-timing": source.forms[1].basics[0].duration = .001f; break;
                case "invalid-shape": source.forms[1].basics[0].box.width = float.NaN; break;
            }
            Assert.That(BasicMeleeCatalog.TryCreate(source, out var catalog, out var error), Is.False); Assert.That(catalog, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
    }
}
