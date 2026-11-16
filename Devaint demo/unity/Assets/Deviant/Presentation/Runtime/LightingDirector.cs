using System;
using System.Collections.Generic;
using Deviant.Definitions;

namespace Deviant.Presentation
{
    public readonly struct LightingState
    {
        public readonly LightColor Color;
        public readonly float Intensity;
        public LightingState(LightColor color, float intensity) { Color = color; Intensity = intensity; }
        public LightingState(LightingProfile profile) : this(profile.Color, profile.Intensity) { }
        internal static LightingState Lerp(LightingState a, LightingState b, float t)
            => t == 0 ? a : t == 1 ? b : new LightingState(LightColor.Lerp(a.Color, b.Color, t), a.Intensity + (b.Intensity - a.Intensity) * t);
    }
    // One owner per request. Releasing a request resolves the remaining current requests,
    // never restores an old color captured by an unrelated cutscene.
    public sealed class LightingDirector : IDisposable
    {
        public sealed class ProfileLease : IDisposable
        {
            internal LightingDirector Director;
            internal int Priority;
            internal LightingProfile Profile;
            internal float Transition;
            internal float weight;
            public float Weight => weight;
            public bool IsActive => Director != null;
            public void SetWeight(float value)
            {
                var owner = Director ?? throw new ObjectDisposedException(nameof(ProfileLease));
                LightingValidation.Unit(value, nameof(value));
                if (weight == value) return;
                weight = value; owner.Resolve(Transition);
            }
            public void Dispose() { var owner = Director; Director = null; owner?.Release(this); }
        }
        readonly List<ProfileLease> requests = new List<ProfileLease>();
        readonly LightingProfile baseline;
        float elapsed, duration;
        bool disposed;
        LightingState start, target;
        public LightingState Current { get; private set; }
        public int RequestCount => requests.Count;
        public LightingDirector(LightingProfile baseline)
        { this.baseline = baseline ?? throw new ArgumentNullException(nameof(baseline)); Current = start = target = new LightingState(baseline); }
        public ProfileLease RequestProfile(string owner, LightingProfile profile, int priority, float transition, float weight = 1)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LightingDirector));
            LightingValidation.Id(owner); LightingValidation.Nonnegative(transition, nameof(transition));
            LightingValidation.Unit(weight, nameof(weight));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var request = new ProfileLease { Director = this, Priority = priority, Profile = profile, Transition = transition, weight = weight };
            int index = requests.Count;
            while (index > 0 && requests[index - 1].Priority > priority) index--;
            requests.Insert(index, request); Resolve(transition); return request;
        }
        void Release(ProfileLease request) { if (requests.Remove(request) && !disposed) Resolve(request.Transition); }
        void Resolve(float transition)
        {
            var next = new LightingState(baseline);
            // Increasing priority, then original registration order. Weight changes
            // never acquire a new tie-break position or replace another owner's lease.
            foreach (var request in requests) next = LightingState.Lerp(next, new LightingState(request.Profile), request.weight);
            if (next.Color.R == target.Color.R && next.Color.G == target.Color.G && next.Color.B == target.Color.B && next.Intensity == target.Intensity) return;
            start = Current; target = next; duration = transition; elapsed = 0;
            if (duration == 0) Current = target;
        }
        public void Advance(float delta)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LightingDirector));
            LightingValidation.Nonnegative(delta, nameof(delta));
            if (duration == 0 || delta == 0) return;
            elapsed = Math.Min(duration, elapsed + delta); Current = LightingState.Lerp(start, target, elapsed / duration);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; foreach (var request in requests) request.Director = null;
            requests.Clear(); Current = new LightingState(baseline);
        }
    }
}
