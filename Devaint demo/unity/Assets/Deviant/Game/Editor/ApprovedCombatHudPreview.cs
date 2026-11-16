using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Game.Editor
{
    // Explicit local review of already-deployed artwork; production asset preparation stays unchanged.
    public static class ApprovedCombatHudPreview
    {
        [MenuItem("Tools/deviant/Combat HUD/Play Applied HUD")]
        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Resources.Load<TextAsset>("ApprovedCombatHUD/bindings"))
                throw new System.InvalidOperationException("Deploy the approved combat HUD before playing its local preview.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }
    }
}
