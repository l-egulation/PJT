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
    public class ActionResourceIntegrationTests
    {
        sealed class Source : IActionResourceSource
        {
            readonly IActionResourceSource inner;
            public int Calls;
            public bool Reject;
            public Source(WeaponState weapon) { inner = new WeaponActionResourceSource(weapon); }
            public ActionResourceResult TryActivate(ActionResourceKey key, float now)
            { Calls++; return Reject ? new ActionResourceResult(ActionResourceStatus.Rejected, reason: "fixture denied") : inner.TryActivate(key, now); }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GamePreservesActivationConsumptionSnapshotsAndWeaponRecovery() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabPreservesActivationConsumptionSnapshotsAndWeaponRecovery() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form = "base_sword", int special = 0)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), special);
            player.Reconfigure(1, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0);
            Assert.That(player.Form.id, Is.EqualTo(form)); Assert.That(player.Weapon.SpecialIndex, Is.EqualTo(special));
            player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static ActionResourceResult Activate(CombatArena.ManualSimulation driver, CombatCast cast)
        {
            for (int i = 0; i < 100 && cast.ResourceResult == null; i++) driver.Step(.02f);
            Assert.That(cast.ResourceResult, Is.Not.Null); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); return cast.ResourceResult;
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("ActionResourceFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(40, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    // Basic first-hit timing is fixed at start, damage at activation.
                    Setup(arena, driver); arena.enemies[0].Position = new Vector2(2.3f, 0); Physics2D.SyncTransforms();
                    float damage = arena.BasicSwordFirst.ToData().damage, health = arena.enemies[0].health;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var basic = player.currentCast; var weapon = player.Weapon;
                    Assert.That(basic.ResourceResult, Is.Null); weapon.StatSlots[0] = 1; var basicReceipt = Activate(driver, basic);
                    Assert.That(basicReceipt.Snapshot.Key.ExecutionId, Is.EqualTo(basic.ExecutionId)); Assert.That(basicReceipt.Snapshot.Key.WeaponId, Is.EqualTo(weapon.InstanceId)); Assert.That(basicReceipt.Snapshot.DamageMultiplier, Is.EqualTo(1.2f));
                    weapon.StatSlots.Clear(); for (int i = 0; i < 40; i++) driver.Step(.02f);
                    Assert.That(arena.enemies[0].health, Is.EqualTo(health - damage * 1.2f).Within(.001f)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(weapon.Cooldown.Capacity)); Assert.That(basic.ResourceResult, Is.SameAs(basicReceipt)); Assert.That(basic.ResourcesActive, Is.False);

                    // Windup cancellation is free; activation owns the first spend.
                    Setup(arena, driver); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var before = player.currentCast;
                    Assert.That(before && !before.isBasic, Is.True); Assert.That(before.ResourceResult, Is.Null); Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Dash)); Assert.That(before.ResourcesActive, Is.False); Assert.That(before.ResourceResult, Is.Null); Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1));

                    // An independent after-image keeps its activation damage after cancel/swap/edit.
                    Setup(arena, driver, special: 3); weapon = player.Weapon; var identity = weapon.InstanceId; weapon.StatSlots[0] = 2;
                    arena.enemies[0].Position = new Vector2(2.3f, 0); Physics2D.SyncTransforms(); health = arena.enemies[0].health;
                    float expected = player.Special.hits[0].damage * 1.2f + player.Special.hits[1].damage * 1.2f;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var follow = player.currentCast; var receipt = Activate(driver, follow);
                    Assert.That(weapon.Cooldown.Available, Is.Zero); float ready = weapon.Cooldown.NextReady;
                    player.CancelAction(); for (int i = 0; i < weapon.StatSlots.Length; i++) weapon.StatSlots[i] = 2;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Swap)); Assert.That(player.selected, Is.EqualTo(1)); Assert.That(player.Weapon.InstanceId, Is.Not.EqualTo(identity));
                    for (int i = 0; i < 40; i++) driver.Step(.02f);
                    Assert.That(arena.enemies[0].health, Is.EqualTo(health - expected).Within(.001f)); Assert.That(follow.ResourceResult, Is.SameAs(receipt)); Assert.That(receipt.Snapshot.DamageMultiplier, Is.EqualTo(1.2f)); Assert.That(follow.ResourcesActive, Is.False);
                    Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(ready));
                    while (arena.Now + .00001f < ready) driver.Step(.02f); driver.Step(.02f);
                    Assert.That(weapon.InstanceId, Is.EqualTo(identity)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(player.weapons[1].Cooldown.Available, Is.EqualTo(1));

                    // Normal admission rejection uses failure recovery without faulting simulation.
                    Setup(arena, driver); var source = new Source(player.Weapon) { Reject = true };
                    var denied = CombatCast.Create(arena, player, player.Special, player.Weapon, false, resourceSource: source); player.currentCast = denied;
                    for (int i = 0; i < 50; i++) driver.Step(.02f);
                    Assert.That(source.Calls, Is.EqualTo(1)); Assert.That(denied.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Rejected)); Assert.That(denied.ResourcesActive, Is.False);
                    Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(arena.SimulationFaulted, Is.False); Assert.That(player.currentCast, Is.Null);

                    // Reset invalidates even a non-current cast before delayed Unity destruction.
                    Setup(arena, driver); source = new Source(player.Weapon);
                    var pending = CombatCast.Create(arena, player, player.Special, player.Weapon, false, resourceSource: source);
                    Assert.That(pending.ResourcesActive, Is.True); driver.Reset(); Assert.That(pending.ResourcesActive, Is.False);
                    pending.BeforeMovement(.4f); Assert.That(source.Calls, Is.Zero); Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(pending.ResourceResult, Is.Null);

                    // Opening the parry window consumes at time zero even without a target.
                    Setup(arena, driver, "shield"); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var parry = player.currentCast;
                    Assert.That(parry.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(parry.ResourceResult.Snapshot.ActivatedAt, Is.EqualTo(0)); Assert.That(player.Weapon.Cooldown.Available, Is.Zero);

                    // The launcher ball is a basic attack and never consumes the D skill resource.
                    Setup(arena, driver, "launcher"); weapon = player.Weapon; weapon.StatSlots[0] = 1;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var launcher = player.currentCast; receipt = Activate(driver, launcher);
                    Assert.That(receipt.Snapshot.Key.IsBasic, Is.True); Assert.That(receipt.Snapshot.DamageMultiplier, Is.EqualTo(1.2f)); Assert.That(weapon.Cooldown.Available, Is.EqualTo(weapon.Cooldown.Capacity));
                    driver.Reset(); Assert.That(launcher.ResourcesActive, Is.False); Assert.That(launcher.ResourceResult, Is.SameAs(receipt));

                    // Its ink-selected D skill spends the cooldown like every other special.
                    Setup(arena, driver, "launcher"); weapon = player.Weapon;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var skill = player.currentCast; receipt = Activate(driver, skill);
                    Assert.That(receipt.Snapshot.Key.IsBasic, Is.False); Assert.That(weapon.Cooldown.Available, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
