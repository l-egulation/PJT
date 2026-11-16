using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using ForestCombatV2;
using Stage1VillageExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Deviant.Stage1Monsters.Game.Editor
{
    // Full product flow: title -> village blueprint table -> departure sign -> equipped run.
    [InitializeOnLoad] public static class Chapter1ApprovedMonsterGameFlowVerification
    {
        const string Pending="Chapter1ApprovedMonsterGameFlow.Pending",Interactive="Chapter1ApprovedMonsterGameFlow.Interactive";
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly string Output=Path.GetFullPath("../outputs/new-monsters/game-flow");
        static int phase;
        static double readyAt,deadline;
        static Chapter1ApprovedMonsterGameFlowVerification(){EditorApplication.update+=Tick;deadline=EditorApplication.timeSinceStartup+240;}
        public static void Run()=>Start(false);
        public static void OpenAndPlay()=>Start(true);
        static void Start(bool interactive)
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1.");
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            SessionState.SetBool(Pending,true);SessionState.SetBool(Interactive,interactive);
            EditorApplication.isPlaying=true;
        }
        static MonoBehaviour Gui()=>Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b=>b.GetType().Name=="TemporaryGameGui");
        static T Field<T>(MonoBehaviour gui,string name)=>(T)gui.GetType().GetField(name,Hidden).GetValue(gui);
        static void Call(MonoBehaviour gui,string name)=>gui.GetType().GetMethod(name,BindingFlags.Public|Hidden).Invoke(gui,null);
        static void Click(Button button)
        { if(!button||!button.interactable)throw new InvalidOperationException("Expected enabled game UI button.");button.onClick.Invoke(); }
        static void Next(){phase++;readyAt=EditorApplication.timeSinceStartup+.25;}
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying)return;
            bool interactive=SessionState.GetBool(Interactive,false);
            if(EditorApplication.timeSinceStartup<readyAt)return;
            Directory.CreateDirectory(Output);
            try
            {
                if(!interactive&&EditorApplication.timeSinceStartup>deadline)throw new InvalidOperationException("Full-flow preview timeout at phase "+phase);
                var gui=Gui();if(!gui)return;
                var world=Object.FindAnyObjectByType<ForestCombatWorld>();
                if(interactive)
                {
                    // The user operates the actual title and village screens before this optional encounter setup.
                    if(SceneManager.GetActiveScene().path!=Chapter1RunHost.ChapterScene||!world||!world.Ready)return;
                    SessionState.SetBool(Pending,false);Spawn(world,out _,out _);
                    Debug.Log("Approved new monsters ready after the normal village departure.");return;
                }
                if(phase==0)
                {
                    if(!Object.FindAnyObjectByType<Chapter1TitleScreen>())return;
                    Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                    Call(gui,"Refresh");Capture(gui,null,"00-title.png");Click(Field<Button>(gui,"menuPrimary"));Next();return;
                }
                var village=Object.FindAnyObjectByType<StartingVillageWorld>();
                if(phase==1)
                {
                    if(!village||!village.Ready)return;
                    if(Field<bool>(gui,"openingActive")){Call(gui,"SkipOpening");readyAt=EditorApplication.timeSinceStartup+.25;return;}
                    if(!Chapter1ProfileHost.Service.Snapshot.HasSeen("opening"))return;
                    village.World.RefreshCamera(0,true);Capture(gui,village.World,"01-village.png");
                    village.World.arena.Player.Position=village.Find(StartingVillageWorld.DepartureTable).Position;Physics2D.SyncTransforms();
                    if(!village.TryInteract())throw new InvalidOperationException("Cannot open village blueprint table.");Next();return;
                }
                if(phase==2)
                {
                    if(!Field<GameObject>(gui,"departure").activeInHierarchy)return;
                    var cards=Field<Button[]>(gui,"departureCards");Click(cards[0]);Click(cards[2]);
                    Call(gui,"Refresh");Capture(gui,village.World,"02-village-equipment.png");
                    Click(Field<Button>(gui,"departureGo"));Next();return;
                }
                if(phase==3)
                {
                    village.World.arena.Player.Position=village.Find(StartingVillageWorld.DepartureSign).Position;
                    Physics2D.SyncTransforms();village.World.RefreshCamera(0,true);
                    Capture(gui,village.World,"03-village-departure.png");
                    if(!village.TryInteract()||!village.Departing)throw new InvalidOperationException("Departure sign did not start the planned run.");
                    Next();return;
                }
                if(phase==4)
                {
                    if(SceneManager.GetActiveScene().path!=Chapter1RunHost.ChapterScene||!world||!world.Ready)return;
                    var offers=world.GetComponent<Chapter1StartingBlueprints>();
                    if(!offers||!offers.Complete)return;
                    var run=world.GetComponent<Chapter1RunBinding>().Run;
                    if(!offers.Preselected||run.Plan==null||run.Plan.Blueprints.Count!=2)
                        throw new InvalidOperationException("Run must retain the village's equipment plan; no start-room blueprint offers.");
                    var viewport=world.GetComponent<VillagePixelViewport>();
                    if(viewport&&!viewport.Ready){viewport.enableInBatchMode=true;viewport.enabled=true;readyAt=EditorApplication.timeSinceStartup+.2;return;}
                    Next();return;
                }
                if(phase==5)
                {
                    SessionState.SetBool(Pending,false);
                    Spawn(world,out var barrel,out var roller);roller.aggressive=false;
                    int warnings=0,holds=0,waves=0;
                    Directory.CreateDirectory(Path.Combine(Output,"frames"));
                    using(var driver=world.arena.AcquireManualSimulation())
                    {
                        for(int i=0;i<225;i++)
                        {
                            if(i==80){barrel.aggressive=false;roller.aggressive=true;}
                            driver.Step(.04f);barrel.DrawPrototype();roller.DrawPrototype();
                            if(barrel.Presentation.Warning.enabled)warnings++;
                            if(roller.State==Chapter1PrototypeEnemy.Phase.Telegraph&&roller.AttackAge>=1.8f)
                            { if(roller.Presentation.ShakeOffset!=Vector2.zero)throw new InvalidOperationException("Final charge hold moved.");holds++; }
                            if(roller.Presentation.Wave.enabled)waves++;
                            world.RefreshCamera(0,true);world.GetComponent<VillagePresentation>()?.RefreshForCapture();
                            world.arena.Presentation.Draw(0);
                            Capture(gui,world,"frames/"+i.ToString("000")+".png");
                        }
                    }
                    if(barrel.HitsDelivered!=1||roller.HitsDelivered!=1||Mathf.Abs(roller.Travelled-12)>.06||warnings<25||holds<4||waves<15)
                        throw new InvalidOperationException($"Full-flow encounter: hits={barrel.HitsDelivered}/{roller.HitsDelivered}, travel={roller.Travelled}, warnings={warnings}, hold={holds}, wave={waves}");
                    File.WriteAllText(Path.Combine(Output,"result.json"),"{\"flow\":\"title-village-equipment-departure-run\",\"preselectedBlueprints\":true,\"uiCaptured\":true,\"barrelHits\":1,\"chargeHits\":1,\"lastHoldSeconds\":0.2}");
                    Chapter1ProfileHost.UseStore(null);Debug.Log("NEW_MONSTER_FULL_GAME_FLOW_PASS: title, village equipment, departure sign, planned run, current GUI, both native attacks.");EditorApplication.Exit(0);
                }
            }
            catch(Exception e)
            {
                SessionState.SetBool(Pending,false);Chapter1ProfileHost.UseStore(null);
                File.WriteAllText(Path.Combine(Output,"error.txt"),e.ToString());Debug.LogException(e);
                if(Application.isBatchMode)EditorApplication.Exit(1);
            }
        }
        static void Spawn(ForestCombatWorld world,out Chapter1PrototypeEnemy barrel,out Chapter1PrototypeEnemy roller)
        {
            foreach(var e in world.arena.enemies.ToArray()){e.aggressive=false;e.CancelAttack();e.gameObject.SetActive(false);}
            var room=world.Data.rooms.First(r=>r.kind=="start");float floor=-room.y-15;
            var player=world.arena.Player;player.Position=new Vector2(room.x+16,floor);player.velocity=Vector2.zero;
            var specs=Chapter1NewMonsterCatalog.Load().monsters;
            barrel=(Chapter1PrototypeEnemy)world.SpawnMonster(specs.Single(s=>s.ranged).id,new Vector2(room.x+8,floor),true);
            roller=(Chapter1PrototypeEnemy)world.SpawnMonster(specs.Single(s=>!s.ranged).id,new Vector2(room.x+24,floor),true);
            barrel.Spec.firstDelayMin=barrel.Spec.firstDelayMax=0;barrel.ResetEnemy();Physics2D.SyncTransforms();
        }
        static void Capture(MonoBehaviour gui,ForestCombatWorld world,string name)
        {
            Call(gui,"Refresh");Canvas.ForceUpdateCanvases();
            foreach(var style in gui.GetComponentsInChildren<MonoBehaviour>().Where(b=>b.GetType().Name=="PaperGuiButtonStyle"))style.SendMessage("LateUpdate");
            var canvas=gui.GetComponent<Canvas>();var camera=world?world.View:Camera.main;
            GameObject temporary=null;if(!camera){temporary=new GameObject("Full UI capture");camera=temporary.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;}
            // Update the native world buffer first, then capture the game's actual UI canvas over it.
            if(world&&camera.targetTexture)camera.Render();
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var target=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+1;
                Call(gui,"FitToWorld");foreach(var text in gui.GetComponentsInChildren<Text>())text.SetAllDirty();
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;
                camera.targetTexture=target;camera.rect=rect;RenderTexture.active=active;
                Call(gui,"FitToWorld");RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);if(temporary)Object.DestroyImmediate(temporary);
            }
        }
    }
}
