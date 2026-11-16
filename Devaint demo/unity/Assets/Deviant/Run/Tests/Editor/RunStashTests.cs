using Deviant.Run;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    public sealed class RunStashTests
    {
        [Test] public void SwapOrderMovesMountedStampsWithoutChangingBagOrEconomy()
        {
            var stash = new RunStash(); var economy = Economy(50);
            var form = stash.AddWeaponStamp("sword", "rapier"); var skill = stash.AddSkillStamp(2);
            Assert.IsTrue(stash.TryApply(0, form.Id, "sword", economy, Apply, out _));
            Assert.IsTrue(stash.TryApply(1, skill.Id, "blunt", economy, Apply, out _));
            var stored = stash.AddSkillStamp(3);
            stash.SwapWeaponSlots();
            Assert.That(stash.EquippedWeaponStamp(1), Is.SameAs(form)); Assert.That(stash.EquippedWeaponStamp(0), Is.Null);
            Assert.That(stash.EquippedSkillStamp(0), Is.SameAs(skill)); Assert.That(stash.EquippedSkillStamp(1), Is.Null);
            Assert.That(stash.Stamps, Is.EqualTo(new[] { stored })); Assert.That(economy.Gold, Is.EqualTo(50));
            stash.SwapWeaponSlots(); Assert.That(stash.EquippedWeaponStamp(0), Is.SameAs(form));
        }
        static RunEconomy Economy(int gold)
        {
            var economy = new RunEconomy(new EconomyData { rewards = new RewardTableData[0], prices = new PriceTrackData[0], stampSwapCost = 20 });
            economy.Earn(gold);
            return economy;
        }

        static string Apply(RunStamp stamp) => null;

        [Test]
        public void WeaponStampMustFitTheSlotBlueprintAndTheFirstOneIsFree()
        {
            var stash = new RunStash(); var economy = Economy(50);
            var stamp = stash.AddWeaponStamp("sword", "rapier");
            Assert.IsFalse(stash.TryApply(0, stamp.Id, "blunt", economy, Apply, out var error));
            Assert.AreEqual("이 도면에 맞는 무기 스탬프가 아닙니다.", error);
            Assert.IsTrue(stash.TryApply(0, stamp.Id, "sword", economy, Apply, out _));
            Assert.AreEqual(50, economy.Gold);
            Assert.AreSame(stamp, stash.EquippedWeaponStamp(0));
            Assert.AreEqual(0, stash.Stamps.Count);
        }

        [Test]
        public void ReplacingCostsTheSwapFeeAndDestroysTheOldStamp()
        {
            var stash = new RunStash(); var economy = Economy(100);
            var first = stash.AddSkillStamp(1); var second = stash.AddSkillStamp(2);
            Assert.IsTrue(stash.TryApply(0, first.Id, "sword", economy, Apply, out _));
            Assert.IsTrue(stash.TryApply(0, second.Id, "sword", economy, Apply, out _));
            Assert.AreEqual(80, economy.Gold);
            Assert.AreSame(second, stash.EquippedSkillStamp(0));
            Assert.AreEqual(0, stash.Stamps.Count, "the replaced stamp is not returned");
            Assert.IsNull(stash.EquippedWeaponStamp(0), "weapon and skill stamps fill separate places");
        }

        [Test]
        public void SameEffectShortfallAndFailedApplicationKeepTheStampAndTheGold()
        {
            var stash = new RunStash(); var economy = Economy(10);
            Assert.IsTrue(stash.TryApply(1, stash.AddSkillStamp(3).Id, "ranged", economy, Apply, out _));
            var same = stash.AddSkillStamp(3); var other = stash.AddSkillStamp(1);
            Assert.IsFalse(stash.TryApply(1, same.Id, "ranged", economy, Apply, out var error));
            Assert.AreEqual("이미 같은 스탬프가 적용되어 있습니다.", error);
            Assert.IsFalse(stash.TryApply(1, other.Id, "ranged", economy, Apply, out error));
            Assert.AreEqual("골드가 부족합니다.", error);
            economy.Earn(50);
            Assert.IsFalse(stash.TryApply(1, other.Id, "ranged", economy, _ => "실패", out error));
            Assert.AreEqual("실패", error);
            Assert.AreEqual(60, economy.Gold);
            Assert.AreEqual(2, stash.Stamps.Count);
            Assert.AreEqual(3, stash.EquippedSkillStamp(1).Skill);
            Assert.IsFalse(stash.TryApply(0, other.Id, null, economy, Apply, out error), "an empty weapon slot takes no stamp");
        }

        [Test]
        public void NewRunsStartWithEmptyStashAndFreshPrices()
        {
            var data = new EconomyData { rewards = new RewardTableData[0], prices = new[] { new PriceTrackData { id = PriceTracks.StatUpgrade, start = 60, step = 30 } } };
            int seed = 0; var flow = new RunFlow(() => ++seed, data);
            var first = flow.StartNewRun(0);
            first.Economy.Earn(500); first.Economy.TryPurchase(PriceTracks.StatUpgrade); first.Stash.AddInkParts(1);
            var second = flow.StartNewRun(0);
            Assert.AreEqual(0, second.Economy.Gold);
            Assert.AreEqual(60, second.Economy.PriceOf(PriceTracks.StatUpgrade));
            Assert.AreEqual(0, second.Stash.InkParts);
        }
    }
}
