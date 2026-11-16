using System;
using NUnit.Framework;

namespace Deviant.Run.Tests
{
    public sealed class StageRecoveryTests
    {
        static StageRecovery Create() => new StageRecovery(new StageRecoveryData
        { usesPerStage = 2, prices = new[] { 100, 150 }, healthFraction = .25f, holdSeconds = 1, reuseDelaySeconds = 1, interactionReach = 1.8f });

        [Test] public void StageBudgetIsTwoQuarterHealthChargesWithNoThirdUse()
        {
            var recovery = Create();
            Assert.That(recovery.TryUse(10, 100, 0, out float first), Is.True);
            Assert.That(first, Is.EqualTo(25));
            Assert.That(recovery.TryUse(35, 100, 1, out float second), Is.True);
            Assert.That(second, Is.EqualTo(25));
            Assert.That(recovery.Remaining, Is.Zero);
            Assert.That(recovery.TryUse(1, 100, 10000, out _), Is.False, "time and revisits never replenish the reservoir");
        }

        [Test] public void OverhealIsCappedAndFullHealthDoesNotSpendAnything()
        {
            var recovery = Create();
            Assert.That(recovery.TryUse(100, 100, 0, out _), Is.False);
            Assert.That(recovery.Remaining, Is.EqualTo(2));
            Assert.That(recovery.TryUse(97.5f, 100, 0, out float amount), Is.True);
            Assert.That(amount, Is.EqualTo(2.5f));
            Assert.That(recovery.Remaining, Is.EqualTo(1));
        }

        [Test] public void HealingTracksUpgradedMaximumAndUsesFractionalHealth()
        {
            var recovery = Create();
            Assert.That(recovery.TryUse(10, 150, 0, out float amount), Is.True);
            Assert.That(amount, Is.EqualTo(37.5f));
        }

        [Test] public void CooldownUsesOnlyTheSuppliedSimulationClock()
        {
            var recovery = Create(); recovery.TryUse(10, 100, 5, out _);
            for (int i = 0; i < 100; i++) Assert.That(recovery.TryUse(10, 100, 5, out _), Is.False);
            Assert.That(recovery.TryUse(10, 100, 5.999f, out _), Is.False);
            Assert.That(recovery.Remaining, Is.EqualTo(1));
            Assert.That(recovery.TryUse(10, 100, 6, out _), Is.True);
        }

        [Test] public void DeadOrInvalidHealthCannotConsumeACharge()
        {
            var recovery = Create();
            foreach (float health in new[] { 0, -1, float.NaN, float.PositiveInfinity })
                Assert.That(recovery.TryUse(health, 100, 0, out _), Is.False);
            foreach (float maximum in new[] { 0, -1, float.NaN, float.PositiveInfinity })
                Assert.That(recovery.TryUse(10, maximum, 0, out _), Is.False);
            Assert.That(recovery.TryUse(10, 100, float.NaN, out _), Is.False);
            Assert.That(recovery.Remaining, Is.EqualTo(2));
        }

        [Test] public void NewStageAndDevelopmentResetHaveFreshIndependentReservoirs()
        {
            var first = Create(); first.TryUse(10, 100, 9, out _);
            var second = Create(); Assert.That(second.Remaining, Is.EqualTo(2));
            Assert.That(first.Remaining, Is.EqualTo(1));
            first.Reset(); Assert.That(first.Remaining, Is.EqualTo(2));
            Assert.That(first.TryUse(10, 100, 0, out _), Is.True, "reset also clears old clock cooldown");
        }

        [Test] public void InvalidTuningIsRejectedAndOriginalDtoCannotChangeLiveBalance()
        {
            Assert.Throws<ArgumentNullException>(() => new StageRecovery(null));
            Assert.Throws<ArgumentException>(() => new StageRecovery(new StageRecoveryData()));
            var data = new StageRecoveryData { usesPerStage = 2, prices = new[] { 100, 150 }, healthFraction = .25f, holdSeconds = 1, interactionReach = 1.8f };
            var recovery = new StageRecovery(data); data.usesPerStage = 100; data.healthFraction = 1;
            recovery.TryUse(1, 100, 0, out float amount);
            Assert.That(amount, Is.EqualTo(25)); Assert.That(recovery.Capacity, Is.EqualTo(2));
        }

        [Test] public void PricesRiseOnlyAfterSuccessfulHealingAndInsufficientGoldCostsNothing()
        {
            var recovery = Create(); var wallet = new RunEconomy(new EconomyData());
            wallet.Earn(99); Assert.That(recovery.Price, Is.EqualTo(100));
            Assert.That(recovery.TryUse(20, 100, 0, out float restored, wallet.TrySpend), Is.False);
            Assert.That(restored, Is.Zero); Assert.That(wallet.Gold, Is.EqualTo(99)); Assert.That(recovery.Remaining, Is.EqualTo(2));
            wallet.Earn(151);
            Assert.That(recovery.TryUse(100, 100, 0, out _, wallet.TrySpend), Is.False);
            Assert.That(wallet.Gold, Is.EqualTo(250));
            Assert.That(recovery.TryUse(20, 100, 0, out _, wallet.TrySpend), Is.True);
            Assert.That(wallet.Gold, Is.EqualTo(150)); Assert.That(recovery.Price, Is.EqualTo(150));
            Assert.That(recovery.TryUse(20, 100, 0, out _, wallet.TrySpend), Is.False, "cooldown cannot charge gold");
            Assert.That(recovery.TryUse(20, 100, 1, out _, wallet.TrySpend), Is.True);
            Assert.That(wallet.Gold, Is.Zero); Assert.That(recovery.Remaining, Is.Zero);
        }

        [Test] public void HoldRequiresOneContinuousSecondAndCompletesOnlyOnce()
        {
            var hold = new StageRecoveryHold(1);
            Assert.That(hold.Begin(2, 3), Is.True);
            Assert.That(hold.Advance(2.99f, true, true, 3), Is.False);
            Assert.That(hold.Holding, Is.True); Assert.That(hold.Progress(2.5f), Is.EqualTo(.5f));
            Assert.That(hold.Advance(3, true, true, 3), Is.True);
            Assert.That(hold.Advance(4, true, true, 3), Is.False, "keeping the key held cannot buy again");
        }

        [Test] public void ReleasedInterruptedAndHitHoldsCancelRatherThanAccumulate()
        {
            var hold = new StageRecoveryHold(1);
            hold.Begin(0, 0); Assert.That(hold.Advance(.9f, false, true, 0), Is.False); Assert.That(hold.Holding, Is.False);
            hold.Begin(1, 0); Assert.That(hold.Advance(1.9f, true, false, 0), Is.False); Assert.That(hold.Holding, Is.False);
            hold.Begin(2, 0); Assert.That(hold.Advance(2.9f, true, true, 1), Is.False); Assert.That(hold.Holding, Is.False);
            hold.Begin(3, 1); Assert.That(hold.Advance(3.9f, true, true, 1), Is.False);
            Assert.That(hold.Advance(4, true, true, 1), Is.True, "each new hold starts from zero");
        }
    }
}
