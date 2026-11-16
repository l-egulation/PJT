using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // The bag tells what each weapon's Q/W skill does (ui-and-interaction.md 가방): the empty 특수 slot shows the
    // current special (skill stamps included) and, while nothing is picked, the detail page describes it.
    public sealed partial class TemporaryGameGui
    {
        Image bagSkillBacking, bagSkillGlyph;
        Deviant.Combat.SkillData BagSkill(int w)
        {
            var player = host.Arena.Player;
            if (!player.IsWeaponSlotReady(w)) return null;
            var state = player.weapons[w]; var form = host.Arena.catalog.forms[state.FormIndex];
            return form.specials != null && state.SpecialIndex >= 0 && state.SpecialIndex < form.specials.Length ? form.specials[state.SpecialIndex] : null;
        }
        // The HUD glyph is ivory line art for the dark HUD card; on paper it is drawn as the maintenance 스킬 slot does,
        // inked (JInk) over the skill stamp backing.
        Sprite BagSkillGlyph(int w)
        {
            var state = host.Arena.Player.weapons[w];
            return GrowthPreviewIcon(host.Arena.catalog.forms[state.FormIndex].id, state.SpecialIndex);
        }
        // The backing and glyph keep the maintenance slot's proportions: a 34 px glyph 17, 27 px into a 68 px backing.
        void PaintSkillStamp(Image backing, Image glyph, float x, float y, float size, Sprite sprite)
        {
            backing.sprite = ItemIcon("backing-stamp-skill"); backing.preserveAspect = true; backing.raycastTarget = false;
            Place(backing, x, y, size, size);
            glyph.sprite = sprite; glyph.preserveAspect = true; glyph.raycastTarget = false; glyph.color = JInk;
            Place(glyph, x + size / 4, y + size * 27 / 68, size / 2, size / 2);
        }
        Image ChildImage(Transform parent, string name)
        {
            var found = parent.Find(name); if (found) return found.GetComponent<Image>();
            return Rect(name, parent, 0, 0, 1, 1).gameObject.AddComponent<Image>();
        }
        // The cooldown the HUD will count: the authored seconds after this run's cooldown reduction.
        string BagSkillReadout(int w, Deviant.Combat.SkillData skill)
        {
            float period = host.Arena.Player.weapons[w].Cooldown.Period; if (period <= 0) period = skill.cooldown;
            string seconds = Mathf.Approximately(period, Mathf.Round(period)) ? Mathf.RoundToInt(period).ToString() : period.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            return $"{KeyLabel(w == 0 ? GameAction.Weapon1Special : GameAction.Weapon2Special)} 키   쿨타임 {seconds}초";
        }
        // The 특수 slot: the inked skill stamp and the skill name in place of 미장착 while no legacy special ink is in it.
        void PaintBagSkillSlot(Button button, Text label, int w, bool empty)
        {
            var backing = ChildImage(button.transform, "Skill backing"); var glyph = ChildImage(backing.transform.parent, "Skill glyph");
            var skill = empty ? BagSkill(w) : null; var sprite = skill != null ? BagSkillGlyph(w) : null;
            bool show = skill != null && sprite;
            backing.gameObject.SetActive(show); glyph.gameObject.SetActive(show);
            if (!show) return;
            button.image.color = Color.white; button.transform.Find("Item").GetComponent<Image>().enabled = false;
            PaintSkillStamp(backing, glyph, 8, Mathf.Round((((RectTransform)button.transform).sizeDelta.y - 44) / 2), 44, sprite);
            label.text = skill.name;
        }
        bool ShowBagSkill(int w)
        {
            if (!bagSkillBacking)
            {
                bagSkillBacking = ChildImage(bagPage, "Bag skill backing"); bagSkillGlyph = ChildImage(bagPage, "Bag skill glyph");
            }
            var skill = BagSkill(w); var sprite = skill != null ? BagSkillGlyph(w) : null;
            bool show = skill != null && sprite;
            bagSkillBacking.gameObject.SetActive(show); bagSkillGlyph.gameObject.SetActive(show);
            if (!show) return false;
            // Key and cooldown ride on the title line in a smaller muted size, so the line itself keeps the scroll-free rows.
            detailName.text = $"{skill.name}   <size=12><color=#{ColorUtility.ToHtmlStringRGB(JMuted)}>{BagSkillReadout(w, skill)}</color></size>";
            PaintInkDetailGrade(detailName, null);
            detail.text = skill.summary ?? "";
            detailDrop.enabled = false; detailPreview.text = "";
            PaintSkillStamp(bagSkillBacking, bagSkillGlyph, 416, 332, 48, sprite);
            return true;
        }
        void HideBagSkill()
        {
            if (bagSkillBacking) { bagSkillBacking.gameObject.SetActive(false); bagSkillGlyph.gameObject.SetActive(false); }
        }
    }
}
