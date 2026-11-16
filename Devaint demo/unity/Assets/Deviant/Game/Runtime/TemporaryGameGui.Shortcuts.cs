using System;
using System.Collections.Generic;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        readonly struct ShortcutRow
        {
            public readonly GameAction Action;
            public readonly string Label, Direction;
            public ShortcutRow(GameAction action, string label, string direction = null)
            { Action = action; Label = label; Direction = direction; }
        }

        static readonly ShortcutRow[][] ShortcutPages =
        {
            new[]
            {
                new ShortcutRow(GameAction.Move, "왼쪽 이동", "left"),
                new ShortcutRow(GameAction.Move, "오른쪽 이동", "right"),
                new ShortcutRow(GameAction.Move, "벽 오르기", "up"),
                new ShortcutRow(GameAction.Move, "아래 방향 / 발판", "down"),
                new ShortcutRow(GameAction.Jump, "점프"),
                new ShortcutRow(GameAction.Dash, "대시"),
                new ShortcutRow(GameAction.SwapWeaponOrder, "무기 순서 교환"),
                new ShortcutRow(GameAction.Interact, "상호작용"),
                new ShortcutRow(GameAction.Weapon1Basic, "무기 1 기본공격"),
                new ShortcutRow(GameAction.Weapon2Basic, "무기 2 기본공격"),
                new ShortcutRow(GameAction.Weapon1Special, "무기 1 스킬"),
                new ShortcutRow(GameAction.Weapon2Special, "무기 2 스킬"),
                new ShortcutRow(GameAction.Inventory, "가방"),
                new ShortcutRow(GameAction.Map, "지도"),
                new ShortcutRow(GameAction.CheckpointInstall, "포탈 1 설치"),
                new ShortcutRow(GameAction.CheckpointReturn, "포탈 2 설치"),
                new ShortcutRow(GameAction.OpenReward, "받아 둔 보상 열기"),
            },
            new[]
            {
                new ShortcutRow(GameAction.Navigate, "메뉴 위", "up"),
                new ShortcutRow(GameAction.Navigate, "메뉴 아래", "down"),
                new ShortcutRow(GameAction.Navigate, "메뉴 왼쪽", "left"),
                new ShortcutRow(GameAction.Navigate, "메뉴 오른쪽", "right"),
                new ShortcutRow(GameAction.Submit, "메뉴 확인"),
                new ShortcutRow(GameAction.Cancel, "메뉴 뒤로"),
                new ShortcutRow(GameAction.Advance, "대화 / 수첩 진행"),
                new ShortcutRow(GameAction.Equip, "가방 장착"),
                new ShortcutRow(GameAction.Pause, "일시정지"),
                new ShortcutRow(GameAction.MapZoomIn, "지도 확대"),
                new ShortcutRow(GameAction.MapZoomOut, "지도 축소"),
                new ShortcutRow(GameAction.MapFocus, "지도 현재 위치"),
                new ShortcutRow(GameAction.MapFit, "지도 전체 범위"),
                new ShortcutRow(GameAction.Help, "조작 안내 (개발)"),
                new ShortcutRow(GameAction.RerollReward, "보상 리롤"),
                new ShortcutRow(GameAction.InventoryRemove, "잉크 칸 비우기 / 장비 해제"),
                new ShortcutRow(GameAction.InventoryRename, "무기 이름 변경"),
                // 전투 리셋은 치트 모드의 「전투 상태 초기화」로 옮겨 키로는 동작하지 않는다(debug-menu.md). 바꿀 수 있는 키로 두지 않는다.
            }
        };

        const string ShortcutHint = "항목을 선택하면 사용할 키를 바꿀 수 있습니다.";
        GameObject shortcutPanel;
        bool shortcutPanelOpen;
        readonly List<(GameAction action, int index, Text text)> shortcutKeys = new List<(GameAction, int, Text)>();
        Text shortcutStatus;
        Button shortcutCancel;
        LocalInput shortcutCapture;
        KeyControl shortcutRelease;
        ShortcutRow editingShortcut;
        int editingBinding = -1, captureStarted;
        GameObject shortcutResetQuestion, shortcutResetReturnFocus;
        ApprovedSettingsLayerOcclusion shortcutResetOcclusion;
        Action shortcutResetApply;
        int shortcutResetClosedFrame = -1, shortcutResetOpenedFrame;

        void OpenShortcuts()
        {
            OpenApprovedSettings();
            if(settingsTab!=2)RequestSettingsTab(2);
        }

        void RefreshShortcutKeys(LocalInput input)
        {
            if(!input)return;
            foreach (var slot in shortcutKeys)
            {
                string path = input.Source.GetAction(slot.action).bindings[slot.index].effectivePath;
                slot.text.text = slot.action == editingShortcut.Action && slot.index == editingBinding ? "새 키를 누르세요" : ShortcutKeyName(path);
            }
            RefreshInlineKeyRows();
        }

        static string ShortcutKeyName(string path)
        {
            switch (path)
            {
                case "<Keyboard>/leftArrow": return "←";
                case "<Keyboard>/rightArrow": return "→";
                case "<Keyboard>/upArrow": return "↑";
                case "<Keyboard>/downArrow": return "↓";
                case "<Keyboard>/escape": return "Esc";
                case "<Keyboard>/leftShift": return "좌 Shift";
                case "<Keyboard>/rightShift": return "우 Shift";
                case "<Keyboard>/numpadEnter": return "Num Enter";
                case "<Keyboard>/numpadPlus": return "Num +";
                case "<Keyboard>/numpadMinus": return "Num −";
                case null: case "": return "미지정";
                // The binding editor tells left from right; every other key uses the short keycap name of all screens.
                default: return LocalInput.KeyboardKeyName(path);
            }
        }

        void StartShortcutEdit(ShortcutRow row, int index)
        {
            EndShortcutEdit();
            shortcutCapture = Controls;
            shortcutCapture.Rebinding = true;
            editingShortcut = row;
            editingBinding = index;
            captureStarted = Time.frameCount;
            shortcutCancel.gameObject.SetActive(true);
            shortcutStatus.text = row.Label + "에 사용할 키를 누르세요.";
            RefreshShortcutKeys(shortcutCapture);
        }

        void TickShortcutEdit()
        {
            if (!shortcutCapture) return;
            if (shortcutRelease != null)
            {
                if (!shortcutRelease.device.added || !shortcutRelease.isPressed) EndShortcutEdit();
                return;
            }
            var keyboard = Keyboard.current;
            if (editingBinding < 0 || Time.frameCount == captureStarted || keyboard == null || !shortcutCapture.Source.IsFocused) return;
            if(keyboard.escapeKey.wasPressedThisFrame){CancelShortcutEdit();return;}
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                var input = shortcutCapture;
                if (!KeyboardShortcutSettings.TryAssign(input, editingShortcut.Action, editingBinding, key, out var conflict))
                {
                    shortcutStatus.text = ShortcutActionLabel(conflict) + "에 사용 중인 키입니다. 다른 키를 누르세요.";
                    return;
                }
                var action = input.Source.GetAction(editingShortcut.Action);
                var keyName = ShortcutKeyName(action.bindings[editingBinding].effectivePath);
                shortcutStatus.text = editingShortcut.Label + " / " + keyName + " 저장됨";
                editingBinding = -1;
                shortcutCancel.gameObject.SetActive(false);
                shortcutRelease = key;
                RefreshShortcutKeys(input);
                return;
            }
        }

        static string ShortcutActionLabel(GameAction action)
        {
            if (action == GameAction.Move) return "이동";
            if (action == GameAction.Navigate) return "메뉴 이동";
            foreach (var page in ShortcutPages)
                foreach (var row in page)
                    if (row.Action == action) return row.Label;
            return action.ToString();
        }

        int shortcutEditClosedFrame=-1;
        void CancelShortcutEdit()
        {
            shortcutEditClosedFrame=Time.frameCount;
            Controls?.SuppressGameplay();
            EndShortcutEdit();
            RefreshShortcutKeys(Controls);
            shortcutStatus.text = ShortcutHint;
        }

        void EndShortcutEdit()
        {
            if (shortcutCapture) shortcutCapture.Rebinding = false;
            shortcutCapture = null;
            shortcutRelease = null;
            editingBinding = -1;
            if (shortcutCancel) shortcutCancel.gameObject.SetActive(false);
        }

        void CloseShortcuts()
        {
            CloseShortcutResetQuestion(false);
            EndShortcutEdit();
            if(shortcutPanel)shortcutPanel.GetComponent<ApprovedSettingsLayerOcclusion>()?.Reveal();
            shortcutPanelOpen = false;
            if(shortcutPanel)shortcutPanel.SetActive(false);
        }
        void ApplyShortcutDefaults()
        {
            KeyboardShortcutSettings.RestoreDefaults(Controls);
            RefreshShortcutKeys(Controls);
            shortcutStatus.text = "기본 단축키로 복원했습니다.";
        }
        void OpenShortcutResetQuestion(Action restore)
        {
            if (shortcutResetQuestion || !shortcutPanelOpen || !shortcutPanel || !shortcutPanel.activeInHierarchy) return;
            if (restore == null) throw new ArgumentNullException(nameof(restore));
            EndShortcutEdit();
            shortcutResetApply = restore;
            shortcutResetOpenedFrame = Time.frameCount;
            shortcutResetReturnFocus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            shortcutResetQuestion = Rect("기본 조작키 복원 확인", MenuStage, 0, 0, 960, 540).gameObject;
            var question = shortcutResetQuestion;
            RegisterScreenBackdrop(question.AddComponent<Image>()).color = Deviant.UI.Theme.ApprovedTheme.ConfirmationBackdrop;
            shortcutResetOcclusion = question.AddComponent<ApprovedSettingsLayerOcclusion>();
            shortcutResetOcclusion.CoverSiblings(MenuStage);
            var paper = new Color32(238, 233, 223, 255);
            var blue = new Color32(181, 216, 228, 255);
            Words("기본 키로 복원할까요?", question.transform, 264, 202.795f, 432, 30.45f, 21, false, paper);
            Words("변경한 키보드 조작키가 모두 기본값으로 돌아갑니다.", question.transform, 264, 250.745f, 432, 28.5f, 17, false, new Color32(179, 187, 196, 255));
            Fill(question.transform, 264, 293.205f, 432, .5f, new Color32(168, 175, 182, 64));
            var cancel = SettingsAction("취소", question.transform, 264, 310.205f, 85, 30,
                () => { if (shortcutResetQuestion == question) CloseShortcutResetQuestion(); }, 19);
            var confirm = SettingsAction("복원", question.transform, 363.715f, 310.205f, 85, 30,
                () => { if (shortcutResetQuestion == question) ConfirmShortcutReset(); }, 19);
            foreach (var button in new[] { cancel, confirm })
            {
                bool isCancel = button == cancel;
                button.name = isCancel ? "Cancel shortcut reset" : "Confirm shortcut reset";
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.GetComponent<ApprovedActionVisual>().enabled = false;
                var label = button.GetComponentInChildren<Text>(); Place(label, 40.5f, 3.68f, 40, 27);
                label.color = isCancel ? paper : blue;
                var cap = Rounded("Keycap " + (isCancel ? "F" : "G"), button.transform, 0, 0, 28.5f, 30, 3, 1, Color.clear, label.color);
                Words(isCancel ? "F" : "G", cap.transform, 0, 0, 28.5f, 30, 18, false, label.color, TextAnchor.MiddleCenter);
            }
            Controls?.SuppressGameplay();
            if (EventSystem.current && !EventSystem.current.alreadySelecting) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }
        void TickShortcutResetQuestion()
        {
            if (!shortcutResetQuestion) return;
            if (!shortcutPanelOpen || !shortcutPanel || !shortcutPanel.activeInHierarchy) { CloseShortcutResetQuestion(false); return; }
            Controls?.SuppressGameplay();
            if (Time.frameCount == shortcutResetOpenedFrame) return;
            var keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true || keyboard?.fKey.wasPressedThisFrame == true) CloseShortcutResetQuestion();
            else if (keyboard?.gKey.wasPressedThisFrame == true) ConfirmShortcutReset();
            else TickFixedChoicePad(shortcutResetQuestion,()=>CloseShortcutResetQuestion(),ConfirmShortcutReset);
        }
        void CloseShortcutResetQuestion(bool restoreFocus = true)
        {
            if (!shortcutResetQuestion) return;
            var question = shortcutResetQuestion; shortcutResetQuestion = null; shortcutResetApply = null;
            shortcutResetClosedFrame = Time.frameCount;
            question.SetActive(false); shortcutResetOcclusion?.Reveal(); shortcutResetOcclusion = null;
            Destroy(question); Controls?.SuppressGameplay();
            if (restoreFocus && shortcutPanelOpen && EventSystem.current && !EventSystem.current.alreadySelecting && shortcutResetReturnFocus && shortcutResetReturnFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(shortcutResetReturnFocus);
            shortcutResetReturnFocus = null;
        }
        void ConfirmShortcutReset()
        {
            if (!shortcutResetQuestion) return;
            var apply = shortcutResetApply;
            CloseShortcutResetQuestion();
            apply?.Invoke();
        }
    }
}
