namespace ForestCombatV2
{
    // Chapter-one adapter; the same pickup also serves later chapter bosses.
    public sealed class Chapter1VillageInkPickup : VillageInkPickup
    {
        public void Initialize(ForestCombatWorld owner, Chapter1BossEncounter boss)
        {
            Initialize(owner.arena, () => owner && owner.Ready && boss && boss.Boss,
                () => boss.Boss.Dead, () => boss.Boss.Position, () => boss.ExitPosition.x,
                () => { var binding=owner.GetComponent<Chapter1RunBinding>();
                    return binding && binding.ProfileRun && Chapter1ProfileHost.Service.PalettePieces == 0; }, 1);
        }
    }
}
