using System;
using System.Linq;
using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        readonly Button[] bagPaletteButtons = new Button[2];
        readonly Text[] bagPaletteCounts = new Text[2];
        bool bagInspectPalette, growthInspectPalette;
        Button growthPaletteButton;
        Text growthPaletteCount;

        Button PaletteInfoButton(Transform parent, float x, float y, float width, Action pressed, out Text count)
        {
            var image = Rect("Palette information", parent, x, y, width, 28).gameObject.AddComponent<Image>();
            image.color = Color.clear;
            var button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { if (button.interactable) { pressed(); Refresh(); } });
            Words("팔레트", image.transform, 0, 0, 100, 28, 18, true);
            count = Words("", image.transform, 100, 0, width - 100, 28, 13, true, JMuted, TextAnchor.MiddleRight);
            return button;
        }

        static string PaletteSummary(GrowthPalette palette) =>
            $"<b>{palette.Slots.Count}칸 / 장착 {palette.Slots.Count(s => s.Part != null)}/{palette.Slots.Count}</b>\n" +
            string.Join(" / ", palette.Slots.Select(s => ForestCombatV2.Chapter1MaintenanceShop.ColorName(s.Color) + " +" + s.Level));

        void SelectBagPalette(int weapon)
        {
            Presenter.SelectWeapon(weapon); Presenter.SetFilter(Presenter.Filter);
            bagColorSelected = Guid.Empty; bagPaletteSlot = -1; bagDetailMounted = false; bagInspectPalette = true;
        }

        void SelectGrowthPalette()
        {
            growthInspectPalette = true; growthInspectMounted = true;
            growthSelectedInk = Guid.Empty; growthSlotPicked = growthPickSlot = false; growthDirty = true;
        }

        void ShowPaletteInfo(Text title, Text body, Image icon, GrowthPalette palette, int weapon)
        {
            title.text = host.WeaponName(weapon) + " 팔레트";
            PaintInkDetailGrade(title, null);
            body.text = Deviant.UI.Theme.ApprovedInkText.Display(PaletteSummary(palette));
            icon.sprite = CombatSprite("pigment-currency"); icon.enabled = icon.sprite; icon.color = Color.white;
        }
    }
}
