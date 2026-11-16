using System;
using Deviant.Presentation.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Presentation.Unity
{
    // Composition supplies a non-raycasting Image and baseline. This component
    // writes color only; it never creates a Canvas or changes input/time/scenes.
    [DisallowMultipleComponent, DefaultExecutionOrder(210)]
    public sealed class FadeOutputBinding : MonoBehaviour, IFadeRequests
    {
        readonly FadeDirector director = new FadeDirector();
        Image target;
        FadeFrame fallback;
        BaseSource baseline;
        bool disposed, applying, closeRequested, destroyRequested, faulted, wasAvailable;
        public IFadeRequests Requests => this;
        public Guid SessionId => director.SessionId;
        public bool IsAvailable => !disposed && this && isActiveAndEnabled && !faulted && target && target.isActiveAndEnabled && !target.raycastTarget && target.canvas && target.canvas.isActiveAndEnabled;
        public int RequestCount => director.RequestCount;
        public FadeSelection LastSelection => director.LastSelection;
        public string LastApplyError { get; private set; }
        public void Configure(Image image, FadeFrame initialBaseline)
        {
            CheckMutation();
            if (!image) throw new ArgumentNullException(nameof(image));
            if (image.raycastTarget) throw new ArgumentException("A fade output Image must not consume UI raycasts.", nameof(image));
            Close(false); CheckMutation(); baseline?.Dispose();
            target = image; fallback = initialBaseline; faulted = false; LastApplyError = null; wasAvailable = IsAvailable;
            ApplyNow();
        }
        public BaseSource BindBaseSource(string owner)
        {
            CheckMutation();
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A baseline owner is required.", nameof(owner));
            if (baseline != null && baseline.IsActive) throw new InvalidOperationException("Fade baseline is already owned by " + baseline.Owner + ".");
            return baseline = new BaseSource(this, owner, fallback);
        }
        public IFadeScope OpenScope(string owner)
        {
            CheckMutation();
            if (!IsAvailable) throw new InvalidOperationException(LastApplyError ?? "Fade output is unavailable.");
            return director.OpenScope(owner);
        }
        void LateUpdate() { ApplyNow(); }
        // Re-enabling inside an Image callback must not reenter the current
        // application. Its pending close is flushed first; LateUpdate resumes.
        void OnEnable() { if (!disposed && !applying) ApplyNow(); }
        public void ApplyNow()
        {
            CheckMutation();
            if (!IsAvailable)
            {
                if (wasAvailable) { wasAvailable = false; Close(false); }
                return;
            }
            wasAvailable = true; applying = true; LastApplyError = null;
            try { Write(director.Resolve(CurrentBaseline).Frame); }
            catch (Exception exception) { LastApplyError = "Fade output failed: " + exception.Message; faulted = true; closeRequested = true; }
            finally
            {
                applying = false;
                // A Graphic callback can remove/disable this output without an
                // immediate MonoBehaviour lifecycle callback in editor usage.
                // Observe actual availability again before returning the frame.
                if (!disposed && !IsAvailable)
                {
                    closeRequested = true; destroyRequested |= !this; wasAvailable = false;
                }
                FlushClose();
            }
        }
        FadeFrame CurrentBaseline => baseline != null && baseline.IsActive ? baseline.Frame : fallback;
        void Write(FadeFrame frame) { if (target) target.color = new Color(frame.Red, frame.Green, frame.Blue, frame.Opacity); }
        void CheckMutation()
        {
            if (disposed || !this) throw new ObjectDisposedException(nameof(FadeOutputBinding));
            if (applying) throw new InvalidOperationException("Fade output mutations cannot reenter Image callbacks.");
        }
        void Close(bool destroy)
        {
            closeRequested = true; destroyRequested |= destroy; wasAvailable = false;
            if (!applying) FlushClose();
        }
        void FlushClose()
        {
            if (!closeRequested) return; closeRequested = false;
            if (destroyRequested) EndDirector(); else director.Reset();
            applying = true;
            try { Write(CurrentBaseline); }
            catch (Exception exception)
            {
                faulted = true; LastApplyError = (LastApplyError == null ? "" : LastApplyError + "; ") + "Fade baseline return failed: " + exception.Message;
            }
            finally
            {
                applying = false;
                // A destroy raised by a baseline Image callback also closes the
                // service. Other close requests are already satisfied by reset.
                if (destroyRequested && !disposed) EndDirector();
                closeRequested = false;
            }
        }
        void EndDirector()
        {
            if (disposed) return; disposed = true;
            baseline?.Dispose(); director.Dispose();
        }
        void OnDisable() { if (!disposed) Close(false); }
        void OnDestroy() { if (!disposed) Close(true); }
        public sealed class BaseSource : IDisposable
        {
            readonly FadeOutputBinding binding;
            bool closed;
            public string Owner { get; }
            public bool IsActive => !closed && !binding.disposed;
            public FadeFrame Frame { get; private set; }
            internal BaseSource(FadeOutputBinding binding, string owner, FadeFrame initial)
            { this.binding = binding; Owner = owner; Frame = initial; }
            public void Submit(FadeFrame frame)
            {
                if (!IsActive) throw new ObjectDisposedException(nameof(BaseSource));
                binding.CheckMutation(); Frame = frame;
            }
            public void Dispose()
            {
                if (closed) return; closed = true;
                if (ReferenceEquals(binding.baseline, this)) { binding.fallback = Frame; binding.baseline = null; }
            }
        }
    }
}
