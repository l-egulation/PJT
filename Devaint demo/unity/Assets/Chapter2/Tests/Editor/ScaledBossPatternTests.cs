using Deviant.Chapter2.RoseBlue;
using Deviant.Combat;
using NUnit.Framework;

namespace Deviant.Chapter2.Tests
{
    public sealed class ScaledBossPatternTests
    {
        [Test] public void DestructibleExchangeTakesTheSameEightSwordHitsAsBeforeScaling()
        {
            var volley = new ExchangeVolley(24, true);
            Assert.That(volley.RoseArrowHp, Is.EqualTo(500));
            Assert.That(volley.BlueOrbHp, Is.EqualTo(500));
            Assert.That(Tuning.RevivalShieldHp, Is.EqualTo(500));
            for (int hit = 0; hit < 7; hit++) volley.DamageBatch(65, 0);
            Assert.That(volley.RoseArrowHp, Is.EqualTo(45));
            volley.DamageBatch(65, 0);
            Assert.That(volley.RoseArrowHp, Is.Zero);
            Assert.That(volley.BlueOrbHp, Is.EqualTo(500));
        }
        // 2026-11-16 user decision: boss HP was raised but gimmick damage and heal stay at a fixed 500
        // instead of keeping their old HP fraction.
        [Test] public void BossGimmickDamageStaysFixedAtFiveHundredAndPlayerDamageIsUnchanged()
        {
            var encounter = new Encounter(RevivalAttackPolicy.PreserveSpawned);
            encounter.CompleteCutscene(encounter.Begin());
            encounter.DamageBatch(Tuning.Phase1OrbMachineDamage, 0, 0);
            Assert.That(Tuning.Phase1OrbMachineDamage, Is.EqualTo(500));
            Assert.That(encounter.MachineHp, Is.EqualTo(Tuning.MachineHp - 500).Within(.00001));
            Assert.That(Tuning.HomingBossDamage, Is.EqualTo(500));
            Assert.That(Tuning.ExchangeHeal, Is.EqualTo(500));
            Assert.That(Tuning.HomingPlayerDamage, Is.EqualTo(20));
            Assert.That(Tuning.ExchangeDamage, Is.EqualTo(20));
        }
    }
}
