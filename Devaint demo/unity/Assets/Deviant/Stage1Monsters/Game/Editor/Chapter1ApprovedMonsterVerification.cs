using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using ForestCombatV2;
using UnityEditor;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game.Editor
{
    [InitializeOnLoad] public static class Chapter1ApprovedMonsterVerification
    {
        const string Pending="Chapter1ApprovedMonsterVerification.Pending";
        const string Interactive="Chapter1ApprovedMonsterVerification.Interactive";
        static double deadline;
        static readonly string Output=Path.GetFullPath("../outputs/new-monsters/unity-capture");
        static Chapter1ApprovedMonsterVerification(){EditorApplication.update+=Tick;deadline=EditorApplication.timeSinceStartup+180;}
        public static void Run()=>Start(false);
        [MenuItem("Tools/deviant/Stage 1 Monsters/Play Approved Barrel And Millstone")]
        public static void OpenAndPlay()=>Chapter1ApprovedMonsterGameFlowVerification.OpenAndPlay();
        static void Start(bool interactive)
        {
            SessionState.SetBool(Interactive,interactive);
            Chapter1NewMonsterArtImporter.Import();
            if(!EditorApplication.ExecuteMenuItem("Tools/Lost Words/Chapter1/Open Random Stage"))throw new InvalidOperationException("Missing common random-stage menu.");
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();world.proceduralSeed=42;
            SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Pending,false);Debug.LogError("Approved monster verification timeout.");EditorApplication.Exit(1);return;}
            if(!EditorApplication.isPlaying)return;
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();if(!world||!world.Ready)return;
            SessionState.SetBool(Pending,false);Directory.CreateDirectory(Output);
            try
            {
                foreach(var e in world.arena.enemies.ToArray())
                { e.aggressive=false;e.CancelAttack();e.gameObject.SetActive(false); }
                var room=world.Data.rooms.First(r=>r.kind=="start");float floor=-room.y-15;
                var player=world.arena.Player;player.Position=new Vector2(room.x+16,floor);player.velocity=Vector2.zero;player.health=player.maxHealth;player.shield=0;
                var specs=Chapter1NewMonsterCatalog.Load().monsters;
                var barrel=(Chapter1PrototypeEnemy)world.SpawnMonster(specs.Single(s=>s.ranged).id,new Vector2(room.x+8,floor),true);
                var roller=(Chapter1PrototypeEnemy)world.SpawnMonster(specs.Single(s=>!s.ranged).id,new Vector2(room.x+24,floor),true);
                barrel.Spec.firstDelayMin=barrel.Spec.firstDelayMax=0;barrel.ResetEnemy();
                if(SessionState.GetBool(Interactive,false))
                { world.RefreshCamera(0,true);Debug.Log("Approved barrel/millstone play preview ready in the common Chapter1 scene.");return; }
                roller.aggressive=false;
                Physics2D.SyncTransforms();int warningFrames=0,stillFrames=0,waveFrames=0;
                using(var driver=world.arena.AcquireManualSimulation())
                {
                    for(int i=0;i<225;i++)
                    {
                        if(i==80){barrel.aggressive=false;roller.aggressive=true;}
                        driver.Step(.04f);barrel.DrawPrototype();roller.DrawPrototype();
                        if(barrel.Presentation.Warning.enabled)
                        {
                            warningFrames++;
                            if(Mathf.Abs(barrel.Presentation.Warning.bounds.size.x-barrel.BlastBounds.size.x)>.001f)
                                throw new InvalidOperationException("Warning must match actual blast bounds.");
                        }
                        if(roller.State==Chapter1PrototypeEnemy.Phase.Telegraph&&roller.AttackAge>=1.8f)
                        { if(roller.Presentation.ShakeOffset!=Vector2.zero)throw new InvalidOperationException("Charge final hold moved.");stillFrames++; }
                        if(roller.Presentation.Wave.enabled)waveFrames++;
                        world.RefreshCamera(0,true);
                        world.GetComponent<Stage1VillageExperiment.VillagePresentation>()?.RefreshForCapture();
                        Capture(world.sceneCamera,Path.Combine(Output,i.ToString("000")+".png"));
                    }
                }
                if(barrel.HitsDelivered!=1||roller.HitsDelivered!=1||Mathf.Abs(roller.Travelled-12)>.06f||warningFrames<25||stillFrames<4||waveFrames<15)
                    throw new InvalidOperationException($"Unexpected approved presentation: hits={barrel.HitsDelivered}/{roller.HitsDelivered}; travel={roller.Travelled}; warning={warningFrames}; hold={stillFrames}; wave={waveFrames}");
                File.WriteAllText(Path.Combine(Output,"result.json"),JsonUtility.ToJson(new Result{unity=Application.unityVersion,barrelHits=barrel.HitsDelivered,chargeHits=roller.HitsDelivered,chargeTravel=roller.Travelled,warningFrames=warningFrames,stillFrames=stillFrames,waveFrames=waveFrames},true));
                Debug.Log("APPROVED_NEW_MONSTERS_PLAY_PASS: common Chapter1 scene, native art, bound-sized bomb, single impact, final hold, charge and wave.");
                EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Output,"error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable] class Result { public string unity;public int barrelHits,chargeHits,warningFrames,stillFrames,waveFrames;public float chargeTravel; }
        static void Capture(Camera camera,string file)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
