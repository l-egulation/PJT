using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Game.Editor
{
    // Local review entry point; leaves the authored common scene and gameplay controls intact.
    [InitializeOnLoad]
    public static class LocalGamePreview
    {
        static bool captured;
        static double readyAt;
        static LocalGamePreview(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            EditorSceneManager.OpenScene(GameEntry.ScenePath);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            SessionState.SetBool("LocalGamePreviewCapture",true);
            EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool("LocalGamePreviewCapture",false)||!EditorApplication.isPlaying)return;
            var arena=UnityEngine.Object.FindAnyObjectByType<Deviant.Combat.CombatArena>();
            if(!arena||!arena.Player)return;
            if(readyAt==0){readyAt=EditorApplication.timeSinceStartup+2;return;}
            if(captured||EditorApplication.timeSinceStartup<readyAt)return;
            captured=true;SessionState.SetBool("LocalGamePreviewCapture",false);
            string path=Path.GetFullPath("../outputs/latest-common-game.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("LOCAL_GAME_READY "+path+" foot="+arena.Player.visual.transform.localPosition+" scale="+arena.Player.visual.transform.localScale);
        }
    }
}
