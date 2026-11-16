using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using ForestCombatV2;
using Stage1VillageExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Deviant.Game.Editor
{
    // Editor-only fixtures; captures the real final backbuffer, including every overlay canvas.
    [InitializeOnLoad]
    public static class PaperGuiShowcase
    {
        const string Request="PaperGuiShowcase.Request";
        static int phase;static bool prepared,startRun;static double next,deadline,quiet;
        static CombatArena.ManualSimulation driver;
        static readonly string[] Names={"title","blueprints","hud","bag","shop","growth","map","pause","death"};
        static string Output=>Path.GetFullPath("../outputs/gui-verification/live");
        static PaperGuiShowcase(){EditorApplication.update+=Tick;}
        [MenuItem("Tools/deviant/Temporary GUI/Capture paper GUI backbuffer")]
        public static void Capture()
        {
            Directory.CreateDirectory(Output);SessionState.SetBool(Request,true);quiet=0;Application.runInBackground=true;
            if(!Application.isBatchMode)
            {
                var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                view.position=new Rect(40,40,1280,760);view.Show();view.Focus();
            }
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Request,false))return;
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating){quiet=0;return;}
                if(quiet==0){quiet=EditorApplication.timeSinceStartup+3;return;}
                if(EditorApplication.timeSinceStartup<quiet)return;
                EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);EditorApplication.EnterPlaymode();return;
            }
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+180;
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(startRun){startRun=false;Chapter1RunHost.Ensure().StartNewRun();next=EditorApplication.timeSinceStartup+1;return;}
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Showcase timeout "+phase);
                if(phase>=Names.Length){Finish(0);return;}
                var gui=Object.FindAnyObjectByType<TemporaryGameGui>();if(!gui)return;
                var world=Object.FindAnyObjectByType<ForestCombatWorld>();
                var host=Object.FindAnyObjectByType<InventoryRuntimeHost>();
                if(phase>0)
                {
                    if(!world||!world.Ready||!host||host.Presenter==null)return;
                    var viewport=Object.FindAnyObjectByType<VillagePixelViewport>();
                    if(viewport&&!viewport.Ready){viewport.enableInBatchMode=true;viewport.enabled=true;return;}
                    if(viewport&&!viewport.ExternalPresentation)return;
                }
                if(!prepared)
                {
                    if(phase==1){driver=world.arena.AcquireManualSimulation();foreach(var enemy in world.arena.enemies)if(enemy){enemy.aggressive=false;enemy.CancelAttack();}}
                    if(phase==2)
                    {
                        var offers=world.GetComponent<Chapter1StartingBlueprints>();var spawn=world.arena.Player.Position;
                        for(int i=0;i<2;i++){world.arena.Player.Position=offers.OfferPosition(i);offers.TryChoose(i);}world.arena.Player.Position=spawn;
                        world.GetComponent<Chapter1RunBinding>().Run.Economy.Earn(240);
                        foreach(var entry in host.Content.items.Take(8))host.Inventory.TryAcquire(new InkInstance(entry.CreateDefinition()),out _);
                    }
                    if(phase==3){host.Presenter.Open();host.Presenter.SelectBag(0);}
                    if(phase==4)
                    {
                        host.Presenter.Close();var room=world.Data.rooms.First(r=>Chapter1MaintenanceFacility.IsFacility(r.kind));
                        world.arena.Player.Position=new Vector2(room.x+room.w/2f,-(room.y+room.entryY));world.GetComponent<Chapter1MaintenanceShop>().TryOpen();
                    }
                    if(phase==5)
                    {
                        var shop=world.GetComponent<Chapter1MaintenanceShop>();var g=shop.Growth;
                        var p=g.GrantPalette(new[]{"red","red","blue","blue"});g.Bind(p.Id,shop.GrowthWeapon(0));
                        g.Insert(shop.GrowthWeapon(0),0,g.GrantInk("ink3_red_01").Id);
                        g.GrantPalette(new[]{"red","red","red"});foreach(var part in g.Data.parts)g.GrantInk(part.id);
                        Click(gui,"팔레트·잉크 관리");
                    }
                    if(phase==6){gui.Back();gui.Back();world.FullMap.SetOpen(true);}
                    if(phase==7){gui.Back();gui.Back();}
                    if(phase==8){gui.Back();world.GetComponent<Chapter1RunBinding>().Host.Flow.TryDie(world.arena.Now);}
                    gui.Refresh();prepared=true;next=EditorApplication.timeSinceStartup+1.2;return;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,Names[phase]+".png"));
                Debug.Log("PAPER_GUI_SCREEN "+Names[phase]);
                // Let ScreenCapture finish before changing this state.
                prepared=false;phase++;next=EditorApplication.timeSinceStartup+1;
                if(phase==1)startRun=true;
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void Click(TemporaryGameGui gui,string label)=>gui.GetComponentsInChildren<Button>().First(b=>b.interactable&&b.GetComponentInChildren<Text>()?.text==label).onClick.Invoke();
        static void Finish(int code)
        {
            driver?.Dispose();SessionState.SetBool(Request,false);EditorApplication.ExitPlaymode();
            if(Application.isBatchMode||Environment.GetCommandLineArgs().Contains("-paperGuiCapture"))EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
        }
    }
}
