using ForestCombatV2;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        // The village bottle is collected immediately; it has no dialogue, question mark or input scope.
        Chapter1VillageInkPickup RedInkPickup => boss ? boss.VillageInkPickup : null;
    }
}
