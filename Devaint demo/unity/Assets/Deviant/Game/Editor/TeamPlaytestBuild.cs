using UnityEditor;

namespace Deviant.Game.Editor
{
public static class TeamPlaytestBuild
{
    [MenuItem("Tools/Team Playtest/Build Windows")]
    public static void BuildWindows() { Deviant.Game.Editor.GameBuilder.BuildWindows(); }
    // Preserve automation entry points; both now build the common game.
    public static void BuildForestWindows() { BuildWindows(); }
}
}
