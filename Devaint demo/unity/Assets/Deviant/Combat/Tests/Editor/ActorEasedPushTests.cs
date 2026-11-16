using System;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorEasedPushTests
    {
        [Test] public void EasedPushTravelsTheSameDistanceFastFirstAndSlowLast()
        {
            var statuses = new ActorStatusState(); const float step = .02f;
            statuses.PushEased(1, -1, .5f, .12f);
            Assert.That(statuses.Read().KnockUntil, Is.EqualTo(1.12f).Within(1e-5f));
            float travelled = 0, first = 0, last = 0;
            for (float now = 1; now < 1.12f - 1e-5f; now += step)
            {
                float remaining = Math.Max(0, Math.Min(1, (statuses.Read().KnockUntil - now) / step));
                float delta = statuses.KnockSpeedAt(now, step) * remaining * step;
                if (now == 1) first = delta; last = delta; travelled += delta;
            }
            Assert.That(travelled, Is.EqualTo(-.5f).Within(1e-3f));
            Assert.That(Math.Abs(first), Is.GreaterThan(Math.Abs(last) * 10));
        }

        [Test] public void AnyOtherKnockWriteReplacesTheEasedCurve()
        {
            var statuses = new ActorStatusState();
            statuses.PushEased(0, 1, 1, .2f);
            statuses.SetKnockMotion(.1f, 3);
            Assert.That(statuses.KnockSpeedAt(0, .02f), Is.EqualTo(3));
            statuses.PushEased(1, 1, 1, .2f); statuses.PlayerHit(1.5f, -1, false);
            Assert.That(statuses.KnockSpeedAt(1.5f, .02f), Is.EqualTo(-5));
            statuses.PushEased(2, 1, 1, .2f); statuses.Reset();
            Assert.That(statuses.KnockSpeedAt(2, .02f), Is.EqualTo(statuses.Read().KnockSpeed));
        }
    }
}
