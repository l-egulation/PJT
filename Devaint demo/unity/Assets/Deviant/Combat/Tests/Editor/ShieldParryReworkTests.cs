using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Equipment.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public partial class WeaponBalanceReleaseTests
    {
        [Test] public void SuccessfulRearmSurvivesRefundPeriodChangesExtraChargesAndClockRebase()
        {
            var charges = Deviant.Equipment.WeaponResources.CreateCharges(2, .24f);
            Assert.That(charges.Spend(10), Is.True);
            ((IWeaponChargeRearm)charges).Rearm(.6f, 10.1f);
            charges.Refund(100, 10.2f);
            charges.Reconfigure(3, .05f, 10.3f);
            Assert.That(charges.Read().Available, Is.Zero);
            Assert.That(charges.Spend(10.699f), Is.False);
            ((IWeaponChargeClock)charges).Rebase(10.699f, 20);
            Assert.That(charges.Spend(20.0005f), Is.False);
            Assert.That(charges.Spend(20.002f), Is.True);
            charges.Reset(1, 1.2f);
            Assert.That(charges.Read().Available, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ShieldClaimsOnceRearmsAndBlocksNeitherRearNorLateHits()
        {
            Ready("shield"); Target.Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
            driver.Step(.001f, new CombatInput(pressed: CombatButtons.Special));
            var cast = Player.currentCast;
            Advance(.10f);
            float now = arena.Now, hp = Player.health, enemyHp = Target.health;
            Assert.That(Player.Receive(10, Target, -1, false, null), Is.False);
            Assert.That(Player.health, Is.EqualTo(hp));
            Assert.That(enemyHp - Target.health, Is.EqualTo(26).Within(.001f));
            Assert.That(cast.TryGetShieldParryBounds(out _), Is.False);
            Assert.That(Player.Receive(10, Target, -1, false, null), Is.True, "second attack in same window is not blocked");
            Assert.That(enemyHp - Target.health, Is.EqualTo(26).Within(.001f), "counter only once");
            Player.Weapon.Cooldown.Refund(100, arena.Now);
            Advance(.105f);
            Assert.That(Player.currentCast, Is.Null, "success recovers after 0.10s");
            Assert.That(Player.Weapon.Cooldown.Available, Is.Zero);
            Advance(.49f);
            Assert.That(arena.Now - now, Is.LessThan(.6f));
            Assert.That(Player.Weapon.Cooldown.Available, Is.Zero);
            Advance(.01f);
            Assert.That(Player.Weapon.Cooldown.Available, Is.EqualTo(1));

            Ready("shield"); Target.Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
            driver.Step(.001f, new CombatInput(pressed: CombatButtons.Special));
            Advance(.201f);
            Assert.That(Player.Receive(10, Target, -1, false, null), Is.True, "late attack");
            Assert.That(Player.Weapon.Cooldown.Remaining(arena.Now), Is.GreaterThan(.9f), "failure keeps authored 1.20s cooldown");
            Ready("shield"); Target.Position = new Vector2(-2, 1000); Physics2D.SyncTransforms();
            driver.Step(.001f, new CombatInput(pressed: CombatButtons.Special));
            Advance(.05f);
            Assert.That(Player.Receive(10, Target, 1, false, null), Is.True, "rear attack");
            Assert.That(arena.SimulationFaulted, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator ShieldClaimsBeforeReentrantCounterCallbacksAndReflectsOnlyOneProjectile()
        {
            Ready("shield"); Target.Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
            bool reentered = false;
            Action<CombatAcceptedHit> callback = hit => {
                if (hit.Origin.Kind != CombatHitKind.Parry || reentered) return;
                reentered = true;
                Assert.That(Player.Receive(3, Target, -1, false, null), Is.True, "claim precedes counter callbacks");
            };
            arena.AcceptedHit += callback;
            driver.Step(.001f, new CombatInput(pressed: CombatButtons.Special)); Advance(.05f);
            Assert.That(Player.Receive(10, Target, -1, false, null), Is.False);
            Assert.That(reentered, Is.True);
            Assert.That(Player.health, Is.EqualTo(97));
            arena.AcceptedHit -= callback;

            Ready("shield"); Target.Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
            driver.Step(.001f, new CombatInput(pressed: CombatButtons.Special));
            var cast = Player.currentCast;
            Assert.That(cast.TryGetShieldParryBounds(out _), Is.True);
            var at = new Vector2(Player.hurtCollider.bounds.max.x + .60f, Player.Center.y);
            var first = new GameObject("First parry projectile").AddComponent<ArenaProjectile>();
            first.Initialize(arena, Target, at, Vector2.left * 8, 10, arena.Now + 3); arena.projectiles.Add(first);
            first.Step(.01f);
            Assert.That(first.Execution.Reflected, Is.True);
            Assert.That(first.Execution.Damage, Is.EqualTo(10));
            Assert.That(cast.TryGetShieldParryBounds(out _), Is.False);
            var second = new GameObject("Second parry projectile").AddComponent<ArenaProjectile>();
            second.Initialize(arena, Target, at, Vector2.left * 8, 10, arena.Now + 3); arena.projectiles.Add(second);
            second.Step(.1f);
            Assert.That(second.Execution.Reflected, Is.False);
            Assert.That(Player.health, Is.EqualTo(90), "success does not grant invulnerability");
            Assert.That(arena.SimulationFaulted, Is.False);
            yield return null;
        }
    }
}
