using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestCombatV2.Editor
{
    // Select a Play-mode overlay on the real main scene; never save a scene fork.
    [InitializeOnLoad]
    public static class DummyTrainingTools
    {
        public const string ScenePath=Chapter1MapSelector.ProceduralScene;
        const string SessionKey="LostWords.DummyTraining.Selected";
        static bool opening;
        static DummyTrainingTools()
        {
            ForestCombatWorld.PreparingEditorPlay-=ConfigureTraining;
            ForestCombatWorld.PreparingEditorPlay+=ConfigureTraining;
            EditorSceneManager.sceneOpened-=SceneOpened;
            EditorSceneManager.sceneOpened+=SceneOpened;
        }
        static void SceneOpened(Scene scene,OpenSceneMode mode)
        {
            if(!opening&&!EditorApplication.isPlayingOrWillChangePlaymode&&scene.path!=ScenePath)SelectNormalMode();
        }
        [MenuItem("Tools/Deviant/Chapter1/Open Dummy Training")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Prepare();
        }
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before selecting training.");
            var asset=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if(!asset)throw new InvalidOperationException("Main test scene is missing: "+ScenePath);
            opening=true;
            try{EditorSceneManager.OpenScene(ScenePath);}finally{opening=false;}
            SessionState.SetBool(SessionKey,true);
            EditorSceneManager.playModeStartScene=null;
        }
        public static void SelectNormalMode()
        {
            SessionState.EraseBool(SessionKey);
            EditorSceneManager.playModeStartScene=null;
        }
        static void ConfigureTraining(ForestCombatWorld world)
        {
            if(!Application.isPlaying||world.gameObject.scene.path!=ScenePath||!SessionState.GetBool(SessionKey,false))return;
            // Awake precedes run binding's sceneLoaded and the shared Start.
            // Scene hierarchy, art, input, movement, weapon data and HUD remain owned by main.
            world.proceduralRooms=true;world.clockBossTestOnly=true;world.dummyTrainingOnly=true;
            world.enablePlaytestWorkbench=false;world.proceduralSeed=31;
        }
    }
}
