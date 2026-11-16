using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Control.Contracts;
using Deviant.Tutorial.Unity;
using Deviant.Input.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Deviant.Tutorial.Tests
{
    public sealed class FirstRunTutorialTests
    {
        GameObject owner;
        FirstRunTutorialWindow Create() => (owner = new GameObject("Tutorial under test", typeof(RectTransform))).AddComponent<FirstRunTutorialWindow>();
        [TearDown] public void Cleanup() { if (owner) Object.DestroyImmediate(owner); }

        [UnityTest] public IEnumerator NavigationIsBoundedAndUsesLivePageCount()
        {
            var view = Create(); view.ShowRewards();
            Assert.That(view.PageCount, Is.EqualTo(5));
            view.Close(); Assert.That(view.IsOpen, Is.True, "opening input is ignored");
            yield return new WaitForSecondsRealtime(.4f);
            view.PreviousPage(); Assert.That(view.PageIndex, Is.Zero);
            for (int i = 0; i < 8; i++) view.NextPage();
            Assert.That(view.PageIndex, Is.EqualTo(4)); Assert.That(view.CanGoNext, Is.False);
            Assert.That(Array.Exists(view.GetComponentsInChildren<Text>(), text => text.text == "5 / 5"), Is.True);
            for (int i = 0; i < 8; i++) view.PreviousPage();
            Assert.That(view.PageIndex, Is.Zero); Assert.That(view.CanGoPrevious, Is.False);
        }

        [UnityTest] public IEnumerator ReplayBoundariesHideUnavailableArrowsAndNeverRestartTheSamePage()
        {
            var view = Create(); var selections = new List<int>(); view.ShowReplay(7, selections.Add);
            var buttons = view.GetComponentsInChildren<Button>(true);
            var previous = Array.Find(buttons, button => button.name == "Previous page");
            var next = Array.Find(buttons, button => button.name == "Next page");
            Assert.That(previous.gameObject.activeSelf, Is.False); Assert.That(next.gameObject.activeSelf, Is.True);
            Assert.That(selections, Is.EqualTo(new[] { 0 }));
            yield return new WaitForSecondsRealtime(.4f);
            view.SelectPage(-1); view.SelectPage(0); view.PreviousPage(); previous.onClick.Invoke();
            Assert.That(view.PageIndex, Is.Zero); Assert.That(selections, Is.EqualTo(new[] { 0 }), "left boundary must not restart the demonstration");
            view.SelectPage(3);
            Assert.That(previous.gameObject.activeSelf, Is.True); Assert.That(next.gameObject.activeSelf, Is.True);
            view.SelectPage(6);
            Assert.That(previous.gameObject.activeSelf, Is.True); Assert.That(next.gameObject.activeSelf, Is.False);
            view.SelectPage(7); view.SelectPage(100); view.SelectPage(6); view.NextPage(); next.onClick.Invoke();
            Assert.That(view.PageIndex, Is.EqualTo(6)); Assert.That(selections, Is.EqualTo(new[] { 0, 3, 6 }), "right boundary must not restart the demonstration");
            view.PreviousPage();
            Assert.That(view.PageIndex, Is.EqualTo(5)); Assert.That(next.gameObject.activeSelf, Is.True);
            view.SelectPage(0);
            Assert.That(previous.gameObject.activeSelf, Is.False); Assert.That(next.gameObject.activeSelf, Is.True);
            Assert.That(selections, Is.EqualTo(new[] { 0, 3, 6, 5, 0 }));
        }

        [UnityTest] public IEnumerator SystemReplayIncludesAttackAndEquipmentAndReleasesItsOwnScope()
        {
            var controls=new Scopes();var view=Create();view.ShowSystem(null,controls);
            Assert.That(view.PageCount,Is.EqualTo(6));Assert.That(controls.Paused,Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.4f);
            view.SelectPage(100);Assert.That(view.PageIndex,Is.EqualTo(5));Assert.That(view.CanGoNext,Is.False);
            view.Close();Assert.That(controls.Paused,Is.Zero);
            view.ShowRewards();Assert.That(view.PageCount,Is.EqualTo(5),"automatic reward guide stays unchanged");
            view.ShowAttack();Assert.That(view.PageCount,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MovementReplayStaysSeparateFromAttackAndEquipment()
        {
            var view = Create();
            view.Build();
            var custom = view.ReplayContent;
            int selected = -1, closed = 0;
            view.ShowReplay(7, index => selected = index, () => closed++);
            Assert.That(view.PageCount, Is.EqualTo(7));
            Assert.That(custom.gameObject.activeSelf, Is.True);
            Assert.That(selected, Is.Zero);
            yield return new WaitForSecondsRealtime(.4f);
            view.SelectPage(100);
            Assert.That(view.PageIndex, Is.EqualTo(6));
            Assert.That(custom.gameObject.activeSelf, Is.True);
            Assert.That(Array.Exists(view.GetComponentsInChildren<Text>(), t => t.text == "공격"), Is.False);
            Assert.That(view.CanGoNext, Is.False);
            view.ShowRewards();
            Assert.That(view.PageCount, Is.EqualTo(5));
            Assert.That(custom.gameObject.activeSelf, Is.False);
            Assert.That(closed, Is.Zero, "reopening replaces the previous callback");
            view.Dismiss();
            Assert.That(view.IsOpen, Is.False);
        }

        [UnityTest] public IEnumerator ClosingReleasesOnlyThisWindowAndInvokesCallbackOnce()
        {
            var controls = new Scopes(); var reward = controls.OpenScope("existing reward"); reward.PauseSimulation();
            int calls = 0; var view = Create(); view.ShowRewards(null, controls, () => calls++);
            Assert.That(controls.Paused, Is.EqualTo(2));
            yield return new WaitForSecondsRealtime(.4f);
            view.Close(); view.Close();
            Assert.That(view.IsOpen, Is.False); Assert.That(calls, Is.EqualTo(1));
            Assert.That(controls.Paused, Is.EqualTo(1), "the reward screen remains paused");
            reward.Dispose();
        }

        [UnityTest] public IEnumerator AttackHidesPaginationAndReopeningResetsToFirstPage()
        {
            var view = Create(); view.ShowRewards();
            yield return new WaitForSecondsRealtime(.4f);
            view.NextPage(); view.NextPage();
            view.ShowAttack(); Assert.That(view.PageCount, Is.EqualTo(1)); Assert.That(view.PageIndex, Is.Zero);
            Assert.That(Array.Exists(view.GetComponentsInChildren<Button>(), button => button.name == "Next page"), Is.False);
            view.ShowRewards(); Assert.That(view.PageIndex, Is.Zero); Assert.That(view.PageCount, Is.EqualTo(5));
        }

        [TestCase("F")]
        [TestCase("Backspace")]
        [TestCase("Triangle")]
        public void AttackBindingFitsItsKeycapWithoutOverlappingCaption(string binding)
        {
            var view = Create(); view.ShowAttack();
            var key = Array.Find(view.GetComponentsInChildren<Image>(), image => image.name == "A key");
            var letter = Array.Find(view.GetComponentsInChildren<Text>(), text => text.name == "A");
            var caption = Array.Find(view.GetComponentsInChildren<Text>(), text => text.text == "공격");
            Assert.That(key, Is.Not.Null); Assert.That(letter, Is.Not.Null); Assert.That(caption, Is.Not.Null);
            letter.text = binding;
            typeof(FirstRunTutorialWindow).GetMethod("LayoutAttackKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
            Canvas.ForceUpdateCanvases();
            var cap = key.rectTransform; var textRect = letter.rectTransform; var label = caption.rectTransform;
            Assert.That(cap.rect.width, Is.GreaterThanOrEqualTo(letter.preferredWidth + 15), "bound label needs side padding");
            Assert.That(textRect.anchoredPosition, Is.EqualTo(cap.anchoredPosition));
            Assert.That(textRect.rect.size, Is.EqualTo(cap.rect.size), "key text uses the cap's centre");
            Assert.That(label.anchoredPosition.x, Is.GreaterThanOrEqualTo(cap.anchoredPosition.x + cap.rect.width + 12), "caption must not overlap a long bound key");
            float groupRight = label.anchoredPosition.x + label.rect.width;
            Assert.That((cap.anchoredPosition.x + groupRight) / 2, Is.EqualTo(268).Within(.5f));
            Assert.That(cap.anchoredPosition.x, Is.GreaterThanOrEqualTo(20));
            Assert.That(groupRight, Is.LessThanOrEqualTo(516));
        }

        [Test] public void DestroyingWindowReleasesItsPauseWithoutCompletingTutorial()
        {
            int calls = 0; var controls = new Scopes(); var view = Create();
            view.ShowRewards(null, controls, () => calls++);
            Object.DestroyImmediate(owner); owner = null;
            Assert.That(controls.Paused, Is.Zero); Assert.That(calls, Is.Zero);
        }

        [UnityTest] public IEnumerator PhysicalFClosesAndHeldRightDoesNotSkipPages()
        {
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var view = Create();
                owner.SetActive(false);
                var input = owner.AddComponent<LocalInput>();
                typeof(LocalInput).GetField("profile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(input, Resources.Load<InputActionAsset>(LocalInput.DefaultProfile));
                owner.SetActive(true);
                input.Source.SetDevices(keyboard);
                int calls = 0; view.ShowRewards(input, null, () => calls++);
                yield return new WaitForSecondsRealtime(.4f);
                input.Source.SetFocused(true);
                if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                InputSystem.Update();
                Assert.That(input.Read(Deviant.Input.Contracts.GameAction.Navigate).X, Is.EqualTo(1), "synthetic keyboard reaches the shared UI input");
                yield return null; yield return null;
                Assert.That(view.PageIndex, Is.EqualTo(1));
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(view.PageIndex, Is.EqualTo(1), "holding a direction must not skip multiple pages");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                InputSystem.Update();
                yield return null; yield return null;
                Assert.That(view.IsOpen, Is.False); Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        sealed class Scopes : IControlRequests
        {
            readonly List<Scope> active = new List<Scope>();
            public int Paused => active.FindAll(x => x.IsActive && x.pause).Count;
            public ControlState Current => new ControlState(InputChannels.None, Paused > 0, 0);
            public IControlScope OpenScope(string name) { var value = new Scope(); active.Add(value); return value; }
            sealed class Scope : IControlScope, IControlLease
            {
                public bool pause; public bool IsActive { get; private set; } = true;
                public IControlLease BlockInput(InputChannels channels) => this;
                public IControlLease PauseSimulation() { pause = true; return this; }
                public void Dispose() => IsActive = false;
            }
        }
    }
}
