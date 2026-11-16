using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class DirectMeleeSpecialNumericTests
    {
        static DirectMeleeSpecialDefinition Source(string id)
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var form = catalog.forms.Single(value => value.specials.Any(skill => skill.id == id));
            Assert.That(DirectMeleeSpecialDefinition.TryCreate(form.id, form.specials.Single(value => value.id == id), out var result, out var error), Is.True, error); return result;
        }
        [TestCase("base_sword_default")] [TestCase("rapier_range")] [TestCase("greatsword_range")]
        public void NumericRecompilePreservesPolicyVersionButChangesExecutionContent(string id)
        {
            var source = Source(id); var numeric = source.ToBasicData(); numeric.damage += 7; numeric.impact += .03f; numeric.duration += .1f; numeric.box.height += .25f;
            var candidate = source.Recompile(numeric); Assert.That(candidate.SourceVersion, Is.EqualTo(source.SourceVersion)); Assert.That(candidate.ContentHash, Is.Not.EqualTo(source.ContentHash));
            Assert.That(candidate.Compile().SourceVersion, Is.EqualTo(candidate.ContentHash)); Assert.That(candidate.Compile().Speed, Is.EqualTo(1));
            Assert.That(candidate.ToData().cooldown, Is.EqualTo(source.Cooldown)); Assert.That(candidate.ToData().capacity, Is.EqualTo(source.Capacity));
            numeric.box.height = 99; Assert.That(candidate.ToBasicData().box.height, Is.Not.EqualTo(99));
            Assert.That(source.Recompile(source.ToBasicData()).ContentHash, Is.EqualTo(source.ContentHash));
        }
        [Test] public void ResourceMetadataChangesAreNotMisidentifiedAsNumericOnlyEdits()
        {
            var source = Source("base_sword_default"); var changed = source.ToData(); changed.cooldown += 1;
            Assert.That(DirectMeleeSpecialDefinition.TryCreate(source.FormId, changed, out var policy, out var error), Is.True, error);
            Assert.That(policy.SourceVersion, Is.Not.EqualTo(source.SourceVersion));
            var symmetry = Source("greatsword_range").ToBasicData(); symmetry.box.x = 1;
            Assert.Throws<ArgumentException>(() => Source("greatsword_range").Recompile(symmetry));
            var invalid = source.ToBasicData(); invalid.active = -1; Assert.Throws<ArgumentException>(() => source.Recompile(invalid));
        }
    }
}
