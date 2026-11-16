using System;
using System.Collections.Generic;

namespace Deviant.Run
{
    // Data shapes of data/combat/chapter1_economy.json. Values are the first balance trial, not final numbers.
    [Serializable] public sealed class RewardRange { public int min, max; }
    [Serializable] public sealed class RewardTableData
    {
        public string role;
        public RewardRange gold;
        public float partsChance; public RewardRange parts;
        public float stampChance; public int stampCount;
        // Bag inks: random inks from the whole eligible pool plus guaranteed special-attack inks.
        public int inkCount, specialInkCount;
    }
    [Serializable] public sealed class PriceTrackData { public string id; public int start, step; }
    [Serializable] public sealed class RoomClearHealthData { public int combat, elite, boss; }
    [Serializable] public sealed class InkShopPriceData { public int grade, gold; }
    // Maintenance ink stall (user decision 2026-11-09): each facility shows offerCount inks of the listed grades at
    // fixed per-grade prices; saleGold is the fixed price the shop pays for one colour ink.
    [Serializable] public sealed class InkShopData { public int[] grades; public RewardRange offerCount; public InkShopPriceData[] gradePrices; public int saleGold; }
    [Serializable] public sealed class EconomyData
    {
        public RewardTableData[] rewards, chapter2Rewards;
        public int[] slotUpgradeCosts;
        public PriceTrackData[] prices;
        public float weaponStampShare;
        public int stampSwapCost;
        public int stampSaleGold;
        // Most stamps one stage may drop (user decision 2026-11-06).
        public int stageStampLimit;
        public RoomClearHealthData roomClearHealth;
        public string[] excludedFormInks;
        public InkShopData inkShop;
    }

    public enum StampKind { Weapon, Skill }

    // Stages that count their stamp drops separately: the chapter 1 random stage and the chapter 2 stage.
    public static class StampStages
    {
        public const string Chapter1 = "chapter1", Chapter2 = "chapter2";
    }

    public readonly struct RewardDrop
    {
        public readonly int Gold, Parts, WeaponStamps, SkillStamps, Inks, SpecialInks;
        public RewardDrop(int gold, int parts, int weaponStamps, int skillStamps, int inks = 0, int specialInks = 0)
        { Gold = gold; Parts = parts; WeaponStamps = weaponStamps; SkillStamps = skillStamps; Inks = inks; SpecialInks = specialInks; }
    }

    public static class PriceTracks
    {
        public const string StatUpgrade = "statUpgrade", SlotUpgrade = "slotUpgrade";
    }

    public static class RewardRoller
    {
        // Each reward kind is drawn independently; quantities and gold include both ends uniformly.
        public static RewardDrop Roll(RewardTableData table, float weaponStampShare, Random random)
        {
            if (table == null) return default;
            int gold = Range(table.gold, random);
            int parts = Chance(table.partsChance, random) ? Range(table.parts, random) : 0;
            int weapon = 0, skill = 0;
            if (Chance(table.stampChance, random))
                for (int i = 0; i < table.stampCount; i++) { if (random.NextDouble() < weaponStampShare) weapon++; else skill++; }
            return new RewardDrop(gold, parts, weapon, skill, Math.Max(0, table.inkCount), Math.Max(0, table.specialInkCount));
        }
        static bool Chance(float chance, Random random) => chance >= 1 || chance > 0 && random.NextDouble() < chance;
        static int Range(RewardRange range, Random random) => range == null ? 0 : random.Next(range.min, range.max + 1);
    }

    // Gold and maintenance purchase counts of one run. A new run starts from a new instance.
    public sealed class RunEconomy
    {
        readonly EconomyData data;
        readonly Dictionary<string, int> purchases = new Dictionary<string, int>();
        readonly Dictionary<string, int> stageStamps = new Dictionary<string, int>();
        public int Gold { get; private set; }
        static readonly int[] DefaultSlotUpgradeCosts = { 50, 100, 180, 350, 600 };
        public int SlotUpgradeCost(int level)
        {
            if(level<0 || level>5) throw new ArgumentOutOfRangeException(nameof(level));
            if(level==5) return 0;
            var costs=data.slotUpgradeCosts??DefaultSlotUpgradeCosts;
            if(costs.Length!=5 || Array.Exists(costs, cost=>cost<0)) throw new ArgumentException("슬롯 강화 비용 데이터 오류");
            return costs[level];
        }
        public RewardTableData Chapter2Table(string role)
        {
            return Array.Find(data.chapter2Rewards??data.rewards, table=>table.role==role);
        }
        public event Action Changed;

        public bool DebugSetGold(int amount)
        {
            if (amount < 0) return false;
            if (Gold != amount) { Gold = amount; Changed?.Invoke(); }
            return true;
        }

        public RunEconomy(EconomyData data) { this.data = data ?? throw new ArgumentNullException(nameof(data)); }

        public RewardTableData Table(string role) => Array.Find(data.rewards, table => table.role == role);
        public float WeaponStampShare => data.weaponStampShare;
        public int StampSwapCost => data.stampSwapCost;
        public int StampSaleGold => Math.Max(0,data.stampSaleGold);
        public InkShopData InkShop => data.inkShop;
        public int InkSaleGold => Math.Max(0, data.inkShop?.saleGold ?? 0);
        // Fixed stall price of one ink of this grade; throws for a grade the stall does not sell.
        public int InkShopPrice(int grade)
        {
            var row = data.inkShop?.gradePrices == null ? null : Array.Find(data.inkShop.gradePrices, p => p.grade == grade);
            return row?.gold ?? throw new ArgumentException("The ink stall does not sell grade " + grade);
        }
        public bool AllowsFormInk(string formId) => data.excludedFormInks == null || Array.IndexOf(data.excludedFormInks, formId) < 0;
        public int RoomClearHealth(string kind) => kind switch
        {
            "combat" => data.roomClearHealth?.combat ?? 0,
            "elite" => data.roomClearHealth?.elite ?? 0,
            "boss" => data.roomClearHealth?.boss ?? 0,
            _ => 0,
        };

        // Stamps count when they drop to the floor, picked up or not. The chest, elites and bosses each drop one while
        // the stage still has room; once the limit is reached no source drops another (user decision 2026-11-06).
        public int StageStampLimit => Math.Max(0, data.stageStampLimit);
        public int StageStampsDropped(string stage) => stageStamps.TryGetValue(stage, out int count) ? count : 0;
        public int StageStampsLeft(string stage) => Math.Max(0, StageStampLimit - StageStampsDropped(stage));

        public bool TryTakeStageStamp(string stage)
        {
            if (string.IsNullOrEmpty(stage)) throw new ArgumentException("A stage id is required", nameof(stage));
            if (StageStampsLeft(stage) <= 0) return false;
            stageStamps[stage] = StageStampsDropped(stage) + 1;
            return true;
        }

        public void Earn(int gold) { if (gold <= 0) return; Gold += gold; Changed?.Invoke(); }

        public int Purchases(string track) => purchases.TryGetValue(track, out int count) ? count : 0;

        // Price rises only after a successful purchase; every maintenance facility of the run shares the count.
        public int PriceOf(string track)
        {
            var price = Array.Find(data.prices, item => item.id == track) ?? throw new ArgumentException("Unknown price track: " + track);
            return price.start + price.step * Purchases(track);
        }

        public bool TryPurchase(string track)
        {
            int price = PriceOf(track);
            if (!TrySpend(price)) return false;
            purchases[track] = Purchases(track) + 1;
            Changed?.Invoke();
            return true;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
