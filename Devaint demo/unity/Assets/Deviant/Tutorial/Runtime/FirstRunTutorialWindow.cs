using System;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Deviant.UI.Theme;

namespace Deviant.Tutorial.Unity
{
    /// <summary>Approved artwork with live navigation. The caller owns reward timing and seen records.</summary>
    public sealed class FirstRunTutorialWindow : MonoBehaviour
    {
        public const int EquipmentPageCount = 5;
        public const float InputGraceSeconds = .35f;
        const float CanvasWidth = 960, CanvasHeight = 540;
        [SerializeField] GameObject[] pages;
        [SerializeField] GameObject window;
        [SerializeField] Image content;
        [SerializeField] Button previous, next, close;
        [SerializeField] Text number, reboundClose;
        // Key letters drawn from the live bindings: the attack page's key and the weapon-swap note (2026-11-13).
        Text attackLetter, attackCaption, swapNote;
        Image attackKey;
        [SerializeField] CanvasScaler scaler;
        LocalInput input;
        int movementPageCount;
        bool replayPages, systemPages;
        Action<int> pageChanged;
        RectTransform replayContent;
        public RectTransform ReplayContent
        {
            get
            {
                if (!content) Build();
                if (!replayContent) replayContent = Box(content.transform, "Movement replay content", 0, 0, 536, 256);
                return replayContent;
            }
        }
        public void ShowReplay(int movementPages, Action<int> changed, Action closed = null)
        {
            if (changed == null) throw new ArgumentNullException(nameof(changed));
            if (movementPages <= 0) throw new ArgumentOutOfRangeException(nameof(movementPages));
            Show(false, null, null, closed, movementPages, changed);
        }
        public void SelectPage(int index)
        {
            if (!AcceptInput()) return;
            int selected = Mathf.Clamp(index, 0, PageCount - 1);
            if (selected == PageIndex) return;
            PageIndex = selected;
            RefreshPage();
        }

        IControlScope scope;
        Action onClosed;
        bool rewardPages, navigationHeld, wired, fixedCloseRequested;
        InputAction fixedClose;
        float shownAt;
        public bool IsOpen => window && window.activeSelf;
        public int PageIndex { get; private set; }
        public int PageCount => replayPages ? movementPageCount : systemPages ? EquipmentPageCount + 1 : rewardPages ? EquipmentPageCount : 1;
        public bool CanGoPrevious => IsOpen && (rewardPages || replayPages || systemPages) && PageIndex > 0;
        public bool CanGoNext => IsOpen && (rewardPages || replayPages || systemPages) && PageIndex + 1 < PageCount;

        void OnEnable()
        {
            Canvas.willRenderCanvases += ConfigureFontSampling;
            if(fixedClose==null)
            {
                fixedClose=new InputAction("Tutorial fixed close",InputActionType.Button,"<Keyboard>/f");
                fixedClose.AddBinding("<Keyboard>/escape");
                fixedClose.performed+=_=>fixedCloseRequested=true;
            }
            fixedClose.Enable();
        }

        void Awake()
        {
            if (!content) Build();
            EnsurePixelFrame();
            ConfigureFontSampling();
            Wire();
            window.SetActive(false);
        }

        public void ShowAttack(LocalInput owner = null, IControlRequests controls = null, Action closed = null)
            => Show(false, owner, controls, closed);
        public void ShowRewards(LocalInput owner = null, IControlRequests controls = null, Action closed = null)
            => Show(true, owner, controls, closed);

        // Replays all first-run system pages without changing the caller's tutorial records or reward state.
        public void ShowSystem(LocalInput owner = null, IControlRequests controls = null, Action closed = null)
            => Show(true, owner, controls, closed, allSystems: true);

