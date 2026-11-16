using System;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class CatalogDefinitionTests
    {
        [Test]
        public void CatalogLivesInAnEngineIndependentAssembly()
        {
            var assembly = typeof(Catalog).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo("Deviant.Definitions"));
            Assert.That(assembly, Is.Not.EqualTo(typeof(CombatArena).Assembly));
            foreach (var reference in assembly.GetReferencedAssemblies())
                Assert.That(reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal), Is.False,
                    "Definitions must not depend on UnityEngine: " + reference.Name);
        }

        [Test]
        public void GeneratedCatalogKeepsAllFormsAndSwordTimingAfterAssemblyExtraction()
        {
            var source = Resources.Load<TextAsset>("MeleeCatalog");
            Assert.That(source, Is.Not.Null);
            var catalog = JsonUtility.FromJson<Catalog>(source.text);
            Assert.That(catalog.forms, Has.Length.EqualTo(10));
            foreach (var form in catalog.forms)
            {
                Assert.That(form.id, Is.Not.Empty);
                Assert.That(form.basics, Is.Not.Empty);
                Assert.That(form.specials, Has.Length.EqualTo(4));
            }

            var sword = Array.Find(catalog.forms, form => form.id == "base_sword");
            Assert.That(sword, Is.Not.Null);
            Assert.That(sword.basics, Has.Length.EqualTo(2));
            Assert.That(sword.basics[0].duration, Is.EqualTo(.33f));
            Assert.That(sword.basics[0].impact, Is.EqualTo(.13f));
            Assert.That(sword.basics[0].active, Is.EqualTo(.08f));
            Assert.That(sword.basics[0].damage, Is.EqualTo(13));
            Assert.That(sword.basics[0].box.width, Is.EqualTo(3.1875f));

            var serialized = JsonUtility.ToJson(catalog);
            var roundTrip = JsonUtility.FromJson<Catalog>(serialized);
            Assert.That(JsonUtility.ToJson(roundTrip), Is.EqualTo(serialized),
                "Unity JSON round-trip must retain nested compatibility DTO fields");
        }
    }
}
