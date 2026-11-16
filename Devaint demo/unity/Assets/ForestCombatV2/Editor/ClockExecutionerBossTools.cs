using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    public static class ClockExecutionerBossTools
    {
        public const string TestScene="Assets/MeadowDuskV3/MapVer2/Scenes/ClockExecutionerTest.unity";
        [MenuItem("Tools/Deviant/Chapter1/Open Boss Test")]
        public static void OpenTest()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            DummyTrainingTools.SelectNormalMode();
            EnsureTestScene();EditorSceneManager.OpenScene(TestScene);
        }
        [MenuItem("Tools/Deviant/Chapter1/Play Boss Test")]
        public static void PlayTest()
        {
            if(EditorApplication.isPlaying)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            DummyTrainingTools.SelectNormalMode();
            EnsureTestScene();EditorSceneManager.OpenScene(TestScene);
            EditorApplication.EnterPlaymode();
        }
        public static void EnsureTestScene()
        {
            if(!File.Exists(TestScene))
            {
                if(!AssetDatabase.CopyAsset(Chapter1MapSelector.ProceduralScene,TestScene))throw new InvalidOperationException("Could not create boss test scene.");
                var scene=EditorSceneManager.OpenScene(TestScene);
                var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
                world.clockBossTestOnly=true;world.proceduralSeed=31;
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if(!scenes.Exists(s=>s.path==TestScene)){scenes.Add(new EditorBuildSettingsScene(TestScene,true));EditorBuildSettings.scenes=scenes.ToArray();}
        }
        // Entry point for a headless compile/setup pass in an isolated verification checkout.
        public static void Prepare()
        {
            EnsureTestScene();
            Debug.Log("CLOCK_BOSS_SETUP_OK");
        }
    }
}
