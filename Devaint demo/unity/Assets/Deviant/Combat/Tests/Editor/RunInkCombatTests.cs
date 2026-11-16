using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class RunInkCombatTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        CombatPlayer Player => arena.Player;
        TestEnemy Target => arena.enemies[0];
        SketchEquipment Equipment => arena.Sketch;
        float minimum, maximum;
        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("RunInkArena").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            arena = Object.FindAnyObjectByType<CombatArena>();
            arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false);
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("RunInkCamera").AddComponent<Camera>(); content.PlayerSpawn = new Vector2(0, 1000);
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = new[] {
                new CombatEnemySpawn("InkTarget", 1, new Vector2(1,1000), Vector2.one*2, Vector2.up, 10000, true, false, 0),
                new CombatEnemySpawn("InkTarget2", 1, new Vector2(1.5f,1000), Vector2.one*2, Vector2.up, 10000, true, false, 0) };
            content.PrepareEnvironment = _ => { var floor = new GameObject("Floor"); floor.transform.position = new Vector2(0, 999.5f); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); };
            Assert.That(arena.Initialize(content), Is.True); driver = arena.AcquireManualSimulation(); driver.Reset();
            var growth = new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text),
                new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text)));
            arena.MvpGrowth = growth;
            growth.Sketch = new SketchEquipment(CombatArena.LoadRunInkCatalog(), growth.Economy, 123, new Dictionary<string, int[]> { ["base_sword"] = new[] { 0 } });
            arena.EnableSketchEquipment(123);
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            minimum = CombatDamageVariance.Settings.minimum; maximum = CombatDamageVariance.Settings.maximum;
            CombatDamageVariance.Settings.minimum = CombatDamageVariance.Settings.maximum = 1;
            driver.Step(.01f);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose(); driver = null;
            CombatDamageVariance.Settings.minimum = minimum; CombatDamageVariance.Settings.maximum = maximum;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        RunInkDefinition Ink(string trigger, string effect, string condition = null) => Equipment.Catalog.inks.Single(i => i.trigger == trigger && i.effect == effect && (condition == null || i.condition == condition));
        RunInkWeapon Equip(int slot, params RunInkDefinition[] inks)
        {
            // An ink-only choice (the ink half of a double reward) is taken until a weapon choice comes.
            while (Equipment.RewardChoices().All(c => c.Weapon == null)) { if (Equipment.PendingReward) Equipment.ChooseReward(0); else { Equipment.RecordKill(false); Equipment.OpenReward(); } }
            int index = Equipment.RewardChoices().ToList().FindIndex(c => c.Weapon != null);
            var weapon = Equipment.ChooseReward(index).Weapon;
            Equipment.DebugResizePalette(weapon.Id, Math.Max(3, inks.Length));
            for (int i = 0; i < weapon.Slots.Count; i++) Equipment.DebugSetSlotColor(weapon.Id, i, "neutral");
            for (int i = 0; i < inks.Length; i++) { Equipment.Register(inks[i].id); Equipment.Place(weapon.Id, i, inks[i].id); }
            Equipment.Confirm(weapon.Id); Equipment.Equip(weapon.Id, slot); arena.SyncSketchLoadout(); return weapon;
        }
        CombatHitOrigin Begin(int slot, bool basic = true)
        {
            var weapon = Player.weapons[slot]; Guid execution = Guid.NewGuid();
            var stamp = new CombatAttackStamp(weapon.InstanceId, "base_sword", "run_ink_fixture", "1", basic, 0, weapon.InkPalette.Read());
            typeof(CombatArena).GetMethod("BeginInkAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { stamp, execution, Player });
            arena.TryBindActor(Player, out var source);
            return new CombatHitOrigin(source, default, execution, 0, "fixture", Guid.Empty, CombatHitKind.Melee, stamp: stamp);
        }
        float Hit(CombatHitOrigin origin, CombatActor target = null, float damage = 100)
        {
            target ??= Target; float before = target.health;
            Assert.That(target.Receive(damage, Player, 1, origin.Stamp.Basic, null, origin: origin), Is.True);
            return before - target.health;
        }
        void Advance(float seconds)
        { float until = arena.Now + seconds; while (arena.Now < until - .000001f) driver.Step(Mathf.Min(.01f, until - arena.Now)); }
        void AssertUnarmedAttackSurvivesLoadoutSync(int slot)
        {
            Player.CancelAction(); Player.ClearInputState();
            Player.Position = new Vector2(0, 1000); Player.SyncFacing(1);
            Target.Position = new Vector2(1, 1000); Target.health = 10000;
            Physics2D.SyncTransforms();
            Guid weapon = Player.weapons[slot].InstanceId;
            int hits = Target.ReceivedHitCount;
            Assert.That(Player.IsWeaponSlotReady(slot), Is.True, "An empty slot admits its unarmed basic attack.");
            driver.Step(.01f, new CombatInput(pressed: slot == 0 ? CombatButtons.Weapon1Basic : CombatButtons.Weapon2Basic));
            var cast = Player.currentCast;
            Assert.That(cast, Is.Not.Null, "Loadout synchronization must not cancel the newly started unarmed attack.");
            Assert.That(cast.Definition.Id, Is.EqualTo("unarmed_basic_0"));
            for (int i = 0; i < 16; i++)
            {
                arena.EnableSketchEquipment(123);
                driver.Step(.01f);
                Assert.That(Player.currentCast, Is.SameAs(cast));
                Assert.That(Player.weapons[slot].InstanceId, Is.EqualTo(weapon));
            }
            Assert.That(Target.ReceivedHitCount, Is.EqualTo(hits + 1));
            Assert.That(Target.health, Is.EqualTo(9960).Within(.002));
            Assert.That(arena.SimulationFaulted, Is.False);
        }
        // With no weapon A and S both punch (team decision 2026-11-13).
        [UnityTest] public IEnumerator BothEmptySlotsKeepUnarmedAttacksAliveThroughLoadoutSync()
        {
            AssertUnarmedAttackSurvivesLoadoutSync(0);
            AssertUnarmedAttackSurvivesLoadoutSync(1);
            yield return null;
        }
        [UnityTest] public IEnumerator EmptySlotCanPunchWhileTheOtherSlotKeepsItsWeapon()
        {
            var weapon = Equip(0);
            AssertUnarmedAttackSurvivesLoadoutSync(1);
            Assert.That(Equipment.Equipped(0), Is.SameAs(weapon));
            Assert.That(weapon.Durability, Is.EqualTo(weapon.Maximum));
            Player.CancelAction(); Player.ClearInputState();
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Weapon2Special));
            Assert.That(Player.currentCast, Is.Null, "An empty slot has no skill.");
            yield return null;
        }
        [UnityTest] public IEnumerator BreakingTheActiveWeaponSelectsTheSurvivorAndKeepsTheEmptySlotUsable()
        {
            var broken = Equip(0); var survivor = Equip(1); Player.selected = 0;
            Equipment.RecordDamage(broken.Id, broken.Maximum); Equipment.FinishHit(); arena.SyncSketchLoadout();
            Assert.That(broken.Destroyed, Is.True);
            Assert.That(Player.selected, Is.EqualTo(1));
            Assert.That(Player.Weapon.InstanceId, Is.EqualTo(survivor.Id));
            AssertUnarmedAttackSurvivesLoadoutSync(0);
            Assert.That(survivor.Durability, Is.EqualTo(survivor.Maximum));
            yield return null;
        }
        int TrackedStatuses => ((IDictionary)typeof(CombatArena).GetField("sketchStatuses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena)).Count;
        [UnityTest] public IEnumerator NeutralQueriesDoNotRegisterTargetsAndExpiredSlowsReleaseTheirState()
        {
            Assert.That(TrackedStatuses, Is.Zero);
            for (int i = 0; i < 100; i++) foreach (var enemy in arena.enemies)
            {
                Assert.That(arena.ReadRunInkSlow(enemy), Is.Zero);
                Assert.That(arena.InkMovementScale(enemy), Is.EqualTo(1));
            }
            Assert.That(TrackedStatuses, Is.Zero);
            var slow = Ink("basic_attack", "slow"); Equip(0, slow); Hit(Begin(0), damage: 1);
            Assert.That(TrackedStatuses, Is.EqualTo(1)); Assert.That(arena.ReadRunInkSlow(Target), Is.GreaterThan(0));
            Advance(slow.duration + .02f);
            Assert.That(arena.ReadRunInkSlow(Target), Is.Zero); Assert.That(TrackedStatuses, Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator DestroyedSlowedTargetsReleaseTheirState()
        {
            Equip(0, Ink("basic_attack", "slow")); Hit(Begin(0), damage: 1);
            Assert.That(TrackedStatuses, Is.EqualTo(1));
            Object.DestroyImmediate(Target.gameObject); driver.Step(.01f);
            Assert.That(TrackedStatuses, Is.Zero); Assert.That(arena.SimulationFaulted, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator SameFirstSkillHitAppliesSlowBeforeBlueBonusAndLaterHitsKeepTheirOwnConditions()
        {
            var weapon = Equip(0, Ink("skill_attack", "slow"), Ink("skill_attack", "skill_attack_damage_increase", "slow"));
            var origin = Begin(0, false); float value = weapon.Slots[1].Value;
            Assert.That(Hit(origin), Is.EqualTo(100 * (1 + value)).Within(.002)); Assert.That(arena.ReadRunInkSlow(Target), Is.GreaterThan(0));
            float before = weapon.Durability; Hit(origin); Assert.That(before - weapon.Durability, Is.EqualTo(100 * (1 + value)).Within(.002));
            Advance(.31f); Assert.That(Target.ReceivedHitCount, Is.EqualTo(2), "slow does not apply damage over time");
            Assert.That(arena.ReadMvpMarkDisplay(Target), Is.Zero); yield return null;
        }
        // 2026-11-13: X swaps a weapon and bare hands between A and B once a weapon is equipped.
        [UnityTest] public IEnumerator SwapKeyMovesTheOnlyWeaponBetweenSlots()
        {
            Assert.That(Player.IsWeaponSlotReady(0) && Player.IsWeaponSlotReady(1), Is.True, "with no weapon both slots punch");
            Assert.That(Player.TrySwapWeaponOrder(), Is.False, "nothing to swap");
            var weapon = Equip(1);
            Assert.That(Player.IsWeaponSlotReady(1), Is.True);
            Assert.That(Player.weapons[1].InstanceId, Is.EqualTo(weapon.Id));
            Player.selected = 1;
            Assert.That(Player.TrySwapWeaponOrder(), Is.True);
            Assert.That(Player.weapons[0].InstanceId, Is.EqualTo(weapon.Id), "the weapon moves to A");
            Assert.That(Equipment.Equipped(1), Is.Null, "B now holds bare hands");
            Assert.That(Player.IsWeaponSlotReady(1), Is.True, "bare-handed B still punches beside a weapon");
            Assert.That(Player.selected, Is.EqualTo(0), "the weapon stays current");
            Assert.That(Equipment.ChangeCooldownRemaining, Is.Zero, "reordering does not start the shared change cooldown");
            yield return null;
        }
        [UnityTest] public IEnumerator RedBuffSurvivesSourceStorageAndAddsToExistingDamageBonusOnTheOtherWeapon()
        {
            var weapon = Equip(0, Ink("basic_attack", "slow"), Ink("basic_attack", "basic_attack_damage_increase", "slow"));
            Equip(1); arena.PlayerDamageScale = 1.2f;
            Assert.That(Hit(Begin(0)), Is.EqualTo(100 * (1.2f + weapon.Slots[1].Value)).Within(.002));
            Equipment.Store(0); arena.SyncSketchLoadout();
            Assert.That(Hit(Begin(1)), Is.EqualTo(100 * (1.2f + weapon.Slots[1].Value)).Within(.002));
            Assert.That(Equipment.State.Arsenal.ChangeCooldownPending || Equipment.ChangeCooldownRemaining > 0, Is.True, "unequipping starts the shared change cooldown once combat time runs"); Advance(4.05f);
            Assert.That(Hit(Begin(1)), Is.EqualTo(120).Within(.002)); yield return null;
        }
        int ContributingCopies(RunInkWeapon weapon) => weapon.Slots.Count(m => arena.ReadRunInkMountBuff(m.Id) > 0);
        [UnityTest] public IEnumerator ElevenRedCopiesShareOneCapAcrossConditionsStorageAndDestruction()
        {
            var red = Ink("basic_attack", "basic_attack_damage_increase", "stun");
            var a = Equip(0, Ink("basic_attack", "stun"), red, red, red, red, red);
            var b = Equip(1, Enumerable.Repeat(Ink("basic_attack", "basic_attack_damage_increase", "root"), 6).ToArray());
            Assert.That(Hit(Begin(0)), Is.EqualTo(250).Within(.002));
            Target.rootUntil = arena.Now + 10;
            Assert.That(Hit(Begin(1)), Is.EqualTo(280).Within(.002), "the activating hit already uses the shared cap");
            Assert.That(ContributingCopies(a), Is.Zero, "equal values prefer the more recent proc, including within one simulation time");
            Assert.That(ContributingCopies(b), Is.EqualTo(6));
            var hud = new List<BuffSlot>(); Player.BuffSlotSource.Collect(hud);
            var suppressed = hud.Single(slot => slot.Icon == BuffSlotIcon.BasicDamage && slot.Tooltip.Contains("무기 1"));
            Assert.That(suppressed.ActiveRemaining(arena.Now), Is.Zero);
            Assert.That(suppressed.Tooltip, Does.Contain("현재 기여 0/5칸"));
            Assert.That(Hit(Begin(1)), Is.EqualTo(280).Within(.002), "refreshing cannot add another copy");
            // Main now locks normal storage during combat; use its cheat-mode storage path.
            Equipment.DebugConfigureWeapon(0, null, 0);
            Equipment.RecordDamage(b.Id, b.Maximum); Equipment.FinishHit(); arena.SyncSketchLoadout();
            Assert.That(b.Destroyed, Is.True);
            Assert.That(Hit(Begin(0)), Is.EqualTo(280).Within(.002), "stored and destroyed sources share the same cap on unarmed attacks");
            Advance(4.51f);
            Assert.That(Hit(Begin(0)), Is.EqualTo(100).Within(.002));
            yield return null;
        }
        [UnityTest] public IEnumerator EnhancedCopiesWinAndSuppressedCopiesReturnWithTheirOriginalExpiry()
        {
            var red = Ink("basic_attack", "basic_attack_damage_increase", "stun");
            var a = Equip(0, Enumerable.Repeat(red, 6).ToArray());
            var b = Equip(1, red, red, red);
            Equipment.DebugSetSlotColor(a.Id, 0, "red"); Equipment.DebugSetSlotColor(a.Id, 1, "red");
            Target.stunUntil = arena.Now + 10;
            Assert.That(Hit(Begin(0)), Is.EqualTo(310).Within(.002));
            Assert.That(arena.ReadRunInkMountBuff(a.Slots[0].Id), Is.EqualTo(5.25f).Within(.002), "six actual slots keep E=7");
            Advance(.1f);
            Assert.That(Hit(Begin(1)), Is.EqualTo(310).Within(.002));
            Assert.That(ContributingCopies(a), Is.EqualTo(3)); Assert.That(ContributingCopies(b), Is.EqualTo(3));
            Assert.That(arena.ReadRunInkMountBuff(a.Slots[0].Id), Is.GreaterThan(0));
            Assert.That(arena.ReadRunInkMountBuff(a.Slots[1].Id), Is.GreaterThan(0));
            // Main now locks normal storage during combat; use its cheat-mode storage path.
            Equipment.DebugConfigureWeapon(0, null, 0); arena.SyncSketchLoadout();
            Advance(2.26f);
            Assert.That(ContributingCopies(a), Is.EqualTo(6)); Assert.That(ContributingCopies(b), Is.Zero);
            Assert.That(arena.ReadRunInkMountBuff(a.Slots[2].Id), Is.EqualTo(2.89f).Within(.005), "suppression did not pause or restart the original 5.25 seconds");
            Assert.That(Hit(Begin(0)), Is.EqualTo(310).Within(.002));
            Advance(2.9f);
            Assert.That(Hit(Begin(0)), Is.EqualTo(100).Within(.002));
            yield return null;
        }
        [UnityTest] public IEnumerator DamageAndHasteHaveSeparateCapsAndKeepTheExistingSpeedLimit()
        {
            var red = Ink("basic_attack", "basic_attack_damage_increase", "stun");
            var haste = Ink("basic_attack", "basic_attack_speed_increase", "stun");
            var a = Equip(0, red, red, red, haste, haste, haste);
            var b = Equip(1, red, red, red, haste, haste, haste);
            Target.stunUntil = arena.Now + 10;
            Hit(Begin(0));
            Assert.That(Hit(Begin(1)), Is.EqualTo(280).Within(.002));
            Assert.That(ContributingCopies(a) + ContributingCopies(b), Is.EqualTo(12), "each effect admits six actual copies");
            Assert.That(arena.ReadRunInkBasicSpeedBonus, Is.EqualTo(.6f).Within(.001));
            yield return null;
        }
        [UnityTest] public IEnumerator MovementSharesSixCopiesAcrossTriggersAndKeepsItsNumericCap()
        {
            var a = Equip(0, Enumerable.Repeat(Ink("basic_attack", "movement_speed_increase"), 6).ToArray());
            var b = Equip(1, Enumerable.Repeat(Ink("skill_attack", "movement_speed_increase"), 6).ToArray());
            Begin(0); Begin(1, false);
            Assert.That(ContributingCopies(a), Is.Zero); Assert.That(ContributingCopies(b), Is.EqualTo(6));
            Assert.That(arena.InkMovementScale(Player), Is.EqualTo(1.4f).Within(.001));
            Assert.That(a.Slots.All(m => m.Cooldown > 0), Is.True, "suppression does not reset cooldowns");
            Advance(3.01f);
            Assert.That(arena.InkMovementScale(Player), Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator HealingFromBothWeaponsIsNotLimitedToSixCopies()
        {
            var heal = Ink("basic_attack", "heal");
            Equip(0, Enumerable.Repeat(heal, 6).ToArray()); Equip(1, Enumerable.Repeat(heal, 6).ToArray());
            Player.health = Player.maxHealth - 20;
            float before = Player.health; Hit(Begin(0), damage: 1); Hit(Begin(1), damage: 1);
            Assert.That(Player.health - before, Is.EqualTo(12));
            yield return null;
        }
        [UnityTest] public IEnumerator ShieldGenerationFromBothWeaponsKeepsOnlyItsExistingResourceCap()
        {
            var shield = Ink("basic_attack", "shield");
            Equip(0, Enumerable.Repeat(shield, 6).ToArray()); Equip(1, Enumerable.Repeat(shield, 6).ToArray());
            Player.maxHealth = Player.health = 1000;
            Hit(Begin(0), damage: 1); Hit(Begin(1), damage: 1);
            Assert.That(arena.ReadInkShield(), Is.EqualTo(84).Within(.001), "all twelve copies generate seven shield each");
            yield return null;
        }
        [UnityTest] public IEnumerator MultiTargetPreflightMakesRedDamageIndependentOfTargetOrder()
        {
            Equip(0, Ink("basic_attack", "basic_attack_damage_increase", "stun"));
            var other = arena.enemies[1]; other.stunUntil = arena.Now + 1;
            var origin = Begin(0);
            typeof(CombatArena).GetMethod("PrepareSketchHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { origin, new CombatActor[] { Target, other } });
            Assert.That(Hit(origin, Target), Is.EqualTo(130).Within(.002));
            Assert.That(Hit(origin, other), Is.EqualTo(130).Within(.002)); yield return null;
        }
        [UnityTest] public IEnumerator BasicAndSkillResourcesRequireActualMonsterDamageAndKeepOneProcPerAttack()
        {
            var weapons = new[] {
                Equip(0, Ink("basic_attack", "heal"), Ink("basic_attack", "shield")),
                Equip(1, Ink("skill_attack", "heal"), Ink("skill_attack", "shield")) };
            Player.maxHealth = 1000; Player.health = 900;
            for (int slot = 0; slot < 2; slot++)
            {
                bool basic = slot == 0;
                Target.health = 10000;
                var weapon = weapons[slot]; var origin = Begin(slot, basic);
                float health = Player.health, shield = arena.ReadInkShield();
                Assert.That(Player.health, Is.EqualTo(health));
                Assert.That(arena.ReadInkShield(), Is.EqualTo(shield));
                Target.Receive(0, Player, 1, basic, null, origin: origin);
                var decoy = arena.enemies[1]; decoy.isDecoy = true;
                Hit(origin, decoy, 1); decoy.isDecoy = false;
                Assert.That(Player.health, Is.EqualTo(health), "misses, zero damage and decoys cannot heal");
                Assert.That(arena.ReadInkShield(), Is.EqualTo(shield));
                Assert.That(weapon.Slots.Take(2).All(m => m.Cooldown == 0), Is.True);
                Hit(origin, damage: 1);
                Assert.That(Player.health, Is.EqualTo(health + 1));
                Assert.That(arena.ReadInkShield(), Is.EqualTo(shield + 7));
                Assert.That(weapon.Slots.Take(2).All(m => m.Cooldown > 0), Is.True);
                Hit(origin, arena.enemies[1], 1);
                Advance(6.1f); Hit(origin, damage: 1);
                Assert.That(Player.health, Is.EqualTo(health + 1), "same execution cannot proc twice, even after cooldown");
                Assert.That(arena.ReadInkShield(), Is.EqualTo(shield + 7));
                Target.health = 1; Hit(Begin(slot, basic), damage: 10);
                Assert.That(Target.Dead, Is.True);
                Assert.That(Player.health, Is.EqualTo(health + 2), "lethal health loss qualifies");
                Assert.That(arena.ReadInkShield(), Is.EqualTo(shield + 14));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator DamageResourceCopiesPreserveCooldownWhenHealingOrShieldGainIsZero()
        {
            var weapon = Equip(0, Ink("basic_attack", "heal"), Ink("basic_attack", "shield"));
            Hit(Begin(0), damage: 1);
            Assert.That(weapon.Slots[0].Cooldown, Is.Zero, "full health preserves healing proc");
            Advance(6.1f);
            Hit(Begin(0), damage: 1); Advance(6.1f);
            var origin = Begin(0); Hit(origin, damage: 1);
            Assert.That(arena.ReadInkShield(), Is.EqualTo(20));
            Advance(6.1f);
            origin = Begin(0); Hit(origin, damage: 1);
            Assert.That(weapon.Slots[1].Cooldown, Is.Zero, "full shield preserves shield proc");
            Player.health -= 5;
            Hit(origin, damage: 1);
            Assert.That(Player.health, Is.EqualTo(Player.maxHealth - 4), "a zero-gain attempt may retry on later damage");
            yield return null;
        }
        [UnityTest] public IEnumerator ResourceAtCapPreservesCopiesAndNormalShieldDecaysOnlyAfterGrace()
        {
            var weapon = Equip(0, Ink("basic_attack", "heal"), Ink("basic_attack", "shield"), Ink("basic_attack", "shield"));
            Begin(0); Assert.That(weapon.Slots[0].Cooldown, Is.Zero, "full health must not spend heal proc");
            Hit(Begin(0), damage: 1); Assert.That(arena.ReadInkShield(), Is.EqualTo(14));
            Advance(9.9f); Assert.That(arena.ReadInkShield(), Is.EqualTo(14));
            Advance(.3f); Assert.That(arena.ReadInkShield(), Is.InRange(12.8f, 13.2f)); yield return null;
        }
        [UnityTest] public IEnumerator BlueReductionUsesFullSkillCooldownAndSkipsZeroReductionCopies()
        {
            var weapon = Equip(0, Ink("skill_attack", "cooldown_reduction", "stun"), Ink("skill_attack", "cooldown_reduction", "stun"));
            var pool = Player.weapons[0].Cooldown;
            float baseCooldown = arena.catalog.forms[Player.weapons[0].FormIndex].specials[0].cooldown;
            pool.Reset(1, baseCooldown); pool.Spend(arena.Now); Advance(baseCooldown * .6f);
            Target.stunUntil = arena.Now + 2; float before = pool.NextReady;
            Hit(Begin(0, false), damage: 1);
            Assert.That(before - pool.NextReady, Is.EqualTo(baseCooldown * (weapon.Slots[0].Value + weapon.Slots[1].Value)).Within(.001));
            Assert.That(weapon.Slots.All(s => s.Ink == null || s.Cooldown > 0), Is.True); yield return null;
        }
        [UnityTest] public IEnumerator ActualBasicInputUsesConfirmedWeaponAndRecordsActualHealthLoss()
        {
            var weapon = Equip(0, Ink("basic_attack", "slow"));
            Player.Position = new Vector2(0, 1000); Target.Position = new Vector2(1, 1000); Physics2D.SyncTransforms();
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Weapon1Basic)); Advance(.7f);
            Assert.That(Target.health, Is.LessThan(10000)); Assert.That(weapon.Durability, Is.LessThan(weapon.Maximum));
            Assert.That(arena.ReadRunInkSlow(Target), Is.GreaterThan(0)); Assert.That(arena.SimulationFaulted, Is.False); yield return null;
        }
        [UnityTest] public IEnumerator DuplicateControlCapsAtThreeSecondsAndRefreshesEveryHitWithoutExtendingWeaponWindow()
        {
            var stun = Ink("basic_attack", "stun"); Equip(0, stun, stun, stun);
            var origin = Begin(0); Hit(origin, damage: 1); float until = Target.stunUntil;
            Assert.That(until - arena.Now, Is.EqualTo(3).Within(.001)); Advance(.5f);
            Hit(origin, damage: 1); Assert.That(Target.stunUntil, Is.EqualTo(until + .5f).Within(.002));
            Hit(origin, arena.enemies[1], 1); Assert.That(arena.enemies[1].stunUntil - arena.Now, Is.EqualTo(3).Within(.001)); yield return null;
        }
        [UnityTest] public IEnumerator ControlWindowAppliesToSkillsOnItsSourceOnlyAndCooldownStartsAfterExpiry()
        {
            var weapon = Equip(0, Ink("basic_attack", "root")); Equip(1);
            Hit(Begin(0), damage: 1); var mount = weapon.Slots[0];
            float duration = mount.ActiveRemaining;
            Assert.That(duration, Is.EqualTo(1.7f).Within(.001));
            Advance(.5f); Hit(Begin(0, false), damage: 1);
            Assert.That(mount.ActiveRemaining, Is.EqualTo(duration - .5f).Within(.002));
            Assert.That(mount.Cooldown, Is.EqualTo(3).Within(.001));
            var other = arena.enemies[1]; Hit(Begin(1), other, 1);
            Assert.That(other.rootUntil, Is.LessThanOrEqualTo(arena.Now));
            Advance(1.21f); Hit(Begin(0), other, 1);
            Assert.That(other.rootUntil, Is.LessThanOrEqualTo(arena.Now));
            Assert.That(mount.Cooldown, Is.EqualTo(2.99f).Within(.003));
            Advance(3); Hit(Begin(0), other, 1);
            Assert.That(other.rootUntil - arena.Now, Is.EqualTo(1).Within(.002));
            yield return null;
        }
        [UnityTest] public IEnumerator SingleEnhancedStunAndRootGrantForTwoPointFiveSecondsBeforeCooldown()
        {
            var stun = Equip(0, Ink("basic_attack", "stun"));
            var root = Equip(1, Ink("basic_attack", "root"));
            Equipment.DebugSetSlotColor(stun.Id, 0, "purple");
            Equipment.DebugSetSlotColor(root.Id, 0, "purple");
            Hit(Begin(0), damage: 1); Hit(Begin(1), damage: 1);
            foreach (var mount in new[] { stun.Slots[0], root.Slots[0] })
                Assert.That(mount.ActiveRemaining, Is.EqualTo(2.5f).Within(.002));
            Advance(2.45f);
            Hit(Begin(0, false), damage: 1); Hit(Begin(1, false), damage: 1);
            Assert.That(Target.stunUntil - arena.Now, Is.EqualTo(1.5f).Within(.002));
            Assert.That(Target.rootUntil - arena.Now, Is.EqualTo(1.5f).Within(.002));
            foreach (var mount in new[] { stun.Slots[0], root.Slots[0] })
            {
                Assert.That(mount.ActiveRemaining, Is.EqualTo(.05f).Within(.002), "hits do not extend the window");
                Assert.That(mount.Cooldown, Is.EqualTo(2.5f).Within(.002), "cooldown waits for expiry");
            }
            Advance(.06f);
            foreach (var mount in new[] { stun.Slots[0], root.Slots[0] })
            {
                Assert.That(mount.ActiveRemaining, Is.Zero);
                Assert.That(mount.Cooldown, Is.EqualTo(2.49f).Within(.003));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator TwoEnhancedSlowsEqualThreeNormalCopiesAndBossKeepsOnlyStatus()
        {
            var slow = Ink("basic_attack", "slow"); var weapon = Equip(0, slow, slow);
            Equipment.DebugSetSlotColor(weapon.Id, 0, "purple"); Equipment.DebugSetSlotColor(weapon.Id, 1, "purple");
            Target.isBoss = true; Hit(Begin(0), damage: 1);
            Assert.That(arena.ReadRunInkSlow(Target), Is.EqualTo(.9f).Within(.001));
            Assert.That(arena.ReadRunInkMotionSlow(Target), Is.Zero);
            Assert.That(arena.InkMovementScale(Target), Is.EqualTo(1));
            Assert.That(weapon.Slots[0].ActiveRemaining, Is.EqualTo(6).Within(.001));
            Assert.That(weapon.Slots[0].Cooldown, Is.EqualTo(1));
            Advance(2.9f); Assert.That(arena.ReadRunInkSlow(Target), Is.GreaterThan(0));
            Advance(.11f); Assert.That(arena.ReadRunInkSlow(Target), Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator RedRefreshesEveryHitAndDestroyedSourceKeepsOriginalAttackAndBuff()
        {
            var red = Ink("basic_attack", "basic_attack_damage_increase", "stun");
            var source = Equip(0, Ink("basic_attack", "stun"), red, red); Equip(1);
            var origin = Begin(0); Assert.That(Hit(origin), Is.EqualTo(160).Within(.002));
            Advance(.6f); Hit(origin, damage: 1);
            Assert.That(arena.ReadRunInkBuffs().RedLeft, Is.EqualTo(1.5f).Within(.002));
            var slots = new List<BuffSlot>(); Player.BuffSlotSource.Collect(slots);
            var redSlot = slots.Single(slot => slot.Icon == BuffSlotIcon.BasicDamage);
            Assert.That(redSlot.ActiveRemaining(arena.Now), Is.EqualTo(1.5f).Within(.002), "the slot tracks the refreshed red buff");
            Assert.That(redSlot.ActiveDuration, Is.EqualTo(1.5f).Within(.002)); Assert.That(redSlot.Remaining(arena.Now), Is.Zero, "red has no cooldown");
            Equipment.RecordDamage(source.Id, source.Maximum); Equipment.FinishHit(); arena.SyncSketchLoadout();
            Advance(.6f);
            var other = arena.enemies[1];
            Assert.That(Hit(origin, other), Is.EqualTo(160).Within(.002));
            Assert.That(other.stunUntil - arena.Now, Is.EqualTo(1).Within(.002));
            Assert.That(Hit(Begin(1)), Is.EqualTo(160).Within(.002));
            Assert.That(Hit(Begin(1, false)), Is.EqualTo(100).Within(.002));
            Advance(1.51f); Assert.That(Hit(Begin(1)), Is.EqualTo(100).Within(.002));
            yield return null;
        }
        [UnityTest] public IEnumerator MovementEffectsWorkOnMissAndSwitchTracksThePreviousActualWeapon()
        {
            var weapon = Equip(0, Ink("basic_attack", "movement_speed_increase"), Ink("weapon_switch", "movement_speed_increase")); Equip(1);
            Begin(0); Assert.That(weapon.Slots[1].Cooldown, Is.Zero, "first attack is not a switch");
            var slots = new List<BuffSlot>(); Player.BuffSlotSource.Collect(slots);
            Assert.That(slots[0].ActiveRemaining(arena.Now), Is.EqualTo(weapon.Slots[0].Ink.duration).Within(.002), "the slot shows the movement buff");
            Assert.That(slots[0].Remaining(arena.Now), Is.EqualTo(weapon.Slots[0].Ink.cooldown).Within(.002), "its cooldown runs alongside the buff");
            Assert.That(slots[1].ActiveRemaining(arena.Now), Is.Zero);
            Assert.That(arena.InkMovementScale(Player), Is.EqualTo(1 + weapon.Slots[0].Value).Within(.001));
            Begin(1); Begin(0);
            Assert.That(arena.InkMovementScale(Player), Is.EqualTo(1 + weapon.Slots[0].Value + weapon.Slots[1].Value).Within(.001));
            Advance(3.1f); Assert.That(arena.InkMovementScale(Player), Is.EqualTo(1)); yield return null;
        }
        [UnityTest] public IEnumerator DashActivatesOnlyCurrentWeaponAndItsShieldGraceExpires()
        {
            var weapon = Equip(0, Ink("dash_use", "heal"), Ink("dash_use", "shield"), Ink("dash_use", "slow"));
            var other = Equip(1, Ink("dash_use", "shield")); Player.selected = 0; Player.health = 80; Player.grounded = true;
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Dash));
            Assert.That(Player.Dashing, Is.True); Assert.That(Player.health, Is.EqualTo(80 + weapon.Slots[0].Value));
            Assert.That(other.Slots[0].Cooldown, Is.Zero); Assert.That(arena.ReadInkShield(), Is.EqualTo(weapon.Slots[1].Value));
            Assert.That(arena.ReadRunInkSlow(Target), Is.GreaterThan(0)); Advance(4);
            Assert.That(arena.ReadInkShield(), Is.Zero); yield return null;
        }
        [UnityTest] public IEnumerator DashControlsApplyOnPathOnlyAndNeverOnBasicOrSkillHits()
        {
            var weapon = Equip(0, Ink("dash_use", "stun"), Ink("dash_use", "root"), Ink("dash_use", "slow"));
            Player.selected = 0; Player.grounded = true;
            var other = arena.enemies[1];
            other.Position = Player.Position + Vector2.right * 100; Physics2D.SyncTransforms();
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Dash));
            Assert.That(Player.Dashing, Is.True);
            Assert.That(Target.stunUntil, Is.GreaterThan(arena.Now));
            Assert.That(Target.rootUntil, Is.GreaterThan(arena.Now));
            Assert.That(arena.ReadRunInkSlow(Target), Is.GreaterThan(0));
            foreach (var group in RunInkGroup.Collect(weapon, Equipment.Catalog))
            {
                Assert.That(group.Description, Does.Contain("대쉬 경로의 적에게"));
                Assert.That(group.Description, Does.Not.Contain("이 무기의 공격에"));
            }
            Hit(Begin(0), other, 1); Hit(Begin(0, false), other, 1);
            Assert.That(other.stunUntil, Is.LessThanOrEqualTo(arena.Now));
            Assert.That(other.rootUntil, Is.LessThanOrEqualTo(arena.Now));
            Assert.That(arena.ReadRunInkSlow(other), Is.Zero);
            Advance(.5f);
            Assert.That(Player.Dashing, Is.False);
            Assert.That(weapon.Slots.All(m => m.ActiveRemaining > 0), Is.True, "the dash windows are still active");
            Hit(Begin(0), other, 1); Hit(Begin(0, false), other, 1);
            Assert.That(other.stunUntil, Is.LessThanOrEqualTo(arena.Now));
            Assert.That(other.rootUntil, Is.LessThanOrEqualTo(arena.Now));
            Assert.That(arena.ReadRunInkSlow(other), Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator DashAlreadyInFlightAppliesItsOriginalInkAfterSourceDestruction()
        {
            var source = Equip(0, Ink("dash_use", "slow")); var survivor = Equip(1, Ink("dash_use", "stun"));
            Player.selected = 0; Player.grounded = true;
            Target.Position = Player.Position + Vector2.right * 20; Physics2D.SyncTransforms();
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Dash));
            Assert.That(Player.Dashing, Is.True); Assert.That(source.Slots[0].ActiveRemaining, Is.GreaterThan(0));
            Equipment.RecordDamage(source.Id, source.Maximum); Equipment.FinishHit(); arena.SyncSketchLoadout();
            Target.Position = Player.Position; Physics2D.SyncTransforms(); driver.Step(.01f);
            Assert.That(source.Destroyed, Is.True);
            Assert.That(arena.ReadRunInkSlow(Target), Is.GreaterThan(0), "the continuing dash uses its destroyed original weapon");
            Assert.That(survivor.Slots[0].ActiveRemaining, Is.Zero, "the replacement weapon cannot lend its dash ink");
            yield return null;
        }
        [UnityTest] public IEnumerator DashHealWaitsForPathContactAndDoesNotMultiplyByTargetsOrTicks()
        {
            var weapon = Equip(0, Ink("dash_use", "heal"));
            var other = Equip(1, Ink("dash_use", "heal"));
            Player.selected = 0; Player.health = 80; Player.grounded = true;
            foreach (var enemy in arena.enemies) enemy.Position = new Vector2(30, 1000);
            Physics2D.SyncTransforms();
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Dash));
            Assert.That(Player.Dashing, Is.True);
            Assert.That(Player.health, Is.EqualTo(80)); Assert.That(weapon.Slots[0].Cooldown, Is.Zero);
            foreach (var enemy in arena.enemies) enemy.Position = Player.Position;
            Physics2D.SyncTransforms(); driver.Step(.01f);
            Assert.That(Player.health, Is.EqualTo(81));
            Assert.That(weapon.Slots[0].Cooldown, Is.GreaterThan(0));
            Assert.That(other.Slots[0].Cooldown, Is.Zero);
            Advance(.05f); Assert.That(Player.health, Is.EqualTo(81));
            yield return null;
        }
        [UnityTest] public IEnumerator DashWithoutEnemyContactKeepsHealReadyForTheWholeDash()
        {
            var weapon = Equip(0, Ink("dash_use", "heal"));
            Player.selected = 0; Player.health = 80; Player.grounded = true;
            foreach (var enemy in arena.enemies) enemy.Position = new Vector2(30, 1000);
            Physics2D.SyncTransforms(); driver.Step(.01f, new CombatInput(pressed: CombatButtons.Dash));
            Advance(.5f);
            Assert.That(Player.Dashing, Is.False); Assert.That(Player.health, Is.EqualTo(80));
            Assert.That(weapon.Slots[0].Cooldown, Is.Zero); yield return null;
        }
        [UnityTest] public IEnumerator FullHealthPathContactPreservesDuplicateHealProcsAndSlotOrder()
        {
            var weapon = Equip(0, Ink("dash_use", "heal"), Ink("dash_use", "heal"));
            Equipment.DebugSetSlotColor(weapon.Id, 0, "green");
            Player.selected = 0; Player.health = Player.maxHealth; Player.grounded = true;
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Dash));
            Assert.That(weapon.Slots.All(m => m.Cooldown == 0), Is.True);
            Player.health = Player.maxHealth - 1;
            foreach (var enemy in arena.enemies) enemy.Position = Player.Position;
            Physics2D.SyncTransforms(); driver.Step(.01f);
            Assert.That(Player.health, Is.EqualTo(Player.maxHealth));
            Assert.That(weapon.Slots[0].Value, Is.EqualTo(2));
            Assert.That(weapon.Slots[0].Cooldown, Is.GreaterThan(0));
            Assert.That(weapon.Slots[1].Cooldown, Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator BrokenSourceKeepsItsSlowWithoutChargingTheOtherWeapon()
        {
            var weapon = Equip(0, Ink("basic_attack", "slow")); var other = Equip(1);
            Hit(Begin(0), damage: 1); Equipment.RecordDamage(weapon.Id, weapon.Maximum); Equipment.FinishHit(); arena.SyncSketchLoadout();
            Assert.That(weapon.Destroyed, Is.True); float before = Target.health; Advance(.31f);
            Assert.That(before - Target.health, Is.Zero); Assert.That(arena.ReadRunInkSlow(Target), Is.GreaterThan(0)); Assert.That(other.Durability, Is.EqualTo(other.Maximum)); yield return null;
        }
        [UnityTest] public IEnumerator RevivedTargetDoesNotInheritSlowFromItsPreviousLife()
        {
            Equip(0, Ink("basic_attack", "slow")); Hit(Begin(0), damage: 1);
            Target.ResetEnemy(); Advance(.5f);
            Assert.That(arena.ReadRunInkSlow(Target), Is.Zero); Assert.That(Target.health, Is.EqualTo(Target.maxHealth)); yield return null;
        }
        [UnityTest] public IEnumerator ZeroGainCopiesPreserveShieldAndCooldownProcOpportunities()
        {
            var weapon = Equip(0, Ink("basic_attack", "shield"), Ink("basic_attack", "shield")); Player.maxHealth = Player.health = 30;
            Hit(Begin(0), damage: 1); Assert.That(arena.ReadInkShield(), Is.EqualTo(6));
            Assert.That(weapon.Slots[0].Cooldown, Is.GreaterThan(0));
            Assert.That(weapon.Slots[1].Cooldown, Is.Zero, $"maximum={Player.maxHealth}, cap={Equipment.Catalog.shieldCap}, values={weapon.Slots[0].Value}/{weapon.Slots[1].Value}, total={arena.ReadInkShield()}"); yield return null;
        }
        [UnityTest] public IEnumerator ExistingRecoveryCancelBonusIsCapturedAndAddedToTheRedBonus()
        {
            Equip(0, Ink("basic_attack", "basic_attack_damage_increase", "stun")); Target.stunUntil = arena.Now + 2;
            var property = typeof(CombatPlayer).GetProperty("RecoveryCancelDamageBonus", BindingFlags.Instance | BindingFlags.NonPublic);
            property.SetValue(Player, .1f); var origin = Begin(0); property.SetValue(Player, 0f);
            Assert.That(Hit(origin), Is.EqualTo(140).Within(.002)); yield return null;
        }

        [UnityTest] public IEnumerator BossStatusRefreshesEveryHitWithoutAnInternalGateAndHudTracksWeaponWindow()
        {
            var weapon = Equip(0, Ink("basic_attack", "stun"));
            Target.isBoss = true; Target.BossControl.Reset();
            Hit(Begin(0), damage: 1); var mount = weapon.Slots[0];
            float firstEnd = Target.stunUntil;
            Assert.That(Target.BossControl.StunRemaining(arena.Now), Is.Zero);
            var slots = new List<BuffSlot>(); Player.BuffSlotSource.Collect(slots);
            Assert.That(slots.Count, Is.EqualTo(1)); Assert.That(slots[0].Icon, Is.EqualTo(BuffSlotIcon.Stun));
            Assert.That(slots[0].ActiveRemaining(arena.Now), Is.EqualTo(1.7f).Within(.002), "the slot shows the granting window as its duration");
            Assert.That(slots[0].ActiveDuration, Is.EqualTo(1.7f).Within(.002));
            Assert.That(slots[0].Remaining(arena.Now), Is.EqualTo(slots[0].Cooldown).Within(.002), "the cooldown waits for the window to end");
            Advance(.5f); Hit(Begin(0), damage: 1);
            Assert.That(Target.stunUntil, Is.EqualTo(firstEnd + .5f).Within(.002));
            Assert.That(mount.ActiveRemaining, Is.EqualTo(1.2f).Within(.002));
            Advance(1.21f); slots.Clear(); Player.BuffSlotSource.Collect(slots);
            Assert.That(slots[0].ActiveRemaining(arena.Now), Is.Zero);
            Assert.That(slots[0].Remaining(arena.Now), Is.EqualTo(2.99f).Within(.003));
            Advance(3); Hit(Begin(0), damage: 1);
            Assert.That(Target.stunUntil - arena.Now, Is.EqualTo(1).Within(.002));
            Target.isBoss = false; yield return null;
        }
        [UnityTest] public IEnumerator ScaledSwordKeepsFourAndTwoHitKillsAndDurabilityFraction()
        {
            var plain = Equip(0);
            var focused = Equip(1, Ink("basic_attack", "stun"),
                Ink("basic_attack", "basic_attack_damage_increase", "stun"),
                Ink("basic_attack", "basic_attack_damage_increase", "stun"),
                Ink("basic_attack", "basic_attack_damage_increase", "stun"));
            float damage = arena.catalog.forms.Single(f => f.id == "base_sword").basics[0].damage;
            Assert.That(damage, Is.InRange(60, 69));
            Target.maxHealth = Target.health = 230;
            var other = arena.enemies[1]; other.maxHealth = other.health = 230;
            for (int i = 0; i < 3; i++) { Hit(Begin(0), damage: damage); Assert.That(Target.Dead, Is.False); }
            Hit(Begin(0), damage: damage); Assert.That(Target.Dead, Is.True);
            Assert.That(Hit(Begin(1), other, damage), Is.EqualTo(123.5f).Within(.01));
            Assert.That(other.Dead, Is.False);
            Hit(Begin(1), other, damage); Assert.That(other.Dead, Is.True);
            Assert.That(focused.Maximum - focused.Durability, Is.EqualTo(230).Within(.01), "Overkill still does not consume durability");
            Assert.That((focused.Maximum - focused.Durability) / focused.Maximum, Is.EqualTo(230f / 24000).Within(.00001), "230 of 6,000 + 4 slots x 4,500");
            Assert.That(Player.maxHealth, Is.EqualTo(100), "Player health uses its original unit");
            yield return null;
        }

        [UnityTest] public IEnumerator BuffGroupsCombineCopiesButKeepWeaponsAndTriggersSeparate()
        {
            var stun = Ink("basic_attack", "stun");
            var dash = Ink("dash_use", "stun");
            var first = Equip(0, stun, stun, dash);
            Equip(1, stun);
            var slots = new List<BuffSlot>(); Player.BuffSlotSource.Collect(slots);
            Assert.That(slots.Count, Is.EqualTo(3));
            Assert.That(slots[0].Tooltip, Does.Contain("무기 1").And.Contain("2칸").And.Contain("기절 2초").And.Contain("3.13초").And.Contain("쿨타임 2초"));
            Assert.That(slots[1].Trigger, Is.EqualTo(BuffSlotTrigger.Dash));
            Assert.That(slots[2].Tooltip, Does.Contain("무기 2").And.Contain("기절 1초"));
            Hit(Begin(0), damage: 1);
            slots.Clear(); Player.BuffSlotSource.Collect(slots);
            Assert.That(slots[0].ActiveRemaining(arena.Now), Is.EqualTo(1.7f + 4.3f / 3).Within(.002));
            Assert.That(slots[2].ActiveRemaining(arena.Now), Is.Zero); Assert.That(slots[2].Remaining(arena.Now), Is.Zero);
            var group = RunInkGroup.Collect(first, Equipment.Catalog).First();
            Assert.That(slots[0].Tooltip, Does.Contain(group.Description), "HUD and inventory share the final description");
            yield return null;
        }

        [UnityTest] public IEnumerator GroupSummariesIncludeEnhancedCopiesAndPartialCooldowns()
        {
            var slow = Ink("basic_attack", "slow");
            var red = Ink("basic_attack", "basic_attack_damage_increase", "slow");
            var weapon = Equip(0, slow, red, red);
            Equipment.DebugSetSlotColor(weapon.Id, 0, "purple");
            Equipment.DebugSetSlotColor(weapon.Id, 1, "red");
            var groups = RunInkGroup.Collect(weapon, Equipment.Catalog).ToArray();
            Assert.That(groups[0].Amount, Is.EqualTo(.6f).Within(.001));
            Assert.That(groups[0].StatusDuration, Is.EqualTo(1.5f));
            Assert.That(groups[0].Duration, Is.EqualTo(3.85f).Within(.001));
            Assert.That(groups[0].Cooldown, Is.EqualTo(2));
            Assert.That(groups[1].Description, Does.Contain("+75%").And.Contain("1.88초").And.Contain("쿨타임 없음"));
            var heal = Ink("dash_use", "heal");
            var other = Equip(1, heal, heal);
            other.Slots[0].Activate();
            var slots = new List<BuffSlot>(); Player.BuffSlotSource.Collect(slots);
            Assert.That(slots.Last().Tooltip, Does.Contain("현재 1/2칸 준비"));
            Assert.That(slots.Last().Remaining(arena.Now), Is.EqualTo(heal.cooldown).Within(.001));
            yield return null;
        }

        [UnityTest] public IEnumerator ConditionalHasteUsesColorValuesWithoutIncreasingDamageAndExpires()
        {
            var haste = Ink("basic_attack", "basic_attack_speed_increase", "slow");
            var weapon = Equip(0, haste, Ink("basic_attack", "slow"));
            Assert.That(Player.BasicSpeed, Is.EqualTo(1).Within(.001));
            Assert.That(Hit(Begin(0, false)), Is.EqualTo(100).Within(.002), "A skill cannot activate basic haste.");
            Assert.That(arena.ReadRunInkBasicSpeedBonus, Is.Zero);
            Assert.That(Hit(Begin(0)), Is.EqualTo(100).Within(.002), "Haste must never enter the damage bucket.");
            Assert.That(Player.BasicSpeed, Is.EqualTo(1.2f).Within(.001));
            Assert.That(arena.ReadRunInkMountBuff(weapon.Slots[0].Id), Is.EqualTo(1.5f).Within(.002));
            Equipment.DebugSetSlotColor(weapon.Id, 0, "red");
            Hit(Begin(0));
            Assert.That(Player.BasicSpeed, Is.EqualTo(1.3f).Within(.001));
            Assert.That(arena.ReadRunInkMountBuff(weapon.Slots[0].Id), Is.EqualTo(2.25f).Within(.002));
            var slots = new List<BuffSlot>(); Player.BuffSlotSource.Collect(slots);
            Assert.That(slots.First().Icon, Is.EqualTo(BuffSlotIcon.BasicSpeed));
            Assert.That(slots.First().Tooltip, Does.Contain("공속 +30%"));
            Assert.That(arena.ReadRunInkBuffs().RedLeft, Is.GreaterThan(0), "Existing red VFX also displays haste.");
            Advance(2.26f);
            Assert.That(Player.BasicSpeed, Is.EqualTo(1).Within(.001));
            Assert.That(arena.ReadRunInkMountBuff(weapon.Slots[0].Id), Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator HasteStacksAcrossWeaponsCapsAndResetsWithLife()
        {
            var haste = Ink("basic_attack", "basic_attack_speed_increase", "stun");
            var first = Equip(0, haste, haste); var other = Equip(1, haste, haste);
            Target.stunUntil = arena.Now + 20;
            Hit(Begin(0)); Hit(Begin(1));
            Assert.That(arena.ReadRunInkBasicSpeedBonus, Is.EqualTo(.6f).Within(.001));
            Assert.That(arena.ReadRunInkMountBuff(first.Slots[0].Id), Is.EqualTo(3).Within(.002));
            Hit(Begin(1));
            Assert.That(arena.ReadRunInkBasicSpeedBonus, Is.EqualTo(.6f).Within(.001), "Reprocs refresh time, never add copies.");
            var method = typeof(CombatArena).GetMethod("RunBasicSpeedScale", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((float)method.Invoke(arena, new object[] { 1f, 1.5f, 1.15f }), Is.EqualTo(2).Within(.001));
            driver.Reset();
            Assert.That(arena.ReadRunInkBasicSpeedBonus, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator HasteSnapshotsNextBasicButLeavesSkillsAndAudioAtOriginalRate()
        {
            Equip(0, Ink("basic_attack", "basic_attack_speed_increase", "root"));
            Player.Position = new Vector2(-20, 1000); Physics2D.SyncTransforms();
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
            var previous = Player.currentCast; Assert.That(previous, Is.Not.Null);
            float original = previous.MotionDuration;
            var originalAudio = arena.GetComponentsInChildren<Deviant.Presentation.Unity.AudioCueOutputBinding>(true)
                .First(o => o.Source && o.Source.clip);
            float originalPitch = originalAudio.Source.pitch;
            Target.rootUntil = arena.Now + 5; Hit(Begin(0));
            Assert.That(previous.MotionDuration, Is.EqualTo(original).Within(.001), "A running action keeps its captured timeline.");
            Player.CancelAction(); Player.ClearInputState();
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
            var accelerated = Player.currentCast; Assert.That(accelerated, Is.Not.Null);
            Assert.That(accelerated.MotionDuration, Is.EqualTo(original / 1.2f).Within(.001));
            Assert.That(accelerated.ImpactTime, Is.EqualTo(.13f / 1.2f).Within(.001));
            var outputs = arena.GetComponentsInChildren<Deviant.Presentation.Unity.AudioCueOutputBinding>(true);
            Assert.That(outputs.Any(o => o.Source && o.Source.clip), Is.True);
            Assert.That(outputs.Where(o => o.Source && o.Source.clip).All(o => Mathf.Approximately(o.Source.pitch, originalPitch)), Is.True,
                "Attack haste preserves selected PCM compensation and never changes its audible pitch/rate.");
            Player.CancelAction(); Player.ClearInputState();
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Weapon1Special));
            Assert.That(Player.currentCast, Is.Not.Null);
            Assert.That(Player.currentCast.MotionDuration, Is.EqualTo(.37f).Within(.001));
            Assert.That(Player.currentCast.ImpactTime, Is.EqualTo(.155f).Within(.001));
            Player.CancelAction();
            yield return null;
        }

        [UnityTest] public IEnumerator BuffSlotsListEveryEquippedMountInSlotOrder()
        {
            var a = Ink("dash_use", "shield"); var b = Ink("skill_attack", "cooldown_reduction", "stun"); var c = Ink("weapon_switch", "root");
            Equip(0, a, b); Equip(1, c);
            var slots = new List<BuffSlot>(); Player.BuffSlotSource.Collect(slots);
            Assert.That(slots.Select(s => s.Icon), Is.EqualTo(new[] { BuffSlotIcon.Shield, BuffSlotIcon.SkillCooldown, BuffSlotIcon.Bind }));
            Assert.That(slots.Select(s => s.Trigger), Is.EqualTo(new[] { BuffSlotTrigger.Dash, BuffSlotTrigger.Skill, BuffSlotTrigger.Switch }));
            Assert.That(slots.All(s => s.Remaining(arena.Now) == 0), Is.True, "fresh mounts are ready");
            yield return null;
        }
    }
}
