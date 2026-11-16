using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat.Contracts;
using ForestCombatV2;
using UnityEngine;

namespace Stage1VillageExperiment
{
    // Explicit test opt-in. Ordinary play never changes input, enemies or position.
    public sealed class VillageTeamMapSmoke : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public bool pass,geometryUnchanged,actorsUnchanged,platformsUnchanged,blueprintsWork,mapWorks,attackWorks,jumpLanded;
            public bool displayChecked,displayValid;
            public int seed,rooms,combatRooms,actors,surfaces,props;
            public float ordinaryJumpPixels;
            public string error;
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--team-map-smoke-output");
            if(at<0||at+1>=args.Length){enabled=false;yield break;}
            string output=args[at+1];float deadline=Time.realtimeSinceStartup+90;
            var world=GetComponent<ForestCombatWorld>();var art=GetComponent<VillagePresentation>();
            while((!world.Ready||!art.Ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            Report report;
            try{report=Check(world,art,output);}catch(Exception e){report=new Report{error=e.ToString()};Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));Debug.LogException(e);}
            // Start can finish before Unity's splash screen and the first native frame.
            // Offscreen camera renders do not prove that the actual display is visible.
            var viewport=GetComponent<VillagePixelViewport>();
            deadline=Time.realtimeSinceStartup+30;
            while((!UnityEngine.Rendering.SplashScreen.isFinished||!viewport||!viewport.Ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            yield return new WaitForEndOfFrame();
            report.displayChecked=true;
            Texture2D screen=null;
            try
            {
                screen=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output,"displayed-game.png"),screen.EncodeToPNG());
                var pixels=screen.GetPixels32();int visible=0,bright=0,min=255,max=0;
                for(int i=0;i<pixels.Length;i+=13)
                {
                    var p=pixels[i];int value=(p.r+p.g+p.b)/3;
                    if(value>16)visible++;if(value>160)bright++;min=Math.Min(min,value);max=Math.Max(max,value);
                }
                float samples=(pixels.Length+12)/13;
                report.displayValid=viewport&&viewport.Ready&&visible/samples>.2f&&bright/samples>.02f&&max-min>50;
                if(!report.displayValid)report.error="Displayed frame is blank, uniform, or the native viewport is not ready.";
            }
            catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
            finally{if(screen)Destroy(screen);}
            report.pass&=report.displayValid;
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("VILLAGE_TEAM_DISPLAY_CHECK "+JsonUtility.ToJson(report));
            Application.Quit(report.pass?0:1);
        }
        public static Report Check(ForestCombatWorld world,VillagePresentation art,string output)
        {
            Directory.CreateDirectory(output);
            if(!world||!world.Ready||!art||!art.Ready)throw new InvalidOperationException("Team map artwork did not initialize.");
            var expected=Chapter1ProceduralMap.Generate(JsonUtility.FromJson<ForestData>(world.worldJson.text),
                JsonUtility.FromJson<RoundedTerrainLayout>(world.roundedTerrainJson.text),world.Data.generationSeed);
            var data=world.Data;
            world.GetComponent<IChapter1MonsterContent>()?.Prepare(expected);
            var report=new Report{seed=data.generationSeed,rooms=data.rooms.Length,combatRooms=data.rooms.Count(r=>r.kind=="combat"),
                actors=data.actors.Length,surfaces=world.SurfaceCount,props=art.PropCount,
                geometryUnchanged=data.ground.SequenceEqual(expected.ground)&&JsonUtility.ToJson(new RectList{items=data.rects})==JsonUtility.ToJson(new RectList{items=expected.rects}),
                actorsUnchanged=JsonUtility.ToJson(new ActorList{items=data.actors})==JsonUtility.ToJson(new ActorList{items=expected.actors}),
                platformsUnchanged=JsonUtility.ToJson(new PlatformList{items=data.platforms})==JsonUtility.ToJson(new PlatformList{items=expected.platforms})};
            foreach(var enemy in world.arena.enemies){enemy.aggressive=false;enemy.CancelAttack();}
            var transition=art.GetComponent<VillageArtTransition>();art.animateEnvironment=false;
            var player=world.arena.Player;player.UseTestInput=true;
            using(var sim=world.arena.AcquireManualSimulation())
            {
                void Draw(){world.RefreshCamera(0,true);art.RefreshForCapture();world.arena.Presentation.Draw(0);world.arena.RangedPresentation?.Tick();}
                var offers=world.GetComponent<Chapter1StartingBlueprints>();
                report.mapWorks=world.FullMap.IsRoomDiscovered(0)&&!world.FullMap.IsRoomDiscovered(1);
                Draw();Save(world.View,Path.Combine(output,"start-authored.png"));
                player.Position=offers.OfferPosition(0);bool first=offers.TryChoose(0);
                player.Position=offers.OfferPosition(1);bool second=offers.TryChoose(1);
                report.blueprintsWork=first&&second&&offers.Complete&&offers.RemainingCount==0;
                world.Warp(0);for(int i=0;i<30;i++)sim.Step(1f/120,new CombatInput(0));
                float floor=player.Position.y,high=floor;
                for(int i=0;i<180;i++){sim.Step(1f/120,new CombatInput(0,i==0?CombatButtons.Jump:CombatButtons.None));high=Mathf.Max(high,player.Position.y);}
                report.ordinaryJumpPixels=(high-floor)*32;report.jumpLanded=player.grounded;
                sim.Step(1f/120,new CombatInput(pressed:CombatButtons.Basic));report.attackWorks=player.currentCast&&player.currentCast.isBasic;
                player.CancelAction();
                foreach(int zone in new[]{0,1,4,9,11,14})
                {
                    world.Warp(zone);world.FullMap.SendMessage("Update");Draw();
                    foreach(var mode in new[]{VillageArtMode.Color,VillageArtMode.Sketch})
                    {transition.SetDisplay(mode);Save(world.View,Path.Combine(output,"room-"+zone.ToString("00")+"-"+mode.ToString().ToLowerInvariant()+".png"));}
                }
                report.mapWorks&=world.FullMap.IsRoomDiscovered(1);
                world.FullMap.SetOpen(true);report.mapWorks&=world.FullMap.IsOpen&&world.arena.Controls.Current.SimulationPaused;
                world.FullMap.SetOpen(false);report.mapWorks&=!world.arena.Controls.Current.SimulationPaused;
                world.Warp(0);transition.UseAuthoredRegions();Draw();
            }
            report.pass=report.geometryUnchanged&&report.actorsUnchanged&&report.platformsUnchanged&&report.blueprintsWork&&report.mapWorks&&report.attackWorks&&report.jumpLanded&&Mathf.Abs(report.ordinaryJumpPixels-96)<2&&report.rooms==14&&report.combatRooms==10;
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            if(!report.pass)throw new InvalidOperationException("Team map checks failed: "+JsonUtility.ToJson(report));
            Debug.Log("VILLAGE_TEAM_MAP_PASSED "+JsonUtility.ToJson(report));return report;
        }
        [Serializable] sealed class RectList{public GroundRect[] items;}
        [Serializable] sealed class ActorList{public MonsterPlacement[] items;}
        [Serializable] sealed class PlatformList{public FoliagePlatform[] items;}
        static void Save(Camera camera,string path)
        {
            var old=camera.targetTexture;var rect=camera.rect;float aspect=camera.aspect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try{rt.filterMode=FilterMode.Point;camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.aspect=960f/540;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.rect=rect;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);DestroyImmediate(image);}
        }
    }
}
