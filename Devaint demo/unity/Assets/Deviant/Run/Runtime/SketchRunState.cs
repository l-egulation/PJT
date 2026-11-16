using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Run
{
    // The run's sketch-weapon progress (docs/design/run/rewards.md): kill gauge per stage, the six normal offers,
    // the boss offer and the arsenal. Pure: the Unity binding reports kills and shows PendingOffer.
    public sealed class SketchShopOffer
    {
        public SketchWeaponDraft Weapon { get; }
        public string Ink { get; internal set; }
        public int Price { get; }
        public bool Sold { get; internal set; }
        // Empty stock marker (an empty catalog has no ink offers).
        public bool Empty { get; internal set; }
        internal SketchShopOffer(SketchWeaponDraft weapon, string ink, int price) { Weapon = weapon; Ink = ink; Price = price; }
    }

    public sealed class SketchRunState
    {
        public Func<string, bool> IsStandaloneInk { get; set; }
        public Func<string, bool> IsUsableInk { get; set; }
        public SketchArsenal Arsenal { get; }
        public SketchRewardPlanner Planner { get; }
        readonly Random random;
        readonly IReadOnlyList<string> inks;

        public int Stage { get; private set; } = 1;
        // Reward gauge (docs/design/run/rewards.md 보상 게이지, MR 371): progress toward the next banked reward,
        // carried over to the next stage.
        public float Gauge { get; private set; }
        // Rewards filled this stage (sets the requirement: 1st-6th 100, 7th-12th 150, ...).
        public int RewardsFilled { get; private set; }
        // Rewards filled this run: the first two use the opening requirements (15, 48; user decision 2026-11-13).
        public int RunRewardsFilled { get; private set; }
        // Filled rewards not opened yet. No limit; opened one at a time with G or from the bag.
        public int Banked { get; private set; }
        // Normal offers opened this stage, in open order (sets the weapon slot count and the guarantee).
        public int OffersOpened { get; private set; }
        // Normal rewards opened this run; its 6-reward blocks are the reward tier (ink rarity only).
        public int RunRewardsOpened { get; private set; }
        public int RewardTier => RunRewardsOpened / Planner.Rules.OffersPerStage + 1;
        // Weapons taken from this stage's normal offers; counts toward the three-weapon guarantee.
        public int WeaponsPicked { get; private set; }
        public bool BossOfferTaken { get; private set; }

        public SketchCandidate[] PendingOffer { get; private set; }
        public bool PendingIsBoss { get; private set; }
        // An event room reward (locked chest): chosen at once, never banked, and outside the normal weapon guarantee.
        public bool PendingIsEvent { get; private set; }
        // The departure bonus reward (also an event offer: never banked and outside the weapon guarantee).
        public bool PendingIsStart { get; private set; }
        // The departure ink reward (a start offer of inks only).
        public bool PendingIsStartInk { get; private set; }
        public int Revision { get; private set; }
        public event Action Changed;

        // inks: every ink id the run may offer; inkColor maps an id to red/yellow/green/blue/purple.
        public SketchRunState(IReadOnlyDictionary<string, string[]> forms, IReadOnlyList<string> inks, Func<string, string> inkColor, int seed,
            SketchRewardRules rules = null, SketchArsenalRules arsenalRules = null)
        {
            Arsenal = new SketchArsenal(inkColor, arsenalRules);
            Planner = new SketchRewardPlanner(forms, rules, Arsenal.Rules.InkColors);
            this.inks = (inks ?? Array.Empty<string>()).Where(i => Arsenal.Rules.InkColors.Contains(inkColor(i))).Distinct().ToArray();
            random = new Random(seed);
            Arsenal.Changed += Publish;
        }

        void Publish() { Revision++; Changed?.Invoke(); }

        // Inks a profile has not unlocked yet (roguelite-progression.md 잉크와 증강 묶음): never offered, sold or shown in a chest.
        readonly HashSet<string> lockedInks = new HashSet<string>();
        public void LockInks(IEnumerable<string> ids)
        {
            lockedInks.Clear();
            if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) lockedInks.Add(id);
            Publish();
        }
        public bool InkLocked(string id) => lockedInks.Contains(id);
        public IReadOnlyList<string> SupplyInks => inks.Where(i => !lockedInks.Contains(i)).ToArray();
        // Permanent shop display unlock: extra weapon and ink places on each new shelf (set at departure).
        int extraShopItems;
        public int ExtraShopItems { get => extraShopItems; set => extraShopItems = Math.Max(0, value); }
        public float Requirement => Planner.Rules.RunRequirement(RunRewardsFilled, RewardsFilled);
        // The open double reward still owes its weapon choice (shown right after the ink choice).
        public bool PendingDoubleWeapons => doubleWeaponSlots > 0;
        int doubleWeaponSlots;
        // Shape of the normal offer on screen, so a reroll redraws the same kind of choice.
        enum NormalShape { Mixed, Weapons, Inks }
        NormalShape pendingShape; int pendingSlots;
        // 0..1 progress of the gauge bar toward the next reward (no numbers are shown to the player).
        public float GaugeFraction => Math.Min(1f, Gauge / Requirement);

        // A new stage keeps the arsenal, codex and leftover gauge but restarts the requirement, offers and guarantee.
        // Unopened rewards never move to the next stage: the exit opens them first (MustOpenBeforeExit).
        public void StartStage(int stage)
        {
            if (stage < 1) throw new ArgumentOutOfRangeException(nameof(stage));
            if (stage == Stage && RewardsFilled == 0 && OffersOpened == 0 && Banked == 0) return;
            Stage = stage; RewardsFilled = 0; OffersOpened = 0; WeaponsPicked = 0; Banked = 0; BossOfferTaken = false;
            PendingOffer = null; PendingIsBoss = false; PendingIsEvent = false; PendingIsStart = false; PendingIsStartInk = false; bossQueued = false; chestQueued = false; doubleWeaponSlots = 0;
            BankFilled(); Publish();
        }

        // role: "normal", "elite" or "boss"; maxHealth picks the size tier. An elite banks one whole reward,
        // a boss adds nothing. Returns the number of rewards banked by this kill.
        public int RecordKill(string role, float maxHealth)
        {
            int before = Banked;
            if (role == "elite") { RewardsFilled++; RunRewardsFilled++; Banked++; }
            else if (role == "normal") { Gauge += Planner.Rules.GaugeGain(Stage, maxHealth); BankFilled(); }
            else return 0;
            Publish();
            return Banked - before;
        }

        // Overflow carries into the next reward; several filled at once are all banked.
        void BankFilled()
        {
            while (Gauge >= Requirement - 1e-4f) { Gauge = Math.Max(0, Gauge - Requirement); RewardsFilled++; RunRewardsFilled++; Banked++; }
        }

        // Console-only grant: bank one choice without inventing a kill or changing gauge/filled-reward thresholds.
        // Opening and choosing it still use the normal offer order, weapon guarantee and run reward tier.
        public void DebugAddReward()
        {
            if (Banked == int.MaxValue) throw new InvalidOperationException("받아 둔 보상 수가 최대입니다.");
            Banked++; Publish();
        }

        // Opens the next banked reward: the three candidates are drawn at this moment, counted in open order.
        public bool OpenBanked()
        {
            if (PendingOffer != null || Banked <= 0) return false;
            Banked--; OffersOpened++; RunRewardsOpened++;
            // In run open order: the 1st, 2nd and every 4th from the 6th are an ink choice and then
            // a weapon choice; the rest show an ink and a weapon in one choice.
            var rules = Planner.Rules; int slots = rules.SlotsForOffer(Stage, OffersOpened);
            pendingSlots = slots;
            if (rules.IsDoubleOffer(RunRewardsOpened)) { pendingShape = NormalShape.Inks; doubleWeaponSlots = slots; }
            else pendingShape = NormalShape.Mixed;
            PendingOffer = DrawNormal();
            PendingIsBoss = false; PendingIsEvent = false;
            Publish();
            return true;
        }

        // The stage exit makes the player choose every unopened reward first.
        public bool MustOpenBeforeExit => Banked > 0 || PendingOffer != null;

        // The boss room's three weapons. A normal offer still waiting is shown first.
        public bool OpenBossOffer()
        {
            if (BossOfferTaken) return false;
            BossOfferTaken = true;
            if (PendingOffer == null)
            {
                PendingOffer = Planner.BossOffer(Stage, random).Select(SketchCandidate.Of).ToArray();
                PendingIsBoss = true; PendingIsEvent = false;
            }
            else bossQueued = true;
            Publish();
            return true;
        }
        bool bossQueued;

        // The locked chest's ink-or-blueprint choice, opened with the key (docs/design/world/stage-gimmicks.md). An offer
        // already on screen is chosen first; the chest's candidates are drawn when it is shown.
        public bool OpenChestOffer()
        {
            if (chestQueued) return false;
            if (PendingOffer == null) ShowChestOffer(); else chestQueued = true;
            Publish();
            return true;
        }
        bool chestQueued;
        void ShowChestOffer() { PendingOffer = EnsureUsableInk(Planner.ChestOffer(Stage, SupplyInks, random), SupplyInks); PendingIsBoss = false; PendingIsEvent = true; PendingIsStart = false; PendingIsStartInk = false; }

        // The departure bonus reward: three weapons chosen at once, shown only when no other offer is on screen.
        public bool OpenStartingOffer()
        {
            if (PendingOffer != null) return false;
            PendingOffer = Planner.StartingOffer(Stage, random).Select(SketchCandidate.Of).ToArray();
            PendingIsBoss = false; PendingIsEvent = true; PendingIsStart = true; PendingIsStartInk = false;
            Publish();
            return true;
        }

        // The departure ink reward: distinct unlocked kinds, including acquired inks; an empty pool returns false.
        public bool OpenStartingInkOffer()
        {
            if (PendingOffer != null) return false;
            var inks = Planner.StartingInkOffer(SupplyInks, random);
            if (inks.Length == 0) return false;
            PendingOffer = inks.Select(SketchCandidate.OfInk).ToArray();
            PendingIsBoss = false; PendingIsEvent = true; PendingIsStart = true; PendingIsStartInk = true;
            Publish();
            return true;
        }

        // A shared reroll (roguelite-progression.md 공용 리롤) redraws the offer on screen with its own rule. It succeeds
        // only when at least one candidate changes; the caller spends the run budget on success only.
        public bool RerollPending()
        {
            var offer = PendingOffer;
            if (offer == null) return false;
            var before = new HashSet<string>(offer.Select(Key));
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var next = PendingIsStartInk ? Planner.StartingInkOffer(SupplyInks, random).Select(SketchCandidate.OfInk).ToArray()
                    : PendingIsStart ? Planner.StartingOffer(Stage, random).Select(SketchCandidate.Of).ToArray()
                    : PendingIsBoss ? Planner.BossOffer(Stage, random).Select(SketchCandidate.Of).ToArray()
                    : PendingIsEvent ? EnsureUsableInk(Planner.ChestOffer(Stage, SupplyInks, random), SupplyInks)
                    : DrawNormal();
                if (before.SetEquals(next.Select(Key))) continue;
                PendingOffer = next; Publish();
                return true;
            }
            return false;
        }
        static string Key(SketchCandidate c) => c.IsInk ? "ink:" + c.Ink : "weapon:" + c.Weapon.LayoutKey;
        SketchCandidate[] EnsureUsableInk(SketchCandidate[] offer, IReadOnlyList<string> supply)
        {
            if (IsUsableInk == null || !offer.Any(c => c.IsInk) || offer.Any(c => c.IsInk && IsUsableInk(c.Ink))) return offer;
            var pool = supply.Where(IsUsableInk).Except(offer.Where(c => c.IsInk).Select(c => c.Ink)).ToArray();
            if (pool.Length > 0) offer[Array.FindIndex(offer, c => c.IsInk)] = SketchCandidate.OfInk(pool[random.Next(pool.Length)]);
            return offer;
        }
        SketchCandidate[] DrawNormal()
        {
            var supply = SupplyInks;
            bool firstInk = RunRewardsOpened <= 2 && pendingShape == NormalShape.Inks;
            if (firstInk && IsStandaloneInk != null) supply = supply.Where(IsStandaloneInk).ToArray();
            var offer = pendingShape switch
            {
                NormalShape.Weapons => Planner.WeaponOffer(Stage, pendingSlots, random).Select(SketchCandidate.Of).ToArray(),
                NormalShape.Inks => Planner.InkOffer(Stage, pendingSlots, supply, random),
                _ => Planner.NormalOffer(Stage, OffersOpened, WeaponsPicked, supply, random),
            };
            return firstInk ? offer : EnsureUsableInk(offer, supply);
        }

        // ---- Shop (docs/design/run/rewards.md) ----

        readonly Dictionary<string, List<SketchShopOffer>> shops = new Dictionary<string, List<SketchShopOffer>>();

        // A facility's shelf is rolled on its first visit and kept for the stage: three weapons from the stage's slot
        // counts (no duplicate layout) and three independently drawn inks (duplicates allowed). Revisits show the same unsold goods.
        public IReadOnlyList<SketchShopOffer> ShopStock(string facility)
        {
            if (string.IsNullOrEmpty(facility)) throw new ArgumentException("상점 오류", nameof(facility));
            string key = Stage + ":" + facility;
            if (!shops.TryGetValue(key, out var stock))
            {
                stock = new List<SketchShopOffer>();
                foreach (var w in Planner.ShopWeapons(Stage, random, ExtraShopItems)) stock.Add(new SketchShopOffer(w, null, Planner.WeaponPrice(Stage, w)));
                foreach (var ink in Planner.ShopInks(SupplyInks, random, ExtraShopItems)) stock.Add(new SketchShopOffer(null, ink, Planner.InkPrice(Stage)));
                shops[key] = stock;
            }
            return stock;
        }

        // Shop refresh (roguelite-progression.md 공용 리롤): redraws the unsold goods of one shelf (weapons or inks) and
        // keeps sold places sold. True only when something changed, so a refresh with nothing to swap spends nothing.
        public bool RerollShop(string facility, bool weapons)
        {
            ShopStock(facility);
            var stock = shops[Stage + ":" + facility];
            bool changed = false;
            if (weapons)
            {
                var taken = new HashSet<string>(stock.Where(o => o.Weapon != null).Select(o => o.Weapon.LayoutKey));
                for (int i = 0; i < stock.Count; i++)
                {
                    var offer = stock[i];
                    if (offer.Weapon == null || offer.Sold) continue;
                    var draft = Planner.ShopWeapon(Stage, taken, random);
                    if (draft.LayoutKey == offer.Weapon.LayoutKey) continue;
                    stock[i] = new SketchShopOffer(draft, null, Planner.WeaponPrice(Stage, draft)); changed = true;
                }
            }
            else
                foreach (var offer in stock)
                {
                    if (offer.Weapon != null || offer.Sold || offer.Ink == null) continue;
                    var pool = SupplyInks.Where(i => i != offer.Ink).ToList();
                    if (pool.Count == 0) continue;
                    offer.Ink = pool[random.Next(pool.Count)]; changed = true;
                }
            if (changed) Publish();
            return changed;
        }

        // Pays gold and takes the goods: a weapon arrives unconfirmed, an ink is registered. A bought place stays sold.
        public SketchWeapon Buy(string facility, int index, RunEconomy economy)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            var stock = ShopStock(facility);
            if (index < 0 || index >= stock.Count) throw new InvalidOperationException("상품이 없습니다.");
            var offer = stock[index];
            if (offer.Sold) throw new InvalidOperationException("품절된 상품입니다.");
            if (!economy.TrySpend(offer.Price)) throw new InvalidOperationException("골드가 부족합니다.");
            offer.Sold = true;
            SketchWeapon weapon = null;
            if (offer.Ink != null) Arsenal.RegisterInk(offer.Ink); else weapon = Arsenal.Acquire(offer.Weapon);
            Publish();
            return weapon;
        }

        // Takes one candidate; the offer cannot be skipped. Returns the new unconfirmed weapon, or null for an ink.
        public SketchWeapon Choose(int index)
        {
            var offer = PendingOffer ?? throw new InvalidOperationException("선택할 보상이 없습니다.");
            if (index < 0 || index >= offer.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var pick = offer[index];
            SketchWeapon weapon = null;
            if (pick.IsInk) Arsenal.RegisterInk(pick.Ink);
            else
            {
                weapon = Arsenal.Acquire(pick.Weapon);
                if (!PendingIsBoss && !PendingIsEvent) WeaponsPicked++;
            }
            PendingOffer = null; PendingIsBoss = false; PendingIsEvent = false; PendingIsStart = false; PendingIsStartInk = false;
            if (doubleWeaponSlots > 0)
            {
                // The double reward's second choice follows at once; it counts toward the normal weapon guarantee.
                pendingShape = NormalShape.Weapons; pendingSlots = doubleWeaponSlots; doubleWeaponSlots = 0;
                PendingOffer = DrawNormal();
            }
            else if (chestQueued) { chestQueued = false; ShowChestOffer(); }
            else if (bossQueued)
            {
                bossQueued = false;
                PendingOffer = Planner.BossOffer(Stage, random).Select(SketchCandidate.Of).ToArray();
                PendingIsBoss = true;
            }
            Publish();
            return weapon;
        }
    }
}
