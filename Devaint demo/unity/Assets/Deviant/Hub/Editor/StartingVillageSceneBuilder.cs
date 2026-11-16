#if UNITY_EDITOR
using System.Collections.Generic;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Hub.Editor
{
    // Rebuilds the starting village scene from the chapter scene so both share the same camera, pixel viewport,
    // combat runtime and player setup. Chapter-only content (village map art, monsters, verification) is removed and
    // the world switches to the flat lobby map. Re-run after changing the chapter scene's shared runtime objects.
    public static class StartingVillageSceneBuilder
    {
        public const string ScenePath = "Assets/Deviant/Hub/Scenes/StartingVillage.unity";
        const string WorldJson = "Assets/Deviant/Hub/Data/StartingVillageWorld.json";
        static readonly HashSet<string> ChapterOnly = new HashSet<string>
        {
            "VillagePresentation", "VillageArtTransition", "VillageTeamMapSmoke", "Stage1VillageMonsterReview",
            "Stage1VillageMonsterContent", "Chapter1MonsterTestSettings", "Chapter1MonsterVerification", "ForestRuntimeVerification",
        };

        [MenuItem("Tools/deviant/Hub/Build Starting Village Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component && ChapterOnly.Contains(component.GetType().Name)) Object.DestroyImmediate(component, true);
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            world.worldJson = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldJson);
            world.proceduralRooms = false; world.villageHub = true; world.clockBossTestOnly = false; world.dummyTrainingOnly = false;
            world.roundedTerrainJson = null; world.roundedTerrainAtlas = null; world.useTeamSketchTerrain = false;
            world.enablePlaytestWorkbench = false; world.enablePlaytestBasicSpeeds = false; world.proceduralSeed = -1;
            world.gameObject.name = "Starting village world";
            if (!world.GetComponent<StartingVillageWorld>()) world.gameObject.AddComponent<StartingVillageWorld>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("STARTING_VILLAGE_SCENE_BUILT " + ScenePath);
        }

        // Batch entry: Unity -batchmode -executeMethod Deviant.Hub.Editor.StartingVillageSceneBuilder.BuildAndQuit
        public static void BuildAndQuit()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
#endif
