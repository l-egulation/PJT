using System;
using System.Collections;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.CombatLab.Tests
{
    public sealed class LightingZoneIntegrationTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Zone integration fixture").AddComponent<LightingLabComposition>();
            yield return new EnterPlayMode(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Advance(CombatArena.ManualSimulation driver, int ticks = 50)
        { for (int i = 0; i < ticks; i++) driver.Step(.02f); }
        static void Place(LightingLabComposition lab, float x)
        { lab.Arena.Player.Position = new Vector2(x, 0); Physics2D.SyncTransforms(); }
        [UnityTest] public IEnumerator RealMovementCrossesTheZoneWithoutChangingCombatAndResetClearsItsLease()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>();
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                driver.Reset(); lab.SetZones(false); var zone = lab.ZoneDefinitions[0]; int ticks = 0;
                while (lab.Arena.Player.Position.x < zone.Left + zone.BlendWidth + .5f && ticks < 200)
                { driver.Step(.02f, new CombatInput(1)); ticks++; }
                Assert.That(ticks, Is.LessThan(200), "The real motor must reach the diagnostic zone core.");
                var position = lab.Arena.Player.Position; float now = lab.Arena.Now, health = lab.Arena.Player.health;
                float swing = lab.Lantern.State.Angle; int selected = lab.Arena.Player.selected;
                driver.Reset(); lab.SetZones(true);
                for (int i = 0; i < ticks; i++) driver.Step(.02f, new CombatInput(1));
                Assert.That(lab.Arena.Player.Position, Is.EqualTo(position)); Assert.That(lab.Arena.Now, Is.EqualTo(now));
                Assert.That(lab.Arena.Player.health, Is.EqualTo(health)); Assert.That(lab.Arena.Player.selected, Is.EqualTo(selected));
                Assert.That(lab.Lantern.State.Angle, Is.EqualTo(swing)); Assert.That(lab.ActiveZoneCount, Is.EqualTo(1)); Assert.That(lab.ZoneEntryCount, Is.EqualTo(1));
                Assert.That(lab.Arena.Player.Position.x, Is.GreaterThan(zone.Left));
                driver.Reset(); Assert.That(lab.ActiveZoneCount, Is.Zero); Assert.That(lab.ZoneEntryCount, Is.Zero);
                driver.Step(.02f); Assert.That(lab.Director.RequestCount, Is.Zero); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.28f).Within(.001));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BorderJitterRetainsOneRequestUntilThePlayerLeavesTheExitHalo()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var zone = lab.ZoneDefinitions[0];
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                driver.Reset(); lab.SetZones(true); Place(lab, zone.Left + .01f); driver.Step(.02f);
                for (int i = 0; i < 60; i++) { Place(lab, zone.Left + (i % 2 == 0 ? -.01f : .01f)); driver.Step(.02f); }
                Assert.That(lab.ZoneEntryCount, Is.EqualTo(1)); Assert.That(lab.ActiveZoneCount, Is.EqualTo(1)); Assert.That(lab.Director.RequestCount, Is.EqualTo(1));
                Place(lab, zone.Left - zone.ExitHysteresis - .05f); driver.Step(.02f); Assert.That(lab.ActiveZoneCount, Is.Zero);
                Place(lab, zone.Left + .01f); driver.Step(.02f); Assert.That(lab.ZoneEntryCount, Is.EqualTo(2));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator PauseAndRenderResamplingCannotCreateOrReleaseZoneRequests()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var zone = lab.ZoneDefinitions[0];
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                driver.Reset(); lab.SetZones(true); Place(lab, zone.Left + zone.BlendWidth + .5f); Advance(driver);
                float now = lab.Arena.Now, intensity = lab.Director.Current.Intensity;
                lab.SetPaused(true); Place(lab, zone.Left - zone.ExitHysteresis - 1);
                Assert.Throws<InvalidOperationException>(() => driver.Step(.02f), "The manual simulation explicitly rejects paused stepping.");
                for (int i = 0; i < 20; i++) lab.SamplePresentation();
                yield return null; Assert.That(lab.Arena.Now, Is.EqualTo(now)); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(intensity));
                Assert.That(lab.ActiveZoneCount, Is.EqualTo(1)); Assert.That(lab.ZoneEntryCount, Is.EqualTo(1));
                lab.SetPaused(false); driver.Step(.02f); Assert.That(lab.ActiveZoneCount, Is.Zero);
                Advance(driver); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.28f).Within(.001));
            }
        }
        [UnityTest] public IEnumerator OverrideReleaseUsesTheZoneAtTheTimeOfReleaseNotTheOldEntryState()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var zone = lab.ZoneDefinitions[0];
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                driver.Reset(); lab.SetZones(true); Place(lab, zone.Left + zone.BlendWidth + .5f); Advance(driver);
                lab.SetSunset(true); Advance(driver); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.65f).Within(.001));
                Place(lab, zone.Left - zone.ExitHysteresis - 1); Advance(driver); Assert.That(lab.ActiveZoneCount, Is.Zero);
                lab.SetSunset(false); Advance(driver); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.28f).Within(.001));
                lab.SetSunset(true); Advance(driver); Place(lab, zone.Left + zone.BlendWidth + .5f); Advance(driver);
                Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.65f).Within(.001));
                lab.SetSunset(false); Advance(driver); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.46f).Within(.001));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator TurningZonesOffOrDisablingTheirOwnerPreservesIndependentRequests()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var zone = lab.ZoneDefinitions[0];
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                driver.Reset(); lab.SetZones(true); Place(lab, zone.Left + zone.BlendWidth + .5f); driver.Step(.02f);
                using (var external = lab.Director.RequestProfile("independent", new LightingProfile(new LightingProfileData { id = "other", color = new[] { 1f, 1f, 1f }, intensity = .9f }), 20, 0))
                {
                    lab.SetZones(false); Assert.That(lab.Director.RequestCount, Is.EqualTo(1)); Assert.That(external.IsActive, Is.True);
                    lab.SetZones(true); driver.Step(.02f); Assert.That(lab.Director.RequestCount, Is.EqualTo(2));
                    lab.enabled = false; Assert.That(lab.Director.RequestCount, Is.EqualTo(1)); Assert.That(lab.ActiveZoneCount, Is.Zero);
                    driver.Step(.02f); Assert.That(lab.Director.RequestCount, Is.EqualTo(1));
                    lab.enabled = true; driver.Step(.02f); Assert.That(lab.Director.RequestCount, Is.EqualTo(2));
                    Assert.That(external.IsActive, Is.True); Assert.That(lab.Director.Current.Intensity, Is.EqualTo(.9f).Within(.001));
                }
            }
            yield return null;
        }
        [Test] public void CatalogRequiresExplicitZonesAndRejectsUnknownProfilesAndDuplicatePlacementIds()
        {
            string json = Resources.Load<TextAsset>("Lighting/Lab").text;
            var data = JsonUtility.FromJson<LightingCatalogData>(json); var catalog = new LightingCatalog(data);
            float left = catalog.Zones[0].Left; data.zones[0].bounds[0] = -500; Assert.That(catalog.Zones[0].Left, Is.EqualTo(left));
            data = JsonUtility.FromJson<LightingCatalogData>(json); data.zones = null; Assert.Throws<ArgumentException>(() => new LightingCatalog(data));
            data = JsonUtility.FromJson<LightingCatalogData>(json); data.zones[0].profile = "missing"; Assert.Throws<ArgumentException>(() => new LightingCatalog(data));
            data = JsonUtility.FromJson<LightingCatalogData>(json); data.zones = new[] { data.zones[0], data.zones[0] }; Assert.Throws<ArgumentException>(() => new LightingCatalog(data));
            data = JsonUtility.FromJson<LightingCatalogData>(json); data.schema = 4; Assert.Throws<ArgumentException>(() => new LightingCatalog(data));
            data = JsonUtility.FromJson<LightingCatalogData>(json); data.zones = Array.Empty<LightingZoneData>(); Assert.That(new LightingCatalog(data).Zones, Is.Empty);
        }
    }
}
