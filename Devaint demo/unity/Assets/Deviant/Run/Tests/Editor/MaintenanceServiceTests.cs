using Deviant.Run;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    public sealed class MaintenanceServiceTests
    {
        static MaintenanceService Service(int gold)
        {
            var economy = new RunEconomy(new EconomyData
            {
                rewards = new RewardTableData[0],
                prices = new[]
                {
                    new PriceTrackData { id = PriceTracks.StatUpgrade, start = 60, step = 30 },
                    new PriceTrackData { id = PriceTracks.SlotUpgrade, start = 50, step = 25 },
                },
                stampSwapCost = 20,
            });
            economy.Earn(gold);
            return new MaintenanceService(economy, new RunStash());
        }

        [Test]
        public void UpgradesGrantOnlyAfterPaying()
        {
            var shop = Service(100);
            Assert.IsTrue(shop.TryUpgradeSlot(out _));
            Assert.AreEqual(1, shop.Stash.SlotLevel);
            Assert.AreEqual(50, shop.Economy.Gold);
            Assert.IsFalse(shop.TryUpgradeStat(out var error));
            Assert.AreEqual("골드가 부족합니다.", error);
            Assert.AreEqual(0, shop.Stash.StatLevel);
            Assert.AreEqual(60, shop.Economy.PriceOf(PriceTracks.StatUpgrade), "a failed purchase does not raise the price");
            Assert.AreEqual(75, shop.Economy.PriceOf(PriceTracks.SlotUpgrade));
            Assert.AreEqual(50, shop.Economy.Gold);
        }
    }
}
