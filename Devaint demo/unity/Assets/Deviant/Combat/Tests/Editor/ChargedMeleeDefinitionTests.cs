using System;
using System.Globalization;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ChargedMeleeDefinitionTests
    {
        static SkillData Source(string form = "rapier") => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text).forms.Single(f => f.id == form).specials[0];
        [TestCase("rapier", .2f, .8f, 51, 90, .53f)] [TestCase("greatsword", .25f, 1f, 70, 120, .7f)]
        public void ExistingChargedSkillKeepsItsTimingDamageAndAuthoredArea(string form, float min, float max, float low, float high, float end)
        {
            var source = Source(form); Assert.That(ChargedMeleeDefinition.TryCreate(form, source, out var definition, out var error), Is.True, error);
            Assert.That(definition.Policy.Minimum, Is.EqualTo(min)); Assert.That(definition.Policy.Maximum, Is.EqualTo(max)); Assert.That(definition.Policy.MinimumDamage, Is.EqualTo(low)); Assert.That(definition.Policy.MaximumDamage, Is.EqualTo(high)); Assert.That(definition.Policy.BaseEnd, Is.EqualTo(end));
            var hit = definition.CreateHit(); Assert.That(hit.targets, Is.EqualTo("all")); Assert.That(hit.box.width, Is.EqualTo(source.hits[0].box.width)); Assert.That(hit.box.height, Is.EqualTo(source.hits[0].box.height)); Assert.That(hit.pushSeconds, Is.EqualTo(source.hits[0].pushSeconds));
            Assert.That(definition.ToData().chargeMaxDamage, Is.EqualTo(high)); Assert.That(definition.Cooldown, Is.EqualTo(source.cooldown)); Assert.That(definition.Capacity, Is.EqualTo(source.capacity));
            Assert.That(typeof(ChargedMeleeDefinition).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat"), Is.False);
        }
        [Test] public void SourceAndReturnedDtoCannotMutateTheStartDefinitionOrHash()
        {
            var source = Source(); ChargedMeleeDefinition.TryCreate("rapier", source, out var definition, out _); string version = definition.ContentHash;
            float width = source.hits[0].box.width, height = source.hits[0].box.height;
            source.hits[0].box.width = 99; source.hits[0].targets = "nearest"; source.chargeMaxDamage = 999; source.name = "edited";
            var copy = definition.ToData(); copy.hits[0].box.height = 999; copy.hits[0].targets = "nearest"; copy.chargeMin = 999;
            Assert.That(definition.ContentHash, Is.EqualTo(version)); Assert.That(definition.CreateHit().box.width, Is.EqualTo(width)); Assert.That(definition.CreateHit().box.height, Is.EqualTo(height)); Assert.That(definition.CreateHit().targets, Is.EqualTo("all")); Assert.That(definition.Policy.MaximumDamage, Is.EqualTo(90));
            var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); ChargedMeleeDefinition.TryCreate("rapier", Source(), out var same, out _); Assert.That(same.ContentHash, Is.EqualTo(version)); }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [TestCase("target")] [TestCase("charge")] [TestCase("damage")] [TestCase("control")] [TestCase("movement")] [TestCase("time")] [TestCase("geometry")]
        public void UnsupportedOrInvalidSourceDoesNotPublishAPartialDefinition(string kind)
        {
            var source = Source();
            switch (kind)
            {
                case "target": source.hits[0].targets = "nearest"; break;
                case "charge": source.chargeMax = source.chargeMin; break;
                case "damage": source.hits[0].damage += 1; break;
                case "control": source.hits[0].stun = 1; break;
                case "movement": source.movement = new MoveData { type = "dash", distance = 3 }; break;
                case "time": source.activation += .01f; break;
                case "geometry": source.hits[0].box.width = float.NaN; break;
            }
            Assert.That(ChargedMeleeDefinition.TryCreate("rapier", source, out var definition, out var error), Is.False); Assert.That(definition, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
        [Test] public void FactoryRejectsWrongClassificationVersionAndPayloadBeforeCreatingACast()
        {
            ChargedMeleeDefinition.TryCreate("rapier", Source(), out var definition, out _); var source = definition.ToData(); var weapon = new WeaponState();
            int count = Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, source, weapon, true, definitionVersion: definition.ContentHash, chargedDefinition: definition));
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, source, weapon, false, definitionVersion: "stale", chargedDefinition: definition));
            source.hits[0].box.width = 99; Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, source, weapon, false, definitionVersion: definition.ContentHash, chargedDefinition: definition));
            Assert.That(Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(count));
        }
    }
}
