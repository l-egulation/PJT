using System.Collections.Generic;
using System.IO;
using Deviant.CombatLab;
using Deviant.Game.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Combat.Editor
{
    public static class CombatArenaBuilder
    {
        public const string ScenePath = "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity";
        const string Art = "Assets/Deviant/World/Art/Character/";
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Deviant/Combat/Resources/MeleeCatalog.json"))
                throw new System.InvalidOperationException("Export MeleeCatalog.json before creating the scene.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root = new GameObject("Combat V2 Arena");
            var arena = root.AddComponent<CombatArena>();
            var diagnostics = root.AddComponent<CombatDiagnostics>();
            diagnostics.arena = arena;
            diagnostics.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Deviant/UI/Fonts/Pretendard-Regular.ttf");
            diagnostics.showPanel = false; arena.showBoxes = false;
            var lab = root.AddComponent<LabComposition>(); lab.arena = arena; lab.diagnostics = diagnostics;
            var idle = Frames("idle"); if (idle.Length > 0) lab.idleSprite = idle[0];
            lab.spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deviant/World/Data/OriginalPixelUnlit.mat");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
            Selection.activeGameObject = arena.gameObject;
            Debug.Log("Combat V2 arena ready. Press Play. No legacy scene/build settings or player save changed.");
        }
        static Sprite[] Frames(string clip)
        {
            var result = new List<Sprite>();
            for (int i = 1; i <= 12; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + clip + "/" + i.ToString("00") + ".png");
                if (sprite) result.Add(sprite);
            }
            return result.ToArray();
        }
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        public static void BuildWindows()
        {
            Build();
            string output = Path.GetFullPath("../deviant-CombatV2-Windows/deviant.exe");
            string[] args=System.Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) if(args[i]=="-combatV2BuildOutput") output=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development, extraScriptingDefines = new[] { "DEVIANT_COMBAT_LAB" } });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("CombatV2 build failed: " + result.summary.result);
        }
    }
}
