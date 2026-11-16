using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Deviant.UI.Theme;

namespace Deviant.Tutorial.Unity
{
    // Presentation only: the chapter exit must settle the run before opening this screen.
    public sealed class DemoCompletionPanel : MonoBehaviour
    {
        public const float InputGraceSeconds = .35f;
        public Button VillageButton { get; private set; }
        public Button TitleButton { get; private set; }
        Text summary;
        Action village, title;
        float openedAt;
        bool submitted;
        static readonly Color Paper = new Color32(243, 239, 222, 255);

        public static DemoCompletionPanel Create(Transform parent, Action goToVillage, Action goToTitle)
        {
            if (goToVillage == null || goToTitle == null) throw new ArgumentNullException();
            var view = Box(parent, "데모 완료", 0, 0, 960, 540).gameObject.AddComponent<DemoCompletionPanel>();
            view.village = goToVillage; view.title = goToTitle;
            view.Build();
            return view;
        }

        void OnEnable()
        {
            openedAt = Time.unscaledTime; submitted = false;
            if (VillageButton) VillageButton.interactable = true;
            if (TitleButton) TitleButton.interactable = true;
        }

        public void SetSummary(string value) => summary.text = value;

        void Submit(Action destination)
        {
            if (!isActiveAndEnabled || submitted || Time.unscaledTime - openedAt < InputGraceSeconds) return;
            submitted = true;
            VillageButton.interactable = TitleButton.interactable = false;
            destination();
        }

        void Build()
        {
            var panel = Box(transform, "Approved final result", 80, 35, 800, 470);
            var heading=Label(panel, "최종 클리어", 0, 0, 800, 85, 58);
            heading.color=new Color32(184,220,208,255);
            Label(panel, "플레이해 주셔서 감사합니다.", 0, 100, 800, 28, 18);
            var next=Label(panel,"정식 출시도 기대해 주세요!",0,134,800,24,15);next.color=ApprovedTheme.Muted;
            Divider(panel,176);
            Label(panel,"이번 도전",0,195,800,32,21);
            summary=Label(panel,"",60,240,680,90,21);summary.color=ApprovedTheme.Muted;
            Divider(panel,356);
            VillageButton = Button(panel, "마을로", 180, () => Submit(village));
            TitleButton = Button(panel, "타이틀로", 420, () => Submit(title));
            VillageButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = TitleButton };
            TitleButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = VillageButton };
            Label(panel,"← →  선택    F  이동",0,434,800,24,13).color=ApprovedTheme.Muted;
        }
        static void Divider(Transform parent,float y)
        {var line=Box(parent,"Divider",0,y,800,1).gameObject.AddComponent<Image>();line.color=new Color32(84,90,96,150);line.raycastTarget=false;}
        void LateUpdate()
        {
            Paint(VillageButton);Paint(TitleButton);
        }
        static void Paint(Button button)
        {
            if(!button)return;
            bool selected=button.interactable&&EventSystem.current&&EventSystem.current.currentSelectedGameObject==button.gameObject;
            var label=button.GetComponentInChildren<Text>();label.fontSize=selected?21:19;
            label.color=!button.interactable?new Color32(110,116,122,255):selected?ApprovedTheme.Paper:ApprovedTheme.Muted;
            var wash=button.GetComponentInChildren<RawImage>();wash.enabled=selected;
            wash.rectTransform.sizeDelta=new Vector2(Mathf.Min(210,label.preferredWidth+40),38);
        }

        static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static Image Picture(Transform parent, string name, float x, float y, float w, float h, string sprite)
        {
            var image = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("FirstRunTutorial/Pixels/" + sprite);
            image.raycastTarget = false;
            return image;
        }

        static Text Label(Transform parent, string value, float x, float y, float w, float h, int size)
        {
            var label = Box(parent, value, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = ApprovedTheme.Font ?? Resources.Load<Font>("FirstRunTutorial/Fonts/Galmuri11");
            if (!ApprovedTheme.ConfigureFontSampling(label.font) && label.font && label.font.material && label.font.material.mainTexture)
                label.font.material.mainTexture.filterMode = FilterMode.Point;
            label.fontSize = size; label.text = value; label.color = Paper;
            label.alignment = TextAnchor.MiddleCenter; label.alignByGeometry = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.raycastTarget = false;
            return label;
        }

        static Button Button(Transform parent, string label, int x, Action action)
        {
            var face = Box(parent,label,x,380,200,42).gameObject.AddComponent<Image>();
            face.color=Color.clear;face.raycastTarget=true;
            var button=face.gameObject.AddComponent<Button>();button.targetGraphic=face;button.transition=Selectable.Transition.None;
            var wash=Box(button.transform,"Name selection",0,0,200,38).gameObject.AddComponent<RawImage>();
            wash.texture=ApprovedTheme.SelectionWash;wash.raycastTarget=false;wash.color=new Color(1,1,1,.48f);
            wash.rectTransform.anchorMin=wash.rectTransform.anchorMax=wash.rectTransform.pivot=Vector2.one*.5f;wash.rectTransform.anchoredPosition=Vector2.zero;
            Label(button.transform,label,0,0,200,42,19);
            button.onClick.AddListener(() => action());
            return button;
        }
    }
}
