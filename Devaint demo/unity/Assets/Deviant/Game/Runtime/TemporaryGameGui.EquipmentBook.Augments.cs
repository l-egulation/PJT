using System.Linq;
using Deviant.Run;
using UnityEngine;

namespace Deviant.Game
{
    // Augments (docs/design/progression/maintenance-augments.md) in the growth tab, where the attack upgrade was. Augments
    // are never sold (user decision 2026-11-13): this page lists the run's cards and where they come from, a boss clear
    // with a next chapter and the seal reward chest. The choice itself is the journal card screen (TemporaryGameGui.Augment).
    public sealed partial class TemporaryGameGui
    {
        static string AugmentKind(AugmentCatalog catalog, AugmentDefinition card)
            => card.Stable ? "안정형" : "도박형 / " + catalog.GroupName(card.group);

        void NativeAugmentPage(EquipmentBook b, WeaponGrowth g)
        {
            var augments = g.Augments;
            if (augments == null) { Words("증강을 사용할 수 없습니다.", b.Body, 420, 74, 348, 36, 14, false, JMuted); return; }
            Words($"보유 증강 {augments.Owned.Count}", b.Body, 420, 56, 348, 20, 14, true);
            var names = augments.Owned.Count == 0 ? "아직 고른 증강이 없습니다." : string.Join("\n", augments.Owned.Select(a => a.name));
            var list = Words(names, b.Body, 420, 82, 348, 156, 13, false, augments.Owned.Count == 0 ? JMuted : JInk, TextAnchor.UpperLeft);
            list.horizontalOverflow = HorizontalWrapMode.Wrap; list.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void NativeAugmentDetail(EquipmentBook b, WeaponGrowth g)
        {
            if (g.Augments == null) return;
            NativeDescription(b, "캐릭터 강화", "증강", Art("sword"), "판매하지 않음",
                "증강은 상점에서 사지 않는다. 다음 챕터로 이어지는 보스를 처치하면 무기 보상 뒤에, 봉인 도전방 보상 상자를 열면 안정형 1장과 서로 다른 계열의 도박형 2장 중 1장을 고른다. 같은 증강은 한 번만 얻고 런 동안 유지된다.", height: 110);
        }
    }
}
