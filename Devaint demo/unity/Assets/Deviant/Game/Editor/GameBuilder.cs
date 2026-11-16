using System;
using System.IO;
using System.Linq;
using Deviant.Game.Diagnostics;
using Deviant.Media.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Game.Editor
{
    public static class GameBuilder
    {
        const string WebTemplate = "PROJECT:DeviantLoading";
        public const string ScenePath = GameEntry.ScenePath;
        // The chapter 1 play build starts at its title; ForestCombatV2 owns the scene.
        public const string Chapter1TitleScene = "Assets/ForestCombatV2/Scenes/Chapter1Title.unity";
        public static readonly string[] EntryScenes = System.Array.Empty<string>();
        [MenuItem("Tools/deviant/Game/Open Common Game")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Chapter1TitleScene);
            EditorSceneManager.OpenScene(ScenePath);
        }
        // Existing command-line callers keep working without regenerating authored content.
        public static void Create() { Open(); }
        [MenuItem("Tools/deviant/Game/Play Common Game")]
        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!GameAssetPreparation.Prepare("Common Game")) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Chapter1TitleScene);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
        // Obsolete CLI entry points now keep only the current title/stage routes.
        public static void BindSceneComposition() { Open(); }
        public static void RebuildEntryScenes()
        {
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(Chapter1TitleScene,true),
                new EditorBuildSettingsScene(ForestCombatV2.Chapter1RunHost.VillageScene,true),
                new EditorBuildSettingsScene("Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity",true),
                new EditorBuildSettingsScene("Assets/Chapter2/Scenes/Chapter2Procedural.unity",true) };
            AssetDatabase.SaveAssets();
        }
        public static void ValidateAssets()
        {
            Deviant.Stage1Monsters.Editor.Stage1RuntimeAssetValidation.ReimportAndValidate();
            foreach (string path in new[] {
                Chapter1TitleScene,
                "Assets/Deviant/UI/Sprites/hp_fill.png",
                "Assets/Deviant/Hub/Resources/Hub/buildings/color/palette-atelier.png",
                "Assets/Deviant/Combat/Resources/CharacterConfirmed/base_sword/F006.png" })
                if (!File.Exists(path)) throw new InvalidOperationException("Required game art missing: " + path + ". Apply the pinned resources selection using tools/assets/update_assets.py; see docs/assets/README.md.");
            // A file can exist but fail texture import. Validate the actual sprites used by the opening before packaging.
            var opening = JsonUtility.FromJson<Deviant.Narrative.Opening.OpeningScript>(Resources.Load<TextAsset>("HubOpening").text);
            foreach (var action in opening.beats.SelectMany(beat => beat.actions).Where(action => action.type == "actor" && action.spawn == "helper"))
                ForestCombatV2.OpeningHelperArt.Load(action.sprite);
        }
        [MenuItem("Tools/deviant/Game/Build Web")]
        public static void BuildWeb()
        {
            string output = Environment.GetEnvironmentVariable("DEVIANT_WEB_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Path.GetFullPath("../outputs/builds/web");
            BuildWebVariant(output, Environment.GetEnvironmentVariable("DEVIANT_WEB_TEXTURE_FORMAT") ?? "dxt");
        }
        [MenuItem("Tools/deviant/Game/Build Web With Cheat Mode")]
        public static void BuildWebCheatMode() => BuildWebVariant(CheatModeOutput("web"),
            Environment.GetEnvironmentVariable("DEVIANT_WEB_TEXTURE_FORMAT") ?? "dxt", cheatMode: true);

        [MenuItem("Tools/deviant/Game/Compare Web BC7 and DXT")]
        public static void BuildWebDual()
        {
            string output = Environment.GetEnvironmentVariable("DEVIANT_WEB_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Path.GetFullPath("../outputs/builds/web-dual");
            try
            {
                BuildWebVariant(Path.Combine(output, "bc7"), "bc7");
                BuildWebVariant(Path.Combine(output, "dxt"), "dxt");
            }
            finally { CompressedMediaBuild.Prepare("dxt"); }
        }
        static void BuildWebVariant(string output, string format, bool cheatMode = false)
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Unity 6000.6.5f1 is required.");
            CompressedMediaBuild.Prepare(format);
            ValidateAssets();
            Deviant.Presentation.Editor.HitFeedbackAssetBuilder.Build();
            // Gzip fallback also runs on static hosts that do not set Content-Encoding.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = WebTemplate;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            Directory.CreateDirectory(output);
            using var media = CompressedMediaBuild.BeginPackaging();
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Chapter1TitleScene, ForestCombatV2.Chapter1RunHost.VillageScene, "Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity", "Assets/Chapter2/Scenes/Chapter2Procedural.unity" },
                locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None,
                subtarget = (int)WebGLTextureSubtarget.DXT,
                extraScriptingDefines = cheatMode ? new[] { "DEVIANT_DEBUG_MENU" } : Array.Empty<string>() });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Common Web game build failed: " + result.summary.result);
            Debug.Log("COMPRESSED_MEDIA_BUILD_OK format=" + format + " cheatMode=" + cheatMode + " options=" + result.summary.options + " bytes=" + result.summary.totalSize);
        }

        [MenuItem("Tools/deviant/Game/Build Linux With Cheat Mode")]
        public static void BuildLinuxCheatMode()
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Unity 6000.6.5f1 is required.");
            CompressedMediaBuild.Prepare("bc7");
            ValidateAssets();
            Deviant.Presentation.Editor.HitFeedbackAssetBuilder.Build();
            string output = CheatModeOutput("linux");
            Directory.CreateDirectory(output);
            var oldMode = PlayerSettings.fullScreenMode;
            bool oldResizable = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;
                using var media = CompressedMediaBuild.BeginPackaging();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { Chapter1TitleScene, ForestCombatV2.Chapter1RunHost.VillageScene,
                        "Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity",
                        "Assets/Chapter2/Scenes/Chapter2Procedural.unity", "Assets/Chapter2/Scenes/RoseBlueBoss.unity" },
                    locationPathName = Path.Combine(output, "deviant.x86_64"), target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.None, extraScriptingDefines = new[] { "DEVIANT_DEBUG_MENU" } });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Linux cheat-mode build failed: " + report.summary.result);
                Debug.Log("CHEAT_MODE_BUILD_OK options=" + report.summary.options + " path=" + output + " bytes=" + report.summary.totalSize);
            }
            finally { PlayerSettings.fullScreenMode = oldMode; PlayerSettings.resizableWindow = oldResizable; }
        }

        // Preserve existing batch callers; cheat mode adds only the existing menu define.
        public static void BuildLinuxDebug() => BuildLinuxCheatMode();

        static string CheatModeOutput(string platform)
        {
            string output = Environment.GetEnvironmentVariable("DEVIANT_CHEAT_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Environment.GetEnvironmentVariable("DEVIANT_DEBUG_BUILD_OUTPUT");
            return string.IsNullOrEmpty(output) ? Path.GetFullPath("../outputs/builds/" + platform + "-cheat") : output;
        }

        [MenuItem("Tools/deviant/Game/Build Windows")]
        public static void BuildWindows() => BuildWindowsVariant(false);

        [MenuItem("Tools/deviant/Game/Build Windows With Cheat Mode")]
        public static void BuildWindowsCheatMode() => BuildWindowsVariant(false, cheatMode: true);

        [MenuItem("Tools/deviant/Game/Build Windows With Debugging")]
        public static void BuildWindowsDebug() => BuildWindowsVariant(true);

        static void BuildWindowsVariant(bool development, bool cheatMode = false)
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Unity 6000.6.5f1 is required.");
            CompressedMediaBuild.Prepare("bc7");
            ValidateAssets();
            // Hit VFX bindings are git-ignored generated assets; refresh them so the player ships the effects.
            Deviant.Presentation.Editor.HitFeedbackAssetBuilder.Build();
            string output = cheatMode ? CheatModeOutput("windows") : Environment.GetEnvironmentVariable(development ? "DEVIANT_DEBUG_BUILD_OUTPUT" : "TEAM_PLAYTEST_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Path.GetFullPath(development ? "../outputs/builds/windows-debug" : "Builds/Windows/deviant");
            Directory.CreateDirectory(output);
            using var media = CompressedMediaBuild.BeginPackaging();
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Chapter1TitleScene, ForestCombatV2.Chapter1RunHost.VillageScene, "Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity", "Assets/Chapter2/Scenes/Chapter2Procedural.unity" }, locationPathName = Path.Combine(output,"deviant.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None,
                extraScriptingDefines = development || cheatMode ? new[] { "DEVIANT_DEBUG_MENU" } : Array.Empty<string>() });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Common game build failed: " + result.summary.result);
            Debug.Log("WINDOWS_BUILD_OK development=" + development + " cheatMode=" + (development || cheatMode) + " options=" + result.summary.options
                + " path=" + output + " bytes=" + result.summary.totalSize);
        }
    }
}
