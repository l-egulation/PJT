using System;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class JumpGravityScenarioTests
    {
        static Deviant.Combat.Catalog CatalogContext => JsonUtility.FromJson<Deviant.Combat.Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        [Test] public void WholeScenarioVersionTransportRetainsAllFourJumpGravityFieldsAndHash()
        {
            var versions = ExperimentScenarioTests.Versions();
            string json = ExperimentScenarioSource.SerializeVersions(versions);
            var fresh = ExperimentScenarioSource.DeserializeVersions(json, CatalogContext);
            Assert.That(fresh.Movement.ContentHash, Is.EqualTo(versions.Movement.ContentHash));
            Assert.That(fresh.Movement.FirstJumpVelocity, Is.EqualTo(20)); Assert.That(fresh.Movement.SecondJumpVelocity, Is.EqualTo(20));
            Assert.That(fresh.Movement.RiseGravity, Is.EqualTo(66.6666667f)); Assert.That(fresh.Movement.FallGravity, Is.EqualTo(104.1666667f));
            var initial = ExperimentScenarioTests.Initial(versions);
            var reloaded = ExperimentScenarioSource.DeserializeInitial(ExperimentScenarioSource.SerializeInitial(initial), CatalogContext);
            Assert.That(reloaded.Versions.Movement.ContentHash, Is.EqualTo(versions.Movement.ContentHash));
        }
        [Test] public void EmbeddedLegacySchemaNeverInventsMissingJumpGravityDefaults()
        {
            var versions = ExperimentScenarioTests.Versions(); string json = ExperimentScenarioSource.SerializeVersions(versions);
            var wrapped = JsonUtility.FromJson<MovementContainer>(json); Assert.That(wrapped.movement.schemaVersion, Is.EqualTo(2));
            string oldSchema = json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1");
            Assert.That(oldSchema, Is.Not.EqualTo(json)); Assert.Catch<ArgumentException>(() => ExperimentScenarioSource.DeserializeVersions(oldSchema, CatalogContext));
        }
        [Serializable] sealed class MovementContainer { public LocomotionData movement; }
    }
}
