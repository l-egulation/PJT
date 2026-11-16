using System;
using System.Collections.Generic;

namespace Deviant.Combat.Contracts
{
    public enum CombatInputSource { Simulation, Keyboard, Gamepad, OtherDevice }
    public enum CombatInputTimeDomain { Simulation, Realtime }
    public enum CombatInputAdmissionStatus { Admitted, Discarded, RejectedSession, RejectedSequence, RejectedMetadata, RejectedCapacity }
    public enum CombatInputDisposition { Consumed, Discarded }
    public enum CombatInputDiscardReason { None, Cleared, Blocked, Paused, Dead, Reset, Replaced }
    public enum CombatInputBatchKind { Consumption, Discard, TickBoundary }
    public enum CombatInputTickOutcome { NotObserved, Completed, Faulted }

    // Actor identity is runtime-local. Every reset invalidates the generation;
    // only NewEpoch restarts the simulation clock and increments Epoch.
    public readonly struct CombatInputSession : IEquatable<CombatInputSession>
    {
        public int ActorId { get; }
        public long Generation { get; }
        public long Epoch { get; }
        public CombatInputSession(int actorId, long generation, long epoch)
        { ActorId = actorId; Generation = generation; Epoch = epoch; }
        public bool Equals(CombatInputSession other) => ActorId == other.ActorId && Generation == other.Generation && Epoch == other.Epoch;
        public override bool Equals(object obj) => obj is CombatInputSession other && Equals(other);
        public override int GetHashCode() => unchecked((ActorId * 397 ^ Generation.GetHashCode()) * 397 ^ Epoch.GetHashCode());
    }

    // One edge, or one continuous sample. Multiple flags in a legacy value intent
    // are expanded by its adapter in the documented stable order.
    public readonly struct CombatInputSample
    {
        public CombatInputSession Session { get; }
        public long Sequence { get; }
        public CombatInput Input { get; }
        public CombatInputSource Source { get; }
        public int DeviceId { get; }
        public CombatInputTimeDomain TimeDomain { get; }
        public double Timestamp { get; }
        public CombatInputSample(CombatInputSession session, long sequence, CombatInput input,
            CombatInputSource source, int deviceId, CombatInputTimeDomain timeDomain, double timestamp)
        { Session = session; Sequence = sequence; Input = input; Source = source; DeviceId = deviceId; TimeDomain = timeDomain; Timestamp = timestamp; }
    }

    // Admission is not attack success, action selection, or even tick consumption.
    // A rejected token/sequence/metadata/capacity never mutates ingress state.
    public readonly struct CombatInputAdmission
    {
        public CombatInputSample Sample { get; }
        public CombatInputAdmissionStatus Status { get; }
        public CombatInputDiscardReason DiscardReason { get; }
        public CombatInputAdmission(CombatInputSample sample, CombatInputAdmissionStatus status, CombatInputDiscardReason discardReason = CombatInputDiscardReason.None)
        { Sample = sample; Status = status; DiscardReason = discardReason; }
    }

    public readonly struct CombatInputReceipt
    {
        public CombatInputSample Sample { get; }
        public CombatInputDisposition Disposition { get; }
        public CombatInputDiscardReason DiscardReason { get; }
        public CombatInputReceipt(CombatInputSample sample, CombatInputDisposition disposition, CombatInputDiscardReason discardReason = CombatInputDiscardReason.None)
        { Sample = sample; Disposition = disposition; DiscardReason = discardReason; }
    }

    // Completed input boundaries, not per-action outcomes. Consumed means the
    // arbiter saw the coalesced intent; TickOutcome independently records whether
    // the whole world tick returned. TickCount/Now use its entry clock. Reset
    // discards retain each sample's old session and use the current reset clock.
    public sealed class CombatInputBatch
    {
        public CombatInputSession Session { get; }
        public long TickCount { get; }
        public float Now { get; }
        public CombatInputBatchKind Kind { get; }
        public CombatInputTickOutcome TickOutcome { get; }
        public float ObservedThroughNow { get; }
        public CombatInput Input { get; }
        public IReadOnlyList<CombatInputReceipt> Receipts { get; }
        public CombatInputBatch(CombatInputSession session, long tickCount, float now, CombatInputBatchKind kind,
            CombatInput input, CombatInputReceipt[] receipts, int receiptCount, CombatInputTickOutcome tickOutcome = CombatInputTickOutcome.NotObserved, float? observedThroughNow = null)
        {
            if (receipts == null) throw new ArgumentNullException(nameof(receipts));
            if (receiptCount < 0 || receiptCount > receipts.Length) throw new ArgumentOutOfRangeException(nameof(receiptCount));
            Session = session; TickCount = tickCount; Now = now; Kind = kind; Input = input;
            TickOutcome = tickOutcome;
            ObservedThroughNow = observedThroughNow ?? now;
            var snapshot = receiptCount == 0 ? Array.Empty<CombatInputReceipt>() : new CombatInputReceipt[receiptCount];
            if (receiptCount > 0) Array.Copy(receipts, snapshot, receiptCount);
            Receipts = Array.AsReadOnly(snapshot);
        }
    }

    // Opt-in bounded polling only: no observation callback can reenter gameplay.
    // Overflow drops the oldest observation, never an admitted gameplay input.
    // The writer stores preallocated values only; immutable snapshot allocation
    // belongs to TryRead on the observer side, outside gameplay execution.
    public interface ICombatInputCapture : IDisposable
    {
        bool IsActive { get; }
        long DroppedBatches { get; }
        bool TryRead(out CombatInputBatch batch);
    }
}
