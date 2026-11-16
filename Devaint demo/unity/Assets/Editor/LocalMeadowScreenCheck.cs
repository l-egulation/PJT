using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Local diagnostic only: explicitly invoked by -executeMethod.
[InitializeOnLoad]
public static class LocalMeadowScreenCheck
{
    const string Pending = "LocalMeadowScreenCheck.Pending";
    static double started;
    static LocalMeadowScreenCheck() { EditorApplication.update += Tick; }
    public static void OpenAndVerify()
    {
        SessionState.SetBool(Pending + ".Launch", true);
        started = 0;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Pending + ".Launch", false))
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { started = 0; return; }
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; started = 0; return; }
            if (started == 0) { started = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - started < 15) return;
            SessionState.SetBool(Pending + ".Launch", false);
            EditorSceneManager.OpenScene("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
            SessionState.SetBool(Pending, true);
            started = 0;
            EditorApplication.isPlaying = true;
            return;
        }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (started == 0) { started = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - started < 4) return;
        SessionState.SetBool(Pending, false);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/meadow-screen-check"));
        Directory.CreateDirectory(output);
        string details = "scene=" + SceneManager.GetActiveScene().path + "\nproject=" + Application.dataPath;
        foreach (var camera in Camera.allCameras)
            details += "\ncamera=" + camera.name + " enabled=" + camera.enabled + " position=" + camera.transform.position + " depth=" + camera.depth + " mask=" + camera.cullingMask;
        File.WriteAllText(Path.Combine(output, "state.txt"), details);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "game.png"));
        Debug.Log("[Meadow screen check] " + details);
    }
}
