using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Game.Diagnostics;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Stage1VillageExperiment.Editor
{
    public static class Stage1VillageBuilder
    {
        public const string Root = "Assets/Stage1VillageExperiment";
        public const string ScenePath = Root + "/Scenes/Stage1VillageExperiment.unity";
        public const string DataPath = Root + "/Data/VillageWorld.json";

        public static void Create() { TryCreate(); }

        internal static bool TryCreate()
        {
            RequireEditorVersion();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create the village from edit mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            if (!json) throw new InvalidOperationException("Authored village layout missing: " + DataPath);
            var data = JsonUtility.FromJson<ForestData>(json.text);
            if (data == null || data.spawn == null) throw new InvalidOperationException("Village layout is invalid.");
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var arena = new GameObject("Shared combat runtime").AddComponent<CombatArena>();
            arena.showBoxes = false;
            arena.showDebugUI = false;
            arena.showPanel = false;
            var diagnostics = arena.gameObject.AddComponent<CombatDiagnostics>();
            diagnostics.arena = arena;
            diagnostics.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Deviant/UI/Fonts/Pretendard-Regular.ttf");
            diagnostics.showDebugUI = false;
            diagnostics.showPanel = false;
            var camera = new GameObject("Village gameplay camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 540f / 64;
            camera.transform.position = new Vector3(15, -8.4375f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(230, 220, 196, 255);
            camera.gameObject.AddComponent<AudioListener>();
            var world = new GameObject("Stage1 authored village").AddComponent<Stage1VillageWorld>();
            world.arena = arena;
            world.worldJson = json;
            world.sceneCamera = camera;
            world.diagnostics = diagnostics;
            world.fixedStepsPerSecond = 120;
            world.enemiesAggressive = true;
            world.spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deviant/World/Data/OriginalPixelUnlit.mat");
            world.playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Deviant/Combat/Resources/CharacterConfirmed/base_sword/F006.png");
            world.monsterAnimations = AssetDatabase.LoadAssetAtPath<Chapter1MonsterCatalog>("Assets/ForestCombatV2/Data/Chapter1MonstersV4.asset");
            world.monsterAttackEffects = AssetDatabase.LoadAssetAtPath<Chapter1AttackEffectCatalog>("Assets/ForestCombatV2/Data/ApprovedAttackEffects.asset");
            if (!world.playerSprite || !world.spriteMaterial || !world.monsterAnimations)
                throw new InvalidOperationException("Restore the approved common character, pixel material and monster catalog before creating the village.");
            world.monsterSprites = (data.monsters ?? Array.Empty<MonsterKind>()).Select(species =>
            {
                var clip = world.monsterAnimations.Find(species.id, "move");
                if (clip == null || clip.frames == null || clip.frames.Length == 0 || !clip.frames[0])
                    throw new InvalidOperationException("Approved village species sprite missing: " + species.id);
                return clip.frames[0];
            }).ToArray();
            world.gameObject.AddComponent<VillagePresentation>().world = world;
            world.gameObject.AddComponent<VillageLandmarkPresentation>().world = world;
            var pixelView=world.gameObject.AddComponent<VillagePixelViewport>();pixelView.world=world;pixelView.sourceCamera=camera;
            world.gameObject.AddComponent<VillageBuildSmoke>().world=world;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("STAGE1_VILLAGE_SCENE_READY " + ScenePath);
            return true;
        }

        public static void VerifyFresh()
        {
            if (!TryCreate()) return;
            Stage1VillageVerification.Run();
        }

        public static void Open()
        {
            RequireEditorVersion();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void BuildWindows()
        {
            RequireEditorVersion();
            if (!File.Exists(ScenePath) && !TryCreate()) return;
            string output = Environment.GetEnvironmentVariable("STAGE1_VILLAGE_BUILD_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = Path.GetFullPath("../outputs/stage1-village/Windows/Stage1Village.exe");
            if (!output.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) output = Path.Combine(output, "Stage1Village.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Stage1 village Windows build failed: " + result.summary.result);
            Debug.Log("STAGE1_VILLAGE_BUILD_READY " + output);
        }

        static void RequireEditorVersion()
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Use the project Unity version 6000.6.5f1.");
        }
    }
}
