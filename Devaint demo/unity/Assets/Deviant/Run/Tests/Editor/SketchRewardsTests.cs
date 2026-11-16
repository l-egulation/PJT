using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    // Reward rules of docs/design/run/rewards.md after the 2026-11-12 slot redesign.
    public sealed class SketchRewardsTests
    {
        static readonly Dictionary<string, string[]> Forms = new Dictionary<string, string[]>
        {
            ["sword"] = new[] { "slash", "pierce" }, ["hammer"] = new[] { "quake" }, ["bow"] = new[] { "volley", "snipe" },
        };
        static readonly string[] Inks = { "red_a", "red_b", "blue_a", "green_a", "yellow_a", "purple_a" };
        static SketchRewardPlanner Planner() => new SketchRewardPlanner(Forms);

        [TestCase(1, new[] { 4, 4, 4, 4, 4, 4 })]
        [TestCase(2, new[] { 5, 5, 5, 5, 5, 5 })]
        [TestCase(3, new[] { 6, 6, 6, 6, 6, 6 })]
        [TestCase(4, new[] { 6, 6, 6, 6, 6, 6 })]
        public void WeaponSlotCountFollowsTheOffer(int stage, int[] expected)
        {
            var rules = new SketchRewardRules();
            CollectionAssert.AreEqual(expected, Enumerable.Range(1, 6).Select(o => rules.SlotsForOffer(stage, o)).ToArray());
        }

        [TestCase(1, 7, 4)]
        [TestCase(1, 12, 4)]
        [TestCase(2, 9, 5)]
        public void OffersAfterTheSixthUseTheStageMaximum(int stage, int offer, int slots)
        {
            Assert.AreEqual(slots, new SketchRewardRules().SlotsForOffer(stage, offer));
        }

        [TestCase(0, 100f)]
        [TestCase(5, 100f)]
        [TestCase(6, 150f)]
        [TestCase(12, 200f)]
        public void GaugeRequirementGrowsBySixRewardBlocks(int filled, float requirement)
        {
            Assert.AreEqual(requirement, new SketchRewardRules().GaugeRequirement(filled));
        }

        [TestCase(1, 40f, 4f)]
        [TestCase(1, 60f, 5f)]
        [TestCase(1, 140f, 8f)]
        [TestCase(2, 60f, 7.5f)]
        [TestCase(3, 140f, 16f)]
        public void GaugeGainFollowsSizeTierAndStage(int stage, float hp, float gain)
        {
            Assert.AreEqual(gain, new SketchRewardRules().GaugeGain(stage, hp), 1e-4);
        }

        [TestCase(1, 44, 4)]
        [TestCase(1, 46, 5)]
        [TestCase(1, 78, 8)]
        [TestCase(2, 54, 7.5f)]
        [TestCase(2, 90, 12)]
        public void RebalancedSpeciesKeepTheirPreviousRewardContributions(int stage, float hp, float gain)
        { Assert.That(new SketchRewardRules().GaugeGain(stage, hp), Is.EqualTo(gain)); }

        [Test]
        public void EveryWeaponOfAnOfferHasThatOffersSlotCount()
        {
            var planner = Planner();
            for (int seed = 0; seed < 200; seed++)
                for (int offer = 1; offer <= 6; offer++)
                    foreach (var c in planner.NormalOffer(1, offer, 0, Inks, new Random(seed)).Where(c => !c.IsInk))
                        Assert.AreEqual(planner.Rules.SlotsForOffer(1, offer), c.Weapon.Slots);
        }

        [Test]
        public void FirstOfferAlwaysHasAnInkAndAWeapon()
        {
            var planner = Planner();
            for (int seed = 0; seed < 500; seed++)
                foreach (int stage in new[] { 1, 2 })
                {
                    var offer = planner.NormalOffer(stage, 1, 0, Inks, new Random(seed));
                    Assert.AreEqual(3, offer.Length);
                    Assert.IsTrue(offer.Any(c => c.IsInk), $"seed {seed}");
                    Assert.IsTrue(offer.Any(c => !c.IsInk), $"seed {seed}");
                }
        }

        [Test]
        public void FirstOfferFallsBackToWeaponsWhenNoInkIsLeft()
        {
            var offer = Planner().NormalOffer(1, 1, 0, Array.Empty<string>(), new Random(3));
            Assert.IsTrue(offer.All(c => !c.IsInk));
            Assert.AreEqual(3, offer.Select(c => c.Weapon.LayoutKey).Distinct().Count());
        }

        [Test]
        public void MissingWeaponsForceThreeWeaponOffers()
        {
            // offer 4 with no weapon picked: 3 offers remain and 3 weapons are missing
            for (int seed = 0; seed < 100; seed++)
                Assert.IsTrue(Planner().NormalOffer(1, 4, 0, Inks, new Random(seed)).All(c => !c.IsInk));
        }

        [Test]
        public void AfterThreeWeaponsBothTypesAppear()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var offer = Planner().NormalOffer(2, 5, 3, Inks, new Random(seed));
                Assert.IsTrue(offer.Any(c => c.IsInk) && offer.Any(c => !c.IsInk), $"seed {seed}");
            }
        }

        [Test]
        public void InksOnOneRewardScreenAreDistinct()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var inks = Planner().NormalOffer(1, 2, 1, new[] { "red_a", "blue_a" }, new Random(seed)).Where(c => c.IsInk).Select(c => c.Ink).ToArray();
                Assert.AreEqual(inks.Length, inks.Distinct().Count());
                Assert.IsTrue(inks.All(i => i == "red_a" || i == "blue_a"));
            }
        }

        [Test]
        public void EnhancedSlotsRespectCapAndShareOneColour()
        {
            var planner = Planner(); int enhancedSeen = 0;
            for (int seed = 0; seed < 2000; seed++)
            {
                var w = planner.RollWeapon(2, 5, new Random(seed));
                var colors = w.Enhance.Where(e => e != null).ToArray();
                Assert.LessOrEqual(colors.Length, 2);
                Assert.LessOrEqual(colors.Distinct().Count(), 1);
                enhancedSeen += colors.Length;
            }
            // 25% per slot, capped at 2 of 5 slots: the mean is a little below 1.25 per weapon
            Assert.That(enhancedSeen / 2000.0, Is.InRange(0.95, 1.25));
        }

        [Test]
        public void OneSlotWeaponCanBeLucky()
        {
            var planner = Planner();
            Assert.IsTrue(Enumerable.Range(0, 200).Any(seed => planner.RollWeapon(1, 1, new Random(seed)).EnhancedSlots == 1));
        }

        [Test]
        public void BossOfferUsesTheStageMaximum()
        {
            Assert.IsTrue(Planner().BossOffer(1, new Random(1)).All(w => w.Slots == 4));
            Assert.IsTrue(Planner().BossOffer(2, new Random(1)).All(w => w.Slots == 5));
        }

        [Test]
        public void ShopWeaponsStayInTheStageRangeAndPricesScaleWithSlots()
        {
            var planner = Planner();
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                var shelf = planner.ShopWeapons(1, new Random(seed));
                Assert.AreEqual(3, shelf.Length);
                Assert.AreEqual(3, shelf.Select(w => w.LayoutKey).Distinct().Count());
                foreach (var w in shelf) seen.Add(w.Slots);
            }
            CollectionAssert.AreEquivalent(new[] { 4 }, seen);
            Assert.AreEqual(300, planner.WeaponPrice(1, new SketchWeaponDraft("sword", "slash", new string[] { null })));
            Assert.AreEqual(550, planner.WeaponPrice(1, new SketchWeaponDraft("sword", "slash", new[] { "red", null, null })));
            Assert.AreEqual(1315, planner.WeaponPrice(2, new SketchWeaponDraft("sword", "slash", new[] { "red", null, null, null, null })));
            Assert.AreEqual(220, planner.InkPrice(1));
        }

        [Test]
        public void ShopDrawsWithReplacementEvenWhenOnlyOneKindExists()
        {
            CollectionAssert.AreEqual(new[] { "red_a", "red_a", "red_a" }, Planner().ShopInks(new[] { "red_a" }, new Random(1)));
            Assert.IsEmpty(Planner().ShopInks(Array.Empty<string>(), new Random(1)));
        }

        [Test]
        public void UndecidedStageThreeValuesRefuseInsteadOfGuessing()
        {
            Assert.Throws<InvalidOperationException>(() => Planner().RollWeapon(3, 6, new Random(1)));
            Assert.Throws<InvalidOperationException>(() => Planner().InkPrice(3));
        }

        [Test]
        public void DraftBecomesAnUnconfirmedWeapon()
        {
            var arsenal = new SketchArsenal(ink => ink.Split('_')[0]);
            var draft = Planner().BossOffer(1, new Random(7))[0];
            var weapon = arsenal.Acquire(draft);
            Assert.AreEqual(SketchWeaponState.Unconfirmed, arsenal.StateOf(weapon));
            Assert.AreEqual(draft.LayoutKey, weapon.LayoutKey);
            Assert.AreEqual(8000f, weapon.MaxDurability);
        }
    }
}
