using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ApprovedVillageIntegrationTests
    {
        [Test] public void ApprovedSpritesAndLayoutHaveNativeUnits()
        {
            var layout=JsonUtility.FromJson<StartingVillageWorld.Layout>(Resources.Load<TextAsset>("HubLayout").text);
            Assert.That(layout.worldWidthUnits,Is.EqualTo(44),"the compact four-station village (data/assets/starting_hub.json)");
            Assert.That(layout.buildings.Any(b=>b.id=="departure_gate"),Is.False);
            Assert.That(layout.stations.Single(s=>s.id=="departure_sign").x,Is.EqualTo(36f));
            var desk=Resources.Load<Sprite>("Hub/props/sketch/record-desk");
            Assert.That(desk.rect.width,Is.EqualTo(92),"the pixel v2 desk (register_stage1_hub_pixel_v2.py)");Assert.That(desk.pixelsPerUnit,Is.EqualTo(32));Assert.That(desk.pivot.y,Is.EqualTo(0));
            // The village backdrop pairs (pixel bands, sky, clouds, birds): both states share every picture's size.
            foreach(var key in new[]{"background/mountains","background/far-village","background/near-village","sky/clouds","sky/sky","sky/bird"}){
                var color=Resources.Load<Sprite>("Hub/pixel-village/"+key.Replace("/","/color/"));var sketch=Resources.Load<Sprite>("Hub/pixel-village/"+key.Replace("/","/sketch/"));
                Assert.That(color,Is.Not.Null,key);Assert.That(sketch,Is.Not.Null,key);Assert.That(color.rect.size,Is.EqualTo(sketch.rect.size),key);
            }
            Assert.That(Resources.Load<Sprite>("Chapter1Art/InkAltar/altar-before").rect.width,Is.EqualTo(48));
            Assert.That(Resources.Load<Sprite>("Chapter1Art/Exit/exit-open").rect.width,Is.EqualTo(128));
        }

        // Clouds and birds move only in colour; in the drained (sketch) village they stand still (user 2026-11-12).
        [UnityTest] public IEnumerator CloudsAndBirdsStandStillInSketchAndMoveInColour()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            StartingVillageWorld v=null;
            for(int i=0;i<600&&!(v&&v.Ready);i++){yield return null;v=Object.FindAnyObjectByType<StartingVillageWorld>();}
            Assert.That(v&&v.Ready,Is.True);
            // The clouds' and birds' own clocks (their positions also follow the camera's parallax).
            const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
            float Clouds()=>((IEnumerable)typeof(StartingVillageWorld).GetField("livingClouds",F).GetValue(v))
                .Cast<object>().Sum(c=>(float)c.GetType().GetField("clock").GetValue(c));
            float[] Birds()=>((float[])typeof(StartingVillageWorld).GetField("birdClock",F).GetValue(v)).ToArray();
            v.SetSketch(true);
            yield return null;yield return null;
            float clouds=Clouds();var birds=Birds();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(Clouds(),Is.EqualTo(clouds),"sketch clouds stand still");
            Assert.That(Birds(),Is.EqualTo(birds),"sketch birds stand still");
            v.SetSketch(false);
            yield return null;
            clouds=Clouds();birds=Birds();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(Clouds(),Is.GreaterThan(clouds),"colour clouds drift");
            Assert.That(Birds()[0],Is.GreaterThan(birds[0]),"colour birds fly");
            v.ClearPreview();
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator PreparationDepartureLeavesCameraBeforeFade() => PreparationDeparture(false);
        [UnityTest] public IEnumerator PreparationDepartureWithAnExistingOpeningRecord() => PreparationDeparture(true);

        static IEnumerator PreparationDeparture(bool returning)
        {
            EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();Chapter1ProfileHost.UseStore(returning ? new MemoryProfileStore(new ProfileData {
                profileId=System.Guid.NewGuid().ToString(),runsStarted=3,pigment=1,
                seenStories=new[]{"opening","record-journal-written-0","guide-ink"}
            }) : new MemoryProfileStore());
            StartingVillageWorld v=null;
            for(int i=0;i<1200&&!(v&&v.Ready);i++){v=Object.FindAnyObjectByType<StartingVillageWorld>();yield return null;}
            Assert.That(v&&v.Ready,Is.True);
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="TemporaryGameGui");
            const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
            yield return null;yield return null;
            gui.GetType().GetField("storyChecked",F).SetValue(gui,false);
            gui.GetType().GetMethod("CheckFirstVisitStory",F).Invoke(gui,null);
            gui.GetType().GetMethod("SkipOpening",F).Invoke(gui,null);
            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("opening"),Is.True,"the fresh in-memory opening was skipped");
            for(int i=0;i<100&&(bool)gui.GetType().GetField("storyActive",F).GetValue(gui);i++)gui.GetType().GetMethod("AdvanceStory",F).Invoke(gui,null);
            yield return null;yield return null;
            var viewport=Object.FindAnyObjectByType<Stage1VillageExperiment.VillagePixelViewport>();
            viewport.enableInBatchMode=true;viewport.enabled=true;
            for(int i=0;i<30&&!viewport.Ready;i++)yield return null;
            Assert.That(viewport.Ready,Is.True,"departure uses the same native camera output as interactive play");
            Assert.That(v.Stations.Count,Is.EqualTo(4));
            Assert.That(v.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.name=="Record farmhouse"),Is.EqualTo(1));
            Assert.That(v.Find("return_well").Position.x,Is.EqualTo(14.5f));
            var player=v.World.arena.Player;
            foreach(var station in v.Stations)
            {
                player.Position=station.Position;Physics2D.SyncTransforms();yield return null;yield return null;
                if(!v.StationOpen(station))continue;
                Assert.That(v.HighlightedStation,Is.SameAs(station),"highlight exactly the F target "+station.Id);
            }
            player.Position=v.Find("return_well").Position;Physics2D.SyncTransforms();v.World.RefreshCamera(0,true);
            yield return null;yield return null;
            v.SetSketch(true);yield return null;Capture(v.World.View,"interaction-outline-sketch");
            int highlighted=WhitePixels(v.World.View);
            var pause=v.World.arena.Controls.OpenScope("outline visibility check");pause.PauseSimulation();
            yield return null;yield return null;
            Assert.That(v.HighlightedStation,Is.Null,"no usable outline behind a modal or story");
            int hidden=WhitePixels(v.World.View);Assert.That(highlighted-hidden,Is.GreaterThan(30),"native render contains a strong white silhouette border");
            pause.Dispose();yield return null;yield return null;
            v.SetSketch(false);yield return null;Capture(v.World.View,"interaction-outline-color");
            player.Position=new Vector2(18,-14);Physics2D.SyncTransforms();yield return null;yield return null;
            Assert.That(v.HighlightedStation,Is.Null,"no outline outside interaction reach");
            player.Position=v.Find(StartingVillageWorld.DepartureSign).Position;Physics2D.SyncTransforms();v.World.RefreshCamera(0,true);
            yield return null;yield return null;
            Directory.CreateDirectory("../outputs/approved-hub/captures");
            // Camera captures are separate: a live pixel viewport owns its render target while walking.
            Assert.That(v,Is.Not.Null,"village before departure");
            Assert.That(v.World,Is.Not.Null,"world before departure");
            Assert.That(v.World.View,Is.Not.Null,"camera before departure");
            var camera=v.World.View;
            Assert.That(camera.transform,Is.Not.Null,"camera transform before departure");
            var locked=camera.transform.position;
            float right=locked.x+camera.orthographicSize*camera.aspect;
            Assert.That(v.TryInteract(), Is.True);
            Assert.That(v.Departing,Is.True);Assert.That(v.StartDeparture(()=>{}),Is.False);
            float fromX=player.Position.x;
            for(int i=0;i<10&&player.Position.x<=fromX+.1f;i++)yield return null;
            Assert.That(player.Position.x,Is.GreaterThan(fromX+.1f),$"departure starts walking: blocked={player.InputBlocked} paused={v.World.arena.Controls.Current.SimulationPaused} velocity={player.velocity}");
            float start=Time.realtimeSinceStartup;
            while(!ChapterSceneFade.Busy&&Time.realtimeSinceStartup-start<15){Assert.That(Vector3.Distance(camera.transform.position,locked),Is.LessThan(.02f));yield return null;}
            Assert.That(ChapterSceneFade.Busy,Is.True,$"player walks fully outside before loading: position={player.Position} right={right} velocity={player.velocity} blocked={player.InputBlocked} paused={v.World.arena.Controls.Current.SimulationPaused}");Assert.That(player.Position.x,Is.GreaterThanOrEqualTo(right+4));
            // The loading goes to chapter 1 with the hero as the village drew it: the bare-handed body.
            Assert.That(ChapterSceneFade.ShowingPlace,Is.EqualTo(LoadingPlace.Chapter1));
            Assert.That(ChapterSceneFade.ShowingHeroForm,Is.EqualTo(v.VillageBody));
            for(float deadline=Time.realtimeSinceStartup+30;UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=Chapter1RunHost.ChapterScene&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,Is.EqualTo(Chapter1RunHost.ChapterScene),"the preparation button loads chapter 1");
            Chapter1ProfileHost.UseStore(null);yield return new ExitPlayMode();
        }

        static int WhitePixels(Camera camera)
        {
            var rt=RenderTexture.GetTemporary(960,540,24);var old=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try
            {
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                return image.GetPixels32().Count(c=>c.r>248&&c.g>248&&c.b>248);
            }
            finally{Object.Destroy(image);camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }

        public static void Capture(Camera camera,string name)
        {
            Directory.CreateDirectory("../outputs/approved-hub/captures");
            var rt=RenderTexture.GetTemporary(960,540,24);var old=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var im=new Texture2D(960,540,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,960,540),0,0);im.Apply();
            File.WriteAllBytes("../outputs/approved-hub/captures/"+name+".png",im.EncodeToPNG());Object.Destroy(im);
            camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
        }
    }
}
