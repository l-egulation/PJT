using System;
using System.Collections;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ChargedMeleeIntegrationTests
    {
        [UnityTest] public IEnumerator GameChargedDefinitionKeepsGeometryNearestSelectionAndFailureRecovery() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabChargedDefinitionKeepsGeometryNearestSelectionAndFailureRecovery() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear(); Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), 0, out var error), Is.True, error);
            p.Position = Vector2.zero; p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player;
            var floor = new GameObject("ChargedDefinitionFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (string form in new[] { "rapier", "greatsword" })
                {
                    Setup(arena, driver, form); var definition = arena.GetChargedMelee(form + "_default"); Assert.That(definition, Is.Not.Null); Assert.That(arena.ChargedMeleeCount, Is.EqualTo(2));
                    arena.enemies[1].Position = new Vector2(2.8f, 0); Physics2D.SyncTransforms();
                    var input = definition.ToData(); var cast = CombatCast.Create(arena, p, input, p.Weapon, false, definitionVersion: definition.ContentHash, chargedDefinition: definition); p.currentCast = cast;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.None, true)); Assert.That(cast.ChargedMelee, Is.SameAs(definition)); Assert.That(cast.DefinitionVersion, Is.EqualTo(definition.ContentHash));
                    var original = p.Form.specials[0]; float oldWidth = original.hits[0].box.width, oldTime = original.hits[0].time; string oldTarget = original.hits[0].targets;
                    try
                    {
                        original.hits[0].box.width = .001f; original.hits[0].time = 99; original.hits[0].targets = "all";
                        input.hits[0].box.width = 999; input.hits[0].damage = 999; input.hits = new HitData[0]; input.vertical = "grounded_required";
                        input.movement = new MoveData { type = "dash", distance = 100, speed = 100, primaryPush = true, endDamage = 999 };
                        input.effect = new EffectData { type = "stealth", duration = 99 }; input.invulStart = new[] { 0f }; input.invulEnd = new[] { 99f };
                        p.Weapon.StatSlots[0] = 2;
                        for (int tick = 0; tick < 110; tick++) driver.Step(.02f, new CombatInput(0, CombatButtons.None, true));
                        Assert.That(cast.ImpactTime, Is.EqualTo(definition.Policy.Maximum)); Assert.That(cast.EndTime-cast.StartedAt, Is.EqualTo(definition.Policy.BaseEnd+(definition.Policy.Maximum-definition.Policy.Minimum)));
                        Assert.That(arena.enemies[0].health, Is.EqualTo(100-definition.Policy.MaximumDamage*1.2f).Within(.00001f)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                        Assert.That(arena.enemies[1].health, Is.EqualTo(60)); Assert.That(arena.enemies[1].ReceivedHitCount, Is.Zero); Assert.That(p.Position.x, Is.EqualTo(0).Within(.000001f)); Assert.That(p.Hidden, Is.False); Assert.That(cast.Invulnerable, Is.False);
                        Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(cast.ResourceResult.Snapshot.Key.IsBasic, Is.False);
                    }
                    finally { original.hits[0].box.width = oldWidth; original.hits[0].time = oldTime; original.hits[0].targets = oldTarget; }

                    Setup(arena, driver, form); arena.enemies[0].Position = new Vector2(35, 0); Physics2D.SyncTransforms(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
                    cast = p.currentCast; Assert.That(cast, Is.Not.Null, "No target still starts the charge; the release whiffs.");
                    for (int tick = 0; tick < 100 && p.currentCast; tick++) driver.Step(.02f);
                    Assert.That(cast.ChargeRelease.HasValue, Is.True); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(p.Weapon.Cooldown.Available, Is.Zero);
                    Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero); Assert.That(p.currentCast, Is.Null);
                    Setup(arena, driver, form); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); cast = p.currentCast; arena.enemies[0].Position = new Vector2(35, 0); Physics2D.SyncTransforms();
                    for (int tick = 0; tick < 100 && p.currentCast; tick++) driver.Step(.02f);
                    Assert.That(cast.ChargeRelease.HasValue, Is.True); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded), "A target lost before release still swings.");
                    Assert.That(p.Weapon.Cooldown.Available, Is.Zero); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero); Assert.That(cast.ResourcesActive, Is.False);
                    Setup(arena, driver, form); driver.Step(.02f, new CombatInput(0, CombatButtons.Special, true)); cast = p.currentCast; driver.Reset();
                    Assert.That(cast.ChargeActive, Is.False); Assert.That(cast.ResourcesActive, Is.False); Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
