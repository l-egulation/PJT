using System;
using System.Linq;
using Deviant.Control.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using NUnit.Framework;

namespace Deviant.Control.Tests
{
    public class ScreenRouterTests
    {
        sealed class View : IScreenView
        {
            Action<ScreenIntent> input;
            public bool Available = true, RejectBind, ThrowBind, RejectFocus, ThrowUnbind, ThrowUnsubscribe, ThrowRestore;
            public int Captures, Restores;
            public bool FocusReady = true;
            public Guid Bound;
            public bool Focused;
            public int Binds, Unbinds;
            public Action OnBind, OnFocus, OnUnbind;
            public Action OnAvailable;
            public bool IsAvailable { get { OnAvailable?.Invoke(); return Available; } }
            public bool IsFocusReady => Available && FocusReady;
            public IScreenFocusReturn CaptureFocusReturn() { Captures++; return new FocusReturn(this); }
            sealed class FocusReturn : IScreenFocusReturn
            {
                readonly View owner;
                public FocusReturn(View owner) { this.owner = owner; }
                public void Restore() { owner.Restores++; if (owner.ThrowRestore) throw new InvalidOperationException("focus return failed"); }
            }
            public event Action<ScreenIntent> Input { add { input += value; } remove { input -= value; if (ThrowUnsubscribe) throw new InvalidOperationException("unsubscribe failed"); } }
            public bool TryBind(Guid bindingId, out string error)
            {
                Binds++; error = RejectBind ? "bind rejected" : null;
                if (ThrowBind) throw new InvalidOperationException("bind failed");
                if (RejectBind) return false;
                Bound = bindingId; OnBind?.Invoke(); return true;
            }
            public bool TrySetFocus(Guid bindingId, bool focused)
            {
                if (bindingId != Bound || RejectFocus) return false;
                Focused = focused; if (focused) OnFocus?.Invoke(); return true;
            }
            public void Unbind(Guid bindingId)
            {
                if (bindingId != Bound) return;
                Bound = Guid.Empty; Focused = false; Unbinds++; OnUnbind?.Invoke();
                if (ThrowUnbind) throw new InvalidOperationException("unbind failed");
            }
            public void Emit(Guid bindingId, ScreenIntentKind kind) => input?.Invoke(new ScreenIntent(bindingId, kind));
        }
        sealed class FailingControls : IControlRequests
        {
            readonly ControlSession inner;
            public bool ThrowBlock, ThrowDispose;
            public FailingControls(ControlSession inner) { this.inner = inner; }
            public ControlState Current => inner.Current;
            public IControlScope OpenScope(string owner) => new Scope(this, inner.OpenScope(owner));
            sealed class Scope : IControlScope
            {
                readonly FailingControls owner; readonly IControlScope inner;
                public Scope(FailingControls owner, IControlScope inner) { this.owner = owner; this.inner = inner; }
                public bool IsActive => inner.IsActive;
                public IControlLease BlockInput(InputChannels channels) { var lease = inner.BlockInput(channels); if (owner.ThrowBlock) throw new InvalidOperationException("block failed"); return lease; }
                public IControlLease PauseSimulation() => inner.PauseSimulation();
                public void Dispose() { inner.Dispose(); if (owner.ThrowDispose) throw new InvalidOperationException("control cleanup failed"); }
            }
        }
        static ScreenPolicy Policy(int priority = 0, bool pause = false, bool back = true) => new ScreenPolicy(priority, InputChannels.Combat, pause, back);
        static IScreenHandle Open(ScreenRouter router, View view, ScreenPolicy policy = null)
        {
            Assert.That(router.TryOpen("fixture screen", view, policy ?? Policy(), out var handle, out var error), Is.True, error); return handle;
        }
        [Test] public void RouterAndViewContractHaveNoEngineImplementationDependencies()
        {
            foreach (var type in new[] { typeof(ScreenRouter), typeof(IScreenView) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor")), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenPolicy(0, (InputChannels)128, false, true));
        }
        [Test] public void NestedCloseRestoresPreviousFocusAndReleasesOnlyItsOwnControl()
        {
            using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture external")) using (var router = new ScreenRouter(controls))
            {
                var other = external.BlockInput(InputChannels.Interaction); var pause = external.PauseSimulation();
                var lower = new View(); var a = Open(router, lower); var upper = new View(); var b = Open(router, upper, Policy(pause: true));
                Assert.That(b.IsFocused && upper.Focused, Is.True); Assert.That(a.IsFocused || lower.Focused, Is.False);
                var result = router.Back(b.BindingId); Assert.That(result.Consumed && result.Closed, Is.True); Assert.That(b.EndReason, Is.EqualTo(ScreenEndReason.Back));
                Assert.That(a.IsFocused && lower.Focused, Is.True); Assert.That(other.IsActive && pause.IsActive, Is.True); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Combat | InputChannels.Interaction));
                Assert.That(router.Back(b.BindingId).Consumed, Is.True); Assert.That(a.Status, Is.EqualTo(ScreenStatus.Open));
                Assert.That(router.Back(a.BindingId).Closed, Is.True); Assert.That(router.Back(a.BindingId).Consumed, Is.True); Assert.That(router.Back(Guid.Empty).Consumed, Is.False);
                Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Interaction)); Assert.That(controls.Current.SimulationPaused, Is.True); Assert.That(router.OpenCount, Is.Zero);
            }
        }
        [Test] public void PriorityAndLatestOrderSelectFocusAndClosingAHiddenScreenKeepsTheCurrentOwner()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var first = Open(router, new View(), Policy(10)); var hidden = Open(router, new View(), Policy(1)); var latest = Open(router, new View(), Policy(10));
                Assert.That(latest.IsFocused, Is.True); hidden.Dispose(); Assert.That(latest.IsFocused, Is.True);
                latest.Dispose(); Assert.That(first.IsFocused, Is.True); Assert.That(router.OpenCount, Is.EqualTo(1));
            }
        }
        [Test] public void BackPolicyAndNavigationRestrictionConsumeWithoutClosingTheScreen()
        {
            using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture external")) using (var router = new ScreenRouter(controls))
            {
                var handle = Open(router, new View(), Policy(back: false)); var result = router.Back(handle.BindingId);
                Assert.That(result.Consumed, Is.True); Assert.That(result.Closed, Is.False); handle.Dispose();
                var next = Open(router, new View()); var restriction = external.BlockInput(InputChannels.Navigation);
                Assert.That(router.Back(next.BindingId).Closed, Is.False); Assert.That(next.Status, Is.EqualTo(ScreenStatus.Open));
                restriction.Dispose(); Assert.That(router.Back(next.BindingId).Closed, Is.True);
            }
        }
        [TestCase(false)] [TestCase(true)] public void BindFailurePreservesExistingScreenAndDoesNotAcquireNewControl(bool exception)
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var first = Open(router, new View()); long revision = controls.Current.Revision;
                var broken = new View { RejectBind = !exception, ThrowBind = exception };
                Assert.That(router.TryOpen("broken", broken, Policy(pause: true), out var failed, out _), Is.False);
                Assert.That(failed.Status, Is.EqualTo(ScreenStatus.Failed)); Assert.That(first.IsFocused, Is.True); Assert.That(router.OpenCount, Is.EqualTo(1));
                Assert.That(controls.Current.Revision, Is.EqualTo(revision));
            }
        }
        [Test] public void AViewCannotBeOwnedTwiceAndAnOldViewTokenCannotCloseItsNewGeneration()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var view = new View(); var first = Open(router, view);
                Assert.That(router.TryOpen("duplicate", view, Policy(), out _, out _), Is.False); first.Dispose(); var next = Open(router, view);
                view.Emit(first.BindingId, ScreenIntentKind.ViewClosed); view.Emit(first.BindingId, ScreenIntentKind.Back); first.Dispose();
                Assert.That(next.IsFocused, Is.True); Assert.That(view.Bound, Is.EqualTo(next.BindingId));
            }
        }
        [Test] public void PartialControlFailureReturnsTheViewAndEveryCreatedLease()
        {
            using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture external"))
            {
                external.BlockInput(InputChannels.Interaction); var requests = new FailingControls(controls) { ThrowBlock = true };
                using (var router = new ScreenRouter(requests))
                {
                    var view = new View(); Assert.That(router.TryOpen("broken", view, Policy(pause: true), out var failed, out _), Is.False);
                    Assert.That(failed.EndReason, Is.EqualTo(ScreenEndReason.ControlFailed)); Assert.That(view.Bound, Is.EqualTo(Guid.Empty));
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Interaction)); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
        [Test] public void FailedFocusAcquisitionClosesTheNewScreenAndKeepsThePreviousOwner()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var first = Open(router, new View()); var broken = new View { RejectFocus = true };
                Assert.That(router.TryOpen("broken", broken, Policy(pause: true), out var failed, out _), Is.False);
                Assert.That(failed.EndReason, Is.EqualTo(ScreenEndReason.FocusFailed)); Assert.That(first.IsFocused, Is.True); Assert.That(controls.Current.SimulationPaused, Is.False);
            }
        }
        [Test] public void ViewLossAndViewClosedEventsUseTheSameOwnershipReturnPath()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var lower = new View(); var first = Open(router, lower); var upper = new View(); var next = Open(router, upper, Policy(pause: true));
                upper.Available = false; router.Poll(); Assert.That(next.EndReason, Is.EqualTo(ScreenEndReason.ViewClosed)); Assert.That(first.IsFocused, Is.True);
                lower.Emit(first.BindingId, ScreenIntentKind.ViewClosed); Assert.That(router.OpenCount, Is.Zero); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None));
            }
        }
        [TestCase(false)] [TestCase(true)] public void CloseOrOwnerDisposalRaisedInsideFocusIsDeferredUntilTheCallbackReturns(bool disposeRouter)
        {
            using (var controls = new ControlSession())
            {
                var router = new ScreenRouter(controls); var view = new View();
                view.OnFocus = () => { if (disposeRouter) router.Dispose(); else view.Emit(view.Bound, ScreenIntentKind.ViewClosed); };
                Assert.That(router.TryOpen("callback", view, Policy(pause: true), out var handle, out _), Is.False);
                Assert.That(handle.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(router.OpenCount, Is.Zero); Assert.That(controls.Current.SimulationPaused, Is.False); router.Dispose();
            }
        }
        [TestCase(false)] [TestCase(true)] public void ClosingDuringBindDoesNotAcquireAnyControlAfterTheOwnerEnds(bool disposeRouter)
        {
            using (var controls = new ControlSession())
            {
                var router = new ScreenRouter(controls); var view = new View();
                view.OnBind = () => { if (disposeRouter) router.Dispose(); else view.Emit(view.Bound, ScreenIntentKind.ViewClosed); };
                Assert.That(router.TryOpen("bind callback", view, Policy(pause: true), out var handle, out _), Is.False);
                Assert.That(handle.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(controls.Current.Revision, Is.Zero);
                Assert.That(router.OpenCount, Is.Zero); Assert.That(view.Bound, Is.EqualTo(Guid.Empty)); router.Dispose();
            }
        }
        [Test] public void TheInitialFocusReturnIsOwnedOnceByTheRouterRatherThanByChildScreens()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var firstView = new View(); var a = Open(router, firstView); var nextView = new View(); var b = Open(router, nextView);
                Assert.That(firstView.Captures, Is.EqualTo(1)); Assert.That(nextView.Captures, Is.Zero); a.Dispose();
                Assert.That(firstView.Restores, Is.Zero); b.Dispose(); Assert.That(firstView.Restores, Is.EqualTo(1));
                router.Dispose(); a.Dispose(); b.Dispose(); Assert.That(firstView.Restores, Is.EqualTo(1));
            }
        }
        [Test] public void FocusReturnFailureIsReportedAfterEveryScreenAndControlHasReturned()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var view = new View { ThrowRestore = true }; var handle = Open(router, view); handle.Dispose();
                Assert.That(router.LastFocusError, Does.Contain("focus return failed")); Assert.That(router.OpenCount, Is.Zero);
                Assert.That(view.Bound, Is.EqualTo(Guid.Empty)); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None));
            }
        }
        [Test] public void ATemporaryFocusTransactionDefersSelectionAndConsumesBackUntilPoll()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var first = Open(router, new View()); var view = new View { FocusReady = false }; var next = Open(router, view);
                Assert.That(first.IsFocused, Is.True); Assert.That(next.IsFocused, Is.False);
                var result = router.Back(first.BindingId); Assert.That(result.Consumed, Is.True); Assert.That(result.Closed, Is.False);
                view.FocusReady = true; router.Poll(); Assert.That(next.IsFocused, Is.True); Assert.That(first.IsFocused, Is.False);
            }
        }
        [Test] public void PollSnapshotSurvivesNestedPollAndCallbackClosingAnotherEntryBeforeItsTurn()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var lowerView = new View(); var upperView = new View();
                var lower = Open(router, lowerView); var upper = Open(router, upperView);
                bool called = false; int upperReads = 0;
                lowerView.OnAvailable = () => {
                    if (called) return; called = true;
                    upper.Dispose(); router.Poll();
                    Assert.That(router.TryOpen("reentrant", new View(), Policy(), out _, out _), Is.False);
                };
                upperView.OnAvailable = () => upperReads++;
                router.Poll();
                Assert.That(called, Is.True); Assert.That(upperReads, Is.EqualTo(1), "The original poll snapshot still visits the requested-close entry.");
                Assert.That(upper.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(lower.IsFocused, Is.True);
                Assert.That(router.OpenCount, Is.EqualTo(1));
            }
        }
        [Test] public void NewCloseRequestsFromUnbindKeepOriginalFocusSettlingBeforeNextPoll()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var lowerView = new View(); var upperView = new View();
                var lower = Open(router, lowerView); var upper = Open(router, upperView);
                var order = new System.Collections.Generic.List<string>();
                upperView.OnUnbind = () => { order.Add("upper"); lower.Dispose(); router.Poll(); };
                lowerView.OnUnbind = () => order.Add("lower");
                upper.Dispose();
                Assert.That(order, Is.EqualTo(new[] { "upper" }));
                Assert.That(lower.Status, Is.EqualTo(ScreenStatus.Open)); Assert.That(router.OpenCount, Is.Zero);
                router.Poll();
                Assert.That(lower.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(router.OpenCount, Is.Zero);
                Assert.That(order, Is.EqualTo(new[] { "upper", "lower" }));
                Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None));
            }
        }
        [Test] public void RepeatedSettledPollsAllocateNoManagedMemoryAfterScreenOpen()
        {
            using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var lower = Open(router, new View()); var upper = Open(router, new View());
                for (int i = 0; i < 8; i++) router.Poll();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++) router.Poll();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
                Assert.That(upper.IsFocused, Is.True); Assert.That(lower.Status, Is.EqualTo(ScreenStatus.Open));
            }
        }
        [Test] public void EveryCleanupIsAttemptedAndFailuresDoNotReleaseOtherOwners()
        {
            using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture external"))
            {
                external.BlockInput(InputChannels.Interaction); var requests = new FailingControls(controls);
                using (var router = new ScreenRouter(requests))
                {
                    var view = new View(); var handle = Open(router, view); view.ThrowUnsubscribe = view.ThrowUnbind = requests.ThrowDispose = true;
                    handle.Dispose(); Assert.That(handle.Status, Is.EqualTo(ScreenStatus.Failed)); Assert.That(handle.EndReason, Is.EqualTo(ScreenEndReason.CleanupFailed));
                    Assert.That(handle.Error, Does.Contain("unsubscribe failed").And.Contain("unbind failed").And.Contain("control cleanup failed"));
                    Assert.That(router.OpenCount, Is.Zero); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Interaction));
                }
            }
        }
    }
}
