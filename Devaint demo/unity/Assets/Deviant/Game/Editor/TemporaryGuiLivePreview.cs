using System.IO;
using Stage1VillageExperiment;
using UnityEditor;
using UnityEngine;

namespace Deviant.Game.Editor
{
    [InitializeOnLoad]
    public static class TemporaryGuiLivePreview
    {
        const string Request="TemporaryGuiLivePreview";
        static int frames,lastFrame=-1;
        static float seconds,maximum;
        static double warmup;
        static TemporaryGuiLivePreview(){EditorApplication.update+=Tick;}
        public static void Open()
        {
            SessionState.SetBool(Request,true);
            Deviant.Combat.Editor.CombatFeelPreview.OpenGame();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Request,false)||!EditorApplication.isPlaying)return;
            var gui=Object.FindAnyObjectByType<TemporaryGameGui>();var viewport=Object.FindAnyObjectByType<VillagePixelViewport>();
            if(!gui||!viewport||!viewport.Ready||!viewport.ExternalPresentation)return;
            if(warmup==0){warmup=EditorApplication.timeSinceStartup+3;return;}
            if(EditorApplication.timeSinceStartup<warmup||Time.frameCount==lastFrame)return;
            lastFrame=Time.frameCount;frames++;seconds+=Time.unscaledDeltaTime;maximum=Mathf.Max(maximum,Time.unscaledDeltaTime);
            if(frames<180)return;
            var path=Path.GetFullPath("../outputs/gui-verification");Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,"live-frame-timing.json"),JsonUtility.ToJson(new Result{frames=frames,averageMs=seconds/frames*1000,maximumMs=maximum*1000,nativeCanvas=viewport.ExternalPresentation,guiActive=gui.isActiveAndEnabled},true));
            ScreenCapture.CaptureScreenshot(Path.Combine(path,"live-backbuffer.png"));SessionState.SetBool(Request,false);
        }
        [System.Serializable]sealed class Result{public int frames;public float averageMs,maximumMs;public bool nativeCanvas,guiActive;}
    }
}
