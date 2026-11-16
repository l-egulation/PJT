using System;
using System.Collections;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class FadeOutputLifecycleBoundaryTests
    {
        static FadeDefinition Fade(bool hold = true) => new FadeDefinition("fixture-callback-fade", .25f, .5f, .75f, 0, 1, .5, hold);
        static readonly FadeFrame Baseline = new FadeFrame(.1f, .2f, .3f, .4f);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static GameObject CreateImage(out Image image)
        {
            var canvas = new GameObject("FadeCallbackCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var graphic = new GameObject("FadeCallbackImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            graphic.transform.SetParent(canvas.transform, false); image = graphic.GetComponent<Image>(); image.raycastTarget = false;
            return canvas;
        }
        [UnityTest] public IEnumerator DestroyingTheOwnerInsideBaselineReturnClosesRetainedPlayback()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var canvas = CreateImage(out var image); var owner = new GameObject("FadeBaselineReturnOwner"); var binding = owner.AddComponent<FadeOutputBinding>();
            binding.Configure(image, new FadeFrame(0, 0, 0, 0)); var service = binding.Requests;
            try
            {
                using (var baseline = binding.BindBaseSource("fixture baseline")) using (var scope = service.OpenScope("fixture retained owner"))
                {
                    baseline.Submit(Baseline); var handle = scope.Play(Fade(), 1); handle.Poll(.5); binding.ApplyNow();
                    Assert.That(handle.Status, Is.EqualTo(FadeStatus.Completed)); Assert.That(handle.IsRetained, Is.True); bool destroyed = false;
                    image.RegisterDirtyVerticesCallback(() => {
                        if (destroyed) return; destroyed = true; Object.DestroyImmediate(owner);
                    });
                    binding.enabled = false; yield return null;
                    Assert.That(destroyed, Is.True); LogAssert.NoUnexpectedReceived();
                    Assert.That(service.IsAvailable, Is.False); Assert.That(service.RequestCount, Is.Zero); Assert.That(baseline.IsActive, Is.False);
                    Assert.That(handle.Status, Is.EqualTo(FadeStatus.Completed)); Assert.That(handle.IsRetained, Is.False);
                    handle.Poll(0); handle.Cancel(); Assert.That(handle.Status, Is.EqualTo(FadeStatus.Completed));
                    Assert.That(image.color, Is.EqualTo(new Color(Baseline.Red, Baseline.Green, Baseline.Blue, Baseline.Opacity)));
                    Assert.Throws<ObjectDisposedException>(() => service.OpenScope("fixture stale output"));
                }
            }
            finally { if (owner) Object.DestroyImmediate(owner); Object.DestroyImmediate(canvas); }
        }
    }
}
