#if UNITY_EDITOR
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Deviant.Game.Editor
{
    public static class SketchTitleEditor
    {
        [MenuItem("Tools/deviant/Open Sketch Title")]
        public static void Open()
        {
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Chapter1RunHost.TitleScene);
            Deviant.Hub.Editor.ApprovedVillageEditor.UseFullHdGameView();
        }
        public static void OpenAndPlay()
        {
            Open();EditorApplication.delayCall+=EditorApplication.EnterPlaymode;
        }
    }
}
#endif
