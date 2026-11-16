using System.Linq;
using UnityEditor;
using UnityEditor.Build;
namespace ForestCombatV2.Editor
{
    public static class DebugMenuBuildOption
    {
        const string Symbol = "DEVIANT_DEBUG_MENU";
        const string Menu = "Tools/deviant/치트 모드/플레이어 빌드에 치트 모드 포함";
        [MenuItem(Menu)] static void Toggle()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (!symbols.Remove(Symbol)) symbols.Add(Symbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
        }
        [MenuItem(Menu, true)] static bool Validate()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            UnityEditor.Menu.SetChecked(Menu, PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Contains(Symbol));
            return true;
        }
    }
}
