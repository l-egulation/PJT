using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Run;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public sealed class RunInkDesignTests
    {
        SketchEquipment Make(int seed = 123)
        {
            var catalog = JsonUtility.FromJson<RunInkCatalog>(Resources.Load<TextAsset>("RunInks").text);
            var economy = new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text));
            return new SketchEquipment(catalog, economy, seed, new Dictionary<string, int[]> { ["base_sword"] = new[] { 0, 1, 2, 3 }, ["hammer"] = new[] { 0, 1, 2, 3 } });
        }
        static RunInkWeapon Obtain(SketchEquipment equipment)
        {
            // An ink-only choice (the ink half of a double reward) is taken until a weapon choice comes.
            while (equipment.RewardChoices().All(c => c.Weapon == null)) { if (equipment.PendingReward) equipment.ChooseReward(0); else { equipment.RecordKill(false); equipment.OpenReward(); } }
            int index = equipment.RewardChoices().ToList().FindIndex(c => c.Weapon != null);
            Assert.That(index, Is.GreaterThanOrEqualTo(0)); return equipment.ChooseReward(index).Weapon;
        }
        [Test] public void DebugWeaponChangesKeepIdentityMountsDurabilityAndNormalConfirmationLock()
        {
            var equipment = Make(); equipment.Economy.Earn(1000);
            var weapon = equipment.DebugConfigureWeapon(0, "base_sword", 0);
            var mount = weapon.Slots[0]; var ink = equipment.Catalog.inks[0];
            equipment.DebugSetInk(weapon.Id, 0, ink.id); mount.Activate(); equipment.Tick(1);
            float remaining = mount.Cooldown;
            equipment.RecordDamage(weapon.Id, 37); weapon.RememberSkill(1, 0, 10, 6);
            var changed = equipment.DebugConfigureWeapon(0, "hammer", 2);
            Assert.That(changed, Is.SameAs(weapon)); Assert.That(changed.Form, Is.EqualTo("hammer"));
            Assert.That(changed.Skill, Is.EqualTo(2)); Assert.That(changed.SkillCapacity, Is.Zero);
            Assert.That(changed.Slots[0], Is.SameAs(mount)); Assert.That(mount.Ink, Is.SameAs(ink));
            Assert.That(mount.Cooldown, Is.EqualTo(remaining)); Assert.That(changed.Durability, Is.EqualTo(changed.Maximum - 37));
            Assert.That(changed.Confirmed, Is.True); Assert.That(equipment.RegisteredCount, Is.Zero);
            Assert.That(equipment.NormalWeaponsAcquired, Is.Zero); Assert.That(equipment.Kills, Is.Zero);
            Assert.That(equipment.Economy.Gold, Is.EqualTo(1000));
            Assert.Throws<InvalidOperationException>(() => equipment.Place(weapon.Id, 0, null));
        }
        [Test] public void DebugEquipmentBypassesSwapAdmissionWithoutSpendingTheTimerOrDiscardingWeapons()
        {
            var equipment = Make(); var stored = equipment.DebugConfigureWeapon(0, "base_sword", 0);
            equipment.Store(0); equipment.Tick(1); Assert.That(equipment.ChangeCooldownRemaining, Is.EqualTo(4).Within(1e-4));
            var weapon = equipment.DebugConfigureWeapon(0, "hammer", 1);
            Assert.That(equipment.ChangeCooldownRemaining, Is.EqualTo(4).Within(1e-4)); Assert.That(stored.Destroyed, Is.False);
            Assert.That(equipment.Find(stored.Id), Is.SameAs(stored));
            equipment.DebugConfigureWeapon(0, null, 0);
            Assert.That(equipment.Equipped(0), Is.Null); Assert.That(equipment.Find(weapon.Id), Is.SameAs(weapon));
            Assert.That(equipment.ChangeCooldownRemaining, Is.EqualTo(4).Within(1e-4)); Assert.That(equipment.Weapons.Count, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => equipment.DebugConfigureWeapon(0, "missing", 0));
            Assert.Throws<InvalidOperationException>(() => equipment.DebugConfigureWeapon(0, "hammer", 4));
            Assert.Throws<InvalidOperationException>(() => equipment.DebugConfigureWeapon(2, "hammer", 0));
            Assert.That(equipment.Equipped(0), Is.Null); Assert.That(equipment.Weapons.Count, Is.EqualTo(2));
        }
        [Test] public void DebugPaletteUsesAll39InksAndColorMatchingWhileKeepingExistingMounts()
        {
            var equipment = Make(); var weapon = equipment.DebugConfigureWeapon(0, "base_sword", 0);
            var mounts = weapon.Slots.ToArray();
            foreach (var ink in equipment.Catalog.inks)
            {
                equipment.DebugSetSlotColor(weapon.Id, 0, "neutral");
                equipment.DebugSetInk(weapon.Id, 0, ink.id);
                Assert.That(mounts[0].Value, Is.EqualTo(ink.baseValue));
                mounts[0].Activate(); equipment.Tick(.25f); float cooldown = mounts[0].Cooldown;
                equipment.DebugSetSlotColor(weapon.Id, 0, ink.color);
                Assert.That(mounts[0].Value, Is.EqualTo(ink.enhancedValue));
                Assert.That(mounts[0].Cooldown, Is.EqualTo(cooldown));
            }
            equipment.DebugResizePalette(weapon.Id, 5);
            Assert.That(weapon.Slots.Take(mounts.Length), Is.EqualTo(mounts));
            equipment.DebugSetInk(weapon.Id, 4, equipment.Catalog.inks[0].id);
            Assert.Throws<InvalidOperationException>(() => equipment.DebugResizePalette(weapon.Id, 3));
            Assert.That(weapon.Slots.Count, Is.EqualTo(5));
            equipment.DebugSetInk(weapon.Id, 4, null); equipment.DebugResizePalette(weapon.Id, mounts.Length);
            Assert.That(weapon.Slots, Is.EqualTo(mounts)); Assert.That(equipment.RegisteredCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => equipment.DebugSetInk(weapon.Id, 0, "missing"));
            Assert.Throws<InvalidOperationException>(() => equipment.DebugSetSlotColor(weapon.Id, 0, "missing"));
            Assert.Throws<InvalidOperationException>(() => equipment.DebugResizePalette(weapon.Id, 0));
        }
        [Test] public void SharedContributionCapKeepsSixActualSlotsAndEnhancedInvestment()
        {
            var catalog = Make().Catalog;
            Assert.That(catalog.maximumContributingInkCopies, Is.EqualTo(6));
            Assert.That(catalog.investment.baseContribution, Is.EqualTo(1));
            Assert.That(catalog.investment.enhancedContribution, Is.EqualTo(1.5f));
            catalog.maximumContributingInkCopies = 0;
            Assert.Throws<ArgumentException>(() => catalog.Validate(), "old or missing contribution rules must not silently disable all buffs");
        }
        [Test] public void CatalogHas39UniqueNamesAndFiveColorIconsWithBaseEnhancedValues()
        {
            var equipment = Make(); var inks = equipment.Catalog.inks;
            Assert.That(inks.Length, Is.EqualTo(39)); Assert.That(inks.Select(i => i.name).Distinct().Count(), Is.EqualTo(39));
            Assert.That(inks.Select(i => i.icon).Distinct().Count(), Is.EqualTo(5));
            foreach (var ink in inks)
            {
                Assert.That(ink.icon, Is.EqualTo("LootPickups/ink-flat-" + ink.color), ink.id);
                var icon = Resources.Load<Sprite>(ink.icon);
                Assert.That(icon, Is.Not.Null, ink.id);
                Assert.That(icon.name, Is.EqualTo("ink-flat-" + ink.color));
            }
            Assert.That(inks.Count(i => i.color == "purple"), Is.EqualTo(12));
            Assert.That(inks.Count(i => i.color == "blue"), Is.EqualTo(6));
            Assert.That(inks.Any(i => i.effect == "poison" || i.condition == "poison"), Is.False);
            Assert.That(inks.All(i => i.enhancedValue > i.baseValue && !string.IsNullOrEmpty(i.description)), Is.True);
        }
        [Test] public void HasteHasThreeConditionsAndApprovedBottleArtInInventoryAndRewards()
        {
            var inks = Make().Catalog.inks.Where(i => i.effect == "basic_attack_speed_increase").ToArray();
            Assert.That(inks.Select(i => i.condition), Is.EquivalentTo(new[] { "stun", "root", "slow" }));
            var theme = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Deviant.UI.Theme.ApprovedTheme")).First(t => t != null);
            foreach (var ink in inks)
            {
                Assert.That(ink.baseValue, Is.EqualTo(.2f)); Assert.That(ink.enhancedValue, Is.EqualTo(.3f));
                var record = theme.GetMethod("FindInk").Invoke(null, new object[] { ink.id });
                Assert.That(record, Is.Not.Null, ink.id);
                Assert.That(record.GetType().GetField("effect").GetValue(record), Is.EqualTo("ui-assets-v1/attackSpeed"));
                var texture = (Texture2D)theme.GetMethod("Ink").Invoke(null, new object[] { ink.id });
                Assert.That(texture, Is.Not.Null, ink.id);
                Assert.That(texture.width, Is.GreaterThan(0));
            }
        }
        [Test] public void ExhaustionHidesOnlyTheSelectionEntryAndReacquisitionRestoresItAcrossStages()
        {
            var equipment = Make(); var weapon = equipment.Find(equipment.State.Arsenal.Acquire("base_sword", "0", new string[4]).Id); var ink = equipment.Catalog.inks[0];
            equipment.DebugResizePalette(weapon.Id, 5); equipment.Register(ink.id);
            var previous = equipment.Find(equipment.State.Arsenal.Acquire("base_sword", "0", new string[5]).Id);
            equipment.DebugResizePalette(previous.Id, 5);
            for (int i = 0; i < 5; i++) equipment.Place(previous.Id, i, ink.id);
            equipment.Confirm(previous.Id);
            for (int i = 0; i < 5; i++) equipment.Place(weapon.Id, i, ink.id);
            Assert.That(equipment.AvailableInkUses(ink.id), Is.Zero);
            Assert.That(equipment.Registered, Does.Contain(ink), "reserved is not spent yet");
            equipment.Confirm(weapon.Id);
            Assert.That(equipment.Registered.Contains(ink), Is.False); Assert.That(equipment.RegisteredCount, Is.Zero);
            equipment.Equip(weapon.Id, 0);
            Assert.That(weapon.Slots.All(m => m.Ink == ink && m.Fixed), Is.True);
            weapon.Slots[0].Activate(); Assert.That(weapon.Slots[0].Cooldown, Is.EqualTo(ink.cooldown));
            equipment.Register(ink.id); equipment.BeginStage(2);
            Assert.That(equipment.Registered, Does.Contain(ink));
            Assert.That(equipment.RemainingInkUses(ink.id), Is.EqualTo(10));
            Assert.That(Make().RegisteredCount, Is.Zero, "a new run has no carried ink supply");
        }

        [TestCase(1f, 1.7f, 3f, 1f)]
        [TestCase(1.5f, 2.5f, 2.5f, 1.5f)]
        [TestCase(3f, 4.5666667f, 1f, 3f)]
        [TestCase(4f, 6f, 1f, 3f)]
        [TestCase(5f, 7.4333333f, 1f, 3f)]
        public void ControlInvestmentKeepsGrowingAfterReuseAndTargetDurationCaps(float copies, float window, float cooldown, float target)
        {
            var tuning = Make().Catalog.investment;
            foreach (string effect in new[] { "stun", "root" })
            {
                Assert.That(tuning.Window(effect, copies), Is.EqualTo(window).Within(.0001));
                Assert.That(tuning.Cooldown(effect, copies), Is.EqualTo(cooldown).Within(.0001));
            }
            Assert.That(tuning.StatusDuration(copies), Is.EqualTo(target));
        }
        [Test] public void FirstRewardGivesAStandaloneEffectInkAndABlueprint()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var e = Make(seed);
                e.State.DebugAddReward(); e.OpenReward();
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var choices = e.RewardChoices();
                    Assert.That(choices.Count, Is.EqualTo(3));
                    Assert.That(choices.All(c => c.Ink != null && e.State.IsStandaloneInk(c.Ink.id)), Is.True);
                    Assert.That(choices.Select(c => c.Ink.id).Distinct().Count(), Is.EqualTo(3));
                    e.State.RerollPending();
                }
                string ink = e.RewardChoices()[0].Ink.id;
                e.ChooseReward(0);
                Assert.That(e.RemainingInkUses(ink), Is.EqualTo(10));
                Assert.That(e.RewardChoices().All(c => c.Weapon != null), Is.True);
                Assert.That(e.ChooseReward(0).Weapon, Is.Not.Null);
                Assert.That(e.PendingReward, Is.False);
                Assert.That(e.State.RunRewardsOpened, Is.EqualTo(1));
            }
        }

        [Test] public void FirstInkOfferIsStandaloneAndLaterGuaranteeUsesOnlyRemainingBagInk()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var equipment = Make(seed); Obtain(equipment);
                equipment.State.DebugAddReward(); equipment.OpenReward();
                Assert.That(equipment.RewardChoices().All(c => c.Ink != null && equipment.State.IsStandaloneInk(c.Ink.id)), Is.True);
                equipment.ChooseReward(0); equipment.ChooseReward(0);
                equipment.State.DebugAddReward(); equipment.OpenReward();
                var inks = equipment.RewardChoices().Where(c => c.Ink != null).ToArray();
                Assert.That(inks.Any(c => equipment.State.IsUsableInk(c.Ink.id)), Is.True);
            }
            var e = Make();
            var purple = e.Catalog.inks.Single(i => i.trigger == "basic_attack" && i.effect == "stun");
            var red = e.Catalog.inks.Single(i => i.effect == "basic_attack_damage_increase" && i.condition == "stun");
            Assert.That(e.State.IsUsableInk(red.id), Is.False);
            e.Register(purple.id); Assert.That(e.State.IsUsableInk(red.id), Is.True);
            for (int slot = 0; slot < 2; slot++)
            {
                var w = e.Find(e.State.Arsenal.Acquire("base_sword", "0", new string[5]).Id);
                for (int i = 0; i < 5; i++) e.Place(w.Id, i, purple.id);
                e.Confirm(w.Id);
            }
            Assert.That(e.State.IsUsableInk(red.id), Is.False, "mounted control is not remaining bag ink");
        }
        [Test] public void InvalidDebugDraftCannotPartiallyConsumeOrLockOnConfirmation()
        {
            var equipment = Make(); var weapon = Obtain(equipment); equipment.DebugResizePalette(weapon.Id, 3);
            var ink = equipment.Catalog.inks[0]; var other = equipment.Catalog.inks[1];
            equipment.Register(ink.id); equipment.Place(weapon.Id, 0, ink.id);
            equipment.DebugSetInk(weapon.Id, 1, other.id);
            Assert.Throws<InvalidOperationException>(() => equipment.Confirm(weapon.Id));
            Assert.That(equipment.RemainingInkUses(ink.id), Is.EqualTo(10));
            Assert.That(weapon.Confirmed, Is.False); Assert.That(weapon.Slots.All(m => !m.Fixed), Is.True);
            equipment.Place(weapon.Id, 1, null); equipment.Confirm(weapon.Id);
            Assert.That(equipment.RemainingInkUses(ink.id), Is.EqualTo(9));
        }

        [Test] public void ConfirmationLocksFilledAndEmptySlotsAcrossStorageAndReequip()
        {
            var equipment = Make(); var weapon = equipment.Find(equipment.State.Arsenal.Acquire("base_sword", "0", new string[4]).Id); var ink = equipment.Catalog.inks[0];
            equipment.DebugResizePalette(weapon.Id, 3);
            Assert.Throws<InvalidOperationException>(() => equipment.Equip(weapon.Id, 0));
            equipment.Register(ink.id); equipment.Place(weapon.Id, 0, ink.id); equipment.Place(weapon.Id, 1, ink.id);
            Assert.That(equipment.RegisteredCount, Is.EqualTo(1));
            equipment.Place(weapon.Id, 1, null); equipment.Confirm(weapon.Id); equipment.Equip(weapon.Id, 0);
            Assert.Throws<InvalidOperationException>(() => equipment.Place(weapon.Id, 1, ink.id));
            Assert.That(weapon.Slots.All(s => s.Fixed), Is.True);
            Assert.Throws<InvalidOperationException>(() => equipment.Place(weapon.Id, 1, null));
            Assert.Throws<InvalidOperationException>(() => equipment.Place(weapon.Id, 0, null));
            equipment.Store(0); equipment.Tick(10); equipment.Equip(weapon.Id, 1);
            Assert.That(weapon.Slots[0].Ink, Is.SameAs(ink)); Assert.That(weapon.Slots[1].Ink, Is.Null);
            Assert.Throws<InvalidOperationException>(() => equipment.Place(weapon.Id, 1, ink.id));
        }
        [Test] public void StorageFreezesMountAndSkillCooldownsButInactiveEquippedWeaponAdvances()
        {
            var equipment = Make(); var weapon = Obtain(equipment); var ink = equipment.Catalog.inks[0];
            equipment.Register(ink.id); equipment.Place(weapon.Id, 0, ink.id); equipment.Confirm(weapon.Id); equipment.Equip(weapon.Id, 0); equipment.Tick(5); // let the change cooldown from the equip run out
            weapon.Slots[0].Activate(); equipment.Tick(1); float remaining = weapon.Slots[0].Cooldown;
            equipment.Store(0); equipment.Tick(20); Assert.That(weapon.Slots[0].Cooldown, Is.EqualTo(remaining));
            equipment.Equip(weapon.Id, 1); equipment.Tick(1); Assert.That(weapon.Slots[0].Cooldown, Is.EqualTo(remaining - 1));
            var charges = new ChargePool(1, 10); charges.Spend(1); charges.Suspend(3); charges.Resume(30);
            Assert.That(charges.NextReady, Is.EqualTo(38)); charges.Tick(37); Assert.That(charges.Available, Is.Zero);
            charges.Tick(38); Assert.That(charges.Available, Is.EqualTo(1));
        }
        [Test] public void DurabilityFinishesTheWholeHitBeforeDestructionAndDoesNotChargeOtherWeapons()
        {
            var equipment = Make(); var weapon = Obtain(equipment); equipment.Confirm(weapon.Id); equipment.Equip(weapon.Id, 0);
            equipment.RecordDamage(weapon.Id, weapon.Maximum - 5); equipment.FinishHit();
            equipment.RecordDamage(weapon.Id, 10); equipment.RecordDamage(weapon.Id, 10);
            Assert.That(weapon.Destroyed, Is.False); Assert.That(weapon.Durability, Is.Zero);
            equipment.FinishHit(); Assert.That(weapon.Destroyed, Is.True); Assert.That(equipment.Equipped(0), Is.Null);
            equipment.RecordDamage(weapon.Id, 999); Assert.That(weapon.Durability, Is.Zero);
        }
        // The first two rewards give an ink and a weapon; later mixed and double offers follow run open order.
        [Test] public void BankedMainGaugeOffersFollowTheOpeningRewardsAndMainSlotProgression()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var equipment = Make(seed); var acquiredInks = new HashSet<string>();
                for (int round = 0; round < 6; round++)
                {
                    while (!equipment.PendingReward) { equipment.RecordKill(false); equipment.OpenReward(); }
                    var choices = equipment.RewardChoices();
                    Assert.That(equipment.RewardChoices()[0], Is.SameAs(choices[0]));
                    int slots = new[] { 4, 4, 4, 4, 4, 4 }[round];
                    Assert.That(choices.Where(c => c.Weapon != null).All(c => c.Weapon.Slots.Count == slots), Is.True);
                    Assert.That(choices.Where(c => c.Weapon != null).Select(c => c.Weapon.CompositionKey).Distinct().Count(), Is.EqualTo(choices.Count(c => c.Weapon != null)));
                    if (round == 0 || round == 1 || round == 5) // the run's 1st, 2nd and 6th rewards are double
                    {
                        Assert.That(choices.All(c => c.Ink != null), Is.True, "inks first");
                        acquiredInks.Add(choices[0].Ink.id); equipment.ChooseReward(0);
                        choices = equipment.RewardChoices();
                        Assert.That(choices.All(c => c.Weapon != null && c.Weapon.Slots.Count == slots), Is.True, "then weapons");
                    }
                    else { Assert.That(choices.Any(c => c.Weapon != null), Is.True); Assert.That(choices.Any(c => c.Ink != null), Is.True); }
                    int index = choices.ToList().FindIndex(c => c.Weapon != null);
                    equipment.ChooseReward(index < 0 ? 0 : index);
                    Assert.That(equipment.PendingReward, Is.False);
                }
                Assert.That(equipment.NormalWeaponsAcquired, Is.EqualTo(6));
                Assert.That(equipment.RegisteredCount, Is.EqualTo(acquiredInks.Count));
                Assert.That(equipment.Registered.Sum(ink => equipment.RemainingInkUses(ink.id)), Is.EqualTo(30));
                Assert.That(equipment.OffersCompleted, Is.EqualTo(6));
            }
        }
        [Test] public void GeneratedAdapterKeepsMainRulesAndSharesNativeWeaponsAcrossStages()
        {
            // The generated catalog uses the approved x5 enemy-HP unit; bare planner defaults are legacy units.
            var equipment = Make(); var main = new SketchRewardRules { SmallBelow = 225, LargeFrom = 390 };
            for (int stage = 1; stage <= 3; stage++)
            {
                var rules = equipment.State.Planner.Rules;
                Assert.That(rules.SlotCountsByOffer[stage], Is.EqualTo(main.SlotCountsByOffer[stage]));
                Assert.That(rules.EnhancedSlotChance[stage], Is.EqualTo(main.EnhancedSlotChance[stage]));
                foreach (float hp in new[] { 220, 225, 389, 390 })
                    Assert.That(rules.GaugeGain(stage, hp), Is.EqualTo(main.GaugeGain(stage, hp)));
                for (int reward = 0; reward < 20; reward++)
                    Assert.That(rules.GaugeRequirement(reward), Is.EqualTo(main.GaugeRequirement(reward)));
            }
            var weapon = Obtain(equipment);
            Assert.That(weapon.Maximum, Is.EqualTo(24000), "6,000 + 4 slots x 4,500 (user decision 2026-11-16)");
            equipment.Confirm(weapon.Id); equipment.Equip(weapon.Id, 0);
            Assert.That(equipment.State.Arsenal.Equipped(0), Is.SameAs(weapon.Model));
            equipment.RecordDamage(weapon.Id, 40); equipment.FinishHit();
            Assert.That(weapon.Model.DurabilityUsed, Is.EqualTo(40));
            var native = equipment.State; equipment.BeginStage(2);
            Assert.That(equipment.State, Is.SameAs(native)); Assert.That(equipment.Equipped(0), Is.SameAs(weapon));
            var stock = equipment.Shop("chapter2:M");
            foreach (var item in stock)
                Assert.That(item.Price, Is.EqualTo(item.Weapon == null ? 400 : 350 + 175 * item.Weapon.Slots.Count + 90 * item.Weapon.Model.EnhancedSlots));
            equipment.BeginStage(3);
            Assert.Throws<InvalidOperationException>(() => equipment.RollWeapon());
            Assert.Throws<InvalidOperationException>(() => equipment.Shop("chapter3:M"));
        }
        [Test] public void ShopKeepsAnInkAcquiredElsewhereAndSoldStockStaysSold()
        {
            var equipment = Make(); equipment.Economy.Earn(10000); var first = equipment.Shop("shop").ToArray();
            Assert.That(first.Length, Is.EqualTo(6)); equipment.Register(first[3].Ink.id); var after = equipment.Shop("shop");
            Assert.That(after[3].Ink.id, Is.EqualTo(first[3].Ink.id)); Assert.That(after[4], Is.SameAs(first[4]));
            Assert.That(after[0], Is.SameAs(first[0])); var bought = equipment.Buy("shop", 4);
            Assert.That(bought.Sold, Is.True); Assert.That(equipment.Shop("shop")[4], Is.SameAs(bought));
            Assert.Throws<InvalidOperationException>(() => equipment.Buy("shop", 4));
        }
    }
}
