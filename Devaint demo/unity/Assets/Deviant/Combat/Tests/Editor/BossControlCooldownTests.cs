using System;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    // docs/design/combat/rules.md#보스의-기절-상태와-패턴: 보스 기절 12초·속박 8초 내부 쿨타임.
    public class BossControlCooldownTests
    {
        [Test] public void RuntimeDataMatchesConfirmedSeconds()
        {
            Assert.That(BossControlCooldownSettings.Settings.stunSeconds, Is.EqualTo(0));
            Assert.That(BossControlCooldownSettings.Settings.rootSeconds, Is.EqualTo(0));
        }

        [Test] public void CooldownStartsOnApplicationAndRefusesUntilItEnds()
        {
            var c = new BossControlCooldowns(12, 8);
            Assert.That(c.TryStun(1), Is.True); Assert.That(c.StunReadyAt, Is.EqualTo(13));
            Assert.That(c.TryStun(12.99f), Is.False, "refused while cooling");
            Assert.That(c.StunReadyAt, Is.EqualTo(13), "a refused application does not restart the clock");
            Assert.That(c.TryStun(13), Is.True); Assert.That(c.StunReadyAt, Is.EqualTo(25));
        }

        [Test] public void StunAndRootKeepSeparateClocks()
        {
            var c = new BossControlCooldowns(12, 8);
            Assert.That(c.TryStun(0), Is.True);
            Assert.That(c.TryRoot(0), Is.True, "a stun does not spend the root cooldown");
            Assert.That(c.TryRoot(7.9f), Is.False); Assert.That(c.TryRoot(8), Is.True);
            Assert.That(c.TryStun(8), Is.False);
            c.Reset(); Assert.That(c.TryStun(8), Is.True);
        }

        [Test] public void ZeroCooldownAllowsRepeatedControlAtTheSameTime()
        {
            var c = new BossControlCooldowns(0, 0);
            Assert.That(c.TryStun(1) && c.TryStun(1) && c.TryRoot(1) && c.TryRoot(1), Is.True);
        }

        [Test] public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossControlCooldowns(-1, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossControlCooldowns(12, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossControlCooldowns(12, 8).TryStun(-1));
        }

        [Test] public void BuffSlotDarknessShrinksWithRemainingTime()
        {
            var slot = new BuffSlot(BuffSlotIcon.Stun, BuffSlotTrigger.None, 12, 12);
            Assert.That(slot.DarkFraction(0), Is.EqualTo(1));
            Assert.That(slot.DarkFraction(9), Is.EqualTo(.25f).Within(1e-6f));
            Assert.That(slot.DarkFraction(12), Is.Zero); Assert.That(slot.Remaining(15), Is.Zero);
        }
    }
}
