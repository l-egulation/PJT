using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class ChargedMeleeNumericTests
    {
        static ChargedMeleeDefinition Source(string id)
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text); var form = catalog.forms.Single(value => value.specials.Any(skill => skill.id == id));
            Assert.That(ChargedMeleeDefinition.TryCreate(form.id, form.specials.Single(value => value.id == id), out var result, out var error), Is.True, error); return result;
        }
        [TestCase("rapier_default")] [TestCase("greatsword_default")]
        public void NumericChargeRecompileRetainsPolicyMetadataAndExistingResolveEvaluation(string id)
        {
            var source = Source(id); var numeric = source.ToNumericData(); numeric.minimum += .03f; numeric.maximum += .06f; numeric.minimumDamage += 7; numeric.maximumDamage += 11; numeric.baseEnd += .1f; numeric.box.height += .25f;
            var candidate = source.Recompile(numeric); Assert.That(candidate.SourceVersion, Is.EqualTo(source.SourceVersion)); Assert.That(candidate.ContentHash, Is.Not.EqualTo(source.ContentHash));
            foreach (float elapsed in new[] { -1f, numeric.minimum, (numeric.minimum + numeric.maximum) / 2, numeric.maximum, 90f }) {
                var expected = new ChargeDefinition(id, numeric.minimum, numeric.maximum, numeric.minimumDamage, numeric.maximumDamage, numeric.baseEnd, source.Policy.HoldUntilRelease, source.Policy.ReleaseWindup).Resolve(elapsed); var actual = candidate.Policy.Resolve(elapsed);
                Assert.That(BitConverter.SingleToInt32Bits(actual.ReleaseTime), Is.EqualTo(BitConverter.SingleToInt32Bits(expected.ReleaseTime)));
                Assert.That(BitConverter.SingleToInt32Bits(actual.Damage), Is.EqualTo(BitConverter.SingleToInt32Bits(expected.Damage))); Assert.That(actual.MotionEnd, Is.EqualTo(expected.MotionEnd));
            }
            var payload = candidate.ToData(); Assert.That(payload.activation, Is.EqualTo(payload.chargeMin)); Assert.That(payload.hits[0].time, Is.EqualTo(payload.chargeMin)); Assert.That(payload.hits[0].damage, Is.EqualTo(payload.chargeMinDamage));
            numeric.box.height = 99; Assert.That(candidate.ToNumericData().box.height, Is.Not.EqualTo(99));
            Assert.That(source.Recompile(source.ToNumericData()).ContentHash, Is.EqualTo(source.ContentHash));
        }
        [Test] public void InvalidChargeBoundsAndPolicyChangesCannotMasqueradeAsNumericOnlyCandidates()
        {
            var source = Source("rapier_default"); var invalid = source.ToNumericData(); invalid.maximum = invalid.minimum;
            Assert.Throws<ArgumentException>(() => source.Recompile(invalid)); invalid = source.ToNumericData(); invalid.box.width = float.NaN;
            Assert.Throws<ArgumentException>(() => source.Recompile(invalid)); var payload = source.ToData(); payload.cooldown += 1;
            Assert.That(ChargedMeleeDefinition.TryCreate(source.FormId, payload, out var changed, out var error), Is.True, error); Assert.That(changed.SourceVersion, Is.Not.EqualTo(source.SourceVersion));
        }
    }
}
