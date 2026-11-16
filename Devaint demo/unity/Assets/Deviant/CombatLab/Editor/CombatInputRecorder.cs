using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab.Contracts;

namespace Deviant.CombatLab
{
    // Fixed-scenario callers append only after a shared tick returns. Live Editor
    // capture instead polls the same ingress's bounded completed boundaries.
    public sealed class CombatInputRecorder : IDisposable
    {
        readonly string scenarioId;
        readonly float stepSeconds;
        readonly int capacity;
        readonly List<CombatInput> inputs = new List<CombatInput>();
        CombatInputRecording completed;
        readonly ICombatInputCapture capture;
        readonly Queue<CombatInputBatch> liveBatches;
        public IReadOnlyList<CombatInputBatch> LiveBatches => liveBatches == null ? (IReadOnlyList<CombatInputBatch>)Array.Empty<CombatInputBatch>() : Array.AsReadOnly(liveBatches.ToArray());
        public bool IsCapturing => capture != null && capture.IsActive;
        public long DroppedLiveBatches { get; private set; }
        public int Count => liveBatches?.Count ?? inputs.Count;
        public bool CanRecord => capture == null && completed == null && Count < capacity;
        public bool IsCompleted => completed != null;
        public CombatInputRecorder(string scenarioId, float stepSeconds, int capacity)
        {
            this.scenarioId = CombatInputRecording.ValidateScenario(scenarioId);
            this.stepSeconds = CombatInputRecording.ValidateStep(stepSeconds);
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }
        // The Editor polls immutable completed boundaries; gameplay invokes no
        // user callback and this recorder cannot change pending input or actions.
        public CombatInputRecorder(CombatPlayer player, int capacity)
        {
            if (!player) throw new ArgumentNullException(nameof(player));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            liveBatches = new Queue<CombatInputBatch>(capacity);
            capture = player.BeginInputCapture(capacity);
        }
        public void Poll()
        {
            if (capture == null) throw new InvalidOperationException("This recorder records explicit scenario tick intents, not live input.");
            while (capture.TryRead(out var batch))
            {
                if (liveBatches.Count == capacity) { liveBatches.Dequeue(); DroppedLiveBatches++; }
                liveBatches.Enqueue(batch);
            }
        }
        public long LostLiveBatches => DroppedLiveBatches + (capture?.DroppedBatches ?? 0);
        public void Dispose() => capture?.Dispose();
        public void Record(CombatInput input)
        {
            if (capture != null) throw new InvalidOperationException("Poll live input boundaries instead of appending caller-provided intents.");
            if (completed != null) throw new InvalidOperationException("The input recording is already completed.");
            if (inputs.Count == capacity) throw new InvalidOperationException("The input recording capacity was reached.");
            inputs.Add(input);
        }
        public CombatInputRecording Complete()
        {
            if (capture != null) throw new InvalidOperationException("Live ingress observation is not a fixed-scenario replay file.");
            if (completed != null) return completed;
            if (inputs.Count == 0) throw new InvalidOperationException("No successfully executed tick intents were recorded.");
            return completed = new CombatInputRecording(scenarioId, stepSeconds, inputs.ToArray());
        }
    }
}
