using Deviant.Chapter2.RoseBlue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Chapter2.Editor
{
    // SessionState survives the Play Mode domain reload. Normal game entry is unchanged.
    [InitializeOnLoad]
    public static class RoseBluePhase1Playtest
    {
        const string Pending="Deviant.RoseBlue.Phase1Playtest";
        public const string ScenePath=RoseBlueBossScene.ScenePath;
        static double playAfter;
        static RoseBluePhase1Playtest()
        {
            EditorApplication.update+=Prepare;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingPlayMode)SessionState.SetBool(Pending,false);};
        }
        [MenuItem("Tools/deviant/Open Rose Blue Boss Scene")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
        [MenuItem("Tools/deviant/Play Rose Blue Phase1")]
        public static void Play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Open();if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath)return;
            SessionState.SetBool(Pending,true);playAfter=EditorApplication.timeSinceStartup+3;
        }
        static void Prepare()
        {
            if(!SessionState.GetBool(Pending,false)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying)
            {if(!EditorApplication.isPlayingOrWillChangePlaymode&&EditorApplication.timeSinceStartup>=playAfter)EditorApplication.isPlaying=true;return;}
            var world=Object.FindAnyObjectByType<Chapter2World>();if(!world||!world.Ready||!world.Boss)return;
            if(!world.Boss.Started){world.WarpToRoom("B");return;}
            if(world.Boss.Model.Phase!=Phase.Machine)return;
            SessionState.SetBool(Pending,false);EditorApplication.isPaused=true;
            var game=System.Type.GetType("UnityEditor.GameView,UnityEditor");if(game!=null)EditorWindow.GetWindow(game).Focus();
            Debug.Log("ROSE_BLUE_PHASE1_READY: actual Chapter2 boss room, paused before combat; release Pause to test.");
        }
    }
}
