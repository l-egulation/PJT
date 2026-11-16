using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // One single-threaded actor/session owns admission, ordered pending samples
    // and the arbiter projection. Retention is bounded even without a recorder.
    public sealed class CombatInputIngress
    {
        public const int DefaultCapacity = 512;
        public const int MaximumCapturedReceipts = 131072;
        readonly CombatInputBuffer buffer = new CombatInputBuffer();
        readonly CombatInputSample[] pending;
        int count;
        long lastSequence;
        long evaluatedThrough;
        CombatInput evaluatedInput;
        bool evaluated;
        Capture capture;
        bool observingTick;
        public CombatInputSession Session { get; private set; }
        public CombatInput Current => buffer.Current;
        public long NextSequence => lastSequence + 1;
        public int AvailableCapacity => pending.Length - count;
        public int PendingCount => count;
        public CombatInputSample PendingAt(int index) => pending[index];
        public bool WasEvaluated(long sequence) => evaluated && sequence <= evaluatedThrough;
        public CombatCommandOrigins Origins { get; private set; }

        public CombatInputIngress(int actorId, int capacity = DefaultCapacity)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Session = new CombatInputSession(actorId, 1, 0);
            pending = new CombatInputSample[capacity];
        }

        public CombatInputAdmission Submit(CombatInputSample sample, long tickCount, float now,
            CombatInputDiscardReason discardReason = CombatInputDiscardReason.None)
        {
            var validation = Validate(sample, discardReason);
            if (validation != CombatInputAdmissionStatus.Admitted) return new CombatInputAdmission(sample, validation);
            lastSequence = sample.Sequence;
            if (discardReason != CombatInputDiscardReason.None)
            {
                Discard(discardReason, tickCount, now, false);
                if (capture != null)
                {
                    capture.Receipts[0] = new CombatInputReceipt(sample, CombatInputDisposition.Discarded, discardReason);
                    Publish(CombatInputBatchKind.Discard, default, 1, tickCount, now);
                }
                return new CombatInputAdmission(sample, CombatInputAdmissionStatus.Discarded, discardReason);
            }
            pending[count++] = sample;
            buffer.Submit(sample.Input);
            return new CombatInputAdmission(sample, CombatInputAdmissionStatus.Admitted);
        }
        public CombatInputAdmissionStatus Validate(CombatInputSample sample, CombatInputDiscardReason discardReason = CombatInputDiscardReason.None)
        {
            if (!sample.Session.Equals(Session)) return CombatInputAdmissionStatus.RejectedSession;
            if (sample.Sequence <= lastSequence || sample.Sequence == long.MaxValue) return CombatInputAdmissionStatus.RejectedSequence;
            if (!ValidMetadata(sample)) return CombatInputAdmissionStatus.RejectedMetadata;
            if (discardReason == CombatInputDiscardReason.None && count == pending.Length) return CombatInputAdmissionStatus.RejectedCapacity;
            return CombatInputAdmissionStatus.Admitted;
        }

        static bool ValidMetadata(CombatInputSample sample)
        {
            if (double.IsNaN(sample.Timestamp) || double.IsInfinity(sample.Timestamp) || sample.Timestamp < 0) return false;
            if (sample.Source == CombatInputSource.Simulation)
            { if (sample.DeviceId != 0 || sample.TimeDomain != CombatInputTimeDomain.Simulation) return false; }
            else if (sample.Source == CombatInputSource.Keyboard || sample.Source == CombatInputSource.Gamepad || sample.Source == CombatInputSource.OtherDevice)
            { if (sample.DeviceId <= 0 || sample.TimeDomain != CombatInputTimeDomain.Realtime) return false; }
            else return false;
            int edges = (int)(sample.Input.Pressed | sample.Input.Released);
            return (edges & (edges - 1)) == 0 && (sample.Input.Pressed & sample.Input.Released) == 0;
        }
        public CombatInput ReadForArbitration()
        {
            evaluatedThrough = lastSequence; evaluatedInput = buffer.Current; evaluated = true;
            long basic = 0, special = 0, dash = 0, jump = 0, swap = 0, drop = 0, weapon1Basic = 0, weapon2Basic = 0, weapon1Special = 0, weapon2Special = 0;
            for (int i = 0; i < count; i++)
            {
                var sample = pending[i];
                switch (sample.Input.Pressed)
                {
                    case CombatButtons.Weapon1Basic: if (weapon1Basic == 0) weapon1Basic = sample.Sequence; break;
                    case CombatButtons.Weapon2Basic: if (weapon2Basic == 0) weapon2Basic = sample.Sequence; break;
                    case CombatButtons.Weapon1Special: if (weapon1Special == 0) weapon1Special = sample.Sequence; break;
                    case CombatButtons.Weapon2Special: if (weapon2Special == 0) weapon2Special = sample.Sequence; break;
                    case CombatButtons.Basic: if (basic == 0) basic = sample.Sequence; break;
                    case CombatButtons.Special: if (special == 0) special = sample.Sequence; break;
                    case CombatButtons.Dash: if (dash == 0) dash = sample.Sequence; break;
                    case CombatButtons.Jump: if (jump == 0) jump = sample.Sequence; break;
                    case CombatButtons.Swap: if (swap == 0) swap = sample.Sequence; break;
                    case CombatButtons.Drop: if (drop == 0) drop = sample.Sequence; break;
                }
            }
            Origins = new CombatCommandOrigins(Session, basic, special, dash, jump, swap, drop, weapon1Basic, weapon2Basic, weapon1Special, weapon2Special);
            return evaluatedInput;
        }
        public void BeginTick() => observingTick = true;
        public void CompleteTick(long entryTick, float entryNow, float endedNow, bool completed)
        {
            observingTick = false;
            capture?.CompleteTick(Session, entryTick, entryNow, endedNow, completed);
        }

        // Called at the existing ClearPressed boundary, after arbitration, never
        // as a claim that any selected action or the rest of the tick succeeded.
        public void Consume(long tickCount, float now)
        {
            var input = evaluated ? evaluatedInput : buffer.Current;
            if (capture != null)
            {
                for (int i = 0; i < count; i++)
                {
                    bool consumed = !evaluated || pending[i].Sequence <= evaluatedThrough;
                    capture.Receipts[i] = new CombatInputReceipt(pending[i], consumed ? CombatInputDisposition.Consumed : CombatInputDisposition.Discarded,
                        consumed ? CombatInputDiscardReason.None : CombatInputDiscardReason.Cleared);
                }
                Publish(CombatInputBatchKind.Consumption, input, count, tickCount, now);
            }
            count = 0;
            evaluated = false;
            buffer.ClearPressed();
        }

        public void Discard(CombatInputDiscardReason reason, long tickCount, float now, bool keepContinuous = false)
        {
            if (capture != null && count > 0)
            {
                for (int i = 0; i < count; i++) capture.Receipts[i] = new CombatInputReceipt(pending[i], CombatInputDisposition.Discarded, reason);
                Publish(CombatInputBatchKind.Discard, buffer.Current, count, tickCount, now);
            }
            count = 0;
            evaluated = false;
            if (keepContinuous) buffer.ClearPressed(); else buffer.Clear();
        }

        public void Reset(bool newEpoch, long tickCount, float now)
        {
            Session = new CombatInputSession(Session.ActorId, checked(Session.Generation + 1), newEpoch ? checked(Session.Epoch + 1) : Session.Epoch);
            Discard(CombatInputDiscardReason.Reset, tickCount, now, !newEpoch);
            // Keep sequence monotonic across generations, including NewEpoch.
        }

        public ICombatInputCapture BeginCapture(int batchCapacity)
        {
            if (batchCapacity <= 0 || batchCapacity > MaximumCapturedReceipts / pending.Length)
                throw new ArgumentOutOfRangeException(nameof(batchCapacity));
            var next = new Capture(this, batchCapacity, pending.Length);
            capture?.Dispose();
            return capture = next;
        }
        public void EndCapture() => capture?.Dispose();

        void Publish(CombatInputBatchKind kind, CombatInput input, int receiptCount, long tickCount, float now)
            => capture.Add(Session, tickCount, now, kind, input, receiptCount, observingTick);

        sealed class Capture : ICombatInputCapture
        {
            CombatInputIngress owner;
            struct BatchSlot
            {
                public CombatInputSession Session;
                public long Tick;
                public float Now, EndedNow;
                public CombatInputBatchKind Kind;
                public CombatInput Input;
                public CombatInputTickOutcome Outcome;
                public CombatInputReceipt[] Receipts;
                public int ReceiptCount;
                public bool AwaitingTick;
            }
            readonly BatchSlot[] batches;
            int start, count;
            public readonly CombatInputReceipt[] Receipts;
            bool hasConsumption;
            public bool IsActive => owner != null;
            public long DroppedBatches { get; private set; }
            public Capture(CombatInputIngress owner, int capacity, int inputCapacity)
            {
                this.owner = owner; batches = new BatchSlot[capacity];
                for (int i = 0; i < capacity; i++) batches[i].Receipts = new CombatInputReceipt[inputCapacity];
                Receipts = new CombatInputReceipt[inputCapacity];
            }
            public void CompleteTick(CombatInputSession session, long entryTick, float entryNow, float endedNow, bool completed)
            {
                var outcome = completed ? CombatInputTickOutcome.Completed : CombatInputTickOutcome.Faulted;
                if (!hasConsumption)
                    Add(session, entryTick, entryNow, CombatInputBatchKind.TickBoundary, default, 0, true);
                for (int i = 0; i < count; i++)
                {
                    ref var slot = ref batches[(start + i) % batches.Length];
                    if (!slot.AwaitingTick) continue;
                    slot.AwaitingTick = false;
                    if (slot.Kind != CombatInputBatchKind.Discard) { slot.Outcome = outcome; slot.EndedNow = endedNow; }
                }
                hasConsumption = false;
            }
            public void Add(CombatInputSession session, long tick, float now, CombatInputBatchKind kind, CombatInput input, int receiptCount, bool awaitingTick)
            {
                if (count == batches.Length)
                { start = (start + 1) % batches.Length; count--; DroppedBatches++; }
                ref var slot = ref batches[(start + count) % batches.Length];
                slot.Session = session; slot.Tick = tick; slot.Now = now; slot.EndedNow = now;
                slot.Kind = kind; slot.Input = input; slot.Outcome = CombatInputTickOutcome.NotObserved; slot.ReceiptCount = receiptCount;
                slot.AwaitingTick = awaitingTick;
                if (awaitingTick && kind == CombatInputBatchKind.Consumption) hasConsumption = true;
                Array.Copy(Receipts, slot.Receipts, receiptCount); count++;
            }
            public bool TryRead(out CombatInputBatch batch)
            {
                batch = null;
                while (count > 0 && owner == null && batches[start].AwaitingTick)
                { start = (start + 1) % batches.Length; count--; DroppedBatches++; }
                if (count == 0 || batches[start].AwaitingTick) return false;
                var slot = batches[start];
                batch = new CombatInputBatch(slot.Session, slot.Tick, slot.Now, slot.Kind, slot.Input, slot.Receipts, slot.ReceiptCount, slot.Outcome, slot.EndedNow);
                start = (start + 1) % batches.Length; count--;
                return true;
            }
            public void Dispose()
            {
                if (owner != null && owner.capture == this) owner.capture = null;
                owner = null;
            }
        }
    }
}
