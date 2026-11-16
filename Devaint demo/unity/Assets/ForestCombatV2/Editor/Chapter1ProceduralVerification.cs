using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    [InitializeOnLoad]
    public static class Chapter1ProceduralVerification
    {
        const string Pending="Chapter1ProceduralVerification.Pending";
        static readonly string Output=Path.GetFullPath("../outputs/chapter1-procedural/captures");
        static double readyAt;

        static Chapter1ProceduralVerification(){EditorApplication.update+=Tick;}

        [MenuItem("Tools/Deviant/Chapter1/Open Random Stage")]
        [MenuItem("Tools/Lost Words/Chapter1/Open Random Stage")]
        public static void Open()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                DummyTrainingTools.SelectNormalMode();
                EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            }
        }

        [MenuItem("Tools/Deviant/Chapter1/Verify Random Stage")]
        public static void Run()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            DummyTrainingTools.SelectNormalMode();
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            readyAt=0;SessionState.SetBool(Pending,true);
        }

        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false))return;
            try
            {
                if(!EditorApplication.isPlaying)
                {
                    if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                    if(readyAt==0){readyAt=EditorApplication.timeSinceStartup+2;return;}
                    if(EditorApplication.timeSinceStartup>=readyAt)EditorApplication.EnterPlaymode();
                    return;
                }
                var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
                if(!world||!world.Ready)return;
                if(world.Data.rooms.Length!=14||world.Data.actors.Length<80)
                    throw new InvalidOperationException("The random stage did not create its room and encounter set.");
                foreach(var enemy in world.arena.enemies)enemy.aggressive=false;
                foreach(int id in new[]{0,2,9,10,11,12,13,8})
                {
                    world.Warp(id);Capture(world.View,"room-"+id.ToString("00"));
                }
                // Gimmick rooms: the seal challenge on the main route and the locked chest bypass, whole room.
                foreach(var room in world.Data.rooms.Where(r=>r.kind=="seal"||r.kind=="chest"))
                {
                    world.Warp(room.id);Capture(world.View,room.kind+"-room");
                    CaptureAt(world,room.x+room.w/2f,room.y+room.h-6,room.kind+"-lower");
                    if(room.h>18)CaptureAt(world,room.x+room.w/2f,room.y+9,room.kind+"-upper");
                }
                // Key carrier with its key, then the locked chest before and after the key opens it.
                var keys=world.GetComponent<Chapter1KeyChest>();keys.Prepare();
                if(!keys.Carrier||!keys.Chest)throw new InvalidOperationException("The key carrier or the locked chest is missing.");
                keys.RefreshGlow();
                CaptureAt(world,keys.Carrier.Position.x,-keys.Carrier.Position.y,"key-carrier");
                CaptureAt(world,keys.Chest.Position.x,-keys.Chest.Position.y,"chest-locked");
                keys.GrantKeyForVerification();keys.TryOpenChest(true);
                if(!keys.Opened)throw new InvalidOperationException("The key did not open the chest.");
                // The chest's ink-or-blueprint choice pauses the run; take its first card so the captures go on.
                keys.TakeChoiceForVerification();
                CaptureAt(world,keys.Chest.Position.x,-keys.Chest.Position.y,"chest-opened");
                // Seal tablet before and right after the challenge starts (gates down).
                var seal=world.GetComponent<Chapter1SealChallenge>();
                if(!seal.Prepare())throw new InvalidOperationException("The seal challenge is missing.");
                world.Warp(seal.Room.id);
                world.arena.Player.Position=seal.TabletPosition+Vector2.left*2;world.arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();
                CaptureAt(world,seal.TabletPosition.x,-seal.TabletPosition.y,"seal-dormant");
                world.arena.Player.Position=seal.TabletPosition;Physics2D.SyncTransforms();
                if(!seal.TryStart())throw new InvalidOperationException("The seal challenge did not start.");
                seal.SnapGatesForVerification();
                Capture(world.View,"seal-active");
                CaptureAt(world,seal.Room.x+5,seal.Room.y+seal.Room.entryY-3,"seal-gate");
                foreach(var room in world.Data.rooms.Where(r=>r.kind=="elite"||r.kind=="boss"))
                    CaptureAt(world,room.x+room.w-4,room.y+17,room.kind+"-closed-end");
                var branch=world.Data.passages.Where(p=>p.h>1).ToArray();
                for(int i=0;i<branch.Length;i++)
                {
                    var passage=branch[i];
                    CaptureAt(world,passage.x+passage.w/2f,passage.y+9,"connector-"+i.ToString("00")+"-top");
                    CaptureAt(world,passage.x+passage.w/2f,passage.y+passage.h-9,"connector-"+i.ToString("00")+"-bottom");
                }
                File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(new Report{
                    pass=true,seed=world.Data.generationSeed,rooms=world.Data.rooms.Length,
                    combatRooms=world.Data.rooms.Count(r=>r.kind=="combat"),enemies=world.arena.enemies.Count},true));
                Finish(true);
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);
                File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(new Report{pass=false,error=exception.ToString()},true));
                Finish(false);
            }
        }

        static void Capture(Camera camera,string name)
        {
            var previous=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.Render();
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previous;camera.rect=rect;RenderTexture.active=active;
                RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static void CaptureAt(ForestCombatWorld world,float x,float y,string name)
        {
            var player=world.arena.Player;
            var position=player.Position;
            try
            {
                player.Position=new Vector2(x,-y);
                world.RefreshCamera(0,true);
                player.Position=position;
                Capture(world.View,name);
            }
            finally
            {
                player.Position=position;
                world.RefreshCamera(0,true);
            }
        }

        static void Finish(bool pass)
        {
            SessionState.SetBool(Pending,false);EditorApplication.ExitPlaymode();
            Debug.Log("CHAPTER1_RANDOM_STAGE pass="+pass);
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(pass?0:1);
        }

        [Serializable] sealed class Report{public bool pass;public int seed,rooms,combatRooms,enemies;public string error;}
    }
}
