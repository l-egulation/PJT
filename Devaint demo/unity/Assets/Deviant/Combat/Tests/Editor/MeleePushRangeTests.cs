using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public sealed class MeleePushRangeTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        TestEnemy Target => arena.enemies[0];
        const float Step = .005f;

        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            arena = new GameObject("Melee push arena").AddComponent<CombatArena>();
            arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Melee push camera").AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSize = new Vector2(.9375f, 2);
            content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSpawn = new Vector2(0, 10000);
            content.Enemies = new[] { new CombatEnemySpawn("Push target", 0, new Vector2(1, 10000),
                new Vector2(1, 2), Vector2.up, 10000, false, false, 1) };
            var floor = new GameObject("Push test floor");
            floor.AddComponent<ArenaSurface>(); floor.transform.position = new Vector3(0, -.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            Assert.That(arena.Initialize(content), Is.True);
            Assert.That(arena.EnemyHitReaction.BasicPushOverlap, Is.EqualTo(.25f));
            arena.Player.UseTestInput = true;
            driver = arena.AcquireManualSimulation();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        void Place(string active, string other, int facing, float distance, bool otherReady = true, bool ground = false)
        {
            driver.Reset(); var player = arena.Player;
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == active), 0, out var error), Is.True, error);
            Assert.That(player.TryReconfigure(1, Array.FindIndex(arena.catalog.forms, form => form.id == other), 0, out error), Is.True, error);
            player.SetWeaponSlotReady(0, true); player.SetWeaponSlotReady(1, otherReady);
            player.selected = 0; player.InputBlocked = false;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, ground ? 0 : 10000); player.velocity = Vector2.zero;
            player.grounded = ground; player.SyncFacing(facing);
            Target.Position = player.Position + Vector2.right * (distance * facing);
            Target.velocity = Vector2.zero; Target.grounded = ground; Target.SyncFacing(-facing);
            Target.maxHealth = Target.health = 10000; Target.aggressive = false; Target.isBoss = Target.isDecoy = false;
            Physics2D.SyncTransforms();
        }
        void Strike(bool special = false, Action afterHit = null, CombatButtons attack = CombatButtons.None)
        {
            int before = Target.ReceivedHitCount;
            if (attack == CombatButtons.None) attack = special ? CombatButtons.Special : CombatButtons.Basic;
            driver.Step(Step, new CombatInput(pressed: attack));
            for (int tick = 0; tick < 600 && Target.ReceivedHitCount == before; tick++) driver.Step(Step);
            Assert.That(Target.ReceivedHitCount, Is.GreaterThan(before), arena.Player.Form.id + " must actually hit");
            afterHit?.Invoke();
            for (int tick = 0; tick < 60; tick++) driver.Step(Step);
            Assert.That(arena.SimulationFaulted, Is.False);
        }
        void AssertX(float distance, int facing)
            => Assert.That(Target.Position.x * facing, Is.EqualTo(distance).Within(.001f));

        [UnityTest] public IEnumerator TwoMeleeWeaponsUseTheirShorterReachAndRepeatedHitsStayInRangeBothWays()
        {
            foreach (int facing in new[] { 1, -1 })
            {
                // Dagger front 2.25625; target half-width .5; retained overlap .25.
                Place("hammer", "dagger", facing, 2);
                for (int hit = 0; hit < 6; hit++) { Strike(); AssertX(2.50625f, facing); }
                Strike(attack: CombatButtons.Weapon2Basic); AssertX(2.50625f, facing);
                Assert.That(arena.Player.selected, Is.EqualTo(1));
                Strike(attack: CombatButtons.Weapon1Basic); AssertX(2.50625f, facing);
                Assert.That(arena.Player.selected, Is.Zero);
                Assert.That(Target.ReceivedHitCount, Is.EqualTo(8));
                Place("hammer", "dagger", facing, 2.6f);
                Strike(); AssertX(2.6f, facing); // Already beyond the short reach: no pull or extra push.
                Place("dagger", "whip", facing, 2.4f);
                Strike(); AssertX(2.50625f, facing); // Ordinary .25 push is capped too.
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RangedAndUnavailableSlotsDoNotShortenMeleeAndTheInnerHitKeepsItsFullPush()
        {
            foreach (int facing in new[] { 1, -1 })
            {
                Place("hammer", "launcher", facing, 2.9f);
                Strike(); AssertX(3.10625f, facing);
                Place("hammer", "dagger", facing, 2.9f, otherReady: false);
                Strike(); AssertX(3.10625f, facing);
                Place("hammer", "launcher", facing, .8f);
                Strike(); AssertX(2.55f, facing); // Full authored 1.75 push, below the limit.
            }
            yield return null;
        }
        [UnityTest] public IEnumerator MeleeSpecialAutomaticPushUsesTheSameLimitButExplicitSkillPushAndRangedHitsDoNot()
        {
            foreach (int facing in new[] { 1, -1 })
            {
                Place("base_sword", "launcher", facing, 2.8f);
                Strike(special: true); AssertX(2.90625f, facing);
                Place("base_sword", "dagger", facing, 4);
                Strike(special: true); AssertX(4, facing);
                Place("hammer", "dagger", facing, 2.5f, ground: true);
                Strike(special: true); AssertX(4, facing); // Explicit slam push of 1.5.
                Place("launcher", "bomb", facing, 8, ground: true);
                Strike(); float rangedBaseline = Target.Position.x * facing;
                Assert.That(rangedBaseline, Is.GreaterThan(8));
                Place("launcher", "dagger", facing, 8, ground: true);
                Strike(); AssertX(rangedBaseline, facing); // Same real projectile timing, independent of the paired weapon.
            }
            yield return null;
        }
        [UnityTest] public IEnumerator HitBoundaryIsCapturedBeforeMovementOrEquipmentChangesAndResetClearsTheMotion()
        {
            foreach (int facing in new[] { 1, -1 })
            {
                Place("hammer", "dagger", facing, 2.9f, otherReady: false);
                Strike(afterHit: () => {
                    arena.Player.SetWeaponSlotReady(1, true);
                    arena.Player.selected = 1;
                    arena.Player.Position += Vector2.right * (10 * facing);
                });
                AssertX(3.10625f, facing);
                Place("hammer", "dagger", facing, 2);
                Strike(); AssertX(2.50625f, facing);
                driver.Reset(); Assert.That(Target.knockUntil, Is.Zero);
                Place("hammer", "dagger", facing, 2); Target.isBoss = true;
                Strike(); AssertX(2, facing);
            }
            yield return null;
        }
    }
}
