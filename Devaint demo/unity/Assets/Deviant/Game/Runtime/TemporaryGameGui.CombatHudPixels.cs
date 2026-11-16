using System.Collections.Generic;
using Deviant.Presentation.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // 전투 HUD 의 도트판(사용자 결정 2026-11-13: 왼쪽 위 캐릭터 초상만 빼고 체력판과 같은 도트로 통일).
    // 승인 HUD 의 배치·동작은 그대로 두고, 각 부품의 그림만 960×540 게임 화소 도트판으로 바꾼 뒤
    // 같은 자리(중심)에서 화소 경계에 맞춘다. 원은 재기 규칙 원, 작은 그림은 손으로 찍은 도트다.
    // 무기·스킬 그림은 손으로 찍은 도트판이 생길 때까지 승인 그림을 그대로 쓴다(자동 축소는 재기 규칙을 못 지킨다).
    // 글자는 도트 글꼴로 다시 쓰되, 글꼴에 없는 글자(한글 등)는 원래 글자를 둔다.
    public sealed partial class TemporaryGameGui
    {
        readonly Image[] combatSpecialDiscs = new Image[2];
        readonly Image[,] combatKeyCaps = new Image[2, 2];
        Image hudCoin, hudPigment, hudBagGlyph, hudPauseFrame, hudPauseGlyph;
        bool pixelHudBuilt;
        readonly Vector3[] pixelCorners = new Vector3[4];
        readonly HashSet<Sprite> pixelSprites = new HashSet<Sprite>();
        // 팔레트 칸(스케치 무기): 원래 방울 그림 대신 도트 얼룩. 잉크를 넣은 칸은 잉크 색 얼룩, 빈 칸은 칸 색 테두리.
        readonly Image[,] pixelDrops = new Image[2, 6];
        readonly string[,] pixelDropColour = new string[2, 6];
        readonly Dictionary<string, Sprite> pixelDropSprites = new Dictionary<string, Sprite>();
        readonly Sprite[] pixelRingSelected = new Sprite[2], pixelRingIdle = new Sprite[2];
        static readonly Color32 RingSelected = new Color32(0, 212, 208, 255), RingIdle = new Color32(162, 158, 146, 255);
        static readonly Color32 RingCooldown = new Color32(255, 240, 218, 255), RingCharge = new Color32(247, 195, 76, 255);
        // 승인 링 그림(256): 바깥 끝 12, 띠 두께 15.
        const float RingOuter = .5f - 12f / 256, RingInner = .5f - 27f / 256;
        const float SkillKeyShift = 3;
        // 무기·스킬 그림(resources ui.combat-hud-pixel-icons-20261113)은 이 캔버스 화소 크기로 그렸다(그림 1칸 = 1화소).
        // 게임용 PNG 는 2배(96·88)라 1080p(캔버스 2배)에서 텍스처 1텍셀 = 화면 1화소로 맞는다.
        public const int WeaponIconPixels = 48, SkillIconPixels = 44;

        sealed class PixelLabel
        {
            public Text Source; public RawImage Image; public Texture2D Texture; public string Shown;
            public Color32 Colour; public Color32? Outline; public int Scale; public bool SingleLarge;
            public RectTransform CentreOn, MiddleOf;
        }
        readonly List<PixelLabel> pixelLabels = new List<PixelLabel>();
        readonly HashSet<Text> pixelLabelled = new HashSet<Text>();

        RectTransform PixelCanvas => (RectTransform)hud.transform;

        UnityEngine.Rect CanvasBox(RectTransform rect)
        {
            var canvas = PixelCanvas; rect.GetWorldCorners(pixelCorners);
            Vector2 a = canvas.InverseTransformPoint(pixelCorners[0]), b = canvas.InverseTransformPoint(pixelCorners[2]);
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        // w×h 캔버스 화소 크기로, centre 를 중심으로 하되 왼쪽 아래 모서리를 정수 화소에 맞춘다.
        void SnapPixels(RectTransform rect, Vector2 centre, int w, int h)
        {
            var canvas = PixelCanvas; var parent = (RectTransform)rect.parent;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one; rect.localEulerAngles = Vector3.zero;
            rect.sizeDelta = new Vector2(w * canvas.lossyScale.x / parent.lossyScale.x, h * canvas.lossyScale.y / parent.lossyScale.y);
            float left = Mathf.Round(centre.x - w / 2f), bottom = Mathf.Round(centre.y - h / 2f);
            rect.position = canvas.TransformPoint(new Vector2(left + w / 2f, bottom + h / 2f));
        }
        Texture2D PixelTexture(string name, Color32[] top, int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(HudPixels.Flipped(top, w, h)); texture.Apply(false);
            return texture;
        }
        Sprite PixelSprite(string name, Color32[] top, int w, int h)
        {
            var sprite = UnityEngine.Sprite.Create(PixelTexture(name, top, w, h), new UnityEngine.Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            // Same name as the approved art it replaces, so HUD checks that read the art name keep working.
            sprite.name = name; pixelSprites.Add(sprite); return sprite;
        }
        const int HudButtonPixels=34;
        Sprite HudButtonSprite(HudPixelKit.ButtonGlyph glyph,int size=HudButtonPixels)
            => PixelSprite("button-frame",HudPixelKit.Button(size,glyph),size,size);
        void UsePixelSprite(Image image, Sprite sprite, Vector2 centre, int w, int h)
        {
            image.sprite = sprite; image.preserveAspect = false; image.color = Color.white;
            SnapPixels(image.rectTransform, centre, w, h);
        }

        // kind: "ink:색"(잉크를 넣은 칸, 잉크 색 얼룩) 또는 "empty:색|neutral"(빈 칸, 얼룩 모양 테두리).
        Sprite BlobSprite(string kind)
        {
            if (!pixelDropSprites.TryGetValue(kind, out var sprite))
            {
                bool ink = kind.StartsWith("ink:"); string colour = kind.Substring(kind.IndexOf(':') + 1);
                var pixels = ink ? HudPixelKit.InkBlob(colour) : HudPixelKit.EmptyBlob(colour);
                pixelDropSprites[kind] = sprite = PixelSprite((ink ? "ink-" : "slot-empty-") + colour, pixels, HudPixelKit.BlobWidth, HudPixelKit.BlobHeight);
            }
            return sprite;
        }

        void BuildPixelHud()
        {
            pixelHudBuilt = true;
            for (int w = 0; w < 2; w++)
            {
                // Weapon disc: 승인 원판(999×1012, 불투명 4~994)이 실제로 그려진 반지름 그대로.
                var box = CanvasBox(weaponCards[w].rectTransform);
                int radius = Mathf.RoundToInt(Mathf.Min(box.width * 1012f / 999, box.height) * DiscArtRadius);
                UsePixelSprite(weaponCards[w], PixelSprite("weapon-disc", HudPixelKit.Disc(radius), 2 * radius, 2 * radius), box.center, 2 * radius, 2 * radius);
                SnapPixels(weaponIcons[w].rectTransform, box.center, WeaponIconPixels, WeaponIconPixels);
                var ring = CanvasBox(combatSelected[w].rectTransform);
                float ringOuter = ring.width * RingOuter; int ringSize = 2 * Mathf.CeilToInt(ringOuter), ringBand = Mathf.RoundToInt(ring.width * (RingOuter - RingInner));
                pixelRingSelected[w] = PixelSprite("ring-selected", HudPixelKit.Ring(ringSize, ringOuter, ringBand, RingSelected), ringSize, ringSize);
                pixelRingIdle[w] = PixelSprite("ring-idle", HudPixelKit.Ring(ringSize, ringOuter, ringBand, RingIdle), ringSize, ringSize);
                UsePixelSprite(combatSelected[w], pixelRingIdle[w], ring.center, ringSize, ringSize);

                var skillBox = CanvasBox(combatSpecialDiscs[w].rectTransform);
                int skillRadius = Mathf.RoundToInt(Mathf.Min(skillBox.width * 1012f / 999, skillBox.height) * DiscArtRadius);
                UsePixelSprite(combatSpecialDiscs[w], PixelSprite("weapon-disc", HudPixelKit.Disc(skillRadius), 2 * skillRadius, 2 * skillRadius), skillBox.center, 2 * skillRadius, 2 * skillRadius);
                SnapPixels(combatSpecial[w].rectTransform, skillBox.center, SkillIconPixels, SkillIconPixels);
                float skillOuter = skillBox.width * RingOuter; int skillRing = 2 * Mathf.CeilToInt(skillOuter), skillBand = Mathf.RoundToInt(skillBox.width * (RingOuter - RingInner));
                foreach (var (image, colour, art) in new[] { (weaponShades[w], RingCooldown, "ring-cooldown"), (combatProgress[w], RingCharge, "ring-charge") })
                {
                    var type = image.type; var method = image.fillMethod; int origin = image.fillOrigin; bool clockwise = image.fillClockwise;
                    UsePixelSprite(image, PixelSprite(art, HudPixelKit.Ring(skillRing, skillOuter, skillBand, colour), skillRing, skillRing), skillBox.center, skillRing, skillRing);
                    image.type = type; image.fillMethod = method; image.fillOrigin = origin; image.fillClockwise = clockwise;
                }

                for (int k = 0; k < 2; k++)
                {
                    var cap = CanvasBox(combatKeyCaps[w, k].rectTransform);
                    // 스킬 키(Q·W)는 스킬 원 위 가운데 쪽으로 3화소 왼쪽(사용자 요청 2026-11-13).
                    var capCentre = cap.center + (k == 1 ? Vector2.left * SkillKeyShift : Vector2.zero);
                    UsePixelSprite(combatKeyCaps[w, k], PixelSprite("button-frame", HudPixelKit.Keycap(20, 20), 20, 20), capCentre, 20, 20);
                    AddPixelLabel(combatKeys[w, k], HudPixels.Ink, null, 2, true, centreOn: combatKeyCaps[w, k].rectTransform);
                }
                var tray = CanvasBox(combatTray[w].rectTransform);
                int trayW = 136; // 편집기 원본(palette-tray, 136×22)과 같은 폭
                UsePixelSprite(combatTray[w], PixelSprite("palette-tray-5", HudPixelKit.PaletteTray(trayW, 22), trayW, 22), tray.center, trayW, 22);
                for (int j = 0; j < 6; j++)
                {
                    pixelDrops[w, j] = Rect("Pixel drop", combatDrops[w, j].transform, 0, 0, 1, 1).gameObject.AddComponent<Image>();
                    pixelDrops[w, j].raycastTarget = false; pixelDrops[w, j].enabled = false;
                }

                AddPixelLabel(weaponSeconds[w], CombatIvory, HudPixels.Ink, 2, false);
                AddPixelLabel(combatChargePercent[w], new Color32(247, 195, 76, 255), HudPixels.Ink, 1, false);
                AddPixelLabel(combatCharges[w], CombatIvory, HudPixels.Ink, 1, false);
                AddPixelLabel(durabilityText[w], HudPixels.Ink, HudPixels.Cream, 1, false);
            }

            var coin = HudPixelKit.Coin(out int coinSize);
            UsePixelSprite(hudCoin, PixelSprite("gold-coin", coin, coinSize, coinSize), CanvasBox(hudCoin.rectTransform).center, coinSize, coinSize);
            var pigment = HudPixelKit.Pigment(out int pigmentW, out int pigmentH);
            UsePixelSprite(hudPigment, PixelSprite("pigment-currency", pigment, pigmentW, pigmentH), CanvasBox(hudPigment.rectTransform).center, pigmentW, pigmentH);
            AddPixelLabel(goldText, HudPixels.Ink, HudPixels.Cream, 2, false, middleOf: hudCoin.rectTransform);
            AddPixelLabel(pigmentText, HudPixels.Ink, HudPixels.Cream, 2, false, middleOf: hudPigment.rectTransform);

            foreach (var (frame, glyphImage, glyph) in new[] { (bagHud.GetComponent<Image>(), hudBagGlyph, HudPixelKit.ButtonGlyph.Bag), (hudPauseFrame, hudPauseGlyph, HudPixelKit.ButtonGlyph.Pause) })
            {
                var box = CanvasBox(frame.rectTransform); int size = Mathf.RoundToInt(Mathf.Min(box.width, box.height)) - 1;
                // The frame owns the button; its children (bag count) keep their own rects.
                UsePixelSprite(frame, HudButtonSprite(glyph,size), box.center, size, size);
                glyphImage.enabled = false;
            }

            // Minimap: its frame keeps the 82% scale, the live map and the 「지도 [M]」 label as children, so the pixel plate
            // is a separate image just behind it and the approved frame art turns transparent (still the click target).
            var minimap = rightTop.transform.Find("Minimap")?.GetComponent<Image>();
            if (minimap)
            {
                var box = CanvasBox(minimap.rectTransform);
                int mw = Mathf.RoundToInt(box.width), mh = Mathf.RoundToInt(box.height);
                var plate = Rect("Minimap plate", minimap.transform.parent, 0, 0, 1, 1).gameObject.AddComponent<Image>();
                plate.raycastTarget = false; plate.transform.SetSiblingIndex(minimap.transform.GetSiblingIndex());
                UsePixelSprite(plate, PixelSprite("Minimap plate", HudPixelKit.Minimap(mw, mh), mw, mh), box.center, mw, mh);
                minimap.color = Color.clear;
            }
        }

        void AddPixelLabel(Text source, Color32 colour, Color32? outline, int scale, bool singleLarge, RectTransform centreOn = null, RectTransform middleOf = null)
        {
            if (!source || !pixelLabelled.Add(source)) return;
            var image = Rect("Pixel text", source.transform, 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            pixelLabels.Add(new PixelLabel { Source = source, Image = image, Colour = colour, Outline = outline, Scale = scale, SingleLarge = singleLarge, CentreOn = centreOn, MiddleOf = middleOf });
        }

        void RefreshPixelLabels()
        {
            foreach (var label in pixelLabels)
            {
                if (!label.Source) continue;
                string text = label.Source.text ?? "";
                bool pixel = HudPixelKit.CanWrite(text);
                label.Source.enabled = !pixel;
                label.Image.enabled = pixel && text.Length > 0;
                if (!pixel || text.Length == 0 || text == label.Shown) continue;
                label.Shown = text;
                // Key caps: one key letter at 2x, longer names at 1x so they still fit the cap.
                int scale = label.SingleLarge && text.Length > 1 ? 1 : label.Scale;
                var top = HudPixelKit.Text(text, scale, label.Colour, label.Outline, out int w, out int h);
                // Same-size text (charge percent, cooldowns, durability) rewrites the texture it has instead of making a new
                // one each change, which happened every frame of a charge (QA 2026-11-16).
                if (label.Texture && label.Texture.width == w && label.Texture.height == h)
                { label.Texture.SetPixels32(HudPixels.Flipped(top, w, h)); label.Texture.Apply(false); }
                else
                {
                    if (label.Texture) Destroy(label.Texture);
                    label.Image.texture = label.Texture = PixelTexture("Pixel text", top, w, h);
                }
                var box = CanvasBox(label.Source.rectTransform);
                float fontPixels = label.Source.fontSize * box.height / Mathf.Max(1, label.Source.rectTransform.rect.height);
                var align = label.Source.alignment;
                float x = align == TextAnchor.UpperLeft || align == TextAnchor.MiddleLeft || align == TextAnchor.LowerLeft ? box.xMin + w / 2f
                    : align == TextAnchor.UpperRight || align == TextAnchor.MiddleRight || align == TextAnchor.LowerRight ? box.xMax - w / 2f : box.center.x;
                // CombatWords lifts upper-anchored text by 4 for Malgun's ascender; the glyph centre sits about 0.62 em down.
                float y = align == TextAnchor.UpperLeft || align == TextAnchor.UpperCenter || align == TextAnchor.UpperRight ? box.yMax - fontPixels * .62f : box.center.y;
                if (label.CentreOn) { var on = CanvasBox(label.CentreOn); x = on.center.x; y = on.center.y; }
                if (label.MiddleOf) y = CanvasBox(label.MiddleOf).center.y;
                SnapPixels(label.Image.rectTransform, new Vector2(x, y), w, h);
            }
        }

        // 칸은 팔레트 판 가운데를 기준으로 BlobStep(22) 간격으로 놓는다(6칸이 판 속에 들어간다).
        void RefreshPixelDrops()
        {
            for (int w = 0; w < 2; w++)
            {
                if (!pixelDrops[w, 0]) continue;
                int n = 0; for (int j = 0; j < 6; j++) if (combatDrops[w, j].gameObject.activeSelf) n++;
                var tray = CanvasBox(combatTray[w].rectTransform).center;
                for (int j = 0; j < 6; j++)
                {
                    var drop = combatDrops[w, j]; var pixel = pixelDrops[w, j];
                    string kind = pixelDropColour[w, j];
                    bool show = kind != null && drop.gameObject.activeInHierarchy;
                    drop.enabled = !show; pixel.enabled = show;
                    if (!show) continue;
                    pixel.sprite = BlobSprite(kind);
                    SnapPixels(pixel.rectTransform, tray + new Vector2((j - (n - 1) / 2f) * HudPixelKit.BlobStep, 0), HudPixelKit.BlobWidth, HudPixelKit.BlobHeight);
                }
            }
        }

        void RefreshPixelHud()
        {
            if (!pixelHudBuilt) BuildPixelHud();
            RefreshPixelDrops();
            for (int w = 0; w < 2; w++) if (Sketch != null) AddPixelLabel(sketchDurabilityTexts[w], HudPixels.Ink, HudPixels.Cream, 1, false);
            RefreshPixelLabels();
        }
    }
}
