using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Chapter2.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    // 보스 앞 48u 통로: 시안 장식이 실제 통로 안에 놓이고 충돌을 더하지 않는다. 입구·가운데·끝 화면을 남긴다.
    public sealed class Chapter2BossHallTests
    {
        [UnityTest] public IEnumerator BossHallDecorSitsInsideTheHallWithoutCollision()
        {
            EditorSceneManager.playModeStartScene=null;Chapter2Tools.Create();
            Object.FindAnyObjectByType<Chapter2World>().seed=17;
            yield return new EnterPlayMode();
            for(int i=0;i<240&&!Object.FindAnyObjectByType<Chapter2World>().Ready;i++)yield return null;
            var world=Object.FindAnyObjectByType<Chapter2World>();Assert.That(world.Ready,Is.True);
            var hall=world.Layout.bossHall;Assert.That(hall,Is.Not.Null);
            var background=world.GetComponent<Chapter2WaterwayBackground>();
            // v26 화풍: 「보스 길목·보스방 시안」 2차(2026-11-12 OK)를 그린 뒷배경 한 장 + 바닥 격자 줄 한 장. 보스방 쪽이 어둡다.
            Assert.That(background.BossHallDecorCount,Is.EqualTo(2));
            var painted=background.PaintedHall;Assert.That(painted,Is.Not.Null);
            Assert.That((Vector2)painted.bounds.min,Is.EqualTo(new Vector2(hall.x,hall.y)));Assert.That((Vector2)painted.bounds.size,Is.EqualTo(new Vector2(48,6)));
            Assert.That(painted.flipX,Is.EqualTo(world.Layout.bossHallToLeft));Assert.That(painted.sortingOrder,Is.LessThan(0));
            var decor=background.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("Waterway boss hall")).ToArray();
            Assert.That(decor.Length,Is.EqualTo(2));
            foreach(var r in decor)
            {
                Assert.That(r.GetComponent<Collider2D>(),Is.Null,r.name);
                Assert.That(r.bounds.min.x,Is.EqualTo(hall.x).Within(.001f),r.name);Assert.That(r.bounds.max.x,Is.EqualTo(hall.x+hall.w).Within(.001f),r.name);
            }
            var grates=decor.Single(r=>r.name.EndsWith("floor grates"));Assert.That(grates.bounds.max.y,Is.EqualTo(hall.y-1/32f).Within(.001f));
            // 그림의 등 셋은 캐릭터 색 맞춤 빛 지도에 들어간다: 보스방 반대쪽(밝은 1구간)이 보스방 쪽(어두운 3구간)보다 밝다.
            var dressing=world.GetComponentInChildren<Chapter2SewerDressing>();
            Assert.That(world.Sewer.PaintedLamps.Count,Is.EqualTo(3));
            float far=world.Layout.bossHallToLeft?hall.x+hall.w-1:hall.x+1,near=world.Layout.bossHallToLeft?hall.x+3:hall.x+hall.w-3;
            Assert.That(dressing.LightAt(new Vector2(far,hall.y+3.3f)).L,Is.GreaterThan(dressing.LightAt(new Vector2(near,hall.y+3.3f)).L+.5f));
            string folder=Path.GetFullPath("../outputs/boss-hall-chapter2");Directory.CreateDirectory(folder);
            var player=world.arena.Player;
            foreach(var (t,name) in new[]{(.12f,"entry"),(.5f,"middle"),(.88f,"end")})
            {
                float x=world.Layout.bossHallToLeft?hall.x+hall.w*(1-t):hall.x+hall.w*t;
                player.Position=new Vector2(x,hall.y+.05f);Physics2D.SyncTransforms();
                for(int i=0;i<10;i++)yield return null;
                world.CameraStep(0,true);yield return null;yield return null;
                Capture(world.sceneCamera,Path.Combine(folder,"hall-"+name+".png"));
            }
            yield return new ExitPlayMode();
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
        static void Capture(Camera camera,string path)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            camera.aspect=16f/9;var prior=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.Destroy(tex);}
            finally{camera.ResetAspect();camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
