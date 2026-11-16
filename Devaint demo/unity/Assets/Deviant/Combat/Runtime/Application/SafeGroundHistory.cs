using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // One actor's recovery history. Current invalidation can require any older
    // sample, so time-only pruning cannot discard the sole remaining safe pose.
    public sealed class SafeGroundHistory
    {
        readonly List<SafeGroundSample> samples = new List<SafeGroundSample>();
        public void Clear() => samples.Clear();
        public void Record(float time, float x, float y, int facing)
        {
            samples.Add(new SafeGroundSample(time, x, y, facing));
        }
        public bool TryBefore(float cutoff, out SafeGroundSample sample)
        {
            for (int i = samples.Count - 1; i >= 0; i--)
                if (samples[i].Time <= cutoff) { sample = samples[i]; return true; }
            sample = default; return false;
        }
        public bool TrySafeBefore(float cutoff, IActorRecoveryQuery query, out SafeGroundSample sample)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                var candidate = samples[i];
                if (candidate.Time <= cutoff && query.IsSafe(new ActorPose(candidate.X, candidate.Y, candidate.Facing)))
                { sample = candidate; return true; }
            }
            sample = default; return false;
        }
    }
}
