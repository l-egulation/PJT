using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Editor
{
    [InitializeOnLoad]
    public static class Chapter1MapVer2Verification
    {
        const string Pending="Chapter1MapVer2Verification.Pending";
        static int phase,checks,landings,dropPasses,dropBlocked,upwardPasses;
        static double deadline;
        static double quietUntil;
        static string Output=>Path.GetFullPath("../outputs/chapter1-map-ver2/unity");
        static Chapter1MapVer2Verification()
        {
            EditorApplication.update+=Tick;
        }
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Chapter1MapSelector.Ver2Scene);}
        public static void Run()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1");
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(Chapter1MapSelector.GameScene);
            EditorSceneManager.OpenScene(Chapter1MapSelector.Ver2Scene);
            EditorSceneManager.OpenScene(Chapter1MapSelector.OriginalScene);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            phase=0;checks=0;landings=dropPasses=dropBlocked=upwardPasses=0;quietUntil=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Debug.Log("CHAPTER1_MAP_VER2 waiting for scene imports");SessionState.SetBool(Pending,true);
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating){quietUntil=0;return;}
                if(quietUntil==0){quietUntil=EditorApplication.timeSinceStartup+8;return;}
                if(EditorApplication.timeSinceStartup<quietUntil)return;
                deadline=EditorApplication.timeSinceStartup+180;EditorApplication.EnterPlaymode();return;
            }
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Map transition timed out");
                string scene=SceneManager.GetActiveScene().path;
                var selector=Object.FindFirstObjectByType<Chapter1MapSelector>();
                if(!selector)return;
                if(phase==3&&scene==Chapter1MapSelector.GameScene)
                {
                    var arena=Object.FindFirstObjectByType<CombatArena>();if(!arena||!arena.Player)return;
                    Check(Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None).Length==1,"Basic game has one arena");
                    Check(selector.Select(Chapter1MapSelector.Ver2Scene),"Basic game loads ver2");phase=4;return;
                }
                var world=Object.FindFirstObjectByType<ForestCombatWorld>();
                if(!world||!world.Ready)return;
                if(phase==0&&scene==Chapter1MapSelector.OriginalScene)
                {
                    Debug.Log("CHAPTER1_MAP_VER2 original ready");
                    Check(world.Data.views.Length==8,"Original map retains eight areas");
                    Check(selector.Select(Chapter1MapSelector.Ver2Scene),"Original button loads ver2");phase=1;return;
                }
                if(phase==1&&scene==Chapter1MapSelector.Ver2Scene)
                {
                    Debug.Log("CHAPTER1_MAP_VER2 ver2 ready");
                    Check(Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None).Length==1,"Only one combat arena after switch");
                    Check(world.Data.views.Length==24,"Ver2 has 24 spaces");
                    Check(world.Data.platforms.Count(p=>p.earthArtwork)==27,"27 earth-art platforms");
                    Check(world.Data.platforms.Count(p=>!p.earthArtwork)==295,"295 wood-art platforms");
                    var layout=JsonUtility.FromJson<RoundedTerrainLayout>(world.roundedTerrainJson.text);
                    Check(layout.geometrySha256==Chapter1RoundedTerrain.GeometryHash(world.Data),"Atlas geometry hash matches");
                    foreach(var enemy in world.arena.enemies)enemy.aggressive=false;
                    using(var manual=world.arena.AcquireManualSimulation())
                    {
                        for(int i=0;i<24;i++)
                        {
                            world.arena.Player.ResetPlayer();world.Warp(i);
                            for(int step=0;step<20;step++)manual.Step(1f/120);
                            Check(world.arena.Player.grounded,"Safe landing in space "+(i+1));
                            world.RefreshCamera(0,true);Check(world.BackgroundCoversBottom(out _),"Background covers space "+(i+1));
                            Capture(world.View,"area-"+(i+1));
                        }
                        foreach(var p in world.Data.platforms)VerifyPlatform(world,manual,p);
                        Check(dropBlocked==27&&dropPasses==295&&landings==322&&upwardPasses==322,"Earth blocks drop; wood permits drop; both permit ascent and support landing");
                        CaptureTerrain(world);
                    }
                    Check(selector.Select(Chapter1MapSelector.OriginalScene),"Ver2 button returns to original");phase=2;return;
                }
                if(phase==2&&scene==Chapter1MapSelector.OriginalScene)
                {
                    Check(world.Data.views.Length==8&&world.Data.platforms.Length==38,"Original map restored after roundtrip");
                    Check(Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None).Length==1,"No retained arena after return");
                    Check(selector.Select(Chapter1MapSelector.GameScene),"Original map loads basic game");phase=3;return;
                }
                if(phase==4&&scene==Chapter1MapSelector.Ver2Scene)
                {
                    Check(world.Data.views.Length==24,"Basic game transition restores ver2");
                    Check(selector.Select(Chapter1MapSelector.OriginalScene),"Ver2 returns to original after basic game");phase=5;return;
                }
                if(phase==5&&scene==Chapter1MapSelector.OriginalScene)
                {
                    Check(world.Data.views.Length==8&&Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None).Length==1,"Final original map has no retained arena");
                    Finish(true,"");
                }
            }catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
        }
        static bool ClearBody(CombatPlayer player,Vector2 feet)
        {
            foreach(var hit in Physics2D.OverlapBoxAll(feet+new Vector2(player.offset.x*player.facing,player.offset.y),player.size-new Vector2(.01f,.01f),0))
            {
                var surface=hit.GetComponent<ArenaSurface>();if(surface&&!surface.oneWay)return false;
            }
            return true;
        }
        static void Place(CombatPlayer player,Vector2 feet,Vector2 velocity)
        {
            player.ResetPlayer();player.Position=feet;player.velocity=velocity;Physics2D.SyncTransforms();player.grounded=player.ProbeGround();
        }
        static void VerifyPlatform(ForestCombatWorld world,CombatArena.ManualSimulation manual,FoliagePlatform p)
        {
            var player=world.arena.Player;float top=-p.y/32f;
            float x=(p.x+p.w/2f)/32;bool found=false;
            float offset=player.offset.x*player.facing,half=player.size.x/2;
            for(float candidate=p.x/32f+half+.01f-offset;candidate<=(p.x+p.w)/32f-half-.01f-offset;candidate+=.125f)
                if(ClearBody(player,new Vector2(candidate,top+.25f))&&ClearBody(player,new Vector2(candidate,top-player.size.y))){x=candidate;found=true;break;}
            Check(found,"Full-body landing clearance at "+p.x+","+p.y);
            Place(player,new Vector2(x,top+.25f),new Vector2(0,-2));
            for(int step=0;step<30;step++)manual.Step(1f/120);
            Check(player.grounded&&Mathf.Abs(player.Position.y-top)<.04f,"Simulated landing at "+p.x+","+p.y);landings++;
            manual.Step(1f/120,new CombatInput(pressed:CombatButtons.Drop));
            for(int step=0;step<27;step++)manual.Step(1f/120);
            if(p.blockDropThrough)
            {
                Check(player.grounded&&Mathf.Abs(player.Position.y-top)<.04f,"Earth platform refuses drop input at "+p.x+","+p.y);dropBlocked++;
            }
            else
            {
                Check(player.Position.y<top-.5f,"Wood platform permits drop input at "+p.x+","+p.y);dropPasses++;
            }
            float below=top-player.size.y;
            Check(ClearBody(player,new Vector2(x,below)),"Platform has full-body ascent clearance at "+p.x+","+p.y);
            Place(player,new Vector2(x,below),new Vector2(0,24));float highest=below;
            for(int step=0;step<50;step++){manual.Step(1f/120);highest=Mathf.Max(highest,player.Position.y);}
            Check(highest>top+.1f,"Player ascends through platform without hitting head at "+p.x+","+p.y);upwardPasses++;
        }
        static void CaptureTerrain(ForestCombatWorld world)
        {
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var enabled=renderers.Select(r=>r.enabled).ToArray();var camera=world.View;
            var color=camera.backgroundColor;var clear=camera.clearFlags;var position=camera.transform.position;
            try
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].enabled=renderers[i] is TilemapRenderer&&renderers[i].transform.IsChildOf(world.transform);
                camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;
                var views=JsonUtility.FromJson<Chapter1TerrainVerification.Views>(File.ReadAllText(Path.Combine(Output,"../reference/views.json")));
                foreach(var v in views.views)
                {
                    camera.transform.position=new Vector3((v.x+480)/32f,-(v.y+270)/32f,-10);Capture(camera,v.name+"-terrain");
                }
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].enabled=enabled[i];
                camera.backgroundColor=color;camera.clearFlags=clear;camera.transform.position=position;
            }
        }
        static void Capture(Camera camera,string name)
        {
            var previous=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try{camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;camera.rect=rect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
        [Serializable] class Report{public bool complete,pass;public int checks,landings,dropPasses,dropBlocked,upwardPasses;public string unityVersion,error;}
        static void Finish(bool pass,string error)
        {
            EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);
            File.WriteAllText(Path.Combine(Output,"verification.json"),JsonUtility.ToJson(new Report{complete=true,pass=pass,checks=checks,landings=landings,dropPasses=dropPasses,dropBlocked=dropBlocked,upwardPasses=upwardPasses,unityVersion=Application.unityVersion,error=error},true));
            Debug.Log("CHAPTER1_MAP_VER2 pass="+pass+" checks="+checks);EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(pass?0:1);
        }
    }
}
