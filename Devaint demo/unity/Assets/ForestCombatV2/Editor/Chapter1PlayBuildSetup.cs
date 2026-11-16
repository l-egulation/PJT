using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    // Common play build order: title, starting village, chapter one, chapter two.
    public static class Chapter1PlayBuildSetup
    {
        public static readonly string[] BuildScenes =
        {
            Chapter1RunHost.TitleScene,
            Chapter1RunHost.VillageScene,
            Chapter1MapSelector.ProceduralScene,
            "Assets/Chapter2/Scenes/Chapter2Procedural.unity",
        };

        [MenuItem("Tools/deviant/Chapter1/Create Title Scene And Build Order")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Title camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<AudioListener>();
            var title = new GameObject("Chapter1 title").AddComponent<Chapter1TitleScreen>();
            title.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Deviant/UI/Fonts/Pretendard-Regular.ttf");
            EditorSceneManager.SaveScene(scene, Chapter1RunHost.TitleScene);
            EditorBuildSettings.scenes = BuildScenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
