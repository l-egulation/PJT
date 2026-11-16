using System;
using Deviant.Presentation.Contracts;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    // The sole Transform writer for a bound camera. Follow controllers supply a
    // current baseline; higher-priority framing never rewrites their state.
    [DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(200)]
    public sealed class CameraOutputBinding : MonoBehaviour
    {
        Camera target;
        CameraDirector director;
        CameraPosition fallback;
        BaseSource baseline;
        AdditiveOffset additiveOffset;
        bool disposed, applying, initialized, closePending;
        public ICameraRequests Requests => Session();
        public CameraSelection LastSelection => director?.LastSelection ?? default;
        public string LastApplyError { get; private set; }
        readonly System.Collections.Generic.List<KickSource> kickSources = new System.Collections.Generic.List<KickSource>();
        public KickSource BindKickSource(CameraKickMixer mixer, int internalHeight = 540)
        {
            Ensure(); if(mixer==null || internalHeight<=0)throw new ArgumentException("Kick mixer and internal output height required.");
            var source=new KickSource(this,mixer,internalHeight);kickSources.Add(source);return source;
        }
        void Awake() { Ensure(); }
        public static CameraOutputBinding GetOrCreate(Camera camera)
        {
            if (!camera) throw new ArgumentNullException(nameof(camera));
            var binding = camera.GetComponent<CameraOutputBinding>();
            return binding ? binding : camera.gameObject.AddComponent<CameraOutputBinding>();
        }
        public BaseSource BindBaseSource(string owner, Action afterApply = null)
        {
            Ensure();
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A baseline owner is required.", nameof(owner));
            if (baseline != null && baseline.IsActive) throw new InvalidOperationException("Camera baseline already owned by " + baseline.Owner + ".");
            return baseline = new BaseSource(this, owner, fallback, afterApply);
        }
        // A bounded test effect adds to the current baseline without replacing
        // follow state. Explicit framing requests suppress this offset.
        public AdditiveOffset BindAdditiveOffset(string owner)
        {
            Ensure();
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("An offset owner is required.", nameof(owner));
            if (additiveOffset != null && additiveOffset.IsActive) throw new InvalidOperationException("Camera additive offset already owned.");
            return additiveOffset = new AdditiveOffset(this);
        }
        void LateUpdate() { ApplyNow(); }
        public void ApplyNow()
        {
            Ensure();
            if (applying) throw new InvalidOperationException("Camera output cannot reenter source resolution.");
            if (!Available) { CloseRequests(); return; }
            var current = Session();
            applying = true; LastApplyError = null;
            try
            {
                var selected = current.Resolve(baseline != null && baseline.IsActive ? baseline.Position : fallback);
                // A source may disable/re-enable or destroy the output. That
                // session's frame and observer cannot cross the lifetime change.
                if (closePending || !Available) return;
                var position = selected.Position;
                var offset = !selected.RequestSequence.HasValue && additiveOffset != null && additiveOffset.IsActive
                    ? additiveOffset.Value : Vector3.zero;
                foreach(var source in kickSources)if(source.IsActive)
                {
                    var pixels=source.mixer.ReadPixels();float unit=2*target.orthographicSize/source.height;
                    offset+=new Vector3(pixels.X*unit,pixels.Y*unit,0);
                }
                target.transform.position = new Vector3(position.X, position.Y, position.Z) + offset;
                try { baseline?.afterApply?.Invoke(); }
                catch (Exception exception) { LastApplyError = "Camera presentation observer failed: " + exception.Message; }
            }
            finally
            {
                applying = false;
                if (closePending || !Available) CloseRequests();
            }
        }
        bool Available => !disposed && this && isActiveAndEnabled && target && target.isActiveAndEnabled;
        CameraDirector Session()
        {
            Ensure();
            if (closePending || !Available)
            {
                CloseRequests();
                throw new InvalidOperationException("Camera output is unavailable; obtain requests from its current active session.");
            }
            return director ?? (director = new CameraDirector());
        }
        void Ensure()
        {
            if (disposed || !this) throw new ObjectDisposedException(nameof(CameraOutputBinding));
            if (initialized) return;
            target = GetComponent<Camera>();
            var at = target.transform.position; fallback = new CameraPosition(at.x, at.y, at.z);
            initialized = true;
        }
        void CloseRequests()
        {
            if (applying) { closePending = true; return; }
            closePending = false;
            var previous = director; director = null; previous?.Dispose();
            foreach(var source in kickSources)source.closed=true;
            kickSources.Clear();
        }
        void OnDisable() { CloseRequests(); }
        void OnDestroy()
        {
            disposed = true; baseline?.Dispose(); additiveOffset?.Dispose(); CloseRequests();
        }
        public sealed class AdditiveOffset : IDisposable
        {
            readonly CameraOutputBinding binding;
            bool closed;
            internal Vector3 Value { get; private set; }
            public bool IsActive => !closed && binding && !binding.disposed;
            internal AdditiveOffset(CameraOutputBinding binding) { this.binding = binding; }
            public void Submit(Vector3 value)
            {
                if (!IsActive) throw new ObjectDisposedException(nameof(AdditiveOffset));
                if (float.IsNaN(value.x) || float.IsInfinity(value.x) || float.IsNaN(value.y) ||
                    float.IsInfinity(value.y) || value.z != 0 || value.sqrMagnitude > 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "A finite 2D offset of at most one world unit is required.");
                Value = value;
            }
            public void Dispose()
            {
                if (closed) return;
                closed = true; Value = Vector3.zero;
                if (ReferenceEquals(binding.additiveOffset, this)) binding.additiveOffset = null;
            }
        }
        public sealed class BaseSource : IDisposable
        {
            readonly CameraOutputBinding binding;
            bool closed;
            public string Owner { get; }
            public bool IsActive => !closed && binding && !binding.disposed;
            public CameraPosition Position { get; private set; }
            internal readonly Action afterApply;
            internal BaseSource(CameraOutputBinding binding, string owner, CameraPosition initial, Action afterApply)
            { this.binding = binding; Owner = owner; Position = initial; this.afterApply = afterApply; }
            public void Submit(CameraPosition position)
            {
                if (!IsActive) throw new ObjectDisposedException(nameof(BaseSource));
                Position = position;
            }
            public void Dispose()
            {
                if (closed) return; closed = true;
                if (ReferenceEquals(binding.baseline, this)) { binding.fallback = Position; binding.baseline = null; }
            }
        }
        public sealed class KickSource : IDisposable
        {
            readonly CameraOutputBinding binding;
            internal readonly CameraKickMixer mixer;
            internal readonly int height;
            internal bool closed;
            public bool IsActive=>!closed && binding && binding.Available;
            internal KickSource(CameraOutputBinding binding,CameraKickMixer mixer,int height)
            {this.binding=binding;this.mixer=mixer;this.height=height;}
            public void Dispose(){if(closed)return;closed=true;if(binding)binding.kickSources.Remove(this);}
        }
    }
}
