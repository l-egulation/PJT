using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class PixelFramedPanelTests
    {
        static void AssertConnected(Func<int,int,bool> light,int width,int height,Func<int,int,bool> region,bool diagonal)
        {
            var all=Enumerable.Range(0,width*height).Where(i=>region(i%width,i/width)&&light(i%width,i/width)).ToArray();
            Assert.That(all.Length,Is.GreaterThan(0));
            var seen=new System.Collections.Generic.HashSet<int>();var pending=new System.Collections.Generic.Queue<int>();
            seen.Add(all[0]);pending.Enqueue(all[0]);
            while(pending.Count>0)
            {
                int at=pending.Dequeue(),x=at%width,y=at/width;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dy==0||!diagonal&&dx!=0&&dy!=0)continue;
                    int nx=x+dx,ny=y+dy;
                    if(nx>=0&&nx<width&&ny>=0&&ny<height&&region(nx,ny)&&light(nx,ny)&&seen.Add(ny*width+nx))pending.Enqueue(ny*width+nx);
                }
            }
            Assert.That(seen.Count,Is.EqualTo(all.Length),diagonal?"The full outline remains connected.":"Orthogonal stair elbows must share horizontal/vertical pixel edges.");
        }
        [TestCase(960, 540, 224, 187, 1, 1)]
        [TestCase(960, 540, 480, 336, 2, 1)]
        [TestCase(1280, 720, 224, 187, 1, 1)]
        [TestCase(1280, 720, 480, 336, 2, 1)]
        [TestCase(1920, 1080, 224, 187, 1, 1)]
        [TestCase(1920, 1080, 480, 336, 2, 1)]
        [TestCase(1920, 1080, 448, 374, 1, 2)]
        [TestCase(1920, 1080, 1072, 624, 2, 2)]
        public void OutlineRemainsUnbrokenAndHasEqualIntegerThicknessAtFractionalPositions(int width, int height, int panelWidth, int panelHeight, int baseThickness, int referenceScale)
        {
            var cameraNode = new GameObject("Frame raster test camera");
            var canvasNode = new GameObject("Frame raster test canvas", typeof(RectTransform), typeof(Canvas));
            var camera = cameraNode.AddComponent<Camera>();
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; camera.aspect = 16f / 9;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(22, 28, 28, 255);
                var canvas = canvasNode.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                var stage = new GameObject("Reference stage", typeof(RectTransform)).GetComponent<RectTransform>();
                stage.SetParent(canvas.transform, false); stage.anchorMin = stage.anchorMax = stage.pivot = Vector2.one * .5f;
                stage.sizeDelta = new Vector2(960, 540) * referenceScale; stage.localScale = Vector3.one * (width / (960f * referenceScale));
                var rect = new GameObject("Pixel frame", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(stage, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(368.35f, -176.68f) * referenceScale; rect.sizeDelta = new Vector2(panelWidth, panelHeight);
                var theme = Type.GetType("Deviant.UI.Theme.ApprovedTheme, Deviant.UI.Theme", true);
                var source = (Sprite)theme.GetProperty("FrameLarge").GetValue(null);
                Assert.That(source, Is.Not.Null, "Use the original deployed texture.");
                var type = Type.GetType("Deviant.UI.Theme.PixelFramedPanel, Deviant.UI.Theme", true);
                var frame = rect.gameObject.AddComponent(type);
                type.GetMethod("SetReferenceScale").Invoke(frame, new object[] { (float)referenceScale });
                type.GetMethod("Configure").Invoke(frame, new object[] { source });
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                var pixels = image.GetPixels32();
                Func<int, int, bool> light = (x, y) => { var p = pixels[y * width + x]; return p.r > 190 && p.g > 190 && p.b > 180; };
                int left = width, right = -1, bottom = height, top = -1;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    if (light(x, y)) { left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y); }
                Assert.That(right, Is.GreaterThan(left), "The actual frame must render.");
                int middleX = (left + right) / 2, middleY = (top + bottom) / 2;
                Assert.That(light(left,top) && light(right,top) && light(left,bottom) && light(right,bottom),Is.True,"All four approved square corners must be filled, without clipping or rounding.");
                int expected = Math.Max(1, Mathf.RoundToInt(baseThickness * width / 960f));
                int topThickness = 0; for (int y = top; y >= bottom && light(middleX, y); y--) topThickness++;
                int leftThickness = 0; for (int x = left; x <= right && light(x, middleY); x++) leftThickness++;
                Assert.That(topThickness, Is.EqualTo(expected), "Horizontal stroke stays on whole screen pixels.");
                Assert.That(leftThickness, Is.EqualTo(expected), "Vertical and horizontal strokes keep the same thickness.");
                for (int y = bottom; y <= top; y++)
                    Assert.That(Enumerable.Range(left, right - left + 1).Any(x => light(x, y)), Is.True, "No missing outline row at " + y);
                int extent=Mathf.RoundToInt(12*width/960f);
                AssertConnected(light,width,height,(x,y)=>true,false);
                AssertConnected(light,width,height,(x,y)=>x<left+extent && y>top-extent,false);
                AssertConnected(light,width,height,(x,y)=>x>right-extent && y<bottom+extent,false);
                var interior = Enumerable.Range(middleY - 10, 20).SelectMany(y => Enumerable.Range(middleX - 20, 40).Select(x => pixels[y * width + x])).Distinct().Count();
                Assert.That(interior, Is.GreaterThan(10), "The painted texture is retained, rather than replaced with a flat fill.");
                string output = Path.GetFullPath("../outputs/ui-pixel-frame-20261115"); Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, (panelWidth==224?"isolated-frame-":"isolated-frame-"+panelWidth+"x"+panelHeight+"-") + width + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image); Object.DestroyImmediate(canvasNode); Object.DestroyImmediate(cameraNode);
            }
        }
    }
}
