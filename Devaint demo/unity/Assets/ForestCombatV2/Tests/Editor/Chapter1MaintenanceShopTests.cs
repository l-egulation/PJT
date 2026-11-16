using System.Collections;
using System.Linq;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1MaintenanceShopTests
    {
        static bool UpgradeSelectedSlot(Chapter1MaintenanceShop shop)=>shop.GrowthTransaction(()=>shop.Growth.Upgrade(shop.GrowthWeapon(0),0),out _);
        [UnityTest] public IEnumerator FacilityScreenSellsUpgradesAndAppliesStampsWithRunPrices()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            yield return null;
            CheckFacility();
            yield return new ExitPlayMode();
        }
        // EnterPlayMode reloads the test assembly; keep capturing lambdas outside its iterator.
        static void CheckFacility()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }

            var shop = world.GetComponent<Chapter1MaintenanceShop>();
            Assert.That(shop, Is.Not.Null);
            var player = world.arena.Player;
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            player.Position = offer.OfferPosition(1);
            Assert.That(offer.TryChoose(1), Is.True, "hammer blueprint into slot 1");
            Assert.That(shop.TryOpen(), Is.False, "the screen opens only inside a facility");

            var rooms = world.Data.rooms.Where(r => Chapter1MaintenanceFacility.IsFacility(r.kind)).ToArray();
            var room = rooms[0];
            player.Position = new Vector2(room.x + room.w / 2f, -(room.y + room.entryY));
            Assert.That(shop.TryOpen(), Is.True);
            Assert.That(shop.IsOpen, Is.True);
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True);

            var service = shop.Service;
            service.Economy.Earn(1000);
            // The hammer received its departure palette when it was equipped; the shop sells none.
            var palette = shop.Growth.Equipped(shop.GrowthWeapon(0));
            Assert.That(palette, Is.Not.Null);
            Assert.That(palette.Slots.Count, Is.EqualTo(3), "no village plan: the base three slots");
            Assert.That(palette.Slots.All(slot => slot.Color != "colorless"), Is.True, "colorless only comes from the wildcard unlock");
            var bag = world.arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>();
            var rewards = world.GetComponent<Chapter1Rewards>();
            Assert.That(rewards.InkPool(), Does.Not.Contain("form.hammer"), "starting forms never enter monster rewards");
            // Ink stall (user decision 2026-11-09): 2~3 different grade 3~4 inks at fixed prices replace the random draw.
            var stock = shop.InkStock;
            Assert.That(stock.Count, Is.InRange(2, 3));
            Assert.That(stock.All(o => o.Part.grade == 3 || o.Part.grade == 4), Is.True);
            Assert.That(stock.Select(o => o.Part.id).Distinct().Count(), Is.EqualTo(stock.Count));
            Assert.That(stock.All(o => o.Price == service.Economy.InkShopPrice(o.Part.grade)), Is.True);
            int bagBefore = bag.Inventory.Used;
            int coloredBefore=shop.Growth.Inks.Count;
            var bought = stock[0];
            Assert.That(shop.TryBuyInk(0, out var buyMessage), Is.True, buyMessage);
            Assert.That(buyMessage, Does.StartWith(bought.Part.name + " 구매"));
            Assert.That(bought.Sold, Is.True);
            Assert.That(shop.TryBuyInk(0, out buyMessage), Is.False, "a sold offer cannot be bought again");
            Assert.That(bag.Inventory.Used, Is.EqualTo(bagBefore), "colored inks have a dedicated palette inventory");
            Assert.That(shop.Growth.Inks.Count,Is.EqualTo(coloredBefore+1));
            Assert.That(shop.Growth.Inks.Last().Part, Is.SameAs(bought.Part));
            Assert.That(shop.TryUpgradeMvpAttack(out _), Is.True);
            Assert.That(UpgradeSelectedSlot(shop),Is.True);
            Assert.That(service.Stash.InkParts, Is.Zero, "the stall does not add colour parts");
            Assert.That(shop.Growth.AttackLevel, Is.EqualTo(1));
            Assert.That(shop.Growth.Equipped(shop.GrowthWeapon(0)).Slots[0].Level, Is.EqualTo(1));
            Assert.That(shop.Growth.Palettes.Count,Is.EqualTo(1), "one palette per equipped weapon");
            Assert.That(service.Economy.Gold, Is.EqualTo(1000 - bought.Price - 60 - 50));

            Assert.That(shop.SlotBlueprintKind(0), Is.EqualTo("blunt"));
            // Stamps (user decision 2026-11-03): the first stamp of a kind is free, replacing costs 20G and destroys the
            // old one; weapon stamps must fit the blueprint kind and the same effect is refused without payment.
            var stash = service.Stash; var forms = world.arena.catalog.forms; var hammer = player.weapons[0];
            var whip = stash.AddWeaponStamp("sword", "whip");
            var shield = stash.AddWeaponStamp("blunt", "shield");
            var greatsword = stash.AddWeaponStamp("blunt", "greatsword");
            var range = stash.AddSkillStamp(1); var followUp = stash.AddSkillStamp(3);
            int gold = service.Economy.Gold;
            Assert.That(shop.TryApplyStamp(0, whip.Id, out var stampError), Is.False);
            Assert.That(stampError, Is.EqualTo("이 도면에 맞는 무기 스탬프가 아닙니다."));
            Assert.That(shop.TryApplyStamp(0, shield.Id, out stampError), Is.True, stampError);
            Assert.That(forms[hammer.FormIndex].id, Is.EqualTo("shield"));
            Assert.That(shop.TryApplyStamp(0, range.Id, out stampError), Is.True, stampError);
            Assert.That(forms[hammer.FormIndex].id, Is.EqualTo("shield"), "a skill stamp keeps the form");
            Assert.That(hammer.SpecialIndex, Is.EqualTo(1));
            Assert.That(service.Economy.Gold, Is.EqualTo(gold), "first stamps are free");
            Assert.That(shop.TryApplyStamp(0, greatsword.Id, out stampError), Is.True, stampError);
            Assert.That(forms[hammer.FormIndex].id, Is.EqualTo("greatsword"));
            Assert.That(hammer.SpecialIndex, Is.EqualTo(1), "a weapon stamp keeps the skill stamp");
            Assert.That(shop.TryApplyStamp(0, followUp.Id, out stampError), Is.True, stampError);
            Assert.That(hammer.SpecialIndex, Is.EqualTo(3));
            Assert.That(service.Economy.Gold, Is.EqualTo(gold - 40), "each replacement costs 20G");
            Assert.That(stash.EquippedWeaponStamp(0), Is.SameAs(greatsword));
            Assert.That(stash.EquippedSkillStamp(0), Is.SameAs(followUp));
            Assert.That(stash.Stamps, Is.EquivalentTo(new[] { whip }), "applied and replaced stamps leave the storage");
            var again = stash.AddWeaponStamp("blunt", "greatsword");
            Assert.That(shop.TryApplyStamp(0, again.Id, out stampError), Is.False);
            Assert.That(stampError, Is.EqualTo("이미 같은 스탬프가 적용되어 있습니다."));
            var back = stash.AddWeaponStamp("blunt", "shield");
            service.Economy.TrySpend(service.Economy.Gold - 19);
            Assert.That(shop.TryApplyStamp(0, back.Id, out stampError), Is.False);
            Assert.That(stampError, Is.EqualTo("골드가 부족합니다."));
            Assert.That(forms[hammer.FormIndex].id, Is.EqualTo("greatsword"), "a refused replacement changes nothing");
            service.Economy.Earn(1);
            Assert.That(shop.TryApplyStamp(0, back.Id, out stampError), Is.True, stampError);
            Assert.That(service.Economy.Gold, Is.Zero);
            Assert.That(stash.Stamps, Is.EquivalentTo(new[] { whip, again }), "refused stamps stay stored");
            Assert.That(shop.Growth.Equipped(shop.GrowthWeapon(0)), Is.SameAs(palette), "the palette stays bound to the stamped weapon");

            shop.Close();
            Assert.That(shop.IsOpen, Is.False);
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
            // The stamped weapon really fights as a shield with the follow-up special.
            using (var sim = world.arena.AcquireManualSimulation())
            {
                player.CancelAction();
                sim.Step(.02f, new Deviant.Combat.Contracts.CombatInput(0, Deviant.Combat.Contracts.CombatButtons.Weapon1Special));
                Assert.That(player.currentCast, Is.Not.Null);
                Assert.That(player.currentCast.Definition.Id, Is.EqualTo("shield_follow_up"));
                player.CancelAction();
            }
            service.Economy.Earn(1000);

            // Reopening the facility keeps the run's price count and the same stall with its sold offer.
            Assert.That(rooms.Length, Is.EqualTo(1));
            Assert.That(shop.TryOpen(), Is.True);
            Assert.That(shop.Service.Economy.PriceOf(PriceTracks.StatUpgrade), Is.EqualTo(90));
            Assert.That(shop.InkStock, Is.SameAs(stock));
            Assert.That(shop.InkStock[0].Sold, Is.True);
            shop.Close();
        }
    }
}
