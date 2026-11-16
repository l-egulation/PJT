using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace Deviant.Run
{
    // Rules of docs/design/equipment/sketch-weapons.md that the arsenal enforces. Defaults mirror
    // data/combat/weapon_ink_run_design.json (inventory / blueprints); callers may pass the parsed values instead.
    public sealed class SketchArsenalRules
    {
        public float ChangeCooldownSeconds = 5f;
        public int InkUsesPerAcquisition = 10;
        public int MinPaletteSlots = 1;
        public int MaxPaletteSlots = 6;
        public int MaxEnhancedSlots = 2;
        public string[] InkColors = { "red", "yellow", "green", "blue", "purple" };
        // Maximum durability = base + perSlot * palette slots (user decision 2026-11-12, first playtest values).
        public float DurabilityBase = 2000f;
        public float DurabilityPerSlot = 1500f;
        public float MaxDurability(int slots) => DurabilityBase + DurabilityPerSlot * slots;
    }

    public enum SketchWeaponState { Unconfirmed, Stored, Equipped, Destroyed }

    // One palette cell. Enhance is null for a colourless cell, otherwise the colour it strengthens.
    // Any ink colour fits any cell; the enhanced value applies only when the ink colour matches Enhance.
    public sealed class SketchSlot
    {
        public string Enhance { get; internal set; }
        public string Ink { get; internal set; }
        public bool Fixed { get; internal set; }
        internal SketchSlot(string enhance) { Enhance = enhance; }
    }

    // A sketch weapon (= blueprint). Form, skill and palette layout are fixed when it drops.
    public sealed class SketchWeapon
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Form { get; internal set; }
        public string Skill { get; internal set; }
        public string CustomName { get; internal set; }
        public IReadOnlyList<SketchSlot> Slots { get; internal set; }
        public bool Confirmed { get; internal set; }
        public bool Destroyed { get; internal set; }
        // Remaining skill cooldown in combat seconds. Advances only while the weapon sits in slot A or B.
        public float SkillCooldown { get; internal set; }
        public float MaxDurability { get; internal set; }
        // Actual enemy HP this weapon has removed, including delayed damage while it sits in the bag.
        public float DurabilityUsed { get; internal set; }
        public float DurabilityLeft => Math.Max(0, MaxDurability - DurabilityUsed);
        public int EnhancedSlots => Slots.Count(s => s.Enhance != null);
        internal SketchWeapon(string form, string skill, string[] enhance, float maxDurability)
        {
            Form = form; Skill = skill; MaxDurability = maxDurability;
            Slots = Array.AsReadOnly(enhance.Select(e => new SketchSlot(e)).ToArray());
        }
        public string LayoutKey => Layout(Form, Skill, Slots.Select(s => s.Enhance));
        // Duplicate key: form, skill and the count of slots per enhance colour (colourless included); order is ignored.
        public static string Layout(string form, string skill, IEnumerable<string> enhance) =>
            form + "|" + skill + "|" + string.Join(",", enhance.GroupBy(e => e ?? "-").OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count()));
    }

    // Run-scoped sketch weapons, ink codex and the A/B combat slots. Pure model: the caller advances combat time
    // with Tick and stops ticking while the inventory is open (the bag pauses combat and every combat timer).
    public sealed class SketchArsenal
    {
        public const int SlotA = 0, SlotB = 1;
        public SketchArsenalRules Rules { get; }
        readonly Func<string, string> inkColor;
        readonly List<SketchWeapon> weapons = new List<SketchWeapon>();
        readonly SketchWeapon[] equipped = new SketchWeapon[2];
        readonly List<string> codex = new List<string>();
        readonly Dictionary<string, int> inkUses = new Dictionary<string, int>();
        readonly Dictionary<string, int> totalInkUses = new Dictionary<string, int>();
        SketchWeapon lastAttackWeapon;

        public int Revision { get; private set; }
        public event Action Changed;
        // Raised when a weapon breaks; stored = true when it broke in the bag (the player gets a notice).
        public event Action<SketchWeapon, bool> WeaponDestroyed;

        // One cooldown shared by equip, unequip and replace. Changes made while the bag is open are free; the first
        // combat tick after them (the bag closed) starts the cooldown, and it blocks those changes until it runs out.
        public float ChangeCooldownRemaining { get; private set; }
        public bool ChangeCooldownPending { get; private set; }
        // The A/B pair before the first change of the current bag visit. Ending with the same weapons (in either
        // order, X being free) is no change and starts no cooldown.
        SketchWeapon pendingFromA, pendingFromB;
        public bool CanChangeEquipment => ChangeCooldownRemaining <= 0;
        string CooldownReason => $"무기 변경 대기 중입니다. ({Math.Ceiling(ChangeCooldownRemaining)}초)";
        // The slot whose weapon is shown as current. Keys stay bound to slots: slot A always answers A/Q, B answers S/W.
        public int ActiveSlot { get; private set; }
        public IReadOnlyList<string> Codex => codex.AsReadOnly();
        public IReadOnlyList<SketchWeapon> Weapons => weapons.Where(w => !w.Destroyed).ToList().AsReadOnly();
        public IEnumerable<SketchWeapon> Bag => weapons.Where(w => !w.Destroyed && !IsEquipped(w));

        // inkColor maps an ink id to its colour (red/yellow/green/blue/purple).
        public SketchArsenal(Func<string, string> inkColor, SketchArsenalRules rules = null)
        {
            this.inkColor = inkColor ?? throw new ArgumentNullException(nameof(inkColor));
            Rules = rules ?? new SketchArsenalRules();
        }

        internal IReadOnlyList<SketchWeapon> AllWeapons => weapons;
        internal void DebugTouch() => Publish();
        internal void DebugEquip(SketchWeapon weapon, int slot)
        {
            RequireSlot(slot); equipped[slot] = weapon;
            if (weapon == null) SelectCurrentAfterLoss(slot);
            Publish();
        }

        void Publish() { Revision++; Changed?.Invoke(); }
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static void RequireSlot(int slot) { if (slot != SlotA && slot != SlotB) throw new ArgumentOutOfRangeException(nameof(slot)); }

        public SketchWeapon Equipped(int slot) { RequireSlot(slot); return equipped[slot]; }

        // Two bare hands may always take a first weapon (recovery after breaks); the running cooldown is kept.
        public bool EquipLockedByCooldown => ChangeCooldownRemaining > 0 && (equipped[SlotA] != null || equipped[SlotB] != null);

        // X swaps what A and B hold (a weapon and bare hands, or two weapons) once at least one weapon is equipped,
        // at any time and without the change cooldown. Durability and skill cooldowns stay with their weapons,
        // and the current weapon stays current.
        public bool CanSwapOrder => equipped[SlotA] != null || equipped[SlotB] != null;
        public void SwapOrder()
        {
            Require(CanSwapOrder, "교체할 무기가 없습니다.");
            (equipped[SlotA], equipped[SlotB]) = (equipped[SlotB], equipped[SlotA]);
            ActiveSlot = 1 - ActiveSlot;
            Publish();
        }
        public bool IsEquipped(SketchWeapon weapon) => weapon != null && (equipped[0] == weapon || equipped[1] == weapon);
        public int SlotOf(SketchWeapon weapon) => equipped[0] == weapon ? SlotA : equipped[1] == weapon ? SlotB : -1;
        public SketchWeaponState StateOf(SketchWeapon weapon) =>
            weapon.Destroyed ? SketchWeaponState.Destroyed : IsEquipped(weapon) ? SketchWeaponState.Equipped
            : weapon.Confirmed ? SketchWeaponState.Stored : SketchWeaponState.Unconfirmed;
        SketchWeapon Find(Guid id) { var w = weapons.Find(x => x.Id == id && !x.Destroyed); Require(w != null, "보유한 무기가 아닙니다."); return w; }

        public void Rename(Guid id, string name)
        {
            var weapon = Find(id);
            Require(weapon.Confirmed, "제작한 무기만 이름을 변경할 수 있습니다.");
            name = name?.Trim();
            Require(!string.IsNullOrEmpty(name) && !name.Any(char.IsControl) &&
                StringInfo.ParseCombiningCharacters(name).Length <= 20, "무기 이름은 1~20자로 입력하세요.");
            if (weapon.CustomName == name) return;
            weapon.CustomName = name;
            Publish();
        }

        // ---- Acquisition ----

        // Adds a rewarded or bought weapon to the bag, unconfirmed. enhance holds one entry per slot (null = colourless).
        public SketchWeapon Acquire(string form, string skill, IReadOnlyList<string> enhance)
        {
            Require(!string.IsNullOrEmpty(form) && !string.IsNullOrEmpty(skill), "무기 종류와 스킬이 필요합니다.");
            Require(enhance != null && enhance.Count >= Rules.MinPaletteSlots && enhance.Count <= Rules.MaxPaletteSlots, "팔레트 칸 수 오류");
            var colors = enhance.Where(e => e != null).ToArray();
            Require(colors.Length <= Rules.MaxEnhancedSlots, "강화 칸 수 오류");
            Require(colors.All(c => Rules.InkColors.Contains(c)), "강화 색 오류");
            Require(colors.Distinct().Count() <= 1, "강화 칸이 여러 개면 같은 색입니다.");
            var weapon = new SketchWeapon(form, skill, enhance.ToArray(), Rules.MaxDurability(enhance.Count));
            weapons.Add(weapon); Publish(); return weapon;
        }

        public SketchWeapon Acquire(SketchWeaponDraft draft) => Acquire(draft?.Form, draft?.Skill, draft?.Enhance);

        // Every acquisition adds the configured number of applications, including repeated acquisitions.
        public bool RegisterInk(string inkId)
        {
            Require(!string.IsNullOrEmpty(inkId), "잉크 오류");
            Require(Rules.InkColors.Contains(inkColor(inkId)), "잉크 색 오류");
            Require(Rules.InkUsesPerAcquisition > 0, "잉크 지급 횟수 오류");
            int total = checked(RemainingInkUses(inkId) + Rules.InkUsesPerAcquisition);
            int acquired = checked(TotalInkUses(inkId) + Rules.InkUsesPerAcquisition);
            if (!codex.Contains(inkId)) codex.Add(inkId);
            inkUses[inkId] = total; totalInkUses[inkId] = acquired; Publish(); return true;
        }
        public bool Registered(string inkId) => codex.Contains(inkId);
        // Remaining includes draft reservations; only confirmation spends it. Spent entries stay in the run history.
        public int TotalInkUses(string inkId) => inkId != null && totalInkUses.TryGetValue(inkId, out var count) ? count : 0;
        public int RemainingInkUses(string inkId) => inkId != null && inkUses.TryGetValue(inkId, out var count) ? count : 0;
        public int ReservedInkUses(string inkId) => inkId == null ? 0 : weapons.Where(w => !w.Destroyed && !w.Confirmed)
            .Sum(w => w.Slots.Count(s => s.Ink == inkId));
        public int AvailableInkUses(string inkId) => Math.Max(0, RemainingInkUses(inkId) - ReservedInkUses(inkId));
        public IEnumerable<string> AvailableInks => codex.Where(i => RemainingInkUses(i) > 0);

        // ---- Palette ----

        // Before the first confirmation inks are placed tentatively and may be moved or removed freely; the draft
        // survives closing the bag. Confirmation locks every slot, including empty slots.
        // Each tentative slot reserves one application across all drafts; clearing or discarding releases it.
        public void PlaceInk(Guid weaponId, int index, string inkId)
        {
            var weapon = Find(weaponId);
            Require(index >= 0 && index < weapon.Slots.Count, "슬롯 오류");
            Require(Registered(inkId), "도감에 등록된 잉크만 부여할 수 있습니다.");
            var slot = weapon.Slots[index];
            Require(!slot.Fixed, "고정된 잉크는 무기가 파괴될 때까지 유지됩니다.");
            Require(!weapon.Confirmed, "확정한 무기는 빈 슬롯에도 잉크를 추가할 수 없습니다.");
            if (slot.Ink == inkId) return;
            Require(AvailableInkUses(inkId) > 0, "이 잉크의 남은 부여 횟수가 모두 소모되었거나 예약 중입니다.");
            slot.Ink = inkId;
            Publish();
        }

        // Removes a tentative ink before confirmation.
        public void ClearInk(Guid weaponId, int index)
        {
            var weapon = Find(weaponId);
            Require(index >= 0 && index < weapon.Slots.Count, "슬롯 오류");
            var slot = weapon.Slots[index];
            Require(!slot.Fixed && !weapon.Confirmed, "확정한 잉크는 뺄 수 없습니다.");
            if (slot.Ink == null) return;
            slot.Ink = null; Publish();
        }

        // Fixes every slot, including empty slots. An empty palette may be confirmed too; its basic attack and skill work right away.
        public void Confirm(Guid weaponId)
        {
            var weapon = Find(weaponId);
            Require(!weapon.Confirmed, "이미 확정한 무기입니다.");
            var costs = weapon.Slots.Where(s => s.Ink != null).GroupBy(s => s.Ink)
                .ToDictionary(g => g.Key, g => g.Count());
            // Validate the entire composition before changing either the counts or the fixed slots.
            foreach (var cost in costs)
                Require(RemainingInkUses(cost.Key) >= ReservedInkUses(cost.Key), "잉크 부여 횟수가 부족합니다.");
            foreach (var cost in costs) inkUses[cost.Key] -= cost.Value;
            weapon.Confirmed = true;
            foreach (var s in weapon.Slots) s.Fixed = true;
            Publish();
        }

        // True when the slot's ink uses its enhanced value (ink colour equals the slot's enhance colour).
        public bool UsesEnhancedValue(SketchSlot slot) => slot?.Ink != null && slot.Enhance != null && inkColor(slot.Ink) == slot.Enhance;

        // ---- Equipment ----

        // Equip and replace wait for the shared change cooldown (except a first weapon into two bare hands);
        // inside one bag visit they repeat freely.
        public bool CanEquip(Guid weaponId, int slot, out string reason)
        {
            RequireSlot(slot); reason = null;
            var weapon = weapons.Find(x => x.Id == weaponId && !x.Destroyed);
            if (weapon == null) reason = "보유한 무기가 아닙니다.";
            else if (!weapon.Confirmed) reason = "팔레트 구성을 먼저 확정하세요.";
            else if (IsEquipped(weapon)) reason = "이미 장착한 무기입니다.";
            else if (EquipLockedByCooldown) reason = CooldownReason;
            return reason == null;
        }

        // Puts a confirmed bag weapon into A or B. Replacing an occupied slot sends the old weapon to the bag
        // (keeping its durability and frozen skill cooldown). The change cooldown starts once combat time resumes.
        public void Equip(Guid weaponId, int slot)
        {
            Require(CanEquip(weaponId, slot, out var reason), reason);
            var weapon = Find(weaponId);
            var previous = equipped[slot];
            MarkChange();
            equipped[slot] = weapon;
            if (previous != null)
            {
                if (lastAttackWeapon == previous) lastAttackWeapon = null;
            }
            if (equipped[ActiveSlot] == null) ActiveSlot = slot;
            Publish();
        }

        void MarkChange()
        {
            if (ChangeCooldownPending) return;
            ChangeCooldownPending = true; pendingFromA = equipped[SlotA]; pendingFromB = equipped[SlotB];
        }
        bool PendingPairUnchanged => equipped[SlotA] == pendingFromA && equipped[SlotB] == pendingFromB ||
            equipped[SlotA] == pendingFromB && equipped[SlotB] == pendingFromA;

        // Manual unequip returns the weapon to the bag; the slot attacks bare-handed. Shares the change cooldown.
        public void Unequip(int slot)
        {
            RequireSlot(slot);
            var weapon = equipped[slot];
            Require(weapon != null, "빈 칸입니다.");
            Require(CanChangeEquipment, CooldownReason);
            MarkChange();
            equipped[slot] = null;
            if (lastAttackWeapon == weapon) lastAttackWeapon = null;
            SelectCurrentAfterLoss(slot);
            Publish();
        }

        // Throws away one bag weapon (confirmed or not). Equipped weapons must be unequipped first.
        public void Discard(Guid weaponId)
        {
            var weapon = Find(weaponId);
            Require(!IsEquipped(weapon), "장착한 무기는 먼저 해제하세요.");
            weapons.Remove(weapon); Publish();
        }

        // Breaks a weapon (durability reached). An equipped weapon leaves its slot; if it was current, the other slot's
        // weapon becomes current without moving keys. The shared swap cooldown keeps running unchanged.
        public void Destroy(Guid weaponId)
        {
            var weapon = Find(weaponId);
            int slot = SlotOf(weapon);
            weapon.Destroyed = true;
            if (lastAttackWeapon == weapon) lastAttackWeapon = null;
            if (slot >= 0) { equipped[slot] = null; SelectCurrentAfterLoss(slot); }
            Publish();
            WeaponDestroyed?.Invoke(weapon, slot < 0);
        }

        void SelectCurrentAfterLoss(int slot)
        {
            if (ActiveSlot == slot && equipped[1 - slot] != null) ActiveSlot = 1 - slot;
        }

        // ---- Durability ----

        // Charges the actual HP a hit removed (overkill excluded) to its source weapon, wherever that weapon is.
        // Several targets of one hit are summed by the caller before charging. Reaching the maximum breaks the weapon
        // after the hit's damage was applied. Damage from effects of an already destroyed weapon is not charged.
        // Returns true when this charge broke the weapon.
        public bool ChargeDurability(Guid weaponId, float hpRemoved)
        {
            var weapon = weapons.Find(x => x.Id == weaponId);
            if (weapon == null || weapon.Destroyed || !(hpRemoved > 0)) return false;
            weapon.DurabilityUsed = Math.Min(weapon.MaxDurability, weapon.DurabilityUsed + hpRemoved);
            if (weapon.DurabilityUsed < weapon.MaxDurability) { Publish(); return false; }
            Destroy(weaponId);
            return true;
        }

        // ---- Combat ----

        // Advances combat time: the change cooldown and the skill cooldowns of equipped weapons only.
        // The bag stops combat time, so the first tick after a bag change is the moment the bag closed:
        // it starts the change cooldown, and the step's combat time counts against it.
        public void Tick(float seconds)
        {
            if (!(seconds > 0)) return;
            if (ChangeCooldownPending)
            {
                ChangeCooldownPending = false;
                if (!PendingPairUnchanged) ChangeCooldownRemaining = Rules.ChangeCooldownSeconds;
                pendingFromA = pendingFromB = null;
                Publish();
            }
            // Combat time stood still while the bag was open, so this whole step already follows the close.
            ChangeCooldownRemaining = Remaining(ChangeCooldownRemaining, seconds);
            foreach (var w in equipped) if (w != null) w.SkillCooldown = Remaining(w.SkillCooldown, seconds);
        }

        // Float steps (9.9 s + 0.1 s) can leave a microscopic remainder; treat it as finished.
        const float TimeEpsilon = 1e-4f;
        static float Remaining(float value, float seconds) { float left = value - seconds; return left <= TimeEpsilon ? 0 : left; }

        // The weapon a slot's basic-attack key uses; null means the bare-hand basic attack.
        public SketchWeapon BasicAttackWeapon(int slot) => Equipped(slot);

        // Skill keys of an empty slot do nothing; a cooling skill cannot start.
        public bool CanUseSkill(int slot) { var w = Equipped(slot); return w != null && w.SkillCooldown <= 0; }
        // The combat skill pool is the live value while a weapon is equipped; the binding stores what was left when
        // the weapon leaves its slot so the bag keeps it frozen.
        public void RememberSkillCooldown(Guid weaponId, float seconds)
        {
            var w = weapons.Find(x => x.Id == weaponId);
            if (w != null) w.SkillCooldown = Math.Max(0, seconds);
        }

        public void StartSkillCooldown(int slot, float seconds)
        {
            var w = Equipped(slot); Require(w != null, "빈 칸에는 스킬이 없습니다.");
            w.SkillCooldown = Math.Max(0, seconds); Publish();
        }

        // Call when an attack really starts from a slot. Returns true when it starts with a different equipped weapon
        // than the previous attack (the ink switching trigger). Failed inputs, bag exchanges and break-driven display
        // changes never call this, so they never count as switching.
        public bool RecordAttackStart(int slot)
        {
            var w = Equipped(slot);
            ActiveSlot = slot;
            if (w == null) return false; // bare hands are not a weapon and leave the switching history as it was
            bool switched = lastAttackWeapon != null && lastAttackWeapon != w;
            lastAttackWeapon = w;
            return switched;
        }
    }
}
