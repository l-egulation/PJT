using System;
using System.Globalization;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class DirectMeleeSpecialDefinitionTests
    {
        static SkillData Source(string id = "base_sword_default") => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text).forms.SelectMany(f => f.specials).Single(s => s.id == id);
        [TestCase("base_sword", "base_sword_default", 35, .155f, .37f)]
        [TestCase("rapier", "rapier_range", 70, .3f, .7f)]
        [TestCase("greatsword", "greatsword_range", 100, .45f, 1.05f)]
        public void ExistingSingleHitSpecialCompilesWithoutBasicSpeed(string form, string id, float damage, float impact, float duration)
        {
            var source = Source(id); Assert.That(DirectMeleeSpecialDefinition.TryCreate(form, source, out var definition, out var error), Is.True, error);
            var timeline = definition.Compile(); Assert.That(definition.Damage, Is.EqualTo(damage)); Assert.That(timeline.Impact, Is.EqualTo(impact)); Assert.That(timeline.End, Is.EqualTo(duration));
            Assert.That(timeline.Speed, Is.EqualTo(1)); Assert.That(timeline.AdvanceDistance, Is.Zero); Assert.That(timeline.SourceVersion, Is.EqualTo(definition.ContentHash));
            Assert.That(timeline.CreateHit().box.width, Is.EqualTo(source.hits[0].box.width)); Assert.That(definition.ToData().cooldown, Is.EqualTo(source.cooldown)); Assert.That(definition.Capacity, Is.EqualTo(source.capacity));
            Assert.That(typeof(DirectMeleeSpecialDefinition).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat"), Is.False);
        }
        [Test] public void StartDefinitionAndReturnedPayloadOwnTheirValuesAndHash()
        {
            var source = Source(); DirectMeleeSpecialDefinition.TryCreate("base_sword", source, out var definition, out _); var version = definition.ContentHash;
            source.hits[0].damage = 999; source.hits[0].box.width = 999; source.end = 99; source.name = "edited";
            var data = definition.ToData(); data.hits[0].damage = 333; data.hits[0].box.width = 333; data.invulStart = new[] { 0f };
            Assert.That(definition.ContentHash, Is.EqualTo(version)); Assert.That(definition.Damage, Is.EqualTo(35)); Assert.That(definition.Duration, Is.EqualTo(.37f)); Assert.That(definition.ToData().hits[0].box.width, Is.EqualTo(7.15625f)); Assert.That(definition.ToData().invulStart, Is.Empty);
            DirectMeleeSpecialDefinition.TryCreate("base_sword", source, out var next, out _); Assert.That(next.ContentHash, Is.Not.EqualTo(version));
            var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); DirectMeleeSpecialDefinition.TryCreate("base_sword", Source(), out var same, out _); Assert.That(same.ContentHash, Is.EqualTo(version)); }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [Test] public void FactoryRejectsSpecialPolicyAndVersionMismatchesBeforeAllocatingACast()
        {
            DirectMeleeSpecialDefinition.TryCreate("base_sword", Source(), out var definition, out _); var timeline = definition.Compile(); var data = definition.ToData(); var weapon = new WeaponState();
            int count = UnityEngine.Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, data, weapon, true, definitionVersion: definition.ContentHash, timeline: timeline, specialDefinition: definition));
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, data, weapon, false, definitionVersion: "stale", timeline: timeline, specialDefinition: definition));
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, data, weapon, false, definitionVersion: definition.ContentHash, timeline: timeline));
            data.end += .1f;
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, data, weapon, false, definitionVersion: definition.ContentHash, timeline: timeline, specialDefinition: definition));
            Assert.That(UnityEngine.Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(count));
        }
        [TestCase("charge")] [TestCase("movement")] [TestCase("effect")] [TestCase("defense")] [TestCase("control")] [TestCase("timing")] [TestCase("ranged")] [TestCase("shape")] [TestCase("vertical")]
        public void UnsupportedPoliciesAndInvalidPayloadsNeverPublishADefinition(string policy)
        {
            var source = Source();
            switch (policy)
            {
                case "charge": source.chargeMax = 1; break;
                case "movement": source.movement = new MoveData { type = "dash", distance = 4 }; break;
                case "effect": source.effect = new EffectData { type = "projectile" }; break;
                case "defense": source.invulStart = new[] { 0f }; source.invulEnd = new[] { .1f }; break;
                case "control": source.hits[0].root = .1f; break;
                case "timing": source.activation += .01f; break;
                case "ranged": source.kind = "ranged"; break;
                case "shape": source.hits[0].box.width = float.NaN; break;
                case "vertical": source.vertical = "grounded_required"; break;
            }
            Assert.That(DirectMeleeSpecialDefinition.TryCreate("base_sword", source, out var definition, out var error), Is.False); Assert.That(definition, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
    }
}
