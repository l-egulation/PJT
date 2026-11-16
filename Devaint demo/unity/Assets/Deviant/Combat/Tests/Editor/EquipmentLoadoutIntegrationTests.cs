using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class EquipmentLoadoutIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameUsesEquipmentSelectionAcrossQueuedSwapDashCancellationRecoveryAndReset() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesEquipmentSelectionAcrossQueuedSwapDashCancellationRecoveryAndReset() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");

        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("EquipmentLoadoutFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(80, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Action prepare = () =>
                    {
                        driver.Reset(); player.selected = 0;
                        foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                        foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                        player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
                        player.Reconfigure(1, Array.FindIndex(arena.catalog.forms, f => f.id == "dagger"), 2);
                        player.weapons[0].StatSlots[0] = 1; player.weapons[1].StatSlots[1] = 3;
                        player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); Physics2D.SyncTransforms();
                    };
                    prepare(); var bindings = player.weapons; var a = bindings[0]; var b = bindings[1]; var ids = player.Loadout.Weapons.Select(w => w.InstanceId).ToArray();
                    Assert.That(player.Weapon, Is.SameAs(a)); Assert.That(player.Loadout.ActiveWeapon.InstanceId, Is.EqualTo(ids[0]));

                    // A pending switch stays pending until this hit completes and
                    // wins over both special and basic reservations.
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var basic = player.currentCast;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Swap | CombatButtons.Special | CombatButtons.Basic));
                    Assert.That(player.Loadout.ActiveSlot, Is.Zero); Assert.That(player.currentCast, Is.SameAs(basic));
                    for (int i = 0; i < 80 && player.selected == 0; i++) driver.Step(.02f);
                    Assert.That(player.Loadout.ActiveSlot, Is.EqualTo(1)); Assert.That(player.currentCast, Is.Null); Assert.That(player.Weapon, Is.SameAs(b));
                    Assert.That(a.Cooldown.Available, Is.EqualTo(1)); player.AttackFinished(basic); Assert.That(player.Loadout.ActiveSlot, Is.EqualTo(1));

                    // A valid dash cancels the special and its pending switch
                    // before activation; selection and charge remain unchanged.
                    prepare(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cancelled = player.currentCast;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Assert.That(player.Loadout.ActiveSlot, Is.Zero);
                    Assert.That(cancelled.ResourceResult, Is.Null); driver.Step(.02f, new CombatInput(0, CombatButtons.Dash));
                    Assert.That(player.Dashing, Is.True); Assert.That(player.currentCast, Is.Null);
                    for (int i = 0; i < 45; i++) driver.Step(.02f);
                    Assert.That(player.Loadout.ActiveSlot, Is.Zero); Assert.That(a.Cooldown.Available, Is.EqualTo(1));

                    // After activation, the queued switch keeps the original
                    // resource owner and that inactive weapon continues recovery.
                    prepare(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var special = player.currentCast;
                    for (int i = 0; i < 60 && special.ResourceResult == null; i++) driver.Step(.02f);
                    var result = special.ResourceResult; Assert.That(result.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(result.Snapshot.Key.WeaponId, Is.EqualTo(ids[0]));
                    float ready = a.Cooldown.NextReady; driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Assert.That(player.Loadout.ActiveSlot, Is.Zero);
                    for (int i = 0; i < 80 && player.selected == 0; i++) driver.Step(.02f);
                    Assert.That(player.Loadout.ActiveSlot, Is.EqualTo(1)); Assert.That(player.Loadout.ActiveWeapon.InstanceId, Is.EqualTo(ids[1]));
                    Assert.That(player.Weapon, Is.SameAs(b)); Assert.That(a.Cooldown.NextReady, Is.EqualTo(ready)); Assert.That(a.Cooldown.Available, Is.Zero); Assert.That(special.ResourceResult, Is.SameAs(result));
                    var beforeRecovery = player.Loadout;
                    for (int i = 0; i < 350 && a.Cooldown.Available == 0; i++) driver.Step(.02f);
                    Assert.That(a.Cooldown.Available, Is.EqualTo(1)); Assert.That(beforeRecovery.Weapons[0].Charges.Available, Is.Zero); Assert.That(beforeRecovery.ActiveSlot, Is.EqualTo(1));

                    driver.Reset();
                    Assert.That(player.weapons, Is.SameAs(bindings)); Assert.That(player.weapons[0], Is.SameAs(a)); Assert.That(player.weapons[1], Is.SameAs(b));
                    Assert.That(player.selected, Is.EqualTo(1)); Assert.That(player.Loadout.ActiveSlot, Is.EqualTo(1)); Assert.That(player.Loadout.Weapons.Select(w => w.InstanceId), Is.EqualTo(ids));
                    Assert.That(a.StatSlots[0], Is.EqualTo(1)); Assert.That(b.StatSlots[1], Is.EqualTo(3)); Assert.That(a.Cooldown.Available, Is.EqualTo(1)); Assert.That(b.Cooldown.Available, Is.EqualTo(3));
                    Assert.Throws<ArgumentOutOfRangeException>(() => player.selected = 2); Assert.That(player.Loadout.ActiveSlot, Is.EqualTo(1));
                    player.InputBlocked = true; driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); player.InputBlocked = false; driver.Step(.02f);
                    Assert.That(player.Loadout.ActiveSlot, Is.EqualTo(1)); driver.Step(.02f, new CombatInput(0, CombatButtons.Swap));
                    Assert.That(player.Loadout.ActiveSlot, Is.Zero); Assert.That(player.Weapon, Is.SameAs(a)); Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
