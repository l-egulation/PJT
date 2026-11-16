using Deviant.Presentation.Unity;
using NUnit.Framework;
using Stage1VillageExperiment;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1DisplayAlignmentTests
    {
        [Test] public void WorldAnchorsFollowPresentationAcrossWindowSizesAndCaptureFallback()
        {
            var root = new GameObject("Display alignment test");
            var camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0,0,-10);
            var texture = new RenderTexture(960,540,16);
            var capture = new RenderTexture(320,180,16);
            try
            {
                camera.targetTexture = texture;
                var binding = root.AddComponent<CameraDisplayProjection>();
                binding.PresentedTexture = texture;
                foreach (var size in new[]{new Vector2Int(2222,922),new Vector2Int(1920,1080),new Vector2Int(701,499)})
                {
                    var rect = VillagePixelViewport.CalculateDisplayRect(size.x,size.y);
                    binding.DisplayRect = () => rect;
                    AssertProjection(camera,rect);
                }
                camera.targetTexture = capture;
                Assert.That(CameraDisplayProjection.WorldToScreenPoint(camera,Vector3.zero),
                    Is.EqualTo(camera.WorldToScreenPoint(Vector3.zero)),"External capture must bypass native mapping");
                camera.targetTexture = null;
                Assert.That(CameraDisplayProjection.WorldToScreenPoint(camera,Vector3.zero),
                    Is.EqualTo(camera.WorldToScreenPoint(Vector3.zero)),"Direct rendering fallback");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(capture);
            }
        }

        public static void AssertProjection(Camera camera, Rect rect)
        {
            foreach (var uv in new[]{new Vector2(.2f,.3f),new Vector2(.8f,.7f),new Vector2(.5f,.9f)})
            {
                var world = camera.ViewportToWorldPoint(new Vector3(uv.x,uv.y,10));
                var screen = CameraDisplayProjection.WorldToScreenPoint(camera,world);
                Assert.That(screen.x,Is.EqualTo(rect.x+uv.x*rect.width).Within(.02f));
                Assert.That(Screen.height-screen.y,Is.EqualTo(rect.yMax-uv.y*rect.height).Within(.02f));
                Assert.That(CameraDisplayProjection.TryScreenToWorldPoint(camera,screen,out var back),Is.True);
                Assert.That(Vector3.Distance(back,world),Is.LessThan(.002f));
            }
            Assert.That(CameraDisplayProjection.TryScreenToWorldPoint(camera,
                new Vector3(rect.x-1,Screen.height-rect.center.y,10),out _),Is.False,"Letterbox must not place monsters");
        }
    }
}
