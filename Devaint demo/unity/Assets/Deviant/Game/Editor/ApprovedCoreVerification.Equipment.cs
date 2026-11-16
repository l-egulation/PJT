using UnityEditor;

namespace Deviant.Game.Editor
{
    public static partial class ApprovedCoreVerification
    {
        // Retain existing editor/CLI entry points for the merged inventory acceptance route.
        [MenuItem("Tools/deviant/Approved UI/Verify equipment and capture")]
        public static void RunEquipment() => RunInventory();
    }
}
