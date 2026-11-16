using UnityEngine;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;

namespace Deviant.Combat
{
    public partial class BombProjectile : ITimedBombHost
    {
        TimedBombExecution timed;
        public bool TimedLanded { get { return timed.IsLanded(Definition.FuseSeconds); } }
        public float TimedFuseRemaining { get { return timed.FuseRemaining(Definition.FuseSeconds); } }
        float ITimedBombHost.FlightLifetime => Definition.Lifetime;
        float ITimedBombHost.FuseSeconds => Definition.FuseSeconds;
        void ITimedBombHost.Detonate() => ExplodeStopped();
        void ITimedBombHost.AdvanceFlight(float duration)
        {
            Vector2 delta=velocity*duration+Vector2.down*(.5f*Definition.Gravity*duration*duration);
            velocity.y-=Definition.Gravity*duration;
            SweepTimed(Position+delta,duration);
        }

        void StepTimed(float dt) => timed.Step(dt,this);

        void SweepTimed(Vector2 destination,float duration)
        {
            var host=new TimedSweepHost(this,destination);
            TimedBombSweepExecution.Sweep(ref host,duration);
        }
    }
}
