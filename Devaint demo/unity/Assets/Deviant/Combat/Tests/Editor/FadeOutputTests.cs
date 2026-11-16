using System;
using System.Collections;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class FadeOutputTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("FadeOutputFixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            public readonly Image Image;
            public readonly FadeOutputBinding Output;
            public Fixture()
            {
                var panel = new GameObject("FadeFixtureImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); panel.transform.SetParent(Root.transform, false);
                Image = panel.GetComponent<Image>(); Image.raycastTarget = false;
                Output = Root.AddComponent<FadeOutputBinding>(); Output.Configure(Image, Baseline(.125f));
            }
            public void Dispose() { if (Root) Object.DestroyImmediate(Root); }
        }
        static FadeFrame Baseline(float alpha) => new FadeFrame(.1f, .2f, .3f, alpha);
        static FadeDefinition Definition(bool hold, float from = 0, float to = 1) => new FadeDefinition("fixture-fade", .25f, .5f, .75f, from, to, .5, hold);
        static Color ColorOf(FadeFrame frame) => new Color(frame.Red, frame.Green, frame.Blue, frame.Opacity);
        [Test] public void ActualImageUsesSelectedFramesAndReturnsToTheCurrentBaseline()
        {
            float timeScale = Time.timeScale;
            using (var fixture = new Fixture()) using (var baseline = fixture.Output.BindBaseSource("fixture baseline")) using (var owner = fixture.Output.Requests.OpenScope("sequence"))
            {
                Assert.That(fixture.Image.color, Is.EqualTo(ColorOf(Baseline(.125f))));
                var fade = owner.Play(Definition(true), 10); fade.Poll(.25); fixture.Output.ApplyNow();
                Assert.That(fixture.Image.color, Is.EqualTo(new Color(.25f, .5f, .75f, .5f))); Assert.That(fixture.Output.LastSelection.RequestSequence, Is.EqualTo(fade.Sequence));
                baseline.Submit(Baseline(.375f)); fade.Poll(.5); fixture.Output.ApplyNow(); Assert.That(fixture.Image.color.a, Is.EqualTo(1));
                owner.Dispose(); fixture.Output.ApplyNow(); Assert.That(fixture.Image.color, Is.EqualTo(ColorOf(Baseline(.375f))));
                Assert.That(fixture.Output.RequestCount, Is.Zero); Assert.That(fixture.Image.raycastTarget, Is.False); Assert.That(Time.timeScale, Is.EqualTo(timeScale));
            }
        }
        [Test] public void HigherRequestReturnKeepsAnotherOwnersCurrentFrame()
        {
            using (var fixture = new Fixture()) using (var a = fixture.Output.OpenScope("a")) using (var b = fixture.Output.OpenScope("b"))
            {
                var first = a.Play(Definition(true), 1); var top = b.Play(Definition(true, 1, 0), 2); top.Poll(.125); fixture.Output.ApplyNow(); Assert.That(fixture.Image.color.a, Is.EqualTo(.75f));
                first.Poll(.25); b.Dispose(); fixture.Output.ApplyNow(); Assert.That(fixture.Image.color.a, Is.EqualTo(.5f));
                Assert.That(first.Status, Is.EqualTo(FadeStatus.Playing)); Assert.That(fixture.Output.RequestCount, Is.EqualTo(1));
            }
        }
        [Test] public void InvalidConfigurationPreservesTheOldOutputAndValidRebindInvalidatesItsSession()
        {
            using (var fixture = new Fixture()) using (var oldOwner = fixture.Output.OpenScope("old")) using (var oldBaseline = fixture.Output.BindBaseSource("old baseline"))
            {
                var held = oldOwner.Play(Definition(true), 1); held.Poll(.5); fixture.Output.ApplyNow(); var session = fixture.Output.SessionId;
                var other = new GameObject("OtherFadeImage", typeof(RectTransform), typeof(Image)); other.transform.SetParent(fixture.Root.transform, false); var image = other.GetComponent<Image>();
                Assert.Throws<ArgumentException>(() => fixture.Output.Configure(image, Baseline(.25f))); Assert.Throws<ArgumentNullException>(() => fixture.Output.Configure(null, Baseline(.25f)));
                Assert.That(fixture.Output.SessionId, Is.EqualTo(session)); Assert.That(fixture.Image.color.a, Is.EqualTo(1)); Assert.That(held.IsRetained, Is.True);
                image.raycastTarget = false; oldBaseline.Submit(Baseline(.5f)); fixture.Output.Configure(image, Baseline(.25f));
                Assert.That(fixture.Image.color, Is.EqualTo(ColorOf(Baseline(.5f)))); Assert.That(image.color, Is.EqualTo(ColorOf(Baseline(.25f))));
                Assert.That(fixture.Output.SessionId, Is.Not.EqualTo(session)); Assert.That(oldOwner.IsActive || oldBaseline.IsActive || held.IsRetained, Is.False);
                Assert.That(held.Status, Is.EqualTo(FadeStatus.Completed)); Assert.Throws<ObjectDisposedException>(() => oldOwner.Play(Definition(true), 1));
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void ObservedComponentOrImageDisableReturnsRequestsAndDoesNotReviveThem(bool imageOnly)
        {
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("old"))
            {
                var handle = owner.Play(Definition(true), 1); handle.Poll(.25); fixture.Output.ApplyNow(); var session = fixture.Output.SessionId;
                if (imageOnly) fixture.Image.enabled = false; else fixture.Output.enabled = false;
                fixture.Output.ApplyNow(); Assert.That(fixture.Output.IsAvailable, Is.False); Assert.That(fixture.Output.SessionId, Is.Not.EqualTo(session));
                Assert.That(handle.Status, Is.EqualTo(FadeStatus.Cancelled)); Assert.That(fixture.Output.RequestCount, Is.Zero); Assert.That(fixture.Image.color.a, Is.EqualTo(.125f));
                if (imageOnly) fixture.Image.enabled = true; else fixture.Output.enabled = true;
                fixture.Output.ApplyNow(); Assert.That(fixture.Output.IsAvailable, Is.True); handle.Poll(.5); Assert.That(fixture.Image.color.a, Is.EqualTo(.125f));
                using (var fresh = fixture.Output.OpenScope("new")) { fresh.Play(Definition(true), 1); owner.Dispose(); Assert.That(fixture.Output.RequestCount, Is.EqualTo(1)); }
            }
        }
        [Test] public void ImageLossAndWriteFailureInvalidateOutputAndCanBeExplicitlyReconfigured()
        {
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("owner"))
            {
                var handle = owner.Play(Definition(true), 1); handle.Poll(.25);
                UnityAction fail = () => throw new InvalidOperationException("fixture fade write failed"); fixture.Image.RegisterDirtyVerticesCallback(fail);
                fixture.Output.ApplyNow(); Assert.That(fixture.Output.LastApplyError, Does.Contain("fixture fade write failed")); Assert.That(fixture.Output.IsAvailable, Is.False);
                Assert.That(handle.Status, Is.EqualTo(FadeStatus.Cancelled)); Assert.That(fixture.Output.RequestCount, Is.Zero);
                fixture.Image.UnregisterDirtyVerticesCallback(fail); fixture.Output.Configure(fixture.Image, Baseline(.25f)); Assert.That(fixture.Output.IsAvailable, Is.True);
                using (var next = fixture.Output.OpenScope("next"))
                {
                    var active = next.Play(Definition(true), 1); Object.DestroyImmediate(fixture.Image.gameObject); fixture.Output.ApplyNow();
                    Assert.That(fixture.Output.IsAvailable, Is.False); Assert.That(active.Status, Is.EqualTo(FadeStatus.Cancelled)); Assert.That(fixture.Output.RequestCount, Is.Zero);
                }
            }
        }
        [Test] public void CanvasAvailabilityIsRequiredAndObservedLossInvalidatesOldRequests()
        {
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("owner"))
            {
                var handle = owner.Play(Definition(true), 1); handle.Poll(.25); fixture.Output.ApplyNow();
                var canvas = fixture.Root.GetComponent<Canvas>(); canvas.enabled = false; fixture.Output.ApplyNow();
                Assert.That(fixture.Output.IsAvailable, Is.False); Assert.That(handle.Status, Is.EqualTo(FadeStatus.Cancelled)); Assert.That(fixture.Output.RequestCount, Is.Zero);
                canvas.enabled = true; fixture.Output.ApplyNow(); Assert.That(fixture.Output.IsAvailable, Is.True); Assert.That(owner.IsActive, Is.False);
                fixture.Image.transform.SetParent(null); fixture.Output.ApplyNow(); Assert.That(fixture.Output.IsAvailable, Is.False);
                Assert.Throws<InvalidOperationException>(() => fixture.Output.OpenScope("no canvas")); Object.DestroyImmediate(fixture.Image.gameObject);
            }
        }
        [Test] public void ImageCallbacksCannotReenterOutputMutations()
        {
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("owner"))
            {
                var handle = owner.Play(Definition(true), 1); handle.Poll(.25); Exception reentry = null;
                UnityAction callback = () => { try { fixture.Output.OpenScope("reentrant"); } catch (Exception exception) { reentry = exception; } };
                fixture.Image.RegisterDirtyVerticesCallback(callback); fixture.Output.ApplyNow(); fixture.Image.UnregisterDirtyVerticesCallback(callback);
                Assert.That(reentry, Is.TypeOf<InvalidOperationException>()); Assert.That(fixture.Output.IsAvailable, Is.True); Assert.That(fixture.Image.color.a, Is.EqualTo(.5f));
            }
        }
        [UnityTest] public IEnumerator DisableAndReenableRaisedInsideAnImageCallbackFinishesCleanup() => CallbackClose(false);
        [UnityTest] public IEnumerator DestroyRaisedInsideAnImageCallbackFinishesCleanup() => CallbackClose(true);
        static IEnumerator CallbackClose(bool destroy)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("owner"))
            {
                var handle = owner.Play(Definition(true), 1); handle.Poll(.25); bool close = true;
                UnityAction callback = () => {
                    if (!close) return; close = false;
                    if (destroy) Object.DestroyImmediate(fixture.Output);
                    else { fixture.Output.enabled = false; fixture.Output.enabled = true; }
                };
                fixture.Image.RegisterDirtyVerticesCallback(callback); fixture.Output.ApplyNow(); fixture.Image.UnregisterDirtyVerticesCallback(callback);
                LogAssert.NoUnexpectedReceived();
                Assert.That(handle.Status, Is.EqualTo(FadeStatus.Cancelled)); Assert.That(owner.IsActive, Is.False); Assert.That(fixture.Image.color.a, Is.EqualTo(.125f));
                if (!destroy) Assert.That(fixture.Output.IsAvailable, Is.True);
            }
        }
    }
}
