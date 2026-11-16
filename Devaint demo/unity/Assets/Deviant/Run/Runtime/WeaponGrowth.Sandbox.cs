using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Run
{
    public sealed partial class WeaponGrowth
    {
        // Explicit session-local owner. Ordinary runs cannot use the free editing commands.
        public bool IsSandbox { get; private set; }
        public static WeaponGrowth CreateSandbox(GrowthData data, RunEconomy economy)
            => new WeaponGrowth(data, economy) { IsSandbox = true };

        public void ConfigureSandboxPalette(Guid weapon, IReadOnlyList<string> parts, IReadOnlyList<int> levels)
        {
            Require(IsSandbox, "테스트 전용 성장 데이터가 아닙니다.");
            Require(weapon != Guid.Empty && parts != null && parts.Count >= 3 && parts.Count <= 5 && levels != null && levels.Count == parts.Count, "팔레트는 3~5칸입니다.");
            var selected = new GrowthPart[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                selected[i] = string.IsNullOrEmpty(parts[i]) ? null : Data.FindPart(parts[i]);
                Require(string.IsNullOrEmpty(parts[i]) || selected[i] != null, "현재 카탈로그에 없는 잉크입니다.");
                Require(levels[i] >= 0 && levels[i] <= 5, "강화는 +0~+5입니다.");
                Require(selected[i] == null || selected[i].duplicate || !selected.Take(i).Contains(selected[i]), "이 잉크는 무기당 한 개만 장착할 수 있습니다.");
            }
            var palette = Equipped(weapon);
            if (palette == null || palette.Slots.Count != parts.Count)
            {
                var previous = palette;
                palette = new GrowthPalette(Enumerable.Repeat("colorless", parts.Count).ToArray());
                if (previous != null)
                    for (int i = 0; i < Math.Min(previous.Slots.Count, palette.Slots.Count); i++)
                    {
                        palette.Slots[i].Part = previous.Slots[i].Part;
                        palette.Slots[i].InstanceId = previous.Slots[i].InstanceId;
                    }
                if (previous != null) palettes.Remove(previous);
                palette.BoundWeapon = weapon;
                palettes.Add(palette); equipped[weapon] = palette;
            }
            for (int i = 0; i < selected.Length; i++)
            {
                var slot = palette.Slots[i];
                if (slot.Part != selected[i]) slot.InstanceId = Guid.NewGuid();
                slot.Color = "colorless"; slot.Part = selected[i]; slot.Level = levels[i];
            }
            Publish();
        }

        public void SetSandboxAttackLevel(int level)
        {
            Require(IsSandbox, "테스트 전용 성장 데이터가 아닙니다.");
            Require(level >= 0 && level <= Data.attackMaxLevel, "공격 강화 단계 범위를 벗어났습니다.");
            AttackLevel = level; Publish();
        }
    }
}
