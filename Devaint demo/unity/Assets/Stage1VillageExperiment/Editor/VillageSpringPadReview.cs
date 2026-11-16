using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Stage1VillageExperiment.Editor
{
    [InitializeOnLoad] public static class VillageSpringPadReview
    {
        const string Key="Village.SpringPadReview.",Output="../outputs/village/jump-pad-gameplay/";
        static bool busy;static double quiet,deadline;
        [Serializable] class Report
        {
            public string unity;
            public bool pass,compressionCarriesPlayer,baseFixed,launchOnce,measuredHeight,ordinaryJumpUnchanged,sideIgnored,undersideIgnored,walkOffCancels,resetClears,reenableWorks,reachesUpperLedge;
            public float minimumOffsetPixels,launchVelocity,risePixels,ordinaryRisePixels,landedMapY;
            public int animationFrames,originalSurfaces,platforms;
        }
        static VillageSpringPadReview(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new Exception("Use Unity 6000.6.5f1");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Output+"frames");
            SessionState.SetBool(Key+"OldOptions",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Key+"Options",(int)EditorSettings.enterPlayModeOptions);
            EditorSceneManager.OpenScene(Stage1VillageBuilder.ScenePath);SessionState.SetBool(Key+"Pending",true);SessionState.SetBool(Key+"Entered",false);
            deadline=EditorApplication.timeSinceStartup+300;quiet=0;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key+"Pending",false)||busy)return;if(deadline==0)deadline=EditorApplication.timeSinceStartup+300;
            if(EditorApplication.timeSinceStartup>deadline){Finish("Spring-pad startup timeout");return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){quiet=0;return;}
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key+"Entered",false))return;
                if(quiet==0)quiet=EditorApplication.timeSinceStartup+3;if(EditorApplication.timeSinceStartup<quiet)return;
                EditorSettings.enterPlayModeOptionsEnabled=false;SessionState.SetBool(Key+"Entered",true);EditorApplication.EnterPlaymode();return;
            }
            var world=Object.FindAnyObjectByType<Stage1VillageWorld>();if(!world||!world.Ready||!world.arena||!world.arena.Player)return;
            var p=world.GetComponent<VillagePresentation>();if(!p||!p.Ready)return;
            busy=true;try{Verify(world,p);Finish(null);}catch(Exception e){Finish(e.ToString());}
        }
        static void Verify(Stage1VillageWorld world,VillagePresentation presentation)
        {
            var arena=world.arena;var player=arena.Player;var pad=presentation.JumpPadArtworkRoot.GetComponentInChildren<VillageSpringPad>();
            if(!pad)throw new Exception("Functional spring pad missing");
            var art=world.GetComponent<VillageArtTransition>();art.SetDisplay(VillageArtMode.Sketch);presentation.animateEnvironment=false;world.showHud=false;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.CancelAttack();}
            var report=new Report{unity=Application.unityVersion,originalSurfaces=world.SurfaceCount,platforms=world.Data.platforms.Length,baseFixed=true,compressionCarriesPlayer=true};
            const float dt=1f/120;float center=pad.transform.position.x,ground=pad.transform.position.y,idleTop=ground+48f/32;
            using(var sim=arena.AcquireManualSimulation())
            {
                void Place(float x,float y,float vy=0)
                {
                    pad.ResetPad();player.ResetPlayer();player.Position=new Vector2(x,y);player.velocity=new Vector2(0,vy);player.grounded=false;Physics2D.SyncTransforms();
                }
                void Draw(){world.RefreshCamera(0,true);presentation.RefreshForCapture();arena.Presentation.Draw(0);arena.RangedPresentation?.Tick();}
                // Direct descending landing, compression, release, and measured ballistic rise.
                Place(center,idleTop+4,-1);int activations=pad.ActivationCount,launches=pad.LaunchCount;
                float launchY=0,highest=float.MinValue;bool released=false,pressedSaved=false;var baseMesh=pad.GetComponent<MeshFilter>().sharedMesh;
                float baseY=baseMesh.vertices[2].y;
                for(int i=0;i<300;i++)
                {
                    sim.Step(dt,new CombatInput(0));
                    report.minimumOffsetPixels=Mathf.Min(report.minimumOffsetPixels,pad.OffsetPixels);
                    if(pad.State==VillageSpringPad.PadState.Compressing)
                    {
                        report.compressionCarriesPlayer&=Mathf.Abs(player.Position.y-pad.DeckWorldY)<.01f;
                        report.baseFixed&=Mathf.Abs(baseMesh.vertices[2].y-baseY)<.00001f;
                        if(pad.OffsetPixels<-11&&!pressedSaved){Draw();Save(world.View,"pressed.png");pressedSaved=true;}
                    }
                    if(pad.LaunchCount>launches&&!released){released=true;launchY=player.Position.y;report.launchVelocity=player.VelocityY;Draw();Save(world.View,"release.png");}
                    if(released){highest=Mathf.Max(highest,player.Position.y);if(player.VelocityY<0){Draw();Save(world.View,"apex.png");break;}}
                }
                report.risePixels=(highest-launchY)*32;report.measuredHeight=released&&Mathf.Abs(report.risePixels-pad.ExpectedRisePixels)<8;
                report.launchOnce=pad.LaunchCount==launches+1&&pad.ActivationCount==activations+1;
                report.compressionCarriesPlayer&=pressedSaved&&report.minimumOffsetPixels<=-11;
                // Ordinary jump away from the pad retains the original profile and height.
                Place(592f/32,ground);float start=player.Position.y,normalHighest=start;int count=pad.ActivationCount;
                for(int i=0;i<180;i++){sim.Step(dt,new CombatInput(0,i==0?CombatButtons.Jump:CombatButtons.None));normalHighest=Mathf.Max(normalHighest,player.Position.y);}
                report.ordinaryRisePixels=(normalHighest-start)*32;
                float expected=arena.Movement.FirstJumpVelocity*arena.Movement.FirstJumpVelocity/(2*arena.Movement.RiseGravity)*32;
                report.ordinaryJumpUnchanged=Mathf.Abs(report.ordinaryRisePixels-expected)<4&&count==pad.ActivationCount;
                // Below the one-way face, upward motion must pass through without arming.
                Place(center,ground);count=pad.ActivationCount;
                for(int i=0;i<15;i++)sim.Step(dt,new CombatInput(0,i==0?CombatButtons.Jump:CombatButtons.None));
                report.undersideIgnored=pad.ActivationCount==count&&player.Position.y>idleTop&&player.VelocityY>0;
                Place(center-3.5f,idleTop-.6f);count=pad.ActivationCount;
                for(int i=0;i<35;i++)sim.Step(dt,new CombatInput(1));
                report.sideIgnored=pad.ActivationCount==count;
                // Walk off before full compression: no remote boost or stuck rider.
                Place(center+45f/32,idleTop+.08f,-1);launches=pad.LaunchCount;
                for(int i=0;i<60;i++)sim.Step(dt,new CombatInput(1));
                report.walkOffCancels=pad.LaunchCount==launches&&pad.Rider==null;
                // Reset while depressed must release the player and restore the deck.
                Place(center,idleTop+.04f,-1);
                for(int i=0;i<14;i++)sim.Step(dt,new CombatInput(0));
                bool wasPressed=pad.State==VillageSpringPad.PadState.Compressing&&pad.OffsetPixels<0;
                sim.Reset();report.resetClears=wasPressed&&pad.Rider==null&&pad.State==VillageSpringPad.PadState.Idle&&pad.OffsetPixels==0;
                // Art toggles must rebind environmental simulation on re-enable.
                pad.gameObject.SetActive(false);pad.gameObject.SetActive(true);
                Place(center,idleTop+.04f,-1);count=pad.ActivationCount;sim.Step(dt,new CombatInput(0));
                for(int i=0;i<6;i++)sim.Step(dt,new CombatInput(0));
                report.reenableWorks=pad.ActivationCount==count+1;
                // Real input replay: jump from the entrance, depress the pad, launch right,
                // and reach an existing upper platform without changing authored geometry.
                Place(592f/32,ground);launches=pad.LaunchCount;bool launched=false,landed=false;
                for(int i=0;i<270;i++)
                {
                    int axis=pad.State==VillageSpringPad.PadState.Compressing?0:1;
                    sim.Step(dt,new CombatInput(axis,i==0?CombatButtons.Jump:CombatButtons.None));
                    launched|=pad.LaunchCount>launches;
                    if(i%5==0)
                    {
                        Draw();Save(world.View,"frames/frame-"+report.animationFrames.ToString("000")+".png");report.animationFrames++;
                    }
                    if(launched&&player.grounded&&player.Position.y>idleTop+1)
                    {landed=true;report.landedMapY=-player.Position.y*32;Draw();Save(world.View,"upper-landing.png");break;}
                }
                report.reachesUpperLedge=landed;
                pad.ResetPad();world.Warp(0);for(int i=0;i<18;i++)sim.Step(dt,new CombatInput(0));Draw();Save(world.View,"idle.png");
            }
            report.pass=report.compressionCarriesPlayer&&report.baseFixed&&report.launchOnce&&report.measuredHeight&&report.ordinaryJumpUnchanged&&report.sideIgnored&&report.undersideIgnored&&report.walkOffCancels&&report.resetClears&&report.reenableWorks&&report.reachesUpperLedge;
            File.WriteAllText(Output+"gameplay-review.json",JsonUtility.ToJson(report,true));
            if(!report.pass)throw new Exception("Spring-pad checks failed; see gameplay-review.json");
        }
        static void Save(Camera camera,string name)
        {
            var old=camera.targetTexture;var rect=camera.rect;float aspect=camera.aspect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try{rt.filterMode=FilterMode.Point;camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.aspect=960f/540;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Output+name,image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.rect=rect;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
        static void Finish(string error)
        {
            SessionState.SetBool(Key+"Pending",false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"OldOptions",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"Options",0);
            if(error!=null){File.WriteAllText(Output+"error.txt",error);Debug.LogError(error);}else Debug.Log("SPRING_PAD_GAMEPLAY_PASSED");
            if(Application.isBatchMode)EditorApplication.Exit(error==null?0:1);else EditorApplication.isPlaying=false;busy=false;
        }
    }
}
