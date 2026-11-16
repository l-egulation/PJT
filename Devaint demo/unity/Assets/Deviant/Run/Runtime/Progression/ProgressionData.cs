using System;

namespace Deviant.Run
{
    // JsonUtility-shaped copy of data/combat/roguelite_progression.json. JsonUtility reads a JSON null number as 0,
    // so a price is only usable when costStatus says it was decided; "pending" always disables the purchase.
    [Serializable] public sealed class ProgressionDropAmounts { public int normal, elite, boss; }
    [Serializable] public sealed class ProgressionCurrency
    {
        public string id, displayName, source, acquisition, dropAmountsStatus;
        public float dropChance;
        public ProgressionDropAmounts dropAmounts;
        public bool lossOnDeath;
    }
    [Serializable] public sealed class ProgressionStartLoadout
    {
        public string[] availableBlueprints;
        public int selectedCount, paletteCount, basePaletteSlots, maxPaletteSlots;
    }
    [Serializable] public sealed class ProgressionBuilding
    {
        public string id, name, station, theme;
        // Village colouring of a building without unlocks (chapter1_boss_first_clear | first_departure); empty keeps it
        // sketched. Unlock buildings colour when any of their unlocks is owned.
        public string colorWhen;
        // Village palette pieces needed before the building is usable (0: from the start).
        public int openAtPieces;
        // An open condition the building also waits for (first_run_settled for the supply office); empty: none.
        public string requiredClear;
    }
    // Tiered effects (palette slots, starting gold, attack percent, max health, bag slots, move speed percent, dash
    // charges) carry the tier's total, not an increment.
    // part: the colour ink part id of an ink_part_unlock.
    [Serializable] public sealed class ProgressionEffect
    {
        public string kind, scope, part;
        public string[] parts;
        public int value, weaponSlot, paletteSlot, amount, healthPercent;
    }
    [Serializable] public sealed class RetiredProgressionUnlock { public string id; public int refund; }
    // A workshop tree tab, in tab order (전투·기동·경제·선택·잉크 연구).
    [Serializable] public sealed class ProgressionBranch { public string id, name; }
    [Serializable] public sealed class ProgressionUnlock
    {
        // requiredClear is one of progressGates: first_run_settled, chapter1_boss_first_clear or chapter2_boss_first_clear.
        public string id, name, building, costStatus, requiredClear;
        // The workshop tree tab (a ProgressionBranch id) the unlock is drawn in.
        public string branch;
        // The workshop node the unlock is a level of ("공격력 I" holds attack tiers 1-4); empty: its own node.
        public string node;
        public int cost, maxPurchases;
        // Village palette pieces needed before the unlock can be bought (0: whenever its building is open).
        public int openAtPieces;
        public string[] prerequisites;
        public ProgressionEffect effect;
    }
    [Serializable] public sealed class ProgressionData
    {
        public int schemaVersion;
        public string status;
        // Open conditions in order; an unlock never opens on an earlier gate than its prerequisites.
        public string[] progressGates;
        // Village palette pieces in all (one per chapter-1 clear); 0 leaves the village open as before the pieces.
        public int palettePieces;
        public ProgressionCurrency currency;
        public ProgressionStartLoadout startLoadout;
        public ProgressionBuilding[] buildings;
        // Empty: the workshop draws every unlock in one tree.
        public ProgressionBranch[] branches;
        public ProgressionUnlock[] unlocks;
        public int revision;
        public RetiredProgressionUnlock[] retiredUnlocks;
    }

    public static class ProgressionIds
    {
        public const string PaletteSlots4 = "palette_slots_4", PaletteSlots5 = "palette_slots_5";
        public const string Wildcard1 = "palette_wildcard_1", Wildcard2 = "palette_wildcard_2";
        public const string DestroyedInkGold = "destroyed_ink_gold_20";
        // One starting-gold payout per run whichever tier is owned.
        public const string StartingGoldReceipt = "starting_gold";
        public const string FirstRunGate = "first_run_settled", BossGate = "chapter1_boss_first_clear", Chapter2Gate = "chapter2_boss_first_clear";
        public const string FirstDeparture = "first_departure";
        public const string PaletteSlotCount = "palette_slot_count", FirstSlotWildcard = "first_slot_wildcard";
        public const string StartingGoldBonus = "starting_gold_bonus", DestroyedInkGoldBonus = "destroyed_ink_gold_bonus";
        public const string AttackBonusPercent = "attack_bonus_percent", MaxHealthBonus = "max_health_bonus";
        // Shared bag slots in all (base 5 + 2 per tier), permanent walk/run speed percent, dash charges in all.
        public const string BagSlotCount = "bag_slot_count", MoveSpeedBonusPercent = "move_speed_bonus_percent", DashCharges = "dash_charges";
        // A colour ink part that stays out of drops, maintenance draws and the boss altar of profile runs until bought.
        public const string InkPartUnlock = "ink_part_unlock";
        public const string InkBundleUnlock = "ink_bundle_unlock", AugmentBundleUnlock = "augment_bundle_unlock";
        public const string SharedRerollCount = "shared_reroll_count", RecoveryExtraUses = "recovery_extra_uses";
        public const string ReviveCount = "revive_count", WeaponBreakGold = "weapon_break_gold", ShopExtraItems = "shop_extra_items";
        public const string StartingRewardCount = "starting_reward_count", KillGoldBonusPercent = "kill_gold_bonus_percent";
        // Departure ink reward: three unlocked, unregistered run inks; one joins the run codex (roguelite-progression.md).
        public const string StartingInkRewardCount = "starting_ink_reward_count";
        // The base dash holds one charge.
        public const int BaseDashCharges = 1;
    }
}
