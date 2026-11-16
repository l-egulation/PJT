using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        GameObject abandonRunQuestion, abandonRunReturnFocus;
        ApprovedSettingsLayerOcclusion abandonRunOcclusion;
        Button abandonRunCancel, abandonRunConfirm;
        GameObject abandonRunCancelKey, abandonRunConfirmKey;
        Action abandonRunLeave;
        int abandonRunClosedFrame = -1;
        bool exitQuestionToTitle;

        void OpenAbandonRunQuestion(Action leave)
        {
            if (abandonRunQuestion || !menu || !menu.activeInHierarchy || !host || !Presenter.Paused) return;
            OpenExitQuestion(leave, false);
        }
        void OpenTitleReturnQuestion(Action leave)
        {
            if (abandonRunQuestion || !menu || !menu.activeInHierarchy || !hubPaused || hubOpen) return;
            OpenExitQuestion(leave, true);
        }
        // Both Figma exit confirmations share modal ownership, input and focus restoration.
        void OpenExitQuestion(Action leave, bool toTitle)
        {
            if (leave == null) throw new ArgumentNullException(nameof(leave));
            exitQuestionToTitle = toTitle;
            abandonRunLeave = leave;
            abandonRunReturnFocus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            abandonRunQuestion = Rect(toTitle ? "타이틀 복귀 확인" : "도전 종료 확인", menu.transform, 0, 0, 960, 540).gameObject;
            var question = abandonRunQuestion;
            question.AddComponent<Image>().color = Color.clear; // Blocks clicks; the existing pause veil owns the shade.
            abandonRunOcclusion = question.AddComponent<ApprovedSettingsLayerOcclusion>();
            abandonRunOcclusion.CoverSiblings(menu.transform);
            var paper = new Color32(238, 233, 223, 255);
            var muted = new Color32(179, 187, 196, 255);
            Words(toTitle ? "타이틀로 돌아갈까요?" : "이번 도전을 마칠까요?", question.transform, 264, toTitle ? 202.795f : 188.565f, 432, 30.45f, 21, false, paper);
            Words(toTitle ? "현재 마을을 나가 타이틀 화면으로 이동합니다." : "현재 도전의 진행 상황과", question.transform, 264, toTitle ? 250.745f : 236.515f, 432, 28.5f, 17, false, muted);
            if (!toTitle) Words("이번 런에서 획득한 영구 재화를 포기합니다.", question.transform, 264, 264.975f, 432, 28.5f, 17, false, muted);
            Fill(question.transform, 264, toTitle ? 293.205f : 307.435f, 432, .5f, new Color32(168, 175, 182, 64));
            float actionY = toTitle ? 310.205f : 324.435f;
            abandonRunCancel = SettingsAction("취소", question.transform, 264, actionY, 85, 30,
                () => { if (abandonRunQuestion == question) CloseAbandonRunQuestion(); }, 19);
            abandonRunConfirm = SettingsAction(toTitle ? "타이틀로" : "포기하고 마을로", question.transform, 363.715f, actionY, toTitle ? 120 : 190, 30,
                () => { if (abandonRunQuestion == question) ConfirmAbandonRun(); }, 19);
            foreach (var button in new[] { abandonRunCancel, abandonRunConfirm })
            {
                var text = button.GetComponentInChildren<Text>();
                Place(text, 40.5f, 3.68f, button == abandonRunCancel ? 40 : toTitle ? 75 : 145, 27);
                text.color = button == abandonRunCancel ? paper : new Color32(227, 181, 159, 255);
                var visual = button.GetComponent<ApprovedActionVisual>();
                visual.NormalColor = visual.SelectedColor = text.color;
                visual.LeadingHighlightWidth = 40.5f;
            }
            abandonRunCancelKey = AbandonActionKey(abandonRunCancel.transform, paper);
            abandonRunConfirmKey = AbandonActionKey(abandonRunConfirm.transform, paper);
            var cancelNavigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = abandonRunConfirm };
            var confirmNavigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = abandonRunCancel };
            abandonRunCancel.navigation = cancelNavigation; abandonRunConfirm.navigation = confirmNavigation;
            Fill(question.transform, 90, 463.9f, 780, .5f, new Color32(168, 175, 182, 64));
            string[] keys = { "← →", "F", "Esc" }, captions = { "이동", "선택", "취소" };
            float[] keyX = { 90, 198.3f, 294.1f }, widths = { 29.6f, 17, 24.6f }, captionX = { 144.058f, 239.828f, 343.207f };
            for (int i = 0; i < keys.Length; i++)
            {
                var cap = Rounded("Confirmation footer " + keys[i], question.transform, keyX[i], 474.4f, widths[i], MenuHintHeight, 2, 1, Color.clear, new Color32(147, 156, 166, 255));
                Words(keys[i], cap.transform, 0, 0, widths[i], MenuHintHeight, 11, false, muted, TextAnchor.MiddleCenter).alignByGeometry = true;
                Words(captions[i], question.transform, captionX[i], 476.9f, 40, 16, MenuHintFontSize, false, muted);
            }
            Controls?.SuppressGameplay();
            if (EventSystem.current && !EventSystem.current.alreadySelecting)
                EventSystem.current.SetSelectedGameObject(abandonRunCancel.gameObject);
            RefreshAbandonRunQuestion();
        }
        GameObject AbandonActionKey(Transform parent, Color color)
        {
            var cap = Rounded("Keycap F", parent, 0, 0, 28.5f, 30, 3, 1, Color.clear, color);
            Words("F", cap.transform, 0, 0, 28.5f, 30, 18, false, color, TextAnchor.MiddleCenter);
            return cap.gameObject;
        }
        void RefreshAbandonRunQuestion()
        {
            if (!abandonRunQuestion) return;
            bool valid = exitQuestionToTitle ? hubPaused && !hubOpen : host && Presenter.Paused && !(run && run.ShowingResult) && !(chapter2 && chapter2.ShowingResult);
            if (!menu || !menu.activeInHierarchy || !valid)
            { CloseAbandonRunQuestion(false); return; }
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            abandonRunCancelKey.SetActive(selected != abandonRunConfirm.gameObject);
            abandonRunConfirmKey.SetActive(selected == abandonRunConfirm.gameObject);
        }
        void CloseAbandonRunQuestion(bool restoreFocus = true)
        {
            if (!abandonRunQuestion) return;
            var question = abandonRunQuestion;
            abandonRunQuestion = null; abandonRunLeave = null; abandonRunClosedFrame = Time.frameCount;
            question.SetActive(false);
            if (abandonRunOcclusion) abandonRunOcclusion.Reveal();
            abandonRunOcclusion = null;
            Destroy(question);
            Controls?.SuppressGameplay();
            if (restoreFocus && menu && menu.activeInHierarchy && EventSystem.current && !EventSystem.current.alreadySelecting)
                EventSystem.current.SetSelectedGameObject(abandonRunReturnFocus && abandonRunReturnFocus.activeInHierarchy ? abandonRunReturnFocus : menuSecondary.gameObject);
            abandonRunReturnFocus = null;
        }
        void ConfirmAbandonRun()
        {
            if (!abandonRunQuestion) return;
            var leave = abandonRunLeave;
            CloseAbandonRunQuestion(false); // Clear first: one confirmation can never navigate twice.
            leave?.Invoke();
        }
    }
}
