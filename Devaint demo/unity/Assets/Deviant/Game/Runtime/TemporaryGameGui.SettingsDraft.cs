using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        int acceptedDisplayWidth, acceptedDisplayHeight, settingsDraftTarget, settingsDraftClosedFrame = -1, settingsDraftOpenedFrame;
        FullScreenMode acceptedDisplayMode;
        GameObject settingsDraftQuestion, settingsDraftReturnFocus;
        ApprovedSettingsLayerOcclusion settingsDraftOcclusion;

        void RememberAcceptedDisplay()
        {
            acceptedDisplayWidth = pendingWidth; acceptedDisplayHeight = pendingHeight; acceptedDisplayMode = pendingMode;
        }
        void RequestSettingsTab(int target)
        {
            if (settingsDraftQuestion || testingDisplay || target < 0 || target >= settingsTabs.Length || target == settingsTab) return;
            bool changed = pendingWidth != acceptedDisplayWidth || pendingHeight != acceptedDisplayHeight || pendingMode != acceptedDisplayMode;
            if (settingsTab == 0 && changed) { OpenSettingsDraftQuestion(target); return; }
            settingsTab = target; RefreshApprovedSettings();
        }
        void OpenSettingsDraftQuestion(int target)
        {
            settingsDraftTarget = target;
            settingsDraftOpenedFrame = Time.frameCount;
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            settingsDraftReturnFocus = selected && selected.transform.IsChildOf(settingsBody) ? selected : settingsTabs[0].gameObject;
            settingsDraftQuestion = Rect("미적용 설정 확인", approvedSettings.transform, 0, 0, 960, 540).gameObject;
            var question = settingsDraftQuestion;
            RegisterScreenBackdrop(question.AddComponent<Image>()).color = Deviant.UI.Theme.ApprovedTheme.ConfirmationBackdrop;
            settingsDraftOcclusion = question.AddComponent<ApprovedSettingsLayerOcclusion>();
            settingsDraftOcclusion.CoverSiblings(approvedSettings.transform,true);
            var paper = new Color32(238, 233, 223, 255);
            var blue = new Color32(181, 216, 228, 255);
            Words("변경한 설정을 적용하지 않았어요.", question.transform, 264, 202.795f, 432, 30.45f, 21, false, paper);
            Words("변경을 취소하고 다른 설정으로 이동할까요?", question.transform, 264, 250.745f, 432, 28.5f, 17, false, new Color32(179, 187, 196, 255));
            Fill(question.transform, 264, 293.205f, 432, .5f, new Color32(168, 175, 182, 64));
            var cancel = SettingsAction("계속 설정", question.transform, 264, 310.205f, 110, 30,
                () => { if (settingsDraftQuestion == question) CloseSettingsDraftQuestion(); }, 19);
            var discard = SettingsAction("변경 취소", question.transform, 397.09f, 310.205f, 110, 30,
                () => { if (settingsDraftQuestion == question) DiscardSettingsDraft(); }, 19);
            foreach (var button in new[] { cancel, discard })
            {
                bool keep = button == cancel;
                button.name = keep ? "Continue display settings" : "Discard display settings";
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.GetComponent<ApprovedActionVisual>().enabled = false;
                var label = button.GetComponentInChildren<Text>(); Place(label, 40.5f, 3.68f, 70, 27);
                label.color = keep ? paper : blue;
                var cap = Rounded("Keycap " + (keep ? "F" : "G"), button.transform, 0, 0, 28.5f, 30, 3, 1, Color.clear, label.color);
                Words(keep ? "F" : "G", cap.transform, 0, 0, 28.5f, 30, 18, false, label.color, TextAnchor.MiddleCenter);
            }
            Controls?.SuppressGameplay();
            if (EventSystem.current && !EventSystem.current.alreadySelecting) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }
        void CloseSettingsDraftQuestion(bool restoreFocus = true)
        {
            if (!settingsDraftQuestion) return;
            var question = settingsDraftQuestion; settingsDraftQuestion = null;
            settingsDraftClosedFrame = settingsInputFrame = Time.frameCount;
            question.SetActive(false); settingsDraftOcclusion?.Reveal(); settingsDraftOcclusion = null;
            Destroy(question); Controls?.SuppressGameplay();
            if (restoreFocus && EventSystem.current && !EventSystem.current.alreadySelecting && settingsDraftReturnFocus && settingsDraftReturnFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(settingsDraftReturnFocus);
            settingsDraftReturnFocus = null;
        }
        void DiscardSettingsDraft()
        {
            if (!settingsDraftQuestion) return;
            int target = settingsDraftTarget;
            pendingWidth = acceptedDisplayWidth; pendingHeight = acceptedDisplayHeight; pendingMode = acceptedDisplayMode;
            CloseSettingsDraftQuestion(false);
            settingsTab = target; RefreshApprovedSettings();
        }
        void TickSettingsDraftQuestion()
        {
            settingsInputFrame = Time.frameCount;
            Controls?.SuppressGameplay();
            if (Time.frameCount == settingsDraftOpenedFrame) return;
            var keyboard = Keyboard.current;
            if (keyboard?.fKey.wasPressedThisFrame == true || keyboard?.escapeKey.wasPressedThisFrame == true) CloseSettingsDraftQuestion();
            else if (keyboard?.gKey.wasPressedThisFrame == true) DiscardSettingsDraft();
            else TickFixedChoicePad(settingsDraftQuestion,()=>CloseSettingsDraftQuestion(),DiscardSettingsDraft);
        }
        void TickFixedChoicePad(GameObject question,System.Action cancel,System.Action confirm)
        {
            var pad=Gamepad.current;if(pad==null||!question)return;
            if(pad.buttonEast.wasPressedThisFrame||pad.startButton.wasPressedThisFrame){cancel();return;}
            bool left=pad.dpad.left.wasPressedThisFrame||pad.leftStick.left.wasPressedThisFrame;
            bool right=pad.dpad.right.wasPressedThisFrame||pad.leftStick.right.wasPressedThisFrame;
            if(!left&&!right&&!pad.buttonSouth.wasPressedThisFrame)return;
            var buttons=question.GetComponentsInChildren<Button>();if(buttons.Length!=2)return;
            var events=EventSystem.current;
            int selected=events&&events.currentSelectedGameObject==buttons[1].gameObject?1:0;
            if(left)selected=0;else if(right)selected=1;
            foreach(var button in buttons)
            {
                var visual=button.GetComponent<ApprovedActionVisual>();
                if(visual&&!visual.enabled)
                {
                    visual.NormalColor=visual.SelectedColor=button.GetComponentInChildren<Text>().color;
                    visual.enabled=true;
                }
            }
            if(events&&!events.alreadySelecting)events.SetSelectedGameObject(buttons[selected].gameObject);
            if(pad.buttonSouth.wasPressedThisFrame){if(selected==1)confirm();else cancel();}
        }
    }
}