        void Show(bool rewards, LocalInput owner, IControlRequests controls, Action closed, int movementPages = 0, Action<int> changed = null, bool allSystems = false)
        {
            if (!content) Build();
            EnsurePixelFrame();
            FitFooterHints();
            Wire();
            if (!Art("Pixels/hero-idle") || !Art("Pixels/keycap"))
                throw new InvalidOperationException("First-run native pixel sprites have not been deployed.");
            Release();
            replayPages = changed != null; systemPages = allSystems;
            movementPageCount = movementPages;
            pageChanged = changed;
            input = owner;
            scope = controls?.OpenScope("approved first-run tutorial");
            scope?.BlockInput(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation);
            scope?.PauseSimulation();
            onClosed = closed;
            rewardPages = rewards;
            PageIndex = 0;
            navigationHeld = owner && Mathf.Abs(owner.Read(GameAction.Navigate).X) > .5f;
            shownAt = Time.unscaledTime;fixedCloseRequested=false;
            window.SetActive(true);
            RefreshPage();
            input?.SuppressGameplay();
        }

        // Apply to both authored prefabs and windows built at runtime.
        void FitFooterHints()
        {
            foreach (var button in new[] { previous, next, close })
            {
                var rect = (RectTransform)button.transform;
                rect.sizeDelta = Vector2.one * ApprovedTheme.ControlHintHeight;
                var arrow = button.transform.Find("arrow") as RectTransform;
                if (arrow)
                {
                    float height = ApprovedTheme.ControlHintHeight * arrow.rect.height / arrow.rect.width;
                    arrow.sizeDelta = new Vector2(ApprovedTheme.ControlHintHeight, height);
                    arrow.anchoredPosition = new Vector2(button == previous ? ApprovedTheme.ControlHintHeight : 0, -(ApprovedTheme.ControlHintHeight - height) / 2);
                }
                foreach (var text in button.GetComponentsInChildren<Text>(true))
                {
                    text.fontSize = ApprovedTheme.ControlHintFontSize;
                    text.rectTransform.sizeDelta = rect.sizeDelta;
                }
            }
            number.fontSize = ApprovedTheme.ControlHintFontSize;
            number.rectTransform.sizeDelta = new Vector2(75, ApprovedTheme.ControlHintHeight);
            number.rectTransform.anchoredPosition = new Vector2(230, -274);
            var caption = close.transform.parent.Find("닫기")?.GetComponent<Text>();
            if (caption)
            {
                caption.fontSize = ApprovedTheme.ControlHintFontSize;
                caption.rectTransform.sizeDelta = new Vector2(44, ApprovedTheme.ControlHintHeight);
                caption.rectTransform.anchoredPosition = new Vector2(478, -274);
            }
        }

        void Wire()
        {
            if (wired) return;
            previous.onClick.AddListener(PreviousPage);
            next.onClick.AddListener(NextPage);
            close.onClick.AddListener(Close);
            wired = true;
        }

        void Update()
        {
            Fit();
            if (!IsOpen || !input) return;
            input.Poll();
            reboundClose.text = "F";
            RefreshKeyLabels();
            // Like the other guide windows, F (Interact) or Esc (Back) closes it.
            var keyboard=Keyboard.current;
            bool dismiss = input.TakeBack() | input.Take(GameAction.Submit) | fixedCloseRequested;
            fixedCloseRequested=false;
            dismiss |= keyboard!=null&&(keyboard.fKey.wasPressedThisFrame||keyboard.escapeKey.wasPressedThisFrame);
            float horizontal = keyboard!=null&&(keyboard.leftArrowKey.isPressed||keyboard.rightArrowKey.isPressed) ? (keyboard.rightArrowKey.isPressed?1:0)-(keyboard.leftArrowKey.isPressed?1:0) : input.UsingGamepad?input.Read(GameAction.Navigate).X:0;
            bool held = Mathf.Abs(horizontal) > .5f;
            if (Time.unscaledTime - shownAt >= InputGraceSeconds)
            {
                if (dismiss) Close();
                else if (held && !navigationHeld)
                {
                    if (horizontal < 0) PreviousPage(); else NextPage();
                }
            }
            navigationHeld = held;
            input.SuppressGameplay();
        }

        public void PreviousPage() { if (AcceptInput() && CanGoPrevious) { PageIndex--; RefreshPage(); } }
        public void NextPage() { if (AcceptInput() && CanGoNext) { PageIndex++; RefreshPage(); } }
        bool AcceptInput() => IsOpen && Time.unscaledTime - shownAt >= InputGraceSeconds;
        public void Close() { if (AcceptInput()) Dismiss(); }
        public void SetCloseLabel(string label) => reboundClose.text = "F";
        public void Dismiss()
        {
            if (!IsOpen) return;
            var completed = onClosed;
            onClosed = null;
            window.SetActive(false);
            Release();
            input?.SuppressGameplay();
            completed?.Invoke();
        }

