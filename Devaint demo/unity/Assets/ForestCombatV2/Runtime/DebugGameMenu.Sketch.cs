using System;
using System.Linq;
using Deviant.Combat;
using Deviant.Run;

namespace ForestCombatV2
{
    public sealed partial class DebugGameMenu
    {
        SketchEquipment RunSketch => Growth?.Sketch;
        static readonly string[] SketchColors = { "neutral", "red", "yellow", "green", "blue", "purple" };

        string RewardGrantUnavailableReason => RunSketch == null ? "현재 런의 보상이 없는 씬입니다." :
            arena.Player.Dead ? "사망한 상태에서는 보상을 추가할 수 없습니다." : null;

        public bool TryAddReward()
        {
            if (!DevFeatures.Enabled || !IsOpen || scope == null || !scope.IsActive || !arena || !arena.Player)
            { status = "치트 모드를 연 상태에서 보상을 추가하세요."; return false; }
            string reason = RewardGrantUnavailableReason;
            if (reason != null) { status = reason; return false; }
            try
            {
                RunSketch.State.DebugAddReward();
                status = $"보상 +1 완료 / 받아 둔 보상 {RunSketch.Banked}개";
                return true;
            }
            catch (InvalidOperationException exception) { status = exception.Message; return false; }
        }

        bool EditSketch(Action<SketchEquipment> edit, out string error)
        {
            error = null;
            if (!DevFeatures.Enabled || !IsOpen || scope == null || !scope.IsActive || !arena || !arena.Player || RunSketch == null)
            { error = "치트 모드에서 런 장비를 선택하세요."; return false; }
            var player = arena.Player;
            if (player.Dead || player.Dashing || player.CheckpointBusy)
            { error = "사망, 대시, 포탈 중에는 장비를 변경할 수 없습니다."; return false; }
            try
            {
                edit(RunSketch);
                player.CancelAction(); player.ClearInputState();
                arena.SyncSketchLoadout();
                return true;
            }
            catch (InvalidOperationException exception) { error = exception.Message; return false; }
        }
        bool EditSketchWeapon(Action<SketchEquipment, RunInkWeapon> edit, out string error)
            => EditSketch(equipment =>
            {
                var weapon = equipment.Equipped(slot);
                if (weapon == null) throw new InvalidOperationException("맨손에는 팔레트가 없습니다. 무기를 먼저 선택하세요.");
                edit(equipment, weapon);
            }, out error);

        bool ApplySketchEquipment(int targetSlot, int targetForm, int targetSkill, out string error)
        {
            string id = arena.catalog.forms[targetForm].id;
            bool applied = EditSketch(equipment => equipment.DebugConfigureWeapon(targetSlot, id == "unarmed" ? null : id, targetSkill), out error);
            if (applied) SelectWeapon(targetSlot);
            return applied;
        }
        public bool TryApplyRunInk(string id, out string error)
            => EditSketchWeapon((equipment, weapon) => equipment.DebugSetInk(weapon.Id, inkSlot, id), out error);
        bool ResizeSketchPalette(int count, out string error)
        {
            bool resized = EditSketchWeapon((equipment, weapon) => equipment.DebugResizePalette(weapon.Id, count), out error);
            if (resized)
            {
                paletteSlot = UnityEngine.Mathf.Clamp(paletteSlot, 0, count - 1);
                inkSlot = UnityEngine.Mathf.Clamp(inkSlot, 0, count - 1);
            }
            return resized;
        }

        void BuildSketchEquipmentRows()
        {
            var player = arena.Player;
            Group("무기, 스킬 / 선택 즉시 반영");
            Tabs("편집할 무기", Enumerable.Range(0, player.weapons.Count).Select(index => "무기 " + (index + 1)).ToArray(), slot, SelectWeapon, caption: "무기");
            slot = UnityEngine.Mathf.Clamp(slot, 0, player.weapons.Count - 1);
            form = player.weapons[slot].FormIndex; skill = player.weapons[slot].SpecialIndex;
            Choice("무기 형태", arena.catalog.forms.Select(value => value.name).ToArray(), form,
                index => ChangeEquipment(index, UnityEngine.Mathf.Clamp(player.weapons[slot].SpecialIndex, 0, arena.catalog.forms[index].specials.Length - 1)));
            var definition = arena.catalog.forms[form];
            if (definition.id == "unarmed") Add("스킬: 없음 / 맨손은 기본 공격만 사용합니다.");
            else Choice("스킬", definition.specials.Select(special => special.name).ToArray(), skill,
                index => ChangeEquipment(player.weapons[slot].FormIndex, index));
            var weapon = RunSketch.Equipped(slot);
            if (weapon == null)
            {
                Add("무기를 선택하면 현재 런에 장착하고 팔레트를 편집할 수 있습니다.");
                return;
            }
            Add($"내구도: {weapon.Durability:0.##} / {weapon.Maximum:0.##}");
            paletteSlot = UnityEngine.Mathf.Clamp(paletteSlot, 0, weapon.Slots.Count - 1);
            inkSlot = UnityEngine.Mathf.Clamp(inkSlot, 0, weapon.Slots.Count - 1);
            var slots = weapon.Slots.Select((cell, index) => (index + 1) + "번 / " + ColorName(cell.Color)).ToArray();
            Group("팔레트 / 칸별 색상");
            Tabs("편집할 팔레트 칸", slots, paletteSlot, SelectPaletteSlot, caption: "편집할 칸");
            Tabs("칸 수 (즉시 변경)", new[] { "1칸", "2칸", "3칸", "4칸", "5칸", "6칸" }, weapon.Slots.Count - 1,
                index => status = TryResizePalette(index + 1, out var error) ? "팔레트 칸 수 변경 완료" : error, caption: "칸 수", share: .5f);
            Choice("색상 (선택 즉시 적용)", SketchColors.Select(ColorName).ToArray(), Array.IndexOf(SketchColors, weapon.Slots[paletteSlot].Color),
                index => status = TryApplyPaletteColor(SketchColors[index], out var error) ? "선택한 칸 색상 변경 완료" : error, caption: "색상");
            Group("런 잉크 / 보유와 관계없이 선택");
            Tabs("편집할 잉크 칸", slots, inkSlot, SelectInkSlot, caption: "편집할 칸");
            var inks = RunSketch.Catalog.inks;
            var mount = weapon.Slots[inkSlot];
            Add("팔레트와 잉크의 색상이 같으면 강화 효과, 다르면 기본 효과를 사용합니다. 무색 칸은 기본 효과입니다.");
            Add(mount.Ink == null ? "아래 목록에서 장착할 런 잉크를 선택하세요." :
                "현재 적용: " + (mount.Color == mount.Ink.color ? "강화 효과" : "기본 효과") + "\n효과 설명: " + mount.Ink.description +
                $"\n재발동 대기: {mount.Ink.cooldown:0.##}초 / 남은 시간: {mount.Cooldown:0.##}초");
            Choice("장착할 잉크", new[] { "없음" }.Concat(inks.Select(ink => ink.name)).ToArray(), Array.IndexOf(inks, mount.Ink) + 1,
                index => status = TryApplyRunInk(index == 0 ? null : inks[index - 1].id, out var error) ? "런 잉크 변경 완료" : error, share:1);
        }
    }
}
