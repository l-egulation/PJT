using System;
using System.Collections.Generic;

namespace Deviant.Run
{
    // A weapon stamp turns its blueprint into one form of the same kind; a skill stamp picks the special variant
    // (1 range, 2 movement, 3 follow-up) of whatever form the weapon has.
    public sealed class RunStamp
    {
        public Guid Id { get; } = Guid.NewGuid();
        public StampKind Kind { get; }
        public string BlueprintKind { get; }
        public string FormId { get; }
        public int Skill { get; }
        RunStamp(StampKind kind, string blueprintKind, string formId, int skill) { Kind = kind; BlueprintKind = blueprintKind; FormId = formId; Skill = skill; }
        public static RunStamp Weapon(string blueprintKind, string formId)
        {
            if (string.IsNullOrEmpty(blueprintKind) || string.IsNullOrEmpty(formId)) throw new ArgumentException("Weapon stamp needs a blueprint kind and a form.");
            return new RunStamp(StampKind.Weapon, blueprintKind, formId, 0);
        }
        public static RunStamp SkillStamp(int skill)
        {
            if (skill < 1 || skill > 3) throw new ArgumentException("Unknown skill stamp.");
            return new RunStamp(StampKind.Skill, null, null, skill);
        }
        public bool SameEffect(RunStamp other) => other != null && other.Kind == Kind && other.FormId == FormId && other.Skill == Skill;
    }

    // Run-scoped holdings of the blueprint redesign. Stamps wait here until they are applied at a maintenance
    // facility; palettes and color inks live in WeaponGrowth. Legacy part counts have no combat effects.
    public sealed class RunStash
    {
        public const int WeaponSlots = 2;
        public int InkParts { get; private set; }
        public int StatLevel { get; private set; }
        public int SlotLevel { get; private set; }
        readonly List<RunStamp> stamps = new List<RunStamp>();
        readonly RunStamp[] weaponStamp = new RunStamp[WeaponSlots];
        readonly RunStamp[] skillStamp = new RunStamp[WeaponSlots];
        public IReadOnlyList<RunStamp> Stamps => stamps;
        public int Count(StampKind kind) { int n = 0; foreach (var s in stamps) if (s.Kind == kind) n++; return n; }
        public event Action Changed;

        public void AddInkParts(int count) { if (count <= 0) return; InkParts += count; Changed?.Invoke(); }
        public RunStamp AddStamp(RunStamp stamp) { stamps.Add(stamp ?? throw new ArgumentNullException(nameof(stamp))); Changed?.Invoke(); return stamp; }
        public RunStamp AddWeaponStamp(string blueprintKind, string formId) => AddStamp(RunStamp.Weapon(blueprintKind, formId));
        public RunStamp AddSkillStamp(int skill) => AddStamp(RunStamp.SkillStamp(skill));
        public void RaiseStatLevel() { StatLevel++; Changed?.Invoke(); }
        public void RaiseSlotLevel() { SlotLevel++; Changed?.Invoke(); }

        public RunStamp EquippedWeaponStamp(int slot) => weaponStamp[slot];
        public RunStamp EquippedSkillStamp(int slot) => skillStamp[slot];
        public void SwapWeaponSlots()
        {
            (weaponStamp[0], weaponStamp[1]) = (weaponStamp[1], weaponStamp[0]);
            (skillStamp[0], skillStamp[1]) = (skillStamp[1], skillStamp[0]);
            Changed?.Invoke();
        }
        public RunStamp Equipped(int slot, StampKind kind) => kind == StampKind.Weapon ? weaponStamp[slot] : skillStamp[slot];

        public void ConfigureSandboxStamps(WeaponGrowth growth, int slot, RunStamp weapon, RunStamp skill)
        {
            if (growth == null || !growth.IsSandbox || slot < 0 || slot >= WeaponSlots ||
                weapon != null && weapon.Kind != StampKind.Weapon || skill != null && skill.Kind != StampKind.Skill)
                throw new InvalidOperationException("Invalid sandbox stamp configuration.");
            weaponStamp[slot] = weapon; skillStamp[slot] = skill; Changed?.Invoke();
        }

        // Run-only debug assignment: no purchase, bag grant, payment or persistent profile change.
        public void DebugConfigureStamps(int slot, RunStamp weapon, RunStamp skill)
        {
            if (slot < 0 || slot >= WeaponSlots || weapon != null && weapon.Kind != StampKind.Weapon ||
                skill != null && skill.Kind != StampKind.Skill) throw new ArgumentException("Invalid debug stamp configuration.");
            weaponStamp[slot] = weapon; skillStamp[slot] = skill; Changed?.Invoke();
        }

        // The first stamp of a kind on a weapon is free; replacing costs the swap fee and destroys the previous stamp
        // without refund. Re-applying the same effect is refused before any payment. `apply` changes the weapon and
        // returns an error to cancel; gold and the stamp are only taken after it succeeds.
        public int CostOf(int slot, RunStamp stamp, RunEconomy economy) => Equipped(slot, stamp.Kind) == null ? 0 : economy.StampSwapCost;

        public bool TrySell(Guid stampId,RunEconomy economy,out string error)
        {
            var stamp=stamps.Find(s=>s.Id==stampId);
            if(stamp==null){error="보관한 스탬프가 없습니다.";return false;}
            if(economy==null){error="런 재화가 없습니다.";return false;}
            stamps.Remove(stamp);economy.Earn(economy.StampSaleGold);Changed?.Invoke();error=null;return true;
        }

        public bool TryApply(int slot, Guid stampId, string slotBlueprintKind, RunEconomy economy, Func<RunStamp, string> apply, out string error)
        {
            error = null;
            if (slot < 0 || slot >= WeaponSlots) { error = "잘못된 무기 슬롯"; return false; }
            if (slotBlueprintKind == null) { error = "도면을 장착하지 않은 슬롯입니다."; return false; }
            var stamp = stamps.Find(s => s.Id == stampId);
            if (stamp == null) { error = "보관한 스탬프가 없습니다."; return false; }
            if (stamp.Kind == StampKind.Weapon && stamp.BlueprintKind != slotBlueprintKind) { error = "이 도면에 맞는 무기 스탬프가 아닙니다."; return false; }
            if (stamp.SameEffect(Equipped(slot, stamp.Kind))) { error = "이미 같은 스탬프가 적용되어 있습니다."; return false; }
            int cost = CostOf(slot, stamp, economy);
            if (economy.Gold < cost) { error = "골드가 부족합니다."; return false; }
            error = apply?.Invoke(stamp);
            if (error != null) return false;
            economy.TrySpend(cost);
            stamps.Remove(stamp);
            if (stamp.Kind == StampKind.Weapon) weaponStamp[slot] = stamp; else skillStamp[slot] = stamp;
            Changed?.Invoke();
            return true;
        }
    }
}
