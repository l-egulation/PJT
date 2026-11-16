using System;
using Deviant.Definitions;

namespace Deviant.Presentation
{
    // Caller-owned presentation state, not a trigger collider or gameplay zone.
    // Sample only from the selected clock's completed simulation steps.
    public sealed class LightingZoneTracker : IDisposable
    {
        readonly LightingDirector director;
        readonly LightingProfile profile;
        LightingDirector.ProfileLease lease;
        bool disposed;
        public LightingZoneDefinition Definition { get; }
        public bool IsActive => lease != null && lease.IsActive;
        public float Weight => IsActive ? lease.Weight : 0;
        public int EntryCount { get; private set; }
        public LightingZoneTracker(LightingDirector director, LightingZoneDefinition definition, LightingProfile profile)
        {
            this.director = director ?? throw new ArgumentNullException(nameof(director));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (profile.Id != definition.ProfileId) throw new ArgumentException("The zone profile binding does not match its definition.");
        }
        public void Sample(float x, float y)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LightingZoneTracker));
            LightingValidation.Finite(x, nameof(x)); LightingValidation.Finite(y, nameof(y));
            double distance = Math.Min(Math.Min((double)x - Definition.Left, (double)Definition.Right - x),
                Math.Min((double)y - Definition.Bottom, (double)Definition.Top - y));
            if (lease != null && distance <= -Definition.ExitHysteresis) { lease.Dispose(); lease = null; return; }
            float t = (float)Math.Max(0, Math.Min(1, distance / Definition.BlendWidth));
            float weight = t * t * (3 - 2 * t);
            if (lease == null)
            {
                if (distance <= 0) return;
                lease = director.RequestProfile("lighting zone / " + Definition.Id, profile, Definition.Priority, Definition.TransitionSeconds, weight);
                EntryCount++;
            }
            else lease.SetWeight(weight);
        }
        public void Reset()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LightingZoneTracker));
            lease?.Dispose(); lease = null; EntryCount = 0;
        }
        public void Dispose()
        {
            if (disposed) return;
            lease?.Dispose(); lease = null; disposed = true;
        }
    }
}
