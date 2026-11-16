using System.Collections.Generic;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Narrative.Opening;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    // The playable opening's screens that follow the HTML prototype (04_테스트/오프닝 테스트/튜토리얼길테스트.html):
    // the investigator's notebook the opening starts with, the guide ghost's keys and its 「Space 넘기기」, the keycaps
    // shared with the top strip, and the strip itself (a full-width dark band fading up and down, user request 2026-11-02).
    public sealed partial class TemporaryGameGui
    {
        static readonly Color InkColour = new Color32(0x1f, 0x1b, 0x19, 255), KeyFace = new Color32(255, 255, 255, 255),
            KeyLit = new Color32(195, 202, 207, 255), StripInk = new Color32(0x17, 0x17, 0x0f, 255);

        Image Plain(Transform parent, float x, float y, float w, float h, Color color, string name = "Plain")
        {
            var image = Picture("", parent, x, y, w, h, color); image.sprite = null; image.type = Image.Type.Simple; image.name = name;
            return image;
        }

        // A keycap 28 px high: ink frame, paper face (yellow when lit), the key in ink. Returns its width.
        float Keycap(Transform parent, string key, float x, float y, out RectTransform cap, out Image face)
        {
            key = LessonKey(key);
            cap = Rect("Key " + key, parent, x, y, 28, 28);
            var frame = Plain(cap, 0, 0, 28, 28, InkColour, "Frame");
            face = Plain(cap, 2, 2, 24, 22, KeyFace, "Face");
            var label = Label(key, cap, 0, 0, 28, 26, 12, InkColour); label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = Mathf.Max(28, Mathf.Ceil(label.preferredWidth) + 16);
            cap.sizeDelta = new Vector2(w, 28); frame.rectTransform.sizeDelta = new Vector2(w, 28); face.rectTransform.sizeDelta = new Vector2(w - 4, 22);
            label.rectTransform.sizeDelta = new Vector2(w, 26);
            return w;
        }

        static Sprite stripSprite;
        // The top strip's band: 60 rows, each as dark as sin(pi (row + .5) / 60) of the peak, the same curve up and down.
        static Sprite StripSprite()
        {
            if (stripSprite) return stripSprite;
            const int rows = 60; const float peak = .45f;
            var t = new Texture2D(1, rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < rows; y++) t.SetPixel(0, y, new Color(1, 1, 1, peak * Mathf.Sin(Mathf.PI * (y + .5f) / rows)));
            t.Apply(false, true);
            stripSprite = UnityEngine.Sprite.Create(t, new Rect(0, 0, 1, rows), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            stripSprite.hideFlags = HideFlags.DontSave;
            return stripSprite;
        }

        static void Stroke(Graphic text, Color colour)
        {
            foreach (var d in new[] { new Vector2(2, 0), new Vector2(-2, 0), new Vector2(0, 2), new Vector2(0, -2) })
            { var o = text.gameObject.AddComponent<Shadow>(); o.effectColor = colour; o.effectDistance = d; }
        }

        // Closed cover, curved written leaves, then a feathered illustration over the same writing.
        const float NotebookClose = .5f, NotebookDone = .55f, NotebookOpenFade = .6f;
        const float NotebookLineInterval = .85f, NotebookLineFade = .6f;
        RectTransform notebookRoot, notebookBook;
        CanvasGroup notebookGroup;
        NotebookBookView notebookView;
        Image notebookOpenFade, notebookCircle;
        Text notebookPageHint;
        readonly List<Text> notebookLines = new List<Text>();
        readonly List<Text> notebookEmphasis = new List<Text>(), notebookSuffix = new List<Text>();
        int notebookPage, notebookFrame, notebookSound;
        float notebookClock, notebookPageAt, notebookCloseAt = -1, notebookFadeAt = -1;
        bool notebookOpen;
        public bool NotebookShowing => notebookOpen;

        void BuildNotebook()
        {
            notebookRoot = Rect("조사 수첩", stage, 0, 0, 960, 540);
            var desk = Rect("Desk", notebookRoot, 0, 0, 960, 540).gameObject.AddComponent<RawImage>();
            desk.texture = Resources.Load<Texture2D>("NotebookOpening/desk"); desk.raycastTarget = false;
            notebookGroup = notebookRoot.gameObject.AddComponent<CanvasGroup>(); notebookGroup.blocksRaycasts = false;
            notebookBook = Rect("Book", notebookRoot, 0, 0, 960, 540);
            notebookView = notebookBook.gameObject.AddComponent<NotebookBookView>();
            notebookView.Initialize(OpeningData.notebook.pages.Length);
            notebookCircle = Plain(notebookBook, 0, 0, 1, 1, Color.white, "Red circle");
            notebookCircle.rectTransform.pivot = new Vector2(.5f, .5f); notebookCircle.enabled = false;
            for (int i = 0; i < 3; i++)
            {
                var line = Label("", notebookRoot, 0, 443 + i * 26, 960, 26, 14, new Color32(231, 220, 198, 255));
                line.font = font; line.supportRichText = false; line.alignment = TextAnchor.MiddleLeft;
                line.horizontalOverflow = HorizontalWrapMode.Overflow; notebookLines.Add(line);
                var emphasis = Label("", notebookRoot, 0, 443 + i * 26, 1, 26, 19, new Color32(246, 229, 190, 255));
                emphasis.font = boldFont ? boldFont : font; emphasis.fontStyle = boldFont ? FontStyle.Normal : FontStyle.Bold;
                emphasis.horizontalOverflow = HorizontalWrapMode.Overflow; emphasis.verticalOverflow = VerticalWrapMode.Overflow;
                notebookEmphasis.Add(emphasis);
                var suffix = Label("", notebookRoot, 0, 443 + i * 26, 1, 26, 14, line.color);
                suffix.font = font; suffix.horizontalOverflow = HorizontalWrapMode.Overflow; notebookSuffix.Add(suffix);
            }
            notebookPageHint = Label("", notebookRoot, 600, 514, 344, MenuHintHeight, MenuHintFontSize, new Color32(232, 220, 196, 255));
            Deviant.UI.Theme.ApprovedTheme.ControlHint(notebookPageHint);
            notebookPageHint.alignment = TextAnchor.MiddleRight;
            notebookRoot.gameObject.SetActive(false);
            notebookOpenFade = Plain(stage, 0, 0, 960, 540, new Color32(8, 7, 6, 255), "수첩 뒤 밝아짐");
            notebookOpenFade.gameObject.SetActive(false);
        }
        void ShowNotebook()
        {
            var nb = OpeningData?.notebook;
            if (nb?.pages == null || nb.pages.Length == 0) { notebookOpen = false; return; }
            if (!notebookRoot) BuildNotebook();
            notebookOpen = true; notebookPage = 0; notebookClock = notebookPageAt = 0;
            notebookFrame = Time.frameCount; notebookSound = -1; notebookCloseAt = notebookFadeAt = -1;
            notebookGroup.alpha = 1; notebookBook.anchoredPosition = Vector2.zero;
            notebookRoot.gameObject.SetActive(true); notebookRoot.SetAsLastSibling();
            ShowNotebookPage(); TickNotebook(null);
        }
        void ShowNotebookPage()
        {
            var lines = OpeningData.notebook.pages[notebookPage].lines;
            for (int i = 0; i < notebookLines.Count; i++) LayoutNotebookLine(i, i < lines.Length ? lines[i] : "");
        }
        float NotebookNarrationAt => notebookPage == 0 ? 5.35f : NotebookBookView.TurnDuration + NotebookBookView.WrittenHold + .5f;
        void TickNotebook(Keyboard k)
        {
            float now = Time.unscaledTime;
            if (notebookFadeAt >= 0)
            {
                float a = 1 - Mathf.Clamp01((now - notebookFadeAt) / NotebookOpenFade);
                notebookOpenFade.color = new Color(8 / 255f, 7 / 255f, 6 / 255f, a);
                if (a <= 0) { notebookFadeAt = -1; notebookOpenFade.gameObject.SetActive(false); }
            }
            if (!notebookOpen || !notebookRoot) return;
            if (notebookFrame != Time.frameCount) { notebookClock += Mathf.Min(Time.unscaledDeltaTime, .05f); notebookFrame = Time.frameCount; }
            float pageAge = notebookClock - notebookPageAt;
            bool opening = notebookClock < NotebookBookView.IntroEnd;
            bool turning = notebookPage > 0 && pageAge < NotebookBookView.TurnDuration;
            if (notebookClock >= 1.32f && notebookSound < 0) { notebookSound = 0; PresentationSfx.PlaySelected("sfx_story_page_turn"); }
            if (notebookClock >= 2.58f && notebookClock < 3.75f)
            {
                int sound = 1 + Mathf.FloorToInt((notebookClock - 2.58f) / .165f);
                if (sound > notebookSound) { notebookSound = sound; PresentationSfx.PlaySelected("sfx_story_page_turn"); }
            }
            var pages = OpeningData.notebook.pages;
            if (Controls && Controls.Pressed(GameAction.Advance))
            {
                TakeAdvance();
                if (!opening && !turning && notebookCloseAt < 0)
                {
                    float complete = NotebookNarrationAt + (pages[notebookPage].lines.Length - 1) * NotebookLineInterval + NotebookLineFade;
                    if (pageAge < complete) { notebookPageAt = notebookClock - complete; pageAge = complete; }
                    else if (notebookPage < pages.Length - 1)
                    {
                        notebookPage++; notebookPageAt = notebookClock; pageAge = 0;
                        ShowNotebookPage(); PresentationSfx.PlaySelected("sfx_story_page_turn");
                    }
                    else notebookCloseAt = notebookClock;
                }
            }
            float closeAge = notebookCloseAt < 0 ? 0 : notebookClock - notebookCloseAt;
            float close = Mathf.Clamp01(closeAge / NotebookClose);
            if (notebookCloseAt >= 0 && closeAge > NotebookDone)
            {
                notebookOpen = false; notebookRoot.gameObject.SetActive(false);
                if (village) village.QuietOpeningRoad();
                notebookFadeAt = now; notebookOpenFade.gameObject.SetActive(true); notebookOpenFade.transform.SetAsLastSibling();
                notebookOpenFade.color = new Color32(8, 7, 6, 255); return;
            }
            notebookView.Draw(notebookClock, notebookPage, pageAge);
            DrawNotebookCircle(pages[notebookPage], !opening && !turning ? pageAge : -1);
            notebookBook.anchoredPosition = new Vector2(0, -close * close * 40);
            notebookGroup.alpha = 1 - close;
            for (int i = 0; i < notebookLines.Count; i++)
                SetNotebookLineAlpha(i, NotebookBookView.Smooth((pageAge - NotebookNarrationAt - i * NotebookLineInterval) / NotebookLineFade));
            notebookPageHint.text = opening || turning ? "" : $"{notebookPage + 1} / {pages.Length}   {KeyLabel(GameAction.Advance)} 넘기기";
        }
        // The map: the red circle drawn round the village at the road's end once the picture is in, frame by frame, held
        // on its last. It rides on the resting right page, so it sinks and fades with the book when it closes.
        void DrawNotebookCircle(OpeningNotebookPage page, float pageAge)
        {
            var nb = OpeningData.notebook;
            float since = pageAge - NotebookBookView.RevealEnd(notebookPage == 0);
            bool show = page.circled && pageAge >= 0 && since >= 0 && nb.circleFrames > 0;
            var ring = show ? Resources.Load<Sprite>($"{nb.circle}{Mathf.Min(nb.circleFrames, Mathf.FloorToInt(since / Mathf.Max(.01f, nb.circleSeconds)) + 1):00}") : null;
            notebookCircle.enabled = ring;
            if (!ring) return;
            notebookCircle.sprite = ring;
            var at = notebookView.SpreadPoint(nb.circleX, nb.circleY, out float scale);
            notebookCircle.rectTransform.anchoredPosition = at;
            notebookCircle.rectTransform.sizeDelta = ring.rect.size * nb.circleScale * scale;
            notebookCircle.transform.SetAsLastSibling();
        }
        void LayoutNotebookLine(int i, string text)
        {
            const string keyword = "스케치화";
            int at = text.IndexOf(keyword, System.StringComparison.Ordinal);
            var before = notebookLines[i]; var emphasis = notebookEmphasis[i]; var after = notebookSuffix[i];
            before.text = at < 0 ? text : text.Substring(0, at); emphasis.text = at < 0 ? "" : keyword;
            after.text = at < 0 ? "" : text.Substring(at + keyword.Length);
            float a = before.preferredWidth, b = emphasis.preferredWidth, c = after.preferredWidth;
            float x = Mathf.Round((960 - a - b - c) / 2);
            Place(before, x, 443 + i * 26, a + 1, 26); Place(emphasis, x + a, 443 + i * 26, b + 1, 26);
            Place(after, x + a + b, 443 + i * 26, c + 1, 26);
        }
        void SetNotebookLineAlpha(int i, float alpha)
        {
            foreach (var line in new[] { notebookLines[i], notebookEmphasis[i], notebookSuffix[i] })
                line.color = new Color(line.color.r, line.color.g, line.color.b, alpha);
        }
        void CloseNotebook()
        {
            notebookOpen = false; notebookFadeAt = -1;
            if (notebookRoot) notebookRoot.gameObject.SetActive(false);
            if (notebookOpenFade) notebookOpenFade.gameObject.SetActive(false);
        }

        // ---- Guide ghost (HTML drawModal): no card; the keys float over the ghost's head with the move's name, each key
        // pressed (yellow, sunk) as the ghost presses it, and 「Space 넘기기」 at the bottom once it has shown the move.
        // Space closes it only when pressed anew after .8 s and once the ghost has finished a run; that Space never jumps.
        const float GhostCloseAfter = .8f, GhostKeyLit = .22f, GhostHeldLit = .03f;
        RectTransform ghostKeys;
        CanvasGroup ghostKeysGroup;
        Text ghostText, ghostSkip;
        readonly List<(RectTransform cap, Image face, string code, int same, int order)> ghostCaps = new List<(RectTransform, Image, string, int, int)>();
        StartingVillageWorld.GhostTrack ghostLesson;
        float ghostLessonAt;
        bool ghostSpaceBlocked, ghostClosing;

        void BuildGhostOverlay()
        {
            ghostKeys = Rect("유령 키", stage, 0, 0, 1, 28);
            ghostKeysGroup = ghostKeys.gameObject.AddComponent<CanvasGroup>(); ghostKeysGroup.blocksRaycasts = false;
            ghostText = Label("", ghostKeys, 0, 0, 10, 28, 16, InkColour); ghostText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stroke(ghostText, new Color(1, 250 / 255f, 240 / 255f, .9f));
            ghostSkip = BindPrompt(Label("", stage, 0, 540 - 22 - 12, 960, MenuHintHeight, MenuHintFontSize, KeyFace), "[{Jump}] 넘기기"); ghostSkip.alignment = TextAnchor.MiddleCenter;
            Deviant.UI.Theme.ApprovedTheme.ControlHint(ghostSkip);
            Stroke(ghostSkip, new Color(23 / 255f, 23 / 255f, 15 / 255f, .6f));
            ghostKeys.gameObject.SetActive(false); ghostSkip.gameObject.SetActive(false);
        }

        void ShowGhostLesson(StartingVillageWorld.GhostTrack track, string[] keys, string text)
        {
            if (!ghostKeys) BuildGhostOverlay();
            ghostDisplayKeys = keys; ghostText.text = text;
            ghostLesson = track; ghostLessonAt = Time.unscaledTime; ghostClosing = false;
            LayoutGhostKeys();
            ghostSpaceBlocked = Controls && Controls.Read(GameAction.Jump).IsPressed;
            village.StartGhost(track);
            ghostKeys.gameObject.SetActive(true); ghostKeys.SetAsLastSibling();
            lessonPause?.Dispose();
            lessonPause = village.World.arena.Controls.OpenScope("opening lesson");
            lessonPause.PauseSimulation(); lessonPause.BlockInput(InputChannels.Combat | InputChannels.Interaction);
            TickGhostLesson(null);
        }
        void LayoutGhostKeys()
        {
            var keys = ghostDisplayKeys; var track = ghostLesson;
            ghostKeySignature = KeySignature(keys);
            foreach (var c in ghostCaps) if (c.cap) { c.cap.gameObject.SetActive(false); Destroy(c.cap.gameObject); }
            ghostCaps.Clear();
            float x = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                string code = track.codes != null && i < track.codes.Length ? track.codes[i] : keys[i];
                int same = 0, order = 0;
                for (int j = 0; j < keys.Length; j++) { string other = track.codes != null && j < track.codes.Length ? track.codes[j] : keys[j]; if (other == code) { same++; if (j <= i) order++; } }
                float w = Keycap(ghostKeys, keys[i], x, 0, out var cap, out var face);
                ghostCaps.Add((cap, face, code, same, order)); x += w + 4;
            }
            ghostText.rectTransform.anchoredPosition = new Vector2(x + 6 - 4, 0);
            ghostText.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(ghostText.preferredWidth) + 4, 28);
            ghostKeys.sizeDelta = new Vector2(x + 6 + Mathf.Ceil(ghostText.preferredWidth), 28);
        }

        void HideGhostLesson()
        {
            ghostLesson = null; ghostClosing = false;
            if (village) village.StopGhost();
            if (ghostKeys) ghostKeys.gameObject.SetActive(false);
            if (ghostSkip) ghostSkip.gameObject.SetActive(false);
        }

        // Returns true when the lesson closed this frame.
        bool TickGhostLesson(Keyboard k)
        {
            if (ghostLesson == null) return false;
            float now = Time.unscaledTime, age = now - ghostLessonAt;
            bool ready = age > GhostCloseAfter && village.GhostShownOnce;
            if (Controls)
            {
                bool held = Controls.Read(GameAction.Jump).IsPressed;
                if (ghostSpaceBlocked && !held) ghostSpaceBlocked = false;
                if (!ghostSpaceBlocked && ready && Controls.Take(GameAction.Jump)) { Controls.SuppressGameplay(); ghostClosing = true; }
                if (ghostClosing && !held) { CloseLesson(); return true; }
            }
            ghostSkip.text = KeyLabel(GameAction.Jump) + " 넘기기";
            var file = village.Ghosts; int frame = village.GhostFrame;
            float fadeIn = Mathf.Clamp01((age - file.wait) / file.fade);
            ghostKeysGroup.alpha = fadeIn;
            // Over the higher of the ghost's head and the hero's.
            var view = village.World.View; var player = village.World.arena.Player;
            var feet = village.GhostFeet; float headY = Mathf.Max(feet.y, player.Position.y) + 3.1f;
            var screen = view.WorldToViewportPoint(new Vector3(feet.x, headY, 0));
            float hx = screen.x * 960, hy = (1 - screen.y) * 540, total = ghostKeys.sizeDelta.x;
            ghostKeys.anchoredPosition = new Vector2(Mathf.Round(hx - total / 2), -Mathf.Round(Mathf.Max(70, hy - 28)));
            // Which keys are down at this frame of the recording.
            for (int i = 0; i < ghostCaps.Count; i++)
            {
                var c = ghostCaps[i]; bool down = false;
                if (frame >= 0 && !village.GhostEnded)
                {
                    int last = -1, count = 0;
                    for (int f = 0; f <= frame; f++) if (Has(ghostLesson.tapped[f], c.code)) { last = f; count++; }
                    down = last >= 0 && (frame - last) / file.fps < GhostKeyLit;
                    if (c.same > 1) down = down && c.order == count;
                    if (c.same == 1 && c.code != "Space" && Has(ghostLesson.held[frame], c.code)) down = true;
                }
                c.face.color = down ? KeyLit : KeyFace;
                float sunk = down ? 3 : 0;
                c.face.rectTransform.anchoredPosition = new Vector2(2, -(2 + sunk)); c.face.rectTransform.sizeDelta = new Vector2(c.cap.sizeDelta.x - 4, 22 - sunk);
                c.cap.Find("Frame").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -sunk);
                c.cap.Find("Frame").GetComponent<RectTransform>().sizeDelta = new Vector2(c.cap.sizeDelta.x, 28 - sunk);
                c.cap.Find("Text").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -sunk / 2);
            }
            bool skip = ready;
            ghostSkip.gameObject.SetActive(skip);
            if (skip) { ghostSkip.color = now % 1 < .65f ? (Color)KeyFace : new Color32(0xcf, 0xc6, 0xb4, 255); ghostSkip.transform.SetAsLastSibling(); }
            return false;
        }

        // Whether a "+"-joined key list holds code, without splitting it (this runs for every key every frame).
        static bool Has(string keys, string code)
        {
            if (string.IsNullOrEmpty(keys) || string.IsNullOrEmpty(code)) return false;
            for (int i = 0; i <= keys.Length;)
            {
                int end = keys.IndexOf('+', i); if (end < 0) end = keys.Length;
                if (end - i == code.Length && string.CompareOrdinal(keys, i, code, 0, code.Length) == 0) return true;
                i = end + 1;
            }
            return false;
        }
    }
}
