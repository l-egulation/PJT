using System;
using Deviant.Definitions;

namespace Deviant.Presentation.Contracts
{
    public readonly struct FadeFrame
    {
        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public float Opacity { get; }
        public FadeFrame(float red, float green, float blue, float opacity)
        { Red = Unit(red); Green = Unit(green); Blue = Unit(blue); Opacity = Unit(opacity); }
        static float Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
    public enum FadeStatus { Playing, Completed, Cancelled, Failed }
    public interface IFadeHandle : IDisposable
    {
        Guid SessionId { get; }
        long Sequence { get; }
        int Priority { get; }
        string DefinitionVersion { get; }
        FadeStatus Status { get; }
        string Error { get; }
        FadeFrame Frame { get; }
        // Completed playback can retain its final output until its owner returns
        // it. Releasing retained output never rewrites a completed playback fact.
        bool IsRetained { get; }
        void Poll(double elapsed);
        void Cancel();
    }
    public interface IFadeScope : IDisposable
    {
        bool IsActive { get; }
        // One screen channel per owner. A valid new playback replaces only this
        // owner's previous playing/held request; invalid input preserves it.
        IFadeHandle Play(FadeDefinition definition, int priority);
    }
    public interface IFadeRequests
    {
        Guid SessionId { get; }
        bool IsAvailable { get; }
        int RequestCount { get; }
        IFadeScope OpenScope(string owner);
    }
    public readonly struct FadeSelection
    {
        public FadeFrame Frame { get; }
        public Guid SessionId { get; }
        public long? RequestSequence { get; }
        public FadeSelection(FadeFrame frame, Guid session, long? sequence)
        { Frame = frame; SessionId = session; RequestSequence = sequence; }
    }
}
