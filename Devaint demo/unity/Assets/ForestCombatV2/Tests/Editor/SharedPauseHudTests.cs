using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class SharedPauseHudTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        const string Approved="753bb6f7a1da38f91ad3ee30da560105ee18c4b85233f1201fdfc6ac40591ed2";
        static object Field(object gui,string name)=>gui.GetType().GetField(name,Hidden).GetValue(gui);
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        [UnityTest] public IEnumerator VillageAndBothChaptersShareApprovedPausePixelsAndResume()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            foreach(string scene in new[]{StartingVillageTests.ScenePath,Chapter1RunHost.ChapterScene,"Assets/Chapter2/Scenes/Chapter2Procedural.unity"})
            {
                SceneManager.LoadScene(scene);yield return null;
                MonoBehaviour gui=null;Button pause=null;
                for(int i=0;i<1200&&!pause;i++)
                {
                    yield return null;
                    gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b=>b.GetType().Name=="TemporaryGameGui");
                    if(gui)pause=gui.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="Pause");
                    if(scene==StartingVillageTests.ScenePath)
                    {
                        var lobby=Object.FindAnyObjectByType<StartingVillageWorld>();
                        if(!lobby||!lobby.Ready)pause=null;
                    }
                }
                Assert.That(pause,Is.Not.Null,scene);
                yield return null;yield return null;
                bool village=scene==StartingVillageTests.ScenePath;
                if(village)
                {
                    gui.GetType().GetMethod("SkipOpening",Hidden).Invoke(gui,null);
                    for(int i=0;i<30&&(bool)Field(gui,"storyActive");i++)gui.GetType().GetMethod("AdvanceStory",Hidden).Invoke(gui,null);
                    yield return null;
                }
                var image=pause.GetComponent<Image>();var texture=image.sprite.texture;
                Assert.That(texture.width,Is.EqualTo(34),scene);Assert.That(texture.height,Is.EqualTo(34),scene);
                Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point),scene);
                var pixels=texture.GetPixels32().SelectMany(p=>new[]{p.r,p.g,p.b,p.a}).ToArray();
                using(var sha=SHA256.Create())Assert.That(System.BitConverter.ToString(sha.ComputeHash(pixels)).Replace("-","").ToLowerInvariant(),Is.EqualTo(Approved),scene);
                if(village)
                {
                    Assert.That(pause.GetComponentsInChildren<Image>().Length,Is.EqualTo(1),"old separate pause bars are removed");
                    var rect=image.rectTransform;Assert.That(rect.sizeDelta,Is.EqualTo(new Vector2(34,34)));
                    Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(910,-14)),"right edge stays at game pixel 944");
                    CaptureVillage(gui,"village-hud");
                }
                bool Paused()=>village?(bool)Field(gui,"hubPaused"):(bool)gui.GetType().GetProperty("Presenter",Hidden).GetValue(gui).GetType().GetProperty("Paused").GetValue(gui.GetType().GetProperty("Presenter",Hidden).GetValue(gui));
                Assert.That(Paused(),Is.False,scene);
                pause.onClick.Invoke();Assert.That(Paused(),Is.True,scene+" pause click");
                if(village)CaptureVillage(gui,"village-paused");
                ((Button)Field(gui,"menuPrimary")).onClick.Invoke();Assert.That(Paused(),Is.False,scene+" resume click");
            }
        }

        static void CaptureVillage(MonoBehaviour gui,string name)
        {
            var camera=Object.FindAnyObjectByType<StartingVillageWorld>().World.View;var canvas=gui.GetComponent<Canvas>();
            var mode=canvas.renderMode;var view=canvas.worldCamera;float distance=canvas.planeDistance;
            var target=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+1;
                gui.GetType().GetMethod("FitToWorld").Invoke(gui,null);Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();Directory.CreateDirectory("../outputs/shared-pause-hud");
                File.WriteAllBytes("../outputs/shared-pause-hud/"+name+"-ui-render.png",image.EncodeToPNG());
            }
            finally{canvas.renderMode=mode;canvas.worldCamera=view;canvas.planeDistance=distance;camera.targetTexture=target;camera.rect=rect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
    }
}
