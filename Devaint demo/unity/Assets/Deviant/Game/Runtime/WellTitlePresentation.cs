using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    /// <summary>The approved animated sketch title and one-shot start transition.</summary>
    public sealed class WellTitlePresentation : MonoBehaviour
    {
        const float Width = 1600, Height = 900;
        const float FadeSeconds = 1.2f;
        const float FinishAt = FadeSeconds;
        const string SoundPreference = "deviant.title.glitch-sound";
        static readonly Color NormalInk = new Color32(52, 56, 44, 255);
        static readonly float[] RowY = {360, 430, 500};

        Canvas ownerCanvas;
        RectTransform backgroundFrame, artworkFrame;
        GameObject settingsPanel;
        Image fade;
        SketchTitleScene scene;
        Button soundButton, settingsBack;
        Text soundLabel;
        Action startAction, quitAction;
        Action settingsOpened, settingsClosed;
        float startedAt;
        int blackFrame=-1;
        bool pending, startCommitted;
        public static bool TitleSoundEnabled
        {
            get=>PlayerPrefs.GetInt(SoundPreference,1)!=0;
            set{PlayerPrefs.SetInt(SoundPreference,value?1:0);PlayerPrefs.Save();}
        }
        bool soundEnabled {get=>TitleSoundEnabled;set=>TitleSoundEnabled=value;}

        public RawImage Background { get; private set; }
        public Image SelectionInk { get; private set; }
        public Image Logo { get; private set; }
        public Text[] MenuLabels { get; private set; }
        public Button[] MenuButtons { get; private set; }
        public int SelectedIndex { get; private set; }
        public bool Transitioning { get; private set; }
        // The existing stamp plays once on accepted button press. The fade and scene handoff add no sound.
        public bool SoundEnabled => soundEnabled;
        public bool SettingsOpen => settingsPanel && settingsPanel.activeSelf;
        public GameObject InputPanel => SettingsOpen ? settingsPanel : gameObject;
        public bool EraseVisible => scene && scene.Progress >= 1;
        public float SketchProgress => scene ? scene.Progress : 0;
        public float FadeAmount => fade ? fade.color.a : 0;
        public int BlackFrame => blackFrame;
        public float TransitionElapsed => Transitioning ? Time.unscaledTime - startedAt : 0;

        public void Build(Font font, Action onStart, Action onQuit)
        {
            ownerCanvas = GetComponentInParent<Canvas>();
            startAction = onStart; quitAction = onQuit;
            Stretch((RectTransform)transform);
            backgroundFrame = Centered("Well title scenery", transform);
            scene = backgroundFrame.gameObject.AddComponent<SketchTitleScene>();
            scene.Build(backgroundFrame); Background = scene.Sky;

            artworkFrame = Centered("Well title UI", transform);
            Logo = TitleArt("Logo", artworkFrame, "logo", new Rect(400, 90, 800, 205));
            SelectionInk = TitleArt("Yellow marker", artworkFrame, "marker", new Rect(692.5f, RowY[0], 215, 58));
            MenuLabels = new Text[3]; MenuButtons = new Button[3];
            string[] labels = {"시작하기", "설정", "종료"};
            for (int i = 0; i < 3; i++)
            {
                var label = At(labels[i]+" 글자",artworkFrame,new Rect(698,RowY[i],204,58)).gameObject.AddComponent<Text>();
                label.font = Deviant.UI.Theme.ApprovedTheme.Font; label.fontSize=36; label.fontStyle=FontStyle.Normal;
                label.alignment=TextAnchor.MiddleCenter; label.color=NormalInk; label.text=labels[i]; label.raycastTarget=false;
                MenuLabels[i]=label;
            }
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var hit = At(labels[i], artworkFrame, new Rect(698, RowY[i], 204, 58));
                var graphic = hit.gameObject.AddComponent<Image>(); graphic.color = Color.clear;
                var button = hit.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => Activate(index));
                hit.gameObject.AddComponent<WellTitleMenuFocus>().Bind(this, index);
                MenuButtons[i] = button;
            }
            for (int i = 0; i < 3; i++)
                MenuButtons[i].navigation = new Navigation {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = MenuButtons[(i + 2) % 3], selectOnDown = MenuButtons[(i + 1) % 3],
                    selectOnLeft = MenuButtons[(i + 2) % 3], selectOnRight = MenuButtons[(i + 1) % 3]
                };

            BuildSettings(font);
            var black = At("Title fade", transform, Rect.zero);
            Stretch(black); fade = black.gameObject.AddComponent<Image>();
            fade.color = new Color(0, 0, 0, 0); fade.raycastTarget = false;
            SelectMenu(0); FitToCanvas();
        }

        static T Required<T>(string name) where T : UnityEngine.Object
        {
            var asset = Resources.Load<T>("TitleWell/" + name);
            if (!asset) throw new InvalidOperationException("Missing title asset: TitleWell/" + name);
            return asset;
        }

        public void FitToCanvas()
        {
            if (!ownerCanvas || !backgroundFrame || !artworkFrame) return;
            var target = ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay && ownerCanvas.worldCamera
                ? ownerCanvas.worldCamera.targetTexture : null;
            float width = target ? target.width : Screen.width, height = target ? target.height : Screen.height;
            float canvasScale = Mathf.Max(.001f, ownerCanvas.scaleFactor);
            backgroundFrame.localScale = new Vector3(width/Width/canvasScale,height/Height/canvasScale,1);
            // The scene fills the window while the character keeps a uniform scale; UI stays in a safe area.
            artworkFrame.localScale = Vector3.one * (Mathf.Min(width / Width, height / Height) / canvasScale);
            scene.Fit();
        }

        public void FocusInitial()
        {
            if (!pending && EventSystem.current && MenuButtons != null)
                EventSystem.current.SetSelectedGameObject(MenuButtons[SelectedIndex].gameObject);
        }

        public void SetPending(bool value)
        {
            if (pending == value) return;
            pending = value; CancelTransition();
            artworkFrame.gameObject.SetActive(!pending);
            if (!pending) FocusInitial();
        }

        public void SelectMenu(int index)
        {
            if (pending || Transitioning || startCommitted || SettingsOpen || MenuButtons == null) return;
            SelectedIndex = Mathf.Clamp(index, 0, 2);
            SelectionInk.rectTransform.anchoredPosition = new Vector2(692.5f, -RowY[SelectedIndex]);
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject != MenuButtons[SelectedIndex].gameObject)
                EventSystem.current.SetSelectedGameObject(MenuButtons[SelectedIndex].gameObject);
        }

        void Activate(int index)
        {
            if (pending || Transitioning || startCommitted || SettingsOpen) return;
            SelectMenu(index);
            if (index == 0) BeginStart();
            else if (index == 1) OpenSettings();
            else quitAction?.Invoke();
        }

        public void BeginStart()
        {
            if (pending || Transitioning || startCommitted || SettingsOpen) return;
            SelectMenu(0); Transitioning = true; startedAt = Time.unscaledTime;
            if(SoundEnabled)ForestCombatV2.PresentationSfx.PlaySelected("sfx_ui_confirm");
            foreach (var button in MenuButtons) button.interactable = false;
            fade.raycastTarget = true;
            RenderTransition(0);
        }

        void Update()
        {
            if(!scene)return;
            FitToCanvas();
            var keyboard=Keyboard.current;
            if(!pending&&!Transitioning&&!startCommitted&&!SettingsOpen&&keyboard!=null)
            {
                if(keyboard.tabKey.wasPressedThisFrame)SelectMenu((SelectedIndex+(keyboard.shiftKey.isPressed?2:1))%3);
                if(keyboard.spaceKey.wasPressedThisFrame)Activate(SelectedIndex);
            }
            float elapsed=Time.unscaledTime-startedAt;
            if(Transitioning)RenderTransition(elapsed);
            scene.Tick(SettingsOpen||pending?0:Time.unscaledDeltaTime,scene.Progress);
            if(!Transitioning||elapsed<FinishAt)return;
            Transitioning=false;startCommitted=true;StartCoroutine(CommitStartAfterBlackFrame());
        }

        IEnumerator CommitStartAfterBlackFrame()
        {
            yield return null;
            if(startCommitted&&isActiveAndEnabled)startAction?.Invoke();
        }

        // The render state has no scene side effects and is also used by editor captures.
        void RenderTransition(float elapsed)
        {
            scene.SetProgress(SketchTitleScene.SplitProgress);
            float darken=Mathf.Clamp01(elapsed/FadeSeconds);
            fade.color=new Color(0,0,0,darken*darken*(3-2*darken));
            if(darken>=1&&blackFrame<0)blackFrame=Time.frameCount;
        }

        public void OpenSettings()
        {
            if (Transitioning || startCommitted || pending) return;
            SelectMenu(1); settingsPanel.SetActive(true);
            foreach (var button in MenuButtons) button.interactable = false;
            if (settingsOpened != null) { artworkFrame.gameObject.SetActive(false); settingsOpened(); }
            else if (EventSystem.current) EventSystem.current.SetSelectedGameObject(soundButton.gameObject);
        }

        public void UseSharedSettings(GameObject panel, Action onOpen, Action onClose)
        {
            if (settingsPanel) Destroy(settingsPanel);
            settingsPanel = panel; settingsOpened = onOpen; settingsClosed = onClose;
            // The shared sound page owns title volume through GameAudioSettings.Master.
        }

        public void CloseSettings()
        {
            if (!SettingsOpen) return;
            settingsPanel.SetActive(false);
            settingsClosed?.Invoke();
            foreach (var button in MenuButtons) button.interactable = true;
            artworkFrame.gameObject.SetActive(!pending);
            FocusInitial();
        }

        void ToggleSound()
        {
            soundEnabled = !soundEnabled;
            RefreshSoundLabel();
        }

        void RefreshSoundLabel() => soundLabel.text = "타이틀 효과음 " + (soundEnabled ? "켜짐" : "꺼짐");

        // The settings screen in the journal look of every other screen (playtest 2026-11-06: it showed as plain white boxes):
        // JournalGUI paper, the title tab over its top-left edge, secondary buttons that turn primary when selected.
        // Coordinates are the 1672x941 artwork frame's; journal sizes are the 960 stage's times FrameScale.
        const float FrameScale = Width / 960f;
        static readonly Color JournalPaperText = new Color32(255, 250, 240, 255), JournalInk = new Color32(23, 23, 17, 255);
        void BuildSettings(Font font)
        {
            settingsPanel = At("타이틀 설정", artworkFrame, new Rect(0, 0, Width, Height)).gameObject;
            var veil = settingsPanel.AddComponent<Image>(); veil.color = new Color32(36, 35, 31, 119);
            var fonts = Resources.Load<JournalFonts>("JournalGUI/JournalFonts");
            var bold = fonts && fonts.bold ? fonts.bold : font;
            const float bookW = 760, bookH = 400, bookX = (Width - bookW) / 2, bookY = 250;
            var book = Journal("수첩", settingsPanel.transform, "paper", new Rect(bookX, bookY, bookW, bookH), 0, 0);
            var tab = Journal("제목 탭", book.transform, "title", new Rect(48 * FrameScale, -20 * FrameScale, 210 * FrameScale, 48 * FrameScale), 14, 10, true);
            var title = At("설정 제목", tab.transform, new Rect(0, 10 * FrameScale, 210 * FrameScale, 28 * FrameScale)).gameObject.AddComponent<Text>();
            title.font = bold; title.fontSize = 40; title.text = "설정"; title.alignment = TextAnchor.MiddleCenter; title.color = JournalPaperText; title.raycastTarget = false;
            const float buttonW = 560, buttonH = 84, buttonX = (bookW - buttonW) / 2;
            soundButton = SettingsButton("효과음", book.transform, new Rect(buttonX, 110, buttonW, buttonH), bold, ToggleSound, out soundLabel);
            settingsBack = SettingsButton("돌아가기", book.transform, new Rect(buttonX, 230, buttonW, buttonH), bold, CloseSettings, out _);
            soundButton.gameObject.AddComponent<WellTitleSettingsCancel>().Bind(this);
            settingsBack.gameObject.AddComponent<WellTitleSettingsCancel>().Bind(this);
            soundButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = settingsBack, selectOnDown = settingsBack };
            settingsBack.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = soundButton, selectOnDown = soundButton };
            RefreshSoundLabel(); settingsPanel.SetActive(false);
        }

        static Button SettingsButton(string name, Transform parent, Rect area, Font font, Action action, out Text label)
        {
            var image = Journal(name, parent, "secondary", area, 14, 9, true, true);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
            var textRect = At("글자", image.transform, new Rect(0, 0, area.width, area.height)); label = textRect.gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = 30; label.color = JournalInk; label.text = name; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            image.gameObject.AddComponent<JournalSelectLook>().Bind(image, label,
                JournalSprite("secondary", 14, true), JournalSprite("primary", 14, true), JournalInk, JournalPaperText);
            button.onClick.AddListener(() => action()); return button;
        }

        // A JournalGUI picture as TemporaryGameGui.Face draws it: `slice` source px at the edges drawn `width` stage px wide.
        static Image Journal(string name, Transform parent, string art, Rect area, int slice, float width, bool sidesOnly = false, bool raycast = false)
        {
            var image = At(name, parent, area).gameObject.AddComponent<Image>();
            image.sprite = JournalSprite(art, slice, sidesOnly); image.raycastTarget = raycast;
            image.type = slice > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = slice > 0 ? slice / (width * FrameScale) : 1;
            return image;
        }
        static Sprite JournalSprite(string art, int slice, bool sidesOnly)
        {
            var texture = Resources.Load<Texture2D>("JournalGUI/" + art);
            if (!texture) throw new InvalidOperationException("Missing journal art: JournalGUI/" + art);
            var border = slice == 0 ? Vector4.zero : sidesOnly ? new Vector4(slice, 0, slice, 0) : Vector4.one * slice;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            sprite.name = art; return sprite;
        }

        static Image Art(string name, Transform parent, string file, Rect area)
        {
            var image = At(name, parent, area).gameObject.AddComponent<Image>();
            image.sprite = Required<Sprite>(file); image.type = Image.Type.Simple; image.raycastTarget = false;
            return image;
        }

        internal static RectTransform At(string name, Transform parent, Rect area)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(area.x, -area.y); rect.sizeDelta = area.size; return rect;
        }

        static RectTransform Centered(string name, Transform parent)
        {
            var rect = At(name, parent, new Rect(0, 0, Width, Height));
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.anchoredPosition = Vector2.zero; return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void CancelTransition()
        {
            Transitioning = false; startCommitted = false;
            blackFrame=-1;
            if (scene) scene.ResetScene();
            if (fade) { fade.color = Color.clear; fade.raycastTarget = false; }
            if (artworkFrame) artworkFrame.anchoredPosition = Vector2.zero;
            if (settingsPanel) settingsPanel.SetActive(false);
            if (MenuButtons != null) foreach (var button in MenuButtons) if (button) button.interactable = true;
        }

        void OnDisable() => CancelTransition();
        static Image TitleArt(string name,Transform parent,string asset,Rect area)
        {
            var sprite=Resources.Load<Sprite>("TitleSketch/"+asset);
            if(!sprite)throw new InvalidOperationException("Missing approved title art: "+asset);
            var image=At(name,parent,area).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image;
        }
    }

    public sealed class WellTitleMenuFocus : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        WellTitlePresentation owner;
        int index;
        public void Bind(WellTitlePresentation presentation, int menuIndex) { owner = presentation; index = menuIndex; }
        public void OnPointerEnter(PointerEventData eventData) { if (owner) owner.SelectMenu(index); }
        public void OnSelect(BaseEventData eventData) { if (owner) owner.SelectMenu(index); }
    }

    public sealed class WellTitleSettingsCancel : MonoBehaviour, ICancelHandler
    {
        WellTitlePresentation owner;
        public void Bind(WellTitlePresentation presentation) { owner = presentation; }
        public void OnCancel(BaseEventData eventData) { if (owner) owner.CloseSettings(); }
    }

    // The title settings buttons: secondary art with ink text, primary art with paper text while selected (keyboard, pad
    // or the pointer over it), as the journal's own buttons read.
    public sealed class JournalSelectLook : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Image image; Text label; Sprite normal, selected; Color normalText, selectedText; bool hovered;
        public void Bind(Image target, Text text, Sprite normalArt, Sprite selectedArt, Color normalInk, Color selectedInk)
        { image = target; label = text; normal = normalArt; selected = selectedArt; normalText = normalInk; selectedText = selectedInk; Apply(); }
        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        void OnDisable() => hovered = false;
        void LateUpdate() => Apply();
        void Apply()
        {
            if (!image) return;
            bool on = hovered || EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject;
            image.sprite = on ? selected : normal;
            if (label) label.color = on ? selectedText : normalText;
        }
    }
}
