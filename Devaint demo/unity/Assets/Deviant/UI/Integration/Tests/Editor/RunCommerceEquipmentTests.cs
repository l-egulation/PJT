using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Run;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.UI.Integration.Tests
{
    public sealed class RunCommerceEquipmentTests
    {
        sealed class Host : IRunCommerceHost
        {
            public void SetCommercePaused(bool paused) { }
            public void OpenBag(Guid? weaponId, string inkId) { }
        }
        static SketchEquipment Equipment()
        {
            // Use the generated game catalog, including its stage rules, IDs and prices. The isolated harness
            // must copy this file to the same Assets path; missing source data is a setup failure, never a skip.
            string path = Path.Combine(Application.dataPath, "Deviant/Combat/Resources/RunInks.json");
            var catalog = JsonUtility.FromJson<RunInkCatalog>(File.ReadAllText(path));
            var economy = new RunEconomy(new EconomyData()); economy.Earn(10000);
            return new SketchEquipment(catalog, economy, 71,
                new Dictionary<string, int[]> { ["base_sword"] = new[] { 0, 1 }, ["hammer"] = new[] { 0 } },
                new Dictionary<string, string[]> { ["base_sword"] = new[] { "base_sword_default", "base_sword_range" },
                    ["hammer"] = new[] { "hammer_default" } });
        }
        [Test] public void RewardUsesEquipmentAcquisitionAndAdoptsExistingPreview()
        {
            var equipment = Equipment(); equipment.State.RecordKill("elite", 100); equipment.OpenReward();
            var a = new RunCommerceAdapter(equipment, new Host());
            var card = a.Snapshot().Rewards.First(c => !c.Item.IsInk);
            var preview = equipment.RewardChoices()[card.Slot].Weapon;
            Assert.AreEqual(CommerceResult.Applied, a.Choose(card.Key));
            var receipt = a.Snapshot().Receipt;
            Assert.AreSame(preview, equipment.Weapons.Single(w => w.Id == receipt.WeaponId));
            Assert.AreSame(equipment.State.Arsenal.Weapons.Single(), preview.Model);
            Assert.IsFalse(preview.Confirmed);
        }
        [Test] public void PurchaseUsesEquipmentEconomyAndAdoptsExistingShopPreview()
        {
            var equipment = Equipment(); var a = new RunCommerceAdapter(equipment, new Host());
            var card = a.Snapshot("forge").Shop[0]; var preview = equipment.Shop("forge")[0].Weapon;
            Assert.AreEqual(CommerceResult.Applied, a.Buy("forge", card.Key));
            Assert.AreEqual(10000 - card.Price, equipment.Economy.Gold);
            Assert.AreSame(preview, equipment.Weapons.Single(w => w.Id == a.Snapshot().Receipt.WeaponId));
            Assert.IsTrue(equipment.Shop("forge")[0].Sold);
        }
    }
}
