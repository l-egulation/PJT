using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using Deviant.Run;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Game.Editor
{
    // Real-scene acceptance checks. Only the profile store is isolated; model, UI and input are production code.
    // Run in a disposable validation project: -executeMethod Deviant.Game.Editor.ApprovedCoreVerification.Run
    [InitializeOnLoad]
    public static partial class ApprovedCoreVerification
    {
        const string Request = "ApprovedCoreVerification.Request";
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static TemporaryGameGui gui;
        static ForestCombatWorld world;
        static InventoryRuntimeHost host;
        static Chapter1MaintenanceShop shop;
        static SketchEquipment model;
        static CombatArena.ManualSimulation driver;
        static Keyboard keyboard;
        static Queue<Action> checks;
        static readonly List<string> passed = new List<string>();
        static RunInkWeapon draft;
        static string ink;
        static int phase, previousFrame = -1;
        static double quietUntil, deadline;
        static string Output => SessionState.GetBool("InventoryFlowVerification",false)?Path.GetFullPath("../outputs/unity-inventory-flow-20261116"):Path.GetFullPath(SessionState.GetBool("ApprovedBagCraftVerification",false)?"../outputs/approved-bag-craft-verification":SessionState.GetBool("ApprovedEquipmentVerification",false)?"../outputs/approved-equipment-verification":"../outputs/approved-core-verification");
        static ApprovedCoreVerification() { EditorApplication.update += Tick; }

        [MenuItem("Tools/deviant/Approved UI/Verify live core and capture")]
        public static void Run() => Begin(null);
        // The mode flag picks the output folder, so it is set before the folder is prepared and old results are cleared.
        static void Begin(string mode)
        {
            SessionState.SetBool("InventoryFlowVerification",false);
            SessionState.SetBool("ApprovedEquipmentVerification",false);
            SessionState.SetBool("ApprovedBagCraftVerification",false);
            if(mode!=null)SessionState.SetBool(mode,true);
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Unity 6000.6.5f1 required.");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before verification.");
            if(!Application.isBatchMode)
            {
                var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                view.position=new Rect(80,80,1280,760);view.Show();view.Focus();
            }
            Directory.CreateDirectory(Output);File.Delete(Path.Combine(Output,"failure.txt"));File.Delete(Path.Combine(Output,"result.json"));
            SessionState.SetBool(Request, true); quietUntil = 0;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Request, false)) return;
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating) { quietUntil = 0; return; }
                    if (quietUntil == 0) { quietUntil = EditorApplication.timeSinceStartup + 3; return; }
                    if (EditorApplication.timeSinceStartup < quietUntil) return;
                    EditorSettings.enterPlayModeOptionsEnabled = false;
                    EditorSceneManager.playModeStartScene = null;
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorApplication.EnterPlaymode(); return;
                }
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 240;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out at phase " + phase);
                if (EditorApplication.isCompiling || Time.frameCount == previousFrame) return;
                previousFrame = Time.frameCount;
                if (phase == 0)
                {
                    Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                    SceneManager.LoadScene(Chapter1MapSelector.ProceduralScene); phase = 1; return;
                }
                world = Object.FindAnyObjectByType<ForestCombatWorld>();
                host = Object.FindAnyObjectByType<InventoryRuntimeHost>();
                gui = Object.FindAnyObjectByType<TemporaryGameGui>();
                if (!world || !world.Ready || !host || host.Presenter == null || !gui || world.arena.Sketch == null) return;
                if (phase == 1) { Prepare(); phase = 2; return; }
                if (checks.Count == 0) { Finish(true); return; }
                ReleaseKeys(); checks.Dequeue()();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "failure.txt"), error.ToString());
                Finish(false);
            }
        }
        static void Prepare()
        {
            model = world.arena.Sketch; shop = world.GetComponent<Chapter1MaintenanceShop>();
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
            driver = world.arena.AcquireManualSimulation();
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>("ApprovedCoreAcceptanceKeyboard");
            var preview = model.RollWeapon();
            var native = model.State.Arsenal.Acquire(preview.Form, preview.Model.Skill, new[] { "red", "red", null, null, null, null });
            draft = model.Find(native.Id);
            model.State.Arsenal.Acquire(preview.Form, preview.Model.Skill, new string[1]);
            ink = model.Catalog.inks.First(i => model.RemainingInkUses(i.id) == 0).id;
            model.Register(ink);
            Check(model.RemainingInkUses(ink) == model.State.Arsenal.Rules.InkUsesPerAcquisition, "Ink acquisition uses the configured grant size");
            checks = new Queue<Action>();
            if(SessionState.GetBool("InventoryFlowVerification",false)){PrepareInventoryFlowChecks();return;}
            PrepareInventoryFlowChecks();
            checks.Enqueue(() =>
            {
                host.Presenter.Close();
                var room=world.Data.rooms.First(r=>Chapter1MaintenanceFacility.IsFacility(r.kind));
                world.arena.Player.Position=new Vector2(room.x+room.w/2f,-(room.y+room.entryY));
                model.Economy.Earn(10000);
            });
            checks.Enqueue(() =>
            {
                Check(shop.TryOpen(), "World facility opens the actual shop");
                foreach(var card in host.Growth.Augments.Catalog.augments.Take(3))host.Growth.Augments.Grant(card.id);
                gui.Refresh(); Press(Key.E);
                Check(Button("Owned augment 0") && Button("Owned augment 1"), "Owned augments render the real run collection");
                Press(Key.DownArrow); Check(Get<int>("liveAugmentPick")==1,"Arrow selects owned augment details");
                var ownedContent=Get<RectTransform>("liveDetail");
                Check(ownedContent.parent.name=="Owned augment list" && ownedContent.rect.width==660,"Owned augments use the approved full-width scrolling rows");
                Check(Button("Owned augment 2") && Button("Owned augment 0").transform.parent.GetComponent<RectTransform>().rect.width==640,"Three owned augment rows keep their icons, names and effects together");
                Capture("owned-augments",1280,720); Press(Key.F);
                Check((int)Get<object>("nativeShop").GetType().GetField("Tab").GetValue(Get<object>("nativeShop"))==0,"F returns from owned augments to the same shop");
                var book = Get<object>("nativeShop"); SetBook(book, "Tab", 0);
                SetBook(book,"Offer",0);Set("liveAction",0);gui.Refresh();
                PressPad(GamepadButton.DpadRight);Check((int)book.GetType().GetField("Offer").GetValue(book)==1,"Gamepad direction selects the next actual shop item");
                var stock = model.Shop(("chapter1:" + shop.Facility)); int index = Enumerable.Range(0, stock.Count).First(i => !stock[i].Sold && stock[i].Weapon != null);
                SetBook(book, "Offer", index); gui.Refresh(); Capture("shop", 960, 540);
                int gold = model.Economy.Gold, cost = stock[index].Price;
                PressPad(GamepadButton.South);
                var receipt = Get<SketchChoice>("liveShopReceipt");
                Check(receipt?.Weapon != null && stock[index].Sold && model.Economy.Gold == gold - cost, "Shop action charges selected actual price and leaves sold shelf");
                Check(model.Weapons.Any(w => w.Id == receipt.Weapon.Id), "Purchase receipt references the acquired inventory identity");
                Capture("shop-receipt", 1280, 720);
                Click("Configure shop receipt");
                Check(host.Presenter.IsOpen && Get<Guid>("liveWeapon") == receipt.Weapon.Id && Get<string>("liveStage") == "palette", "Receipt configures exactly the purchased weapon");
                Press(Key.Escape);
                Check(!host.Presenter.IsOpen && shop.IsOpen && model.Shop(("chapter1:" + shop.Facility))[index].Sold, "Returning from configuration preserves same shop and sold state");
                Capture("shop-sold",1280,720);
            });
            checks.Enqueue(() =>
            {
                foreach (var w in model.Weapons.Where(w => !w.Destroyed && !w.Confirmed).ToArray()) model.Confirm(w.Id);
                var book = Get<object>("nativeShop"); var stock = model.Shop(("chapter1:" + shop.Facility));
                int index = Enumerable.Range(0, stock.Count).First(i => !stock[i].Sold && stock[i].Ink != null);
                string chosen = stock[index].Ink.id; int before = model.RemainingInkUses(chosen);
                SetBook(book, "Offer", index); Set("liveAction", 0); gui.Refresh(); Click("Buy selected shop item");
                Check(model.RemainingInkUses(chosen) == before + model.State.Arsenal.Rules.InkUsesPerAcquisition, "Shop ink purchase uses the configured grant size");
                Check(!Button("Configure shop receipt").interactable, "Ink receipt disables configuration without an unfinished sketch");
                Set("liveAction", 1); Press(Key.F);
                Check(!host.Presenter.IsOpen && Get<SketchChoice>("liveShopReceipt") != null, "F cannot invoke disabled ink configuration");
                PressPad(GamepadButton.East);Check(shop.IsOpen && Get<SketchChoice>("liveShopReceipt")==null,"Gamepad East closes only the shop receipt");
                PressPad(GamepadButton.East);
                Check(!shop.IsOpen, "Gamepad East then closes only the shop");
            });
            checks.Enqueue(() =>
            {
                host.Presenter.TogglePause(); gui.Refresh();
                var pauseButton=Get<Button>("menuShortcuts");
                var pauseGroup=pauseButton.gameObject.AddComponent<CanvasGroup>();
                pauseGroup.alpha=.8f;pauseGroup.blocksRaycasts=false;
                Call("OpenApprovedSettings"); gui.Refresh();
                Check(pauseGroup.alpha==0&&!pauseGroup.interactable&&!pauseGroup.blocksRaycasts,"Settings hides previous menu even after LayoutMenu refresh");
                var panel=Get<GameObject>("approvedSettings");
                Check(panel.activeInHierarchy && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(panel.transform),"Settings opens with a usable selected action");
                Capture("settings",1280,720);
                int proposedWidth=Screen.width==960?1280:960;
                Set("pendingWidth",proposedWidth);Set("pendingHeight",proposedWidth*9/16);Call("TestDisplay");
                var modal=Get<GameObject>("displayQuestion");
                var labels=modal.GetComponentsInChildren<Text>();
                Check(labels.Any(t=>t.text=="F")&&labels.Any(t=>t.text=="G"),"Display confirmation shows separate F and G keycaps");
                Check(labels.Any(t=>t.text.Contains(Get<int>("pendingWidth").ToString())),"Display confirmation identifies the resolution being kept");
                Check(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(modal.transform),"Display confirmation owns selection focus");
                var coveredBody=Get<RectTransform>("settingsBody").GetComponent<CanvasGroup>();
                Check(coveredBody&&coveredBody.alpha==0&&!coveredBody.interactable,"Display confirmation hides settings content without obscuring the game background");
                Capture("settings-display-confirmation",1280,720);
                PressDisplayPad(GamepadButton.South);
                Check(!Get<bool>("testingDisplay")&&!Get<GameObject>("displayQuestion"),"Gamepad South keeps the proposed display settings");
                Check(Get<GameObject>("approvedSettings").activeInHierarchy,"Display confirm does not close its parent settings");
                Check(Get<int>("pendingWidth")==proposedWidth,"Controller keep preserves the proposed resolution");
                Set("pendingWidth",Screen.width==960?1280:960);Call("TestDisplay");PressDisplayPad(GamepadButton.East);
                Check(!Get<bool>("testingDisplay")&&!Get<GameObject>("displayQuestion"),"Gamepad East restores the previous display settings");
                Check(Get<int>("pendingWidth")==Get<int>("savedWidth")&&Get<int>("pendingHeight")==Get<int>("savedHeight"),"Controller revert restores the saved resolution");
                Check(!(bool)Call("TakeBack"),"Display controller cancel is not handled again by the parent menu");
                Call("TestDisplay");PressDisplayPad(GamepadButton.Start);Check(!Get<bool>("testingDisplay"),"Controller Start restores display settings");
                Call("TestDisplay");PressDisplayKey(Key.G);Check(!Get<bool>("testingDisplay"),"Keyboard G still keeps display settings");
                Call("TestDisplay");PressDisplayKey(Key.F);Check(!Get<bool>("testingDisplay"),"Keyboard F still reverts display settings");
                Check(coveredBody.alpha==1&&coveredBody.interactable,"Closing confirmation restores settings content");
                Set("settingsTab",1);Call("RefreshApprovedSettings");Capture("settings-sound",1280,720);
                Set("settingsTab",2);Call("RefreshApprovedSettings");Capture("settings-controls",1280,720);
                Call("CloseApprovedSettings");
                Check(Mathf.Approximately(pauseGroup.alpha,.8f)&&pauseGroup.interactable&&!pauseGroup.blocksRaycasts,"Closing settings restores the previous CanvasGroup values exactly");
                pauseGroup.alpha=1;pauseGroup.blocksRaycasts=true;host.Presenter.TogglePause();gui.Refresh();
                world.FullMap.SetOpen(true);gui.Refresh();Capture("full-map",1280,720);world.FullMap.SetOpen(false);gui.Refresh();
            });
            checks.Enqueue(() =>
            {
                model.State.DebugAddReward();var binding=world.GetComponent<SketchRunBinding>();
                Check(binding.TryOpenBankedReward(),"Banked reward opens through the actual run binding");
                RewardCall(binding,"RefreshRewardScreen");var screen=RewardView(binding);
                Check(screen.Items.Count==model.State.PendingOffer.Length && screen.Items.All(i=>!string.IsNullOrEmpty(i.Name)&&!string.IsNullOrEmpty(i.Detail)),"Reward names and descriptions bind to the actual offer");
                screen.Move(1);Check(screen.Selected==1,"Reward navigation changes the selected candidate");
                binding.ToggleRewardBag();Call("SetLiveTab",1);gui.Refresh();
                PressPad(GamepadButton.LeftShoulder);
                Check(host.Presenter.IsOpen&&binding.PanelOpen&&Get<int>("liveTab")==0,"Reward comparison consumes LB as a tab change without closing the bag");
                binding.ToggleRewardBag();gui.Refresh();
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"reward-live.png"));
            });
            checks.Enqueue(() =>
            {
                var binding=world.GetComponent<SketchRunBinding>();var screen=RewardView(binding);int before=model.Weapons.Count;
                screen.Activate();RewardCall(binding,"RefreshRewardScreen");
                Check(screen.Acquired && model.Weapons.Count==before+1,"Selected reward action acquires the actual weapon and displays its receipt");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"reward-receipt-live.png"));
            });
            checks.Enqueue(() =>
            {
                world.GetComponent<SketchRunBinding>().ContinueReward();
                var choice=world.GetComponent<AugmentRewardChoice>();Check(choice.OpenChoice(),"Actual free augment reward opens");gui.Refresh();
                var cards=Get<Button[]>("augmentCards");Check(cards.Count(b=>b.gameObject.activeInHierarchy)==choice.Offer.Length,"All actual augment candidates render");
                cards[1].onClick.Invoke();gui.Refresh();
                Check(Get<int>("augmentPick")==1 && Get<Button>("augmentTake").interactable,"Selecting an augment enables its take action");
                Capture("augment-choice",1280,720);
                string id=choice.Offer[1].id;Get<Button>("augmentTake").onClick.Invoke();gui.Refresh();
                Check(!choice.IsOpen && host.Growth.Augments.Owned.Any(a=>a.id==id),"Augment take transfers the selected ID into the real owned list");
            });
            checks.Enqueue(() =>
            {
                var run=world.GetComponent<Chapter1RunBinding>();run.Host.Flow.TryDie(world.arena.Now);gui.Refresh();
                Check(run.ShowingResult && Get<GameObject>("menu").activeInHierarchy,"Actual death routes to the themed result screen");
                Check(Get<Text>("resultHeading").alignment==TextAnchor.MiddleCenter,"Result headline remains centered");
                Check(EventSystem.current.currentSelectedGameObject==Get<Button>("menuPrimary").gameObject,"Result immediately focuses its primary action and displays its F keycap");
                Capture("death-result",960,540);Capture("death-result-1280",1280,720);
            });
        }
        static SketchRewardChoiceScreen RewardView(SketchRunBinding binding)=>(SketchRewardChoiceScreen)typeof(SketchRunBinding).GetField("rewardScreen",Hidden).GetValue(binding);
        static object RewardCall(SketchRunBinding binding,string method)=>typeof(SketchRunBinding).GetMethod(method,Hidden).Invoke(binding,null);
        static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); passed.Add(name); Debug.Log("Approved core PASS: " + name); }
        static T Get<T>(string name) => (T)typeof(TemporaryGameGui).GetField(name, Hidden).GetValue(gui);
        static void Set(string name, object value) => typeof(TemporaryGameGui).GetField(name, Hidden).SetValue(gui, value);
        static object Call(string name, params object[] args) => typeof(TemporaryGameGui).GetMethod(name, Hidden).Invoke(gui, args);
        static void SetBook(object book, string name, object value) { book.GetType().GetField(name).SetValue(book, value); book.GetType().GetField("Key").SetValue(book, null); }
        static Button Button(string name) => gui.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name);
        static void Click(string name) { var button = Button(name); Check(button && button.interactable, "Enabled action: " + name); button.onClick.Invoke(); gui.Refresh(); }
        static void DynamicInputUpdate()=>typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null).Invoke(null,new object[]{InputUpdateType.Dynamic});
        static void ReleaseKeys() { if (keyboard == null) return; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); DynamicInputUpdate(); if(gui)Set("liveDirection",0); }
        static void PressPad(GamepadButton button)
        {
            ReleaseKeys();var pad=InputSystem.AddDevice<Gamepad>("ApprovedCoreAcceptanceGamepad");
            try
            {
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));DynamicInputUpdate();
                Set("liveInputFrame",-1);Call("TickApprovedCoreInput");gui.Refresh();
                Call("TickApprovedCoreInput");gui.Refresh();
                Call("Update");
                var binding=world.GetComponent<SketchRunBinding>();
                if(binding&&binding.RewardModalShown)RewardCall(binding,"UpdateRewardInput");
                if(button==GamepadButton.East)Check(!(bool)Call("TakeBack"),"Gamepad cancel is not handled again by the parent menu");
                InputSystem.QueueStateEvent(pad,new GamepadState());DynamicInputUpdate();
            }
            finally{InputSystem.RemoveDevice(pad);}
        }
        static void PressDisplayPad(GamepadButton button)
        {
            ReleaseKeys();var pad=InputSystem.AddDevice<Gamepad>("ApprovedDisplayAcceptanceGamepad");
            try
            {
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));DynamicInputUpdate();
                Set("settingsInputFrame",-1);Call("SyncGuiInput");Call("SyncGuiInput");
                InputSystem.QueueStateEvent(pad,new GamepadState());DynamicInputUpdate();
            }
            finally{InputSystem.RemoveDevice(pad);}
        }
        static void PressDisplayKey(Key key)
        {
            ReleaseKeys();InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));DynamicInputUpdate();
            Set("settingsInputFrame",-1);Call("SyncGuiInput");Call("SyncGuiInput");ReleaseKeys();
        }
        static void Press(Key key)
        {
            ReleaseKeys(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); DynamicInputUpdate();
            Debug.Log("Acceptance key "+key+": held="+keyboard[key].isPressed+", pressed="+keyboard[key].wasPressedThisFrame+", current="+(Keyboard.current==keyboard)+", tab="+Get<int>("liveTab")+", stage="+Get<string>("liveStage")+", pick="+Get<int>("livePick")+", action="+Get<int>("liveAction"));
            Set("liveInputFrame", -1); Call("TickApprovedCoreInput"); gui.Refresh();
            Call("TickApprovedCoreInput"); gui.Refresh();
            if(key==Key.Escape)Check(!(bool)Call("TakeBack"),"Core Esc is not handled a second time by the parent menu");
            ReleaseKeys();
        }
        static void Capture(string name, int width, int height)
        {
            var canvas = gui.GetComponent<Canvas>(); var camera = world.View;
            if (!camera) throw new Exception("Scene has no presentation camera");
            var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
            var target = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                gui.FitToWorld(); foreach (var label in gui.GetComponentsInChildren<Text>()) label.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                foreach(var scroll in gui.GetComponentsInChildren<ScrollRect>())scroll.SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases();
                if(name=="bag-1280")
                {
                    var panel=(RectTransform)Get<GameObject>("bag").transform;
                    var corners=new Vector3[4];panel.GetWorldCorners(corners);
                    var lower=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
                    var upper=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                    Check(Mathf.Abs(upper.x-lower.x-width)<2&&Mathf.Abs(upper.y-lower.y-height)<2,
                        "Menus fit the 1280x720 viewport instead of snapping to the 960x540 world buffer");
                    Check(Mathf.Approximately(canvas.scaleFactor,1),"HUD retains its integer world scale at 1280x720");
                }
                camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory(Output); File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
                camera.targetTexture = target; camera.rect = rect; RenderTexture.active = active;
                gui.FitToWorld(); RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image);
            }
        }
        static void Finish(bool success)
        {
            driver?.Dispose(); driver = null;
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            SessionState.SetBool(Request, false);
            File.WriteAllText(Path.Combine(Output, "result.json"), "{\"passed\":" + (success ? "true" : "false") + ",\"assertions\":" + passed.Count + "}");
            File.WriteAllLines(Path.Combine(Output, "checks.txt"), passed);
            EditorApplication.ExitPlaymode();
            if (Application.isBatchMode || Environment.GetCommandLineArgs().Contains("-approved-ui-exit")) EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
