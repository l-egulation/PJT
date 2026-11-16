using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Deviant.Presentation.Tests
{
    public sealed class HitFeedbackGraphicsTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Category("RequiresGraphics")]public IEnumerator Actual2DRendererDistortsTransparentWorldAndLeavesExternalTargetAndHudAlone()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var cameraRoot=new GameObject("World camera");var camera=cameraRoot.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3;
            // Match the native village viewport: no HDR, MSAA or post processing
            // can incidentally allocate the intermediate color required by the pass.
            camera.allowHDR=false;camera.allowMSAA=false;
            camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.08f,.12f);
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)pixels[y*128+x]=new Color(x%16<8?1:.05f,y%16<8?.8f:.1f,.2f,.8f);
            texture.SetPixels(pixels);texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,128,128),Vector2.one*.5f,32);
            var body=new GameObject("Transparent checker Sprite").AddComponent<SpriteRenderer>();body.sprite=sprite;
            var rt=new RenderTexture(960,540,24){name="External capture-owned world target"};rt.Create();camera.targetTexture=rt;
            var canvas=new GameObject("HUD overlay",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var output=new GameObject("World frame",typeof(RawImage)).GetComponent<RawImage>();output.transform.SetParent(canvas.transform,false);output.texture=rt;
            var rect=output.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var hud=new GameObject("Unwarped HUD",typeof(Image)).GetComponent<Image>();hud.transform.SetParent(canvas.transform,false);hud.color=Color.cyan;
            hud.rectTransform.sizeDelta=new Vector2(110,36);hud.rectTransform.anchoredPosition=new Vector2(0,100);
            // Explicit world-RT -> final display camera -> HUD composition. It lets
            // us read the final HUD pixels without relying on the desktop/game view.
            var displayCamera=new GameObject("Final display camera").AddComponent<Camera>();displayCamera.orthographic=true;displayCamera.cullingMask=1<<5;
            camera.cullingMask=~(1<<5);var final=new RenderTexture(960,540,24);final.Create();displayCamera.targetTexture=final;
            foreach(var graphic in canvas.GetComponentsInChildren<Graphic>())graphic.gameObject.layer=5;
            canvas.gameObject.layer=5;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=displayCamera;canvas.planeDistance=1;
            var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");Assert.That(bindings,Is.Not.Null);
            using(var distortion=new WorldDistortionOutput(camera,bindings.distortionShader,4,3))
            {
                yield return null;Canvas.ForceUpdateCanvases();camera.Render();displayCamera.Render();var baseline=Read(rt);var hudBefore=Read(final);
                Assert.That(baseline.Count(p=>p.r>100),Is.GreaterThan(1000),"The transparent sprite really rendered into the world frame.");
                Assert.That(distortion.Add(Vector2.zero,.15f,3,100,12),Is.True);distortion.Advance(.075f);camera.Render();var warped=Read(rt);
                int changed=baseline.Zip(warped,(a,b)=>!a.Equals(b)).Count(v=>v);
                Assert.That(distortion.RenderedFrames,Is.GreaterThan(0),distortion.LastError);Assert.That(changed,Is.GreaterThan(40),"The post-transparent pass must move actual Sprite pixels.");
                displayCamera.Render();var hudAfter=Read(final);
                var corners=new Vector3[4];hud.rectTransform.GetWorldCorners(corners);var lo=displayCamera.WorldToScreenPoint(corners[0]);var hi=displayCamera.WorldToScreenPoint(corners[2]);
                int hudPixels=0;
                for(int y=Mathf.CeilToInt(lo.y)+1;y<Mathf.FloorToInt(hi.y)-1;y++)for(int x=Mathf.CeilToInt(lo.x)+1;x<Mathf.FloorToInt(hi.x)-1;x++)
                {int index=y*final.width+x;Assert.That(hudBefore[index],Is.EqualTo(hudAfter[index]));Assert.That(hudAfter[index].g,Is.GreaterThan(200));Assert.That(hudAfter[index].b,Is.GreaterThan(200));hudPixels++;}
                Assert.That(hudPixels,Is.GreaterThan(100));Assert.That(camera.targetTexture,Is.SameAs(rt));Assert.That(displayCamera.targetTexture,Is.SameAs(final));
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../captures"));Directory.CreateDirectory(folder);Save(rt,Path.Combine(folder,"transparent-world-warp.png"));
                distortion.Intensity=0;camera.Render();Assert.That(Read(rt),Is.EqualTo(baseline),"Disabled output uses the original world frame without sampling an old RT.");
                Assert.That(hud.color,Is.EqualTo(Color.cyan));Save(rt,Path.Combine(folder,"transparent-world-original.png"));
            }
            displayCamera.targetTexture=null;final.Release();Object.Destroy(final);Object.Destroy(displayCamera.gameObject);
            camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(cameraRoot);Object.Destroy(body.gameObject);Object.Destroy(canvas.gameObject);Object.Destroy(sprite);Object.Destroy(texture);
        }
        [UnityTest,Category("RequiresGraphics")]public IEnumerator ConfiguredStrengthExceedsTheOldCeilingWhileOverlapAndIntensityStayBounded()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var camera=new GameObject("Distortion strength camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3;
            camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)pixels[y*128+x]=new Color(x%16<8?1:.05f,y%16<8?.8f:.1f,.2f,.8f);
            texture.SetPixels(pixels);texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,128,128),Vector2.one*.5f,32);
            var body=new GameObject("Strength checker Sprite").AddComponent<SpriteRenderer>();body.sprite=sprite;
            var rt=new RenderTexture(960,540,24);rt.Create();camera.targetTexture=rt;
            var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");Assert.That(bindings,Is.Not.Null);
            try
            {
                yield return null;
                var clipped=RenderStrength(camera,bindings.distortionShader,3,10,1);
                var boosted=RenderStrength(camera,bindings.distortionShader,15,10,1);
                Assert.That(clipped.Zip(boosted,(a,b)=>!a.Equals(b)).Count(v=>v),Is.GreaterThan(40),"The boosted wave must move actual Sprite pixels beyond the former 3-pixel ceiling.");
                var overlap=RenderStrength(camera,bindings.distortionShader,15,10,4);
                var equivalent=RenderStrength(camera,bindings.distortionShader,15,40,1);
                AssertPixelsNear(overlap,equivalent,"Four coincident waves must share the same total displacement clamp.");
                var unbounded=RenderStrength(camera,bindings.distortionShader,100,40,1);
                Assert.That(overlap.Zip(unbounded,(a,b)=>!a.Equals(b)).Count(v=>v),Is.GreaterThan(40),"The configured total limit must still constrain overlapping waves.");
                var reduced=RenderStrength(camera,bindings.distortionShader,15,10,1,.5f);
                var halfStrength=RenderStrength(camera,bindings.distortionShader,15,5,1);
                AssertPixelsNear(reduced,halfStrength,"The existing intensity option must scale the boosted output.");
            }
            finally
            {
                camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(camera.gameObject);Object.Destroy(body.gameObject);Object.Destroy(sprite);Object.Destroy(texture);
            }
        }
        static Color32[] RenderStrength(Camera camera,Shader shader,float maximum,float pixels,int count,float intensity=1)
        {
            using(var distortion=new WorldDistortionOutput(camera,shader,4,maximum))
            {
                distortion.Intensity=intensity;
                for(int i=0;i<count;i++)Assert.That(distortion.Add(Vector2.zero,3,pixels,200,9),Is.True);
                distortion.Advance(1.5f);camera.Render();Assert.That(distortion.RenderedFrames,Is.GreaterThan(0),distortion.LastError);
                return Read(camera.targetTexture);
            }
        }
        static void AssertPixelsNear(Color32[] a,Color32[] b,string reason)
        {
            Assert.That(a.Zip(b,(x,y)=>Mathf.Abs(x.r-y.r)>2||Mathf.Abs(x.g-y.g)>2||Mathf.Abs(x.b-y.b)>2||Mathf.Abs(x.a-y.a)>2).Count(v=>v),Is.Zero,reason);
        }
        [UnityTest,Category("RequiresGraphics")]public IEnumerator LicensedParticlePrefabReallyRendersInTheOrthographic2DRenderer()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var camera=new GameObject("Particle camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2;camera.transform.position=new Vector3(0,0,-10);camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(480,270,24);rt.Create();camera.targetTexture=rt;var root=new GameObject("Particle fixture");
            var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");
            using(var factory=new PrefabCueFactory(root.transform,bindings,4,8192))
            {
                foreach(string key in new[]{"hit_slash","hit_blunt"})
                foreach(bool paper in new[]{false,true})
                {
                    factory.PaperInk=paper;
                    Deviant.Presentation.Contracts.CueViewResult result=null;var definition=factory.Definition(key);
                    factory.Capture(key,0,.35f,109).Begin(definition,new Deviant.Presentation.Contracts.CueAnchor(0,0,1),r=>result=r);
                    result.View.Sample(.08f);camera.Render();var pixels=Read(rt);
                    Assert.That(pixels.Count(p=>p.r>30||p.g>30||p.b>30),Is.GreaterThan(10),key+" must render actual visible particles, not merely load a prefab.");
                    string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../captures"));Directory.CreateDirectory(folder);Save(rt,Path.Combine(folder,key+(paper?"-paper":"")+".png"));result.View.Dispose();
                }
            }
            camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(root);Object.Destroy(camera.gameObject);
        }
        [UnityTest,Category("RequiresGraphics")]public IEnumerator PaperOutlineColorRendersWithHardPixelEdgesAtNativeAndScaledSizes()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var shader=Resources.Load<Shader>("HitFeedbackPaperInk");Assert.That(shader,Is.Not.Null);Assert.That(shader.isSupported,Is.True);
            var material=new Material(shader);material.SetColor("_OutlineColor",Color.red);material.SetFloat("_Core",0);
            var camera=new GameObject("Paper edge camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3;
            camera.transform.position=new Vector3(0,0,-10);camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.allowHDR=false;camera.allowMSAA=false;
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float dx=(x+.5f)/64-1,dy=(y+.5f)/64-1;
                pixels[y*128+x]=new Color(1,1,1,Mathf.Clamp01((.8f-Mathf.Abs(dx)-Mathf.Abs(dy)*.65f)*16));
            }
            texture.SetPixels(pixels);texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,128,128),Vector2.one*.5f,32);
            var body=new GameObject("Slanted paper particle silhouette").AddComponent<SpriteRenderer>();body.sprite=sprite;body.sharedMaterial=material;
            RenderTexture rt=null;
            try
            {
                yield return null;
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../captures"));Directory.CreateDirectory(folder);
                foreach(int height in new[]{540,1080})
                {
                    rt=new RenderTexture(height*16/9,height,24){antiAliasing=1};rt.Create();camera.targetTexture=rt;
                    camera.Render();var hard=Read(rt);
                    Assert.That(hard.Count(p=>p.r>240&&p.g<5&&p.b<5),Is.GreaterThan(30),"The configured red outline must be visible.");
                    Assert.That(hard.Count(p=>p.r>240&&p.g>240&&p.b>240),Is.GreaterThan(1000),"The light particle body stays visible.");
                    Assert.That(hard.Count(p=>p.r>5&&p.r<240&&p.g<5&&p.b<5),Is.Zero,"The outline keeps the hard pixel boundary.");
                    material.SetColor("_OutlineColor",new Color32(26,26,26,255));camera.Render();var gray=Read(rt);
                    Assert.That(gray.Count(p=>p.r>5&&p.r<150&&Mathf.Abs(p.r-p.g)<=1&&Mathf.Abs(p.g-p.b)<=1),Is.GreaterThan(30),"The default outline is a visible neutral dark gray.");
                    Save(rt,Path.Combine(folder,"paper-outline-gray-"+height+".png"));
                    material.SetColor("_OutlineColor",Color.blue);camera.Render();var blue=Read(rt);
                    Assert.That(blue.Count(p=>p.b>240&&p.r<5&&p.g<5),Is.GreaterThan(30),"The edge must use the selected blue tint.");
                    Assert.That(blue.Count(p=>p.r>240&&p.g<5&&p.b<5),Is.Zero,"Changing the outline must remove the old red edge.");
                    material.SetColor("_OutlineColor",Color.red);
                    camera.targetTexture=null;rt.Release();Object.Destroy(rt);rt=null;
                }
            }
            finally
            {
                camera.targetTexture=null;if(rt){rt.Release();Object.Destroy(rt);}
                Object.Destroy(body.gameObject);Object.Destroy(camera.gameObject);Object.Destroy(material);Object.Destroy(sprite);Object.Destroy(texture);
            }
        }
        static Color32[] Read(RenderTexture rt)
        {
            var previous=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();return image.GetPixels32();}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);}
        }
        static void Save(RenderTexture rt,string path)
        {
            var previous=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);}
        }
    }
}
