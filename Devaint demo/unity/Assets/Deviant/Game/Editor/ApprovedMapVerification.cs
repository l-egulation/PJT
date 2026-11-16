using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using Deviant.Run;
using Deviant.UI.Theme;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Deviant.Game.Editor
{
    // Runs production scenes, renderers and models. Only the persistent profile is replaced with memory storage.
    [InitializeOnLoad]
    public static class ApprovedMapVerification
    {
        const string Request="ApprovedMapVerification";
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static string Output=>Path.GetFullPath("../outputs/map-verification");
        static TemporaryGameGui gui;
        static ForestCombatWorld forest;
        static Chapter2World sewer;
        static InventoryRuntimeHost host;
        static Chapter1FullMap map;
        static CombatArena.ManualSimulation driver;
        static Keyboard keyboard;
        static Queue<Action> checks;
        static readonly List<string> passed=new List<string>();
        static int phase,frame=-1;
        static double quiet,deadline;
        static ApprovedMapVerification(){EditorApplication.update+=Tick;}
        [MenuItem("Tools/deviant/Approved UI/Verify live map and capture")]
        public static void Run()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new Exception("Unity 6000.6.5f1 required");
            Directory.CreateDirectory(Output);File.Delete(Path.Combine(Output,"failure.txt"));File.Delete(Path.Combine(Output,"result.json"));
            passed.Clear();SessionState.SetBool(Request,true);phase=0;quiet=deadline=0;
        }
        static void Tick()
        {
            if(File.Exists(Path.Combine(Output,"request.txt"))){File.Delete(Path.Combine(Output,"request.txt"));Run();}
            if(!SessionState.GetBool(Request,false))return;
            try
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(!EditorApplication.isPlaying)
                {
                    if(quiet==0){quiet=EditorApplication.timeSinceStartup+2;return;}
                    if(EditorApplication.timeSinceStartup<quiet)return;
                    EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();return;
                }
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+240;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Map verification timed out at phase "+phase);
                if(frame==Time.frameCount)return;frame=Time.frameCount;
                if(phase==0)
                {
                    Chapter1ProfileHost.UseStore(new MemoryProfileStore());
                    SceneManager.LoadScene(Chapter1MapSelector.ProceduralScene);phase=1;return;
                }
                if(phase==3){driver?.Dispose();driver=null;SceneManager.LoadScene("Assets/Chapter2/Scenes/Chapter2Procedural.unity");phase=4;return;}
                forest=Object.FindAnyObjectByType<ForestCombatWorld>();sewer=Object.FindAnyObjectByType<Chapter2World>();
                host=Object.FindAnyObjectByType<InventoryRuntimeHost>();gui=Object.FindAnyObjectByType<TemporaryGameGui>();
                if(!gui||!host||host.Presenter==null||!(forest&&forest.Ready||sewer&&sewer.Ready))return;
                map=forest?forest.FullMap:sewer.FullMap;
                if(phase==1||phase==4){Prepare();phase++;return;}
                if(checks.Count>0){Release();checks.Dequeue()();return;}
                if(phase==2){phase=3;return;}
                Finish(true);
            }
            catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"failure.txt"),error.ToString());Finish(false);}
        }
        static void Prepare()
        {
            driver=host.Arena.AcquireManualSimulation();
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
            foreach(var enemy in host.Arena.enemies)if(enemy){enemy.aggressive=false;enemy.CancelAttack();}
            host.Arena.Player.health=host.Arena.Player.maxHealth=100000;
            if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>("LiveMapAcceptance");
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard);
            var actions=PlayerControls.For(host.Arena.Player);actions.Source.SetDevices(keyboard);actions.Source.SetFocused(true);actions.Source.LoadBindingOverrides("{\"bindings\":[]}");
            checks=new Queue<Action>();string chapter=forest?"chapter1":"chapter2";
            checks.Enqueue(()=>
            {
                map.SetOpen(false);FreshMini();
                var mini=Get<object>("miniMap");var miniType=mini.GetType();
                var terrain=(RawImage)miniType.GetField("Terrain").GetValue(mini);
                var geometry=(ApprovedMapGeometry)miniType.GetField("Geometry").GetValue(mini);
                var clip=(RectTransform)miniType.GetField("Clip").GetValue(mini);
                Check(!terrain.gameObject.activeSelf&&geometry.PatchCount>0,"Combat minimap renders walkable contours instead of the solid-terrain texture");
                Check(Mathf.Abs(clip.sizeDelta.x/Get<Chapter1MapViewport>("miniViewport").Scale(clip.sizeDelta)-1536)<1,"Minimap shows a constant 48-unit local width in each chapter");
                Check(clip.parent.GetComponent<RectTransform>().sizeDelta.x==394&&Mathf.Approximately(clip.parent.localScale.x,.82f),"Existing minimap frame dimensions and scale are preserved");
                Check(((Image)miniType.GetField("PlayerGlyph").GetValue(mini)).sprite.name.EndsWith("player"),"Combat current-position marker uses the approved character icon");
                map.SetOpen(true);gui.Refresh();
                Check(Get<GameObject>("mapPanel").activeInHierarchy,"Actual "+chapter+" opens approved live map");
                Check(Get<ApprovedMapGeometry>("mapGeometry").PatchCount>0,"Actual "+chapter+" discovered walkable geometry renders");
                foreach(var id in new[]{"player","workshop-yellow","chest","seal","boss-crimson","portal-1","portal-2","unknown"})
                    Check(ApprovedTheme.Sprite("common/full-map-v1__assets__"+id),"Local approved asset: "+id);
                foreach(int id in new[]{0,1,2,3,9})Check(ApprovedTheme.Sprite("ui-assets-v1/world-"+id),"Local approved world atlas icon: "+id);
                Check(Get<ScrollRect>("mapLegend").content.rect.height>Get<ScrollRect>("mapLegend").viewport.rect.height,"Full legend scrolls inside its own viewport");
                Check(Get<GameObject>("mapPanel").GetComponentsInChildren<Image>(true).Where(i=>i.name.StartsWith("common/full-map")||i.name.StartsWith("ui-assets-v1/")).All(i=>i.sprite&&i.sprite.texture.width>1),"Approved map assets resolve to local textures");
                Capture(chapter+"-initial",1280,720);
                Check(Get<Text>("mapZoom").text=="100%","Opening fit displays 100 percent");
            });
            checks.Enqueue(()=>
            {
                var trigger=Clip().GetComponent<EventTrigger>();
                var pointer=new PointerEventData(EventSystem.current){scrollDelta=Vector2.up,position=new Vector2(Screen.width/2,Screen.height/2)};
                float before=map.Viewport.Zoom;trigger.triggers.First(e=>e.eventID==EventTriggerType.Scroll).callback.Invoke(pointer);gui.Refresh();
                Check(map.Viewport.Zoom>before,"Pointer wheel zooms the live map");
                var local=Clip().GetComponent<RectTransform>();var original=map.Viewport.Center;
                pointer.delta=new Vector2(80,0);trigger.triggers.First(e=>e.eventID==EventTriggerType.Drag).callback.Invoke(pointer);gui.Refresh();
                Check(map.Viewport.Center!=original,"Pointer drag pans the live map");
                Invoke("FocusMap");gui.Refresh();
                var location=map.Viewport.ToScreen(map.PlayerMapPoint,Clip().sizeDelta);
                Check(location.x>=0&&location.y>=0&&location.x<=595&&location.y<=340,"Focus keeps real player inside map viewport");
                Capture(chapter+"-zoom",1920,1080);
                var legend=Get<ScrollRect>("mapLegend");legend.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();Capture(chapter+"-legend",1280,720);
                legend.verticalNormalizedPosition=1;
            });
            checks.Enqueue(()=>
            {
                map.SetOpen(false);gui.Refresh();var player=host.Arena.Player;var stood=player.Position;var projected=map.WorldMapPoint(stood);
                foreach(var room in map.MapData.rooms)
                {
                    var target=new Vector2((room.x+room.w*.5f)*32,(room.y+room.h*.5f)*32);
                    player.Position=stood+new Vector2((target.x-projected.x)/32,-(target.y-projected.y)/32);map.SendMessage("RevealCurrentRoom");
                }
                player.Position=stood;map.SetOpen(true);gui.Refresh();
                Check(Get<ApprovedMapGeometry>("mapGeometry").DiscoveredCount==map.MapData.rooms.Length,"Both chapters reveal actual rooms without preview fixtures");
                Capture(chapter+"-explored",1920,1080);
            });
            checks.Enqueue(()=>
            {
                // Portal fixture sets only committed slot state, the same contract the production presenter reads.
                var hero=host.Arena.Player;
                var installed=(bool[])typeof(CombatPlayer).GetField("portalInstalled",Hidden).GetValue(hero);
                var positions=(Vector2[])typeof(CombatPlayer).GetField("portalPositions",Hidden).GetValue(hero);
                installed[0]=installed[1]=true;positions[0]=hero.Position+Vector2.left*3;positions[1]=hero.Position+Vector2.right*3;gui.Refresh();
                var one=Mark("Portal 1");var two=Mark("Portal 2");
                Check(one&&two,"Two committed portal endpoints appear independently");
                var oldOne=one.anchoredPosition;var oldTwo=two.anchoredPosition;positions[0]+=Vector2.left*2;gui.Refresh();
                Check(Mark("Portal 1").anchoredPosition!=oldOne&&Mark("Portal 2").anchoredPosition==oldTwo,"Moving portal 1 preserves portal 2");
                installed[0]=installed[1]=false;gui.Refresh();Check(!Mark("Portal 1")&&!Mark("Portal 2"),"Cleared portal endpoints leave no stale markers");
            });
            checks.Enqueue(()=>
            {
                map.SetOpen(false);gui.Refresh();
                var hero=host.Arena.Player;var stood=hero.Position;
                var enemy=host.Arena.enemies.First(e=>e&&e.isActiveAndEnabled&&!e.Dead);
                hero.Position=enemy.Position+Vector2.left*2;map.SendMessage("RevealCurrentRoom");Physics2D.SyncTransforms();
                if(forest)forest.RefreshCamera(0,true);else sewer.CameraStep(0,true);
                var installed=(bool[])typeof(CombatPlayer).GetField("portalInstalled",Hidden).GetValue(hero);
                var positions=(Vector2[])typeof(CombatPlayer).GetField("portalPositions",Hidden).GetValue(hero);
                installed[0]=installed[1]=true;positions[0]=hero.Position+Vector2.left*6;positions[1]=hero.Position+Vector2.right*6;
                float oldHealth=hero.health,oldMax=hero.maxHealth;hero.health=hero.maxHealth=100;FreshMini();
                var mini=Get<object>("miniMap");var clip=(RectTransform)mini.GetType().GetField("Clip").GetValue(mini);
                var viewport=Get<Chapter1MapViewport>("miniViewport");var marker=(RectTransform)mini.GetType().GetField("Player").GetValue(mini);
                var point=viewport.ToScreen(map.PlayerMapPoint,clip.sizeDelta);
                Check(Vector2.Distance(marker.anchoredPosition,new Vector2(point.x,-point.y))<2,"Combat player marker follows the actual map projection");
                var icons=(List<Image>)mini.GetType().GetField("Icons").GetValue(mini);
                Check(icons.Count(i=>i.gameObject.activeSelf&&(i.name=="1번 포탈"||i.name=="2번 포탈"))==2,"Combat minimap shows both numbered committed portals");
                Check(((List<Image>)mini.GetType().GetField("Marks").GetValue(mini)).Any(i=>i.gameObject.activeSelf),"Combat minimap retains live enemy dots");
                float scale=viewport.Scale(clip.sizeDelta);map.Viewport.ZoomAt(2,Clip().sizeDelta/2,Clip().sizeDelta);FreshMini();
                Check(Mathf.Approximately(scale,viewport.Scale(clip.sizeDelta)),"Full-map zoom does not change the combat minimap scale");
                Capture(chapter+"-combat-local-1280",1280,720);Capture(chapter+"-combat-local-1920",1920,1080);Capture(chapter+"-combat-local-960",960,540);
                installed[0]=installed[1]=false;hero.Position=stood;hero.health=oldHealth;hero.maxHealth=oldMax;FreshMini();
            });
            checks.Enqueue(()=>
            {
                map.SetOpen(false);gui.Refresh();
                var keys=forest?forest.GetComponent<Chapter1KeyChest>():sewer.Keys;
                Check(keys&&keys.Chest,"Actual chapter chest actor is available");
                keys.GrantKeyForVerification();host.Arena.Player.Position=keys.Chest.Position;
                Check(keys.TryOpenChest(),"Real key chest opens through domain interaction");
                keys.TakeChoiceForVerification();map.SetOpen(true);gui.Refresh();
                Check(Get<GameObject>("mapPanel").GetComponentsInChildren<RectTransform>(false).Any(t=>t.name=="Completed event check"),"Completed chest retains a faded icon with a check");
                Capture(chapter+"-event-complete",1920,1080);
            });
            checks.Enqueue(()=>
            {
                Invoke("FitMapExplored");gui.Refresh();
                var a=map.Viewport.ToScreen(map.ExploredBounds.min,Clip().sizeDelta);var b=map.Viewport.ToScreen(map.ExploredBounds.max,Clip().sizeDelta);
                Check(a.x>=-1&&a.y>=-1&&b.x<=596&&b.y<=341,"Home fits discovered bounds");
                Click("Close live map");Check(!map.IsOpen,"Close button returns map control lease");
                map.SetOpen(true);gui.Refresh();Capture(chapter+"-reopen",960,540);
                map.SetOpen(false);gui.Refresh();Capture(chapter+"-minimap",1280,720);
                Check(!Get<GameObject>("mapPanel").activeInHierarchy,"Closing map restores gameplay presentation");
            });
            checks.Enqueue(()=>{PressMap(Key.M);Check(map.IsOpen,"M opens the actual map through input coordinator");});
            checks.Enqueue(()=>
            {
                map.Viewport.ZoomAt(2,Clip().sizeDelta/2,Clip().sizeDelta);map.Viewport.Focus(map.Viewport.Size/2,Clip().sizeDelta);
                float x=map.Viewport.Center.x;PressMap(Key.RightArrow);Check(map.Viewport.Center.x>x,"Keyboard right arrow pans the real viewport");
            });
            checks.Enqueue(()=>
            {
                var legend=Get<ScrollRect>("mapLegend");legend.verticalNormalizedPosition=1;var centre=map.Viewport.Center;
                PressMap(Key.S);Check(legend.verticalNormalizedPosition<1&&map.Viewport.Center==centre,"S scrolls only the legend without panning the map");
            });
            checks.Enqueue(()=>{PressMap(Key.M);Check(!map.IsOpen,"M closes the map without opening pause");});
            checks.Enqueue(()=>{PressMap(Key.M);Check(map.IsOpen,"M reopens after releasing prior key");});
            checks.Enqueue(()=>{PressMap(Key.Escape);Check(!map.IsOpen&&!host.Presenter.Paused,"Esc returns to gameplay without opening pause");});
        }
        static void FreshMini(){typeof(TemporaryGameGui).GetField("nextMiniMarkers",Hidden).SetValue(gui,0d);gui.Refresh();}
        static void DynamicInputUpdate()=>typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null).Invoke(null,new object[]{InputUpdateType.Dynamic});
        static void PressMap(Key key){InputSystem.EnableDevice(keyboard);PlayerControls.For(host.Arena.Player).Source.SetFocused(true);Release();InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));DynamicInputUpdate();
            var input=PlayerControls.For(host.Arena.Player);input.Poll();
            var native=input.Source.GetAction(Deviant.Input.Contracts.GameAction.Map);
            Debug.Log("MAP NATIVE keyHeld="+keyboard[key].isPressed+" keyboardEnabled="+keyboard.enabled+" actionEnabled="+native.enabled+" controls="+native.controls.Count+" binding="+native.bindings[0].effectivePath+" mapRead="+input.Source.Read(Deviant.Input.Contracts.GameAction.Map).X+" currentKeyboard="+(Keyboard.current==keyboard));
            Debug.Log("MAP INPUT key="+key+" focused="+input.Source.IsFocused+" captured="+input.MenuCaptured+" events="+input.EventCount+" modal="+(forest?forest.GetComponent<SketchRunBinding>().RewardModalShown:sewer.GetComponent<SketchRunBinding>().RewardModalShown));
            gui.SendMessage("Update");gui.Refresh();Release();}
        static T Get<T>(string name)=>(T)typeof(TemporaryGameGui).GetField(name,Hidden).GetValue(gui);
        static void Invoke(string method)=>typeof(TemporaryGameGui).GetMethod(method,Hidden).Invoke(gui,null);
        static RectTransform Clip()
        {
            var full=Get<object>("fullMap");return (RectTransform)full.GetType().GetField("Clip").GetValue(full);
        }
        static RectTransform Mark(string name)=>Clip().GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.name==name&&t.gameObject.activeInHierarchy);
        static void Click(string name)=>gui.GetComponentsInChildren<Button>().First(b=>b.name==name).onClick.Invoke();
        static void Release(){if(keyboard!=null){InputSystem.QueueStateEvent(keyboard,new KeyboardState());DynamicInputUpdate();}}
        static void Check(bool value,string message){if(!value)throw new Exception(message);passed.Add(message);Debug.Log("MAP PASS: "+message);}
        static void Capture(string name,int width,int height)
        {
            var canvas=gui.GetComponent<Canvas>();var camera=forest?forest.View:sewer.sceneCamera;
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldDistance=canvas.planeDistance;
            var oldTarget=camera.targetTexture;var oldRect=camera.rect;var oldActive=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+1;
                gui.FitToWorld();foreach(var text in gui.GetComponentsInChildren<Text>())text.SetAllDirty();Canvas.ForceUpdateCanvases();
                foreach(var scroll in gui.GetComponentsInChildren<ScrollRect>())scroll.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
            }
            finally{canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;camera.targetTexture=oldTarget;camera.rect=oldRect;RenderTexture.active=oldActive;gui.FitToWorld();RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
        static void Finish(bool success)
        {
            driver?.Dispose();driver=null;if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}
            SessionState.SetBool(Request,false);File.WriteAllLines(Path.Combine(Output,"checks.txt"),passed);
            File.WriteAllText(Path.Combine(Output,"result.json"),"{\"passed\":"+(success?"true":"false")+",\"assertions\":"+passed.Count+"}");
            EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(success?0:1);
        }
    }
}
