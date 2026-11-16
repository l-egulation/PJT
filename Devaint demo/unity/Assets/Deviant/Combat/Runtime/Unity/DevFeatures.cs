using UnityEngine;

namespace Deviant.Combat
{
    // Cheat mode uses the existing DEVIANT_DEBUG_MENU flag, independent of Development Build.
    public static class DevFeatures
    {
        public static bool ShowEnemyHealthBars { get; set; } = true;
        static bool? forced;
#if DEVIANT_DEBUG_MENU
        public const bool IncludedInPlayer = true;
#else
        public const bool IncludedInPlayer = false;
#endif
        public static bool Enabled => forced ?? (Application.isEditor || IncludedInPlayer);
        // EditMode tests stand in for a player build; pass null to restore the real build flag.
        public static void ForceForTests(bool? enabled) { forced = enabled; }
    }
}
