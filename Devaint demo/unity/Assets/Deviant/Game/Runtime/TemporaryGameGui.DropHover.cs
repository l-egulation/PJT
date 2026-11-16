using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    // Drop names on hover (playtest 2026-11-06): items on the floor carry no name line; when the player walks up to one,
    // its name shows in the speech bubble every chapter speaker uses, its tail on the item. Only the nearest drop in
    // reach is named, so a pile never stacks bubbles. Gold has no bubble (its amount pops on pickup).
    public sealed partial class TemporaryGameGui
    {
        public const float DropHoverReach = 2.6f;
        MaintenanceBubbleView dropBubble;
        Camera dropBubbleView;
        public Chapter1RewardPickup HoveredDrop { get; private set; }

        void RefreshDropHover(bool hudShown)
        {
            var player = host && host.Arena ? host.Arena.Player : null;
            HoveredDrop = null;
            // The first-run reaction line already talks over the hero while the guide ink lands.
            if (hudShown && player && !player.Dead && !(inkGuideBubble && InkGuideBubbleShown))
            {
                float best = DropHoverReach;
                foreach (var drop in Chapter1RewardPickup.Live)
                {
                    if (!drop || drop.Arena != host.Arena || !drop.Ready || !drop.View || string.IsNullOrEmpty(drop.HoverName)) continue;
                    float distance = Vector2.Distance(player.Center, drop.transform.position);
                    if (distance <= best) { best = distance; HoveredDrop = drop; }
                }
            }
            if (!HoveredDrop) { if (dropBubble) dropBubble.Hide(); return; }
            if (dropBubble && dropBubbleView != HoveredDrop.View) { Destroy(dropBubble.gameObject); dropBubble = null; }
            if (!dropBubble) { dropBubbleView = HoveredDrop.View; dropBubble = MaintenanceBubbleView.Create(dropBubbleView); dropBubble.name = "Drop name bubble"; }
            string name = HoveredDrop.HoverName;
            dropBubble.Show(name, name.Length, null, true, HoveredDrop.HoverAnchor, Time.unscaledTime, false);
        }

        public bool DropBubbleShown => dropBubble && dropBubble.GetComponentInChildren<MaintenanceBubbleGraphic>(false);

        void DisposeDropHover() { if (dropBubble) Destroy(dropBubble.gameObject); }
    }
}
