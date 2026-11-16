using Deviant.Input.Contracts;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // First-run ink tutorial on the play GUI (docs/design/story/tutorials/first-run-guide.md, guide-ink · guide-bag). The world part
    // (monster, guide ink, floor outline, records) is Chapter1FirstRunInkGuide; this draws the hero's line over the head
    // once the guide ink lands and, inside the bag, the palette slot the ink fits, ringed,
    // with its prompt. Nothing here picks a slot or mounts the ink for the player.
    public sealed partial class TemporaryGameGui
    {
        public const string InkGuideBagLine = "색이 맞는 칸에 장착해 보자";
        static readonly Color InkGuideRing = new Color32(222, 168, 40, 255);
        const float InkGuideTypeRate = 24, InkGuideBlinkSeconds = .8f;
        Chapter1FirstRunInkGuide inkGuide;
        MaintenanceBubbleView inkGuideBubble;
        Text inkGuidePrompt;
        bool inkGuideTabSet;

        Chapter1FirstRunInkGuide InkGuide
        {
            get { if (!inkGuide && world) inkGuide = world.GetComponent<Chapter1FirstRunInkGuide>(); return inkGuide; }
        }

        // No top 「인벤토리를 열어보자 [Tab]」 prompt: the palette notebook says the bag is [Tab] (user 2026-11-09).

        // The hero's 「어? 이건 뭐지?」 in the speech bubble every chapter speaker uses, typed out over the head, when the
        // guide ink lands. Only on a guide run without the notebooks; with them the palette notebook opens on the pickup.
        void RefreshInkGuideBubble(bool hudShown)
        {
            var guide = InkGuide; var player = host && host.Arena ? host.Arena.Player : null;
            bool show = hudShown && !firstRunNotebook && !(RedInkPickup && RedInkPickup.Collected) && guide && guide.ReactionShowing &&
                (guide.Current == Chapter1FirstRunInkGuide.Stage.Dropped || guide.Current == Chapter1FirstRunInkGuide.Stage.InBag) &&
                world && world.View && player && !player.Dead;
            if (!show) { if (inkGuideBubble) inkGuideBubble.Hide(); return; }
            if (!inkGuideBubble) inkGuideBubble = MaintenanceBubbleView.Create(world.View);
            string line = Chapter1FirstRunInkGuide.ReactionLine;
            int count = Mathf.Clamp(Mathf.FloorToInt(guide.ReactionAge * InkGuideTypeRate) + 1, 1, line.Length);
            inkGuideBubble.Show(line, count, null, true, player.Position + Vector2.up * (player.size.y + .4f), Time.unscaledTime, false);
        }

        // In the bag: the prompt and a blinking ring on the fitting palette slot. When the bag opens the weapon tab turns
        // once to that slot's weapon; the slot itself is left for the player to pick.
        void RefreshInkGuideBag()
        {
            if(nativeBag!=null){RefreshNativeInkGuide();return;}
            if (!bag) return;
            if (!inkGuidePrompt)
            {
                inkGuidePrompt = Words(InkGuideBagLine, bag.transform, 32, 372, 348, 36, 16, true, JInk, TextAnchor.MiddleLeft);
                inkGuidePrompt.name = "First-run ink prompt"; inkGuidePrompt.gameObject.SetActive(false);
            }
            var guide = InkGuide;
            int weapon = -1, slot = -1;
            bool teaching = Presenter.IsOpen && guide && guide.Current == Chapter1FirstRunInkGuide.Stage.InBag && guide.TargetSlot(out weapon, out slot);
            if (!Presenter.IsOpen) inkGuideTabSet = false;
            else if (teaching && !inkGuideTabSet)
            {
                inkGuideTabSet = true;
                if (Presenter.SelectedWeapon != weapon) { Presenter.SelectWeapon(weapon); bagPaletteSlot = -1; }
            }
            inkGuidePrompt.gameObject.SetActive(teaching);
            float alpha = .45f + .55f * (.5f + .5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2 / InkGuideBlinkSeconds));
            for (int w = 0; w < 2; w++)
                for (int s = 1; s <= 5; s++)
                    InkGuideRingOn(equipment[w * 7 + s], teaching && w == weapon && s - 1 == slot, alpha);
        }

        // Outside the amber selection ring, so both read at once.
        void InkGuideRingOn(Button cell, bool on, float alpha)
        {
            if (!cell) return;
            var ring = cell.transform.Find("First-run ink ring");
            if (!on) { if (ring) ring.gameObject.SetActive(false); return; }
            var size = ((RectTransform)cell.transform).sizeDelta;
            if (!ring) ring = Rounded("First-run ink ring", cell.transform, -7, -7, size.x + 14, size.y + 14, 6, 3, Color.clear, InkGuideRing).transform;
            ring.gameObject.SetActive(true); ring.SetAsLastSibling();
            ring.GetComponent<Image>().color = new Color(1, 1, 1, alpha);
        }

        public bool InkGuidePromptShown => inkGuidePrompt && inkGuidePrompt.gameObject.activeInHierarchy;
        public bool InkGuideBubbleShown => inkGuideBubble && inkGuideBubble.GetComponentInChildren<MaintenanceBubbleGraphic>(false);

        void DisposeInkGuide() { if (inkGuideBubble) Destroy(inkGuideBubble.gameObject); }
    }
}
