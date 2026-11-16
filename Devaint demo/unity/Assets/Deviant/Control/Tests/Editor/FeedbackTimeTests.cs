using System;
using Deviant.Control.Contracts;
using NUnit.Framework;

namespace Deviant.Control.Tests
{
    public sealed class FeedbackTimeTests
    {
        [Test] public void MinimumSlowExpiresWithoutClearingOtherOwnersOrInput()
        {
            using(var session=new ControlSession())using(var a=session.OpenScope("A"))using(var b=session.OpenScope("B"))
            {
                a.BlockInput(InputChannels.Interaction);
                var slowA=((IFeedbackTimeScope)a).RequestSlow(.5f,1);
                var slowB=((IFeedbackTimeScope)b).RequestSlow(.35f,.15f);
                Assert.That(session.Current.SimulationScale,Is.EqualTo(.35f));session.AdvanceAccent(.16f);
                Assert.That(slowB.IsActive,Is.False);Assert.That(slowA.IsActive,Is.True);
                Assert.That(session.Current.SimulationScale,Is.EqualTo(.5f));Assert.That(session.Current.Blocked,Is.EqualTo(InputChannels.Interaction));
                slowA.Dispose();Assert.That(session.Current.SimulationScale,Is.EqualTo(1));
            }
        }
        [Test] public void PauseHoldsAccentTtlAndCancelledSlowCannotRevive()
        {
            using(var session=new ControlSession())using(var a=session.OpenScope("feedback"))using(var ui=session.OpenScope("menu"))
            {
                var slow=((IFeedbackTimeScope)a).RequestSlow(.35f,.15f);var pause=ui.PauseSimulation();
                session.AdvanceAccent(10);Assert.That(slow.IsActive,Is.True);Assert.That(session.Current.SimulationScale,Is.Zero);
                a.Dispose();pause.Dispose();Assert.That(session.Current.SimulationScale,Is.EqualTo(1));
            }
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(1.1f)]
        public void InvalidSlowDoesNotChangeControl(float value)
        {
            using(var session=new ControlSession())using(var scope=session.OpenScope("feedback"))
            {var before=session.Current;Assert.Throws<ArgumentOutOfRangeException>(()=>((IFeedbackTimeScope)scope).RequestSlow(value,.1f));Assert.That(session.Current,Is.EqualTo(before));}
        }
    }
}
