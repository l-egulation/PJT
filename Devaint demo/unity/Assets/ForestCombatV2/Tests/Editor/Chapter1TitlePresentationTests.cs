using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using Deviant.Input.Unity;
using Deviant.Input.Contracts;
using Object=UnityEngine.Object;
namespace ForestCombatV2.Tests
{
    public sealed class Chapter1TitlePresentationTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour o,string n)=>(T)o.GetType().GetField(n,Hidden).GetValue(o);
        static T Prop<T>(MonoBehaviour o,string n)=>(T)o.GetType().GetProperty(n).GetValue(o);
        static void Call(MonoBehaviour o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Public|Hidden).Invoke(o,args);
        static MonoBehaviour Gui()=>Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="TemporaryGameGui"&&b.GetType().GetField("oldTitle",Hidden).GetValue(b)!=null);
        static MonoBehaviour Well(MonoBehaviour gui)=>Field<MonoBehaviour>(gui,"wellTitle");
        static void Prepare(){EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);}
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();Chapter1ProfileHost.UseStore(null);}
        [UnityTest] public IEnumerator TitleSpritesRenderAndSceneryCoversWideAndTallWindows()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var gui=Gui();var well=Well(gui);Assert.IsFalse(Field<GameObject>(gui,"menu").activeSelf);
            CollectionAssert.AreEqual(new[]{"시작하기","설정","종료"},Prop<Text[]>(well,"MenuLabels").Select(l=>l.text));
            foreach(var label in Prop<Text[]>(well,"MenuLabels"))Assert.AreSame(Resources.Load<Font>("ApprovedTheme/Fonts/MemomentKkukkukk"),label.font);
            Assert.AreEqual("marker",Prop<Image>(well,"SelectionInk").sprite.name);
            Assert.IsNotNull(Prop<Image>(well,"Logo").sprite);Assert.IsFalse(Prop<bool>(well,"EraseVisible"));
            Assert.That(Prop<float>(well,"SketchProgress"),Is.EqualTo(.5f));
            var scene=Field<MonoBehaviour>(well,"scene");
            Assert.That(Prop<RawImage>(scene,"Hero").uvRect.width,Is.EqualTo(1),"human faces right");
            Assert.That(Prop<RawImage>(scene,"InkHero").uvRect.width,Is.EqualTo(-1),"ink faces left");
            Assert.That(Prop<Image>(well,"Logo").rectTransform.anchoredPosition.x+Prop<Image>(well,"Logo").rectTransform.rect.width/2,Is.EqualTo(800));
            foreach(var button in Prop<Button[]>(well,"MenuButtons"))Assert.That(button.GetComponent<RectTransform>().anchoredPosition.x+button.GetComponent<RectTransform>().rect.width/2,Is.EqualTo(800));
            foreach(var size in new[]{new Vector2Int(960,540),new Vector2Int(1280,720),new Vector2Int(2340,1080),new Vector2Int(3440,1440),new Vector2Int(800,1000)})Capture(gui,well,size.x,size.y,"calm",true);
        }
        [UnityTest] public IEnumerator NativeOverlayRendersHumanOnLeftAndInkOnRight()
        {
            Prepare();
            var helper=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Deviant.Hub.Editor.ApprovedVillageEditor")).First(t=>t!=null);
            helper.GetMethod("UseFullHdGameView").Invoke(null,null);
            yield return new EnterPlayMode();yield return null;yield return null;
            var gui=Gui();var well=Well(gui);var scene=Field<MonoBehaviour>(well,"scene");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay,gui.GetComponent<Canvas>().renderMode,"keep the actual Play rendering mode");
            well.enabled=false;Canvas.ForceUpdateCanvases();
            for(int i=0;i<4;i++)yield return null;
            string output=Path.GetFullPath("../outputs/title-overlay-20261116");Directory.CreateDirectory(output);
            string capture=Path.Combine(output,"overlay-"+System.DateTime.UtcNow.Ticks+".png");
            ScreenCapture.CaptureScreenshot(capture);
            float deadline=Time.realtimeSinceStartup+8;
            while(!File.Exists(capture)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(File.Exists(capture),"capture the real Game view including Overlay UI");
            var screenshot=new Texture2D(2,2,TextureFormat.RGBA32,false);
            var human=new Texture2D(2,2,TextureFormat.RGBA32,false);var ink=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.IsTrue(screenshot.LoadImage(File.ReadAllBytes(capture)));
                Assert.That(screenshot.width,Is.GreaterThanOrEqualTo(800));
                string art=Path.Combine(Application.dataPath,"Deviant/Game/Resources/TitleSketch/hero");
                human.LoadImage(File.ReadAllBytes(Path.Combine(art,"human/F01.png")));
                ink.LoadImage(File.ReadAllBytes(Path.Combine(art,"ink/F01.png")));
                var left=OverlayFaceErrors(screenshot,Prop<RawImage>(scene,"Hero"),human,ink);
                var right=OverlayFaceErrors(screenshot,Prop<RawImage>(scene,"InkHero"),human,ink);
                Debug.Log($"TITLE_OVERLAY image={capture} size={screenshot.width}x{screenshot.height} left human={left.human:F1} ink={left.ink:F1}; right human={right.human:F1} ink={right.ink:F1}");
                Assert.That(left.samples,Is.GreaterThan(30));Assert.That(right.samples,Is.GreaterThan(30));
                Assert.That(left.human+25,Is.LessThan(left.ink),"the real Overlay left face must be human");
                Assert.That(right.ink+25,Is.LessThan(right.human),"the real Overlay right face must be ink, not a second human");
                var skyLeft=screenshot.GetPixel(screenshot.width/10,screenshot.height-30);
                var skyRight=screenshot.GetPixel(screenshot.width*9/10,screenshot.height-30);
                Assert.That(skyRight.r-skyLeft.r,Is.GreaterThan(.04f),"the Overlay right world has paper sky");
                Assert.That(skyLeft.b-skyRight.b,Is.GreaterThan(.02f),"the Overlay left world keeps its blue sky");
            }
            finally {Object.DestroyImmediate(screenshot);Object.DestroyImmediate(human);Object.DestroyImmediate(ink);}
        }
        static (float human,float ink,int samples) OverlayFaceErrors(Texture2D screen,RawImage image,Texture2D human,Texture2D ink)
        {
            var a=human.GetPixels32();var b=ink.GetPixels32();var pixels=screen.GetPixels32();
            float ha=0,ib=0;int count=0;var rect=image.rectTransform.rect;
            for(int y=1;y<human.height-1;y++)for(int x=1;x<human.width-1;x++)
            {
                int p=y*human.width+x;
                if(a[p].a<250||b[p].a<250||a[p].r<=b[p].r+70||ColourDistance(a[p],b[p])<220)continue;
                float u=((x+.5f)/human.width-image.uvRect.x)/image.uvRect.width;
                float v=((y+.5f)/human.height-image.uvRect.y)/image.uvRect.height;
                var point=RectTransformUtility.WorldToScreenPoint(null,image.rectTransform.TransformPoint(new Vector3(rect.xMin+u*rect.width,rect.yMin+v*rect.height)));
                int px=Mathf.Clamp(Mathf.FloorToInt(point.x),0,screen.width-1),py=Mathf.Clamp(Mathf.FloorToInt(point.y),0,screen.height-1);
                var actual=pixels[py*screen.width+px];ha+=ColourDistance(actual,a[p]);ib+=ColourDistance(actual,b[p]);count++;
            }
            return(ha/Mathf.Max(1,count),ib/Mathf.Max(1,count),count);
        }
        static int ColourDistance(Color32 a,Color32 b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
        [UnityTest] public IEnumerator SharedInkFollowsPointerAndKeyboardAndSettingsReturnsFocus()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var gui=Gui();var well=Well(gui);var buttons=Prop<Button[]>(well,"MenuButtons");var ink=Prop<Image>(well,"SelectionInk");var labels=Prop<Text[]>(well,"MenuLabels");
            yield return null;yield return null;
            Assert.IsTrue(EventSystem.current.sendNavigationEvents);Assert.AreSame(buttons[0].gameObject,EventSystem.current.currentSelectedGameObject);
            ExecuteEvents.Execute(buttons[1].gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            Assert.AreEqual(1,Prop<int>(well,"SelectedIndex"));Assert.AreEqual(-430,ink.rectTransform.anchoredPosition.y);
            Assert.AreEqual(labels[0].color,labels[1].color,"marker selection keeps every label in dark main-font ink");Capture(gui,well,1280,720,"settings-selected",true);
            ExecuteEvents.Execute(buttons[1].gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Down},ExecuteEvents.moveHandler);
            Assert.AreEqual(2,Prop<int>(well,"SelectedIndex"));Assert.AreEqual(-500,ink.rectTransform.anchoredPosition.y);
            buttons[1].onClick.Invoke();Assert.IsTrue(Prop<bool>(well,"SettingsOpen"));Assert.IsFalse(Prop<bool>(well,"Transitioning"));Assert.IsTrue(buttons.All(b=>!b.interactable));
            yield return null;yield return null;
            var submitModule=EventSystem.current.GetComponent<InputSystemUIInputModule>();
            Assert.IsTrue(submitModule.submit.action.bindings.Any(b=>b.effectivePath=="<Keyboard>/f"),"opening settings switches from native title submit to F");
            var shared=Field<GameObject>(gui,"approvedSettings");
            Assert.IsTrue(shared.activeInHierarchy);Assert.AreSame(shared,Field<GameObject>(well,"settingsPanel"));
            Assert.IsFalse(Prop<Image>(well,"Logo").gameObject.activeInHierarchy,"title menu art stays hidden behind settings, like the in-game pause menu");
            Assert.AreSame(Field<RectTransform>(gui,"approvedMenuStage"),shared.transform.parent,"title settings uses the same full-screen viewport as the game menus");
            CollectionAssert.AreEqual(new[]{"화면","소리","조작"},Field<Button[]>(gui,"settingsTabs").Select(b=>b.GetComponentInChildren<Text>().text));
            Assert.IsFalse(gui.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith("타이틀 효과음")));
            Capture(gui,well,1280,720,"settings",false);Capture(gui,well,1920,1080,"settings",false);
            Field<Button[]>(gui,"settingsTabs")[2].onClick.Invoke();
            yield return null;
            var shortcuts=Field<GameObject>(gui,"shortcutPanel");Assert.IsTrue(shortcuts.activeInHierarchy);
            Assert.IsTrue(Field<bool>(gui,"shortcutsInSettings"));
            Assert.AreSame(Field<RectTransform>(gui,"settingsBody"),shortcuts.transform.parent,"key editing is now inline in main's shared settings");
            Assert.IsTrue(shortcuts.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("Binding ")));
            Capture(gui,well,1280,720,"shortcuts",false);
            Call(well,"CloseSettings");Assert.IsFalse(Prop<bool>(well,"SettingsOpen"));Assert.IsTrue(buttons.All(b=>b.interactable));Assert.AreSame(buttons[1].gameObject,EventSystem.current.currentSelectedGameObject);
            yield return null;yield return null;Assert.AreSame(buttons[1].gameObject,EventSystem.current.currentSelectedGameObject);
            Assert.AreSame(gui.GetComponent<LocalInput>().Source.GetAction(GameAction.Submit),submitModule.submit.action,"closing settings restores native title submit");
            buttons[1].onClick.Invoke();yield return null;
            Call(gui,"Back");
            yield return null;Assert.IsFalse(Prop<bool>(well,"SettingsOpen"));Assert.AreSame(buttons[1].gameObject,EventSystem.current.currentSelectedGameObject);
        }
        [UnityTest] public IEnumerator NativeKeyboardNavigatesAndOpensSettings()
        {
            Prepare();yield return new EnterPlayMode();yield return null;yield return null;
            var well=Well(Gui());var buttons=Prop<Button[]>(well,"MenuButtons");
            var background=InputSystem.settings.backgroundBehavior;var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard=null;var input=Gui().GetComponent<LocalInput>();
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                // Configure focus before creating the synthetic device, as the native UI fixtures do.
                keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
                input.Source.SetDevices(keyboard);input.Source.SetFocused(true);
                InputSystem.EnableDevice(keyboard);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.DownArrow));InputSystem.Update();
                var navigate=input.Source.GetAction(GameAction.Navigate);var module=EventSystem.current.GetComponent<InputSystemUIInputModule>();
                Assert.That(input.Read(GameAction.Navigate).Y,Is.LessThan(-.5f),$"Native keyboard: key={keyboard.downArrowKey.isPressed}, deviceEnabled={keyboard.enabled}, sourceFocus={input.Source.IsFocused}, contexts={input.Source.Contexts}, actionEnabled={navigate.enabled}, controls={navigate.controls.Count}, value={navigate.ReadValue<Vector2>()}, sameAction={module.move.action==navigate}");
                EventSystem.current.GetComponent<InputSystemUIInputModule>().Process();
                Assert.AreEqual(1,Prop<int>(well,"SelectedIndex"));Assert.AreSame(buttons[1].gameObject,EventSystem.current.currentSelectedGameObject);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));InputSystem.Update();
                EventSystem.current.GetComponent<InputSystemUIInputModule>().Process();yield return null;yield return null;
                Assert.IsTrue(Prop<bool>(well,"SettingsOpen"));
                Call(well,"CloseSettings");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Tab));InputSystem.Update();well.SendMessage("Update");
                Assert.AreEqual(2,Prop<int>(well,"SelectedIndex"),"Tab navigates the title without starting it");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift,Key.Tab));InputSystem.Update();well.SendMessage("Update");
                Assert.AreEqual(1,Prop<int>(well,"SelectedIndex"),"Shift+Tab returns to the previous row");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));InputSystem.Update();well.SendMessage("Update");
                Assert.IsTrue(Prop<bool>(well,"SettingsOpen"),"Space activates the focused menu item");
            }
            finally
            {
                InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
                if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            }
        }
        [UnityTest] public IEnumerator NativeTitleKeepsGifTimingAndFreezesOnlyScenery()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var scene=Field<MonoBehaviour>(Well(Gui()),"scene");var type=scene.GetType();
            Assert.AreEqual(0,type.GetMethod("FrameAt").Invoke(null,new object[]{.079f}));
            Assert.AreEqual(1,type.GetMethod("FrameAt").Invoke(null,new object[]{.081f}));
            Assert.AreEqual(3,type.GetMethod("FrameAt").Invoke(null,new object[]{.241f}));
            Assert.AreEqual(0,type.GetMethod("FrameAt").Invoke(null,new object[]{3.6f}));
            float before=Prop<float>(scene,"Clock");for(int i=0;i<10;i++)Call(scene,"Tick",.1f,.5f);
            Assert.That(Prop<float>(scene,"Clock")-before,Is.EqualTo(1).Within(.001f),"a slow frame cannot slow down the approved GIF clock");
            var leaf=scene.GetComponentsInChildren<RawImage>().Where(r=>r.name=="Title leaf and ash").ToArray();
            var positions=leaf.Select(r=>r.rectTransform.anchoredPosition).ToArray();Call(scene,"Tick",0f,.5f);
            CollectionAssert.AreEqual(positions,leaf.Select(r=>r.rectTransform.anchoredPosition).ToArray());
            var clouds=scene.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("Title cloud ")).ToArray();
            var rightClouds=clouds.Where(r=>r.anchoredPosition.x>950).ToArray();Assert.That(rightClouds.Length,Is.GreaterThan(0));
            var leftClouds=clouds.Where(r=>r.anchoredPosition.x<500).ToArray();Assert.That(leftClouds.Length,Is.GreaterThan(0));
            var flowers=scene.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="Title daisy"&&r.anchoredPosition.x>950).ToArray();
            var tips=scene.GetComponentsInChildren<RawImage>().Where(r=>r.name=="Grass tips"&&((RectTransform)r.transform.parent).anchoredPosition.x>950).ToArray();
            var rightPositions=rightClouds.Select(r=>r.anchoredPosition).ToArray();var leftPositions=leftClouds.Select(r=>r.anchoredPosition).ToArray();
            var rotations=flowers.Select(r=>r.localRotation).ToArray();var bends=tips.Select(r=>(float)r.GetType().GetProperty("Sway").GetValue(r)).ToArray();int hero=Prop<int>(scene,"HeroFrame");
            Call(scene,"Tick",.3f,.5f);
            CollectionAssert.AreEqual(rightPositions,rightClouds.Select(r=>r.anchoredPosition).ToArray());
            Assert.IsFalse(leftPositions.SequenceEqual(leftClouds.Select(r=>r.anchoredPosition)),"the bright side keeps living");
            CollectionAssert.AreEqual(rotations,flowers.Select(r=>r.localRotation).ToArray());
            CollectionAssert.AreEqual(bends,tips.Select(r=>(float)r.GetType().GetProperty("Sway").GetValue(r)).ToArray());
            Assert.AreNotEqual(hero,Prop<int>(scene,"HeroFrame"));
            Assert.AreSame(Field<Texture2D[]>(scene,"human")[Prop<int>(scene,"HeroFrame")],Prop<RawImage>(scene,"Hero").texture);
            Assert.AreSame(Field<Texture2D[]>(scene,"ink")[Prop<int>(scene,"HeroFrame")],Prop<RawImage>(scene,"InkHero").texture,"right idle frames bind the actual ink original");
            Assert.IsFalse(positions.SequenceEqual(leaf.Select(r=>r.rectTransform.anchoredPosition)),"ash still drifts in the sketched world");
            Call(scene,"ResetScene");Assert.That(Prop<float>(scene,"Progress"),Is.EqualTo(.5f));Assert.That(Prop<int>(scene,"AshAlive"),Is.EqualTo(104));
        }
        [UnityTest] public IEnumerator NativeGamepadNavigatesAndOpensSettings()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var well=Well(Gui());var input=Gui().GetComponent<LocalInput>();Gamepad pad=null;
            var background=InputSystem.settings.backgroundBehavior;var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();InputSystem.EnableDevice(pad);input.Source.SetDevices(pad);input.Source.SetFocused(true);
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));InputSystem.Update();
                var module=EventSystem.current.GetComponent<InputSystemUIInputModule>();module.Process();
                Assert.AreEqual(1,Prop<int>(well,"SelectedIndex"));
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();module.Process();
                yield return null;Assert.IsTrue(Prop<bool>(well,"SettingsOpen"));
            }
            finally
            {
                InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
                if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            }
        }
        [UnityTest] public IEnumerator SplitCompositionStaysFixedAndOnlyFadesBeforeStartingVillage()
        {
            Prepare();yield return new EnterPlayMode();Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());yield return null;
            var gui=Gui();var well=Well(gui);var scene=Field<MonoBehaviour>(well,"scene");
            Assert.AreEqual(48,Field<Texture2D[]>(scene,"human").Length);Assert.AreEqual(48,Field<Texture2D[]>(scene,"ink").Length);
            Call(well,"RenderTransition",0f);Assert.That(Prop<float>(well,"FadeAmount"),Is.Zero);
            var half=Capture(gui,well,1280,720,"split",false);
            var left=half[(720-40)*1280+40];var right=half[(720-40)*1280+1240];
            Assert.That(right.r,Is.GreaterThan(left.r+8),"right sky is paper while left sky stays bright");
            Assert.That(right.b,Is.LessThan(left.b));
            foreach(float elapsed in new[]{0f,.09f,.18f,.6f,1.2f}){
                Call(well,"RenderTransition",elapsed);
                Assert.That(Prop<float>(well,"SketchProgress"),Is.EqualTo(.5f),"starting never moves the boundary");
            }
            Call(well,"RenderTransition",.6f);Assert.That(Prop<float>(well,"FadeAmount"),Is.EqualTo(.5f).Within(.005f));
            Capture(gui,well,1280,720,"split-fade",false);
            Assert.That(Prop<RawImage>(scene,"InkHero").material.GetFloat("_Outline"),Is.EqualTo(1));
            Call(well,"RenderTransition",1.2f);var black=Capture(gui,well,1280,720,"black",false);
            Assert.That(black.Count(p=>p.r+p.g+p.b<30),Is.GreaterThan(black.Length*.99f));Call(well,"CancelTransition");
            Time.timeScale=0;var start=Prop<Button[]>(well,"MenuButtons")[0];start.onClick.Invoke();start.onClick.Invoke();
            Assert.IsTrue(Prop<bool>(well,"Transitioning"));Assert.That(Prop<float>(well,"SketchProgress"),Is.EqualTo(.5f));
            Assert.IsTrue(Prop<Button[]>(well,"MenuButtons").All(b=>!b.interactable));
            Assert.AreEqual(Chapter1RunHost.TitleScene,SceneManager.GetActiveScene().path);
            float deadline=Time.realtimeSinceStartup+12;
            while(SceneManager.GetActiveScene().path!=Chapter1RunHost.VillageScene&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(Chapter1RunHost.VillageScene,SceneManager.GetActiveScene().path);Time.timeScale=1;
        }
        static int startCalls;static float committedFade;static float committedSplit;static int committedFrameGap;static MonoBehaviour callbackTitle;
        static void CountStart(){startCalls++;committedFade=Prop<float>(callbackTitle,"FadeAmount");committedSplit=Prop<float>(callbackTitle,"SketchProgress");committedFrameGap=Time.frameCount-Prop<int>(callbackTitle,"BlackFrame");}
        [UnityTest] public IEnumerator StartCommitsOnceAfterBlackAndPendingCancelsItsCallback()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var well=Well(Gui());callbackTitle=well;startCalls=0;
            well.GetType().GetField("startAction",Hidden).SetValue(well,new System.Action(CountStart));
            Call(well,"BeginStart");Call(well,"BeginStart");
            yield return new WaitForSecondsRealtime(1.8f);yield return null;
            Assert.AreEqual(1,startCalls);Assert.That(committedFrameGap,Is.GreaterThanOrEqualTo(1),"one black frame precedes the start callback");Assert.That(committedSplit,Is.EqualTo(.5f));Assert.That(committedFade,Is.GreaterThan(.99f));
            Call(well,"BeginStart");yield return null;Assert.AreEqual(1,startCalls);
            Call(well,"CancelTransition");Call(well,"BeginStart");Call(well,"SetPending",true);
            yield return new WaitForSecondsRealtime(1.8f);Assert.AreEqual(1,startCalls,"pending navigation cancels an unfinished start");
            Call(well,"SetPending",false);Assert.That(Prop<float>(well,"SketchProgress"),Is.EqualTo(.5f));Assert.That(Prop<float>(well,"FadeAmount"),Is.Zero);
            Assert.IsTrue(Prop<Button[]>(well,"MenuButtons").All(b=>b.interactable));callbackTitle=null;
        }
        [UnityTest] public IEnumerator SharedTitleSoundRespectsTheVisibleSoundToggleAndMasterVolume([Values(0f,1f)] float master)
        {
            const string legacy="deviant.title.glitch-sound",volume="deviant.audio.master";
            bool hadLegacy=PlayerPrefs.HasKey(legacy),hadVolume=PlayerPrefs.HasKey(volume);
            int oldLegacy=PlayerPrefs.GetInt(legacy,1);float oldVolume=PlayerPrefs.GetFloat(volume,1);
            PlayerPrefs.SetInt(legacy,0);PlayerPrefs.SetFloat(volume,master);PlayerPrefs.Save();
            try
            {
                Prepare();yield return new EnterPlayMode();Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());yield return null;
                var gui=Gui();var well=Well(gui);var buttons=Prop<Button[]>(well,"MenuButtons");
                Assert.IsFalse(Prop<bool>(well,"SoundEnabled"),"the shared sound page retains the saved title-effect toggle");
                Assert.That(AudioListener.volume,Is.EqualTo(master).Within(.001f));
                Assert.IsFalse(well.GetComponentsInChildren<AudioSource>().Any(a=>a.clip&&a.clip.name.Contains("glitch")),"the sketch transition has no impact/glitch audio");
                int confirms=PresentationSfx.CountOf("sfx_ui_confirm");buttons[0].onClick.Invoke();
                Assert.IsTrue(Prop<bool>(well,"Transitioning"));
                float deadline=Time.realtimeSinceStartup+12;
                while(SceneManager.GetActiveScene().path!=Chapter1RunHost.VillageScene&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.AreEqual(Chapter1RunHost.VillageScene,SceneManager.GetActiveScene().path);
                Assert.AreEqual(confirms,PresentationSfx.CountOf("sfx_ui_confirm"),"the title-effect toggle also mutes the title start confirmation");
            }
            finally
            {
                if(hadLegacy)PlayerPrefs.SetInt(legacy,oldLegacy);else PlayerPrefs.DeleteKey(legacy);
                if(hadVolume)PlayerPrefs.SetFloat(volume,oldVolume);else PlayerPrefs.DeleteKey(volume);
                PlayerPrefs.Save();AudioListener.volume=oldVolume;
            }
        }
        [UnityTest] public IEnumerator ExistingStartStampPlaysImmediatelyOnceAndNotAfterTheFade()
        {
            const string preference="deviant.title.glitch-sound";
            bool had=PlayerPrefs.HasKey(preference);int old=PlayerPrefs.GetInt(preference,1);
            PlayerPrefs.SetInt(preference,1);PlayerPrefs.Save();
            try
            {
                Prepare();yield return new EnterPlayMode();Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());yield return null;
                var well=Well(Gui());var buttons=Prop<Button[]>(well,"MenuButtons");
                int before=PresentationSfx.CountOf("sfx_ui_confirm"),clicks=PresentationSfx.CountOf("sfx_ui_resume");
                buttons[0].onClick.Invoke();
                Assert.AreEqual(before+1,PresentationSfx.CountOf("sfx_ui_confirm"),"the original stamp plays synchronously on button acceptance");
                buttons[0].onClick.Invoke();Call(well,"BeginStart");
                Assert.AreEqual(before+1,PresentationSfx.CountOf("sfx_ui_confirm"),"repeated input cannot replay the stamp");
                float deadline=Time.realtimeSinceStartup+12;
                while(SceneManager.GetActiveScene().path!=Chapter1RunHost.VillageScene&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.AreEqual(Chapter1RunHost.VillageScene,SceneManager.GetActiveScene().path);
                Assert.AreEqual(before+1,PresentationSfx.CountOf("sfx_ui_confirm"),"scene handoff adds no second stamp");
                Assert.AreEqual(clicks,PresentationSfx.CountOf("sfx_ui_resume"),"the proposed replacement click is not used");
            }
            finally {if(had)PlayerPrefs.SetInt(preference,old);else PlayerPrefs.DeleteKey(preference);PlayerPrefs.Save();}
        }
        [UnityTest] public IEnumerator PendingChapterMenuReturnsToTheNewTitle()
        {
            Prepare();yield return new EnterPlayMode();Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());yield return null;
            Chapter1RunHost.Ensure().GoToTitle(true);yield return null;yield return null;var gui=Gui();var primary=Field<Button>(gui,"menuPrimary");
            Assert.AreEqual("타이틀로",primary.GetComponentInChildren<Text>().text);Assert.IsFalse(Field<Button>(gui,"menuSecondary").gameObject.activeSelf);
            primary.onClick.Invoke();yield return null;Assert.IsFalse(Field<GameObject>(gui,"menu").activeSelf);
            Assert.IsTrue(Prop<Image>(Well(gui),"Logo").gameObject.activeInHierarchy);Assert.IsTrue(Prop<Button[]>(Well(gui),"MenuButtons").All(b=>b.interactable));
        }
        static Color32[] Capture(MonoBehaviour gui,MonoBehaviour well,int width,int height,string state,bool check)
        {
            var camera=Object.FindAnyObjectByType<Camera>();var canvas=gui.GetComponent<Canvas>();var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
            var oldTarget=camera.targetTexture;var oldRect=camera.rect;var oldActive=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);var labels=Prop<Text[]>(well,"MenuLabels");
            try
            {
                camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+1;Call(gui,"FitToWorld");
                foreach(var text in gui.GetComponentsInChildren<Text>()){text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);text.SetAllDirty();}
                Canvas.ForceUpdateCanvases();
                foreach(string property in new[]{"Hero","InkHero"}){
                    var hero=Prop<RawImage>(Field<MonoBehaviour>(well,"scene"),property).rectTransform;var corners=new Vector3[4];hero.GetWorldCorners(corners);
                    Assert.That(Vector3.Distance(corners[0],corners[3])/Vector3.Distance(corners[0],corners[1]),Is.EqualTo(1).Within(.002f),"neither character stretches with the window");
                }
                camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();var visible=texture.GetPixels32();
                string output=Path.GetFullPath("../outputs/title-sketch-tests");Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,$"title-{state}-{width}x{height}.png"),texture.EncodeToPNG());if(!check)return visible;
                int scenery=0,samples=0;for(int y=2;y<height-2;y+=17){samples+=2;if(Brightness(visible[y*width+1])>30)scenery++;if(Brightness(visible[y*width+width-2])>30)scenery++;}Assert.That(scenery,Is.GreaterThan(samples*.9f),"Scenery covers both window edges");
                var areas=labels.Select(l=>PixelRect(l.rectTransform,camera,width,height)).ToArray();foreach(var label in labels)label.enabled=false;
                Canvas.ForceUpdateCanvases();camera.Render();texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();var blank=texture.GetPixels32();
                for(int i=0;i<labels.Length;i++)
                {
                    int changed=0;var area=areas[i];Assert.That(area.width,Is.GreaterThan(10));Assert.That(area.height,Is.GreaterThan(8));
                    for(int y=area.yMin;y<area.yMax;y++)for(int x=area.xMin;x<area.xMax;x++){int p=y*width+x;var a=visible[p];var b=blank[p];if(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>30)changed++;}
                    Assert.That(changed,Is.GreaterThan(20),$"{labels[i].name} renders pixels at {width}x{height}");
                }
                return visible;
            }
            finally{foreach(var label in labels)label.enabled=true;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;camera.targetTexture=oldTarget;camera.rect=oldRect;RenderTexture.active=oldActive;Call(gui,"FitToWorld");RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(texture);}
        }
        static int Brightness(Color32 c)=>c.r+c.g+c.b;
        static RectInt PixelRect(RectTransform transform,Camera camera,int width,int height)
        {
            var corners=new Vector3[4];transform.GetWorldCorners(corners);var a=camera.WorldToViewportPoint(corners[0]);var b=camera.WorldToViewportPoint(corners[2]);Assert.That(a.x,Is.GreaterThanOrEqualTo(-.001f));Assert.That(a.y,Is.GreaterThanOrEqualTo(-.001f));Assert.That(b.x,Is.LessThanOrEqualTo(1.001f));Assert.That(b.y,Is.LessThanOrEqualTo(1.001f));
            int x=Mathf.Clamp(Mathf.FloorToInt(a.x*width),0,width),y=Mathf.Clamp(Mathf.FloorToInt(a.y*height),0,height);return new RectInt(x,y,Mathf.Clamp(Mathf.CeilToInt(b.x*width),0,width)-x,Mathf.Clamp(Mathf.CeilToInt(b.y*height),0,height)-y);
        }
    }
}
