using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    public sealed class SketchRunStateTests
    {
        static readonly Dictionary<string, string[]> Forms = new Dictionary<string, string[]>
        {
            ["base_sword"] = new[] { "base_sword_default", "base_sword_range" }, ["hammer"] = new[] { "hammer_default" },
        };
        static readonly string[] Inks = { "red_a", "blue_a", "green_a", "yellow_a", "purple_a", "purple_b" };
        // The per-stage block and guarantee checks run without the run-opening rewards; NewOpening uses the decided rules.
        static SketchRunState New(int seed = 1) => new SketchRunState(Forms, Inks, i => i.Split('_')[0], seed,
            new SketchRewardRules { OpeningRequirements = new float[0], OpeningOffers = false, DoubleOfferEvery = 0 });
        static SketchRunState NewOpening(int seed = 1) => new SketchRunState(Forms, Inks, i => i.Split('_')[0], seed);
        static int WeaponIndex(SketchRunState s) => System.Array.FindIndex(s.PendingOffer, c => !c.IsInk);
        static int InkIndex(SketchRunState s) => System.Array.FindIndex(s.PendingOffer, c => c.IsInk);

        [Test]
        public void OpeningRewardsNeedFifteenThenFortyEightThenTheStageBlocks()
        {
            var s = NewOpening();
            Assert.AreEqual(15f, s.Requirement);
            Kill(s, 3); // 3 x 5 = 15
            Assert.AreEqual(1, s.Banked, "the run's first reward needs 15");
            Assert.AreEqual(48f, s.Requirement);
            Kill(s, 9); // 45
            Assert.AreEqual(1, s.Banked);
            Kill(s, 1); // 50: the second reward and 2 over
            Assert.AreEqual(2, s.Banked, "the second reward needs 48 more");
            Assert.AreEqual(2f, s.Gauge, 1e-3, "overflow carries");
            Assert.AreEqual(100f, s.Requirement, "the stage blocks follow");
            for (int i = 0; i < 4; i++) s.RecordKill("elite", 700);
            Assert.AreEqual(150f, s.Requirement, "the stage's 7th reward needs 150 as before");
            s.StartStage(2);
            Assert.AreEqual(100f, s.Requirement, "the opening is once per run; stage 2 starts at 100");
        }

        [Test]
        public void AnEliteFillsTheOpeningRewardToo()
        {
            var s = NewOpening();
            s.RecordKill("elite", 700);
            Assert.AreEqual(1, s.Banked);
            Assert.AreEqual(48f, s.Requirement);
        }

        [Test]
        public void FirstAndSecondOffersEachGiveAnInkThenAWeaponChoice()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = NewOpening(seed);
                s.RecordKill("elite", 700); s.RecordKill("elite", 700);
                Assert.IsTrue(s.OpenBanked());
                Assert.AreEqual(3, s.PendingOffer.Length);
                Assert.IsTrue(s.PendingOffer.All(c => c.IsInk));
                Assert.IsTrue(s.PendingDoubleWeapons);
                string firstInk = s.PendingOffer[0].Ink;
                Assert.IsNull(s.Choose(0));
                Assert.AreEqual(10, s.Arsenal.RemainingInkUses(firstInk));
                Assert.AreEqual(1, s.Banked, "both choices consume only one banked reward");
                Assert.AreEqual(1, s.RunRewardsOpened);
                Assert.IsTrue(s.PendingOffer.All(c => !c.IsInk && c.Weapon.Slots == 4));
                Assert.AreEqual(3, s.PendingOffer.Select(c => c.Weapon.LayoutKey).Distinct().Count());
                Assert.IsNotNull(s.Choose(0));
                Assert.IsTrue(s.OpenBanked());
                Assert.IsTrue(s.PendingOffer.All(c => c.IsInk), "the second reward first offers three inks");
                Assert.IsTrue(s.PendingDoubleWeapons);
                s.Choose(1);
                Assert.IsNotNull(s.PendingOffer, "the weapon choice follows at once");
                Assert.IsTrue(s.PendingOffer.All(c => !c.IsInk && c.Weapon.Slots == 4));
                Assert.IsTrue(s.MustOpenBeforeExit);
                Assert.IsNotNull(s.Choose(2));
                Assert.IsNull(s.PendingOffer);
                Assert.AreEqual(2, s.WeaponsPicked, "both weapons count toward the normal guarantee");
                Assert.That(s.Arsenal.Codex.Count, Is.InRange(1, 2));
            }
        }

        [Test]
        public void FromTheThirdOffersMixAndEveryFourthFromTheSixthIsDouble()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var s = NewOpening(seed);
                for (int i = 0; i < 14; i++) s.RecordKill("elite", 700);
                for (int n = 1; n <= 14; n++)
                {
                    Assert.IsTrue(s.OpenBanked());
                    bool doubled = n == 1 || n == 2 || n == 6 || n == 10 || n == 14;
                    Assert.AreEqual(doubled, s.PendingDoubleWeapons, $"seed {seed} reward {n}");
                    if (doubled)
                    {
                        Assert.IsTrue(s.PendingOffer.All(c => c.IsInk), $"seed {seed} reward {n}: inks first");
                        s.Choose(0);
                        Assert.IsTrue(s.PendingOffer.All(c => !c.IsInk), $"seed {seed} reward {n}: then weapons");
                        s.Choose(0);
                        continue;
                    }
                    Assert.GreaterOrEqual(InkIndex(s), 0, $"seed {seed} reward {n}: an ink");
                    Assert.GreaterOrEqual(WeaponIndex(s), 0, $"seed {seed} reward {n}: a weapon");
                    s.Choose(WeaponIndex(s));
                }
            }
        }

        [Test]
        public void DoubleRewardFillsMissingInksWithWeapons()
        {
            var s = NewOpening(4);
            s.LockInks(Inks.Skip(1));
            s.RecordKill("elite", 700); s.RecordKill("elite", 700);
            s.OpenBanked(); s.Choose(0); s.Choose(0);
            s.OpenBanked();
            Assert.AreEqual(1, s.PendingOffer.Count(c => c.IsInk), "one ink is left");
            Assert.AreEqual(2, s.PendingOffer.Count(c => !c.IsInk));
            s.Choose(InkIndex(s));
            Assert.IsTrue(s.PendingOffer.All(c => !c.IsInk));
        }

        [Test]
        public void DoubleRewardCanOfferEveryAcquiredKindAgainAndDebugGrantPreservesTheOpeningGauge()
        {
            var s = NewOpening();
            foreach (var ink in Inks) s.Arsenal.RegisterInk(ink);
            float requirement = s.Requirement;
            s.DebugAddReward(); s.OpenBanked(); s.Choose(0); s.Choose(0);
            s.DebugAddReward(); s.OpenBanked();
            Assert.AreEqual(requirement, s.Requirement, "debug grants do not fill the combat gauge");
            Assert.IsTrue(s.PendingDoubleWeapons); Assert.IsTrue(s.PendingOffer.All(c => c.IsInk));
            Assert.AreEqual(3, s.PendingOffer.Select(c => c.Ink).Distinct().Count());
            var picked = s.PendingOffer[0].Ink; int before = s.Arsenal.RemainingInkUses(picked); s.Choose(0);
            Assert.AreEqual(before + 10, s.Arsenal.RemainingInkUses(picked));
            Assert.IsTrue(s.PendingOffer.All(c => !c.IsInk)); s.Choose(0);
            Assert.IsNull(s.PendingOffer); Assert.AreEqual(2, s.RunRewardsOpened);
        }

        [Test]
        public void FirstRewardAndItsRerollsOnlyOfferUnlockedStandaloneInks()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = NewOpening(seed);
                s.IsStandaloneInk = id => id.StartsWith("green_") || id.StartsWith("purple_");
                s.LockInks(new[] { "purple_b" });
                s.DebugAddReward(); s.OpenBanked();
                for (int reroll = 0; reroll < 4; reroll++)
                {
                    var inks = s.PendingOffer.Where(c => c.IsInk).Select(c => c.Ink).ToArray();
                    CollectionAssert.AreEquivalent(new[] { "green_a", "purple_a" }, inks);
                    Assert.AreEqual(1, s.PendingOffer.Count(c => !c.IsInk), "missing ink uses the existing weapon fallback");
                    s.RerollPending();
                }
                s.Choose(InkIndex(s));
                s.RerollPending();
                Assert.IsTrue(s.PendingOffer.All(c => !c.IsInk), "the second half stays blueprints on reroll");
                s.Choose(0);
                Assert.IsNull(s.PendingOffer);
                Assert.AreEqual(1, s.WeaponsPicked);
            }
        }

        // Stage-1 medium monsters (60 HP) add 5: twenty fill one 100 gauge.
        static void Kill(SketchRunState s, int n, float hp = 60) { for (int i = 0; i < n; i++) s.RecordKill("normal", hp); }
        static void TakeOffer(SketchRunState s, bool preferInk)
        {
            Assert.IsTrue(s.OpenBanked());
            int pick = System.Array.FindIndex(s.PendingOffer, c => c.IsInk == preferInk);
            s.Choose(pick < 0 ? 0 : pick);
        }

        [Test]
        public void DebugRewardOnlyAddsBankedChoicesAndLeavesGaugeAndPendingOfferUntouched()
        {
            var s = New();
            for (int i = 0; i < 6; i++) s.RecordKill("elite", 700);
            Kill(s, 3); s.OpenBanked();
            var offer = s.PendingOffer; float gauge = s.Gauge, requirement = s.Requirement;
            int filled = s.RewardsFilled, opened = s.OffersOpened, runOpened = s.RunRewardsOpened, weapons = s.WeaponsPicked;
            int banked = s.Banked, revision = s.Revision, changes = 0;
            s.Changed += () => changes++;
            s.DebugAddReward(); s.DebugAddReward();
            Assert.AreEqual(banked + 2, s.Banked); Assert.AreEqual(2, changes); Assert.Greater(s.Revision, revision);
            Assert.AreSame(offer, s.PendingOffer); Assert.AreEqual(gauge, s.Gauge); Assert.AreEqual(requirement, s.Requirement);
            Assert.AreEqual(filled, s.RewardsFilled); Assert.AreEqual(opened, s.OffersOpened);
            Assert.AreEqual(runOpened, s.RunRewardsOpened); Assert.AreEqual(weapons, s.WeaponsPicked);
            s.Choose(System.Array.FindIndex(s.PendingOffer, c => !c.IsInk));
            Assert.IsTrue(s.OpenBanked()); Assert.AreEqual(opened + 1, s.OffersOpened);
            Assert.AreEqual(runOpened + 1, s.RunRewardsOpened); Assert.IsTrue(s.MustOpenBeforeExit);
        }

        [Test]
        public void DebugRewardCanBeOpenedNormallyAndDoesNotCarryToAnotherStageOrNewRun()
        {
            var s = New(); s.DebugAddReward();
            Assert.IsNull(s.PendingOffer); Assert.AreEqual(0, s.RewardsFilled);
            Assert.IsTrue(s.OpenBanked()); Assert.AreEqual(0, s.Banked);
            Assert.IsTrue(s.PendingOffer.Any(c => c.IsInk) && s.PendingOffer.Any(c => !c.IsInk));
            s.Choose(0); s.DebugAddReward(); s.StartStage(2);
            Assert.AreEqual(0, s.Banked); Assert.AreEqual(0, New().Banked);
        }

        [Test]
        public void GaugeFillsByMonsterSizeAndBossAddsNothing()
        {
            var s = New();
            s.RecordKill("normal", 40); s.RecordKill("normal", 60); s.RecordKill("normal", 140); s.RecordKill("boss", 3500);
            Assert.AreEqual(4 + 5 + 8, s.Gauge, 1e-4);
            Assert.AreEqual(0.17f, s.GaugeFraction, 1e-4);
        }

        [Test]
        public void StageTwoGainsHalfAgainButNeedsTheSame()
        {
            var s = New(); s.StartStage(2);
            s.RecordKill("normal", 60);
            Assert.AreEqual(7.5f, s.Gauge, 1e-4);
            Assert.AreEqual(100f, s.Requirement);
        }

        [Test]
        public void FilledRewardIsBankedNotOpened()
        {
            var s = New();
            Kill(s, 19);
            Assert.AreEqual(0, s.Banked);
            Kill(s, 1);
            Assert.AreEqual(1, s.Banked);
            Assert.IsNull(s.PendingOffer, "a filled reward waits until the player opens it");
            Assert.IsTrue(s.OpenBanked());
            Assert.AreEqual(0, s.Banked);
            Assert.IsTrue(s.PendingOffer.Any(c => c.IsInk), "the first offer of a stage has an ink");
            Assert.IsTrue(s.PendingOffer.Where(c => !c.IsInk).All(c => c.Weapon.Slots == 4));
            Assert.IsFalse(s.OpenBanked(), "one at a time");
        }

        [Test]
        public void EliteBanksAWholeRewardAndOverflowCarries()
        {
            var s = New();
            Kill(s, 18, 140); // 144: one reward and 44 over
            Assert.AreEqual(1, s.Banked);
            Assert.AreEqual(44f, s.Gauge, 1e-3);
            s.RecordKill("elite", 700);
            Assert.AreEqual(2, s.Banked);
            Assert.AreEqual(44f, s.Gauge, 1e-3, "an elite does not touch the bar");
        }

        [Test]
        public void RequirementGrowsEverySixRewardsWithinAStage()
        {
            var s = New();
            for (int i = 0; i < 6; i++) s.RecordKill("elite", 700);
            Assert.AreEqual(150f, s.Requirement);
            Kill(s, 20);
            Assert.AreEqual(6, s.Banked, "100 is no longer enough after six rewards");
            Kill(s, 10);
            Assert.AreEqual(7, s.Banked);
        }

        [Test]
        public void SeventhOfferUsesTheStageMaximum()
        {
            var s = New();
            for (int i = 0; i < 7; i++) s.RecordKill("elite", 700);
            for (int i = 0; i < 6; i++) TakeOffer(s, preferInk: false);
            Assert.IsTrue(s.OpenBanked());
            Assert.IsTrue(s.PendingOffer.Where(c => !c.IsInk).All(c => c.Weapon.Slots == 4));
        }

        [Test]
        public void ChoosingAnInkAddsTenUsesAndKeepsItInSupply()
        {
            var s = New();
            Kill(s, 20); s.OpenBanked();
            var ink = s.PendingOffer.First(c => c.IsInk).Ink;
            s.Choose(System.Array.FindIndex(s.PendingOffer, c => c.IsInk));
            Assert.IsTrue(s.Arsenal.Registered(ink));
            CollectionAssert.Contains(s.SupplyInks.ToArray(), ink);
            Assert.AreEqual(10, s.Arsenal.RemainingInkUses(ink));
        }

        [Test]
        public void PreviouslyAcquiredKindRemainsInNormalRewardsAndDuplicateShopSlotsSpendIndependently()
        {
            var s = new SketchRunState(Forms, new[] { "red_a" }, i => "red", 7,
                new SketchRewardRules { OpeningRequirements = new float[0], OpeningOffers = false, DoubleOfferEvery = 0 });
            s.Arsenal.RegisterInk("red_a"); s.RecordKill("elite", 700); s.OpenBanked();
            Assert.AreEqual(1, s.PendingOffer.Count(c => c.IsInk));
            s.Choose(System.Array.FindIndex(s.PendingOffer, c => c.IsInk));
            Assert.AreEqual(20, s.Arsenal.RemainingInkUses("red_a"));
            var stock = s.ShopStock("c1"); var economy = Economy(1000);
            Assert.AreEqual(3, stock.Count(c => c.Ink == "red_a"));
            s.Buy("c1", 3, economy); s.Buy("c1", 4, economy);
            Assert.AreEqual(40, s.Arsenal.RemainingInkUses("red_a"));
            Assert.AreEqual(560, economy.Gold);
            Assert.IsTrue(stock[3].Sold && stock[4].Sold); Assert.IsFalse(stock[5].Sold);
            s.StartStage(2);
            Assert.AreEqual(40, s.Arsenal.RemainingInkUses("red_a"));
        }

        [Test]
        public void SixOpenedOffersGuaranteeThreeWeapons()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = New(seed);
                for (int i = 0; i < 6; i++) s.RecordKill("elite", 700);
                for (int i = 0; i < 6; i++) TakeOffer(s, preferInk: true);
                Assert.GreaterOrEqual(s.WeaponsPicked, 3, $"seed {seed}");
            }
        }

        [Test]
        public void LockedChestOffersAnInkAndABlueprintOutsideTheGuarantee()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = New(seed);
                Assert.IsTrue(s.OpenChestOffer());
                Assert.IsTrue(s.PendingIsEvent);
                Assert.AreEqual(3, s.PendingOffer.Length);
                Assert.IsTrue(s.PendingOffer.Any(c => c.IsInk), $"seed {seed}: an unregistered ink");
                Assert.IsTrue(s.PendingOffer.Any(c => !c.IsInk), $"seed {seed}: a blueprint");
                Assert.IsTrue(s.PendingOffer.Where(c => !c.IsInk).All(c => c.Weapon.Slots == 4), "the stage's largest slot count");
                Assert.AreEqual(0, s.Banked, "an event reward is never banked");
                int weapon = System.Array.FindIndex(s.PendingOffer, c => !c.IsInk);
                Assert.IsNotNull(s.Choose(weapon));
                Assert.AreEqual(0, s.WeaponsPicked, "chest weapons are outside the normal guarantee");
                Assert.IsFalse(s.PendingIsEvent);
            }
        }

        [Test]
        public void LockedChestQueuesBehindAnOpenOfferAndCanOfferAcquiredInks()
        {
            var s = New(3);
            foreach (var registered in Inks) s.Arsenal.RegisterInk(registered);
            Kill(s, 20); s.OpenBanked();
            Assert.IsTrue(s.OpenChestOffer());
            Assert.IsFalse(s.PendingIsEvent, "the open normal offer comes first");
            Assert.IsFalse(s.OpenChestOffer(), "one chest choice waits at a time");
            int ink = System.Array.FindIndex(s.PendingOffer, c => c.IsInk);
            s.Choose(ink >= 0 ? ink : 0);
            Assert.IsTrue(s.PendingIsEvent);
            Assert.IsTrue(s.PendingOffer.Any(c => c.IsInk), "acquired kinds remain eligible");
            var pick = s.PendingOffer.First(c => c.IsInk).Ink;
            int before = s.Arsenal.RemainingInkUses(pick);
            s.Choose(System.Array.FindIndex(s.PendingOffer, c => c.IsInk));
            Assert.AreEqual(before + 10, s.Arsenal.RemainingInkUses(pick));
        }

        [Test]
        public void BossOfferIsThreeStageMaximumWeaponsAndQueuesBehindAnOpenOffer()
        {
            var s = New();
            Kill(s, 20); s.OpenBanked();
            Assert.IsTrue(s.OpenBossOffer());
            Assert.IsFalse(s.PendingIsBoss, "the open normal offer comes first");
            s.Choose(0);
            Assert.IsTrue(s.PendingIsBoss);
            Assert.IsTrue(s.PendingOffer.All(c => !c.IsInk && c.Weapon.Slots == 4));
            int before = s.WeaponsPicked;
            s.Choose(1);
            Assert.AreEqual(before, s.WeaponsPicked, "boss weapons are outside the guarantee");
            Assert.IsFalse(s.OpenBossOffer());
        }

        [Test]
        public void ExitNeedsEveryBankedRewardChosen()
        {
            var s = New();
            Kill(s, 40);
            Assert.IsTrue(s.MustOpenBeforeExit);
            TakeOffer(s, false); TakeOffer(s, false);
            Assert.IsFalse(s.MustOpenBeforeExit);
        }

        static RunEconomy Economy(int gold)
        {
            var e = new RunEconomy(new EconomyData
            {
                rewards = new[] { new RewardTableData { role = "normal", gold = new RewardRange(), parts = new RewardRange() } },
                prices = new[] { new PriceTrackData { id = PriceTracks.StatUpgrade, start = 60, step = 30 }, new PriceTrackData { id = PriceTracks.SlotUpgrade, start = 50, step = 25 } },
                inkShop = new InkShopData { grades = new[] { 3 }, offerCount = new RewardRange { min = 1, max = 1 }, gradePrices = new[] { new InkShopPriceData { grade = 3, gold = 1 } } },
            });
            e.Earn(gold); return e;
        }

        [Test]
        public void ShopShelfIsThreeWeaponsAndThreeInksKeptAcrossVisits()
        {
            var s = New();
            var first = s.ShopStock("c1");
            Assert.AreEqual(3, first.Count(o => o.Weapon != null));
            Assert.AreEqual(3, first.Count(o => o.Ink != null));
            foreach (var o in first.Where(o => o.Weapon != null))
            {
                Assert.That(o.Weapon.Slots, Is.EqualTo(4));
                Assert.AreEqual(200 + 100 * o.Weapon.Slots + 50 * o.Weapon.EnhancedSlots, o.Price);
            }
            var again = s.ShopStock("c1");
            CollectionAssert.AreEqual(first.Select(o => o.Weapon?.LayoutKey ?? o.Ink).ToArray(), again.Select(o => o.Weapon?.LayoutKey ?? o.Ink).ToArray());
        }

        [Test]
        public void BuyingPaysGoldAndLeavesThePlaceSoldOut()
        {
            var s = New(); var economy = Economy(1000);
            var stock = s.ShopStock("c1");
            int weaponIndex = stock.ToList().FindIndex(o => o.Weapon != null);
            int price = stock[weaponIndex].Price;
            var weapon = s.Buy("c1", weaponIndex, economy);
            Assert.AreEqual(SketchWeaponState.Unconfirmed, s.Arsenal.StateOf(weapon));
            Assert.AreEqual(1000 - price, economy.Gold);
            Assert.IsTrue(s.ShopStock("c1")[weaponIndex].Sold);
            Assert.Throws<System.InvalidOperationException>(() => s.Buy("c1", weaponIndex, economy));
            Assert.AreEqual(0, s.WeaponsPicked, "shop weapons are outside the normal-offer guarantee");
        }

        [Test]
        public void NotEnoughGoldRefusesWithoutTakingAnything()
        {
            var s = New(); var economy = Economy(50);
            Assert.Throws<System.InvalidOperationException>(() => s.Buy("c1", 0, economy));
            Assert.AreEqual(50, economy.Gold);
            Assert.IsFalse(s.ShopStock("c1")[0].Sold);
        }

        [Test]
        public void ShelfInkAcquiredElsewhereStaysInPlaceAndCanBeBoughtAgain()
        {
            var s = New();
            var stock = s.ShopStock("c1");
            int inkIndex = stock.ToList().FindIndex(o => o.Ink != null);
            string ink = stock[inkIndex].Ink;
            s.Arsenal.RegisterInk(ink);
            var refreshed = s.ShopStock("c1");
            Assert.AreEqual(ink, refreshed[inkIndex].Ink);
            Assert.IsFalse(refreshed[inkIndex].Sold);
            s.Buy("c1", inkIndex, Economy(1000));
            Assert.AreEqual(20, s.Arsenal.RemainingInkUses(ink));
            Assert.IsTrue(s.ShopStock("c1")[inkIndex].Sold);
        }

        [Test]
        public void NextStageKeepsArsenalCarriesTheGaugeAndUsesLargerWeapons()
        {
            var s = New();
            Kill(s, 20); TakeOffer(s, preferInk: false);
            for (int i = 0; i < 6; i++) s.RecordKill("elite", 700);
            Kill(s, 3); // 15 left on the bar
            s.StartStage(2);
            Assert.AreEqual(1, s.Arsenal.Weapons.Count);
            Assert.AreEqual(0, s.Banked, "unopened rewards do not move to the next stage");
            Assert.AreEqual(15f, s.Gauge, 1e-3, "the unfilled gauge carries over");
            Assert.AreEqual(100f, s.Requirement, "the requirement restarts at 100");
            Kill(s, 12); // 15 + 12 x 7.5 = 105
            Assert.AreEqual(1, s.Banked);
            s.OpenBanked();
            Assert.IsTrue(s.PendingOffer.Where(c => !c.IsInk).All(c => c.Weapon.Slots == 5));
        }
    }
}
