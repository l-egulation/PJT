using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace Deviant.Run
{
    [Serializable] public sealed class RunInkDefinition
    {
        public string id, name, description, color, trigger, effect, condition, icon;
        public float baseValue, enhancedValue, duration, interval, cooldown;
    }
    [Serializable] public sealed class SketchStage
    {
        public int stage, weaponBase, perSlot, perEnhanced, inkPrice;
        public bool pricesKnown, enhancementKnown;
        public float enhancedChance;
        public int[] offerSlots;
    }
    [Serializable] public sealed class RunInkInvestment
    {
        public float baseContribution, enhancedContribution, redBaseDurationSeconds, controlBaseWindowSeconds,
            controlEnhancedWindowSeconds, controlFourCopiesWindowSeconds, controlCooldownAtOne, controlCooldownFloor, slowTwoCopiesWindowSeconds,
            slowCooldownAtOne, slowCooldownFloor, slowRateAtOne, slowRateAtTwo, slowRateCap, targetDurationCapSeconds;
        public void Validate()
        {
            var values = new[] { baseContribution, enhancedContribution, redBaseDurationSeconds, controlBaseWindowSeconds,
                controlEnhancedWindowSeconds, controlFourCopiesWindowSeconds, controlCooldownAtOne, controlCooldownFloor, slowTwoCopiesWindowSeconds,
                slowCooldownAtOne, slowCooldownFloor, slowRateAtOne, slowRateAtTwo, slowRateCap, targetDurationCapSeconds };
            if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v <= 0) || baseContribution != 1 || enhancedContribution != 1.5f ||
                controlBaseWindowSeconds >= controlEnhancedWindowSeconds || controlEnhancedWindowSeconds >= controlFourCopiesWindowSeconds || slowTwoCopiesWindowSeconds < controlBaseWindowSeconds ||
                slowRateAtTwo < slowRateAtOne || slowRateCap < slowRateAtTwo || slowRateCap >= 1)
                throw new ArgumentException("잉크 중복 수치 오류");
        }
        public float Copies(IEnumerable<SketchMount> mounts) => mounts.Sum(m => m.Color == m.Ink.color ? enhancedContribution : baseContribution);
        public float Window(string effect, float copies) => effect == "slow"
            ? controlBaseWindowSeconds + (Math.Min(copies, 2) - 1) * (slowTwoCopiesWindowSeconds - controlBaseWindowSeconds)
            : copies == enhancedContribution ? controlEnhancedWindowSeconds
            : controlBaseWindowSeconds + (copies - 1) * (controlFourCopiesWindowSeconds - controlBaseWindowSeconds) / 3;
        public float Cooldown(string effect, float copies) => effect == "slow"
            ? Math.Max(slowCooldownFloor, slowCooldownAtOne - (copies - 1) * (slowCooldownAtOne - slowCooldownFloor))
            : Math.Max(controlCooldownFloor, controlCooldownAtOne - (copies - 1));
        public float Slow(float copies) => copies <= 2 ? slowRateAtOne * copies : Math.Min(slowRateCap, slowRateAtTwo + (copies - 2) * (slowRateCap - slowRateAtTwo));
        public float StatusDuration(float copies) => Math.Min(targetDurationCapSeconds, copies);
    }
    [Serializable] public sealed class RunInkCatalog
    {
        public int schema, inkUsesPerAcquisition, maximumContributingInkCopies;
        public float damageUnitScale = 1, keyCarrierHp = 120;
        public float basicAttackSpeedCap, basicAttackSpeedDurationCap, maximumBasicSpeedScale;
        public RunInkInvestment investment;
        public float durabilityBase, durabilityPerSlot, equipmentChangeCooldownSeconds, moveCap, shieldCap, shieldGrace, shieldDecay, dashShieldGrace, dashShieldDecay, controlCap;
        public float gaugeBase, gaugeStep, smallBelow, largeFrom, smallGain, mediumGain, largeGain;
        // Opening and double rewards (user decision 2026-11-13).
        public float[] openingRequirements;
        public bool openingOffers;
        public int doubleFrom, doubleEvery;
        public RunInkDefinition[] inks;
        public SketchStage[] stages;
        public RunInkDefinition Find(string id)
        { foreach (var ink in inks) if (ink.id == id) return ink; return null; }
        public SketchStage Stage(int stage) => Array.Find(stages, s => s.stage == Math.Min(3, stage)) ?? throw new InvalidOperationException("스테이지 규칙이 없습니다.");
        public void Validate()
        {
            if (maximumContributingInkCopies != 6 || inkUsesPerAcquisition <= 0 || schema != 2 || inks == null || inks.Length != 39 || inks.Select(i => i.id).Distinct().Count() != 39 || !(durabilityBase > 0) || !(durabilityPerSlot > 0) || !(gaugeBase > 0) || gaugeStep < 0 || stages == null || stages.Length != 3)
                throw new ArgumentException("런 잉크 카탈로그 오류");
            if (investment == null) throw new ArgumentException("잉크 중복 수치가 없습니다.");
            investment.Validate();
            if (!(basicAttackSpeedCap > 0) || !(basicAttackSpeedDurationCap > 0) || !(maximumBasicSpeedScale >= 1))
                throw new ArgumentException("기본 공격 공속 제한 오류");
            foreach (var ink in inks)
                if (string.IsNullOrEmpty(ink.name) || string.IsNullOrEmpty(ink.description) || (float.IsNaN(ink.cooldown) || float.IsInfinity(ink.cooldown) || ink.cooldown < 0 || ink.color != "red" && ink.cooldown == 0) || !(ink.baseValue > 0) || ink.enhancedValue < ink.baseValue || ink.effect == "poison" || ink.condition == "poison")
                    throw new ArgumentException("잉크 수치 오류: " + ink.id);
        }
        internal SketchRewardRules RewardRules()
        {
            var rules = new SketchRewardRules { GaugeBase = gaugeBase, GaugeStepPerBlock = gaugeStep, SmallBelow = smallBelow,
                LargeFrom = largeFrom, SmallGain = smallGain, MediumGain = mediumGain, LargeGain = largeGain,
                OpeningRequirements = openingRequirements ?? Array.Empty<float>(), OpeningOffers = openingOffers,
                DoubleOfferFrom = doubleFrom, DoubleOfferEvery = doubleEvery };
            rules.SlotCountsByOffer.Clear(); rules.EnhancedSlotChance.Clear(); rules.ShopPrices.Clear();
            foreach (var stage in stages)
            {
                rules.SlotCountsByOffer[stage.stage] = stage.offerSlots;
                rules.EnhancedSlotChance[stage.stage] = stage.enhancementKnown ? stage.enhancedChance : (float?)null;
                if (stage.pricesKnown) rules.ShopPrices[stage.stage] = new SketchShopPrice
                    { Base = stage.weaponBase, PerSlot = stage.perSlot, PerEnhancedSlot = stage.perEnhanced, Ink = stage.inkPrice };
            }
            return rules;
        }
    }
    // Ink execution state decorates main's slot; ownership, fixed slots and layout remain in SketchArsenal.
    public sealed class SketchMount
    {
        internal readonly SketchSlot Native;
        readonly RunInkCatalog catalog;
        string loadedId;
        RunInkDefinition loadedInk;
        public Guid Id { get; } = Guid.NewGuid();
        public string Color { get => Native.Enhance ?? "neutral"; internal set => Native.Enhance = value == "neutral" ? null : value; }
        public RunInkDefinition Ink
        {
            get
            {
                if (Native.Ink != loadedId) { loadedId = Native.Ink; loadedInk = loadedId == null ? null : catalog.Find(loadedId); }
                return loadedInk;
            }
            internal set { Native.Ink = loadedId = value?.id; loadedInk = value; }
        }
        public bool Fixed => Native.Fixed;
        public float Cooldown { get; internal set; }
        public float ActiveRemaining { get; private set; }
        public float ActiveDuration { get; private set; }
        public float ReuseDuration { get; private set; }
        public void ActivateControl(float duration, float cooldown)
        { ActiveRemaining = ActiveDuration = duration; ReuseDuration = cooldown; Cooldown = cooldown; }
        internal void Tick(float delta, bool cooldownRuns)
        {
            float afterWindow = Math.Max(0, delta - ActiveRemaining);
            bool wasActive = ActiveRemaining > 0;
            ActiveRemaining = Math.Max(0, ActiveRemaining - delta);
            if (cooldownRuns || wasActive || Ink?.color == "purple") Cooldown = Math.Max(0, Cooldown - afterWindow);
        }
        internal void ResetExecution() { Cooldown = ActiveRemaining = ActiveDuration = ReuseDuration = 0; }
        public void Activate() { if (Ink == null) throw new InvalidOperationException("빈 잉크 슬롯입니다."); Cooldown = Ink.cooldown; }
        public float Value => Ink == null ? 0 : Color == Ink.color ? Ink.enhancedValue : Ink.baseValue;
        internal SketchMount(SketchSlot slot, RunInkCatalog catalog) { Native = slot; this.catalog = catalog; }
    }
    // Combat/UI view of main's weapon. It never owns a second arsenal, durability rule or reward state.
    public sealed class RunInkWeapon
    {
        internal SketchWeapon Native;
        readonly SketchEquipment owner;
        readonly Dictionary<SketchSlot, SketchMount> mounts = new Dictionary<SketchSlot, SketchMount>();
        IReadOnlyList<SketchSlot> slotSource;
        IReadOnlyList<SketchMount> slots;
        internal float PendingDamage;
        public SketchWeapon Model => Native;
        public Guid Id => Native.Id;
        public string Form => Native.Form;
        public string CustomName => Native.CustomName;
        public int Skill => owner.SkillIndex(Form, Native.Skill);
        public IReadOnlyList<SketchMount> Slots
        {
            get
            {
                if (!ReferenceEquals(slotSource, Native.Slots))
                {
                    slotSource = Native.Slots;
                    slots = Array.AsReadOnly(slotSource.Select(s =>
                    {
                        if (!mounts.TryGetValue(s, out var mount)) mounts[s] = mount = new SketchMount(s, owner.Catalog);
                        return mount;
                    }).ToArray());
                }
                return slots;
            }
        }
        public bool Confirmed => Native.Confirmed;
        public bool Destroyed => Native.Destroyed;
        public float Durability => Math.Max(0, Native.DurabilityLeft - PendingDamage);
        public float Maximum => Native.MaxDurability;
        public int SkillCapacity { get; private set; }
        public int SkillAvailable { get; private set; }
        public float SkillPeriod { get; private set; }
        public float SkillRemaining { get; private set; }
        public void RememberSkill(int capacity, int available, float period, float remaining)
        {
            SkillCapacity = capacity; SkillAvailable = available; SkillPeriod = period; SkillRemaining = remaining;
            owner.State.Arsenal.RememberSkillCooldown(Id, remaining);
        }
        internal RunInkWeapon(SketchWeapon native, SketchEquipment owner) { Native = native; this.owner = owner; }
        public string CompositionKey => Native.LayoutKey;
    }
    public sealed class SketchChoice
    {
        public RunInkWeapon Weapon { get; }
        public RunInkDefinition Ink { get; }
        public int Price { get; internal set; }
        public bool Sold { get; internal set; }
        // The shelf place this view was built from; a shop refresh replaces a weapon place with a new offer.
        internal object Source;
        internal SketchChoice(RunInkWeapon weapon) { Weapon = weapon; }
        internal SketchChoice(RunInkDefinition ink) { Ink = ink; }
    }
    // The 36-ink adapter uses main's SketchRunState for every slot/durability/reward/shop decision.
    public sealed class SketchEquipment
    {
        public RunInkCatalog Catalog { get; }
        public RunEconomy Economy { get; }
        public SketchRunState State { get; }
        readonly Dictionary<string, int[]> forms;
        readonly IDictionary<string, string[]> skillIds;
        readonly Dictionary<Guid, RunInkWeapon> byId = new Dictionary<Guid, RunInkWeapon>();
        readonly List<RunInkWeapon> weapons = new List<RunInkWeapon>();
        readonly Dictionary<string, SketchChoice[]> shops = new Dictionary<string, SketchChoice[]>();
        SketchCandidate[] choiceSource;
        SketchChoice[] choices;
        public int Revision => State.Revision;
        public int StageNumber => State.Stage;
        public int Kills { get; private set; }
        public int OffersCompleted => State.OffersOpened;
        public int NormalWeaponsAcquired => State.WeaponsPicked;
        public float ChangeCooldownRemaining => State.Arsenal.ChangeCooldownRemaining;
        public int Banked => State.Banked;
        public float GaugeFraction => State.GaugeFraction;
        public bool PendingReward => State.PendingOffer != null;
        public IReadOnlyList<RunInkWeapon> Weapons { get { foreach (var native in State.Arsenal.AllWeapons) Wrap(native); return weapons.AsReadOnly(); } }
        public IEnumerable<RunInkDefinition> Registered => Catalog.inks.Where(i => State.Arsenal.RemainingInkUses(i.id) > 0);
        public int RegisteredCount => Registered.Count();
        public int TotalInkUses(string id) => State.Arsenal.TotalInkUses(id);
        public int RemainingInkUses(string id) => State.Arsenal.RemainingInkUses(id);
        public int ReservedInkUses(string id) => State.Arsenal.ReservedInkUses(id);
        public int AvailableInkUses(string id) => State.Arsenal.AvailableInkUses(id);
        public event Action Changed;
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        public SketchEquipment(RunInkCatalog catalog, RunEconomy economy, int seed, IDictionary<string, int[]> allowedForms, IDictionary<string, string[]> skillIds = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); catalog.Validate();
            Economy = economy ?? throw new ArgumentNullException(nameof(economy)); this.skillIds = skillIds;
            forms = allowedForms.ToDictionary(p => p.Key, p => p.Value.ToArray());
            Require(forms.Count > 0 && forms.All(p => p.Value.Length > 0), "사용 가능한 무기·스킬이 없습니다.");
            var allowed = forms.ToDictionary(p => p.Key, p => p.Value.Select(i => SkillId(p.Key, i)).ToArray());
            var arsenal = new SketchArsenalRules { ChangeCooldownSeconds = catalog.equipmentChangeCooldownSeconds,
                InkUsesPerAcquisition = catalog.inkUsesPerAcquisition, DurabilityBase = catalog.durabilityBase, DurabilityPerSlot = catalog.durabilityPerSlot };
            State = new SketchRunState(allowed, catalog.inks.Select(i => i.id).ToArray(), id => catalog.Find(id)?.color, seed, catalog.RewardRules(), arsenal);
            State.IsStandaloneInk = id => {
                var ink = Catalog.Find(id);
                return ink != null && ink.color != "red" && ink.color != "blue" && ink.trigger != "status_condition";
            };
            State.IsUsableInk = id => {
                var ink = Catalog.Find(id);
                if (ink == null) return false;
                if (State.IsStandaloneInk(id)) return true;
                return Catalog.inks.Any(source => source.color == "purple" &&
                    (ink.trigger == "status_condition" || source.effect == ink.condition) && State.Arsenal.RemainingInkUses(source.id) > 0);
            };
            State.Changed += () => Changed?.Invoke();
        }
        string SkillId(string form, int index) => skillIds != null && skillIds.TryGetValue(form, out var names) ? names[index] : index.ToString(CultureInfo.InvariantCulture);
        internal int SkillIndex(string form, string id)
        {
            if (skillIds != null && skillIds.TryGetValue(form, out var names))
            { int index = Array.IndexOf(names, id); if (index >= 0) return index; }
            return int.Parse(id, CultureInfo.InvariantCulture);
        }
        RunInkWeapon Wrap(SketchWeapon native)
        {
            if (native == null) return null;
            if (!byId.TryGetValue(native.Id, out var weapon)) { byId[native.Id] = weapon = new RunInkWeapon(native, this); weapons.Add(weapon); }
            return weapon;
        }
        RunInkWeapon Preview(SketchWeaponDraft draft) => new RunInkWeapon(new SketchWeapon(draft.Form, draft.Skill, draft.Enhance.ToArray(), State.Arsenal.Rules.MaxDurability(draft.Slots)), this);
        void Adopt(RunInkWeapon preview, SketchWeapon native)
        {
            if (native == null) return;
            if (byId.TryGetValue(native.Id, out var old)) weapons.Remove(old);
            preview.Native = native; byId[native.Id] = preview; weapons.Add(preview);
        }
        public RunInkWeapon Equipped(int slot) => Wrap(State.Arsenal.Equipped(slot));
        public RunInkWeapon Find(Guid id)
        {
            if (byId.TryGetValue(id, out var weapon)) return weapon;
            return Wrap(State.Arsenal.AllWeapons.FirstOrDefault(w => w.Id == id));
        }
        public void BeginStage(int stage)
        {
            if (StageNumber == stage) return;
            Require(!State.MustOpenBeforeExit, "받아 둔 보상을 먼저 선택하세요."); Catalog.Stage(stage);
            State.StartStage(stage); Kills = 0; choiceSource = null; choices = null;
        }
        public bool Register(string id) { Require(Catalog.Find(id) != null, "없는 잉크입니다."); return State.Arsenal.RegisterInk(id); }
        public void Place(Guid weapon, int slot, string ink)
        { if (ink == null) State.Arsenal.ClearInk(weapon, slot); else State.Arsenal.PlaceInk(weapon, slot, ink); }
        public void Confirm(Guid id) => State.Arsenal.Confirm(id);
        public void Equip(Guid id, int slot) => State.Arsenal.Equip(id, slot);
        public void Store(int slot) => State.Arsenal.Unequip(slot);
        public void Rename(Guid id, string name) => State.Arsenal.Rename(id, name);
        public bool CanSwapOrder => State.Arsenal.CanSwapOrder;
        public void SwapOrder() => State.Arsenal.SwapOrder();
        public void Discard(Guid id) { var weapon = Find(id); State.Arsenal.Discard(id); weapon.Native.Destroyed = true; }
        public void Tick(float delta)
        {
            if (!(delta > 0)) return;
            State.Arsenal.Tick(delta);
            foreach (var w in weapons)
                foreach (var mount in w.Slots) mount.Tick(delta, w.Destroyed || State.Arsenal.IsEquipped(w.Native));
        }
        public void RecordDamage(Guid id, float actualHealthLoss)
        { var weapon = Find(id); if (weapon != null && !weapon.Destroyed && actualHealthLoss > 0) weapon.PendingDamage += actualHealthLoss; }
        // Preserve complete hit batches before main's arsenal decides destruction.
        public void FinishHit()
        {
            foreach (var weapon in weapons)
            {
                float damage = weapon.PendingDamage; weapon.PendingDamage = 0;
                if (damage > 0) State.Arsenal.ChargeDurability(weapon.Id, damage);
            }
        }
        public void RecordKill(string role, float maxHealth) { if (role != "boss") Kills++; State.RecordKill(role, maxHealth); }
        public void RecordKill(bool boss) { if (boss) State.OpenBossOffer(); else RecordKill("normal", 100); }
        public bool OpenReward() => State.OpenBanked();
        public IReadOnlyList<SketchChoice> RewardChoices()
        {
            var source = State.PendingOffer;
            if (source == null) { choiceSource = null; choices = null; return Array.Empty<SketchChoice>(); }
            if (!ReferenceEquals(source, choiceSource))
            { choiceSource = source; choices = source.Select(c => c.IsInk ? new SketchChoice(Catalog.Find(c.Ink)) : new SketchChoice(Preview(c.Weapon))).ToArray(); }
            return Array.AsReadOnly(choices);
        }
        public SketchChoice ChooseReward(int index)
        {
            var offers = RewardChoices(); Require(index >= 0 && index < offers.Count, "보상 후보 오류");
            var pick = choices[index]; var native = State.Choose(index);
            if (native != null) Adopt(pick.Weapon, native);
            choiceSource = null; choices = null; return pick;
        }
        public IReadOnlyList<SketchChoice> Shop(string key)
        {
            var stock = State.ShopStock(key); string cacheKey = StageNumber + ":" + key;
            if (!shops.TryGetValue(cacheKey, out var result)) shops[cacheKey] = result = new SketchChoice[stock.Count];
            for (int i = 0; i < stock.Count; i++)
            {
                var entry = stock[i]; var current = result[i];
                if (current == null || !ReferenceEquals(current.Source, entry) || current.Ink?.id != entry.Ink)
                    result[i] = current = entry.Weapon != null ? new SketchChoice(Preview(entry.Weapon)) : new SketchChoice(Catalog.Find(entry.Ink));
                current.Source = entry; current.Price = entry.Price; current.Sold = entry.Sold;
            }
            return Array.AsReadOnly(result);
        }
        // Shared-reroll draws (the caller spends RunPerks only when these return true).
        public bool RerollReward() => State.RerollPending();
        public bool RerollShop(string key, bool weapons) => State.RerollShop(key, weapons);
        public SketchChoice Buy(string key, int index)
        {
            var offers = Shop(key); Require(index >= 0 && index < offers.Count, "상품이 없습니다."); var chosen = offers[index];
            var native = State.Buy(key, index, Economy); if (native != null) Adopt(chosen.Weapon, native); chosen.Sold = true; return chosen;
        }
        public RunInkWeapon RollWeapon(bool boss = false)
        {
            int slots = boss ? State.Planner.Rules.BossSlots(StageNumber) : State.Planner.Rules.SlotsForOffer(StageNumber, OffersCompleted + 1);
            return Preview(State.Planner.RollWeapon(StageNumber, slots, new Random(Revision + 17)));
        }
        // Console editing bypasses normal admission, without replacing the main arsenal or changing its timer.
        public RunInkWeapon DebugConfigureWeapon(int slot, string form, int skill)
        {
            Require(slot >= 0 && slot < 2, "장착 칸 오류");
            if (form == null) { State.Arsenal.DebugEquip(null, slot); return null; }
            Require(forms.TryGetValue(form, out var skills) && skills.Contains(skill), "현재 런에서 사용할 수 없는 무기·스킬입니다.");
            var weapon = Equipped(slot);
            if (weapon == null)
            {
                var native = State.Arsenal.Acquire(form, SkillId(form, skill), Enumerable.Repeat<string>(null, State.Planner.Rules.BossSlots(StageNumber)).ToArray());
                State.Arsenal.Confirm(native.Id); State.Arsenal.DebugEquip(native, slot); return Wrap(native);
            }
            if (weapon.Form == form && weapon.Skill == skill) return weapon;
            weapon.Native.Form = form; weapon.Native.Skill = SkillId(form, skill); weapon.RememberSkill(0, 0, 0, 0);
            State.Arsenal.DebugTouch(); return weapon;
        }
        RunInkWeapon DebugWeapon(Guid id) { var w = Find(id); Require(w != null && !w.Destroyed, "편집할 스케치 무기가 없습니다."); return w; }
        SketchMount DebugMount(Guid id, int slot)
        { var w = DebugWeapon(id); Require(slot >= 0 && slot < w.Slots.Count, "없는 잉크 칸입니다."); return w.Slots[slot]; }
        public void DebugSetSlotColor(Guid id, int slot, string color)
        {
            Require(color == "neutral" || Catalog.inks.Any(i => i.color == color), "이 팔레트에서 사용할 수 없는 색상입니다.");
            DebugMount(id, slot).Color = color; State.Arsenal.DebugTouch();
        }
        public void DebugSetInk(Guid id, int slot, string ink)
        {
            var mount = DebugMount(id, slot); Require(ink == null || Catalog.Find(ink) != null, "현재 런 도감에 없는 잉크입니다.");
            if (mount.Ink?.id == ink) return;
            mount.Ink = ink == null ? null : Catalog.Find(ink); mount.Native.Fixed = DebugWeapon(id).Confirmed;
            mount.ResetExecution(); State.Arsenal.DebugTouch();
        }
        public void DebugResizePalette(Guid id, int count)
        {
            Require(count >= State.Arsenal.Rules.MinPaletteSlots && count <= State.Arsenal.Rules.MaxPaletteSlots, "팔레트 칸 수는 1~6칸입니다.");
            var weapon = DebugWeapon(id); if (weapon.Slots.Count == count) return;
            Require(weapon.Slots.Skip(count).All(m => m.Ink == null), "제거될 칸의 잉크를 먼저 비우세요.");
            var slots = weapon.Native.Slots.Take(count).ToList();
            while (slots.Count < count) slots.Add(new SketchSlot(null) { Fixed = weapon.Confirmed });
            weapon.Native.Slots = Array.AsReadOnly(slots.ToArray()); weapon.Native.MaxDurability = State.Arsenal.Rules.MaxDurability(count);
            weapon.Native.DurabilityUsed = Math.Min(weapon.Native.DurabilityUsed, weapon.Native.MaxDurability);
            State.Arsenal.DebugTouch();
        }
    }
}
