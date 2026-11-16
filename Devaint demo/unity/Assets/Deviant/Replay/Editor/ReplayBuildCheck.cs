using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Replay.Editor
{
    public static class ReplayBuildCheck
    {
        // Batch-only packaging check using the real project's packages and code, without importing substitute media.
        public static void BuildSmoke()
        {
            const string fixture = "Assets/__ReplayBuildFixture.unity";
            if (File.Exists(fixture)) throw new IOException("Refusing to overwrite a build fixture.");
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Camera", typeof(Camera)); EditorSceneManager.SaveScene(scene, fixture);
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/replay/player")); Directory.CreateDirectory(directory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { fixture }, target = BuildTarget.StandaloneLinux64,
                    locationPathName = Path.Combine(directory, "ReplaySmoke"), options = BuildOptions.Development });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Replay packaging check: " + report.summary.result);
                Debug.Log("Replay packaging smoke succeeded; this does not validate the complete game or real-hour size.");
            }
            finally { AssetDatabase.DeleteAsset(fixture); }
        }
    }
}
