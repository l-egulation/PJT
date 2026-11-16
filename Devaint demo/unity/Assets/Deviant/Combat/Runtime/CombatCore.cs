using System;
using System.Collections.Generic;
using Deviant.Equipment.Contracts;

namespace Deviant.Combat
{
    // No Unity dependency: these rules can be checked without an installed editor.
    public static class CombatRules
    {
        public const float BodyWidth = .9375f, BodyHeight = 2f, BodyOffsetX = .1875f;
        public static float Cooldown(float baseValue, float reduction) => Deviant.Equipment.WeaponResources.Cooldown(baseValue, reduction);
        public static float Damage(float value, float increase) { return value * (1 + increase); }
        public static bool IntersectsTime(float previous, float now, float start, float duration)
        { return now >= start && previous < start + duration; }
        public static float RefreshStatus(float oldEnd, float now, float duration)
        { return Math.Max(oldEnd, now + duration); }
    }

    // Compatibility adapter for existing scene/tool callers. Equipment owns all
    // numeric state; this object stores only the command/query interface.
    public sealed class ChargePool
    {
        readonly IWeaponCharges owner;
        float offset, pausedAt;
        bool paused;
        float Clock(float now) => (paused ? pausedAt : now) - offset;
        public void Suspend(float now) { if (!paused) { owner.Tick(Clock(now)); pausedAt = now; paused = true; } }
        public void Resume(float now) { if (paused) { offset += now-pausedAt; paused = false; } }
        public ChargeState Snapshot { get { var c = owner.Read(); return new ChargeState(c.Capacity, c.Available, c.Period, c.NextReady + offset); } }
        public float Remaining(float now) => Available >= Capacity ? 0 : Math.Max(0, owner.Read().NextReady - Clock(now));
        public int Capacity => owner.Read().Capacity;
        public int Available => owner.Read().Available;
        public float NextReady => owner.Read().NextReady + offset;
        public float Period => owner.Read().Period;
        public ChargePool(int capacity, float period) : this(Deviant.Equipment.WeaponResources.CreateCharges(capacity, period)) { }
        public ChargePool(IWeaponCharges owner) { this.owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
        public void Reset(int capacity, float period) { offset = pausedAt = 0; paused = false; owner.Reset(capacity, period); }
        public void Tick(float now) => owner.Tick(Clock(now));
        public bool Spend(float now) => owner.Spend(Clock(now));
        public void Refund(float seconds, float now) => owner.Refund(seconds, Clock(now));
        public void Rearm(float seconds, float now)
        {
            if (!(owner is IWeaponChargeRearm recovery)) throw new InvalidOperationException("Weapon charges do not support a fixed rearm delay.");
            recovery.Rearm(seconds, Clock(now));
        }
        public float Debt(float now) => owner.Debt(Clock(now));
        public void Reconfigure(int capacity, float period, float now) => owner.Reconfigure(capacity, period, Clock(now));
    }

