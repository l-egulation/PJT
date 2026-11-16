using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Equipment.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class EquipmentMaintenanceIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameRejectsInvalidMaintenanceWithoutChangingTheProfile() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabRejectsInvalidMaintenanceWithoutChangingTheProfile() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Same(WeaponSnapshot before, WeaponSnapshot after)
        {
            Assert.That(after.InstanceId, Is.EqualTo(before.InstanceId)); Assert.That(after.Selection.FormIndex, Is.EqualTo(before.Selection.FormIndex)); Assert.That(after.Selection.SpecialIndex, Is.EqualTo(before.Selection.SpecialIndex));
            Assert.That(after.Stats.Slots, Is.EqualTo(before.Stats.Slots)); Assert.That(after.Charges.Capacity, Is.EqualTo(before.Charges.Capacity)); Assert.That(after.Charges.Available, Is.EqualTo(before.Charges.Available));
            Assert.That(after.Charges.Period, Is.EqualTo(before.Charges.Period)); Assert.That(after.Charges.NextReady, Is.EqualTo(before.Charges.NextReady));
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("EquipmentMaintenanceFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(80, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); player.selected = 0;
                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                    foreach (var w in player.weapons) w.StatSlots.Clear();
                    int sword = Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), dagger = Array.FindIndex(arena.catalog.forms, f => f.id == "dagger"), whip = Array.FindIndex(arena.catalog.forms, f => f.id == "whip");
                    player.Reconfigure(0, sword, 0); var weapon = player.Weapon; weapon.StatSlots[0] = 1; weapon.Cooldown.Spend(arena.Now); var before = weapon.Snapshot; var other = player.weapons[1].Snapshot;
                    Assert.That(player.EditingAllowed, Is.True);
                    // The original void facade must reject without throwing or
                    // storing an out-of-catalog index before it fails.
                    Assert.DoesNotThrow(() => player.Reconfigure(0, arena.catalog.forms.Length, 0)); Same(before, weapon.Snapshot);
                    foreach (var request in new[] { (-1, sword, 0), (2, sword, 0), (0, -1, 0), (0, arena.catalog.forms.Length, 0), (0, sword, -1), (0, sword, 4) })
                    {
                        Assert.That(player.TryReconfigure(request.Item1, request.Item2, request.Item3, out var error), Is.False); Assert.That(error, Is.Not.Null.And.Not.Empty);
                        Same(before, weapon.Snapshot); Same(other, player.weapons[1].Snapshot);
                    }
                    var skill = arena.catalog.forms[sword].specials[0]; float cooldown = skill.cooldown; int capacity = skill.capacity;
                    try
                    {
                        skill.cooldown = float.NaN; Assert.That(player.TryReconfigure(0, sword, 0, out _), Is.False); Same(before, weapon.Snapshot);
                        skill.cooldown = cooldown; skill.capacity = 0; Assert.That(player.TryReconfigure(0, sword, 0, out _), Is.False); Same(before, weapon.Snapshot);
                        skill.capacity = capacity; arena.catalog.forms[sword].specials[0] = null; Assert.That(player.TryReconfigure(0, sword, 0, out _), Is.False); Same(before, weapon.Snapshot);
                    }
                    finally { skill.cooldown = cooldown; skill.capacity = capacity; arena.catalog.forms[sword].specials[0] = skill; }
                    float debt = weapon.Cooldown.Debt(arena.Now); var identity = weapon.InstanceId;
                    Assert.That(player.TryReconfigure(0, dagger, 2, out var validError), Is.True, validError);
                    Assert.That(weapon.InstanceId, Is.EqualTo(identity)); Assert.That(weapon.FormIndex, Is.EqualTo(dagger)); Assert.That(weapon.SpecialIndex, Is.EqualTo(2)); Assert.That(weapon.Cooldown.Capacity, Is.EqualTo(3)); Assert.That(weapon.Cooldown.Debt(arena.Now), Is.EqualTo(debt).Within(.00001f)); Assert.That(weapon.StatSlots[0], Is.EqualTo(1)); Same(other, player.weapons[1].Snapshot);

                    // Existing actor/combat maintenance gates still own access.
                    driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                    player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var action = player.currentCast; var active = weapon.Snapshot;
                    Assert.That(player.TryReconfigure(0, sword, 0, out _), Is.False); Same(active, weapon.Snapshot); Assert.That(player.currentCast, Is.SameAs(action));
                    driver.Reset(); arena.enemies[0].aggressive = true; var blocked = weapon.Snapshot;
                    Assert.That(player.TryReconfigure(0, sword, 0, out _), Is.False); Same(blocked, weapon.Snapshot); arena.enemies[0].aggressive = false;

                    // Invalid maintenance cannot erase an existing recast mark;
                    // successful maintenance ends that mark after committing.
                    player.Reconfigure(0, whip, 2); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                    player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var mark = player.currentCast;
                    for (int tick = 0; tick < 80 && player.currentCast; tick++) driver.Step(.02f);
                    Assert.That(player.currentCast, Is.Null); Assert.That(mark.RecastRemaining, Is.GreaterThan(0)); float remaining = mark.RecastRemaining; var marked = weapon.Snapshot;
                    Assert.That(player.TryReconfigure(0, arena.catalog.forms.Length, 0, out _), Is.False); Same(marked, weapon.Snapshot); Assert.That(mark.RecastRemaining, Is.EqualTo(remaining));
                    Assert.That(player.TryReconfigure(0, sword, 0, out validError), Is.True, validError); Assert.That(mark.RecastRemaining, Is.Zero); Assert.That(weapon.InstanceId, Is.EqualTo(identity));
                    Assert.That(mark.ResourceResult.Snapshot.Key.WeaponId, Is.EqualTo(identity)); Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
