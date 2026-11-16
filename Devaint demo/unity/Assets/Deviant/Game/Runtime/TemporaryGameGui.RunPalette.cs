using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // At the start of a run, once both weapons hold their departure palettes, a card in the middle of the screen shows
    // each weapon's palette colours for a few seconds (playtest 2026-11-04: what palette did I get?). Once per run's
    // growth (it carries on into chapter 2 without showing again). Part of the HUD, so screens and results hide it.
    public sealed partial class TemporaryGameGui
    {
        public const float RunPaletteSeconds = 3.5f;
        RectTransform runPaletteCard;
        readonly Image[] runPaletteWeapons = new Image[2];
        readonly Image[,] runPaletteDrops = new Image[2, 5];
        Text[] runPaletteNames;
        Text runPaletteHint;
        RectTransform runPaletteKeyRow;
        Image runPaletteKey;
        // The held card's close prompt: the game's own F keycap (2 screen px per art px, like the world keycaps) beside
        // 「계속」, bobbing 4 screen px a second like every other keycap (user 2026-11-09: the text prompt was easy to miss).
        const float CardKeyRowY = 548, CardKeyW = 59, CardKeyH = 62, CardKeyBob = 7;
        WeaponGrowth runPaletteShownFor;
        float runPaletteAt = -1;
        public bool RunPaletteShowing => runPaletteCard && runPaletteCard.gameObject.activeSelf;

        void BuildRunPalette()
        {
            runPaletteCard = Rect("Run palette", combatArtwork, 0, 0, 1670, 942);
            CombatArt("heading-plaque", runPaletteCard, "reveal-heading", 691, 248, 289, 67, contain: false);
            CombatWords("출발 팔레트", runPaletteCard, "reveal-heading", 835, 254, 360, 55, 35, true);
            CombatArt("chevron", runPaletteCard, "reveal-heading", 720, 271, 9, 15);
            var right = CombatArt("chevron", runPaletteCard, "reveal-heading", 940, 271, 9, 15);
            right.rectTransform.localScale = new Vector3(-1, 1, 1); right.rectTransform.anchoredPosition += new Vector2(9, 0);
            runPaletteNames = new Text[2];
            for (int w = 0; w < 2; w++)
            {
                string group = "reveal-" + w; float x = 516 + w * 325;
                CombatArt("run-card", runPaletteCard, group, x, 332, 313, 201, contain: false);
                CombatArt("weapon-disc", runPaletteCard, group, x + 112, 290, 90, 90);
                runPaletteWeapons[w] = CombatArt("state-empty-weapon", runPaletteCard, group, x + 131, 309, 52, 52);
                runPaletteNames[w] = CombatWords("", runPaletteCard, group, x + 156.5f, 376, 290, 54, 34, true, JInk);
                CombatArt("divider", runPaletteCard, group, x + 42, 423, 232, 4, contain: false);
                for (int j = 0; j < 5; j++) runPaletteDrops[w, j] = CombatArt("slot-empty", runPaletteCard, group, 0, 444, 49, 64, "ink-" + j);
                var corner = CombatArt("corner-accent", runPaletteCard, group, x + (w == 0 ? 10 : 271), 491, 31, 31);
                if (w == 1) { corner.rectTransform.localScale = new Vector3(-1, 1, 1); corner.rectTransform.anchoredPosition += new Vector2(31, 0); }
            }
            // Only on the first-run guide's run, where the card waits to be closed.
            runPaletteKeyRow = Rect("Card close prompt", runPaletteCard, 835 - 90, CardKeyRowY, 180, CardKeyH);
            runPaletteKey = Glyph(ForestCombatV2.KeycapMarker.SpriteFor('F'), runPaletteKeyRow, 0, 0, CardKeyW, CardKeyH, Color.white);
            runPaletteKey.preserveAspect = true;
            runPaletteHint = Words("계속", runPaletteKeyRow, CardKeyW + 12, 0, 110, CardKeyH, 32, true, JInk, TextAnchor.MiddleLeft);
            runPaletteKeyRow.gameObject.SetActive(false);
            runPaletteCard.gameObject.SetActive(false);
        }

        void RefreshRunPalette()
        {
            if (!runPaletteCard) return;
            var growth = shop ? shop.Growth : null;
            if (growth != null && growth != runPaletteShownFor && run && run.Run != null)
            {
                var first = growth.Equipped(shop.GrowthWeapon(0)); var second = growth.Equipped(shop.GrowthWeapon(1));
                if (first != null && second != null)
                {
                    runPaletteShownFor = growth; runPaletteAt = Time.unscaledTime; departureCardDismissed = false;
                    var palettes = new[] { first, second };
                    for (int w = 0; w < 2; w++)
                    {
                        var weapon = CombatWeapon(w); var skill = CombatSkill(w, weapon);
                        runPaletteNames[w].text = skill?.presentationName ?? host.WeaponName(w);
                        runPaletteWeapons[w].sprite = CombatSprite(skill?.presentationIcon ?? weapon?.icon);
                        int n = Mathf.Clamp(palettes[w].Slots.Count, 3, 5); float dw = n == 5 ? 36 : n == 4 ? 43 : 49, gap = n == 5 ? 49 : n == 4 ? 60 : 70;
                        for (int j = 0; j < 5; j++)
                        {
                            var drop = runPaletteDrops[w, j]; drop.gameObject.SetActive(j < n);
                            if (j >= n) continue;
                            string colour = palettes[w].Slots[j].Color;
                            drop.sprite = CombatSprite("drop-" + colour); drop.name = "Plain drop " + colour;
                            CombatPlace(drop.rectTransform, "reveal-" + w + ".ink-" + j, 516 + w * 325 + 156.5f + (j - (n - 1) / 2f) * gap - dw / 2, 444, dw, dw * 64 / 49);
                        }
                    }
                }
            }
            bool guideRun = FirstRunGuideRun;
            bool show = runPaletteAt >= 0 && (guideRun ? !departureCardDismissed : Time.unscaledTime - runPaletteAt < RunPaletteSeconds);
            LockDepartureCard(show && guideRun);
            if (runPaletteKeyRow)
            {
                runPaletteKeyRow.gameObject.SetActive(show && guideRun);
                if (show && guideRun)
                {
                    // A rebound key without keycap art falls back to its label in brackets.
                    string label = KeyLabel(Deviant.Input.Contracts.GameAction.Interact);
                    var art = label.Length == 1 ? ForestCombatV2.KeycapMarker.SpriteFor(label[0]) : null;
                    runPaletteKey.enabled = art; if (art) runPaletteKey.sprite = art;
                    runPaletteHint.text = art ? "계속" : $"[{label}] 계속";
                    float bob = Mathf.Round((Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f) * .5f + .5f) * CardKeyBob);
                    runPaletteKey.rectTransform.anchoredPosition = new Vector2(0, bob);
                }
            }
            runPaletteCard.gameObject.SetActive(show);
            if (show) runPaletteCard.SetAsLastSibling();
        }
    }
}
