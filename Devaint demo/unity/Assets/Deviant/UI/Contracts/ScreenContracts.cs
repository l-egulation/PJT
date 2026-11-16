using System;

namespace Deviant.UI.Contracts
{
    public enum ScreenIntentKind { Back, ViewClosed }
    public readonly struct ScreenIntent
    {
        public Guid BindingId { get; }
        public ScreenIntentKind Kind { get; }
        public ScreenIntent(Guid bindingId, ScreenIntentKind kind) { BindingId = bindingId; Kind = kind; }
    }
    public enum ScreenStatus { Open, Closed, Failed }
    public enum ScreenEndReason { None, Requested, Back, ViewClosed, OwnerDisposed, BindingFailed, ControlFailed, FocusFailed, CleanupFailed }
    public interface IScreenFocusReturn { void Restore(); }
    public interface IScreenView
    {
        bool IsAvailable { get; }
        bool IsFocusReady { get; }
        IScreenFocusReturn CaptureFocusReturn();
        event Action<ScreenIntent> Input;
        bool TryBind(Guid bindingId, out string error);
        bool TrySetFocus(Guid bindingId, bool focused);
        // Unbind only this generation and return this view's display/focus state.
        // A throwing bind releases partial state; unbind must be idempotent.
        void Unbind(Guid bindingId);
    }
    public interface IScreenHandle : IDisposable
    {
        Guid BindingId { get; }
        ScreenStatus Status { get; }
        ScreenEndReason EndReason { get; }
        string Error { get; }
        bool IsFocused { get; }
    }
    public readonly struct ScreenBackResult
    {
        public bool Consumed { get; }
        public bool Closed { get; }
        public string Error { get; }
        public ScreenBackResult(bool consumed, bool closed, string error)
        { Consumed = consumed; Closed = closed; Error = error; }
    }
}
