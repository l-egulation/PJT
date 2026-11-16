using System;
using System.Collections.Generic;
using Deviant.Control.Contracts;
using Deviant.UI.Contracts;

namespace Deviant.UI.Presentation
{
    public sealed class ScreenPolicy
    {
        public int Priority { get; }
        public InputChannels BlockedInput { get; }
        public bool PauseSimulation { get; }
        public bool AllowBack { get; }
        public ScreenPolicy(int priority, InputChannels blockedInput, bool pauseSimulation, bool allowBack)
        {
            const InputChannels all = InputChannels.Combat | InputChannels.Interaction | InputChannels.Dialogue | InputChannels.Navigation;
            if ((blockedInput & ~all) != 0) throw new ArgumentOutOfRangeException(nameof(blockedInput));
            Priority = priority; BlockedInput = blockedInput; PauseSimulation = pauseSimulation; AllowBack = allowBack;
        }
    }
    // First router slice: focusable view ownership and generation-bound Back.
    // Registration, screen content, device mapping and product layers are supplied
    // by composition; this class never instantiates a view or selects a policy.
    public sealed class ScreenRouter : IDisposable
    {
        readonly IControlRequests controls;
        readonly List<Entry> entries = new List<Entry>();
        Entry[] snapshot = Array.Empty<Entry>();
        Entry focused;
        IScreenFocusReturn baselineFocus;
        public string LastFocusError { get; private set; }
        long sequence;
        bool busy, disposed, disposeRequested;
        public Guid FocusedBindingId => focused != null && focused.Status == ScreenStatus.Open && !focused.CloseRequested ? focused.BindingId : Guid.Empty;
        public int OpenCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Status == ScreenStatus.Open && !entries[i].CloseRequested) count++;
                return count;
            }
        }
        public ScreenRouter(IControlRequests controls) { this.controls = controls ?? throw new ArgumentNullException(nameof(controls)); }
        public bool TryOpen(string owner, IScreenView view, ScreenPolicy policy, out IScreenHandle handle, out string error)
        {
            handle = null; error = null;
            if (disposed || disposeRequested || busy) { error = "Screen router is closed or busy."; return false; }
            if (string.IsNullOrWhiteSpace(owner) || view == null || policy == null) { error = "A screen owner, view and explicit policy are required."; return false; }
            for (int i = 0; i < entries.Count; i++)
                if (ReferenceEquals(entries[i].View, view)) { error = "Screen view is already owned by this router."; return false; }
            if (sequence == long.MaxValue) { error = "Screen order exhausted."; return false; }
            // Capacity grows only when a screen opens, not during frame polling.
            if (snapshot.Length < entries.Count + 1) Array.Resize(ref snapshot, Math.Max(4, (entries.Count + 1) * 2));
            var entry = new Entry(this, view, policy, ++sequence); handle = entry; entries.Add(entry); busy = true;
            var reason = ScreenEndReason.BindingFailed;
            try
            {
                if (!view.IsAvailable) throw new InvalidOperationException("Screen view is unavailable.");
                if (entries.Count == 1)
                {
                    baselineFocus = view.CaptureFocusReturn() ?? throw new InvalidOperationException("Screen focus return is unavailable.");
                    LastFocusError = null;
                }
                entry.Subscribed = true; view.Input += entry.OnInput;
                if (!view.TryBind(entry.BindingId, out error)) throw new InvalidOperationException(error ?? "Screen view cannot bind.");
                if (disposeRequested || entry.CloseRequested)
                    Finish(entry, ScreenStatus.Closed, disposeRequested ? ScreenEndReason.OwnerDisposed : entry.CloseReason, null);
                else
                {
                    reason = ScreenEndReason.ControlFailed;
                    entry.Control = controls.OpenScope(owner + " " + entry.BindingId) ?? throw new InvalidOperationException("Screen control scope is missing.");
                    if (policy.BlockedInput != InputChannels.None) entry.Control.BlockInput(policy.BlockedInput);
                    if (policy.PauseSimulation) entry.Control.PauseSimulation();
                    if (!entry.Control.IsActive) throw new InvalidOperationException("Screen control owner is inactive.");
                }
            }
            catch (Exception exception) { Finish(entry, ScreenStatus.Failed, reason, exception.Message); }
            finally { busy = false; Settle(); }
            error = entry.Error;
            if (entry.Status != ScreenStatus.Open) { error = error ?? entry.EndReason.ToString(); return false; }
            return true;
        }
        // The caller captures FocusedBindingId once for one physical edge. It
        // must honor Consumed before forwarding that edge to HUD/game input.
        // Any old nonempty view token is consumed without affecting a new view.
        public ScreenBackResult Back(Guid expectedBinding)
        {
            if (busy) return new ScreenBackResult(expectedBinding != Guid.Empty || OpenCount > 0, false, "Screen router is busy.");
            if (disposed) return new ScreenBackResult(expectedBinding != Guid.Empty, false, "Screen router is closed.");
            Settle();
            if (!ReferenceEquals(focused, Top())) return new ScreenBackResult(true, false, "Screen focus transition is pending.");
            if (focused == null) return new ScreenBackResult(expectedBinding != Guid.Empty, false, null);
            if (expectedBinding == Guid.Empty || expectedBinding != focused.BindingId)
                return new ScreenBackResult(true, false, "Stale screen input.");
            if (controls.Current.Blocks(InputChannels.Navigation)) return new ScreenBackResult(true, false, "Navigation is restricted by an active owner.");
            if (!focused.Policy.AllowBack) return new ScreenBackResult(true, false, null);
            var entry = focused; RequestClose(entry, ScreenEndReason.Back);
            return new ScreenBackResult(true, entry.Status != ScreenStatus.Open, entry.Error ?? LastFocusError);
        }
        public void Poll()
        {
            if (busy) return;
            if (disposed) return;
            busy = true;
            int count = entries.Count;
            entries.CopyTo(snapshot);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var entry = snapshot[i];
                    try
                    {
                        if (!entry.View.IsAvailable) RequestClose(entry, ScreenEndReason.ViewClosed);
                        else if (!entry.Control.IsActive) Finish(entry, ScreenStatus.Failed, ScreenEndReason.ControlFailed, "Screen control owner is inactive.");
                    }
                    catch (Exception exception) { Finish(entry, ScreenStatus.Failed, ScreenEndReason.FocusFailed, exception.Message); }
                }
            }
            finally { Array.Clear(snapshot, 0, count); busy = false; Settle(); }
        }
        Entry Top()
        {
            Entry top = null;
            for (int i = 0; i < entries.Count; i++)
            {
                var candidate = entries[i];
                if (candidate.Status != ScreenStatus.Open || candidate.CloseRequested) continue;
                if (top == null || candidate.Policy.Priority > top.Policy.Priority ||
                    candidate.Policy.Priority == top.Policy.Priority && candidate.Sequence > top.Sequence) top = candidate;
            }
            return top;
        }
        bool HasPendingClose()
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].CloseRequested) return true;
            return false;
        }
        void FinishSnapshot(bool all)
        {
            int count = 0;
            for (int i = 0; i < entries.Count; i++)
                if (all || entries[i].CloseRequested) snapshot[count++] = entries[i];
            if (all) Array.Sort(snapshot, 0, count, EntryOrder.Instance);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var entry = snapshot[i];
                    Finish(entry, ScreenStatus.Closed, all ? ScreenEndReason.OwnerDisposed : entry.CloseReason, null);
                }
            }
            finally { Array.Clear(snapshot, 0, count); }
        }
        sealed class EntryOrder : IComparer<Entry>
        {
            public static readonly EntryOrder Instance = new EntryOrder();
            public int Compare(Entry first, Entry second)
            {
                int priority = second.Policy.Priority.CompareTo(first.Policy.Priority);
                return priority != 0 ? priority : second.Sequence.CompareTo(first.Sequence);
            }
        }
        void RequestClose(Entry entry, ScreenEndReason reason)
        {
            if (entry.Status != ScreenStatus.Open) return;
            entry.CloseRequested = true; entry.CloseReason = reason;
            if (!busy) Settle();
        }
        void Settle()
        {
            if (busy) return; busy = true;
            try
            {
                while (true)
                {
                    if (disposeRequested)
                    {
                        disposed = true;
                        FinishSnapshot(true);
                        focused = null; RestoreBaseline(); return;
                    }
                    FinishSnapshot(false);
                    var next = Top();
                    if (ReferenceEquals(next, focused)) { RestoreBaseline(); return; }
                    if (next != null)
                    {
                        try { if (!next.View.IsFocusReady) return; }
                        catch (Exception exception) { Finish(next, ScreenStatus.Failed, ScreenEndReason.FocusFailed, exception.Message); continue; }
                    }
                    if (focused != null)
                    {
                        try { if (!focused.View.IsFocusReady) return; }
                        catch (Exception exception) { Finish(focused, ScreenStatus.Failed, ScreenEndReason.FocusFailed, exception.Message); continue; }
                    }
                    var previous = focused; focused = null;
                    if (previous != null && previous.Status == ScreenStatus.Open)
                    {
                        try { if (!previous.View.TrySetFocus(previous.BindingId, false)) Finish(previous, ScreenStatus.Failed, ScreenEndReason.FocusFailed, "Screen could not release focus."); }
                        catch (Exception exception) { Finish(previous, ScreenStatus.Failed, ScreenEndReason.FocusFailed, exception.Message); }
                    }
                    next = Top();
                    if (next != null)
                    {
                        focused = next;
                        try { if (!next.View.TrySetFocus(next.BindingId, true)) Finish(next, ScreenStatus.Failed, ScreenEndReason.FocusFailed, "Screen could not acquire focus."); }
                        catch (Exception exception) { Finish(next, ScreenStatus.Failed, ScreenEndReason.FocusFailed, exception.Message); }
                    }
                    if (!disposeRequested && !HasPendingClose() && ReferenceEquals(focused, Top())) { RestoreBaseline(); return; }
                }
            }
            finally { busy = false; }
        }
        void RestoreBaseline()
        {
            if (entries.Count != 0) return;
            var previous = baselineFocus; baselineFocus = null;
            try { previous?.Restore(); }
            catch (Exception exception) { LastFocusError = "Screen focus return failed: " + exception.Message; }
        }
        void Finish(Entry entry, ScreenStatus status, ScreenEndReason reason, string error)
        {
            if (entry.Status != ScreenStatus.Open) return;
            entry.Status = status; entry.EndReason = reason; entry.Error = error; entries.Remove(entry);
            if (ReferenceEquals(focused, entry)) focused = null;
            string cleanup = null;
            if (entry.Subscribed)
            {
                entry.Subscribed = false;
                try { entry.View.Input -= entry.OnInput; } catch (Exception exception) { cleanup = exception.Message; }
            }
            try { entry.View.Unbind(entry.BindingId); } catch (Exception exception) { cleanup = Join(cleanup, exception.Message); }
            var control = entry.Control; entry.Control = null;
            try { control?.Dispose(); } catch (Exception exception) { cleanup = Join(cleanup, exception.Message); }
            if (cleanup != null) { entry.Status = ScreenStatus.Failed; entry.EndReason = ScreenEndReason.CleanupFailed; entry.Error = Join(error, cleanup); }
        }
        static string Join(string first, string second) => first == null ? second : first + "; " + second;
        public void Dispose()
        {
            if (disposed) return; disposeRequested = true;
            if (!busy) Settle();
        }
        sealed class Entry : IScreenHandle
        {
            readonly ScreenRouter router;
            public readonly IScreenView View;
            public readonly ScreenPolicy Policy;
            public readonly long Sequence;
            public IControlScope Control;
            public bool Subscribed, CloseRequested;
            public ScreenEndReason CloseReason;
            public Guid BindingId { get; } = Guid.NewGuid();
            public ScreenStatus Status { get; internal set; } = ScreenStatus.Open;
            public ScreenEndReason EndReason { get; internal set; }
            public string Error { get; internal set; }
            public bool IsFocused => Status == ScreenStatus.Open && !CloseRequested && ReferenceEquals(router.focused, this);
            public Entry(ScreenRouter router, IScreenView view, ScreenPolicy policy, long sequence) { this.router = router; View = view; Policy = policy; Sequence = sequence; }
            public void OnInput(ScreenIntent input)
            {
                if (input.BindingId != BindingId || Status != ScreenStatus.Open) return;
                if (input.Kind == ScreenIntentKind.ViewClosed) router.RequestClose(this, ScreenEndReason.ViewClosed);
                else if (input.Kind == ScreenIntentKind.Back) router.Back(input.BindingId);
            }
            public void Dispose() => router.RequestClose(this, ScreenEndReason.Requested);
        }
    }
}
