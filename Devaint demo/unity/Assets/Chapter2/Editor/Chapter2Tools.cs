using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Game.Diagnostics;
using Deviant.Stage1Monsters;
using Deviant.Stage1Monsters.Game;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Deviant.Chapter2.Editor
{
    public static class Chapter2Tools
    {
        public const string ScenePath="Assets/Chapter2/Scenes/Chapter2Procedural.unity";
        static double playAfter;
        [MenuItem("Tools/deviant/Play Chapter2")]
        public static void Play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Open();playAfter=EditorApplication.timeSinceStartup+5;
            EditorApplication.update-=PlayWhenImported;EditorApplication.update+=PlayWhenImported;
        }
        static void PlayWhenImported()
        {
            // Startup CLI calls may still have an import/reload queued after opening the scene.
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){playAfter=EditorApplication.timeSinceStartup+5;return;}
            if(EditorApplication.timeSinceStartup<playAfter)return;
            EditorApplication.update-=PlayWhenImported;
            if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;
        }
        [MenuItem("Tools/deviant/Chapter2")]
        public static void Open(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))Create();else EditorSceneManager.OpenScene(ScenePath);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);}
        public static void Create()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity6000.6.5f1.");
            Directory.CreateDirectory("Assets/Chapter2/Scenes");AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var arena=new GameObject("Chapter2 combat").AddComponent<CombatArena>();arena.uiFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Deviant/UI/Fonts/Pretendard-Regular.ttf");arena.showDebugUI=false;arena.showPanel=false;
            var diagnostics=arena.gameObject.AddComponent<CombatDiagnostics>();diagnostics.arena=arena;diagnostics.uiFont=arena.uiFont;diagnostics.showDebugUI=false;diagnostics.showPanel=false;
            var camera=new GameObject("Chapter2 camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=8.4375f;camera.clearFlags=CameraClearFlags.SolidColor;camera.transform.position=new Vector3(10,8,-10);camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var world=new GameObject("Chapter2 stage").AddComponent<Chapter2World>();world.terrainShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Chapter2/Runtime/Chapter2Terrain.shader");world.arena=arena;world.diagnostics=diagnostics;world.sceneCamera=camera;
            world.spriteMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Deviant/World/Data/OriginalPixelUnlit.mat");world.playerSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Deviant/Combat/Resources/CharacterConfirmed/base_sword/F006.png");world.terrainPalette=AssetDatabase.LoadAssetAtPath<Chapter1TeamTerrainPalette>("Assets/ForestCombatV2/Resources/Chapter1TeamSketch.asset");world.bossSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ForestCombatV2/Resources/ClockExecutioner/Base.png");
            var content=world.gameObject.AddComponent<Stage1VillageMonsterContent>();content.content=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Deviant/Stage1Monsters/Game/Data/VillageMonsterContent.json");content.motions=null;content.effects=AssetDatabase.LoadAssetAtPath<Stage1MonsterEffectCatalog>("Assets/Deviant/Stage1Monsters/Game/Data/Stage1MonsterEffects.asset");world.monsterContent=content;
            world.windowCityShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Chapter2/Runtime/Chapter2WindowCity.shader");
            if(!world.spriteMaterial||!world.playerSprite||!world.terrainPalette||!content.content||!world.bossSprite||!world.windowCityShader)throw new InvalidOperationException("Existing approved game assets missing.");
            EditorSceneManager.SaveScene(scene,ScenePath);var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==ScenePath)){scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();}AssetDatabase.SaveAssets();Debug.Log("CHAPTER2_SCENE_READY "+ScenePath);
        }
        [MenuItem("Tools/deviant/Validation/Chapter2 Layout Bank")]
        public static void ValidateBank(){var b=C2Bank.Load();foreach(var l in b.layouts){var errors=C2Bank.Validate(l);if(errors.Count>0)throw new InvalidOperationException(l.id+": "+string.Join(",",errors));}Debug.Log("CHAPTER2_BANK_VALID "+b.layouts.Length);}
        // Batch entry point. Build artifacts belong in ignored outputs, never beside approved assets.
        public static void BuildStandalone()
        {
            Create();ValidateBank();var output=Environment.GetEnvironmentVariable("C2_BUILD_PATH");if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Set C2_BUILD_PATH to an outputs executable path.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Chapter2 build failed: "+report.summary.result);
            Debug.Log("CHAPTER2_BUILD_READY "+output+" bytes="+report.summary.totalSize);
        }
    }
}
