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
    public class EquipmentStatIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameReadsEquipmentStatsAtExistingStartAndActivationBoundaries() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabReadsEquipmentStatsAtExistingStartAndActivationBoundaries() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form = "base_sword", int special = 0)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), special); Assert.That(player.Form.id, Is.EqualTo(form));
            player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static void Activate(CombatArena.ManualSimulation driver, CombatCast cast)
        {
            for (int tick = 0; tick < 100 && cast.ResourceResult == null; tick++) driver.Step(.02f);
            Assert.That(cast.ResourceResult, Is.Not.Null); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("EquipmentStatFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(40, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Setup(arena, driver); var weapon = player.Weapon; var id = weapon.InstanceId; weapon.StatSlots[0] = 3;
                    var startStats = weapon.StatSlots.Snapshot; float duration = arena.BasicSwordFirst.ToData().duration, impact = arena.BasicSwordFirst.ToData().impact, damage = arena.BasicSwordFirst.ToData().damage;
                    arena.enemies[0].Position = new Vector2(2.3f, 0); Physics2D.SyncTransforms(); float health = arena.enemies[0].health;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var basic = player.currentCast; Assert.That(basic.MotionDuration, Is.EqualTo(duration / 1.1f)); Assert.That(basic.ImpactTime, Is.EqualTo(impact / 1.1f));
                    weapon.StatSlots[0] = 0; weapon.StatSlots[1] = 1; Activate(driver, basic); var receipt = basic.ResourceResult;
                    Assert.That(receipt.Snapshot.DamageMultiplier, Is.EqualTo(1.2f)); Assert.That(weapon.Speed, Is.EqualTo(1)); Assert.That(startStats.Speed, Is.EqualTo(1.1f)); Assert.That(startStats.BasicBonus, Is.Zero);
                    weapon.StatSlots.Clear(); for (int tick = 0; tick < 40; tick++) driver.Step(.02f);
                    Assert.That(arena.enemies[0].health, Is.EqualTo(health - damage * 1.2f).Within(.001f)); Assert.That(basic.ResourceResult, Is.SameAs(receipt)); Assert.That(weapon.InstanceId, Is.EqualTo(id));

                    Setup(arena, driver); weapon = player.Weapon; weapon.StatSlots[0] = 4; player.Reconfigure(0, weapon.FormIndex, weapon.SpecialIndex);
                    float period = player.Special.cooldown * .9f; Assert.That(weapon.Cooldown.Period, Is.EqualTo(period));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var special = player.currentCast; weapon.StatSlots[1] = 2; Activate(driver, special); receipt = special.ResourceResult;
                    Assert.That(receipt.Snapshot.DamageMultiplier, Is.EqualTo(1.2f)); float ready = weapon.Cooldown.NextReady;
                    weapon.StatSlots.Clear(); Assert.That(weapon.Cooldown.Period, Is.EqualTo(period)); Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(ready)); Assert.That(receipt.Snapshot.DamageMultiplier, Is.EqualTo(1.2f));
                    player.CancelAction(); weapon.StatSlots.CopyFrom(new[] { 1, 2, 3, 4, 0 }); var preserved = weapon.StatSlots.Snapshot; var copied = (int[])weapon.StatSlots.Clone();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Assert.That(player.selected, Is.EqualTo(1)); Assert.That(player.Weapon.BasicBonus, Is.Zero); Assert.That(player.Weapon.Speed, Is.EqualTo(1));
                    driver.Reset(); Assert.That((int[])weapon.StatSlots.Clone(), Is.EqualTo(copied)); Assert.That(preserved.Slots[0], Is.EqualTo(StatInkKind.BasicDamage)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(weapon.Cooldown.Capacity));

                    // Launcher basics use the basic bonus; its D skills use the special bonus and cooldown reduction.
                    Setup(arena, driver, "launcher", 1); weapon = player.Weapon; weapon.StatSlots.CopyFrom(new[] { 2, 4, 1, 3, 0 }); player.Reconfigure(0, weapon.FormIndex, weapon.SpecialIndex);
                    Assert.That(weapon.SpecialBonus, Is.EqualTo(.2f)); Assert.That(weapon.Reduction, Is.EqualTo(.1f)); Assert.That(weapon.Cooldown.Period, Is.EqualTo(player.Special.cooldown * .9f));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var launcher = player.currentCast; Activate(driver, launcher);
                    Assert.That(launcher.ResourceResult.Snapshot.Key.IsBasic, Is.True); Assert.That(launcher.ResourceResult.Snapshot.DamageMultiplier, Is.EqualTo(1.2f)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(weapon.Cooldown.Capacity));
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
