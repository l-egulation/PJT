using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ForestCombatV2;
using Deviant.Combat;
using Deviant.Combat.Contracts;

[InitializeOnLoad] public static class LocalTeamShareCapture
{
    const string Key="TeamShareCapture";
    static readonly string Output=Path.GetFullPath(Environment.GetEnvironmentVariable("CHAPTER1_SHARE_OUTPUT") ?? Path.Combine(Application.dataPath,"../../outputs/room-replacement/captures"));
    static Queue<Action> jobs=new Queue<Action>();
    static double readyAt;
    static ForestCombatWorld world;
    static CombatArena.ManualSimulation driver;
    static LocalTeamShareCapture(){EditorApplication.update+=Tick;}
    public static void Run(){Directory.CreateDirectory(Output);EditorSettings.enterPlayModeOptionsEnabled=false;SessionState.SetInt(Key,4);SessionState.SetInt(Key+"Round",0);readyAt=EditorApplication.timeSinceStartup;}
    public static void RunCombat(){Run();SessionState.SetBool(Key+"OnlyCombat",true);}
    static void Open(){EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=new[]{0,17,113,42,1,3,5,6,8,9}[SessionState.GetInt(Key+"Round",0)];EditorApplication.EnterPlaymode();readyAt=0;}
    static void Tick()
    {
        int phase=SessionState.GetInt(Key,0);if(phase==0||EditorApplication.isCompiling)return;
        try
        {
            if(phase==4){if(EditorApplication.isUpdating){readyAt=EditorApplication.timeSinceStartup;return;}if(EditorApplication.timeSinceStartup-readyAt<3)return;SessionState.SetInt(Key,1);Open();return;}
            if(phase==2&&!EditorApplication.isPlaying)
            {
                if(SessionState.GetBool(Key+"OnlyCombat",false)){SessionState.SetBool(Key+"OnlyCombat",false);SessionState.SetInt(Key,0);EditorApplication.Exit(0);return;}
                int round=SessionState.GetInt(Key+"Round",0)+1;
                if(round>=3&&Directory.GetFiles(Output,"room-*.png").Length>=13){SessionState.SetInt(Key,0);EditorApplication.Exit(0);return;}
                if(round>=10)throw new Exception("Could not capture all room templates in ten runs");
                SessionState.SetInt(Key+"Round",round);SessionState.SetInt(Key,1);Open();return;
            }
            if(!EditorApplication.isPlaying)return;
            world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();if(!world||!world.Ready)return;
            if(readyAt==0){readyAt=EditorApplication.timeSinceStartup;return;}
            if(EditorApplication.timeSinceStartup-readyAt<3)return;
            if(phase==1){Prepare();SessionState.SetInt(Key,3);return;}
            if(phase==3)
            {
                if(jobs.Count>0){jobs.Dequeue()();return;}
                driver?.Dispose();driver=null;SessionState.SetInt(Key,2);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
    static void Prepare()
    {
        jobs.Clear();driver=world.arena.AcquireManualSimulation();
        foreach(var e in world.arena.enemies)e.aggressive=false;
        int round=SessionState.GetInt(Key+"Round",0);var d=world.Data;
        File.WriteAllText(Path.Combine(Output,"layout-"+round+".json"),JsonUtility.ToJson(d));
        foreach(var r in d.rooms)
        {
            string name=r.kind=="combat"?"room-"+(r.sourceZone-100).ToString("00"):r.kind+"-"+round;
            if(r.sourceZone==109)name+="-"+(r.mirrored?"left":"right");
            if(File.Exists(Path.Combine(Output,name+".png")))continue;
            var room=r;
            jobs.Enqueue(()=>{world.Warp(room.id);world.RefreshCamera(0,true);});
            jobs.Enqueue(()=>Capture(name,room.x+room.w/2f,-room.y-room.h/2f,room.w/2f+2,1280,room.h>18?1120:800));
        }
        if(round<3)
        {
            foreach(var folded in d.rooms.Where(r=>r.sourceZone==109)) {
                var f=folded;
                jobs.Enqueue(()=>Capture("digeut-connection-"+(f.mirrored?"left":"right"),f.x+f.w/2f,-f.y-f.h/2f,29,1440,1440));
            }
            foreach(var passage in d.passages)
            {
                var p=passage;var from=d.rooms[p.from];var to=d.rooms[p.to];
                string kind=from.y==to.y?"straight":from.mainPath?(to.y<from.y?"split_up":"split_down"):(from.y<to.y?"merge_from_up":"merge_from_down");
                string file="connector-"+kind;
                if(File.Exists(Path.Combine(Output,file+".png")))continue;
                jobs.Enqueue(()=>{world.arena.Player.Position=new Vector2(p.x+4,-from.y-15);world.RefreshCamera(0,true);});
                jobs.Enqueue(()=>Capture(file,p.x+p.w/2f,-p.y-p.h/2f+3,p.h==1?7:12,p.h==1?1536:720,p.h==1?864:1800));
            }
            jobs.Enqueue(()=>Capture("layout-"+round,d.cols/2f,-d.rows/2f,Mathf.Max(d.cols/2f+10,(d.rows/2f+6)*3200f/960),3200,960));
        }
        if(round==0)
        {
            var start=d.rooms.First(r=>r.kind=="start");
            jobs.Enqueue(()=>{world.Warp(start.id);world.RefreshCamera(0,true);});
            jobs.Enqueue(()=>Capture("blueprints-three",start.x+start.w/2f+5,-start.y-13,10,1280,720));
            jobs.Enqueue(()=>{var o=world.GetComponent<Chapter1StartingBlueprints>();world.arena.Player.Position=o.OfferPosition(1);o.TryChoose(1);});
            jobs.Enqueue(()=>Capture("blueprints-one",start.x+start.w/2f+5,-start.y-13,10,1280,720));
            jobs.Enqueue(()=>{var o=world.GetComponent<Chapter1StartingBlueprints>();world.arena.Player.Position=o.OfferPosition(2);o.TryChoose(2);});
            jobs.Enqueue(()=>Capture("blueprints-two",start.x+start.w/2f+5,-start.y-13,10,1280,720));
            jobs.Enqueue(()=>{world.Warp(start.id);world.arena.Player.CancelAction();world.arena.Player.selected=0;driver.Step(1f/120,new CombatInput(pressed:CombatButtons.Basic));for(int i=0;i<43;i++)driver.Step(1f/120);world.RefreshCamera(0,true);});
            jobs.Enqueue(()=>Capture("hammer-impact",world.arena.Player.Position.x+3,world.arena.Player.Position.y+3,7,1280,720));
            jobs.Enqueue(()=>{world.arena.Player.CancelAction();int b=Array.FindIndex(world.arena.catalog.forms,f=>f.id=="bomb");world.arena.Player.Reconfigure(0,b,0);world.arena.Player.ResetPlayer();world.arena.Player.SetWeaponSlotReady(0,true);driver.Step(1f/120,new CombatInput(pressed:CombatButtons.Basic));for(int i=0;i<45;i++)driver.Step(1f/120);world.RefreshCamera(0,true);});
            jobs.Enqueue(()=>Capture("bomb-basic",world.arena.Player.Position.x+5,world.arena.Player.Position.y+3,8,1280,720));
            jobs.Enqueue(()=>{world.arena.Player.CancelAction();driver.Step(1f/120,new CombatInput(pressed:CombatButtons.Special));for(int i=0;i<50;i++)driver.Step(1f/120);});
            jobs.Enqueue(()=>Capture("bomb-special",world.arena.Player.Position.x+6,world.arena.Player.Position.y+3,9,1280,720));
        }
    }
    static void Capture(string file,float x,float y,float halfWidth,int width,int height)
    {
        Camera c=world.View;var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
        var prev=c.targetTexture;var active=RenderTexture.active;var rect=c.rect;var pos=c.transform.position;float size=c.orthographicSize,aspect=c.aspect;
        var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
        try{c.targetTexture=target;c.rect=new Rect(0,0,1,1);c.aspect=(float)width/height;c.orthographicSize=halfWidth/c.aspect;c.transform.position=new Vector3(x,y,pos.z);c.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,file+".png"),image.EncodeToPNG());Debug.Log("TEAM_CAPTURE "+file);}
        finally{c.targetTexture=prev;c.rect=rect;c.aspect=aspect;c.orthographicSize=size;c.transform.position=pos;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(image);}
    }
}
