using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Retired scenes live outside Assets as text fixtures (unity/Tests/Fixtures/LegacyScenes/README.md). A test that opens
// one runs only after that fixture has been copied back; otherwise it is skipped rather than failed. Scenes that exist,
// and missing scenes that are not preserved fixtures, open exactly as EditorSceneManager.OpenScene would.
// The same helper is copied into each test assembly that opens retired scenes (no shared test assembly exists).
internal static class LegacySceneFixture
{
    const string FixtureRoot = "Tests/Fixtures/LegacyScenes";

    public static Scene Open(string path, OpenSceneMode mode = OpenSceneMode.Single)
    {
        if (!File.Exists(path) && File.Exists(Path.Combine(FixtureRoot, path + ".txt")))
            Assert.Ignore("Retired scene not restored: " + path + ". See unity/Tests/Fixtures/LegacyScenes/README.md.");
        return EditorSceneManager.OpenScene(path, mode);
    }
}
