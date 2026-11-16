using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Combat.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Stage1VillageExperiment.Editor
{
    [InitializeOnLoad]
    public static class VillageNearStructureReview
    {
        const string Key="Village.NearStructureReview.";
        const string Output="../outputs/village/near-structure/";
        static double quiet,deadline;
        static bool busy;
        [Serializable] class Report
        {
            public string unity,scope;
            public bool colorUnchanged,physicsUnchanged,placementUnchanged,terrainUnchanged;
            public int nearbyRenderers,areas,surfaces,platforms;
            public string[] captures;
        }
        static VillageNearStructureReview(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new Exception("Use Unity 6000.6.5f1");
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key+"OldOptions",EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key+"Options",(int)EditorSettings.enterPlayModeOptions);
            EditorSceneManager.OpenScene(Stage1VillageBuilder.ScenePath);
            SessionState.SetBool(Key+"Pending",true);SessionState.SetBool(Key+"Entered",false);
            quiet=0;deadline=EditorApplication.timeSinceStartup+240;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key+"Pending",false)||busy)return;
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+240;
            if(EditorApplication.timeSinceStartup>deadline){Finish("Near review startup timed out");return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){quiet=0;return;}
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key+"Entered",false))return;
                if(quiet==0)quiet=EditorApplication.timeSinceStartup+3;
                if(EditorApplication.timeSinceStartup<quiet)return;
                EditorSettings.enterPlayModeOptionsEnabled=false;SessionState.SetBool(Key+"Entered",true);
                EditorApplication.EnterPlaymode();return;
            }
            var world=Object.FindAnyObjectByType<Stage1VillageWorld>();
            if(!world||!world.Ready||!world.arena||!world.arena.Player)return;
            var presentation=world.GetComponent<VillagePresentation>();
            if(!presentation||!presentation.Ready)return;
            busy=true;
            try{Capture(world,presentation);Finish(null);}catch(Exception e){Finish(e.ToString());}
        }
        static void Capture(Stage1VillageWorld world,VillagePresentation p)
        {
            var art=world.GetComponent<VillageArtTransition>();var arena=world.arena;
            p.animateEnvironment=false;world.showHud=false;arena.Player.UseTestInput=true;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.CancelAttack();}
            var report=new Report{unity=Application.unityVersion,scope="Existing world-fixed near scenery ink only; approved terrain, platforms and distant scenery retained.",areas=world.Data.views.Length,surfaces=world.SurfaceCount,platforms=world.Data.platforms.Length};
            var captures=new List<string>();
            using(var simulation=arena.AcquireManualSimulation())
            {
                void SetZone(int zone)
                {
                    arena.Player.ResetPlayer();world.Warp(zone);
                    for(int i=0;i<18;i++)simulation.Step(1f/120,new CombatInput(0));
                    world.RefreshCamera(0,true);p.RefreshForCapture();arena.Presentation.Draw(0);arena.RangedPresentation?.Tick();
                }
                SetZone(0);
                var colliders=world.TerrainSurfaces.Select(s=>s.GetComponent<Collider2D>().bounds).ToArray();
                var feet=arena.Player.Position;
                var scenery=p.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.sharedMaterial&&r.sharedMaterial.HasProperty("_NearSketchInk")&&r.sharedMaterial.GetFloat("_NearSketchInk")>.5f).ToArray();
                report.nearbyRenderers=scenery.Length;
                var placements=scenery.Select(r=>(r.transform.position,r.transform.localScale,r.sprite.rect,r.sortingOrder)).ToArray();
                p.SetNearSketchInkEnabled(false);art.SetDisplay(VillageArtMode.Color);var before=Save(world.View,"color-before.png");
                p.SetNearSketchInkEnabled(true);var after=Save(world.View,"color-after.png");
                report.colorUnchanged=before.SequenceEqual(after);
                report.physicsUnchanged=feet==arena.Player.Position&&colliders.SequenceEqual(world.TerrainSurfaces.Select(s=>s.GetComponent<Collider2D>().bounds));
                report.placementUnchanged=placements.SequenceEqual(scenery.Select(r=>(r.transform.position,r.transform.localScale,r.sprite.rect,r.sortingOrder)));
                report.terrainUnchanged=p.referenceGroundEnabled&&p.ReferenceGroundRoot.GetComponentsInChildren<Renderer>().All(r=>!r.sharedMaterial.HasProperty("_NearSketchInk"));
                foreach(int zone in new[]{0,1,3,6})
                {
                    SetZone(zone);art.SetDisplay(VillageArtMode.Sketch);
                    foreach(bool enabled in new[]{false,true})
                    {
                        p.SetNearSketchInkEnabled(enabled);
                        string name="zone-"+(zone+1).ToString("00")+(enabled?"-after.png":"-before.png");
                        Save(world.View,name);captures.Add(name);
                    }
                }
                SetZone(0);p.SetNearSketchInkEnabled(true);
                foreach(float weight in new[]{0f,.25f,.5f,.75f,1f})
                {art.SetDisplay(VillageArtMode.Blend,weight);Save(world.View,"transition-"+(int)(weight*100)+".png");}
                art.UseAuthoredRegions();Save(world.View,"initial-game.png");
            }
            report.captures=captures.ToArray();
            File.WriteAllText(Output+"near-review.json",JsonUtility.ToJson(report,true));
            if(!report.colorUnchanged||!report.physicsUnchanged||!report.placementUnchanged||!report.terrainUnchanged||report.nearbyRenderers==0)
                throw new Exception("Near-scenery preservation failed; see near-review.json");
        }
        static Color32[] Save(Camera camera,string name)
        {
            var old=camera.targetTexture;var rect=camera.rect;float aspect=camera.aspect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try
            {
                rt.filterMode=FilterMode.Point;camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.aspect=960f/540;
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                File.WriteAllBytes(Output+name,image.EncodeToPNG());return image.GetPixels32();
            }
            finally{camera.targetTexture=old;camera.rect=rect;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
        static void Finish(string error)
        {
            SessionState.SetBool(Key+"Pending",false);
            EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"OldOptions",false);
            EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"Options",0);
            if(error!=null){File.WriteAllText(Output+"error.txt",error);Debug.LogError(error);}else Debug.Log("NEAR_STRUCTURE_REVIEW_PASSED");
            if(Application.isBatchMode)EditorApplication.Exit(error==null?0:1);else EditorApplication.isPlaying=false;
            busy=false;
        }
    }
}
