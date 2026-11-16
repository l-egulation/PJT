using System;
using Deviant.UI.Theme;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // The journal kit shared by the village and chapter-1 screens.
    public sealed partial class TemporaryGameGui
    {
        // Adopted journal design, drawn with its own art and numbers: output/gui/journal-preview (faithful.css, hub-polish.css,
        // 2026-11-02). Art is resources ui.journal-preview deployed to TemporaryGUI/Resources/JournalGUI; text is Malgun
        // Gothic regular and bold (JournalFonts) through the JournalText shader. Positions are the HTML stage's, in px.
        static readonly Color JInk = new Color32(23, 23, 17, 255), JPaperText = new Color32(255, 255, 255, 255),
            JMuted = new Color32(132, 119, 97, 255), JDisabled = new Color32(139, 128, 106, 255), JWarning = new Color32(154, 59, 44, 255),
            JSlotMark = new Color32(155, 144, 122, 255), JDark = new Color32(36, 35, 29, 255), JRule = new Color32(184, 170, 140, 255),
            JSoftOnInk = new Color32(216, 204, 177, 255), JBubble = new Color32(255, 255, 255, 255), JBubbleEdge = new Color32(57, 54, 44, 255);
        // Every book stands centred on the 960 stage (playtest 2026-11-06: they were right-aligned to x 940 and read as
        // pushed right): x = (960 - width) / 2.
        const float StageW = 960, BookW = 800, BookX = (StageW - BookW) / 2, BookY = 76, BookH = 440, LeftX = 32, RightX = 420, TopY = 34, ColumnW = 348, ListY = 74;
        Font journalFont, journalBold;
        Material journalText;
        static readonly Color Veil = Deviant.UI.Theme.ApprovedTheme.MenuBackdrop;
        // Fonts and the text material for every journal screen, village or chapter.
        void InitJournal()
        {
            if (journalFont) return;
            var fonts = Resources.Load<JournalFonts>("JournalGUI/JournalFonts");
            journalFont = fonts && fonts.regular ? fonts.regular : font; journalBold = fonts && fonts.bold ? fonts.bold : journalFont;
            journalFont.RequestCharactersInTexture("가", journalFont.fontSize);
            var shader = Resources.Load<Shader>("JournalGUI/JournalText"); if (shader && shader.isSupported) journalText = new Material(shader);
        }

        static readonly Dictionary<string, Sprite> JournalSprites = new Dictionary<string, Sprite>();
        // CSS border-image: `slice` source px at each edge (the sides only for the 240x56 faces), drawn at `width` px.
        static Sprite Art(string name, int slice = 0, bool sidesOnly = false)
        {
            string key = name + "/" + slice + (sidesOnly ? "h" : "");
            if (JournalSprites.TryGetValue(key, out var sprite) && sprite) return sprite;
            var texture = Resources.Load<Texture2D>("JournalGUI/" + name);
            if (!texture) return null;
            var border = slice == 0 ? Vector4.zero : sidesOnly ? new Vector4(slice, 0, slice, 0) : Vector4.one * slice;
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            sprite.name = name; return JournalSprites[key] = sprite;
        }
        Image Face(string art, Transform parent, float x, float y, float w, float h, int slice = 0, float width = 0, bool sidesOnly = false, bool raycast = false)
        {
            var image = Rect(art, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = Art(art, slice, sidesOnly); image.raycastTarget = raycast;
            if(UseApprovedTheme(parent)&&IsLegacySurface(art)){image.sprite=null;image.color=Color.clear;}
            image.type = slice > 0 ? Image.Type.Sliced : Image.Type.Simple; image.pixelsPerUnitMultiplier = slice > 0 ? slice / width : 1;
            return image;
        }
        Image Fill(Transform parent, float x, float y, float w, float h, Color color)
        { var image = Rect("", parent, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        Image Rounded(string name, Transform parent, float x, float y, float w, float h, int radius, int border, Color fill, Color edge, bool raycast = false)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite(radius, border, fill, edge); image.type = Image.Type.Sliced; image.raycastTarget = raycast; return image;
        }
        Image Glyph(Sprite sprite, Transform parent, float x, float y, float w, float h, Color color)
        { var image = Rect(sprite.name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = color; image.raycastTarget = false; return image; }
        Text Words(string text, Transform parent, float x, float y, float w, float h, int size, bool bold = false, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var label = Rect("Text", parent, x, y, w, h).gameObject.AddComponent<Text>();
            bool approved=UseApprovedTheme(parent);
            label.font = approved&&ApprovedTheme.Font ? ApprovedTheme.Font : bold ? journalBold : journalFont; label.fontSize = size; if (!approved&&journalText) label.material = journalText;
            label.text = ApprovedInkText.Display(text); label.color = approved?ApprovedTextColor(color??JInk):color??JInk; label.alignment = align; label.raycastTarget = false; label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow; return label;
        }
        // A wrapped paragraph with a CSS line-height; LineCount sizes the block below it.
        Text Paragraph(Transform parent, float x, float y, float w, int size, float lineHeight, Color color)
        {
            var label = Words("", parent, x, y, w, lineHeight, size, false, color, TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.lineSpacing = lineHeight / (size * label.font.lineHeight / label.font.fontSize); return label;
        }
        static int LineCount(Text label)
        {
            if (string.IsNullOrEmpty(label.text)) return 0;
            var generator = label.cachedTextGeneratorForLayout;
            generator.Populate(label.text, label.GetGenerationSettings(new Vector2(label.rectTransform.rect.width, 0)));
            return Mathf.Max(1, generator.lineCount);
        }
        static float Width(Text label) => Mathf.Ceil(label.preferredWidth);
        static void Place(Component c, float x, float y, float w, float h)
        { var rt = (RectTransform)c.transform; rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); }

        // HTML .btn: a 240x56 face whose 14 px sides draw at 9 px, stretched to the height; disabled.png while unusable.
        Button Action(string text, Transform parent, float x, float y, float w, float h, Action action, bool primary = false)
        {
            var image = Face(primary ? "primary" : "secondary", parent, x, y, w, h, 14, 9, true, true); image.name = text;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
            Words(text, image.transform, 0, 0, w, h, 16, true, primary ? JPaperText : JInk, TextAnchor.MiddleCenter);
            if(UseApprovedTheme(parent))button.gameObject.AddComponent<ApprovedActionVisual>();
            button.onClick.AddListener(() => { if (!button.interactable) return; action(); if (host) Refresh(); else if (village) RefreshHub(); });
            return button;
        }
        static void Paint(Button button, bool primary)
        {
            if(button.GetComponent<ApprovedActionVisual>())return;
            button.image.sprite = Art(!button.interactable ? "disabled" : primary ? "primary" : "secondary", 14, true);
            button.GetComponentInChildren<Text>().color = !button.interactable ? JDisabled : primary ? JPaperText : JInk;
        }

        // Small CSS shapes (rounded boxes, the lock, the close cross, the check, swatch discs), rasterised once with 4x4
        // supersampling. Colour comes from the sample function and is averaged premultiplied.
        static Sprite Drawn(string name, int w, int h, Func<float, float, Color> sample, int border = 0)
        {
            if (JournalSprites.TryGetValue(name, out var cached) && cached) return cached;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Journal " + name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++)
                { var c = sample(x + (i + .5f) / 4, y + (j + .5f) / 4); r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a; }
                pixels[(h - 1 - y) * w + x] = a > 0 ? new Color(r / a, g / a, b / a, a / 16) : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply();
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * border);
            sprite.name = name; return JournalSprites[name] = sprite;
        }
        // Signed distance to a w x h box with corner radius r (negative inside), origin at its top-left.
        static float BoxDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2) - (w / 2 - r), qy = Mathf.Abs(y - h / 2) - (h / 2 - r);
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }
        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); return (p - (a + t * ab)).magnitude; }
        static Sprite RoundedSprite(int radius, int border, Color fill, Color edge)
        {
            int size = radius * 2 + 4;
            return Drawn($"round {radius} {border} {(Color32)fill} {(Color32)edge}", size, size, (x, y) =>
            { float d = BoxDistance(x, y, size, size, radius); return d > 0 ? Color.clear : d > -border ? edge : fill; }, radius + 1);
        }
        // hub-polish.js lockGlyph: a 12x14 viewBox (shackle stroke 2, body 10x8) drawn at 11x13.
        static Sprite LockGlyph => Drawn("locked", 11, 13, (x, y) =>
        {
            var p = new Vector2(x * 12 / 11, y * 14 / 13);
            bool body = p.x >= 1 && p.x <= 11 && p.y >= 6;
            bool shackle = SegmentDistance(p, new Vector2(3, 6), new Vector2(3, 4)) <= 1 || SegmentDistance(p, new Vector2(9, 6), new Vector2(9, 4)) <= 1 ||
                p.y <= 4 && Mathf.Abs((p - new Vector2(6, 4)).magnitude - 3) <= 1;
            return body || shackle ? Color.white : Color.clear;
        });
        // faithful.css .close:after: two 45 degree bands, 12% of the 16 px box's gradient line wide.
        static Sprite CrossGlyph => Drawn("close", 16, 16, (x, y) =>
            Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x + y - 16)) / Mathf.Sqrt(2) <= 1.36f ? Color.white : Color.clear);
        static Sprite CheckGlyph => Drawn("check", 12, 12, (x, y) =>
        {
            var p = new Vector2(x, y);
            return Mathf.Min(SegmentDistance(p, new Vector2(1.5f, 6.5f), new Vector2(4.5f, 9.5f)), SegmentDistance(p, new Vector2(4.5f, 9.5f), new Vector2(10.5f, 2.5f))) <= 1.1f ? Color.white : Color.clear;
        });
        // The max-health upgrade's heart: two 11 px lobes over a downward point, drawn at 44x40.
        static readonly Color HeartRed = new Color32(174, 51, 46, 255);
        static Sprite HeartGlyph => Drawn("heart", 44, 40, (x, y) =>
        {
            var p = new Vector2(x, y);
            bool lobes = (p - new Vector2(12, 12)).magnitude <= 11 || (p - new Vector2(32, 12)).magnitude <= 11;
            bool point = p.y >= 12 && Mathf.Abs(p.x - 22) <= 21 * (1 - (p.y - 12) / 27f);
            return lobes || point ? Color.white : Color.clear;
        });
        static Sprite Disc(int size) => Drawn("disc " + size, size, size, (x, y) =>
            (new Vector2(x, y) - Vector2.one * size / 2f).magnitude <= size / 2f ? Color.white : Color.clear);

        // The book: paper.png at 800x440, the spine strip between spread pages, the title.png tab over the top-left
        // edge and the slot.png close square in the top-right corner. Page content starts at (32, 34).
        GameObject Book(string title, bool spread, out Text heading, out Button close) => Book(title, spread, BookX, BookW, out heading, out close);
        // The narrower maintenance book (696 wide, centred) keeps the same tab, close square and spine.
        GameObject Book(string title, bool spread, float x, float width, out Text heading, out Button close) => Book(title, spread, x, BookY, width, BookH, out heading, out close);
        // The map (840 wide) and result (580 wide) books, centred the same way.
        GameObject Book(string title, bool spread, float x, float y, float width, float height, out Text heading, out Button close)
        {
            var book = Face("paper", MenuStage, x, y, width, height, raycast: true).gameObject; book.name = title;
            if (spread) Face("spine", book.transform, width / 2 - 12, 0, 24, height);
            var tab = Face("title", book.transform, 48, -20, 210, 48, 14, 10, true); tab.name = "Title tab";
            heading = Words(title, tab.transform, 0, 10, 210, 28, 24, true, JPaperText, TextAnchor.MiddleCenter);
            FitTitle(heading);
            var square = Face("slot", book.transform, width - 44, 4, 36, 36, 10, 3, raycast: true); square.name = "Close";
            close = square.gameObject.AddComponent<Button>(); close.transition = Selectable.Transition.None; close.onClick.AddListener(Back);
            Glyph(CrossGlyph, square.transform, 10, 10, 16, 16, UseApprovedTheme(stage)?ApprovedThemeText:JDark);
            if(UseApprovedTheme(stage)){heading.alignment=TextAnchor.MiddleLeft;Place(tab,0,-32,width-60,40);Place(heading,0,0,width-60,40);if(title!="결과")Fill(book.transform,0,height-26,width,1,new Color32(85,90,96,100));}
            return book;
        }
        // The tab is at least 210 px and keeps 24 px of padding either side of its title.
        static void FitTitle(Text heading)
        {
            float width = Mathf.Max(210, Width(heading) + 48);
            heading.rectTransform.sizeDelta = new Vector2(width, 28);
            ((RectTransform)heading.transform.parent).sizeDelta = new Vector2(width, 48);
        }
        Text Heading(Transform page, string text, float x, float y) => Words(text, page, x, y, ColumnW, 24, 20, true);
        // card.png drawn as the HTML .card (10 px source edge at 4 px).
        Image Card(Transform parent, float x, float y, float w, float h) => Face("card", parent, x, y, w, h, 10, 4);

        // HTML .tabs .btn: tab.png / tab-active.png with the button's 14 -> 9 px sides; the active tab reads in paper.
        Button Tab(string text, Transform parent, float x, float y, float w, float h, int size, Action action)
        {
            var image = Face("tab", parent, x, y, w, h, 14, 9, true, true); image.name = text;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
            Words(text, image.transform, 0, 0, w, h, size, true, JInk, TextAnchor.MiddleCenter);
            if(UseApprovedTheme(parent))button.gameObject.AddComponent<ApprovedActionVisual>();
            System.Action activate=() => { action(); if (host) Refresh(); else if (village) RefreshHub(); };
            button.gameObject.AddComponent<ApprovedTabFocus>().Selected=activate;
            button.onClick.AddListener(()=>activate());
            return button;
        }
        static void SetTab(Button tab, bool active)
        {
            var approved=tab.GetComponent<ApprovedActionVisual>();if(approved){approved.PersistentSelection=active;return;}
            tab.image.sprite = Art(active ? "tab-active" : "tab", 14, true);
            var label = tab.GetComponentInChildren<Text>(); if (label) label.color = active ? JPaperText : JInk;
        }
        // HTML .slot (slot.png, 4 px edge at 3 px) and .slot.active (slot-active.png, 10 px edge at 6 px).
        Image SlotFace(Transform parent, float x, float y, float w, float h, bool raycast = false)
        { var image = Face("slot", parent, x, y, w, h, 4, 3, raycast: raycast); return image; }
        void SetSlot(Image image, bool active)
        { image.sprite = Art(active ? "slot-active" : "slot", active ? 10 : 4); image.pixelsPerUnitMultiplier = active ? 10 / 6f : 4 / 3f; SelectionRing(image.transform, active); }
        // The chosen cell carries a 3 px amber ring just outside its frame (playtest 2026-11-04: the journal's own
        // active frame alone did not read as selected).
        static readonly Color SelectedRing = new Color32(222, 128, 30, 255);
        void SelectionRing(Transform cell, bool on)
        {
            var ring = cell.Find("Selected ring");
            if (!on) { if (ring) ring.gameObject.SetActive(false); return; }
            var size = ((RectTransform)cell).sizeDelta;
            if (!ring) ring = Rounded("Selected ring", cell, -3, -3, size.x + 6, size.y + 6, 5, 3, Color.clear, SelectedRing).transform;
            else Place((RectTransform)ring, -3, -3, size.x + 6, size.y + 6);
            ring.gameObject.SetActive(true); ring.SetAsLastSibling();
        }
        Button SlotButton(string name, Transform parent, float x, float y, float w, float h, Action action)
        {
            var image = SlotFace(parent, x, y, w, h, true); image.name = name;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { action(); if (host) Refresh(); else if (village) RefreshHub(); });
            return button;
        }
        // Older parts can still use the journal's legacy ink icons.
        static readonly Dictionary<string, string> ColourDrops = new Dictionary<string, string>
        { ["red"] = "ink2_red_01", ["yellow"] = "ink2_yellow_01", ["green"] = "ink2_green_03", ["blue"] = "ink2_blue_02", ["purple"] = "ink2_purple_01" };
        static Sprite DropArt(Deviant.Run.GrowthPart part, string colour = null)
        {
            if (part == null)
                return colour != null ? ItemIcon("backing-ink-" + colour) ?? Art("parts/ink-slot-empty-" + colour) : null;

            var approved = ItemIcon(part.id);
            if (approved) return approved;
            var unique = Art("parts/" + part.id);
            if (unique) return unique;

            string icon = !string.IsNullOrEmpty(part.icon) && part.icon.StartsWith("ink-") ? part.icon.Substring(4) : null;
            var sprite = icon != null ? Art("parts/" + icon) : null;
            return sprite ? sprite : ColourDrops.TryGetValue(part.color ?? "", out var drop) ? Art("parts/" + drop) : null;
        }
        Image Drop(Transform parent, Deviant.Run.GrowthPart part, string colour, float x, float y, float w, float h)
        {
            var image = Rect("Drop", parent, x, y, w, h).gameObject.AddComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
            image.sprite = DropArt(part, colour); image.enabled = image.sprite;
            // Empty colour slots use a faint unmarked drop; equipped parts remain full strength.
            image.color = part == null ? new Color(1, 1, 1, .4f) : Color.white; return image;
        }
        // coin.png and a bold "N G" (HTML .price).
        Text Price(Transform parent, float x, float y, int coin, int size, string text)
        {
            Glyph(CombatSprite("gold-coin"), parent, x, y, coin, coin, Color.white).name = "Coin";
            return Words(text, parent, x + coin + 6, y, 120, coin, size, true);
        }
        // Keep the viewport/content relationship used by keyboard scrolling, and expose overflow
        // with the same thin muted-blue rail as the approved detail panels.
        RectTransform JournalScroll(string name, Transform parent, float x, float y, float w, float h)
        {
            var viewport = Rect(name, parent, x, y, w, h);
            var image = viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("내용", viewport, 0, 0, w, h);
            var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            var rail=Rect("Description scrollbar",viewport,w-4,0,3,h);
            var track=rail.gameObject.AddComponent<UnityEngine.UI.Image>();track.color=new Color32(65,72,79,110);
            var handle=Rect("Scrollbar thumb",rail,0,0,3,h);
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.pivot=new Vector2(.5f,.5f);handle.offsetMin=handle.offsetMax=Vector2.zero;
            var thumb=handle.gameObject.AddComponent<UnityEngine.UI.Image>();thumb.color=new Color32(166,190,198,205);
            var bar=rail.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();bar.handleRect=handle;bar.targetGraphic=thumb;
            bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;bar.transition=UnityEngine.UI.Selectable.Transition.None;
            bar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            // Rebuilt menus must not flash a rail before ScrollRect measures its final content in LateUpdate.
            rail.gameObject.SetActive(false);
            return content;
        }
    }
}