        void RefreshKeyLabels()
        {
            if (!input) return;
            if (attackLetter) { attackLetter.text = input.Label(GameAction.Weapon1Basic); LayoutAttackKey(); }
            if (swapNote) swapNote.text = SwapNote(input.Label(GameAction.SwapWeaponOrder), input.Label(GameAction.Weapon1Basic), input.Label(GameAction.Weapon2Basic));
        }
        // A rebound key or controller label can be wider than a single letter. Keep the cap and caption centred together.
        void LayoutAttackKey()
        {
            if (!attackKey || !attackLetter || !attackCaption) return;
            float keyWidth = Mathf.Max(40, Mathf.Ceil(attackLetter.preferredWidth) + 16);
            float captionWidth = Mathf.Max(72, Mathf.Ceil(attackCaption.preferredWidth));
            float left = Mathf.Round(268 - (keyWidth + 12 + captionWidth) / 2);
            attackKey.rectTransform.anchoredPosition = new Vector2(left, -186);
            attackKey.rectTransform.sizeDelta = new Vector2(keyWidth, 40);
            attackLetter.rectTransform.anchoredPosition = new Vector2(left, -186);
            attackLetter.rectTransform.sizeDelta = new Vector2(keyWidth, 40);
            attackCaption.rectTransform.anchoredPosition = new Vector2(left + keyWidth + 12, -181);
            attackCaption.rectTransform.sizeDelta = new Vector2(captionWidth, 50);
        }
        static string SwapNote(string swap, string a, string b) => $"* {swap} 키로 {a} 칸과 {b} 칸의 무기를 서로 교체";

        void RefreshPage()
        {
            RefreshKeyLabels();
            int artworkPage = replayPages ? -1 : systemPages ? PageIndex : rewardPages ? PageIndex + 1 : 0;
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == artworkPage);
            if (replayContent) replayContent.gameObject.SetActive(replayPages && artworkPage < 0);
            pageChanged?.Invoke(PageIndex);
            bool multiple = rewardPages || replayPages || systemPages;
            previous.gameObject.SetActive(CanGoPrevious);
            next.gameObject.SetActive(CanGoNext);
            number.gameObject.SetActive(multiple);
            previous.interactable = CanGoPrevious;
            next.interactable = CanGoNext;
            number.text = (PageIndex + 1) + " / " + PageCount;
        }

        void Release() { onClosed = null; scope?.Dispose(); scope = null; }
        void OnDisable() { fixedClose?.Disable();fixedCloseRequested=false; Canvas.willRenderCanvases -= ConfigureFontSampling; if (window) window.SetActive(false); Release(); input?.SuppressGameplay(); }
        void OnDestroy() { fixedClose?.Dispose();fixedClose=null;Release(); }

