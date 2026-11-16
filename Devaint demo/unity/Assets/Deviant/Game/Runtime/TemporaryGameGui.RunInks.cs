using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Run;
using Deviant.Control.Contracts;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        SketchEquipment Sketch => host && host.Arena ? host.Arena.Sketch : null;
        Guid sketchSelectedWeapon;
        string sketchSelectedInk;
        RectTransform sketchRewardPanel;
        RectTransform sketchRewardContent;
        IControlScope sketchRewardScope;
        SketchChoice sketchClaimed;
        string sketchRewardKey;
        ForestCombatV2.SketchRunBinding sharedSketchBinding;
        bool SketchRewardShowing => !SketchBinding && Sketch != null && (Sketch.PendingReward || sketchClaimed != null) && !Presenter.IsOpen;
        readonly Text[] sketchDurabilityTexts = new Text[2];
        bool SketchDurabilityNumbers => ForestCombatV2.SketchRunBinding.DurabilityAsNumber;
        readonly Dictionary<string, Sprite> runInkSprites = new Dictionary<string, Sprite>();
        Sprite RunInkIcon(RunInkDefinition ink)
        {
            if (ink == null) return null;
            if (string.IsNullOrEmpty(ink.icon)) return CombatSprite("drop-" + ink.color);
            if (runInkSprites.TryGetValue(ink.icon, out var icon)) return icon;
            icon = Resources.Load<Sprite>(ink.icon) ?? CombatSprite("drop-" + ink.color);
            runInkSprites[ink.icon] = icon;
            return icon;
        }
        void RefreshSketchDurability(int slot, RunInkWeapon weapon)
        {
            if (Sketch == null) return;
            if (!sketchDurabilityTexts[slot])
            {
                float x = 72 + slot * 370;
                sketchDurabilityTexts[slot] = CombatWords("", combatWeaponRoots[slot], "weapon-" + slot, x - 10, 919, 120, 22, 16, true, CombatIvory);
            }
            bool numbers = SketchDurabilityNumbers;
            // 게이지 방식은 무기 원 바깥 테두리의 도트 원호(ShowDurabilityArc)로 보인다.
            // 맨손 칸은 빈 원호(채움 없음, 사용자 2026-11-13 「맨손은 게이지 텅텅」). 숫자 방식이면 원호를 숨긴다.
            ShowDurabilityArc(slot, numbers ? (float?)null : weapon != null ? weapon.Durability / Mathf.Max(1, weapon.Maximum) : 0f);
            sketchDurabilityTexts[slot].gameObject.SetActive(weapon != null && numbers);
            if (weapon != null) sketchDurabilityTexts[slot].text = Mathf.CeilToInt(weapon.Durability).ToString();
        }
        string SketchWeaponName(RunInkWeapon weapon)
        {
            if (weapon == null) return "맨손";
            var form = host.Arena.catalog.forms.First(f => f.id == weapon.Form);
            return form.name + " / " + form.specials[weapon.Skill].name;
        }
        string SketchComposition(RunInkWeapon weapon) => string.Join(" / ", weapon.Slots.Select(s => s.Color == "neutral" ? "무색" : ForestCombatV2.Chapter1MaintenanceShop.ColorName(s.Color) + " 강화"));
        void SketchTrade(EquipmentBook b, Action action)
        {
            try { action(); host.Arena.SyncSketchLoadout(); b.Message = ""; }
            catch (InvalidOperationException e) { b.Message = e.Message; }
            b.Key = null;
        }
        void RefreshSketchBook(EquipmentBook b)
        {
            if (!b.Shop) { RefreshApprovedSketchBook(b); return; }
            if (b.Shop ? shop == null || !shop.IsOpen : !Presenter.IsOpen) { b.Key = null; return; }
            var equipment = Sketch;
            if (equipment == null) return;
            if (equipment.RemainingInkUses(sketchSelectedInk) == 0) sketchSelectedInk = null;
            for (int i = 0; i < b.Tabs.Length; i++)
            {
                bool visible = i < (b.Shop ? 2 : 1); b.Tabs[i].gameObject.SetActive(visible);
                if (visible) { b.Tabs[i].GetComponentInChildren<Text>().text = i == 0 ? (b.Shop ? "상점" : "무기 / 잉크 도감") : "증강"; RefreshLiveTab(b.Tabs[i], i == b.Tab); }
            }
            if (b.Tab != 0 && !(b.Shop && b.Tab == 1)) b.Tab = 0;
            RefreshApprovedShopHeader(b);
            var keyboardHelp=b.Root.transform.Find("Shop keyboard help");if(keyboardHelp)keyboardHelp.GetComponent<Text>().text=b.Tab==1?"방향키  증강 선택     Esc  닫기     Q / E  탭     W / S  설명 스크롤":"방향키  상품 선택     F  구매     Esc  닫기     Q / E  탭     W / S  설명 스크롤";
            string key = $"sketch:{equipment.Revision}/{equipment.Economy.Gold}/{b.Tab}/{sketchSelectedWeapon}/{sketchSelectedInk}/{b.Slot}/{Mathf.CeilToInt(equipment.ChangeCooldownRemaining)}/{b.Message}/{b.Offer}/{liveShopReceipt?.Weapon?.Id}/{liveShopReceipt?.Ink?.id}/{liveAction}/{liveAugmentPick}/{NativeGrowth.Revision}";
            if (b.Key == key) return;
            b.Key = key; b.Gold.text = equipment.Economy.Gold.ToString(); ClearGrowth(b.Body);
            if (b.Shop && b.Tab == 1)
            {
                DrawApprovedOwnedAugments(b);
                return;
            }
            if (b.Shop) { SketchShop(b, equipment); return; }
            Words("장착 무기", b.Body, 32, 26, 348, 22, 16, true);
            for (int i = 0; i < 2; i++)
            {
                int slot = i; var equipped = equipment.Equipped(i);
                NativeButton(b, "Sketch equipped " + i, (i == 0 ? "A  " : "B  ") + (equipped == null ? "빈 슬롯" : SketchWeaponName(equipped)), b.Body, 32, 54 + i * 40, 348, 34,
                    () => { if (equipped != null) sketchSelectedWeapon = equipped.Id; b.Slot = 0; });
            }
            Words("보유 무기 / 보관 한도 없음", b.Body, 32, 140, 348, 20, 13, true);
            var storage = JournalScroll("Sketch weapons", b.Body, 32, 168, 348, 90); int row = 0;
            foreach (var weapon in equipment.Weapons.Where(w => !w.Destroyed))
            {
                var copy = weapon;
                NativeButton(b, "Sketch weapon " + weapon.Id, SketchWeaponName(weapon) + (weapon.Confirmed ? "" : " [미확정]"), storage, 0, row++ * 34, 330, 30,
                    () => { sketchSelectedWeapon = copy.Id; b.Slot = 0; });
            }
            storage.sizeDelta = new Vector2(348, Math.Max(90, row * 34));
            Words("런 잉크 도감  " + equipment.RegisteredCount + " / " + equipment.Catalog.inks.Length, b.Body, 420, 26, 348, 22, 16, true);
            NativeButton(b, "Open banked sketch reward", "받아 둔 보상 " + equipment.Banked, b.Body, 420, 2, 250, 24,
                () => { Presenter.Close(); if (SketchBinding) SketchBinding.TryOpenBankedReward(); else equipment.OpenReward(); }, equipment.Banked > 0 && !equipment.PendingReward);
            NativeIcon(BossIcon, b.Body, 696, 4, 18, 18);
            Words(equipment.Banked.ToString(), b.Body, 720, 0, 48, 24, 14, true);
            var catalog = JournalScroll("Run ink catalog", b.Body, 420, 54, 348, 202); row = 0;
            foreach (var ink in equipment.Registered)
            {
                var copy = ink; int top = row++ * 52;
                var button = NativeButton(b, "Run ink " + ink.id, ink.name, catalog, 34, top, 296, 30, () => sketchSelectedInk = copy.id);
                Words($"남은 {equipment.RemainingInkUses(ink.id)}회 / 예약 {equipment.ReservedInkUses(ink.id)}회 / 부여 가능 {equipment.AvailableInkUses(ink.id)}회", catalog, 34, top + 30, 296, 20, 11, false, JMuted);
                NativeIcon(RunInkIcon(ink), catalog, 0, top, 28, 28);
            }
            catalog.sizeDelta = new Vector2(348, Math.Max(202, row * 52));
            if (row == 0) Words("처치 보상과 상점에서 잉크를 획득하세요.", catalog, 0, 8, 330, 46, 13, false, JMuted);
            var selected = equipment.Find(sketchSelectedWeapon);
            if (selected == null || selected.Destroyed) selected = equipment.Weapons.FirstOrDefault(w => !w.Destroyed);
            if (selected != null)
            {
                sketchSelectedWeapon = selected.Id; var w = selected;
                Words(SketchWeaponName(w), b.Body, 32, 268, 348, 24, 14, true);
                Words($"내구도 {Mathf.CeilToInt(w.Durability)} / {w.Maximum:0}  /  " + (w.Confirmed ? "구성 확정" : "임시 구성"), b.Body, 32, 294, 348, 18, 12, false, JMuted);
                b.Slot = Mathf.Clamp(b.Slot, 0, w.Slots.Count - 1);
                for (int i = 0; i < w.Slots.Count; i++)
                {
                    int slot = i; var mount = w.Slots[i]; float x = 32 + i * 58;
                    var button = NativeButton(b, "Sketch mount " + i, (i + 1).ToString(), b.Body, x, 318, 52, 42, () => b.Slot = slot, primary: b.Slot == i);
                    if (mount.Ink != null) NativeIcon(RunInkIcon(mount.Ink), button.transform, 18, 5, 28, 28);
                    Words(mount.Color == "neutral" ? "무색" : ForestCombatV2.Chapter1MaintenanceShop.ColorName(mount.Color) + "↑", b.Body, x, 360, 52, 16, 10, false, JMuted);
                }
                if (!w.Confirmed)
                {
                    NativeButton(b, "Place run ink", "선택 잉크 배치", b.Body, 32, 382, 128, 32,
                        () => SketchTrade(b, () => equipment.Place(w.Id, b.Slot, sketchSelectedInk)), equipment.AvailableInkUses(sketchSelectedInk) > 0);
                    NativeButton(b, "Clear draft mount", "비우기", b.Body, 164, 382, 70, 32, () => SketchTrade(b, () => equipment.Place(w.Id, b.Slot, null)));
                    NativeButton(b, "Confirm sketch", "구성 확정", b.Body, 240, 382, 140, 32, () => SketchTrade(b, () => equipment.Confirm(w.Id)), primary: true);
                }
                else if (equipment.Equipped(0) != w && equipment.Equipped(1) != w)
                {
                    for (int i = 0; i < 2; i++) { int slot = i; NativeButton(b, "Equip sketch " + i, (i == 0 ? "A" : "B") + "에 장착", b.Body, 32 + i * 80, 382, 76, 32, () => SketchTrade(b, () => equipment.Equip(w.Id, slot)), equipment.State.Arsenal.CanEquip(w.Id, slot, out _)); }
                    NativeButton(b, "Discard sketch", "버리기", b.Body, 192, 382, 76, 32, () => SketchTrade(b, () => equipment.Discard(w.Id)));
                    NativeButton(b, "Skip sketch equip", "장착 건너뛰기", b.Body, 272, 382, 108, 32, () => Presenter.Close());
                }
                var selectedMount = w.Slots[b.Slot];
                Words("선택 칸: " + (selectedMount.Ink?.name ?? "빈 슬롯") + (selectedMount.Ink != null && selectedMount.Color == selectedMount.Ink.color ? " / 강화 수치" : ""), b.Body, 420, 330, 348, 34, 12, true);
                Words(w.Confirmed ? "빈 슬롯을 포함한 구성이 고정되어 수정할 수 없습니다." : "칸마다 1회 예약 / 확정 시 소비합니다.", b.Body, 420, 370, 348, 38, 12, false, JMuted);
            }
            var pickedInk = equipment.Catalog.Find(sketchSelectedInk);
            if (pickedInk != null) { Words(pickedInk.name, b.Body, 420, 266, 348, 24, 14, true); Words(pickedInk.description, b.Body, 420, 292, 348, 42, 12); }
            if (equipment.State.Arsenal.EquipLockedByCooldown) Words($"장착 대기 {equipment.ChangeCooldownRemaining:0.0}초", b.Body, 32, 420, 348, 18, 11, false, JMuted);
            if (!string.IsNullOrEmpty(b.Message)) Words(b.Message, b.Body, 420, 416, 348, 22, 11, false, JMuted);
        }
        void SketchShop(EquipmentBook b, SketchEquipment equipment) => DrawApprovedSketchShop(b, equipment);
        void RefreshSketchRewards()
        {
            var equipment = Sketch;
            var binding = SketchBinding;
            if (binding && equipment != null)
            {
                if (sharedSketchBinding != binding)
                {
                    sharedSketchBinding = binding;
                    binding.SharedBagOpen = () => Presenter.IsOpen;
                    binding.SharedBagVisibility = visible => { if (visible) Presenter.Open(); else Presenter.Close(); };
                    binding.SharedBagSelection = SelectLiveReceipt;
                    binding.SharedRewardBackdrop = () => this && isActiveAndEnabled && dim && dim.activeInHierarchy;
                }
                sketchRewardScope?.Dispose(); sketchRewardScope = null;
                if (sketchRewardPanel) sketchRewardPanel.gameObject.SetActive(false);
            }
            if (equipment == null) { sketchRewardScope?.Dispose(); sketchRewardScope = null; if (sketchRewardPanel) sketchRewardPanel.gameObject.SetActive(false); return; }
            if (binding) return;
            bool pending = equipment.PendingReward || sketchClaimed != null;
            if (pending && sketchRewardScope == null)
            { sketchRewardScope = host.Arena.Controls.OpenScope("run ink reward"); sketchRewardScope.BlockInput(InputChannels.Combat | InputChannels.Interaction); sketchRewardScope.PauseSimulation(); }
            if (!pending) { sketchRewardScope?.Dispose(); sketchRewardScope = null; sketchRewardKey = null; }
            if (!sketchRewardPanel)
            {
                sketchRewardPanel = Rect("Sketch reward choice", stage, BookX, BookY, BookW, BookH);
                var anchor = Action("", sketchRewardPanel, 0, 0, 1, 1, () => { });
                anchor.gameObject.SetActive(false);
                sketchRewardContent = Rect("Reward content", sketchRewardPanel, 0, 0, BookW, BookH);
                RegisterScreen(sketchRewardPanel.gameObject, anchor);
            }
            bool shown = pending && !Presenter.IsOpen; sketchRewardPanel.gameObject.SetActive(shown);
            if (!shown) return;
            sketchRewardPanel.SetAsLastSibling();
            string key = equipment.Revision + "/" + (sketchClaimed?.Weapon?.Id.ToString() ?? sketchClaimed?.Ink?.id ?? "offer");
            if (key == sketchRewardKey) return; sketchRewardKey = key; ClearGrowth(sketchRewardContent);
            NativeArt("WorkshopFrames/book-800x440", sketchRewardContent, 0, 0, BookW, BookH);
            var state = new EquipmentBook { Body = sketchRewardContent };
            if (sketchClaimed == null) Words("보상 하나를 선택하세요", sketchRewardContent, 32, 28, 736, 36, 22, true);
            if (sketchClaimed == null)
            {
                var choices = equipment.RewardChoices();
                for (int i = 0; i < choices.Count; i++)
                {
                    int index = i; var choice = choices[i]; float x = 32 + i * 250;
                    Words(choice.Weapon != null ? SketchWeaponName(choice.Weapon) : $"{choice.Ink.name} / +{equipment.Catalog.inkUsesPerAcquisition}회", sketchRewardContent, x, 98, 232, 54, 16, true);
                    if (choice.Ink != null) NativeIcon(RunInkIcon(choice.Ink), sketchRewardContent, x + 88, 158, 56, 56);
                    Words(choice.Weapon != null ? SketchComposition(choice.Weapon) : choice.Ink.description, sketchRewardContent, x, 226, 232, 86, 13);
                    NativeButton(state, "Select sketch reward " + i, "선택", sketchRewardContent, x, 330, 232, 44,
                        () => { equipment.ChooseReward(index); sketchClaimed = null; sketchRewardKey = null; });
                }
                Words("Tab으로 인벤토리를 열어 비교할 수 있습니다.", sketchRewardContent, 32, 394, 736, 24, 13, false, JMuted);
            }
            else
            {
                var choice = sketchClaimed;
                Words(choice.Weapon != null ? SketchWeaponName(choice.Weapon) : $"{choice.Ink.name} / +{equipment.Catalog.inkUsesPerAcquisition}회", sketchRewardContent, 32, 120, 736, 48, 20, true);
                Words(choice.Weapon != null ? "미확정 무기를 획득했습니다." : $"잉크 부여 횟수 +{equipment.Catalog.inkUsesPerAcquisition}회 / 무기 슬롯마다 1회 사용합니다.", sketchRewardContent, 32, 190, 736, 54, 15);
                NativeButton(state, "Continue sketch reward", choice.Weapon != null ? "가방에 넣고 계속" : "계속", sketchRewardContent, 80, 320, 300, 48,
                    () => { sketchClaimed = null; sketchRewardKey = null; });
                NativeButton(state, "Configure sketch reward", choice.Weapon != null ? "지금 구성하기" : "지금 부여하기", sketchRewardContent, 420, 320, 300, 48,
                    () => { SelectLiveReceipt(choice.Weapon?.Id ?? Guid.Empty, choice.Ink?.id); sketchClaimed = null; sketchRewardKey = null; Presenter.Open(); });
            }
            var first = sketchRewardPanel.GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable);
            sketchRewardPanel.gameObject.SetActive(true);
            if (first && EventSystem.current) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }
}
