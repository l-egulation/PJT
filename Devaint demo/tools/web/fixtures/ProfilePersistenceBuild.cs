using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProfilePersistenceBuild
{
    public static void Build()
    {
        if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Unity 6000.6.5f1 required");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Camera", typeof(Camera));
        new GameObject("Profile persistence fixture", typeof(ProfilePersistenceProbe));
        const string path = "Assets/ProfilePersistenceFixture.unity";
        EditorSceneManager.SaveScene(scene, path);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../web"));
        Directory.CreateDirectory(output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { path }, target = BuildTarget.WebGL,
            locationPathName = output, options = BuildOptions.None });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Profile fixture build failed: " + report.summary.result);
    }
}
