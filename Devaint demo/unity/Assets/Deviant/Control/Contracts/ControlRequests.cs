using System;

namespace Deviant.Control.Contracts
{
    [Flags]
    public enum InputChannels { None = 0, Combat = 1, Interaction = 2, Dialogue = 4, Navigation = 8 }

    public readonly struct ControlState
    {
        public InputChannels Blocked { get; }
        public bool SimulationPaused { get; }
        public long Revision { get; }
        readonly float slowScale;
        public float SimulationScale => SimulationPaused ? 0 : slowScale > 0 ? slowScale : 1;
        public ControlState(InputChannels blocked, bool simulationPaused, long revision, float simulationScale = 1)
        { Blocked = blocked; SimulationPaused = simulationPaused; Revision = revision; slowScale = simulationScale; }
        public bool Blocks(InputChannels channel) => (Blocked & channel) != 0;
    }
    public interface IControlLease : IDisposable { bool IsActive { get; } }
    public interface IFeedbackTimeScope : IControlScope
    {
        IControlLease RequestSlow(float scale, float accentSeconds);
    }
    public interface IControlScope : IDisposable
    {
        bool IsActive { get; }
        IControlLease BlockInput(InputChannels channels);
        IControlLease PauseSimulation();
    }
    public interface IControlRequests
    {
        ControlState Current { get; }
        // Every call opens a distinct owner scope, even with the same diagnostic name.
        IControlScope OpenScope(string owner);
    }
}
