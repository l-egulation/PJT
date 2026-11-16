using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Deviant.Combat;

namespace Deviant.Game.Editor
{
    // Local editor entry point. Wait through startup imports before creating runtime-only state.
    [InitializeOnLoad]
    public static class PlaytestWorkbenchPreview
    {
        static readonly string RequestPath=Path.GetFullPath("../outputs/playtest-workbench.request");
        static double quietUntil,captureAt;
        static PlaytestWorkbenchPreview(){EditorApplication.update+=Tick;}
        public static void Open()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));File.WriteAllText(RequestPath,"open");
        }
        static void Tick()
        {
            if(File.Exists(RequestPath))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;quietUntil=0;return;}
                if(EditorApplication.isCompiling||EditorApplication.isUpdating){quietUntil=0;return;}
                if(quietUntil==0){quietUntil=EditorApplication.timeSinceStartup+8;return;}
                if(EditorApplication.timeSinceStartup<quietUntil)return;
                File.Delete(RequestPath);quietUntil=0;
                EditorSceneManager.OpenScene("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
                var world=Object.FindAnyObjectByType<ForestCombatV2.ForestCombatWorld>();
                world.enablePlaytestWorkbench=true;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                var gameView=EditorWindow.focusedWindow;
                if(gameView && gameView.GetType().Name=="GameView")gameView.maximized=true;
                SessionState.SetBool("WorkbenchPreviewCapture",true);EditorApplication.isPlaying=true;
                return;
            }
            if(!SessionState.GetBool("WorkbenchPreviewCapture",false)||!EditorApplication.isPlaying)return;
            var arena=Object.FindAnyObjectByType<Deviant.Combat.CombatArena>();
            if(!arena || !arena.Player)return;
            var workbench=Object.FindAnyObjectByType<PlaytestEnvironment>();
            if(!workbench || workbench.Current==null)return;
            if(captureAt==0){workbench.SetOpen(true);captureAt=EditorApplication.timeSinceStartup+2;return;}
            if(EditorApplication.timeSinceStartup<captureAt)return;
            SessionState.SetBool("WorkbenchPreviewCapture",false);captureAt=0;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("../outputs/playtest-workbench-preview.png"));
            Debug.Log("WORKBENCH_READY enemies="+arena.enemies.Count+" panel="+arena.showPanel+" paused="+arena.Controls.Current.SimulationPaused);
        }
    }
}
