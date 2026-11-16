using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using Deviant.Run;
using ForestCombatV2;
using Stage1VillageExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Deviant.Game.Editor
{
    // Repeatable acceptance checks against the actual chapter scene and real UI button listeners.
    [InitializeOnLoad]
    public static class TemporaryGuiVerification
    {
        const string Request="TemporaryGuiVerification.Request";
        static int phase, assertions;
        static bool shopVerified;
        static System.Collections.Generic.Queue<Action> growthChecks;
        static double readyAt, deadline, quietUntil;
        static CombatArena.ManualSimulation driver;
        static TemporaryGameGui gui;
        static ForestCombatWorld world;
        static InventoryRuntimeHost host;
        static string Output=>Path.GetFullPath("../outputs/gui-verification");
        static TemporaryGuiVerification() {EditorApplication.update+=Tick;}
        [MenuItem("Tools/deviant/Temporary GUI/Verify six screens")]
        public static void Verify()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new Exception("Unity 6000.6.5f1 required.");
            Directory.CreateDirectory(Output);SessionState.SetBool(Request,true);
            quietUntil=0;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Request,false))return;
            // executeMethod can run before AssetDatabase's deferred reload. Entering play
            // immediately destroys nonserialized run services during that late reload.
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating){quietUntil=0;return;}
                if(quietUntil==0){quietUntil=EditorApplication.timeSinceStartup+3;return;}
                if(EditorApplication.timeSinceStartup<quietUntil)return;
                EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
                EditorApplication.EnterPlaymode();return;
            }
            if(EditorApplication.isCompiling)return;
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+240;
            if(EditorApplication.timeSinceStartup<readyAt)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("GUI verification timed out at phase "+phase);
                gui=Object.FindAnyObjectByType<TemporaryGameGui>();if(!gui)return;
                if(phase==0)
                {
                    if(!Object.FindAnyObjectByType<Chapter1TitleScreen>())return;
                    Capture("05-title");Click("새 게임");Next();return;
                }
                if(phase==8)
                {
                    if(!Object.FindAnyObjectByType<Chapter1TitleScreen>())return;
                    Check(Chapter1RunHost.Instance.ChapterTwoPending,"Chapter 2 placeholder is reached");Capture("05-chapter2");Click("타이틀로");
                    Check(!Chapter1RunHost.Instance.ChapterTwoPending,"Placeholder returns to title");Finish(true);return;
                }
                world=Object.FindAnyObjectByType<ForestCombatWorld>();host=Object.FindAnyObjectByType<InventoryRuntimeHost>();
                if(!world||!world.Ready||!host||host.Presenter==null)return;
                var art=world.GetComponent<VillagePresentation>();if(art&&!art.Ready)return;
                var run=world.GetComponent<Chapter1RunBinding>();var shop=world.GetComponent<Chapter1MaintenanceShop>();
                var offers=world.GetComponent<Chapter1StartingBlueprints>();var player=world.arena.Player;
                gui.Refresh();
                switch(phase)
                {
                    case 1:
                        var viewport=Object.FindAnyObjectByType<VillagePixelViewport>();
                        if(viewport && !viewport.Ready)
                        { viewport.enableInBatchMode=true;viewport.enabled=true;return; }
                        if(viewport && !viewport.ExternalPresentation)return;
                        Check(!viewport||viewport.ExternalPresentation,"Native world uses the canvas output behind the GUI");
                        var guiSprites=Resources.LoadAll<Sprite>("TemporaryGUI");int warmCopies=Array.FindAll(guiSprites,s=>s.name.EndsWith("-warm")).Length;
                        var pixelSprites=Resources.LoadAll<Sprite>("TemporaryGUI/Pixel");
                        Check(guiSprites.Length-warmCopies-pixelSprites.Length==23&&warmCopies==6,"Original GUI sprite assets and warm frame copies load");
                        Check(pixelSprites.Length==20&&pixelSprites.All(s=>s.texture.width==32&&s.texture.height==32&&s.texture.filterMode==FilterMode.Point&&s.border==Vector4.zero),"20 native 32px point-filtered ink symbols load without slicing borders");
                        Check(host.Inventory.Capacity==5,"5 individual bag slots shared by every ink");
                        Check(world.ExternalPresentation&&world.FullMap.ExternalInput,"Old presentation and duplicate keys are suppressed");
                        driver=world.arena.AcquireManualSimulation();foreach(var enemy in world.arena.enemies)if(enemy){enemy.aggressive=false;enemy.CancelAttack();}
                        Capture("04-blueprints");
                        var spawn=player.Position;player.Position=offers.ExitDoor+Vector2.left*1.5f;
                        Check(offers.ExitLocked&&offers.PlayerAtLockedExit,"Starting room exit stays locked before two blueprints");Capture("04-start-exit-locked");
                        player.Position=offers.OfferPosition(0);Check(offers.TryChoose(0),"First in-world blueprint equips");
                        player.Position=offers.OfferPosition(1);Check(offers.TryChoose(1)&&offers.Complete,"Second different blueprint completes selection");
                        Check(!offers.ExitLocked,"Second blueprint opens the starting room exit");
                        player.Position=spawn;
                        foreach(var entry in host.Content.items.Take(5))Check(host.Inventory.TryAcquire(new InkInstance(entry.CreateDefinition()),out _),"Acquire individual ink");
                        run.Run.Economy.Earn(1000);Next();break;
                    case 2:
                        Check(gui.gameObject.activeSelf&&world.ExternalPresentation,"Play GUI remains active with legacy presentation suppressed");
                        Check(Object.FindAnyObjectByType<Chapter1MapSelector>().ExternalPresentation,"Play GUI hides development navigation without disabling its component");
                        Capture("01-hud");
                        ClickPrefix("가방 ");Check(host.Presenter.IsOpen,"HUD bag button opens inventory");
                        Check(!world.arena.Controls.Current.SimulationPaused,"Bag blocks combat without pausing simulation");
                        var room=world.Data.rooms.First(r=>Chapter1MaintenanceFacility.IsFacility(r.kind));player.Position=new Vector2(room.x+room.w/2f,-(room.y+room.entryY));
                        Next();break;
                    case 3:
                        var bagButtons=Field<Button[]>("slots");
                        int stat=Enumerable.Range(0,host.Inventory.Capacity).First(i=>host.Inventory.BagAt(i)?.Definition.Kind==InkKind.Stat);
                        bagButtons[stat].onClick.Invoke();gui.Refresh();Capture("02-bag");
                        Capture("02-bag-1280",1280,720);Capture("02-bag-1600",1600,900);Capture("02-bag-1920",1920,1080);Click("장착");
                        Check(host.Inventory.Used==4,"Equip button transfers a physical bag item");
                        gui.Back();Check(!host.Presenter.IsOpen&&!host.Presenter.Paused,"One Back closes only the bag");
                        gui.Back();Check(host.Presenter.Paused,"Back from gameplay pauses");Capture("05-pause");
                        using(var external=world.arena.Controls.OpenScope("verification other pause"))
                        {external.PauseSimulation();gui.Back();Check(!host.Presenter.Paused&&world.arena.Controls.Current.SimulationPaused,"Closing our pause preserves another owner's pause");}
                        Check(!world.arena.Controls.Current.SimulationPaused,"External pause releases independently");
                        Next();break;
                    case 4:
                        if(!world.FullMap.IsOpen){Click("지도 M");Check(world.FullMap.IsOpen,"Minimap button opens full map");readyAt=EditorApplication.timeSinceStartup+.5;return;}
                        Capture("06-map");float zoom=world.FullMap.Viewport.Zoom;Click("+");Check(world.FullMap.Viewport.Zoom>zoom,"Map zoom button uses the map viewport");
                        gui.Back();Check(!world.FullMap.IsOpen&&!host.Presenter.Paused,"Map Back does not also open pause");
                        Check(shop.TryOpen(),"Shop opens only inside actual facility");gui.Refresh();Next();break;
                    case 5:
                        if(!shopVerified)
                        {
                        Click("성장");Check(Field<Button[]>("tradeButtons")[0].gameObject.activeInHierarchy&&!Field<Button[]>("paletteButtons")[0].gameObject.activeInHierarchy,"Shop tabs show only the selected actions");Capture("03-shop-growth");
                        Click("스탬프");Capture("03-shop-stamps");
                        Click("잉크");Check(Field<System.Collections.Generic.List<Button>>("stallButtons").Count>=2,"Ink tab shows the facility's ink stall");Capture("03-shop-ink");Click("팔레트");
                        // Palettes come with the departure; the tab shows each weapon's palette and opens its management.
                        Check(shop.Growth.Equipped(shop.GrowthWeapon(0))!=null&&Field<Button[]>("paletteButtons")[0].interactable,"Departure palette is listed for weapon 1");
                        Capture("03-shop");
                        shopVerified=true;
                        }
                        if(!VerifyGrowth(shop))return;
                        shop.Service.Economy.TrySpend(shop.Service.Economy.Gold);gui.Refresh();
                        Check(!Field<Button[]>("tradeButtons")[0].interactable&&Field<System.Collections.Generic.List<Button>>("stallButtons").All(b=>!b.interactable),"Insufficient funds disable unaffordable purchases; management remains accessible");
                        gui.Back();Check(!shop.IsOpen&&!host.Presenter.Paused,"Shop Back releases only shop pause");
                        // Seal challenge reward: three augment cards, one marked, then taken.
                        var augments=world.GetComponent<AugmentRewardChoice>();
                        Check(augments&&augments.OpenChoice(),"The seal reward opens three augment cards");gui.Refresh();
                        Field<Button[]>("augmentCards")[1].onClick.Invoke();gui.Refresh();Capture("07-augment");
                        Click("증강 획득");Check(!augments.IsOpen&&!world.arena.Controls.Current.SimulationPaused,"Taking an augment closes the choice and resumes play");
                        run.Host.Flow.TryDie(world.arena.Now);gui.Refresh();Capture("05-death");
                        Check(run.ShowingResult&&world.arena.Controls.Current.SimulationPaused,"Death result owns simulation pause");
                        gui.Back();Check(run.ShowingResult,"Escape cannot dismiss the result");
                        driver.Dispose();driver=null;Click("다시 하기");Next();break;
                    case 6:
                        if(run.ShowingResult)return;
                        Check(host.Inventory.Used==0&&run.Run.Economy.Gold==0,"Retry resets inventory and economy");
                        driver=world.arena.AcquireManualSimulation();
                        var boss=world.GetComponent<Chapter1BossEncounter>();player.Position=boss.Boss.Position;gui.Refresh();Capture("01-boss");
                        Check(Field<Image>("bossFill").color==boss.Boss.bossBarColor,"Boss bar takes the boss phase colour");
                        boss.Boss.health=0;Next();break;
                    case 7:
                        var exit=world.GetComponent<Chapter1BossEncounter>();if(!exit.ExitOpen)return;
                        player.Position=exit.ExitPosition;Check(exit.TryUseExit(),"Boss defeat unlocks the actual exit");gui.Refresh();Capture("05-clear");
                        driver.Dispose();driver=null;Click("2챕터 입구로");Next();break;
                }
            }
            catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"failure.txt"),error.ToString());Finish(false);}
        }
        static T Field<T>(string name)=>(T)typeof(TemporaryGameGui).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(gui);
        static bool VerifyGrowth(Chapter1MaintenanceShop shop)
        {
            if(growthChecks!=null)
            {
                if(growthChecks.Count==0)return true;
                growthChecks.Dequeue()();readyAt=EditorApplication.timeSinceStartup+.35;return false;
            }
            growthChecks=new System.Collections.Generic.Queue<Action>();
            void Step(Action action)=>growthChecks.Enqueue(action);
            var g=shop.Growth;g.Economy.Earn(2000);
            var palette=g.GrantPalette(new[]{"red","red","blue","blue"});
            var red=g.GrantInk("ink3_red_01");var replacement=g.GrantInk("ink3_red_03");var blue=g.GrantInk("ink3_blue_02");
            foreach(var part in g.Data.parts)g.GrantInk(part.id);
            Step(()=>Click("팔레트·잉크 관리"));
            Step(()=>{
            Check(Field<GameObject>("growthPanel").activeInHierarchy,"Growth management opens above shop");
            var paletteList=Field<RectTransform>("growthPalettes");
            paletteList.parent.GetComponent<ScrollRect>().verticalNormalizedPosition=0;
            });
            Step(()=>{
            ClickNamed("GrowthPalette"+palette.Id);
            Check(g.Equipped(shop.GrowthWeapon(0))==palette,"Palette button binds to actual weapon A");
            });
            Step(()=>ClickNamed("GrowthInk"+red.Id));
            Step(()=>{
            ClickButton(Field<Button>("growthInsert"));
            Check(palette.Slots[0].Part.id==red.Part.id&&!g.Inks.Contains(red),"Free insert consumes selected ink and equips it");
            });
            Step(()=>{
            int gold=g.Economy.Gold,fee=g.UpgradeCost(shop.GrowthWeapon(0),0);
            ClickButton(Field<Button>("growthUpgrade"));
            Check(palette.Slots[0].Level==1&&g.Economy.Gold==gold-fee,"Upgrade button charges exact current price");
            });
            Step(()=>ClickNamed("GrowthInk"+replacement.Id));
            Step(()=>{
            int gold=g.Economy.Gold;
            ClickButton(Field<Button>("growthInsert"));
            Check(palette.Slots[0].Part==replacement.Part&&palette.Slots[0].Level==1&&g.Economy.Gold==gold&&g.Inks.Any(i=>i.Id==red.Id),"Free replacement preserves level and returns the old ink");
            });
            Step(()=>{
            ClickNamed("GrowthInk"+blue.Id);Check(!Field<Button>("growthInsert").interactable,"Wrong slot colour disables ordinary insert");
            });
            Step(()=>{
            int gold=g.Economy.Gold;ClickButton(Field<Button>("growthRecolor"));
            Check(palette.ColorChanged&&palette.Slots[0].Color=="blue"&&palette.Slots[0].Part==blue.Part&&g.Economy.Gold==gold-g.Data.colorChangeGold,"Recolour and replace consumes once at live price");
            Check(g.ColorChangesUsed==1&&g.ColorChangesLeft==g.Data.colorChangesPerRun-1,"Recolour uses one of the run allowance");
            });
            Step(()=>{
            int gold=g.Economy.Gold,count=g.Inks.Count,fee=g.SalePrice;
            ClickButton(Field<Button>("growthSell"));Check(g.Inks.Count==count-1&&g.Economy.Gold==gold+fee,"Sell button removes one ink and credits current sale price");
            });
            Step(()=>{
            int gold=g.Economy.Gold,count=g.Inks.Count;
            ClickButton(Field<Button>("growthDestroy"));Check(g.Inks.Count==count-1&&g.Economy.Gold==gold+g.DestroyedInkGoldBonus,"Destroy removes one ink and pays only the unlocked recovery bonus");
            });
            Step(()=>{
            ClickButton(Field<Button[]>("growthFilters")[4]);
            Check(Field<RectTransform>("growthInks").GetComponentsInChildren<Button>().All(b=>g.Inks.Any(i=>i.Part.color=="blue"&&b.name=="GrowthInk"+i.Id)),"Blue filter shows only owned blue inks");
            });
            Step(()=>{
            ClickButton(Field<Button[]>("growthFilters")[0]);
            Capture("04-growth");Capture("04-growth-1280",1280,720);Capture("04-growth-1920",1920,1080);
            });
            Step(()=>{
            ClickButton(Field<Button[]>("growthWeapons")[1]);
            Check(!gui.GetComponentsInChildren<Button>().First(b=>b.name=="GrowthPalette"+palette.Id).interactable,"Weapon B cannot take weapon A bound palette");
            });
            Step(()=>{
            ClickButton(Field<Button[]>("growthWeapons")[0]);
            gui.Back();Check(shop.IsOpen&&!Field<GameObject>("growthPanel").activeInHierarchy&&world.arena.Controls.Current.SimulationPaused,"Growth Back returns to shop and preserves its pause");
            });
            readyAt=EditorApplication.timeSinceStartup+.35;return false;
        }
        static void ClickNamed(string name)=>ClickButton(gui.GetComponentsInChildren<Button>().First(b=>b.name==name));
        static void Check(bool condition,string description) {if(!condition)throw new Exception(description);assertions++;Debug.Log("GUI_CHECK "+description);}
        static void Click(string label)=>ClickWhere(t=>t==label,label);
        static void ClickPrefix(string label)=>ClickWhere(t=>t.StartsWith(label),label);
        static void ClickWhere(Func<string,bool> predicate,string description)
        {
            var button=gui.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.interactable&&predicate(b.GetComponentInChildren<Text>()?.text??""));
            Check(button,"Visible actionable button: "+description);ClickButton(button);
        }
        static void ClickButton(Button button)
        {
            Check(button&&button.interactable&&button.gameObject.activeInHierarchy,"Enabled UI action: "+button.name);
            Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)
                Debug.LogError("GUI_RAYCAST "+button.name+" at "+point+" screen "+Screen.width+"x"+Screen.height+" depth "+button.image.depth+" cull "+button.image.canvasRenderer.cull+" target "+button.image.raycastTarget+" groups "+string.Join(",",button.GetComponentsInParent<CanvasGroup>().Select(c=>c.name+":"+c.blocksRaycasts))+" hits "+string.Join(",",hits.Select(h=>h.gameObject.name+"/"+h.gameObject.transform.parent.name)));
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Pointer raycast reaches control: "+button.name);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);gui.Refresh();
        }
        static void Next(){phase++;readyAt=EditorApplication.timeSinceStartup+.7;}
        static void Capture(string name,int width=960,int height=540)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var style in gui.GetComponentsInChildren<PaperGuiButtonStyle>())style.SendMessage("LateUpdate");
            world=Object.FindAnyObjectByType<ForestCombatWorld>();
            if(world){world.GetComponent<VillagePresentation>()?.RefreshForCapture();world.arena.Presentation.Draw(0);}
            gui.Refresh();
            foreach(var label in gui.GetComponentsInChildren<Text>())
                foreach(char glyph in label.text)
                    if(!char.IsWhiteSpace(glyph)&&!label.font.GetCharacterInfo(glyph,out _,label.font.fontSize))
                        throw new Exception($"Missing visible glyph '{glyph}' in {name}: {label.text}");
            var canvas=gui.GetComponent<Canvas>();var camera=world?world.View:Camera.main;
            GameObject temporary=null;if(!camera){temporary=new GameObject("Capture camera");camera=temporary.AddComponent<Camera>();camera.backgroundColor=new Color(.12f,.15f,.13f);camera.clearFlags=CameraClearFlags.SolidColor;}
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var target=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+1;
                gui.FitToWorld();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;camera.targetTexture=target;camera.rect=rect;RenderTexture.active=active;gui.FitToWorld();
                RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);if(temporary)Object.DestroyImmediate(temporary);Canvas.ForceUpdateCanvases();
            }
        }
        static void Finish(bool success)
        {
            driver?.Dispose();driver=null;SessionState.SetBool(Request,false);
            File.WriteAllText(Path.Combine(Output,"result.json"),"{\"passed\":"+(success?"true":"false")+",\"assertions\":"+assertions+",\"phase\":"+phase+"}");
            EditorApplication.ExitPlaymode();if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(success?0:1);
        }
    }
}
