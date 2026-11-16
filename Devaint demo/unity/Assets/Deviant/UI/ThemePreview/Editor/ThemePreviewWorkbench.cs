using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Deviant.ThemePreview
{
    [InitializeOnLoad]
    public static partial class ThemePreviewWorkbench
    {
        const string ScenePath="Assets/Deviant/UI/ThemePreview/ThemePreview.unity";
        const string Running="Deviant.ThemePreview.Active";
        static string Output=>Path.GetDirectoryName(ThemePreviewScreen.ConfigPath);
        static Keyboard keyboard;
        static Queue<Action> steps;
        static int nextFrame;
        static readonly List<string> passed=new List<string>();
        static ThemePreviewWorkbench(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingPlayMode){if(keyboard!=null)InputSystem.RemoveDevice(keyboard);keyboard=null;steps=null;SessionState.SetBool(Running,false);}};}
        [MenuItem("Deviant/UI Theme Preview/Configure local sources")]
        public static void Configure()
        {
            var package=EditorUtility.OpenFolderPanel("승인 unity-handoff-v1 폴더", "", "");if(string.IsNullOrEmpty(package))return;
            var font=EditorUtility.OpenFilePanel("로컬 메모먼트 폰트", "", "ttf,otf");if(string.IsNullOrEmpty(font))return;
            Directory.CreateDirectory(Output);File.WriteAllText(ThemePreviewScreen.ConfigPath,JsonUtility.ToJson(new PreviewConfig{packagePath=package,fontPath=font},true));
        }
        [MenuItem("Deviant/UI Theme Preview/Open independent scene")]
        public static void OpenConfigured()
        { ThemePreviewScreen.LaunchExtended=false; ThemePreviewScreen.LaunchCore=false; OpenScene(); }
        [MenuItem("Deviant/UI Theme Preview/Open bag crafting equipment")]
        public static void OpenCore()
        { ThemePreviewScreen.LaunchExtended=false; ThemePreviewScreen.LaunchCore=true; OpenScene(); }
        [MenuItem("Deviant/UI Theme Preview/Open remaining Figma screens")]
        public static void OpenRemaining(){ThemePreviewScreen.LaunchExtended=true;ThemePreviewScreen.LaunchCore=false;OpenScene();}
        [MenuItem("Deviant/UI Theme Preview/Simulate 10 seconds of gameplay")]
        public static void AdvancePreview()
        { if(Screen!=null&&Screen.CoreMode){Screen.Inventory.AdvanceGameplay(10);Screen.Render();} }
        [MenuItem("Deviant/UI Theme Preview/Simulate world progress")]
        public static void AdvanceWorldPreview(){if(Screen!=null&&Screen.ExtendedMode)Screen.HandleExtended("F");}
        static void OpenScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before opening the preview.");
            if(!File.Exists(ThemePreviewScreen.ConfigPath)) {Configure();if(!File.Exists(ThemePreviewScreen.ConfigPath))return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var config=JsonUtility.FromJson<PreviewConfig>(File.ReadAllText(ThemePreviewScreen.ConfigPath));config.coreScreens=ThemePreviewScreen.LaunchCore;config.extendedScreens=ThemePreviewScreen.LaunchExtended;File.WriteAllText(ThemePreviewScreen.ConfigPath,JsonUtility.ToJson(config,true));
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);
            else
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Independent UI Theme Preview");root.AddComponent<ThemePreviewScreen>();
                var camera=new GameObject("Preview Camera",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.20f);camera.cullingMask=0;
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            SetSize(1920,1080);SessionState.SetBool(Running,true);EditorApplication.isPlaying=true;
        }
        [MenuItem("Deviant/UI Theme Preview/Run keyboard and render checks")]
        public static void Verify(){Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"request.txt"),"verify");}
        static ThemePreviewScreen Screen=>UnityEngine.Object.FindFirstObjectByType<ThemePreviewScreen>();
        static void Expect(bool ok,string label){if(!ok)throw new InvalidOperationException(label);passed.Add(label);}
        static void Press(Key key)
        {
            steps.Enqueue(()=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(key)));
            steps.Enqueue(()=>InputSystem.QueueStateEvent(keyboard,new KeyboardState()));
        }
        static void BeginChecks()
        {
            File.Delete(Path.Combine(Output,"failure.txt"));File.Delete(Path.Combine(Output,"checks.json"));
            Screen.ResetFoundation();passed.Clear();steps=new Queue<Action>();keyboard=InputSystem.AddDevice<Keyboard>("ThemePreviewVerification");
            int frame=Screen.Frames; nextFrame=frame+8;
            steps.Enqueue(()=>Expect(Screen.Frames>frame,"Play frames advance"));
            steps.Enqueue(()=>Expect(Screen.Error==null,"No preview exception"));
            steps.Enqueue(()=>Expect(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1,"Exactly one EventSystem"));
            steps.Enqueue(()=>Expect(SceneManager.GetActiveScene().path==ScenePath,"Independent scene only"));
            steps.Enqueue(()=>Capture("frame-1920.png"));
            Press(Key.E);steps.Enqueue(()=>Expect(Screen.Focus.Tab==1&&Screen.Focus.Depth==0,"E switches tab"));
            Press(Key.F);steps.Enqueue(()=>Expect(Screen.Focus.Depth==1,"F enters slot selection"));
            Press(Key.RightArrow);steps.Enqueue(()=>Expect(Screen.Focus.Slot==1,"Arrow selects next slot"));
            Press(Key.F);steps.Enqueue(()=>Expect(Screen.Focus.Depth==2,"F enters ink selection"));
            Press(Key.RightArrow);steps.Enqueue(()=>Expect(Screen.Focus.Ink==1&&Screen.Focus.Slot==1,"Ink focus does not move slot"));
            Press(Key.F);steps.Enqueue(()=>Expect(Screen.Focus.Applied[1]==1&&Screen.Focus.Depth==1,"Confirm applies preview ink once"));
            steps.Enqueue(()=>Capture("palette-1920.png"));
            Press(Key.Escape);steps.Enqueue(()=>Expect(Screen.Focus.Depth==0&&Screen.Focus.Slot==1,"Esc restores parent selection"));
            Press(Key.Escape);steps.Enqueue(()=>Expect(Screen.Focus.Closed,"Root Esc closes preview"));
            Press(Key.F);steps.Enqueue(()=>Expect(!Screen.Focus.Closed,"F reopens closed preview"));
            Press(Key.E);steps.Enqueue(()=>Expect(Screen.Focus.Tab==2,"Typography tab reachable"));
            Press(Key.S);steps.Enqueue(()=>Expect(Screen.ScrollLine==1,"S scrolls text"));
            Press(Key.W);steps.Enqueue(()=>Expect(Screen.ScrollLine==0,"W scrolls back"));
            steps.Enqueue(()=>Capture("type-1920.png"));
            steps.Enqueue(()=>SetSize(960,540));steps.Enqueue(()=>Capture("type-960.png"));
            Press(Key.Q);steps.Enqueue(()=>Expect(Screen.Focus.Tab==1,"Q returns to palette"));Press(Key.F);
            steps.Enqueue(()=>Capture("palette-960.png"));Press(Key.Q);steps.Enqueue(()=>Capture("frame-960.png"));
            steps.Enqueue(()=>SetSize(1920,1080));
            steps.Enqueue(()=>{
                foreach(var t in UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
                {t.ForceMeshUpdate();Expect(!t.isTextOverflowing,"Text fits: "+t.name);}
                File.WriteAllText(Path.Combine(Output,"checks.json"),"{\"passed\":true,\"checks\":["+string.Join(",",passed.Select(x=>"\""+x.Replace("\"","'")+"\""))+"],\"frame\":"+Screen.Frames+"}");
            });
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Running,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            EditorApplication.QueuePlayerLoopUpdate();var screen=Screen;if(screen==null||screen.Frames<4)return;
            var request=Path.Combine(Output,"request.txt");
            try
            {
                if(File.Exists(request)&&steps==null){var cmd=File.ReadAllText(request).Trim();File.Delete(request);if(cmd=="verify")BeginChecks();else if(cmd=="verify-core")BeginCoreChecks();else if(cmd=="verify-extended")BeginExtendedChecks();}
                if(steps!=null&&screen.Frames>=nextFrame)
                {
                    if(steps.Count==0){steps=null;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);keyboard=null;return;}
                    steps.Dequeue()();nextFrame=screen.Frames+8;
                }
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(Output,"failure.txt"),ex.ToString());Debug.LogException(ex);steps=null;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);keyboard=null;}
        }
        static void Capture(string name)
        {
            foreach(var t in UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
            {t.ForceMeshUpdate();Expect(!t.isTextOverflowing,"Text fits " + name + ": " + t.name);}
            UnityEngine.ScreenCapture.CaptureScreenshot(Path.Combine(Output,name));
        }
        static void SetSize(int width,int height)
        {
            var asm=typeof(Editor).Assembly;var sizesType=asm.GetType("UnityEditor.GameViewSizes");
            var single=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var sizes=single.GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
            var sizeType=asm.GetType("UnityEditor.GameViewSize");var typeEnum=asm.GetType("UnityEditor.GameViewSizeType");
            var count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);int selected=-1;
            for(int i=0;i<count;i++){var s=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[]{i});if((int)sizeType.GetProperty("width").GetValue(s)==width&&(int)sizeType.GetProperty("height").GetValue(s)==height){selected=i;break;}}
            if(selected<0){var size=Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{Enum.ToObject(typeEnum,1),width,height,"Theme preview "+width+"×"+height},null);group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});selected=count;}
            var gameType=asm.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(gameType);gameType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,selected);view.Focus();view.Repaint();
        }
    }
}
