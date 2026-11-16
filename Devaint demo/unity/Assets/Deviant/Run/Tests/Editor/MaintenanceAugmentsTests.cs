using System;
using System.Linq;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    public sealed class MaintenanceAugmentsTests
    {
        static AugmentDefinition Card(string id, string group, AugmentEffect effect) => new AugmentDefinition { id = id, group = group, name = id, gain = "", cost = "", play = "", note = "", effect = effect };

        static AugmentCatalog Catalog() => new AugmentCatalog
        {
            schema = 1, offeredCards = 3, stableCards = 1, riskCards = 2,
            minimumDamageScale = .1f, minimumIncomingScale = .1f, minimumMaxHealthScale = .1f, minimumWalkSpeedScale = .2f, minimumBasicSpeedScale = .2f,
            maximumSkillCooldownReduction = .8f, maximumCritChance = 1,
            groups = new[] { "safe", "combat", "slow", "special" }.Select(g => new AugmentGroup { id = g, name = g }).ToArray(),
            augments = new[]
            {
                Card("S01", "safe", new AugmentEffect { damage = .2f }),
                Card("S02", "safe", new AugmentEffect { damage = .12f, maxHealth = .2f }),
                Card("S09", "safe", new AugmentEffect { damage = .12f, healing = .3f }),
                Card("C01", "combat", new AugmentEffect { damage = .35f, incoming = .25f }),
                Card("C13", "combat", new AugmentEffect { maxHealth = 1, healing = -.5f }),
                Card("C14", "combat", new AugmentEffect { growthStartRatio = .3f, growthKills = 10, growthStepRatio = .1f, growthFinalRatio = 1.5f }),
                Card("M01", "slow", new AugmentEffect { damage = .4f, walkSpeed = -.2f }),
                Card("M02", "slow", new AugmentEffect { incoming = -.45f, walkSpeed = -.25f }),
                Card("X09", "special", new AugmentEffect { skillDamage = .7f, bookmarkSeconds = 3 }),
            },
        };

        [Test]
        public void RewardOffersOneStableAndTwoRiskCardsOfDifferentGroupsNeverOwned()
        {
            for (int i = 0; i < 200; i++)
            {
                var augments = new MaintenanceAugments(Catalog(), i);
                augments.Grant("S01"); augments.Grant("C01");
                var offer = augments.DrawReward();
                Assert.AreEqual(3, offer.Length);
                Assert.IsTrue(offer[0].Stable);
                Assert.IsFalse(offer[1].Stable || offer[2].Stable);
                Assert.AreNotEqual(offer[1].group, offer[2].group);
                Assert.IsFalse(offer.Any(c => c.id == "S01" || c.id == "C01"), "owned cards are not offered again");
            }
        }

        [Test]
        public void GrantingACardOwnsItOnce()
        {
            var augments = new MaintenanceAugments(Catalog(), 3);
            var chosen = augments.DrawReward()[1];
            int revision = augments.Revision;
            Assert.IsTrue(augments.Grant(chosen.id));
            Assert.Greater(augments.Revision, revision);
            Assert.IsTrue(augments.Has(chosen.id));
            Assert.IsFalse(augments.Grant(chosen.id), "the same card is owned at most once");
            Assert.IsFalse(augments.Grant("missing"));
        }

        [Test]
        public void RewardShowsTheRemainingCardsWhenAFullOfferCannotForm()
        {
            var augments = new MaintenanceAugments(Catalog(), 1);
            foreach (var id in new[] { "C01", "C13", "C14", "X09" }) augments.Grant(id);
            Assert.IsFalse(augments.CanDraw, "only the slow group is left among risk cards");
            foreach (var id in new[] { "M01" }) augments.Grant(id);
            var reward = augments.DrawReward();
            Assert.AreEqual(3, reward.Length, "four cards are left; three are shown");
            Assert.AreEqual(3, reward.Select(c => c.id).Distinct().Count());
            foreach (var id in new[] { "S01", "S02", "S09", "M02" }) augments.Grant(id);
            Assert.IsEmpty(augments.DrawReward(), "nothing is left to offer");
        }

        [Test]
        public void SameStatCardsAddBeforeMultiplying()
        {
            var augments = new MaintenanceAugments(Catalog(), 1);
            augments.Grant("S01"); augments.Grant("C01"); augments.Grant("M02"); augments.Grant("M01");
            Assert.That(augments.DamageScale(augments.Sum(e => e.damage)), Is.EqualTo(1 + .2f + .35f + .4f).Within(1e-5));
            Assert.That(augments.IncomingScale(augments.Sum(e => e.incoming)), Is.EqualTo(1 + .25f - .45f).Within(1e-5));
            Assert.That(augments.WalkSpeedBonus, Is.EqualTo(-.45f).Within(1e-5));
            Assert.That(augments.DamageScale(-5), Is.EqualTo(.1f).Within(1e-5), "the sum keeps its floor");
            var healing = new MaintenanceAugments(Catalog(), 1);
            healing.Grant("S09"); healing.Grant("C13");
            Assert.That(healing.HealingScale, Is.EqualTo(.8f).Within(1e-5));
            Assert.That(healing.MaxHealthRatio, Is.EqualTo(2).Within(1e-5));
        }

        [Test]
        public void GrowingPainsStartsAtThirtyPercentAndGrowsToOneAndAHalf()
        {
            var augments = new MaintenanceAugments(Catalog(), 1);
            augments.Grant("C14");
            Assert.That(augments.MaxHealthRatio, Is.EqualTo(.3f).Within(1e-5));
            for (int i = 0; i < 9; i++) Assert.IsFalse(augments.RecordKill());
            Assert.IsTrue(augments.RecordKill());
            Assert.That(augments.MaxHealthRatio, Is.EqualTo(.4f).Within(1e-5), "10 kills add 10% of the base, not 10% of 30");
            for (int i = 10; i < 70; i++) augments.RecordKill();
            Assert.That(augments.MaxHealthRatio, Is.EqualTo(1).Within(1e-5), "70 kills return to the original maximum");
            for (int i = 70; i < 200; i++) augments.RecordKill();
            Assert.That(augments.MaxHealthRatio, Is.EqualTo(1.5f).Within(1e-5), "120 kills reach the final 150% and it stops there");
        }

        [Test]
        public void GrowingPainsUsesTheMaximumHealthBeforeItsSelection()
        {
            var augments = new MaintenanceAugments(Catalog(), 1);
            augments.Grant("S02");
            augments.Grant("C14");
            Assert.That(augments.MaxHealthRatio, Is.EqualTo(1.2f * .3f).Within(1e-5));
            for (int i = 0; i < 120; i++) augments.RecordKill();
            Assert.That(augments.MaxHealthRatio, Is.EqualTo(1.2f * 1.5f).Within(1e-5));
            augments.Grant("C13");
            Assert.That(augments.MaxHealthRatio, Is.EqualTo(1.2f * 1.5f + 1).Within(1e-5), "a later card adds on top");
        }
    }
}
