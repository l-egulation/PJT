using Deviant.Game.Editor;
using ForestCombatV2.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Deviant.Playtest.Editor
{
    public static class GameSandboxTools
    {
        [MenuItem("Tools/deviant/Testing/Open Game Sandbox")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Prepare();
        }
        public static void Prepare()
        {
            DummyTrainingTools.SelectNormalMode();
            EditorSceneManager.OpenScene(GameSandbox.ScenePath);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameSandbox.ScenePath);
        }
        [MenuItem("Tools/deviant/Testing/Play Game Sandbox")]
        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!GameAssetPreparation.Prepare("Game Sandbox")) return;
            Prepare();
            EditorApplication.isPlaying = true;
        }
    }
}
