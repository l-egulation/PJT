using UnityEditor;

namespace Deviant.Game.Editor
{
    public static partial class ApprovedCoreVerification
    {
        // Retain existing editor/CLI entry points for the merged inventory acceptance route.
        [MenuItem("Tools/deviant/Approved UI/Verify bag and crafting and capture")]
        public static void RunBagCraft() => RunInventory();
    }
}
