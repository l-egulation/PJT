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
    // 기습: 은신 중 시작한 플레이어 직접 타격 공격의 첫 적중 대상 하나에 stealth_ambush.json 의 추가 피해를 더한다.
    public sealed class StealthAmbushTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        TestEnemy Target => arena.enemies[0];
        const float Step = .005f;
        const float Health = 10000;

        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            arena = new GameObject("Ambush arena").AddComponent<CombatArena>();
            arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Ambush camera").AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSize = new Vector2(.9375f, 2);
            content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSpawn = new Vector2(0, 10000);
            content.Enemies = new[] { new CombatEnemySpawn("Ambush target", 0, new Vector2(1, 10000),
                new Vector2(1, 2), Vector2.up, Health, false, false, 1) };
            Assert.That(arena.Initialize(content), Is.True);
            arena.Player.UseTestInput = true;
            driver = arena.AcquireManualSimulation();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        void Place(string form, float distance)
        {
            driver.Reset(); var player = arena.Player;
            int index = Array.FindIndex(arena.catalog.forms, value => value.id == form);
            Assert.That(player.TryReconfigure(0, index, 0, out var error), Is.True, error);
            Assert.That(player.TryReconfigure(1, index, 0, out error), Is.True, error);
            player.SetWeaponSlotReady(0, true); player.SetWeaponSlotReady(1, true);
            player.selected = 0; player.InputBlocked = false;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, 10000); player.velocity = Vector2.zero; player.grounded = false; player.SyncFacing(1);
            Target.Position = player.Position + Vector2.right * distance; Target.velocity = Vector2.zero; Target.SyncFacing(-1);
            Target.maxHealth = Target.health = Health; Target.aggressive = false; Target.isBoss = Target.isDecoy = false;
            Physics2D.SyncTransforms();
        }
        // 한 번 눌러 첫 적중까지 진행하고, 그 적중으로 줄어든 체력을 돌려준다.
        float StrikeOnce()
        {
            int before = Target.ReceivedHitCount; float health = Target.health;
            driver.Step(Step, new CombatInput(pressed: CombatButtons.Basic));
            for (int tick = 0; tick < 600 && Target.ReceivedHitCount == before; tick++) driver.Step(Step);
            Assert.That(Target.ReceivedHitCount, Is.EqualTo(before + 1), "basic must land exactly once");
            Assert.That(arena.SimulationFaulted, Is.False);
            float lost = health - Target.health;
            for (int tick = 0; tick < 80; tick++) driver.Step(Step);
            return lost;
        }
        float Basic(string form, int index) => Array.Find(arena.catalog.forms, value => value.id == form).basics[index].damage;
        static void AssertDamage(float lost, float nominal)
        {
            var variance = CombatDamageVariance.Settings;
            Assert.That(lost, Is.InRange(nominal * variance.minimum - .001f, nominal * variance.maximum + .001f));
        }

        [UnityTest] public IEnumerator HiddenFirstHitAddsTheAmbushBonusOnceAndBreaksStealth()
        {
            float bonus = arena.StealthAmbush.StealthFirstHitBonusDamage;
            Assert.That(bonus, Is.Zero, "stealth_ambush.json stealthFirstHitBonusDamage (2026-11-10 삭제)");
            float first = Basic("dagger", 0), second = Basic("dagger", 1);
            Place("dagger", 1.2f);
            arena.Player.stealthUntil = arena.Now + 3;
            Assert.That(arena.Player.Hidden, Is.True);
            AssertDamage(StrikeOnce(), first + bonus);
            Assert.That(arena.Player.Hidden, Is.False, "starting the attack clears stealth");
            AssertDamage(StrikeOnce(), second);   // 다음 타는 은신이 풀린 뒤 시작했으니 기습 없음
            yield return null;
        }
        [UnityTest] public IEnumerator BreakingOrExpiringHoldBreathStealthHastesOnlyBasicsForTheExitWindow()
        {
            var rule = arena.StealthAmbush; var player = arena.Player;
            Assert.That(rule.ExitBasicSpeedBonus, Is.EqualTo(.5f)); Assert.That(rule.ExitSeconds, Is.EqualTo(4));
            Place("dagger", 1.2f);
            Assert.That(player.StealthExitHasteScale, Is.EqualTo(1));
            player.stealthUntil = arena.Now + 3;
            Assert.That(player.StealthExitHasteScale, Is.EqualTo(1), "no haste while still hidden");
            float broken = arena.Now; StrikeOnce();
            Assert.That(player.Hidden, Is.False);
            Assert.That(player.StealthExitHasteScale, Is.EqualTo(1.5f), "attacking out of stealth starts the haste");
            Assert.That(player.BasicSpeed, Is.EqualTo(player.Weapon.Speed * 1.5f).Within(.0001f));
            while (arena.Now < broken + rule.ExitSeconds + .2f) driver.Step(Step);
            Assert.That(player.StealthExitHasteScale, Is.EqualTo(1), "the haste ends after the exit window");
            Place("dagger", 1.2f);
            player.stealthUntil = arena.Now + .1f;
            for (int tick = 0; tick < 40; tick++) driver.Step(Step);
            Assert.That(player.StealthExitHasteScale, Is.EqualTo(1.5f), "natural expiry also starts the haste");
            Place("dagger", 1.2f);
            player.smokeUntil = arena.Now + .1f;
            for (int tick = 0; tick < 40; tick++) driver.Step(Step);
            Assert.That(player.StealthExitHasteScale, Is.EqualTo(1), "smoke concealment does not haste");
            yield return null;
        }
        [UnityTest] public IEnumerator VisibleAttackHasNoBonusAndSmokeConcealmentAlsoAmbushes()
        {
            float bonus = arena.StealthAmbush.FirstHitBonusDamage, first = Basic("dagger", 0);
            Place("dagger", 1.2f);
            Assert.That(arena.Player.Hidden, Is.False);
            AssertDamage(StrikeOnce(), first);
            Place("dagger", 1.2f);
            arena.Player.smokeUntil = arena.Now + 3;
            Assert.That(arena.Player.Hidden, Is.True);
            AssertDamage(StrikeOnce(), first + bonus);
            yield return null;
        }
        [UnityTest] public IEnumerator OtherWeaponsStartedWhileHiddenAmbushToo()
        {
            float bonus = arena.StealthAmbush.StealthFirstHitBonusDamage, first = Basic("base_sword", 0);
            Place("base_sword", 1.5f);
            arena.Player.stealthUntil = arena.Now + 3;
            AssertDamage(StrikeOnce(), first + bonus);
            yield return null;
        }
    }
}
