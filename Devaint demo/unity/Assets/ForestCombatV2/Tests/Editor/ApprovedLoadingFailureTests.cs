using System;
using System.Reflection;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ForestCombatV2.Tests
{
    public sealed class ApprovedLoadingFailureTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Test]
        public void LoadingViewPreservesApprovedFontSmoothSampling()
        {
            WithFailureView((canvas,stage)=>
            {
                var approved=Resources.Load<Font>("ApprovedTheme/Fonts/MemomentKkukkukk");
                foreach(var text in stage.GetComponentsInChildren<Text>())
                    Assert.That(text.font,Is.SameAs(approved));
                Assert.That(approved.material.mainTexture.filterMode,Is.EqualTo(FilterMode.Bilinear));
            });
        }
        [Test]
        public void ConfirmedLoadExceptionCanRetryWithoutReplacingThePendingAction()
        {
            var go=new GameObject("loading failure test");go.SetActive(false);
            try
            {
                var fade=go.AddComponent<ChapterSceneFade>();var type=typeof(ChapterSceneFade);int attempts=0;
                Action load=()=>{attempts++;if(attempts==1)throw new InvalidOperationException("fixture confirmed failure");};
                var invoke=type.GetMethod("TryLoad",Private);var failed=type.GetField("failed",Private);
                Assert.That(invoke.Invoke(fade,new object[]{load}),Is.EqualTo(false));
                Assert.That(failed.GetValue(fade),Is.EqualTo(true));
                type.GetMethod("Retry",Private).Invoke(fade,null);
                Assert.That(failed.GetValue(fade),Is.EqualTo(false));
                Assert.That(invoke.Invoke(fade,new object[]{load}),Is.EqualTo(true));
                Assert.That(attempts,Is.EqualTo(2));
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]
        public void FailureViewKeepsTextAndKeycapsInsideTheApprovedContentBlock()
        {
            WithFailureView((canvas, stage) =>
            {
                var content=(RectTransform)stage.Find("Loading failure/Failure content");
                var title=content.Find("Failure heading").GetComponent<Text>();
                var body=content.Find("Failure description").GetComponent<Text>();
                Assert.That(title.alignment,Is.EqualTo(TextAnchor.MiddleLeft));
                Assert.That(body.preferredHeight,Is.LessThanOrEqualTo(body.rectTransform.rect.height));
                var buttons=content.GetComponentsInChildren<Button>();
                Assert.That(buttons.Length,Is.EqualTo(2));
                var firstBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(content,buttons[0].transform);
                var secondBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(content,buttons[1].transform);
                Assert.That(firstBounds.max.x,Is.LessThan(secondBounds.min.x),"confirmation actions must not overlap");
                foreach(var button in buttons.Where(b=>b.transform.Find("Keycap")))
                {
                    var cap=(RectTransform)button.transform.Find("Keycap");
                    var key=cap.Find("Key").GetComponent<Text>();
                    Assert.That(key.alignment,Is.EqualTo(TextAnchor.MiddleCenter));
                    Assert.That(key.alignByGeometry,Is.True);
                    Assert.That(key.preferredWidth,Is.LessThan(cap.rect.width));
                    Assert.That(key.rectTransform.rect.size,Is.EqualTo(cap.rect.size));
                    var label=button.transform.Find("Label").GetComponent<Text>();
                    Assert.That(label.rectTransform.anchoredPosition.x,Is.GreaterThan(cap.anchoredPosition.x+cap.rect.width));
                }
                Assert.That(buttons[0].transform.Find("Selection wash").GetComponent<Image>().sprite,Is.Not.Null);
                Assert.That(buttons[1].transform.Find("Selection wash").GetComponent<Image>().color.a,Is.EqualTo(.1f).Within(.001f));
                Assert.That(buttons[0].transform.Find("Keycap/Key").GetComponent<Text>().text,Is.EqualTo("F"));
                Assert.That(buttons[1].transform.Find("Keycap"),Is.Null,"Figma's title action has no G badge; its existing keyboard action stays available.");
                Assert.That(buttons.Select(b=>b.transform.Find("Label").GetComponent<Text>().text),Is.EqualTo(new[]{"다시 시도","타이틀로"}));
                foreach(var button in buttons)
                {
                    var label=button.transform.Find("Label").GetComponent<Text>();
                    Assert.That(label.preferredWidth,Is.LessThanOrEqualTo(label.rectTransform.rect.width+1),"Action captions must remain fully visible.");
                }
                var rect=content.rect;
                foreach(var child in content.GetComponentsInChildren<RectTransform>())
                {
                    if(child==content)continue;
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(content,child);
                    Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(rect.xMin-.1f),child.name);
                    Assert.That(bounds.max.x,Is.LessThanOrEqualTo(rect.xMax+.1f),child.name);
                    Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(rect.yMin-.1f),child.name);
                    Assert.That(bounds.max.y,Is.LessThanOrEqualTo(rect.yMax+.1f),child.name);
                }
            });
        }

        // Captures the real failure view without loading a scene or changing a run/profile.
        [TestCase(960,540)]
        [TestCase(1280,720)]
        [TestCase(1920,1080)]
        public void CaptureFailureViewForReview(int width,int height)
        {
            WithFailureView((canvas,stage)=>
            {
                var cameraObject=new GameObject("Loading failure review camera");var camera=cameraObject.AddComponent<Camera>();
                camera.enabled=false;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color32(27,29,32,255);camera.cullingMask=1<<31;
                var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                var image=new Texture2D(width,height,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
                try
                {
                    foreach(var child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                    camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=width/960f;
                    foreach(var text in canvas.GetComponentsInChildren<Text>()){text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);text.SetAllDirty();}
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                    var pixels=image.GetPixels32();var background=pixels[0];
                    Assert.That(pixels.Count(p=>p.r!=background.r||p.g!=background.g||p.b!=background.b),Is.GreaterThan(100),"the capture must contain the rendered failure UI");
                    const string directory="../outputs/loading-failure-review";Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory,$"loading-failed-{width}x{height}.png"),image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
                    UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(cameraObject);
                }
            });
        }

        static void WithFailureView(Action<Canvas,RectTransform> inspect)
        {
            var owner=new GameObject("Inactive loading controller");owner.SetActive(false);
            var root=new GameObject("Failure view canvas",typeof(RectTransform),typeof(Canvas));
            try
            {
                var fade=owner.AddComponent<ChapterSceneFade>();var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.pixelPerfect=true;
                var stage=new GameObject("Stage",typeof(RectTransform)).GetComponent<RectTransform>();stage.SetParent(root.transform,false);
                stage.anchorMin=stage.anchorMax=stage.pivot=Vector2.one*.5f;stage.sizeDelta=new Vector2(960,540);stage.anchoredPosition=Vector2.zero;
                var font=Resources.Load<Font>("ApprovedTheme/Fonts/MemomentKkukkukk");Assert.That(font,Is.Not.Null);
                typeof(ChapterSceneFade).GetMethod("BuildFailure",Private).Invoke(fade,new object[]{stage,font});
                stage.Find("Loading failure").gameObject.SetActive(true);Canvas.ForceUpdateCanvases();inspect(canvas,stage);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(owner);}
        }

        [Test]
        public void ReadinessStillPendingIsNotReportedAsFailure()
        {
            var go=new GameObject("pending loading test");go.SetActive(false);
            try
            {
                var fade=go.AddComponent<ChapterSceneFade>();var type=typeof(ChapterSceneFade);
                Assert.That(type.GetMethod("IsReady",Private).Invoke(fade,new object[]{(Func<bool>)(()=>false)}),Is.EqualTo(false));
                Assert.That(type.GetField("failed",Private).GetValue(fade),Is.EqualTo(false));
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
