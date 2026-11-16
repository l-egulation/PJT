using System;
using System.Collections.Generic;

namespace Deviant.Run
{
    // Immutable departure settings; spending is owned by the RunSession, never by a scene or shop.
    public sealed class RunPerkSettings
    {
        public static readonly RunPerkSettings None = new RunPerkSettings();
        public int SharedRerolls { get; }
        public int ExtraRecoveryUses { get; }
        public int Revives { get; }
        public int ReviveHealthPercent { get; }
        public int WeaponBreakGold { get; }
        public int ExtraShopItems { get; }
        public int StartingRewards { get; }
        public int KillGoldBonusPercent { get; }
        public int StartingInkRewards { get; }
        public RunPerkSettings(int rerolls = 0, int recovery = 0, int revives = 0, int reviveHealthPercent = 50,
            int weaponBreakGold = 0, int shopItems = 0, int rewards = 0, int goldPercent = 0, int inkRewards = 0)
        {
            if (rerolls < 0 || recovery < 0 || revives < 0 || weaponBreakGold < 0 || shopItems < 0 || rewards < 0 || inkRewards < 0 ||
                goldPercent < 0 || goldPercent > 100 || reviveHealthPercent < 1 || reviveHealthPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(rerolls), "Invalid run perk settings.");
            SharedRerolls = rerolls; ExtraRecoveryUses = recovery; Revives = revives; ReviveHealthPercent = reviveHealthPercent;
            WeaponBreakGold = weaponBreakGold; ExtraShopItems = shopItems; StartingRewards = rewards; KillGoldBonusPercent = goldPercent;
            StartingInkRewards = inkRewards;
        }
    }

    public sealed class RunPerks
    {
        readonly RunEconomy economy;
        readonly Func<bool> active;
        readonly HashSet<Guid> brokenWeapons = new HashSet<Guid>();
        bool rerolling;
        int goldRemainder;
        public RunPerkSettings Settings { get; }
        public bool IsActive => active();
        public int RerollsRemaining { get; private set; }
        public int RevivesRemaining { get; private set; }
        public int StartingRewardsRemaining { get; private set; }
        public int StartingInkRewardsRemaining { get; private set; }
        public int Revision { get; private set; }
        public event Action Changed;

        internal RunPerks(RunPerkSettings settings, RunEconomy economy, Func<bool> active)
        {
            Settings = settings ?? RunPerkSettings.None; this.economy = economy; this.active = active;
            RerollsRemaining = Settings.SharedRerolls; RevivesRemaining = Settings.Revives;
            StartingRewardsRemaining = Settings.StartingRewards; StartingInkRewardsRemaining = Settings.StartingInkRewards;
        }
        void Publish() { Revision++; Changed?.Invoke(); }

        // Validate/draw first. Failed admissions and empty candidate pools never spend the shared charge.
        public bool TryReroll(Func<bool> draw)
        {
            if (!active() || rerolling || RerollsRemaining <= 0 || draw == null) return false;
            rerolling = true;
            try { if (!draw()) return false; RerollsRemaining--; Publish(); return true; }
            finally { rerolling = false; }
        }
        public bool TryRevive(float health, float maximum, out float restored)
        {
            restored = 0;
            if (!active() || RevivesRemaining <= 0 || float.IsNaN(health) || float.IsInfinity(health) || health > 0 ||
                float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0) return false;
            restored = maximum * Settings.ReviveHealthPercent / 100f;
            RevivesRemaining--; Publish(); return true;
        }
        public bool TryTakeStartingReward()
        {
            if (!active() || StartingRewardsRemaining <= 0) return false;
            StartingRewardsRemaining--; Publish(); return true;
        }
        public bool TryTakeStartingInkReward()
        {
            if (!active() || StartingInkRewardsRemaining <= 0) return false;
            StartingInkRewardsRemaining--; Publish(); return true;
        }
        // Keep sub-gold fractions across kills so +1% also matters on six-gold enemies.
        public int AwardKillGold(int baseGold)
        {
            if (!active() || economy == null || baseGold <= 0) return 0;
            int total = AdjustKillGold(baseGold);
            economy.Earn(total); return total;
        }
        // Used by native currency pickups, which already own their one-time wallet credit and visual.
        public int AdjustKillGold(int baseGold)
        {
            if (!active() || baseGold <= 0) return 0;
            long numerator = (long)baseGold * Settings.KillGoldBonusPercent + goldRemainder;
            int total = checked(baseGold + (int)(numerator / 100)); goldRemainder = (int)(numerator % 100);
            return total;
        }
        public int AwardWeaponBreak(Guid weapon)
        {
            if (!active() || economy == null || weapon == Guid.Empty || Settings.WeaponBreakGold <= 0 || !brokenWeapons.Add(weapon)) return 0;
            economy.Earn(Settings.WeaponBreakGold); Publish(); return Settings.WeaponBreakGold;
        }
    }
}