        static Sprite Art(string name) => Resources.Load<Sprite>("FirstRunTutorial/" + name);
        static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        static Image Picture(Transform parent, string name, float x, float y, float w, float h, Sprite sprite, Color color)
        {
            var image = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }
        static Button Control(Transform parent, string name, float x, float y, float w, float h, Sprite sprite)
        {
            var image = Picture(parent, name, x, y, w, h, sprite, Color.white);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1, 1, .9f);
            colors.pressedColor = new Color(.75f, .75f, .7f);
            colors.disabledColor = new Color(1, 1, 1, .28f);
            button.colors = colors;
            return button;
        }

        /// <summary>Editor authoring entry point; saved prefabs contain real Images and Buttons.</summary>
        public void Build()
        {
            if (content) { EnsurePixelFrame(); return; }
            ConfigureFontSampling();
            var canvas = GetComponent<Canvas>();
            if (!canvas) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 180;
            scaler = GetComponent<CanvasScaler>();
            if (!scaler) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
            var safe = Box(transform, "960x540 game viewport", 0, 0, CanvasWidth, CanvasHeight);
            safe.anchorMin = safe.anchorMax = safe.pivot = Vector2.one * .5f;
            safe.anchoredPosition = Vector2.zero;
            window = safe.gameObject;
            var shade = Picture(safe, "Live world dim", 0, 0, 960, 540, null, new Color(0, 0, 0, .42f));
            shade.raycastTarget = true;
            var panel = Box(safe, "Approved tutorial panel", 212, 114, 536, 312);
            ApprovedTheme.CreateFrame(panel, 0, 0, 536, 312);
            content = Picture(panel, "Live tutorial content", 0, 0, 536, 256, null, Color.clear);
            pages = new GameObject[6];
            string[] titles = { "조작", "스케치", "잉크", "팔레트", "활성화", "내구도" };
            for (int i = 0; i < pages.Length; i++)
            {
                var page = Box(content.transform, titles[i], 0, 0, 536, 256);
                pages[i] = page.gameObject;
                Label(page, titles[i], 20, 14, 160, 32, 16).alignment = TextAnchor.MiddleLeft;
            }
            var attackPage = pages[0].transform;
            Icon(attackPage, "grass-line", 96, 144, 1);
            Icon(attackPage, "hero-idle", 137, 89, 1);
            Icon(attackPage, "arrow", 250, 106, 1);
            Icon(attackPage, "hero-punch", 304, 89, 1);
            attackKey = Picture(attackPage, "A key", 213, 186, 40, 40, Art("Pixels/keycap"), Color.white);
            attackKey.type = Image.Type.Sliced;
            attackLetter = Label(attackPage, "A", 212, 185, 42, 42, 21);
            attackLetter.alignByGeometry = true;
            attackCaption = Label(attackPage, "공격", 261, 181, 72, 50, 26);
            LayoutAttackKey();

            var sketch = pages[1].transform;
            Picture(sketch, "Sketch card", 136, 51, 100, 98, Art("Pixels/keycap"), Color.white);
            Icon(sketch, "sketch", 154, 68, 2); Icon(sketch, "arrow", 260, 87, 2); Icon(sketch, "sword", 333, 68, 2);
            Label(sketch, "스케치", 138, 151, 96, 28, 14); Label(sketch, "무기", 317, 151, 96, 28, 14);
            Phrase(sketch, 199, "", "활성화", " 후 무기로 사용 가능", new Color32(107, 121, 79, 255));
            // A weapon and bare hands, or two weapons, trade A and S with the swap key (user decision 2026-11-13).
            swapNote = Label(sketch, SwapNote("X", "A", "S"), 16, 230, 504, 20, 12);
            swapNote.color = new Color32(228, 139, 119, 255);

            var ink = pages[2].transform;
            Icon(ink, "ink", 122, 59, 2);
            var line = new Color32(235, 229, 211, 255);
            Picture(ink, "Branch stem", 184, 88, 22, 2, null, line);
            Picture(ink, "Weapon branches", 205, 59, 2, 58, null, line);
            for (int row = 0; row < 2; row++)
            {
                int y = 36 + row * 57;
                Picture(ink, "Connector", 206, y + 23, 15, 2, null, line);
                Icon(ink, "arrow", 216, y + 16, 1);
                Icon(ink, row == 0 ? "sword" : "hammer", 241, y - 10, 2);
                for (int column = 0; column < 3; column++)
                {
                    bool filled = column == 0 || (column == 1 && row == 0);
                    Picture(ink, "Weapon slot", 318 + column * 35, y + 10, 32, 24, Art(filled ? "Pixels/slot" : "Pixels/slot-empty"),
                        filled ? new Color32(162, 91, 203, 255) : new Color32(243, 235, 217, 255));
                }
            }
            Phrase(ink, 153, "잉크 장착으로 무기에 ", "효과 부여", "", new Color32(107, 121, 79, 255));
            Phrase(ink, 187, "같은 무기의 여러 칸과 다른 무기에 ", "중복 장착", " 가능", new Color32(130, 75, 160, 255));
            Note(ink, "* 잉크는 획득당 10회, 칸마다 1회 사용 / 같은 잉크 재획득 가능");

            var palette = pages[3].transform;
            Icon(palette, "ink", 252, 13, 1);
            Picture(palette, "Stem", 267, 46, 2, 15, null, line);
            Picture(palette, "Branch", 193, 60, 151, 2, null, line);
            foreach (int x in new[] { 193, 342 })
            {
                Picture(palette, "Drop", x, 60, 2, 20, null, line);
                for (int n = 1; n <= 5; n++)
                {
                    Picture(palette, "Tip", x-n, 79-n, 2, 2, null, line);
                    Picture(palette, "Tip", x+n, 79-n, 2, 2, null, line);
                }
            }
            Slot(palette, 162, 87, new Color32(130, 126, 118, 255), true);
            Slot(palette, 311, 87, new Color32(162, 91, 203, 255), true);
            Label(palette, "기본 효과", 134, 135, 120, 28, 14);
            Label(palette, "강화 효과", 283, 135, 120, 28, 14);
            Phrase(palette, 180, "", "모든 색", " 잉크 장착 가능", new Color32(130, 75, 160, 255));
            Phrase(palette, 212, "슬롯과 ", "같은 색", " 장착 시 효과 강화", new Color32(107, 121, 79, 255));

            var activation = pages[4].transform;
            Label(activation, "활성화 전", 48, 60, 176, 24, 15);
            Label(activation, "활성화 후", 316, 60, 176, 24, 15);
            for (int i = 0; i < 3; i++)
            {
                Slot(activation, 32 + i * 72, 98, new Color32(130, 126, 118, 255), i == 0);
                Slot(activation, 300 + i * 72, 98, new Color32(130, 126, 118, 255), i == 0);
                Icon(activation, "lock", 332 + i * 72, 120, 2);
            }
            Icon(activation, "arrow", 246, 106, 2);
            Phrase(activation, 178, "활성화 후 ", "팔레트 구성 변경 불가", "", new Color32(153, 100, 99, 255));
            Note(activation, "* 빈 슬롯에도 추가 장착 불가", 218);

            var durability = pages[5].transform;
            Icon(durability, "sword", 145, 35, 2); Icon(durability, "arrow", 245, 56, 2); Icon(durability, "broken-sword", 335, 35, 2);
            Picture(durability, "Gauge", 128, 105, 98, 14, Art("Pixels/gauge"), Color.white);
            Picture(durability, "Remaining", 131, 107, 72, 10, Art("Pixels/gauge-fill"), Color.white);
            Picture(durability, "Empty gauge", 318, 105, 98, 14, Art("Pixels/gauge"), Color.white);
            Icon(durability, "ink", 252, 119, 1);
            Label(durability, "가한 피해에 따라 내구도 소모", 20, 162, 496, 28, 16);
            Phrase(durability, 192, "내구도 소진 시 ", "무기 소멸", "", new Color32(153, 100, 99, 255));
            Note(durability, "* 획득한 잉크는 유지", 230);

            Picture(panel, "Footer divider", 20, 256, 496, 1, null, new Color(1, 1, 1, .25f));
            previous = Control(panel, "Previous page", 190, 274, ApprovedTheme.ControlHintHeight, ApprovedTheme.ControlHintHeight, Art("Pixels/keycap"));
            var left = Icon(previous.transform, "arrow", 0, 4, 1); left.rectTransform.localScale = new Vector3(-1, 1, 1); left.rectTransform.anchoredPosition = new Vector2(ApprovedTheme.ControlHintHeight, -3);
            next = Control(panel, "Next page", 320, 274, ApprovedTheme.ControlHintHeight, ApprovedTheme.ControlHintHeight, Art("Pixels/keycap"));
            Icon(next.transform, "arrow", 0, 4, 1);
            close = Control(panel, "F close", 450, 274, ApprovedTheme.ControlHintHeight, ApprovedTheme.ControlHintHeight, Art("Pixels/keycap"));
            reboundClose = Label(close.transform, "F", 0, 0, ApprovedTheme.ControlHintHeight, ApprovedTheme.ControlHintHeight, ApprovedTheme.ControlHintFontSize);
            reboundClose.alignByGeometry = true;
            Label(panel, "닫기", 478, 274, 44, ApprovedTheme.ControlHintHeight, ApprovedTheme.ControlHintFontSize);
            number = Label(panel, "1 / 5", 230, 274, 75, ApprovedTheme.ControlHintHeight, ApprovedTheme.ControlHintFontSize);
            FitFooterHints();
            Fit();
            window.SetActive(false);
        }

        void EnsurePixelFrame()
        {
            if (!content) return;
            var panel = content.transform.parent;
            var current = panel.Find("Approved pixel frame");
            if (current)
            {
                current.GetComponent<PixelFramedPanel>().Configure(ApprovedTheme.FrameLarge);
                return;
            }
            // Serialized tutorial prefabs still contain the previous panel Image.
            foreach (var name in new[] { "Approved frame", "Native pixel panel" })
            {
                var legacy = panel.Find(name); if (legacy) legacy.gameObject.SetActive(false);
            }
            var frame = ApprovedTheme.CreateFrame(panel, 0, 0, 536, 312);
            frame.transform.SetAsFirstSibling();
        }

        void Fit()
        {
            if (!scaler) return;
            var canvas = GetComponent<Canvas>();
            var target = canvas && canvas.worldCamera ? canvas.worldCamera.targetTexture : null;
            float width = target ? target.width : Screen.width, height = target ? target.height : Screen.height;
            scaler.scaleFactor = Mathf.Max(.01f, Mathf.Min(width / CanvasWidth, height / CanvasHeight));
        }

        static void ConfigureFontSampling()
        {
            foreach (var font in Resources.LoadAll<Font>("FirstRunTutorial/Fonts"))
            {
                if (font.material && font.material.mainTexture)
                    font.material.mainTexture.filterMode = FilterMode.Point;
            }
        }

        const int NativeFontSize = 12;

        static Text Label(Transform parent, string value, float x, float y, float w, float h, int size)
        {
            var text = Box(parent, value, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = ApprovedTheme.Font ?? Resources.Load<Font>("FirstRunTutorial/Fonts/Galmuri11") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(243, 239, 222, 255); text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignByGeometry = true;
            return text;
        }

        static Image Icon(Transform parent, string name, int x, int y, int scale)
        {
            var sprite = Art("Pixels/" + name);
            return Picture(parent, name, x, y, sprite ? sprite.rect.width * scale : 32,
                sprite ? sprite.rect.height * scale : 32, sprite, Color.white);
        }

        static void Slot(Transform parent, int x, int y, Color color, bool filled)
        {
            Picture(parent, "Palette slot", x, y, 64, 48, Art("Pixels/slot"), color);
            if (filled) Icon(parent, "ink", x + 16, y + 8, 1);
        }

        static void Note(Transform parent, string text, int y = 225)
        {
            Label(parent, text, 16, y, 504, 20, 12).color = new Color32(228, 139, 119, 255);
        }

        static void Phrase(Transform parent, int y, string prefix, string emphasis, string suffix, Color tint)
        {
            // Static pixel fonts need the real bold face, not a rich-text style override.
            var lead = Label(parent, prefix, 0, y, 1000, 30, 16);
            var focus = Label(parent, emphasis, 0, y, 1000, 30, 16);
            var tail = Label(parent, suffix, 0, y, 1000, 30, 16);
            var bold = ApprovedTheme.Font ?? Resources.Load<Font>("FirstRunTutorial/Fonts/Galmuri11-Bold");
            if (bold) focus.font = bold;
            float a = Mathf.Ceil(lead.preferredWidth), b = Mathf.Ceil(focus.preferredWidth), c = Mathf.Ceil(tail.preferredWidth);
            float x = Mathf.Round(268 - (a + b + c) / 2);
            Picture(parent, "Keyword brush", x + a - 8, y + 1, b + 16, 28, ApprovedTheme.Sprite("common/maintenance-palette-v1__assets__selection-wash-v1") ?? Art("Pixels/brush"), new Color(tint.r, tint.g, tint.b, .65f))
                .transform.SetSiblingIndex(lead.transform.GetSiblingIndex());
            Text[] words = { lead, focus, tail }; float[] widths = { a, b, c };
            for (int i = 0; i < words.Length; i++)
            {
                var rect = words[i].rectTransform;
                rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(widths[i], 30);
                x += widths[i];
            }
        }
    }
}
