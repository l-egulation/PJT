using Deviant.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        static readonly (string Term, string Meaning)[] InkGlossary =
        {
            ("표식", "공격으로 적에게 쌓는 표시. 다른 무기로 그 적을 맞히면 쌓인 표식이 터져 피해를 준다."),
            ("속박", "걸려 있는 동안 움직일 수 없다. 공격은 할 수 있다."),
            ("기절", "하던 공격이 끊긴다. 풀릴 때까지 움직이거나 새로 공격할 수 없다."),
            ("상태 이상", "속박, 기절, 둔화처럼 적에게 일정 시간 걸리는 효과."),
            ("보호막", "받는 피해를 체력보다 먼저 흡수한다. 흡수량을 다 쓰거나 지속시간이 끝나면 사라진다."),
            ("전기장", "범위에 들어온 적에게 일정 간격으로 피해를 주는 영역."),
            ("쿨타임", "스킬을 사용한 뒤 다시 쓸 수 있을 때까지 남은 시간."),
            ("충전", "스킬을 쓰는 데 필요한 횟수. 충전이 남아 있으면 다시 사용할 수 있다."),
            ("후딜", "공격을 마친 뒤 다음 공격을 시작할 수 있을 때까지 기다리는 시간."),
            ("무기 교체", "사용 중인 무기에서 다른 무기로 전환하는 것.")
        };

        RectTransform inkGlossaryTarget, inkGlossaryViewport, inkGlossaryPopup;
        GrowthPart inkGlossaryPart;

        void ClearInkGlossaryTarget()
        {
            inkGlossaryTarget = null;
            inkGlossaryViewport = null;
            inkGlossaryPart = null;
            if (!inkGlossaryPopup) return;
            inkGlossaryPopup.gameObject.SetActive(false);
            Destroy(inkGlossaryPopup.gameObject);
            inkGlossaryPopup = null;
        }

        void SetInkGlossaryTarget(Button button, GrowthPart part)
        {
            if (string.IsNullOrEmpty(part?.description)) return;
            inkGlossaryTarget = (RectTransform)button.transform;
            inkGlossaryViewport = button.GetComponentInParent<ScrollRect>()?.viewport;
            inkGlossaryPart = part;
        }

        void LateUpdate()
        {
            RefreshBuffTooltip();
            var mouse = Mouse.current;
            bool hovered = mouse != null && Application.isFocused && bag && bag.activeInHierarchy &&
                inkGlossaryTarget && inkGlossaryTarget.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(inkGlossaryTarget, mouse.position.ReadValue()) &&
                (!inkGlossaryViewport || RectTransformUtility.RectangleContainsScreenPoint(inkGlossaryViewport, mouse.position.ReadValue()));
            if (!hovered)
            {
                if (inkGlossaryPopup) inkGlossaryPopup.gameObject.SetActive(false);
                return;
            }

            const float width = 226, gap = 8, margin = 8;
            if (!inkGlossaryPopup)
            {
                inkGlossaryPopup = Rect("Ink glossary", stage, 0, 0, width, 0);
                float y = 0;
                foreach (var entry in InkGlossary)
                {
                    if (!inkGlossaryPart.description.Contains(entry.Term)) continue;
                    var panel = Rounded(entry.Term, inkGlossaryPopup, 0, y, width, 40, 6, 1, JBubble, JRule);
                    Words(entry.Term, panel.transform, 10, 5, width - 20, 18, 14, true);
                    var meaning = Paragraph(panel.transform, 10, 25, width - 20, 13, 17, JInk);
                    meaning.text = entry.Meaning;
                    float textHeight = Mathf.Max(1, LineCount(meaning)) * 17;
                    Place(meaning, 10, 25, width - 20, textHeight);
                    float height = 32 + textHeight;
                    Place(panel, 0, y, width, height);
                    y += height + 4;
                }
                inkGlossaryPopup.sizeDelta = new Vector2(width, Mathf.Max(0, y - 4));
            }
            if (inkGlossaryPopup.childCount == 0) return;

            var target = inkGlossaryTarget.rect;
            var left = stage.InverseTransformPoint(inkGlossaryTarget.TransformPoint(new Vector3(target.xMin, target.yMax)));
            var right = stage.InverseTransformPoint(inkGlossaryTarget.TransformPoint(new Vector3(target.xMax, target.yMax)));
            var bounds = stage.rect;
            float x = left.x - gap - width;
            if (x < bounds.xMin + margin) x = right.x + gap;
            x = Mathf.Clamp(x, bounds.xMin + margin, bounds.xMax - width - margin);
            float top = Mathf.Clamp(left.y, bounds.yMin + inkGlossaryPopup.rect.height + margin, bounds.yMax - margin);
            inkGlossaryPopup.anchoredPosition = new Vector2(x - bounds.xMin, top - bounds.yMax);
            inkGlossaryPopup.SetAsLastSibling();
            inkGlossaryPopup.gameObject.SetActive(true);
        }
    }
}