    // Legacy index editing is translated into Equipment commands. Clone returns
    // a detached array; no caller receives the owner's mutable slot storage.
    public sealed class StatSlotView : IReadOnlyList<int>, ICloneable
    {
        readonly IWeaponStats owner;
        public StatSlotView(IWeaponStats owner) { this.owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
        public int Length => WeaponStatSnapshot.SlotCount;
        public int Count => Length;
        public WeaponStatSnapshot Snapshot => owner.Read();
        public int this[int index] { get => (int)owner.Read().Slots[index]; set => owner.SetSlot(index, (StatInkKind)value); }
        public object Clone()
        {
            var snapshot = owner.Read(); var copy = new int[Length];
            for (int i = 0; i < copy.Length; i++) copy[i] = (int)snapshot.Slots[i];
            return copy;
        }
        public void Clear() => owner.Clear();
        public void CopyFrom(int[] values)
        {
            if (values == null || values.Length != Length) throw new ArgumentException("Five stat slots are required.", nameof(values));
            var copy = new StatInkKind[Length]; for (int i = 0; i < copy.Length; i++) copy[i] = (StatInkKind)values[i];
            owner.ReplaceSlots(copy);
        }
        public IEnumerator<int> GetEnumerator() { foreach (var kind in owner.Read().Slots) yield return (int)kind; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class WeaponState
    {
        readonly IWeaponInstance owner;
        internal IWeaponInstance Resource => owner;
        public Guid InstanceId => owner.InstanceId;
        public IInkPalette InkPalette => owner.InkPalette;
        public bool ExperimentalChargeStamp { get => owner.ExperimentalChargeStamp; set => owner.ExperimentalChargeStamp=value; }
        public int FormIndex { get => owner.ReadSelection().FormIndex; set => Select(value, SpecialIndex); }
        public int SpecialIndex { get => owner.ReadSelection().SpecialIndex; set => Select(FormIndex, value); }
        public WeaponSnapshot Snapshot => new WeaponSnapshot(InstanceId, owner.ReadSelection(), StatSlots.Snapshot, Cooldown.Snapshot, InkPalette.Read(), ExperimentalChargeStamp);
        public readonly StatSlotView StatSlots;
        public readonly ChargePool Cooldown;
        public WeaponState() : this(Deviant.Equipment.WeaponResources.CreateWeapon()) { }
        public WeaponState(IWeaponStats stats) : this(Deviant.Equipment.WeaponResources.CreateWeapon(stats)) { }
        public WeaponState(IWeaponInstance owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            StatSlots = new StatSlotView(owner.Stats); Cooldown = new ChargePool(owner.Charges);
        }
        public void Select(int form, int special) => owner.Select(new WeaponSelection(form, special));
        public int Count(int kind) { int n = 0; foreach (int value in StatSlots) if (value == kind) n++; return n; }
        public float TestBasicDamageScale = 1, TestSpecialDamageScale = 1, TestSpeedScale = 1, TestFormSpeedScale = 1;
        public float TestCooldownSeconds = -1;
        public float CooldownPeriod(float authored) => TestCooldownSeconds >= 0 ? TestCooldownSeconds : CombatRules.Cooldown(authored, Reduction);
        public float BasicBonus => (1 + StatSlots.Snapshot.BasicBonus) * TestBasicDamageScale - 1;
        public float SpecialBonus => (1 + StatSlots.Snapshot.SpecialBonus) * TestSpecialDamageScale - 1;
        public float Speed => StatSlots.Snapshot.Speed * TestSpeedScale * TestFormSpeedScale;
        public float Reduction => StatSlots.Snapshot.Reduction;
    }

    // Stable compatibility bindings for the fixed occupied pair. Equipment
    // owns membership and the active slot; this array contains views only.
    public sealed class WeaponLoadoutView : IReadOnlyList<WeaponState>
    {
        readonly IWeaponLoadout owner;
        readonly IEquipmentCommands commands;
        readonly Dictionary<Guid,WeaponState> views = new Dictionary<Guid,WeaponState>();
        public WeaponLoadoutView(IWeaponLoadout owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            commands = Deviant.Equipment.WeaponResources.CreateCommands(owner);
            for (int i = 0; i < WeaponLoadoutSnapshot.SlotCount; i++) { var w = owner.GetWeapon(i); views[w.InstanceId] = new WeaponState(w); }
        }
        public int Length => WeaponLoadoutSnapshot.SlotCount;
        public int Count => Length;
        public int ActiveSlot => owner.ActiveSlot;
        public WeaponState ActiveWeapon => this[ActiveSlot];
        public WeaponLoadoutSnapshot Snapshot => new WeaponLoadoutSnapshot(ActiveSlot, new[] { this[0].Snapshot, this[1].Snapshot });
        // A slot replaced through TryReplace gets its view on first read.
        public WeaponState this[int slot]
        {
            get
            {
                var weapon = owner.GetWeapon(slot);
                if (!views.TryGetValue(weapon.InstanceId, out var view)) views.Add(weapon.InstanceId, view = new WeaponState(weapon));
                return view;
            }
        }
        public void ReplaceSlot(int slot, IWeaponInstance weapon)
        { owner.ReplaceSlot(slot, weapon); if (!views.TryGetValue(weapon.InstanceId, out var view) || !ReferenceEquals(view.Resource, weapon)) views[weapon.InstanceId] = new WeaponState(weapon); }
        public bool TryReplace(int slot, Guid expected, IWeaponInstance next)
            => owner is IWeaponLoadoutReplacement replacement && replacement.TryReplace(slot, expected, next);
        public void SelectSlot(int slot) => owner.SelectSlot(slot);
        public void SwapSlots() => owner.SwapSlots();
        public IWeaponInstance CaptureInstance(int slot) => owner.GetWeapon(slot);
        public WeaponState Find(Guid id) => views.TryGetValue(id, out var view) ? view : null;
        public EquipmentCommandResult Reconfigure(int slot, WeaponConfiguration configuration, float now) => commands.Reconfigure(slot,
            slot >= 0 && slot < Count ? this[slot].InstanceId : Guid.Empty, configuration, now);
        public IEnumerator<WeaponState> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class HitLedger
    {
        readonly HashSet<long> hits = new HashSet<long>();
        public bool Contains(int hitIndex, int targetId) => hits.Contains(((long)hitIndex << 32) | (uint)targetId);
        public bool First(int hitIndex, int targetId)
        { return hits.Add(((long)hitIndex << 32) | (uint)targetId); }
    }


}
