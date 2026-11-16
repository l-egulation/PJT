using System;
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
    public static class VillageJumpPadArtReview
    {
        const string Key="Village.JumpPadArtReview.";
        const string Output="../outputs/village/super-jump-placement/";
        static bool busy;
        static double quiet,deadline;
        [Serializable] class Report
        {
            public string unity,status;
            public bool physicsUnchanged,noGameplayComponents,groundSupported,fullCanvas,transparentPadding;
            public int sprites,areas,surfaces,platforms;
            public float x,y,contactScreenX,contactScreenY;
        }
        static VillageJumpPadArtReview(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            var layout=Resources.Load<TextAsset>("Village/JumpPadArt/layout");
            if(layout&&JsonUtility.FromJson<VillageJumpPadArtwork.Layout>(layout.text).gameplayEnabled){VillageSpringPadReview.Run();return;}
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
            if(EditorApplication.timeSinceStartup>deadline){Finish("Jump-pad review startup timed out");return;}
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
        static void Capture(Stage1VillageWorld world,VillagePresentation presentation)
        {
            var art=world.GetComponent<VillageArtTransition>();var arena=world.arena;
            presentation.animateEnvironment=false;world.showHud=false;arena.Player.UseTestInput=true;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.CancelAttack();}
            var root=presentation.JumpPadArtworkRoot;
            var sprites=root.GetComponentsInChildren<SpriteRenderer>();
            var layout=JsonUtility.FromJson<VillageJumpPadArtwork.Layout>(Resources.Load<TextAsset>("Village/JumpPadArt/layout").text);
            var report=new Report{unity=Application.unityVersion,status="Actual existing map: visual placement only; super-jump not implemented.",sprites=sprites.Length,areas=world.Data.views.Length,surfaces=world.SurfaceCount,platforms=world.Data.platforms.Length,x=layout.placements[0].x,y=layout.placements[0].y};
            report.noGameplayComponents=root.GetComponentsInChildren<Component>().All(c=>c is Transform||c is SpriteRenderer);
            report.fullCanvas=sprites.All(r=>r.sprite.rect==new Rect(0,0,r.sprite.texture.width,r.sprite.texture.height));
            report.transparentPadding=true;
            foreach(var texture in new[]{sprites[0].sprite.texture,(Texture2D)sprites[0].sharedMaterial.GetTexture("_SketchTex")})
            {
                var pixels=texture.GetPixels32();
                for(int x=0;x<texture.width;x++)if(pixels[x].a!=0||pixels[(texture.height-1)*texture.width+x].a!=0)report.transparentPadding=false;
                for(int y=0;y<texture.height;y++)if(pixels[y*texture.width].a!=0||pixels[y*texture.width+texture.width-1].a!=0)report.transparentPadding=false;
            }
            int row=Mathf.RoundToInt(report.y/32);report.groundSupported=true;
            for(int x=Mathf.FloorToInt((report.x-64)/32);x<=Mathf.FloorToInt((report.x+63)/32);x++)
                if(world.Data.ground[row*world.Data.cols+x]==0||world.Data.ground[(row-1)*world.Data.cols+x]!=0)report.groundSupported=false;
            using(var simulation=arena.AcquireManualSimulation())
            {
                arena.Player.ResetPlayer();world.Warp(0);
                for(int i=0;i<18;i++)simulation.Step(1f/120,new CombatInput(0));
                world.RefreshCamera(0,true);presentation.RefreshForCapture();arena.Presentation.Draw(0);arena.RangedPresentation?.Tick();
                var bounds=world.TerrainSurfaces.Select(s=>s.GetComponent<Collider2D>().bounds).ToArray();var feet=arena.Player.Position;
                var contact=world.View.WorldToViewportPoint(new Vector3(report.x/32,-report.y/32));
                report.contactScreenX=contact.x*960;report.contactScreenY=(1-contact.y)*540;
                foreach(bool enabled in new[]{false,true})
                {
                    root.SetActive(enabled);string suffix=enabled?"after":"before";
                    art.SetDisplay(VillageArtMode.Color);Save(world.View,"color-"+suffix+".png");
                    art.SetDisplay(VillageArtMode.Sketch);Save(world.View,"sketch-"+suffix+".png");
                }
                foreach(float weight in new[]{0f,.25f,.5f,.75f,1f})
                {art.SetDisplay(VillageArtMode.Blend,weight);Save(world.View,"transition-"+(int)(weight*100)+".png");}
                art.UseAuthoredRegions();Save(world.View,"initial-game.png");
                report.physicsUnchanged=feet==arena.Player.Position&&bounds.SequenceEqual(world.TerrainSurfaces.Select(s=>s.GetComponent<Collider2D>().bounds));
            }
            File.WriteAllText(Output+"placement-review.json",JsonUtility.ToJson(report,true));
            if(!report.physicsUnchanged||!report.noGameplayComponents||!report.groundSupported||!report.fullCanvas||!report.transparentPadding||report.sprites!=1)
                throw new Exception("Jump-pad placement checks failed; see placement-review.json");
        }
        static void Save(Camera camera,string name)
        {
            var old=camera.targetTexture;var rect=camera.rect;float aspect=camera.aspect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try
            {
                rt.filterMode=FilterMode.Point;camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.aspect=960f/540;
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                File.WriteAllBytes(Output+name,image.EncodeToPNG());
            }
            finally{camera.targetTexture=old;camera.rect=rect;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
        static void Finish(string error)
        {
            SessionState.SetBool(Key+"Pending",false);
            EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"OldOptions",false);
            EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"Options",0);
            if(error!=null){File.WriteAllText(Output+"error.txt",error);Debug.LogError(error);}else Debug.Log("JUMP_PAD_ART_PLACEMENT_PASSED");
            if(Application.isBatchMode)EditorApplication.Exit(error==null?0:1);else EditorApplication.isPlaying=false;
            busy=false;
        }
    }
}
