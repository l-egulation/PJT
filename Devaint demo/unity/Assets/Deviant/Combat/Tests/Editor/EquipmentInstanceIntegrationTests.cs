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
    public class EquipmentInstanceIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameKeepsEquipmentIdentityAndSelectionAcrossActivationSwapMaintenanceAndReset() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabKeepsEquipmentIdentityAndSelectionAcrossActivationSwapMaintenanceAndReset() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("EquipmentInstanceFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(40, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); player.selected = 0;
                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                    foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                    int sword = Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), dagger = Array.FindIndex(arena.catalog.forms, f => f.id == "dagger");
                    player.Reconfigure(0, sword, 0); player.Reconfigure(1, dagger, 2);
                    var a = player.weapons[0]; var b = player.weapons[1]; var idA = a.InstanceId; var idB = b.InstanceId;
                    a.StatSlots[0] = 2; b.StatSlots[1] = 3; var before = a.Snapshot;
                    Assert.That(idA, Is.Not.EqualTo(idB)); Assert.That(before.Selection.FormIndex, Is.EqualTo(sword)); Assert.That(before.Selection.SpecialIndex, Is.Zero);
                    Assert.That(b.Snapshot.Selection.FormIndex, Is.EqualTo(dagger)); Assert.That(b.Cooldown.Capacity, Is.EqualTo(3));
                    player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cast = player.currentCast; Assert.That(cast && !cast.isBasic, Is.True);
                    // The existing active-action maintenance gate remains in the caller.
                    player.Reconfigure(0, dagger, 2); Assert.That(a.Snapshot.Selection.FormIndex, Is.EqualTo(sword));
                    for (int tick = 0; tick < 60 && cast.ResourceResult == null; tick++) driver.Step(.02f);
                    var result = cast.ResourceResult; Assert.That(result.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(result.Snapshot.Key.WeaponId, Is.EqualTo(idA)); Assert.That(result.Snapshot.DamageMultiplier, Is.EqualTo(1.2f));
                    float ready = a.Cooldown.NextReady; player.CancelAction(); driver.Step(.02f, new CombatInput(0, CombatButtons.Swap));
                    Assert.That(player.selected, Is.EqualTo(1)); Assert.That(player.Weapon.InstanceId, Is.EqualTo(idB)); Assert.That(player.Form.id, Is.EqualTo("dagger")); Assert.That(player.Special, Is.SameAs(arena.catalog.forms[dagger].specials[2]));
                    Assert.That(a.InstanceId, Is.EqualTo(idA)); Assert.That(a.Cooldown.Available, Is.Zero); Assert.That(a.Cooldown.NextReady, Is.EqualTo(ready)); Assert.That(cast.ResourceResult, Is.SameAs(result));
                    float debt = a.Cooldown.Debt(arena.Now); player.Reconfigure(0, dagger, 2);
                    Assert.That(a.Snapshot.Selection.FormIndex, Is.EqualTo(dagger)); Assert.That(a.Snapshot.Selection.SpecialIndex, Is.EqualTo(2)); Assert.That(a.InstanceId, Is.EqualTo(idA));
                    Assert.That(a.Cooldown.Capacity, Is.EqualTo(3)); Assert.That(a.Cooldown.Debt(arena.Now), Is.EqualTo(debt).Within(.00001f)); Assert.That(a.SpecialBonus, Is.EqualTo(.2f));
                    Assert.That(before.Selection.FormIndex, Is.EqualTo(sword)); Assert.That(before.Charges.Available, Is.EqualTo(1));
                    var modeA = a.Snapshot.Selection; var modeB = b.Snapshot.Selection; var statsA = (int[])a.StatSlots.Clone(); var statsB = (int[])b.StatSlots.Clone();
                    driver.Reset(); Assert.That(a.InstanceId, Is.EqualTo(idA)); Assert.That(b.InstanceId, Is.EqualTo(idB));
                    Assert.That(a.FormIndex, Is.EqualTo(modeA.FormIndex)); Assert.That(a.SpecialIndex, Is.EqualTo(modeA.SpecialIndex)); Assert.That(b.FormIndex, Is.EqualTo(modeB.FormIndex)); Assert.That(b.SpecialIndex, Is.EqualTo(modeB.SpecialIndex));
                    Assert.That((int[])a.StatSlots.Clone(), Is.EqualTo(statsA)); Assert.That((int[])b.StatSlots.Clone(), Is.EqualTo(statsB)); Assert.That(a.Cooldown.Available, Is.EqualTo(3)); Assert.That(b.Cooldown.Available, Is.EqualTo(3));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var next = player.currentCast;
                    for (int tick = 0; tick < 60 && next.ResourceResult == null; tick++) driver.Step(.02f);
                    Assert.That(next.ResourceResult.Snapshot.Key.WeaponId, Is.EqualTo(idB)); Assert.That(b.Cooldown.Available, Is.EqualTo(2)); Assert.That(a.Cooldown.Available, Is.EqualTo(3)); Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
