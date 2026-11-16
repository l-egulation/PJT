using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Input.Contracts;
using Stage1VillageExperiment;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // First-run notebooks (docs/design/story/tutorials/first-run-guide.md, user decision 2026-11-09) on the same notebook paper as
    // the portal and eroder notes. On the profile's first run the combat page opens once the departure palette card has
    // gone: the hero with weapon 1 and with weapon 2 take turns swinging their real basic attack clips while the basic key
    // under each is pressed, with the skill key beside it. The palette page opens once the first-run guide ink is picked
    // up: the departure palettes as empty slots, and the picked-up ink part fading into the slot of its colour. Interact,
    // Advance or Back closes it. The play GUI owns the control scope that pauses the world; this view only draws and reads
    // the input. Both previews are presentation only and never act on the real player or bag.
    public sealed class FirstRunNotebookView : MonoBehaviour
    {
        // The guide ink of the palette page: its part art (the emblem drop the bag shows) and the palette slot it fits
        // (weapon 0/1, slot from 0).
        public struct GuideInk { public Sprite part; public int weapon, slot; }

        CombatPlayer player;
        Action closed;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform page;
        RectTransform[] pages;
        Font font;
        Sprite paper;
        Text paletteText, bagKey, bagRest, combatText, hint;
        readonly Text[] basicKeys = new Text[2], skillKeys = new Text[2];
        readonly Image[] basicKeyFaces = new Image[2], basicKeyFrames = new Image[2], heroes = new Image[2];
        readonly CharacterClip[,] heroClips = new CharacterClip[2, 3]; // idle, attack1, attack2
        readonly Image[,] slots = new Image[2, 5];
        Image guidePart;
        RectTransform guidePartHolder;
        float slotLeft;
        GuideInk? guideInk;
        float shownAt;
        public bool Combat { get; private set; }

        // Combat preview: weapon 1 swings first, then weapon 2; each presses its basic key twice (the two-hit combo).
        public const float PreviewCycle = 3f, PreviewLead = .35f, PreviewSecond = 1.5f, KeyPressSeconds = .14f;
        // Palette preview: the empty slot holds, the guide part fades into it with a small pop, then holds.
        public const float InkCycle = 2.4f, InkWait = .4f, InkFade = .5f, InkPop = .2f;
        const float HeroFloor = 262, KeyTop = 276, WorldPixelsPerUnit = 32;
        const float SlotStep = 48, SlotTop = 170, SlotRowStep = 56, SlotW = 38, SlotH = 49, NameW = 120, NameGap = 16;
        static readonly float[] HeroCentre = { 370, 590 };
        static readonly Color Ink = new Color32(47, 38, 30, 255);
        static readonly Color KeyNormal = new Color32(255, 250, 240, 255), KeyLit = new Color32(242, 193, 78, 255);
        static readonly Color EmptySlot = new Color(1, 1, 1, .4f);
        const float InputGrace = .35f;

        // palettes: each weapon's display name and its slot colour ids; drop: the empty slot art for a colour id (the bag's);
        // heroSets: the character animation set of weapon 1 and weapon 2; guide: the picked-up guide ink (palette page).
        public static FirstRunNotebookView Create(Transform owner, CombatPlayer player, IList<(string name, IList<string> colours)> palettes,
            Func<string, Sprite> drop, IList<CharacterAnimationSet> heroSets, GuideInk? guide, bool combat, Action closed)
        {
            var view = new GameObject("첫 런 수첩", typeof(RectTransform)).AddComponent<FirstRunNotebookView>();
            view.transform.SetParent(owner, false);
            view.player = player;
            view.closed = closed;
            view.Combat = combat;
            view.guideInk = guide;
            for (int w = 0; w < 2 && heroSets != null && w < heroSets.Count; w++)
            {
                var set = heroSets[w];
                if (set == null) continue;
                var first = set.Find("attack1") ?? set.Find("attack_single");
                view.heroClips[w, 0] = Loaded(set.Find("idle"));
                view.heroClips[w, 1] = Loaded(first);
                view.heroClips[w, 2] = Loaded(set.Find("attack2") ?? first);
            }
            view.Build(palettes, drop);
            return view;
        }

        RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        Image Picture(string name, Transform parent, float x, float y, float w, float h, Sprite sprite)
        {
            var image = Box(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        Text Words(string text, Transform parent, float x, float y, float w, float h, int size, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var label = Box(text, parent, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.text = text; label.color = Ink;
            label.alignment = anchor; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        Text Key(Transform parent, float x, float y, out Image frame, out Image face)
        {
            frame = Picture("Key frame", parent, x, y, 40, 32, null); frame.color = Ink;
            face = Picture("Key face", frame.transform, 2, 2, 36, 26, null); face.color = KeyNormal;
            return Words("", frame.transform, 0, 0, 40, 30, 16);
        }

        // The arena's clips normally carry their sprites already; a clip never shown in play loads them here.
        static CharacterClip Loaded(CharacterClip clip)
        {
            if (clip != null && (clip.sprites == null || clip.sprites.Length == 0) && clip.frames != null && clip.frames.Length > 0) clip.Load();
            return clip;
        }

        static Sprite Load(string path)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (!texture) throw new InvalidOperationException("First-run notebook art missing: " + path);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100);
        }

        void Build(IList<(string name, IList<string> colours)> palettes, Func<string, Sprite> drop)
        {
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150; canvas.pixelPerfect = true;
            scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();
            page = Box("Safe 960x540 canvas", transform, 0, 0, 960, 540);
            page.anchorMin = page.anchorMax = page.pivot = Vector2.one * .5f; page.anchoredPosition = Vector2.zero;
            var shade = Picture("수첩 뒤 음영", page, 0, 0, 960, 540, null);
            shade.color = new Color(0, 0, 0, .65f); shade.raycastTarget = true;
            font = Resources.Load<Font>("PixelInventory/Fonts/Galmuri11");
            paper = Load("VillageInk/ui/notebook-paper");
            Picture("같은 수첩 종이", page, 220, 40, 520, 460, paper);
            Words("발견한 기록", page, 270, 72, 420, 24, 12);
            pages = new[] { Box("팔레트 쪽", page, 0, 0, 960, 540), Box("전투 쪽", page, 0, 0, 960, 540) };

            // Palette page: each weapon's departure palette as empty slots, the rows centred on the page, then three lines.
            var first = pages[0];
            Words("팔레트", first, 270, 102, 420, 36, 22);
            // Centred on what is drawn: the widest weapon name as it renders, the gap, and the slots.
            int most = 3;
            var names = new List<Text>();
            for (int w = 0; w < 2 && palettes != null && w < palettes.Count; w++)
            {
                most = Mathf.Max(most, Mathf.Min(5, palettes[w].colours?.Count ?? 0));
                names.Add(Words(palettes[w].name ?? "", first, 0, SlotTop - 1 + w * SlotRowStep, NameW, 40, 14, TextAnchor.MiddleRight));
            }
            float nameWidth = 0;
            foreach (var label in names) nameWidth = Mathf.Max(nameWidth, Mathf.Ceil(label.preferredWidth));
            float rowWidth = nameWidth + NameGap + (most - 1) * SlotStep + SlotW;
            float rowLeft = Mathf.Round(480 - rowWidth / 2);
            slotLeft = rowLeft + nameWidth + NameGap;
            foreach (var label in names) label.rectTransform.anchoredPosition = new Vector2(slotLeft - NameGap - NameW, label.rectTransform.anchoredPosition.y);
            for (int w = 0; w < 2 && palettes != null && w < palettes.Count; w++)
            {
                var colours = palettes[w].colours;
                for (int j = 0; colours != null && j < colours.Count && j < 5; j++)
                {
                    var sprite = drop?.Invoke(colours[j]);
                    var image = Picture("빈 칸 " + colours[j], first, slotLeft + j * SlotStep, SlotTop + w * SlotRowStep, SlotW, SlotH, sprite);
                    image.preserveAspect = true; image.enabled = sprite; image.color = EmptySlot;
                    slots[w, j] = image;
                }
            }
            if (guideInk.HasValue && guideInk.Value.part)
            {
                // The part is drawn exactly like the empty slot under it (same box, top-left pivot: preserveAspect aligns
                // the art to the pivot, so a centred box would sit lower). The pop scales a centred holder instead.
                var g = guideInk.Value;
                float boxX = slotLeft + g.slot * SlotStep, boxY = SlotTop + g.weapon * SlotRowStep;
                guidePartHolder = Box("끼운 잉크 파츠 자리", first, boxX + SlotW / 2, boxY + SlotH / 2, SlotW, SlotH);
                guidePartHolder.pivot = Vector2.one * .5f;
                guidePart = Picture("끼운 잉크 파츠", guidePartHolder, 0, 0, SlotW, SlotH, g.part);
                guidePart.preserveAspect = true; guidePart.color = new Color(1, 1, 1, 0);
            }
            // Two regular lines, then the bag line built from a bold and a regular piece one line below (20px each).
            paletteText = Words("", first, 270, 296, 420, 60, 14);
            paletteText.lineSpacing = 1.1f;
            bagKey = Words("", first, 0, 356, 1, 20, 14, TextAnchor.MiddleLeft);
            var bold = Resources.Load<Font>("PixelInventory/Fonts/Galmuri11-Bold");
            if (bold) bagKey.font = bold;
            bagRest = Words("", first, 0, 356, 1, 20, 14, TextAnchor.MiddleLeft);

            // Combat page: weapon 1 and weapon 2 swinging over their basic and skill keys, then three lines.
            var second = pages[1];
            Words("전투", second, 270, 102, 420, 36, 22);
            for (int w = 0; w < 2; w++)
            {
                string name = palettes != null && w < palettes.Count ? palettes[w].name : "";
                Words("무기 " + (w + 1) + "  " + name, second, HeroCentre[w] - 100, 138, 200, 20, 13);
                heroes[w] = Picture(w == 0 ? "무기 1 주인공" : "무기 2 주인공", second, 0, 0, 1, 1, null);
                var outline = heroes[w].gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(34, 26, 21, 255); outline.effectDistance = Vector2.one; outline.useGraphicAlpha = true;
                basicKeys[w] = Key(second, HeroCentre[w] - 45, KeyTop, out basicKeyFrames[w], out basicKeyFaces[w]);
                skillKeys[w] = Key(second, HeroCentre[w] + 5, KeyTop, out _, out _);
                Words("기본", second, HeroCentre[w] - 45, KeyTop + 34, 40, 16, 11);
                Words("스킬", second, HeroCentre[w] + 5, KeyTop + 34, 40, 16, 11);
            }
            combatText = Words("", second, 270, 344, 420, 66, 14);
            combatText.lineSpacing = 1.1f;

            hint = Words("", page, 280, 447, 400, Deviant.UI.Theme.ApprovedTheme.ControlHintHeight, Deviant.UI.Theme.ApprovedTheme.ControlHintFontSize);
            var next = Picture("닫기", page, 650, 444, 46, 30, null);
            next.color = new Color(0, 0, 0, 0); next.raycastTarget = true;
            next.gameObject.AddComponent<Button>().onClick.AddListener(Confirm);
            shownAt = Time.unscaledTime;
            for (int i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(i == (Combat ? 1 : 0));
            Tick();
        }

        public void FitToWorld()
        {
            var target = canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera ? canvas.worldCamera.targetTexture : null;
            var frame = VillagePixelViewport.CalculateDisplayRect(target ? target.width : Screen.width, target ? target.height : Screen.height);
            scaler.scaleFactor = frame.width / 960f; canvas.scaleFactor = scaler.scaleFactor;
        }

        public void Tick()
        {
            FitToWorld();
            var input = PlayerControls.For(player);
            if (!input) return;
            input.Poll();
            basicKeys[0].text = input.Label(GameAction.Weapon1Basic);
            basicKeys[1].text = input.Label(GameAction.Weapon2Basic);
            skillKeys[0].text = input.Label(GameAction.Weapon1Special);
            skillKeys[1].text = input.Label(GameAction.Weapon2Special);
            if (Combat) PreviewAt(Time.unscaledTime - shownAt); else PalettePreviewAt(Time.unscaledTime - shownAt);
            // Short lines (about 20 characters) so each reads at a glance on the notebook page.
            paletteText.text = "팔레트 색은 출발할 때 무작위로 정해집니다.\n같은 색 칸에 잉크 파츠를 끼우면\n해당 잉크 파츠 능력이 발동됩니다.";
            // Third line: 「[Tab] 가방」 in the bold face, the rest regular, centred together (the static fonts take no <b>).
            bagKey.text = input.FormatPrompt("[{Inventory}] 가방"); bagRest.text = "에서 언제든 바꿀 수 있습니다.";
            float keyWidth = Mathf.Ceil(bagKey.preferredWidth), restWidth = Mathf.Ceil(bagRest.preferredWidth);
            float lineLeft = Mathf.Round(480 - (keyWidth + restWidth) / 2);
            bagKey.rectTransform.anchoredPosition = new Vector2(lineLeft, bagKey.rectTransform.anchoredPosition.y);
            bagKey.rectTransform.sizeDelta = new Vector2(keyWidth, bagKey.rectTransform.sizeDelta.y);
            bagRest.rectTransform.anchoredPosition = new Vector2(lineLeft + keyWidth, bagRest.rectTransform.anchoredPosition.y);
            bagRest.rectTransform.sizeDelta = new Vector2(restWidth, bagRest.rectTransform.sizeDelta.y);
            combatText.text = input.FormatPrompt("[{Weapon1Basic}], [{Weapon2Basic}] 키로 무기를 바꿔 기본공격합니다.\n[{Weapon1Special}], [{Weapon2Special}] 키로 각 무기의 스킬을 씁니다.\n[{SwapWeaponOrder}] 키로 무기 순서를 변경할 수 있습니다.");
            hint.text = input.FormatPrompt("[{Interact}] 닫기");
            if (Time.unscaledTime - shownAt < InputGrace) { input.SuppressGameplay(); return; }
            bool close = input.Take(GameAction.Interact) | input.Take(GameAction.Advance) | input.TakeBack();
            input.SuppressGameplay();
            if (close) Confirm();
        }

        public void Confirm()
        {
            if (Time.unscaledTime - shownAt < InputGrace || closed == null) return;
            PlayerControls.For(player)?.SuppressGameplay();
            var action = closed; closed = null; action();
        }

        public void PreviewAt(float seconds)
        {
            float time = Mathf.Repeat(Mathf.Max(0, seconds), PreviewCycle);
            for (int w = 0; w < 2; w++)
            {
                float start = w == 0 ? PreviewLead : PreviewSecond;
                var idle = heroClips[w, 0]; var first = heroClips[w, 1]; var second = heroClips[w, 2];
                float firstEnd = start + (first != null ? first.Duration : 0), secondEnd = firstEnd + (second != null ? second.Duration : 0);
                Sprite sprite;
                if (first != null && time >= start && time < firstEnd) sprite = first.At(time - start, false);
                else if (second != null && time >= firstEnd && time < secondEnd) sprite = second.At(time - firstEnd, false);
                else sprite = idle != null ? idle.At(time, true) : null;
                Draw(heroes[w], sprite, HeroCentre[w]);
                bool pressed = first != null && (time >= start && time < start + KeyPressSeconds || time >= firstEnd && time < firstEnd + KeyPressSeconds);
                basicKeyFaces[w].color = pressed ? KeyLit : KeyNormal;
                basicKeyFrames[w].rectTransform.anchoredPosition = new Vector2(HeroCentre[w] - 45, -(KeyTop + (pressed ? 2 : 0)));
            }
        }

        public void PalettePreviewAt(float seconds)
        {
            if (!guidePart) return;
            var g = guideInk.Value;
            var empty = g.weapon >= 0 && g.weapon < 2 && g.slot >= 0 && g.slot < 5 ? slots[g.weapon, g.slot] : null;
            float time = Mathf.Repeat(Mathf.Max(0, seconds), InkCycle);
            // The part fades in over the empty slot, which gives way as it does, then a small pop once it is in.
            float fade = Mathf.Clamp01((time - InkWait) / InkFade);
            fade = fade * fade * (3 - 2 * fade);
            guidePart.color = new Color(1, 1, 1, fade);
            if (empty) empty.color = new Color(1, 1, 1, EmptySlot.a * (1 - fade));
            float full = InkWait + InkFade;
            float pop = time >= full && time < full + InkPop ? 1 + .15f * Mathf.Sin((time - full) / InkPop * Mathf.PI) : 1;
            guidePartHolder.localScale = new Vector3(pop, pop, 1);
        }

        // Character frames come at different resolutions (32 or 128 pixels per unit); size them by world units as play does.
        static void Draw(Image image, Sprite sprite, float x)
        {
            image.enabled = sprite;
            if (!sprite) return;
            image.sprite = sprite;
            var rect = image.rectTransform;
            rect.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            rect.anchoredPosition = new Vector2(Mathf.Round(x), -HeroFloor);
            rect.sizeDelta = sprite.rect.size * (WorldPixelsPerUnit / sprite.pixelsPerUnit);
        }

        void OnDestroy() { if (paper) Destroy(paper); }
    }
}
