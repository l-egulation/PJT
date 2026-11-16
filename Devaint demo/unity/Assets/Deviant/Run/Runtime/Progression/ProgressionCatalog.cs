using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Run
{
    // Validated, read-only view of the roguelite progression rules. Construction fails on any inconsistent data.
    public sealed class ProgressionCatalog
    {
        static readonly HashSet<string> Kinds = new HashSet<string>
        { ProgressionIds.PaletteSlotCount, ProgressionIds.FirstSlotWildcard, ProgressionIds.StartingGoldBonus, ProgressionIds.DestroyedInkGoldBonus,
          ProgressionIds.AttackBonusPercent, ProgressionIds.MaxHealthBonus, ProgressionIds.BagSlotCount, ProgressionIds.MoveSpeedBonusPercent, ProgressionIds.DashCharges,
          ProgressionIds.InkPartUnlock, ProgressionIds.InkBundleUnlock, ProgressionIds.AugmentBundleUnlock,
          ProgressionIds.SharedRerollCount, ProgressionIds.RecoveryExtraUses, ProgressionIds.ReviveCount,
          ProgressionIds.WeaponBreakGold, ProgressionIds.ShopExtraItems, ProgressionIds.StartingRewardCount, ProgressionIds.StartingInkRewardCount, ProgressionIds.KillGoldBonusPercent };
        static readonly HashSet<string> PricedStatuses = new HashSet<string> { "trial-1", "trial-2", "trial-3", "confirmed" };
        public static readonly string[] Gates = { ProgressionIds.FirstRunGate, ProgressionIds.BossGate, ProgressionIds.Chapter2Gate };

        public ProgressionData Data { get; }
        readonly Dictionary<string, ProgressionUnlock> unlocks;
        public IReadOnlyList<ProgressionUnlock> Unlocks { get; }
        public IReadOnlyList<ProgressionBuilding> Buildings { get; }
        public int BaseSlots => Data.startLoadout.basePaletteSlots;
        public int MaxSlots => Data.startLoadout.maxPaletteSlots;
        public int PalettePieces => Data.palettePieces;
        public ProgressionBuilding BuildingAt(string station) => Buildings.FirstOrDefault(b => b.station == station);
        public ProgressionBuilding Building(string id) => Buildings.FirstOrDefault(b => b.id == id);

        public ProgressionCatalog(ProgressionData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            var errors = Validate(data);
            if (errors.Count > 0) throw new ArgumentException("Roguelite progression data is invalid: " + string.Join("; ", errors));
            unlocks = data.unlocks.ToDictionary(u => u.id);
            Unlocks = Array.AsReadOnly(data.unlocks);
            Buildings = Array.AsReadOnly(data.buildings);
        }

        public ProgressionUnlock Find(string id) => id != null && unlocks.TryGetValue(id, out var unlock) ? unlock : null;
        public bool Contains(string id) => Find(id) != null;
        public bool PriceKnown(ProgressionUnlock unlock) => unlock != null && PricedStatuses.Contains(unlock.costStatus);

        // The highest owned tier of an effect, or 0. Tiers carry their total, so tiers never add up.
        public int Highest(string kind, Func<string, bool> owns, Func<ProgressionEffect, int> field) =>
            Unlocks.Where(u => u.effect.kind == kind && owns(u.id)).Select(u => field(u.effect)).DefaultIfEmpty(0).Max();

        // The same effect's next tier: the unlock whose prerequisite is this one. Null for the last tier.
        public ProgressionUnlock NextTier(ProgressionUnlock unlock) =>
            unlock == null ? null : Unlocks.FirstOrDefault(u => u.effect.kind == unlock.effect.kind && u.prerequisites != null && u.prerequisites.Contains(unlock.id));
        // The first tier of the line this unlock belongs to.
        public ProgressionUnlock FirstTier(ProgressionUnlock unlock)
        {
            while (unlock?.prerequisites != null)
            {
                var lower = unlock.prerequisites.Select(Find).FirstOrDefault(p => p != null && p.effect.kind == unlock.effect.kind);
                if (lower == null) break;
                unlock = lower;
            }
            return unlock;
        }

        // Colour ink parts that profile runs leave out until their unlock is owned.
        public IReadOnlyList<string> LockedInkParts(Func<string, bool> owns) =>
            Unlocks.Where(u => !owns(u.id) && (u.effect.kind == ProgressionIds.InkPartUnlock || u.effect.kind == ProgressionIds.InkBundleUnlock))
                .SelectMany(u => u.effect.kind == ProgressionIds.InkPartUnlock ? new[] { u.effect.part } : u.effect.parts).Distinct().ToArray();

        public IReadOnlyList<string> LockedAugments(Func<string, bool> owns) =>
            Unlocks.Where(u => u.effect.kind == ProgressionIds.AugmentBundleUnlock && !owns(u.id)).SelectMany(u => u.effect.parts).Distinct().ToArray();

        // Amount granted for one kill of the given role ("normal"/"elite"/"boss"); 0 while the table is undecided.
        public int PigmentFor(string role)
        {
            var c = Data.currency;
            if (c.dropAmounts == null || !PricedStatuses.Contains(c.dropAmountsStatus)) return 0;
            switch (role) { case "normal": return c.dropAmounts.normal; case "elite": return c.dropAmounts.elite; case "boss": return c.dropAmounts.boss; default: return 0; }
        }

        public static List<string> Validate(ProgressionData data)
        {
            var errors = new List<string>();
            void Need(bool ok, string message) { if (!ok) errors.Add(message); }
            Need(data.schemaVersion == 1, "schemaVersion must be 1");
            Need(data.currency != null && !string.IsNullOrEmpty(data.currency.id), "currency id is required");
            Need(data.progressGates != null && data.progressGates.SequenceEqual(Gates), "progress gates must be first_run_settled, chapter1_boss_first_clear then chapter2_boss_first_clear");
            var load = data.startLoadout;
            Need(load != null && load.selectedCount == 2 && load.paletteCount == 2, "start loadout must select 2 blueprints with 2 palettes");
            Need(load != null && load.basePaletteSlots >= 3 && load.maxPaletteSlots <= 5 && load.basePaletteSlots <= load.maxPaletteSlots, "palette slots must stay within 3..5");
            Need(load?.availableBlueprints != null && load.availableBlueprints.Distinct().Count() == load.availableBlueprints.Length && load.availableBlueprints.Length >= 2, "blueprints must be distinct");
            if (data.currency?.dropAmounts != null)
                Need(data.currency.dropAmounts.normal >= 0 && data.currency.dropAmounts.elite >= 0 && data.currency.dropAmounts.boss >= 0, "drop amounts must be nonnegative");
            Need(data.currency == null || data.currency.dropChance >= 0 && data.currency.dropChance <= 1, "drop chance must be within 0..1");
            var buildings = new HashSet<string>(data.buildings?.Select(b => b.id) ?? Enumerable.Empty<string>());
            Need(data.buildings != null && buildings.Count == data.buildings.Length, "building ids must be unique");
            var unlockBuildings = new HashSet<string>(data.unlocks?.Select(u => u.building) ?? Enumerable.Empty<string>());
            foreach (var b in data.buildings ?? Array.Empty<ProgressionBuilding>())
                if (!string.IsNullOrEmpty(b.colorWhen))
                    Need(!unlockBuildings.Contains(b.id) && (b.colorWhen == ProgressionIds.BossGate || b.colorWhen == ProgressionIds.FirstDeparture), b.id + ": invalid colorWhen " + b.colorWhen);
            Need(data.palettePieces >= 0, "palettePieces must be nonnegative");
            foreach (var b in data.buildings ?? Array.Empty<ProgressionBuilding>())
                Need(b.openAtPieces >= 0 && b.openAtPieces <= data.palettePieces && (string.IsNullOrEmpty(b.requiredClear) || Gates.Contains(b.requiredClear)),
                    b.id + ": openAtPieces beyond the pieces or unknown requiredClear " + b.requiredClear);
            foreach (var u in data.unlocks ?? Array.Empty<ProgressionUnlock>())
                Need(u.openAtPieces >= 0 && u.openAtPieces <= data.palettePieces, u.id + ": openAtPieces beyond the pieces");
            if (data.unlocks == null || data.unlocks.Length == 0) { errors.Add("unlocks are required"); return errors; }
            var ids = data.unlocks.Select(u => u.id).ToArray();
            Need(ids.All(id => !string.IsNullOrEmpty(id)) && ids.Distinct().Count() == ids.Length, "unlock ids must be unique and non-empty");
            var known = new HashSet<string>(ids);
            var branches = new HashSet<string>(data.branches?.Select(b => b.id) ?? Enumerable.Empty<string>());
            Need(data.branches == null || branches.Count == data.branches.Length && branches.All(id => !string.IsNullOrEmpty(id)), "branch ids must be unique and non-empty");
            foreach (var u in data.unlocks)
            {
                Need(buildings.Contains(u.building), u.id + ": unknown building " + u.building);
                Need(branches.Count == 0 || branches.Contains(u.branch), u.id + ": unknown branch " + u.branch);
                Need(u.maxPurchases == 1, u.id + ": each unlock is bought once");
                Need(u.effect != null && Kinds.Contains(u.effect.kind), u.id + ": unknown effect");
                Need(u.costStatus == "pending" || PricedStatuses.Contains(u.costStatus), u.id + ": unknown cost status " + u.costStatus);
                Need(!PricedStatuses.Contains(u.costStatus) || u.cost > 0, u.id + ": a decided price must be positive");
                Need(u.cost >= 0, u.id + ": negative price");
                int gate = Array.IndexOf(Gates, u.requiredClear);
                Need(gate >= 0, u.id + ": unknown requiredClear " + u.requiredClear);
                foreach (var pre in u.prerequisites ?? Array.Empty<string>())
                {
                    Need(known.Contains(pre) && pre != u.id, u.id + ": missing prerequisite " + pre);
                    var parent = data.unlocks.FirstOrDefault(x => x.id == pre);
                    if (parent != null) Need(Array.IndexOf(Gates, parent.requiredClear) <= gate, u.id + ": opens before its prerequisite " + pre);
                }
                if (u.effect?.kind == ProgressionIds.PaletteSlotCount) Need(u.effect.value > load.basePaletteSlots && u.effect.value <= load.maxPaletteSlots, u.id + ": slot count out of range");
                if (u.effect?.kind == ProgressionIds.FirstSlotWildcard) Need((u.effect.weaponSlot == 1 || u.effect.weaponSlot == 2) && u.effect.paletteSlot == 1, u.id + ": wildcard must target a weapon's first slot");
                if (u.effect?.kind == ProgressionIds.StartingGoldBonus || u.effect?.kind == ProgressionIds.DestroyedInkGoldBonus) Need(u.effect.amount > 0, u.id + ": bonus must be positive");
                if (u.effect?.kind == ProgressionIds.AttackBonusPercent || u.effect?.kind == ProgressionIds.MaxHealthBonus || u.effect?.kind == ProgressionIds.MoveSpeedBonusPercent)
                    Need(u.effect.value > 0, u.id + ": bonus must be positive");
                // A bag tier carries the run's whole bag; the export checks it against the base bag of temporary-inks.json.
                if (u.effect?.kind == ProgressionIds.BagSlotCount) Need(u.effect.value > 0, u.id + ": bag slots must be positive");
                if (u.effect?.kind == ProgressionIds.InkPartUnlock) Need(!string.IsNullOrEmpty(u.effect.part), u.id + ": an ink unlock names its part");
                if (u.effect?.kind == ProgressionIds.DashCharges) Need(u.effect.value > ProgressionIds.BaseDashCharges, u.id + ": dash charges must exceed the base dash");
                if (u.effect != null && IsPerk(u.effect.kind))
                {
                    Need(u.effect.value > 0, u.id + ": perk total must be positive");
                    if (u.effect.kind == ProgressionIds.ReviveCount) Need(u.effect.healthPercent > 0 && u.effect.healthPercent <= 100, u.id + ": invalid revival health");
                    if (u.effect.kind == ProgressionIds.KillGoldBonusPercent) Need(u.effect.value <= 100, u.id + ": invalid gold bonus");
                }
                if (u.effect?.kind == ProgressionIds.InkBundleUnlock || u.effect?.kind == ProgressionIds.AugmentBundleUnlock)
                    Need(u.effect.parts != null && u.effect.parts.Length >= 2 && u.effect.parts.All(p => !string.IsNullOrEmpty(p)) &&
                        u.effect.parts.Distinct().Count() == u.effect.parts.Length, u.id + ": a bundle needs distinct members");
            }
            // Prerequisite cycles would make an unlock impossible to buy.
            var state = new Dictionary<string, int>();
            bool Cyclic(string id)
            {
                if (state.TryGetValue(id, out int s)) return s == 1;
                state[id] = 1;
                var u = data.unlocks.FirstOrDefault(x => x.id == id);
                bool cyclic = u?.prerequisites != null && u.prerequisites.Where(known.Contains).Any(Cyclic);
                state[id] = 2; return cyclic;
            }
            foreach (var id in ids) if (Cyclic(id)) { errors.Add("prerequisite cycle through " + id); break; }
            return errors;
        }

        static bool IsPerk(string kind) => kind == ProgressionIds.SharedRerollCount || kind == ProgressionIds.RecoveryExtraUses ||
            kind == ProgressionIds.ReviveCount || kind == ProgressionIds.WeaponBreakGold || kind == ProgressionIds.ShopExtraItems ||
            kind == ProgressionIds.StartingRewardCount || kind == ProgressionIds.StartingInkRewardCount || kind == ProgressionIds.KillGoldBonusPercent;
    }
}
