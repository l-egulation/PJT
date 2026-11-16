using System;
using Deviant.Control.Contracts;
using Deviant.UI.Contracts;

namespace Deviant.UI.Presentation
{
    public enum HudDialog { None, Pause, Bag }

    // The shared router owns dialog focus/input/pause; this presenter owns only
    // its dialog handle and pointer/closing-edge guard, never the router itself.
    public sealed class HudControlPresenter : IDisposable
    {
        static readonly ScreenPolicy BagPolicy = new ScreenPolicy(0, InputChannels.Combat, false, true);
        static readonly ScreenPolicy PausePolicy = new ScreenPolicy(0, InputChannels.Combat, true, true);
        readonly IControlScope scope;
        readonly ScreenRouter router;
        readonly IScreenView view;
        IScreenHandle screen;
        IControlLease pointerInput;
        bool disposed, paused;
        HudDialog dialog;
        public bool Paused => !disposed && screen != null && screen.Status == ScreenStatus.Open && paused;
        public HudDialog Dialog => !disposed && screen != null && screen.Status == ScreenStatus.Open ? dialog : HudDialog.None;
        public Guid BindingId => !disposed && screen != null && screen.Status == ScreenStatus.Open ? screen.BindingId : Guid.Empty;

        public HudControlPresenter(IControlRequests controls, ScreenRouter router, IScreenView view)
        {
            this.router = router ?? throw new ArgumentNullException(nameof(router));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            scope = (controls ?? throw new ArgumentNullException(nameof(controls))).OpenScope("Battle HUD pointer");
        }

        public void TogglePause()
        {
            Check();
            if (Paused) CloseDialog();
            else Show(HudDialog.Pause, true);
        }

        public void OpenBag()
        {
            Check();
            Show(HudDialog.Bag, Paused);
        }

        void Show(HudDialog next, bool pause)
        {
            if (screen != null && screen.Status == ScreenStatus.Open && paused == pause)
            { dialog = next; return; }
            var previous = screen;
            screen = null;
            previous?.Dispose();
            if (!router.TryOpen("Battle HUD dialog", view, pause ? PausePolicy : BagPolicy, out var opened, out var error))
                throw new InvalidOperationException(error ?? "The HUD dialog could not acquire its screen binding.");
            screen = opened; paused = pause; dialog = next;
        }

        public void CloseDialog()
        {
            Check();
            var previous = screen;
            screen = null; paused = false; dialog = HudDialog.None;
            previous?.Dispose();
        }

        // A physical Back is already routed exactly once by ScreenInputBinding.
        // Never issue Back again with the focus produced by closing that screen.
        public void BackProcessed(ScreenBackResult result)
        {
            Check();
            if (!result.Consumed) TogglePause();
        }

        public void SetPointerBlocked(bool blocked)
        {
            Check();
            if (blocked && pointerInput == null) pointerInput = scope.BlockInput(InputChannels.Combat);
            else if (!blocked && pointerInput != null) { pointerInput.Dispose(); pointerInput = null; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var previous = screen;
            screen = null; paused = false; dialog = HudDialog.None;
            try { previous?.Dispose(); }
            finally { scope.Dispose(); }
        }

        void Check() { if (disposed) throw new ObjectDisposedException(nameof(HudControlPresenter)); }
    }
}
