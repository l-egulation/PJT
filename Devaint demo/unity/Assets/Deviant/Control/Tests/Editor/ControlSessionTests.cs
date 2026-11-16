using System;
using System.Linq;
using Deviant.Control.Contracts;
using Deviant.UI.Presentation;
using Deviant.UI.Contracts;
using NUnit.Framework;

namespace Deviant.Control.Tests
{
    public class ControlSessionTests
    {
        [Test] public void RuntimeAndPresenterHaveNoUnityOrCombatDependency()
        {
            foreach (var assembly in new[] { typeof(ControlSession).Assembly, typeof(ControlState).Assembly, typeof(HudControlPresenter).Assembly })
                Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("Deviant.Combat")), Is.False, assembly.FullName);
        }
        [Test] public void DistinctScopesWithSameNameCannotReleaseEachOthersInputOrPause()
        {
            using (var session = new ControlSession())
            {
                var a = session.OpenScope("modal"); var b = session.OpenScope("modal");
                var ai = a.BlockInput(InputChannels.Combat | InputChannels.Interaction); var ap = a.PauseSimulation();
                var bi = b.BlockInput(InputChannels.Combat | InputChannels.Dialogue); var bp = b.PauseSimulation();
                var before = session.Current;
                a.Dispose(); a.Dispose(); ai.Dispose(); ap.Dispose();
                Assert.That(session.Current.Blocked, Is.EqualTo(InputChannels.Combat | InputChannels.Dialogue));
                Assert.That(session.Current.SimulationPaused, Is.True); Assert.That(bi.IsActive && bp.IsActive, Is.True);
                Assert.That(ai.IsActive || ap.IsActive || a.IsActive, Is.False);
                Assert.That(before.Blocked, Is.EqualTo(InputChannels.Combat | InputChannels.Interaction | InputChannels.Dialogue), "Snapshots are immutable values.");
                bi.Dispose(); Assert.That(session.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(session.Current.SimulationPaused, Is.True);
                bp.Dispose(); Assert.That(session.Current.SimulationPaused, Is.False);
            }
        }
        [Test] public void EachLeaseIsIndependentEvenWithinOneOwner()
        {
            using (var session = new ControlSession()) using (var owner = session.OpenScope("dialogue"))
            {
                var first = owner.BlockInput(InputChannels.Combat); var second = owner.BlockInput(InputChannels.Combat);
                var revision = session.Current.Revision;
                first.Dispose(); first.Dispose(); Assert.That(session.Current.Revision, Is.EqualTo(revision));
                Assert.That(session.Current.Blocks(InputChannels.Combat), Is.True);
                second.Dispose(); Assert.That(session.Current.Blocks(InputChannels.Combat), Is.False);
                Assert.That(session.Current.Revision, Is.GreaterThan(revision));
            }
        }
        [TestCase(InputChannels.None)] [TestCase((InputChannels)16)] [TestCase((InputChannels)(-1))]
        public void InvalidInputRequestCannotMutateExistingControl(InputChannels invalid)
        {
            using (var session = new ControlSession()) using (var owner = session.OpenScope("UI"))
            {
                var active = owner.PauseSimulation(); var before = session.Current;
                Assert.Throws<ArgumentOutOfRangeException>(() => owner.BlockInput(invalid));
                Assert.That(session.Current, Is.EqualTo(before)); Assert.That(active.IsActive, Is.True);
            }
        }
        [TestCase(null)] [TestCase("")] [TestCase("  ")]
        public void MissingOwnerIsRejected(string owner)
        {
            using (var session = new ControlSession())
            {
                Assert.Throws<ArgumentException>(() => session.OpenScope(owner)); Assert.That(session.Current, Is.EqualTo(default(ControlState)));
            }
        }
        [Test] public void SessionEndInvalidatesAllScopesAndStaleHandlesCannotAffectNewSession()
        {
            var old = new ControlSession(); var scope = old.OpenScope("old");
            var lease = scope.BlockInput(InputChannels.Combat); scope.PauseSimulation(); old.Dispose(); old.Dispose();
            Assert.That(scope.IsActive || lease.IsActive, Is.False); Assert.That(old.Current.SimulationPaused, Is.False);
            Assert.Throws<ObjectDisposedException>(() => old.OpenScope("late"));
            Assert.Throws<ObjectDisposedException>(() => scope.BlockInput(InputChannels.Navigation));
            Assert.Throws<ObjectDisposedException>(() => scope.PauseSimulation());
            using (var next = new ControlSession()) using (var owner = next.OpenScope("new"))
            {
                owner.BlockInput(InputChannels.Navigation); owner.PauseSimulation(); scope.Dispose(); lease.Dispose();
                Assert.That(next.Current.SimulationPaused, Is.True); Assert.That(next.Current.Blocked, Is.EqualTo(InputChannels.Navigation));
            }
        }
        [Test] public void InputRestrictionAndSimulationPauseAreSeparateRequests()
        {
            using (var session = new ControlSession()) using (var owner = session.OpenScope("test"))
            {
                var input = owner.BlockInput(InputChannels.Combat);
                Assert.That(session.Current.SimulationPaused, Is.False); input.Dispose();
                owner.PauseSimulation(); Assert.That(session.Current.Blocked, Is.EqualTo(InputChannels.None));
            }
        }
    }
    public class HudControlPresenterTests
    {
        sealed class View : IScreenView, IScreenFocusReturn
        {
            Guid binding;
            public bool IsAvailable => true;
            public bool IsFocusReady => true;
            public event Action<ScreenIntent> Input;
            public IScreenFocusReturn CaptureFocusReturn() => this;
            public bool TryBind(Guid value, out string error) { binding = value; error = null; return true; }
            public bool TrySetFocus(Guid value, bool focused) => value == binding;
            public void Unbind(Guid value) { if (binding == value) binding = Guid.Empty; }
            public void Restore() { }
            public void Close() => Input?.Invoke(new ScreenIntent(binding, ScreenIntentKind.ViewClosed));
        }
        static void Back(ScreenRouter router, HudControlPresenter hud)
        {
            var token = router.FocusedBindingId;
            hud.BackProcessed(router.Back(token));
        }
        [Test] public void ClosingPauseReturnsOnlyHudControlWhileDialogueStillBlocksCombat()
        {
            using (var session = new ControlSession()) using (var dialogue = session.OpenScope("dialogue"))
            using (var router = new ScreenRouter(session)) using (var hud = new HudControlPresenter(session, router, new View()))
            {
                dialogue.BlockInput(InputChannels.Combat | InputChannels.Interaction);
                hud.TogglePause(); Assert.That(hud.Paused && session.Current.SimulationPaused, Is.True);
                Assert.That(hud.Dialog, Is.EqualTo(HudDialog.Pause));
                hud.CloseDialog(); Assert.That(hud.Paused || session.Current.SimulationPaused, Is.False);
                Assert.That(hud.Dialog, Is.EqualTo(HudDialog.None));
                Assert.That(session.Current.Blocked, Is.EqualTo(InputChannels.Combat | InputChannels.Interaction));
            }
        }
        [Test] public void BagDoesNotPauseAndEscapeClosesBagBeforeTogglingPause()
        {
            using (var session = new ControlSession()) using (var router = new ScreenRouter(session))
            using (var hud = new HudControlPresenter(session, router, new View()))
            {
                hud.OpenBag(); hud.OpenBag(); Assert.That(session.Current.SimulationPaused, Is.False);
                Assert.That(session.Current.Blocked, Is.EqualTo(InputChannels.Combat)); Assert.That(hud.Dialog, Is.EqualTo(HudDialog.Bag));
                Back(router, hud); Assert.That(session.Current.Blocked, Is.EqualTo(InputChannels.None));
                Back(router, hud); Assert.That(hud.Paused, Is.True); Back(router, hud); Assert.That(hud.Paused, Is.False);
            }
        }
        [Test] public void PointerAndDialogRestrictionsHaveIndependentLifetimes()
        {
            using (var session = new ControlSession()) using (var router = new ScreenRouter(session))
            using (var hud = new HudControlPresenter(session, router, new View()))
            {
                hud.SetPointerBlocked(true); hud.SetPointerBlocked(true); hud.OpenBag(); hud.CloseDialog();
                Assert.That(session.Current.Blocks(InputChannels.Combat), Is.True);
                hud.SetPointerBlocked(false); Assert.That(session.Current.Blocks(InputChannels.Combat), Is.False);
                hud.TogglePause(); hud.SetPointerBlocked(true); hud.SetPointerBlocked(false);
                Assert.That(session.Current.SimulationPaused && session.Current.Blocks(InputChannels.Combat), Is.True);
            }
        }
        [Test] public void DisableOrRebindDoesNotReleaseAnotherOwnerAndOldPresenterRejectsCallbacks()
        {
            using (var session = new ControlSession()) using (var external = session.OpenScope("sequence"))
            {
                external.PauseSimulation(); external.BlockInput(InputChannels.Interaction);
                using (var router = new ScreenRouter(session))
                {
                    var old = new HudControlPresenter(session, router, new View()); old.TogglePause(); old.SetPointerBlocked(true); old.Dispose(); old.Dispose();
                    Assert.That(old.Paused, Is.False); Assert.That(old.Dialog, Is.EqualTo(HudDialog.None));
                    Assert.That(session.Current.SimulationPaused, Is.True); Assert.That(session.Current.Blocked, Is.EqualTo(InputChannels.Interaction));
                    using (var next = new HudControlPresenter(session, router, new View()))
                    {
                        next.OpenBag(); old.Dispose();
                        Assert.Throws<ObjectDisposedException>(() => old.TogglePause());
                        Assert.Throws<ObjectDisposedException>(() => old.OpenBag());
                        Assert.Throws<ObjectDisposedException>(() => old.SetPointerBlocked(false));
                        Assert.That(session.Current.Blocks(InputChannels.Combat), Is.True);
                    }
                }
            }
        }
        [Test] public void ClosingHudNeverUnpausesAnotherPauseOwner()
        {
            using (var session = new ControlSession()) using (var external = session.OpenScope("other"))
            using (var router = new ScreenRouter(session)) using (var hud = new HudControlPresenter(session, router, new View()))
            {
                var pause = external.PauseSimulation(); hud.TogglePause(); hud.TogglePause();
                Assert.That(session.Current.SimulationPaused, Is.True); pause.Dispose(); Assert.That(session.Current.SimulationPaused, Is.False);
            }
        }
        [Test] public void BackClosesOnlyFocusedOverlayAndExternalRouterSurvivesHudDisposal()
        {
            using (var session = new ControlSession()) using (var router = new ScreenRouter(session))
            {
                var view = new View(); var hud = new HudControlPresenter(session, router, view);
                hud.TogglePause(); hud.OpenBag();
                Assert.That(hud.Paused, Is.True); Assert.That(hud.Dialog, Is.EqualTo(HudDialog.Bag));
                Assert.That(router.OpenCount, Is.EqualTo(1));
                Assert.That(router.TryOpen("external overlay", new View(), new ScreenPolicy(10, InputChannels.Interaction, false, true), out var overlay, out _), Is.True);
                Back(router, hud);
                Assert.That(overlay.Status, Is.EqualTo(ScreenStatus.Closed));
                Assert.That(hud.Dialog, Is.EqualTo(HudDialog.Bag)); Assert.That(hud.Paused, Is.True);
                hud.Dispose(); Assert.That(router.OpenCount, Is.Zero);
                Assert.That(router.TryOpen("router still owned", new View(), new ScreenPolicy(10, InputChannels.None, false, true), out var later, out _), Is.True);
                Assert.That(later.Status, Is.EqualTo(ScreenStatus.Open));
            }
        }
        [Test] public void ViewCloseReleasesOnlyHudScreenAndConsumedStaleBackDoesNotOpenPause()
        {
            using (var session = new ControlSession()) using (var router = new ScreenRouter(session))
            {
                var view = new View(); using (var hud = new HudControlPresenter(session, router, view))
                {
                    hud.OpenBag(); var stale = hud.BindingId; view.Close();
                    Assert.That(hud.Dialog, Is.EqualTo(HudDialog.None)); Assert.That(router.OpenCount, Is.Zero);
                    hud.BackProcessed(router.Back(stale));
                    Assert.That(hud.Paused, Is.False); Assert.That(router.OpenCount, Is.Zero);
                }
            }
        }
    }
}
