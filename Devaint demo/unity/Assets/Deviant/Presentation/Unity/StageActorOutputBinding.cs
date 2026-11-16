using System;
using Deviant.Presentation.Contracts;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    // A dedicated, nonphysical Sprite view only. Gameplay objects and scripted
    // hierarchies are rejected before any pose write, including cleanup writes.
    [DisallowMultipleComponent, DefaultExecutionOrder(220)]
    public sealed class StageActorOutputBinding : MonoBehaviour, IStageActorRequests
    {
        readonly StageActorSession session = new StageActorSession();
        SpriteRenderer target;
        Sprite content;
        StagePose fallback = new StagePose(default, 0, 1, false);
        BaseSource baseline;
        bool disposed, applying, closing, ending, wasAvailable, faulted;
        public IStageActorRequests Requests => this;
        public Guid SessionId => session.SessionId;
        public bool IsOwned => session.IsOwned;
        public bool IsAvailable => !disposed && this && isActiveAndEnabled && !faulted && Safe(target) && target.gameObject.activeInHierarchy && content && target.sprite == content;
        public string LastApplyError { get; private set; }
        public void Configure(SpriteRenderer view, StagePose initialBaseline)
        {
            Check(); ValidatePose(initialBaseline);
            if (!Safe(view)) throw new ArgumentException("A separate Sprite-only stage view with no physical or scripted hierarchy is required.", nameof(view));
            Close(false); Check(); baseline?.Dispose();
            target = view; content = view.sprite; fallback = initialBaseline; faulted = false; LastApplyError = null; wasAvailable = IsAvailable; ApplyNow();
        }
        public BaseSource BindBaseSource(string owner)
        {
            Check(); if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A stage baseline owner is required.", nameof(owner));
            if (baseline != null && baseline.IsActive) throw new InvalidOperationException("Stage baseline already has an owner.");
            return baseline = new BaseSource(this, fallback);
        }
        public IStageActorScope OpenScope(string owner)
        { Check(); if (!IsAvailable) throw new InvalidOperationException(LastApplyError ?? "Stage view is unavailable."); return session.OpenScope(owner); }
        void LateUpdate() { ApplyNow(); }
        void OnEnable() { if (!disposed && !applying) ApplyNow(); }
        public void ApplyNow()
        {
            Check();
            if (!IsAvailable) { if (wasAvailable) { wasAvailable = false; Close(false); } return; }
            wasAvailable = true; applying = true; LastApplyError = null;
            try { Write(session.Resolve(CurrentBaseline)); }
            catch (Exception exception) { faulted = true; LastApplyError = "Stage output failed: " + exception.Message; closing = true; }
            finally { applying = false; if (!disposed && !IsAvailable) { closing = true; ending |= !this; wasAvailable = false; } Flush(); }
        }
        StagePose CurrentBaseline => baseline != null && baseline.IsActive ? baseline.Pose : fallback;
        void Write(StagePose pose)
        {
            if (!Safe(target)) return;
            target.transform.position = new Vector3(pose.Position.X, pose.Position.Y, pose.Depth);
            target.flipX = pose.Facing < 0; target.enabled = pose.Visible;
        }
        static bool Safe(SpriteRenderer view)
        {
            if (!view || view.transform.childCount != 0) return false;
            foreach (var component in view.GetComponents<Component>()) if (!component || !(component is Transform) && !ReferenceEquals(component, view)) return false;
            for (var parent = view.transform.parent; parent; parent = parent.parent)
                foreach (var component in parent.GetComponents<Component>())
                    if (!component || component is MonoBehaviour || component is Collider2D || component is Rigidbody2D || component is Collider || component is Rigidbody || component is Animator || component is Animation) return false;
            return true;
        }
        static void ValidatePose(StagePose pose) { if (pose.Facing != -1 && pose.Facing != 1) throw new ArgumentException("A valid stage baseline is required."); }
        void Check()
        {
            if (disposed || !this) throw new ObjectDisposedException(nameof(StageActorOutputBinding));
            if (applying) throw new InvalidOperationException("Stage output mutations cannot reenter view application.");
        }
        void Close(bool destroy) { closing = true; ending |= destroy; wasAvailable = false; if (!applying) Flush(); }
        void Flush()
        {
            if (!closing) return; closing = false;
            if (ending) End(); else session.Reset();
            applying = true;
            try { Write(CurrentBaseline); }
            catch (Exception exception) { faulted = true; LastApplyError = "Stage baseline return failed: " + exception.Message; }
            finally { applying = false; if (ending && !disposed) End(); closing = false; }
        }
        void End() { if (disposed) return; disposed = true; baseline?.Dispose(); session.Dispose(); }
        void OnDisable() { if (!disposed) Close(false); }
        void OnDestroy() { if (!disposed) Close(true); }
        public sealed class BaseSource : IDisposable
        {
            readonly StageActorOutputBinding binding;
            bool closed;
            public bool IsActive => !closed && !binding.disposed;
            public StagePose Pose { get; private set; }
            internal BaseSource(StageActorOutputBinding binding, StagePose pose) { this.binding = binding; Pose = pose; }
            public void Submit(StagePose pose) { if (!IsActive) throw new ObjectDisposedException(nameof(BaseSource)); binding.Check(); ValidatePose(pose); Pose = pose; }
            public void Dispose() { if (closed) return; closed = true; if (ReferenceEquals(binding.baseline, this)) { binding.fallback = Pose; binding.baseline = null; } }
        }
    }
}
