using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Inventory;
using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        sealed class BagEntry { public int Legacy=-1; public GrowthInk Color; }
        readonly List<BagEntry> bagEntries=new List<BagEntry>();
        Guid bagColorSelected;
        int bagPaletteSlot=-1;
        // The detail page shows what was pressed last: a mounted palette ink or the picked bag ink (user 2026-11-09).
        bool bagDetailMounted;
        GrowthInk SelectedBagColor=>host.Growth?.Inks.FirstOrDefault(i=>i.Id==bagColorSelected);
        void RebuildBagEntries()
        {
            bagEntries.Clear();
            // Legacy inks and colour inks fill the same bag, so the entries never outnumber its cells.
            foreach(InkKind kind in Enum.GetValues(typeof(InkKind)))
            {
                foreach(var index in host.Inventory.VisibleSlots(kind))bagEntries.Add(new BagEntry{Legacy=index});
                if(kind==InkKind.Stat&&host.Growth!=null)foreach(var ink in host.Growth.Inks)bagEntries.Add(new BagEntry{Color=ink});
            }
        }
        void SelectBagEntry(int cell)
        {
            if(cell>=bagEntries.Count)return;var entry=bagEntries[cell];
            bagInspectPalette=false;
            // A colour ink keeps the palette slot already picked, so slot → ink → 장착 works in that order (user 2026-11-05).
            if(entry.Color==null)bagPaletteSlot=-1;bagColorSelected=entry.Color?.Id??Guid.Empty;bagColorDestroyArmed=false;bagDetailMounted=false;
            if(entry.Color!=null)Presenter.SetFilter(Presenter.Filter);else Presenter.SelectBag(entry.Legacy);
            Refresh();
        }
        void SelectBagEquipment(int weapon,int slot)
        {
            bagInspectPalette=false;
            if(host.Growth!=null&&slot>=1&&slot<=5)
            {
                // Picking a slot only selects it; nothing is mounted until 장착 is pressed.
                Presenter.SelectWeapon(weapon);bagPaletteSlot=slot-1;bagDetailMounted=true;Presenter.SetFilter(Presenter.Filter);
            }
            else {bagColorSelected=Guid.Empty;bagPaletteSlot=-1;Presenter.SelectEquipment(weapon,slot);}
            Refresh();
        }
        // The player picks the target slot; there is no automatic choice (-1 until a slot is picked).
        int SuggestedBagColorSlot()=>bagPaletteSlot;
        GrowthSlot BagColorTarget()
        {
            var palette=host.Growth?.Equipped(host.Arena.Player.weapons[Presenter.SelectedWeapon].InstanceId);int slot=SuggestedBagColorSlot();
            return palette!=null&&slot>=0&&slot<palette.Slots.Count?palette.Slots[slot]:null;
        }
        bool CanEquipBagColor()
        {
            var ink=SelectedBagColor;var palette=host.Growth?.Equipped(host.Arena.Player.weapons[Presenter.SelectedWeapon].InstanceId);int slot=SuggestedBagColorSlot();
            if(ink==null||palette==null||slot<0||slot>=palette.Slots.Count)return false;
            var target=palette.Slots[slot];
            return (target.Color=="colorless"||target.Color==ink.Part.color)&&(ink.Part.duplicate||!palette.Slots.Where((s,i)=>i!=slot).Any(s=>s.Part==ink.Part));
        }
        void EquipBagSelected()
        {
            if(nativeBag!=null){NativeEquip(nativeBag);return;}
            if(bagColorSelected==Guid.Empty){PlayEquipResult(Presenter.EquipSelected());return;}
            int slot=SuggestedBagColorSlot();if(slot<0){Presenter.Notify(PickSlotFirst);PlayEquipResult(false);return;}
            bool replacing=BagColorTarget()?.Part!=null;
            bool done=host.TryEquipColor(bagColorSelected,Presenter.SelectedWeapon,slot,out var error);
            Presenter.Notify(done?replacing?"교체했습니다. 기존 잉크는 가방으로 돌아갔습니다.":"장착했습니다.":error);
            PlayEquipResult(done);
            if(done){bagColorSelected=Guid.Empty;bagPaletteSlot=slot;bagDetailMounted=true;}
        }
        // A selected mounted palette slot (and no bag ink picked) unequips its ink back to the bag; the slot stays selected.
        void UnequipBagSelected()
        {
            if(bagPaletteSlot<0||bagColorSelected!=Guid.Empty){Presenter.UnequipSelected();return;}
            bool done=host.TryUnequipColor(Presenter.SelectedWeapon,bagPaletteSlot,out var error);
            Presenter.Notify(done?"해제했습니다. 잉크는 가방으로 돌아갔습니다.":error);
            PlayEquipResult(done);
        }
        const string PickSlotFirst="장착할 팔레트 칸을 먼저 누르세요.";
        static void PlayEquipResult(bool accepted) => ForestCombatV2.PresentationSfx.PlaySelected(
            accepted ? "sfx_items_equip" : "sfx_ui_reject");

        // Colour inks are destroyed from the bag too, with the same second press as the bag's own destroy.
        bool bagColorDestroyArmed;
        void DestroyBagColor()
        {
            if(!bagColorDestroyArmed){bagColorDestroyArmed=true;return;}
            bagColorDestroyArmed=false;
            bool done=host.TryDestroyColor(bagColorSelected,out var error);
            Presenter.Notify(done?"파괴했습니다.":error);
            if(done){bagColorSelected=Guid.Empty;ForestCombatV2.PresentationSfx.Play(ForestCombatV2.PresentationSfx.InkDestroy);}
        }
        void SetInkGrade(Image icon, int? grade)
        {
            var badge = icon.transform.Find("Ink grade")?.GetComponent<Image>();
            var emblem = grade.HasValue && icon.enabled ? Art($"parts/ink-grade-{grade.Value}") : null;
            if (!emblem)
            {
                if (badge) badge.gameObject.SetActive(false);
                return;
            }

            if (!badge)
            {
                var rect = Rect("Ink grade", icon.transform, 0, 0, 18, 22);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(6, 0);
                badge = rect.gameObject.AddComponent<Image>();
                badge.raycastTarget = false;
            }

            badge.sprite = emblem;
            badge.gameObject.SetActive(true);
        }

        void SetSlotLevel(Transform cell, int? value)
        {
            var badge = cell.Find("Slot level");
            if (!value.HasValue)
            {
                if (badge) badge.gameObject.SetActive(false);
                return;
            }

            const float width = 20, height = 16;
            if (!badge)
            {
                var image = Rounded("Slot level", cell, 0, 0, width, height, 3, 1, JPaperText, JRule);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-3, -3);
                Words("", rect, 0, 0, width, height, 12, true, JInk, TextAnchor.MiddleCenter);
                badge = rect;
            }
            badge.gameObject.SetActive(true);
            badge.GetComponentInChildren<Text>().text = $"+{value.Value}";
        }

        void PaintBagColor(Button button,GrowthPart part,string color,int? level,bool selected)
        {
            SetBagSlot(button,selected);button.image.color=Color.white;
            var icon=button.transform.Find("Item").GetComponent<Image>();icon.sprite=DropArt(part,color);icon.enabled=icon.sprite;
            icon.color=part==null?new Color(1,1,1,.4f):Color.white;
            var size=((RectTransform)button.transform).sizeDelta;float w=Mathf.Min(48,size.x-4),h=Mathf.Min(level.HasValue?40:52,size.y-8);
            Place(icon,Mathf.Round((size.x-w)/2),level.HasValue?8:4,w,h);
            SetInkGrade(icon, level.HasValue ? null : part?.grade);
            SetSlotLevel(button.transform, level);
        }
    }
}
