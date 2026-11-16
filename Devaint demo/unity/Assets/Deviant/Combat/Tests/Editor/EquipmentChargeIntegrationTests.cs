using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Equipment.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class EquipmentChargeIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameKeepsEquipmentRecoveryAcrossSwapMaintenanceAndReset() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabKeepsEquipmentRecoveryAcrossSwapMaintenanceAndReset() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form, int special)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), special);
            player.Reconfigure(1, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
            Assert.That(player.Form.id, Is.EqualTo(form)); Assert.That(player.Weapon.SpecialIndex, Is.EqualTo(special));
            player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("EquipmentChargeFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(40, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Setup(arena, driver, "dagger", 2); var weapon = player.Weapon; var identity = weapon.InstanceId;
                    Assert.That(weapon.Cooldown.Snapshot.Capacity, Is.EqualTo(3)); Assert.That(typeof(ChargeState).Assembly.GetName().Name, Is.EqualTo("Deviant.Equipment.Contracts"));
                    float firstReady = -1;
                    for (int use = 0; use < 3; use++)
                    {
                        driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cast = player.currentCast; Assert.That(cast && !cast.isBasic, Is.True);
                        for (int tick = 0; tick < 50 && cast.ResourceResult == null; tick++) driver.Step(.02f);
                        Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(cast.ResourceResult.Snapshot.Key.WeaponId, Is.EqualTo(identity));
                        if (use == 0) firstReady = weapon.Cooldown.NextReady;
                        Assert.That(weapon.Cooldown.Snapshot.Available, Is.EqualTo(2 - use)); Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(firstReady));
                        player.CancelAction(); Assert.That(cast.ResourcesActive, Is.False);
                    }
                    var empty = weapon.Cooldown.Snapshot; driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Assert.That(player.selected, Is.EqualTo(1));
                    while (arena.Now + .00001f < firstReady) driver.Step(.02f); driver.Step(.02f);
                    Assert.That(weapon.Cooldown.Snapshot.Available, Is.EqualTo(1)); Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(firstReady + 5));
                    Assert.That(empty.Available, Is.Zero); Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(player.Weapon.InstanceId, Is.Not.EqualTo(identity));

                    // Explicit test maintenance changes one owner, retaining its normalized debt.
                    Setup(arena, driver, "base_sword", 0); weapon = player.Weapon; identity = weapon.InstanceId;
                    var key = new ActionResourceKey(Guid.NewGuid(), identity, false);
                    using (var activation = new ActionResourceExecution(key, new WeaponActionResourceSource(weapon))) Assert.That(activation.TryActivate(key.ExecutionId, arena.Now).Status, Is.EqualTo(ActionResourceStatus.Succeeded));
                    float ready = weapon.Cooldown.NextReady;
                    for (int tick = 0; tick < 125; tick++) driver.Step(.02f);
                    float debt = weapon.Cooldown.Debt(arena.Now); Assert.That(debt, Is.EqualTo(.5f).Within(.00001f)); Assert.That(player.EditingAllowed, Is.True);
                    player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "dagger"), 2);
                    Assert.That(weapon.Cooldown.Capacity, Is.EqualTo(3)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(weapon.Cooldown.Debt(arena.Now), Is.EqualTo(debt).Within(.00001f));
                    Assert.That(weapon.InstanceId, Is.EqualTo(identity));
                    player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
                    Assert.That(weapon.Cooldown.Available, Is.Zero); Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(ready).Within(.00001f)); Assert.That(weapon.Cooldown.Debt(arena.Now), Is.EqualTo(debt).Within(.00001f));
                    var beforeReset = weapon.Cooldown.Snapshot; driver.Reset();
                    Assert.That(weapon.InstanceId, Is.EqualTo(identity)); Assert.That(weapon.Cooldown.Snapshot.Available, Is.EqualTo(1)); Assert.That(weapon.Cooldown.NextReady, Is.Zero); Assert.That(beforeReset.Available, Is.Zero);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
