using System;
using Deviant.Run;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    public sealed class RunEconomyTests
    {
        static EconomyData Data() => new EconomyData
        {
            rewards = new[]
            {
                new RewardTableData { role = "normal", gold = new RewardRange { min = 3, max = 5 }, parts = new RewardRange() },
                new RewardTableData { role = "elite", gold = new RewardRange { min = 100, max = 140 }, partsChance = 1, parts = new RewardRange { min = 2, max = 3 }, stampChance = .3f, stampCount = 1 },
                new RewardTableData { role = "boss", gold = new RewardRange { min = 300, max = 360 }, partsChance = 1, parts = new RewardRange { min = 3, max = 4 }, stampChance = 1, stampCount = 1 },
            },
            prices = new[]
            {
                new PriceTrackData { id = PriceTracks.StatUpgrade, start = 60, step = 30 },
                new PriceTrackData { id = PriceTracks.SlotUpgrade, start = 50, step = 25 },
            },
            weaponStampShare = .5f, stampSwapCost = 20,
            inkShop = new InkShopData
            {
                grades = new[] { 3, 4 }, offerCount = new RewardRange { min = 2, max = 3 }, saleGold = 8,
                gradePrices = new[] { new InkShopPriceData { grade = 3, gold = 420 }, new InkShopPriceData { grade = 4, gold = 600 } },
            },
        };

        [Test]
        public void RewardRangesIncludeBothEndsAndStayInsideThem()
        {
            var economy = new RunEconomy(Data()); var random = new Random(1);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 2000; i++)
            {
                var drop = RewardRoller.Roll(economy.Table("normal"), economy.WeaponStampShare, random);
                Assert.That(drop.Gold, Is.InRange(3, 5));
                Assert.AreEqual(0, drop.Parts + drop.WeaponStamps + drop.SkillStamps);
                sawMin |= drop.Gold == 3; sawMax |= drop.Gold == 5;
            }
            Assert.IsTrue(sawMin && sawMax);
        }

        [Test]
        public void BossAlwaysDropsStampPartsAndGold()
        {
            var economy = new RunEconomy(Data()); var random = new Random(2);
            int weapon = 0, skill = 0;
            for (int i = 0; i < 1000; i++)
            {
                var drop = RewardRoller.Roll(economy.Table("boss"), economy.WeaponStampShare, random);
                Assert.That(drop.Gold, Is.InRange(300, 360));
                Assert.That(drop.Parts, Is.InRange(3, 4));
                Assert.AreEqual(1, drop.WeaponStamps + drop.SkillStamps);
                weapon += drop.WeaponStamps; skill += drop.SkillStamps;
            }
            Assert.That(weapon, Is.InRange(400, 600));
            Assert.That(skill, Is.InRange(400, 600));
        }

        [Test]
        public void EliteChancesFollowTheTrialRates()
        {
            var economy = new RunEconomy(Data()); var random = new Random(3);
            int stamps = 0; const int rolls = 4000;
            for (int i = 0; i < rolls; i++)
            {
                var drop = RewardRoller.Roll(economy.Table("elite"), economy.WeaponStampShare, random);
                Assert.That(drop.Gold, Is.InRange(100, 140));
                Assert.That(drop.Parts, Is.InRange(2, 3));
                stamps += drop.WeaponStamps + drop.SkillStamps;
            }
            Assert.That(stamps / (double)rolls, Is.EqualTo(.3).Within(.04));
        }

        [Test]
        public void EachTrackRaisesItsOwnPriceOnlyAfterASuccessfulPurchase()
        {
            var economy = new RunEconomy(Data());
            Assert.IsFalse(economy.TryPurchase(PriceTracks.StatUpgrade));
            Assert.AreEqual(60, economy.PriceOf(PriceTracks.StatUpgrade));
            economy.Earn(1000);
            Assert.IsTrue(economy.TryPurchase(PriceTracks.StatUpgrade));
            Assert.IsTrue(economy.TryPurchase(PriceTracks.StatUpgrade));
            Assert.AreEqual(120, economy.PriceOf(PriceTracks.StatUpgrade));
            Assert.AreEqual(850, economy.Gold);
            Assert.AreEqual(50, economy.PriceOf(PriceTracks.SlotUpgrade));
            Assert.IsTrue(economy.TryPurchase(PriceTracks.SlotUpgrade));
            Assert.AreEqual(75, economy.PriceOf(PriceTracks.SlotUpgrade));
            Assert.AreEqual(120, economy.PriceOf(PriceTracks.StatUpgrade), "tracks count independently");
        }

        // The ink stall's fixed prices (user decision 2026-11-09): grade 3 = attack upgrades 1~4 in total, grade 4 = 1~5.
        [Test]
        public void InkStallPricesAreFixedPerGrade()
        {
            var economy = new RunEconomy(Data());
            Assert.AreEqual(60 + 90 + 120 + 150, economy.InkShopPrice(3));
            Assert.AreEqual(60 + 90 + 120 + 150 + 180, economy.InkShopPrice(4));
            Assert.Throws<ArgumentException>(() => economy.InkShopPrice(2));
            Assert.AreEqual(8, economy.InkSaleGold);
        }

        [Test]
        public void SequencesMatchTheTrialPriceTables()
        {
            var economy = new RunEconomy(Data()); economy.Earn(100000);
            int[] Sequence(string track) { var prices = new int[4]; for (int i = 0; i < 4; i++) { prices[i] = economy.PriceOf(track); economy.TryPurchase(track); } return prices; }
            CollectionAssert.AreEqual(new[] { 60, 90, 120, 150 }, Sequence(PriceTracks.StatUpgrade));
            CollectionAssert.AreEqual(new[] { 50, 75, 100, 125 }, Sequence(PriceTracks.SlotUpgrade));
        }

        [Test]
        public void StageStampLimitStopsDropsPerStageAndCountsStagesSeparately()
        {
            var data = Data(); data.stageStampLimit = 3;
            var economy = new RunEconomy(data);
            for (int i = 0; i < 3; i++) Assert.IsTrue(economy.TryTakeStageStamp(StampStages.Chapter1));
            Assert.IsFalse(economy.TryTakeStageStamp(StampStages.Chapter1), "the fourth stamp of a stage does not drop");
            Assert.AreEqual(3, economy.StageStampsDropped(StampStages.Chapter1));
            Assert.AreEqual(0, economy.StageStampsLeft(StampStages.Chapter1));
            Assert.AreEqual(3, economy.StageStampsLeft(StampStages.Chapter2), "chapter 2 starts from zero");
            Assert.IsTrue(economy.TryTakeStageStamp(StampStages.Chapter2));
            Assert.AreEqual(3, new RunEconomy(data).StageStampsLeft(StampStages.Chapter1), "a new run starts from zero");
            Assert.Throws<ArgumentException>(() => economy.TryTakeStageStamp(""));
        }

        [Test]
        public void TrialDataLimitsEachStageToThreeStamps()
        {
            var data = UnityEngine.JsonUtility.FromJson<EconomyData>(System.IO.File.ReadAllText("../data/combat/chapter1_economy.json"));
            Assert.AreEqual(3, data.stageStampLimit);
            Assert.AreEqual(3, new RunEconomy(data).StageStampsLeft(StampStages.Chapter1));
            foreach (var role in new[] { "elite", "boss" })
            {
                var table = Array.Find(data.rewards, t => t.role == role);
                Assert.AreEqual(1f, table.stampChance, role + " stamp is guaranteed");
                Assert.AreEqual(1, table.stampCount, role);
            }
            Assert.AreEqual(0, Array.Find(data.rewards, t => t.role == "normal").stampCount, "normal monsters drop no stamp");
        }

        [Test]
        public void SpendingRejectsShortfallWithoutChangingGold()
        {
            var economy = new RunEconomy(Data()); economy.Earn(15);
            Assert.IsFalse(economy.TrySpend(20));
            Assert.AreEqual(15, economy.Gold);
            Assert.IsTrue(economy.TrySpend(15));
            Assert.AreEqual(0, economy.Gold);
        }
    }
}
