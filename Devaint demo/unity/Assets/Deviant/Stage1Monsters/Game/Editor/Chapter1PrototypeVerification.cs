using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using ForestCombatV2;
using UnityEditor;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game.Editor
{
    [InitializeOnLoad] public static class Chapter1PrototypeVerification
    {
        const string Pending="Chapter1PrototypeVerification.Pending";
        static double deadline;
        static readonly string Output=Path.GetFullPath("../outputs/new-monsters-play");
        static Chapter1PrototypeVerification(){EditorApplication.update+=Tick;deadline=EditorApplication.timeSinceStartup+120;}
        public static void Run()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1.");
            if(!EditorApplication.ExecuteMenuItem("Tools/Lost Words/Chapter1/Open Random Stage"))throw new InvalidOperationException("User execution menu is missing.");
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();world.proceduralSeed=42;
            SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Pending,false);Debug.LogError("New monster play verification timed out.");EditorApplication.Exit(1);return;}
            if(!EditorApplication.isPlaying)return;
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
            if(!world||!world.Ready)return;
            SessionState.SetBool(Pending,false);Directory.CreateDirectory(Output);
            try
            {
                foreach(var e in world.arena.enemies){e.aggressive=false;e.CancelAttack();if(e.visual)e.visual.enabled=false;}
                var room=world.Data.rooms.First(r=>r.kind=="start");float floor=-room.y-15;
                var player=world.arena.Player;player.Position=new Vector2(room.x+16,floor);player.velocity=Vector2.zero;player.health=player.maxHealth;
                var specs=Chapter1NewMonsterCatalog.Load().monsters;
                var barrel=(Chapter1PrototypeEnemy)world.SpawnMonster(specs.Single(s=>s.ranged).id,new Vector2(room.x+8,floor),true);
                var roller=(Chapter1PrototypeEnemy)world.SpawnMonster(specs.Single(s=>!s.ranged).id,new Vector2(room.x+24,floor),true);
                barrel.Spec.firstDelayMin=barrel.Spec.firstDelayMax=0;barrel.ResetEnemy();Physics2D.SyncTransforms();
                using(var driver=world.arena.AcquireManualSimulation())
                {
                    for(int i=0;i<60;i++)driver.Step(.01f);Draw(world,barrel,roller,"01-warning.png");
                    for(int i=0;i<93;i++)driver.Step(.01f);Draw(world,barrel,roller,"02-explosion.png");
                    for(int i=0;i<137;i++)driver.Step(.01f);Draw(world,barrel,roller,"03-charge.png");
                    for(int i=0;i<20;i++)driver.Step(.01f);
                }
                if(barrel.AttacksStarted!=1||barrel.HitsDelivered!=1||roller.HitsDelivered!=1||Mathf.Abs(roller.Travelled-12)>.05f)
                    throw new InvalidOperationException($"Unexpected execution: barrel={barrel.AttacksStarted}/{barrel.HitsDelivered}; charge={roller.Travelled}/{roller.HitsDelivered}");
                float chargeTravel=roller.Travelled;int chargeHits=roller.HitsDelivered;
                if(roller.Definition.recovery!=3)throw new InvalidOperationException("Charge recovery must be three seconds.");
                barrel.aggressive=false;barrel.CancelAttack();roller.ResetEnemy();
                player.Position=roller.spawn+Vector2.left*8;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
                using(var driver=world.arena.AcquireManualSimulation())
                {
                    driver.Step(.01f);
                    roller.Receive(roller.maxHealth*.5f,player,1,false,null,bypass:true);
                    if(roller.State!=Chapter1PrototypeEnemy.Phase.Telegraph)throw new InvalidOperationException("Half health interrupted the charge telegraph.");
                    Draw(world,barrel,roller,"04-half-health-telegraph.png");
                }
                File.WriteAllText(Path.Combine(Output,"report.json"),"{\"unity\":\""+Application.unityVersion+"\",\"menu\":\"Tools/Lost Words/Chapter1/Open Random Stage\",\"seed\":42,\"barrelHits\":"+barrel.HitsDelivered+",\"chargeHits\":"+chargeHits+",\"chargeTravel\":"+chargeTravel.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"halfHealthTelegraphVerified\":true,\"chargeRecoverySeconds\":3}");
                Debug.Log("NEW_MONSTERS_PLAY_VERIFIED: real Chapter1 scene, user menu, warning/explosion/charge captures.");
                EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Output,"error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Draw(ForestCombatWorld world,Chapter1PrototypeEnemy a,Chapter1PrototypeEnemy b,string filename)
        {
            a.DrawPrototype();b.DrawPrototype();world.RefreshCamera(0,true);
            world.GetComponent<Stage1VillageExperiment.VillagePresentation>()?.RefreshForCapture();
            var camera=world.sceneCamera;var previous=camera.targetTexture;var old=RenderTexture.active;
            var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,filename),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=old;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
