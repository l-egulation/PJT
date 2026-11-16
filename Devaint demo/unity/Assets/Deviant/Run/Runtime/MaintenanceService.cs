namespace Deviant.Run
{
    // Purchases at maintenance facilities; price counts are shared by every facility of the run. Palettes are not
    // sold: each weapon gets its palette at departure.
    // Colour inks are sold by the run's WeaponGrowth stall (the random draw was removed, user decision 2026-11-09).
    // Stat and slot upgrades only raise levels here until their effects are decided.
    public sealed class MaintenanceService
    {
        readonly RunEconomy economy;
        readonly RunStash stash;

        public MaintenanceService(RunEconomy economy, RunStash stash)
        { this.economy = economy; this.stash = stash; }

        public RunEconomy Economy => economy;
        public RunStash Stash => stash;
        public bool TryUpgradeStat(out string error) => Purchase(PriceTracks.StatUpgrade, stash.RaiseStatLevel, out error);
        public bool TryUpgradeSlot(out string error) => Purchase(PriceTracks.SlotUpgrade, stash.RaiseSlotLevel, out error);

        bool Purchase(string track, System.Action grant, out string error)
        {
            error = null;
            if (!economy.TryPurchase(track)) { error = "골드가 부족합니다."; return false; }
            grant();
            return true;
        }
    }
}
