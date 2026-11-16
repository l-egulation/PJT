using UnityEngine;

namespace Deviant.Game
{
    // Malgun Gothic regular and bold for the village journal screens (TemporaryGameGui.Hub). The asset in
    // TemporaryGUI/Resources/JournalGUI references the deployed UI fonts, so they load at runtime without a second copy.
    public sealed class JournalFonts : ScriptableObject
    {
        public Font regular, bold;
    }
}
