using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace Deviant.Inventory
{
    public enum InkKind { Form, Stat, Special }

    // Content describes an ink; ownership always belongs to a separate physical instance.
    public sealed class InkDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public InkKind Kind { get; }
        public string FormId { get; }
        public string WeaponCategory { get; }
        public int Value { get; }
        public InkDefinition(string id, string name, string description, InkKind kind,
            int value = 0, string formId = null, string weaponCategory = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Ink ID/name required.");
            if (!Enum.IsDefined(typeof(InkKind), kind)) throw new ArgumentException("Invalid ink kind.");
            if (kind == InkKind.Stat && (value < 1 || value > 4)) throw new ArgumentException("Unknown stat ink.");
            if (kind == InkKind.Special && (value < 1 || value > 3)) throw new ArgumentException("Unknown special ink.");
            if (kind == InkKind.Form && (string.IsNullOrWhiteSpace(formId) || string.IsNullOrWhiteSpace(weaponCategory)))
                throw new ArgumentException("Form ink needs a form ID and weapon category.");
            Id = id; Name = name; Description = description ?? ""; Kind = kind;
            Value = value; FormId = formId; WeaponCategory = weaponCategory;
        }
    }

    public sealed class InkInstance
    {
        static long nextId;
        public long InstanceId { get; } = Interlocked.Increment(ref nextId);
        public InkDefinition Definition { get; }
        public InkInstance(InkDefinition definition) { Definition = definition ?? throw new ArgumentNullException(nameof(definition)); }
    }

    public sealed class EquipmentLayout
    {
        readonly InkInstance[][] slots;
        public int WeaponCount => slots.Length;
        public int StatSlots { get; }
        public int SlotCount => StatSlots + 2;
        internal EquipmentLayout(InkInstance[][] source, int statSlots)
        { slots = Copy(source); StatSlots = statSlots; }
        public InkInstance Get(int weapon, int slot) => slots[weapon][slot];
        internal static InkInstance[][] Copy(InkInstance[][] source)
        {
            var result = new InkInstance[source.Length][];
            for (int i = 0; i < source.Length; i++) result[i] = (InkInstance[])source[i].Clone();
            return result;
        }
    }

    public interface IInkEquipmentPort
    {
        // Null means success. A rejection must not change combat state.
        string Apply(EquipmentLayout candidate);
    }

    public sealed class InkInventory
    {
        InkInstance[] bag;
        InkInstance[][] equipped;
        readonly string[] categories;
        readonly IInkEquipmentPort equipmentPort;
        bool publishing;
        // Colour inks live in WeaponGrowth but fill the same single bag, so they count against Capacity too.
        public Func<int> ExternalUsage { get; set; }
        public int Capacity => bag.Length;
        public int Stored(InkKind kind) { int count=0;foreach(var item in bag)if(item!=null&&item.Definition.Kind==kind)count++;return count; }
        public bool HasRoom => Used+Math.Max(0,ExternalUsage?.Invoke()??0)<Capacity;
        public int StatSlots { get; }
        public int WeaponCount => equipped.Length;
        public int Used { get { int n = 0; foreach (var item in bag) if (item != null) n++; return n; } }
        public event Action Changed;
        public event Action<InkInstance> Acquired;
        public InkInventory(string[] weaponCategories, IInkEquipmentPort port = null, int capacity = 5, int statSlots = 5)
        {
            if (weaponCategories == null || weaponCategories.Length == 0 || capacity < 1 || statSlots < 1)
                throw new ArgumentException("Inventory dimensions required.");
            categories = (string[])weaponCategories.Clone();
            foreach (var category in categories) if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Weapon category required.");
            bag = new InkInstance[capacity]; StatSlots = statSlots; equipmentPort = port;
            equipped = new InkInstance[categories.Length][];
            for (int w = 0; w < equipped.Length; w++) equipped[w] = new InkInstance[statSlots + 2];
        }
        // The run's bag grows to its departure size (village bag unlock); stored items keep their cells. The bag never
        // shrinks, so no stored item is lost.
        public void SetCapacity(int capacity)
        {
            if (publishing) throw new InvalidOperationException("Cannot resize the bag now.");
            if (capacity < bag.Length) throw new ArgumentOutOfRangeException(nameof(capacity), "The bag never shrinks.");
            if (capacity == bag.Length) return;
            Array.Resize(ref bag, capacity);
            Publish(null);
        }
        public InkInstance BagAt(int slot) => bag[slot];
        public InkInstance EquippedAt(int weapon, int slot) => equipped[weapon][slot];
        public EquipmentLayout Equipment => new EquipmentLayout(equipped, StatSlots);
        // Follow a whole-weapon reorder; this does not transfer ink between weapons
        // or reapply equipment through the port (which would reset its resources).
        public void SwapWeaponSlots()
        {
            if (publishing || WeaponCount != 2) throw new InvalidOperationException("Cannot reorder equipment now.");
            (equipped[0], equipped[1]) = (equipped[1], equipped[0]);
            (categories[0], categories[1]) = (categories[1], categories[0]);
            Publish(null);
        }
        // The debug equipment supplier may replace an unmodified base weapon.
        public void SetUnmodifiedWeaponCategory(int weapon, string category)
        {
            if (publishing || weapon < 0 || weapon >= categories.Length || string.IsNullOrWhiteSpace(category))
                throw new InvalidOperationException("Cannot change the base weapon category now.");
            if (equipped[weapon][0] == null) categories[weapon] = category;
        }
        public ReadOnlyCollection<int> VisibleSlots(InkKind? filter)
        {
            var result = new List<int>();
            for (int i = 0; i < bag.Length; i++)
                if (!filter.HasValue || bag[i] != null && bag[i].Definition.Kind == filter.Value) result.Add(i);
            return result.AsReadOnly();
        }
        public bool Owns(InkInstance item)
        {
            if (item == null) return false;
            foreach (var entry in bag) if (entry == item) return true;
            foreach (var weapon in equipped) foreach (var entry in weapon) if (entry == item) return true;
            return false;
        }
        public bool TryAcquire(InkInstance item, out string error)
        {
            error = null;
            if (publishing) { error = "상태 변경 중입니다."; return false; }
            if (item == null || Owns(item)) { error = "이미 보유한 아이템입니다."; return false; }
            int free = Array.FindIndex(bag, entry => entry == null);
            if(!HasRoom)free=-1;
            if (free < 0) { error = "가방이 가득 찼습니다. 아이템은 바닥에 남습니다."; return false; }
            bag[free] = item;
            Publish(item); return true;
        }
        public bool TryExchangeGround(int bagSlot, InkInstance incoming, out InkInstance outgoing, out string error)
        {
            outgoing = null; error = null;
            if (publishing) { error = "상태 변경 중입니다."; return false; }
            if (!BagSlot(bagSlot) || bag[bagSlot] == null || incoming == null || Owns(incoming))
            { error = "교환 대상을 다시 선택하세요."; return false; }
            outgoing = bag[bagSlot]; bag[bagSlot] = incoming; Publish(incoming); return true;
        }
        public const string LockedMessage = "장착한 잉크는 해제·교체·이동할 수 없습니다.";
        // Equipping is permanent for the run: only empty slots accept ink, and equipped ink never returns to the bag.
        public bool TryEquip(int bagSlot, int weapon, int slot, out string error)
        {
            error = null;
            if (!BagSlot(bagSlot) || bag[bagSlot] == null || !Fits(bag[bagSlot], weapon, slot))
            { error = "이 슬롯에 장착할 수 없는 잉크입니다."; return false; }
            if (equipped[weapon][slot] != null) { error = "이미 잉크가 장착된 슬롯입니다. " + LockedMessage; return false; }
            var nextBag = (InkInstance[])bag.Clone(); var next = EquipmentLayout.Copy(equipped);
            nextBag[bagSlot] = null; next[weapon][slot] = bag[bagSlot];
            return Commit(nextBag, next, out error);
        }
        // Removes a bag ink so the caller can leave it on the ground. Equipped inks must be unequipped first.
        public bool TryDiscard(int bagSlot, out InkInstance discarded, out string error)
        {
            discarded = null; error = null;
            if (publishing) { error = "상태 변경 중입니다."; return false; }
            if (!BagSlot(bagSlot) || bag[bagSlot] == null) { error = "버릴 잉크를 가방에서 선택하세요."; return false; }
            discarded = bag[bagSlot]; bag[bagSlot] = null;
            Publish(null); return true;
        }
        public bool TryUnequip(int weapon, int slot, out string error)
        {
            error = !EquipmentSlot(weapon, slot) || equipped[weapon][slot] == null ? "빈 슬롯입니다." : LockedMessage;
            return false;
        }
        public bool TryMoveEquipment(int fromWeapon, int fromSlot, int toWeapon, int toSlot, out string error)
        {
            error = LockedMessage;
            return false;
        }
        public bool Fits(InkInstance item, int weapon, int slot)
        {
            if (item == null || !EquipmentSlot(weapon, slot)) return false;
            var kind = slot == 0 ? InkKind.Form : slot == StatSlots + 1 ? InkKind.Special : InkKind.Stat;
            return item.Definition.Kind == kind && (kind != InkKind.Form || item.Definition.WeaponCategory == categories[weapon]);
        }
        public void Clear()
        {
            if (publishing) throw new InvalidOperationException("Reentrant inventory mutation.");
            Array.Clear(bag, 0, bag.Length);
            foreach (var weapon in equipped) Array.Clear(weapon, 0, weapon.Length);
            Publish(null);
        }
        bool BagSlot(int slot) => slot >= 0 && slot < bag.Length;
        bool EquipmentSlot(int weapon, int slot) => weapon >= 0 && weapon < equipped.Length && slot >= 0 && slot < StatSlots + 2;
        bool Commit(InkInstance[] nextBag, InkInstance[][] next, out string error)
        {
            error = null;
            if (publishing) { error = "상태 변경 중입니다."; return false; }
            publishing = true;
            try
            {
                error = equipmentPort?.Apply(new EquipmentLayout(next, StatSlots));
                if (error != null) return false;
                bag = nextBag; equipped = next;
            }
            finally { publishing = false; }
            Publish(null); return true;
        }
        void Publish(InkInstance acquired)
        {
            publishing = true;
            try { Changed?.Invoke(); if (acquired != null) Acquired?.Invoke(acquired); }
            finally { publishing = false; }
        }
    }
}
