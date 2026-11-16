using System;
using System.IO;
using System.Linq;
using Deviant.Definitions;
using Deviant.Game.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deviant.Presentation.Editor
{
    public static class HitFeedbackPlaytestBuilder
    {
        public static readonly string[] Scenes={
            "Assets/ForestCombatV2/Scenes/Chapter1Title.unity",
            "Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity"
        };
        [MenuItem("Tools/deviant/Presentation/Build Hit Feedback Linux Playtest")]
        public static void BuildLinuxPlaytest()=>Build(BuildTarget.StandaloneLinux64,"deviant-hit-feedback.x86_64");
        [MenuItem("Tools/deviant/Presentation/Build Hit Feedback Windows Playtest")]
        public static void BuildWindowsPlaytest()=>Build(BuildTarget.StandaloneWindows64,"deviant-hit-feedback.exe");
        static void Build(BuildTarget target,string filename)
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("This playtest requires Unity 6000.6.5f1.");
            GameBuilder.ValidateAssets();
            foreach(var scene in Scenes)if(!File.Exists(scene))throw new FileNotFoundException("Playtest scene missing",scene);
            var time=Resources.Load<TextAsset>("HitFeedback/HitFeedbackTime");
            if(!time)throw new InvalidOperationException("Export canonical feedback settings before building.");
            JsonUtility.FromJson<HitFeedbackTimeData>(time.text).Validate();
            HitFeedbackAssetBuilder.Build();
            var arguments=Environment.GetCommandLineArgs();int index=Array.IndexOf(arguments,"-hitFeedbackOutput");
            string output=Path.GetFullPath(index>=0&&index+1<arguments.Length?arguments[index+1]:"Builds/HitFeedback/"+target);
            Directory.CreateDirectory(output);
            var oldScenes=EditorBuildSettings.scenes;bool oldResizable=PlayerSettings.resizableWindow;var oldMode=PlayerSettings.fullScreenMode;
            int oldWidth=PlayerSettings.defaultScreenWidth,oldHeight=PlayerSettings.defaultScreenHeight;
            try
            {
                EditorBuildSettings.scenes=Scenes.Select(path=>new EditorBuildSettingsScene(path,true)).ToArray();
                PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
                PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=Scenes,locationPathName=Path.Combine(output,filename),target=target,options=BuildOptions.Development});
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Hit feedback playtest build failed: "+report.summary.result);
                File.WriteAllText(Path.Combine(output,"PLAY.txt"),
                    "deviant hit-feedback playtest / Unity "+Application.unityVersion+"\n"+
                    "Start: chapter 1 title -> current procedural stage with both feedback layers.\n"+
                    "F1: independently toggle hit VFX, white flash, corpse debris, camera kick, distortion and player-death slow.\n"+
                    "F6/F7/F8: original combat-feel comparison, strength and optional hit-stop.\n\n"+
                    "Left/Right arrows: move; Space: jump; Shift: dash; A/S: basic attacks; Q/W: skills; X: swap weapon order.\n"+
                    "F1: equipment, enemy AI, camera/distortion intensity. F2: hitboxes. F4: monster placement.\n"+
                    "R: reset combat and health. M: map. Player death triggers the short slow-motion accent; monster deaths do not.\n"+
                    "All accepted player attack hits request world distortion; misses do not. F1 adjusts camera/distortion intensity.\n"+
                    "Keep the executable, *_Data directory and Unity player support files together.\n");
                Debug.Log("HIT_FEEDBACK_PLAYTEST_BUILD_OK target="+target+" path="+Path.Combine(output,filename)+" bytes="+report.summary.totalSize);
            }
            finally
            {
                EditorBuildSettings.scenes=oldScenes;PlayerSettings.resizableWindow=oldResizable;PlayerSettings.fullScreenMode=oldMode;
                PlayerSettings.defaultScreenWidth=oldWidth;PlayerSettings.defaultScreenHeight=oldHeight;
            }
        }
    }
}
