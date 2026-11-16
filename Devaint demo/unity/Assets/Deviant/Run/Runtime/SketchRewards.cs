using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Run
{
    // A weapon shown on a reward or shop screen before it is taken. Acquiring it creates a new SketchWeapon.
    public sealed class SketchWeaponDraft
    {
        public string Form { get; }
        public string Skill { get; }
        public IReadOnlyList<string> Enhance { get; }
        public int Slots => Enhance.Count;
        public int EnhancedSlots => Enhance.Count(e => e != null);
        public string LayoutKey => SketchWeapon.Layout(Form, Skill, Enhance);
        public SketchWeaponDraft(string form, string skill, IReadOnlyList<string> enhance) { Form = form; Skill = skill; Enhance = enhance.ToArray(); }
    }

    // One of the three choices: either a weapon draft or an ink.
    public sealed class SketchCandidate
    {
        public SketchWeaponDraft Weapon { get; }
        public string Ink { get; }
        public bool IsInk => Ink != null;
        SketchCandidate(SketchWeaponDraft weapon, string ink) { Weapon = weapon; Ink = ink; }
        public static SketchCandidate Of(SketchWeaponDraft weapon) => new SketchCandidate(weapon ?? throw new ArgumentNullException(nameof(weapon)), null);
        public static SketchCandidate OfInk(string ink) => new SketchCandidate(null, ink ?? throw new ArgumentNullException(nameof(ink)));
    }

    public sealed class SketchShopPrice { public int Base, PerSlot, PerEnhancedSlot, Ink; }

    // Values of data/combat/weapon_ink_run_design.json (blueprints / rewards). Stage 3 and later share stage 3's row.
    public sealed class SketchRewardRules
    {
        public int OffersPerStage = 6;
        public int ChoicesPerOffer = 3;
        public int MinimumWeaponsPerStage = 3;
        // Candidate type roll for free slots (first playtest value): weapon 50 / ink 50.
        public float WeaponTypeWeight = 50f, InkTypeWeight = 50f;
        // User decision 2026-11-12: every weapon of one offer has the same slot count.
        public Dictionary<int, int[]> SlotCountsByOffer = new Dictionary<int, int[]>
        {
            [1] = new[] { 4, 4, 4, 4, 4, 4 },
            [2] = new[] { 5, 5, 5, 5, 5, 5 },
            [3] = new[] { 6, 6, 6, 6, 6, 6 },
        };
        // Per-slot chance (percent) that a slot becomes an enhanced slot. First playtest values awaiting confirmation;
        // stage 3 is undecided and refuses to roll.
        public Dictionary<int, float?> EnhancedSlotChance = new Dictionary<int, float?> { [1] = 20f, [2] = 25f, [3] = null };
        // Reward gauge (MR 371, first playtest values): one reward per filled gauge; the requirement is 100 for the
        // 1st-6th reward of a stage and grows by 50 every 6 rewards. A normal monster adds by its max-health tier
        // (stage 1: under 45 -> 4, 45-77 -> 5, 78+ -> 8), scaled by 1 + 0.5 x (stage - 1).
        public float GaugeBase = 100f, GaugeStepPerBlock = 50f;
        public float SmallBelow = 45f, LargeFrom = 78f;
        public float SmallGain = 4f, MediumGain = 5f, LargeGain = 8f;
        public float GaugeRequirement(int rewardsFilledThisStage) => GaugeBase + GaugeStepPerBlock * (Math.Max(0, rewardsFilledThisStage) / OffersPerStage);
        // Opening rewards (user decision 2026-11-13): the run's 1st reward needs 15 and the 2nd 48, each filled anew;
        // later rewards follow the per-stage requirement above. Both opening offers are an ink choice followed by
        // a weapon choice.
        public float[] OpeningRequirements = { 15f, 48f };
        public bool OpeningOffers = true;
        // From the run's 6th opened reward, every 4th is also an ink choice followed by a weapon choice (6, 10, 14, ...).
        public int DoubleOfferFrom = 6, DoubleOfferEvery = 4;
        public float RunRequirement(int rewardsFilledThisRun, int rewardsFilledThisStage) =>
            rewardsFilledThisRun < OpeningRequirements.Length ? OpeningRequirements[rewardsFilledThisRun] : GaugeRequirement(rewardsFilledThisStage);
        // runOffer: the run's normal rewards in open order, 1-based.
        public bool IsDoubleOffer(int runOffer) =>
            OpeningOffers && (runOffer == 1 || runOffer == 2) || DoubleOfferEvery > 0 && runOffer >= DoubleOfferFrom && (runOffer - DoubleOfferFrom) % DoubleOfferEvery == 0;
        public float GaugeGain(int stage, float maxHealth)
        {
            float tier = maxHealth < SmallBelow ? SmallGain : maxHealth < LargeFrom ? MediumGain : LargeGain;
            return tier * (1f + 0.5f * (Math.Max(1, stage) - 1));
        }
        public int ShopWeapons = 3, ShopInks = 3;
        public Dictionary<int, SketchShopPrice> ShopPrices = new Dictionary<int, SketchShopPrice>
        {
            [1] = new SketchShopPrice { Base = 200, PerSlot = 100, PerEnhancedSlot = 50, Ink = 220 }, // user decision 2026-11-12
            [2] = new SketchShopPrice { Base = 350, PerSlot = 175, PerEnhancedSlot = 90, Ink = 400 }, // stage 1 x income ratio 1.75
        };

        static int Row(int stage) { if (stage < 1) throw new ArgumentOutOfRangeException(nameof(stage)); return Math.Min(stage, 3); }
        public int[] OfferSlots(int stage) => SlotCountsByOffer[Row(stage)];
        // Offers 1-6 of a stage follow the table; the 7th and later use the stage maximum (user decision 2026-11-12).
        public int SlotsForOffer(int stage, int offer)
        {
            if (offer < 1) throw new ArgumentOutOfRangeException(nameof(offer));
            var slots = OfferSlots(stage);
            return offer <= slots.Length ? slots[offer - 1] : slots.Max();
        }
        public int[] StageSlotCounts(int stage) => OfferSlots(stage).Distinct().OrderBy(n => n).ToArray();
        public int BossSlots(int stage) => OfferSlots(stage).Max();
        public float EnhancedChance(int stage) =>
            EnhancedSlotChance.TryGetValue(Row(stage), out var c) && c.HasValue ? c.Value : throw new InvalidOperationException($"{stage}스테이지 강화 칸 확률이 미정입니다.");
        public SketchShopPrice Prices(int stage) =>
            ShopPrices.TryGetValue(Row(stage), out var p) && p != null ? p : throw new InvalidOperationException($"{stage}스테이지 상점 가격이 미정입니다.");
    }

    // Draws reward and shop candidates (docs/design/run/rewards.md). Pure and seedable: pass the run's Random.
    public sealed class SketchRewardPlanner
    {
        // form id -> skill ids of that form. Bare hands are never a reward.
        readonly IReadOnlyDictionary<string, string[]> forms;
        readonly string[] inkColors;
        public SketchRewardRules Rules { get; }

        public SketchRewardPlanner(IReadOnlyDictionary<string, string[]> forms, SketchRewardRules rules = null, IReadOnlyList<string> inkColors = null)
        {
            if (forms == null || forms.Count == 0 || forms.Any(f => f.Value == null || f.Value.Length == 0)) throw new ArgumentException("무기 종류마다 스킬이 필요합니다.", nameof(forms));
            if (forms.ContainsKey("unarmed")) throw new ArgumentException("맨손은 보상 후보가 아닙니다.", nameof(forms));
            this.forms = forms; Rules = rules ?? new SketchRewardRules();
            this.inkColors = (inkColors ?? new SketchArsenalRules().InkColors).ToArray();
        }

        // One weapon: form uniform, skill uniform within the form, then each slot rolls for enhancement;
        // the first enhanced slot picks a colour uniformly, a second one shares it, and slots after the cap stay colourless.
        public SketchWeaponDraft RollWeapon(int stage, int slots, Random random)
        {
            var formIds = forms.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            string form = formIds[random.Next(formIds.Length)];
            var skills = forms[form];
            string skill = skills[random.Next(skills.Length)];
            float chance = Rules.EnhancedChance(stage);
            int cap = new SketchArsenalRules().MaxEnhancedSlots;
            string color = null; int enhanced = 0;
            var enhance = new string[slots];
            for (int i = 0; i < slots; i++)
            {
                if (enhanced >= cap || random.NextDouble() * 100 >= chance) continue;
                color ??= inkColors[random.Next(inkColors.Length)];
                enhance[i] = color; enhanced++;
            }
            return new SketchWeaponDraft(form, skill, enhance);
        }

        // Rolls weapons that differ in form, skill or palette layout from the ones already on this screen.
        SketchWeaponDraft DistinctWeapon(int stage, int slots, ICollection<string> taken, Random random)
        {
            SketchWeaponDraft draft = null;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                draft = RollWeapon(stage, slots, random);
                if (!taken.Contains(draft.LayoutKey)) break;
            }
            taken.Add(draft.LayoutKey); // a catalog too small for three distinct layouts keeps the last roll
            return draft;
        }

        enum Kind { Weapon, Ink, Roll }

        // A normal kill-milestone offer. offer is 1..6; weaponsPicked counts weapons chosen from earlier normal offers
        // of this stage; supplyInks includes every eligible kind regardless of acquisition history.
        public SketchCandidate[] NormalOffer(int stage, int offer, int weaponsPicked, IReadOnlyList<string> supplyInks, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            int slots = Rules.SlotsForOffer(stage, offer);
            int remaining = Math.Max(1, Rules.OffersPerStage - offer + 1), missing = Math.Max(0, Rules.MinimumWeaponsPerStage - weaponsPicked);
            Kind[] kinds;
            if (offer == 1) kinds = new[] { Kind.Ink, Kind.Weapon, Kind.Roll };                 // user decision 2026-11-12
            else if (missing > 0 && remaining <= missing) kinds = new[] { Kind.Weapon, Kind.Weapon, Kind.Weapon };
            else if (missing == 0) kinds = new[] { Kind.Weapon, Kind.Ink, Kind.Roll };
            // From the run's 3rd reward every mixed offer shows both an ink and a weapon (user decision 2026-11-13).
            else kinds = new[] { Kind.Ink, Kind.Weapon, Kind.Roll };

            var inkPool = (supplyInks ?? Array.Empty<string>()).Distinct().ToList();
            var taken = new HashSet<string>();
            var result = new List<SketchCandidate>();
            foreach (var k in kinds)
            {
                var kind = k == Kind.Roll ? (random.NextDouble() * (Rules.WeaponTypeWeight + Rules.InkTypeWeight) < Rules.WeaponTypeWeight ? Kind.Weapon : Kind.Ink) : k;
                if (kind == Kind.Ink && inkPool.Count > 0)
                {
                    int i = random.Next(inkPool.Count);
                    result.Add(SketchCandidate.OfInk(inkPool[i])); inkPool.RemoveAt(i);
                }
                else result.Add(SketchCandidate.Of(DistinctWeapon(stage, slots, taken, random))); // no ink left -> distinct weapon
            }
            return result.ToArray();
        }

        // Locked chest (docs/design/world/stage-gimmicks.md, user decision 2026-11-13): choose an ink or a blueprint.
        // One ink and one weapon are always shown and the third place rolls weapon 50 / ink 50; weapons use
        // the stage's largest slot count like the boss reward. With no ink left every place is a distinct weapon.
        public SketchCandidate[] ChestOffer(int stage, IReadOnlyList<string> supplyInks, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            int slots = Rules.BossSlots(stage);
            var inkPool = (supplyInks ?? Array.Empty<string>()).Distinct().ToList();
            var taken = new HashSet<string>();
            var result = new List<SketchCandidate>();
            foreach (var k in new[] { Kind.Ink, Kind.Weapon, Kind.Roll })
            {
                var kind = k == Kind.Roll ? (random.NextDouble() * (Rules.WeaponTypeWeight + Rules.InkTypeWeight) < Rules.WeaponTypeWeight ? Kind.Weapon : Kind.Ink) : k;
                if (kind == Kind.Ink && inkPool.Count > 0)
                {
                    int i = random.Next(inkPool.Count);
                    result.Add(SketchCandidate.OfInk(inkPool[i])); inkPool.RemoveAt(i);
                }
                else result.Add(SketchCandidate.Of(DistinctWeapon(stage, slots, taken, random)));
            }
            return result.ToArray();
        }

        // Three different weapons of one slot count: the weapon half of a double reward.
        public SketchWeaponDraft[] WeaponOffer(int stage, int slots, Random random)
        {
            var taken = new HashSet<string>();
            return Enumerable.Range(0, Rules.ChoicesPerOffer).Select(_ => DistinctWeapon(stage, slots, taken, random)).ToArray();
        }

        // The ink half of a double reward: three distinct unlocked kinds; places without an eligible kind become different weapons.
        public SketchCandidate[] InkOffer(int stage, int slots, IReadOnlyList<string> supplyInks, Random random)
        {
            var pool = (supplyInks ?? Array.Empty<string>()).Distinct().ToList();
            var taken = new HashSet<string>(); var result = new List<SketchCandidate>();
            for (int n = 0; n < Rules.ChoicesPerOffer; n++)
            {
                if (pool.Count > 0) { int i = random.Next(pool.Count); result.Add(SketchCandidate.OfInk(pool[i])); pool.RemoveAt(i); }
                else result.Add(SketchCandidate.Of(DistinctWeapon(stage, slots, taken, random)));
            }
            return result.ToArray();
        }

        // Boss room: three weapons with the stage's largest slot count.
        public SketchWeaponDraft[] BossOffer(int stage, Random random)
        {
            int slots = Rules.BossSlots(stage); var taken = new HashSet<string>();
            return Enumerable.Range(0, Rules.ChoicesPerOffer).Select(_ => DistinctWeapon(stage, slots, taken, random)).ToArray();
        }

        // Shop weapons: each slot count drawn evenly among the stage's counts; no duplicate layout on one shelf.
        // extra: the permanent shop display unlock (roguelite-progression.md 상점 진열 확장) adds places to each shelf.
        public SketchWeaponDraft[] ShopWeapons(int stage, Random random, int extra = 0)
        {
            var taken = new HashSet<string>();
            return Enumerable.Range(0, Rules.ShopWeapons + Math.Max(0, extra)).Select(_ => ShopWeapon(stage, taken, random)).ToArray();
        }
        // One shelf weapon whose layout differs from the ones in taken (a shop reroll keeps sold places and redraws the rest).
        public SketchWeaponDraft ShopWeapon(int stage, ICollection<string> taken, Random random)
        {
            var counts = Rules.StageSlotCounts(stage);
            return DistinctWeapon(stage, counts[random.Next(counts.Length)], taken, random);
        }

        // Departure ink reward: up to three distinct unlocked ink kinds, including acquired ones.
        public string[] StartingInkOffer(IReadOnlyList<string> supplyInks, Random random)
        {
            var pool = (supplyInks ?? Array.Empty<string>()).Distinct().ToList(); var result = new List<string>();
            while (result.Count < Rules.ChoicesPerOffer && pool.Count > 0) { int i = random.Next(pool.Count); result.Add(pool[i]); pool.RemoveAt(i); }
            return result.ToArray();
        }

        public string[] ShopInks(IReadOnlyList<string> supplyInks, Random random, int extra = 0)
        {
            var pool = (supplyInks ?? Array.Empty<string>()).Distinct().ToList(); var result = new List<string>();
            while (result.Count < Rules.ShopInks + Math.Max(0, extra) && pool.Count > 0) { int i = random.Next(pool.Count); result.Add(pool[i]); }
            return result.ToArray();
        }

        // Departure bonus reward (roguelite-progression.md 출발 추가 보상, user decision 2026-11-13): three distinct weapons,
        // each slot count drawn evenly among the stage's counts like the shop. Independent of the normal offers.
        public SketchWeaponDraft[] StartingOffer(int stage, Random random)
        {
            var taken = new HashSet<string>();
            return Enumerable.Range(0, Rules.ChoicesPerOffer).Select(_ => ShopWeapon(stage, taken, random)).ToArray();
        }

        // Weapon price = base + slots x per-slot surcharge + enhanced slots x enhanced surcharge.
        public int WeaponPrice(int stage, SketchWeaponDraft weapon)
        {
            var p = Rules.Prices(stage);
            return p.Base + weapon.Slots * p.PerSlot + weapon.EnhancedSlots * p.PerEnhancedSlot;
        }
        public int InkPrice(int stage) => Rules.Prices(stage).Ink;
    }
}
