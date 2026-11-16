using System;
using Deviant.Definitions;

namespace Deviant.Presentation.Contracts
{
    public enum CueStatus { Pending, Playing, Completed, Cancelled, Failed }
    public readonly struct CueOccurrence : IEquatable<CueOccurrence>
    {
        public Guid ExecutionId { get; }
        public string CueId { get; }
        public int Ordinal { get; }
        public CueOccurrence(Guid executionId, string cueId, int ordinal = 0)
        {
            if (executionId == Guid.Empty || string.IsNullOrWhiteSpace(cueId) || ordinal < 0) throw new ArgumentException("Execution, cue ID and nonnegative occurrence ordinal are required.");
            ExecutionId = executionId; CueId = cueId; Ordinal = ordinal;
        }
        public bool Equals(CueOccurrence other) => ExecutionId == other.ExecutionId && CueId == other.CueId && Ordinal == other.Ordinal;
        public override bool Equals(object obj) => obj is CueOccurrence other && Equals(other);
        public override int GetHashCode() => ExecutionId.GetHashCode() ^ (CueId?.GetHashCode() ?? 0) ^ Ordinal;
    }
    public readonly struct CueAnchor
    {
        public float X { get; }
        public float Y { get; }
        public int Facing { get; }
        public CueAnchor(float x, float y, int facing)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) || facing != -1 && facing != 1)
                throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y; Facing = facing;
        }
    }
    public interface ICueClock { float ReadElapsed(); }
    public interface ICueHandle : IDisposable
    {
        Guid SessionId { get; }
        CueOccurrence Occurrence { get; }
        string DefinitionVersion { get; }
        CueStatus Status { get; }
        string Error { get; }
        // Sample only this handle after its owner updates the injected clock.
        // Other handles and their clocks remain untouched.
        void Poll();
        void Cancel();
    }
    public interface ICueScope : IDisposable
    {
        bool IsActive { get; }
        ICueHandle Play(ICueDefinition definition, CueOccurrence occurrence, CueAnchor anchor, ICueClock clock);
    }
    public interface ICuePlayer
    {
        Guid SessionId { get; }
        int ActiveCount { get; }
        ICueScope OpenScope(string owner, ICueViewFactory factory = null);
    }
    public interface ICueView : IDisposable
    {
        // Presentation only: no damage, motion, resource or time authority.
        // The injected clock owns elapsed time and completion. Outputs sample
        // this time directly; sprite frames are one medium's interpretation.
        void Sample(float elapsed);
        // Dispose deactivates/stops immediately; returning twice must be safe.
    }
    public sealed class CueViewResult
    {
        public ICueView View { get; }
        public string Error { get; }
        CueViewResult(ICueView view, string error) { View = view; Error = error; }
        public static CueViewResult Ready(ICueView view) => new CueViewResult(view ?? throw new ArgumentNullException(nameof(view)), null);
        public static CueViewResult Failure(string error) => new CueViewResult(null, string.IsNullOrWhiteSpace(error) ? "Cue media unavailable." : error);
    }
    public interface ICueViewFactory
    {
        // Resolve on the presentation thread, synchronously or later. The view
        // remains inactive until Sample. Return a cancellable ticket for a
        // pending load; synchronous completion may return null. Late/duplicate
        // results are accepted only by the original session/handle.
        IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved);
    }
}
