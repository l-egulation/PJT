using System;
using System.Collections;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using Deviant.UI.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class ScreenSelectionCallback : MonoBehaviour, ISelectHandler
    {
        public Action Selected;
        public void OnSelect(BaseEventData data) { Selected?.Invoke(); }
    }
    public class ScreenFocusIntegrationTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly GameObject Canvas = new GameObject("ScreenFocusCanvas", typeof(RectTransform), typeof(UnityEngine.Canvas), typeof(GraphicRaycaster));
            public readonly GameObject EventOwner = new GameObject("ScreenFocusEvents", typeof(EventSystem));
            public EventSystem Events => EventOwner.GetComponent<EventSystem>();
            public readonly Button Baseline;
            public Fixture()
            {
                Canvas.GetComponent<UnityEngine.Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Baseline = Button(Canvas.transform, "Baseline"); Events.SetSelectedGameObject(Baseline.gameObject);
            }
            public ViewSet Create(string name)
            {
                var panel = new GameObject(name + "Panel", typeof(RectTransform), typeof(CanvasGroup)); panel.transform.SetParent(Canvas.transform, false);
                var first = Button(panel.transform, name + "First"); var second = Button(panel.transform, name + "Second"); var back = Button(panel.transform, name + "Back");
                var owner = new GameObject(name + "Owner"); owner.transform.SetParent(Canvas.transform, false); var view = owner.AddComponent<ScreenView>();
                var group = panel.GetComponent<CanvasGroup>(); group.interactable = true; group.blocksRaycasts = true; panel.SetActive(false);
                view.Configure(panel, group, Events, first, back, true, true); return new ViewSet { View = view, Panel = panel, Group = group, First = first, Second = second, Back = back };
            }
            static Button Button(Transform parent, string name)
            {
                var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); root.transform.SetParent(parent, false);
                return root.GetComponent<Button>();
            }
            public void Dispose() { Object.DestroyImmediate(Canvas); Object.DestroyImmediate(EventOwner); }
        }
        sealed class ViewSet { public ScreenView View; public GameObject Panel; public CanvasGroup Group; public Button First, Second, Back; }
        static ScreenPolicy Policy(bool pause = false) => new ScreenPolicy(10, InputChannels.Combat, pause, true);
        static IScreenHandle Open(ScreenRouter router, ViewSet view, bool pause = false)
        { Assert.That(router.TryOpen("fixture screen", view.View, Policy(pause), out var handle, out var error), Is.True, error); return handle; }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator NestedViewsRestoreRememberedSelectionAndDoNotLeakTheSameBackEdge()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture()) using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture external")) using (var router = new ScreenRouter(controls))
            {
                var other = external.BlockInput(InputChannels.Interaction); var pause = external.PauseSimulation();
                var lower = fixture.Create("Lower"); var a = Open(router, lower); fixture.Events.SetSelectedGameObject(lower.Second.gameObject);
                var upper = fixture.Create("Upper"); var b = Open(router, upper, true);
                Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(upper.First.gameObject)); Assert.That(lower.Group.interactable || lower.Group.blocksRaycasts, Is.False);
                lower.Back.onClick.Invoke(); Assert.That(b.Status, Is.EqualTo(ScreenStatus.Open), "An unfocused view cannot emit Back through a direct button callback.");
                int behind = 0; var token = b.BindingId; if (!router.Back(token).Consumed) behind++; if (!router.Back(token).Consumed) behind++;
                Assert.That(behind, Is.Zero); Assert.That(a.Status, Is.EqualTo(ScreenStatus.Open)); Assert.That(a.IsFocused, Is.True);
                Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(lower.Second.gameObject)); Assert.That(upper.Panel.activeSelf, Is.False);
                Assert.That(other.IsActive && pause.IsActive, Is.True); lower.Back.onClick.Invoke();
                Assert.That(a.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(fixture.Baseline.gameObject));
                Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Interaction)); Assert.That(controls.Current.SimulationPaused, Is.True);
                Assert.That(lower.Group.interactable && lower.Group.blocksRaycasts, Is.True); LogAssert.NoUnexpectedReceived();
            }
        }
        [UnityTest] public IEnumerator OwnerDisposalClosesTopFirstAndOldGenerationsCannotReleaseNewViews()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture()) using (var controls = new ControlSession())
            {
                var lower = fixture.Create("Lower"); var upper = fixture.Create("Upper"); var router = new ScreenRouter(controls);
                var a = Open(router, lower); var b = Open(router, upper); router.Dispose();
                Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(fixture.Baseline.gameObject)); Assert.That(router.OpenCount, Is.Zero);
                using (var nextRouter = new ScreenRouter(controls))
                {
                    var next = Open(nextRouter, upper); a.Dispose(); b.Dispose(); upper.View.Unbind(b.BindingId);
                    Assert.That(next.IsFocused, Is.True); Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(upper.First.gameObject));
                    upper.View.enabled = false; nextRouter.Poll(); Assert.That(next.Status, Is.EqualTo(ScreenStatus.Closed));
                    Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(fixture.Baseline.gameObject)); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None));
                }
                LogAssert.NoUnexpectedReceived();
            }
        }
        [UnityTest] public IEnumerator OwnerDisposalInsideSelectionCallbackWaitsUntilEventSystemSelectionReturns()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture()) using (var controls = new ControlSession())
            {
                var router = new ScreenRouter(controls); var view = fixture.Create("Callback"); var callback = view.First.gameObject.AddComponent<ScreenSelectionCallback>(); callback.Selected = router.Dispose;
                Assert.That(router.TryOpen("selection callback", view.View, Policy(true), out var handle, out _), Is.False);
                Assert.That(handle.EndReason, Is.EqualTo(ScreenEndReason.OwnerDisposed)); Assert.That(controls.Current.SimulationPaused, Is.False);
                Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(fixture.Baseline.gameObject)); Assert.That(view.Panel.activeSelf, Is.False); LogAssert.NoUnexpectedReceived();
            }
        }
        [UnityTest] public IEnumerator ASelectionCallbackOutsideRouterMutationDefersParentFocusAndFinalReturn()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture()) using (var controls = new ControlSession())
            {
                var router = new ScreenRouter(controls); var lower = fixture.Create("Lower"); var a = Open(router, lower); var upper = fixture.Create("Upper"); var b = Open(router, upper);
                var closeChild = upper.Second.gameObject.AddComponent<ScreenSelectionCallback>(); closeChild.Selected = b.Dispose;
                fixture.Events.SetSelectedGameObject(upper.Second.gameObject); Assert.That(a.Status, Is.EqualTo(ScreenStatus.Open));
                yield return null; router.Poll(); Assert.That(a.IsFocused, Is.True); Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(lower.First.gameObject));
                a.Dispose(); Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(fixture.Baseline.gameObject));
                var finalView = fixture.Create("Final"); var final = Open(router, finalView); var closeOwner = finalView.Second.gameObject.AddComponent<ScreenSelectionCallback>(); closeOwner.Selected = router.Dispose;
                fixture.Events.SetSelectedGameObject(finalView.Second.gameObject); yield return null;
                Assert.That(final.Status, Is.EqualTo(ScreenStatus.Closed)); Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(fixture.Baseline.gameObject));
                Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None)); LogAssert.NoUnexpectedReceived();
            }
        }
        // The village departure book disables the card that is not picked; when that card is the configured first focus the
        // screen must still open on another selectable instead of failing and hiding its panel under the veil.
        [UnityTest] public IEnumerator ADisabledFirstSelectionFocusesTheNextSelectableInsteadOfFailing()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture()) using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var view = fixture.Create("Screen"); view.First.interactable = false;
                var handle = Open(router, view);
                Assert.That(handle.IsFocused, Is.True); Assert.That(view.Panel.activeSelf, Is.True);
                Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(view.Second.gameObject));
                handle.Dispose(); Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(fixture.Baseline.gameObject));
                LogAssert.NoUnexpectedReceived();
            }
        }
        [UnityTest] public IEnumerator DestroyedPriorSelectionIsNotRestoredAndScreenLayoutCanBeRebound()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture()) using (var controls = new ControlSession()) using (var router = new ScreenRouter(controls))
            {
                var view = fixture.Create("Screen"); var handle = Open(router, view); Object.DestroyImmediate(fixture.Baseline.gameObject);
                handle.Dispose(); Assert.That(fixture.Events.currentSelectedGameObject, Is.Null);
                view.View.Configure(view.Panel, view.Group, fixture.Events, view.Second, view.Back, true, true);
                var next = Open(router, view); view.View.Unbind(handle.BindingId); Assert.That(fixture.Events.currentSelectedGameObject, Is.EqualTo(view.Second.gameObject));
                next.Dispose(); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None)); LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
