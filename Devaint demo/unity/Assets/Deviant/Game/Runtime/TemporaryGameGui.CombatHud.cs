using System;
using System.Collections.Generic;
using Deviant.Input.Contracts;
using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Presentation only: authored 1670x942 geometry, projected into the existing safe world viewport.
    public sealed partial class TemporaryGameGui
    {
        [Serializable] sealed class HudEdit
        {
            public string id; public float dx, dy, sx = 1, sy = 1, rotation, opacity = 1;
            public bool hidden;
        }
        [Serializable] sealed class HudWeapon { public string id, name, icon, basicIcon; public string[] specials; }
        [Serializable] sealed class HudSkill { public string id, runtimeId, weaponId, icon, presentationIcon, presentationName; }
        [Serializable] sealed class HudBindings { public HudEdit[] layers; public HudWeapon[] weapons; public HudSkill[] skills; }
        HudBindings combatBindings;
        readonly Dictionary<string, HudEdit> combatEdits = new Dictionary<string, HudEdit>();
        readonly Dictionary<string, Sprite> combatSprites = new Dictionary<string, Sprite>();
        RectTransform combatArtwork;
        RectTransform combatHealthRoot;
        Image healthHitFlash;
        UnityEngine.Object healthHitPlayer;
        Vector2 healthRestPosition;
        int healthHitCount=-1;
        float healthHitAt=-1;
        const float HealthHitShakeSeconds=.24f, HealthHitFlashSeconds=.12f, HealthHitShakePixels=2.5f, HealthSlashGap=3f;
        // The player's health plaque is drawn in 960x540 game pixels (HealthPlaque): plate,
        // pixel numbers and the slanted gauge with its heal brush, shield frame and erased damage.
        Deviant.Presentation.Unity.HealthPlaque healthPlaque;
        RawImage healthPlaqueImage;
        Texture2D healthPlaqueTexture;
        UnityEngine.Object healthInkPlayer;
        int healthInkHits = -1;
        // Top-left of the plaque texture: game pixel (49, 32) in the 1670x942 HUD layout, so the plate's left end tucks
        // behind the approved character portrait, which keeps its size and sits 6 game pixels lower, in front.
        const float HealthPlaqueX = 49 * 1670f / 960, HealthPlaqueY = 32 * 942f / 540, HealthPortraitDrop = 6 * 942f / 540;
        readonly RectTransform[] combatWeaponRoots = new RectTransform[2];
        // Keep the authored per-asset coordinates intact; move each complete cluster together.
        const float CombatWeaponLeftShift = 20, CombatWeaponGapReduction = 50;
        Text pigmentText;
        // Sketch-weapon durability: a pixel arc around each weapon disc's outer rim, or the remaining value (player setting).
        readonly Text[] durabilityText = new Text[2];
        readonly RawImage[] durabilityArcImages = new RawImage[2];
        readonly Deviant.Presentation.Unity.DurabilityArc[] durabilityArcs = new Deviant.Presentation.Unity.DurabilityArc[2];
        readonly Texture2D[] durabilityArcTextures = new Texture2D[2];
        readonly Vector3[] durabilityCorners = new Vector3[4];
        // The approved disc art (999×1012, opaque 4..994) is drawn with preserveAspect inside its square.
        const float DiscArtRadius = 990f / 1012 / 2;
        readonly Image[] combatSpecial = new Image[2], combatSelected = new Image[2], combatTray = new Image[2], combatProgress = new Image[2];
        readonly Image[,] combatDrops = new Image[2, 6];
        readonly Text[,] combatKeys = new Text[2, 2];
        readonly Text[] combatCharges = new Text[2], combatChargePercent = new Text[2];
        readonly Text[] combatUnarmedNames = new Text[2];
        static readonly Color CombatIvory = new Color32(255, 240, 218, 255);

        Sprite CombatSprite(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "state-unavailable";
            if (combatSprites.TryGetValue(id, out var value)) return value;
            value = Resources.Load<Sprite>("ApprovedCombatHUD/sprites/" + id);
            if (!value) throw new InvalidOperationException("Approved combat HUD sprite missing: " + id);
            combatSprites[id] = value; return value;
        }
        static string HudId(string group, string type, float x, float y) => group + "." + type + ":" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Rect HudRect(string id, float x, float y, float w, float h)
        {
            if (combatEdits.TryGetValue(id, out var e)) return new Rect(x + e.dx, y + e.dy, w * e.sx, h * e.sy);
            return new Rect(x, y, w, h);
        }
        void CombatPlace(RectTransform t, string id, float x, float y, float w, float h)
        {
            var r = HudRect(id, x, y, w, h); Place(t, r.x, r.y, r.width, r.height);
            if (combatEdits.TryGetValue(id, out var e)) { t.localEulerAngles = new Vector3(0, 0, -e.rotation); t.gameObject.SetActive(!e.hidden); }
        }
        Image CombatArt(string sprite, Transform parent, string group, float x, float y, float w, float h, string key = null, bool contain = true)
        {
            string id = group + "." + key; if (key == null) id = HudId(group, "sprite", x, y);
            var image = Rect(id, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = CombatSprite(sprite); image.preserveAspect = contain; image.raycastTarget = false;
            CombatPlace(image.rectTransform, id, x, y, w, h);
            if (combatEdits.TryGetValue(id, out var e)) image.color = new Color(1, 1, 1, e.opacity);
            return image;
        }
        Text CombatWords(string value, Transform parent, string group, float x, float y, float width, float height, int size, bool center = false, Color? color = null)
        {
            string id = HudId(group, "text", x, y);
            float left = center ? x - width / 2 : x;
            var e = combatEdits.TryGetValue(id, out var edit) ? edit : null;
            // Malgun's uGUI line ascender sits below the canvas editor's top baseline.
            var t = Words(value, parent, left + (e?.dx ?? 0), y - 4 + (e?.dy ?? 0), width, height, size, true, color ?? CombatIvory, center ? TextAnchor.UpperCenter : TextAnchor.UpperLeft);
            t.name = id; t.rectTransform.localScale = new Vector3(e?.sx ?? 1, e?.sy ?? 1, 1);
            // Scaling around a centred text anchor must not move its centre.
            if (center) t.rectTransform.anchoredPosition += new Vector2(width * (1 - (e?.sx ?? 1)) / 2, 0);
            return t;
        }
        Button CombatButton(string name, Transform parent, string group, float x, float y, Action action)
        {
            var frame = CombatArt("button-frame", parent, group, x, y, 61, 61); frame.raycastTarget = true; frame.name = name;
            var button = frame.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => { action(); Refresh(); }); return button;
        }
        Button combatSwapButton;
        Text combatSwapKey, combatSwapRemaining;
        GameObject bankedRewardPrompt;
        Text bankedRewardCaption, bankedRewardKey;
        Image bankedRewardFrame;
        SketchRunState inventoryPromptRun;
        bool inventoryPromptSeen,inventoryPromptWaiting;
        Outline bankedRewardCaptionGlow,bankedRewardKeyGlow,bankedRewardFrameGlow;
        void BuildApprovedHud()
        {
            var asset = Resources.Load<TextAsset>("ApprovedCombatHUD/bindings");
            if (!asset) throw new InvalidOperationException("Approved combat HUD bindings are not deployed.");
            combatBindings = JsonUtility.FromJson<HudBindings>(asset.text);
            foreach (var edit in combatBindings.layers) combatEdits[edit.id] = edit;
            combatArtwork = Rect("Approved combat HUD", hud.transform, 0, 0, 1670, 942);
            combatArtwork.localScale = new Vector3(960f / 1670, 540f / 942, 1);
            combatHealthRoot=Rect("Health feedback",combatArtwork,0,0,1670,942);
            healthRestPosition=combatHealthRoot.anchoredPosition;
            var plaque = CombatArt("health-plaque", combatHealthRoot, "health", 96, 61, 428, 82, contain: false).rectTransform;
            healthFill = CombatArt("health-fill-red", combatHealthRoot, "health", 165, 112, 330, 18, contain: false);
            // A layout edit may move or shrink the fill, but a full bar must still reach the plaque track's right end (x 495 of the 96..524 plaque).
            var fillRect = healthFill.rectTransform;
            float trackRight = plaque.anchoredPosition.x + plaque.sizeDelta.x * (495f - 96f) / 428f;
            fillRect.sizeDelta = new Vector2(Mathf.Max(0, trackRight - fillRect.anchoredPosition.x), fillRect.sizeDelta.y);
            healthFill.type = Image.Type.Filled; healthFill.fillMethod = Image.FillMethod.Horizontal; healthFill.fillOrigin = 0;
            healthHitFlash=Rect("Health hit flash",healthFill.transform,0,0,healthFill.rectTransform.sizeDelta.x,healthFill.rectTransform.sizeDelta.y).gameObject.AddComponent<Image>();
            healthHitFlash.sprite=healthFill.sprite;healthHitFlash.type=Image.Type.Filled;
            healthHitFlash.fillMethod=Image.FillMethod.Horizontal;healthHitFlash.fillOrigin=0;
            // The journal shader uses the source alpha only, so a red texture really flashes white.
            healthHitFlash.material=journalText;healthHitFlash.raycastTarget=false;healthHitFlash.gameObject.SetActive(false);
            var portrait = CombatArt("hero-portrait", combatHealthRoot, "health", 29, 30, 147, 118);
            healthText = CombatWords("", combatHealthRoot, "health", 192, 59, 130, 64, 42);
            healthMax = CombatWords("", combatHealthRoot, "health", 263, 73, 160, 44, 28);
            // The current value ends just before "/ max" on the same top line, so 3- and 4-digit health never runs into the slash.
            var current = healthText.rectTransform; var maxAt = healthMax.rectTransform.anchoredPosition;
            healthText.alignment = TextAnchor.UpperRight;
            current.anchoredPosition = new Vector2(maxAt.x - HealthSlashGap - current.sizeDelta.x * current.localScale.x, maxAt.y);
            // The pixel plaque replaces the painted plaque, gauge and numbers. They stay built (their state is still
            // updated) but hidden. The approved portrait stays, in front of the pixel plaque.
            foreach (var old in new Component[] { plaque, healthFill, healthText, healthMax }) old.gameObject.SetActive(false);
            healthPlaque = new Deviant.Presentation.Unity.HealthPlaque();
            healthPlaqueImage = Rect("Health plaque", combatHealthRoot, HealthPlaqueX, HealthPlaqueY,
                Deviant.Presentation.Unity.HealthPlaque.Width * 1670f / 960, Deviant.Presentation.Unity.HealthPlaque.Height * 942f / 540).gameObject.AddComponent<RawImage>();
            healthPlaqueImage.raycastTarget = false;
            portrait.rectTransform.anchoredPosition += Vector2.down * HealthPortraitDrop;
            portrait.transform.SetAsLastSibling();
            rightTop = Rect("Right top", combatArtwork, 0, 0, 1670, 942).gameObject;
            goldChip = Rect("Currencies", rightTop.transform, 0, 0, 1670, 942).gameObject;
            hudCoin = CombatArt("gold-coin", goldChip.transform, "currencies", 1255, 28, 47, 47);
            goldText = CombatWords("", goldChip.transform, "currencies", 1308, 34, 100, 48, 34, color: JInk);
            hudPigment = CombatArt("pigment-currency", goldChip.transform, "currencies", 1390, 28, 47, 47);
            pigmentText = CombatWords("", goldChip.transform, "currencies", 1446, 34, 100, 48, 34, color: JInk);
            // Sketch reward gauge: a pixel strip right under the health plaque (like an experience bar) with the banked
            // count after it, no numbers on the bar (docs/design/run/rewards.md). Drawn on the 960×540 canvas at integer
            // game pixels; the character buff row moves down by RewardGaugeBuffDrop while it shows.
            rewardGaugeImage = Rect("Reward gauge", hud.transform, RewardGaugeX, RewardGaugeY - Deviant.Presentation.Unity.RewardGaugeStrip.TopMargin,
                Deviant.Presentation.Unity.RewardGaugeStrip.Width, Deviant.Presentation.Unity.RewardGaugeStrip.Height).gameObject.AddComponent<RawImage>();
            rewardGaugeImage.raycastTarget = false; rewardGaugeImage.gameObject.SetActive(false);
            bankedRewardPrompt=Rect("Banked reward prompt",hud.transform,415,486,130,32).gameObject;
            var promptHit=bankedRewardPrompt.AddComponent<Image>();promptHit.color=Color.clear;
            var promptButton=bankedRewardPrompt.AddComponent<Button>();promptButton.transition=Selectable.Transition.None;
            promptButton.onClick.AddListener(()=>{if(inventoryPromptWaiting){Presenter.Open();Refresh();}else if(SketchBinding)SketchBinding.TryOpenBankedReward();});
            rewardGaugeKey=ApprovedKeycap(hud.transform,"",315,90,48,13,9);rewardGaugeKey.gameObject.SetActive(false);
            var rewardKey=ApprovedKeycap(bankedRewardPrompt.transform,"G",0,3,25,25,14);
            var rewardKeyText=bankedRewardKey=rewardKey.GetComponentInChildren<Text>();rewardKeyText.color=Color.white;
            bankedRewardFrame=rewardKey;bankedRewardFrame.material=journalText;
            bankedRewardFrameGlow=rewardKey.gameObject.AddComponent<Outline>();
            bankedRewardCaption=Words("보상",bankedRewardPrompt.transform,37,0,90,32,19,true,Color.white);
            bankedRewardCaptionGlow=bankedRewardCaption.gameObject.AddComponent<Outline>();
            bankedRewardKeyGlow=rewardKeyText.gameObject.AddComponent<Outline>();
            foreach(var halo in new[]{bankedRewardCaptionGlow,bankedRewardKeyGlow,bankedRewardFrameGlow})
            {halo.effectDistance=new Vector2(2,2);halo.useGraphicAlpha=true;}
            bankedRewardPrompt.SetActive(false);
            // 보상을 받아 두는 순간 화면 가장자리 노란빛(회복 초록빛 Chapter1RoomClearHealFx 와 같은 두께·세기·시간, 사용자 2026-11-13).
            rewardEdgeGlow = Rect("Reward edge glow", hud.transform, 0, 0, 960, 540).gameObject.AddComponent<RawImage>();
            rewardEdgeGlow.raycastTarget = false; rewardEdgeGlow.texture = RewardEdgeTexture(); rewardEdgeGlow.transform.SetAsFirstSibling(); rewardEdgeGlow.enabled = false;
            bagHud = CombatButton("Bag", rightTop.transform, "inventory", 1510, 24, () => Presenter.Open());
            hudBagGlyph = CombatArt("bag-glyph", rightTop.transform, "inventory", 1528, 39, 26, 31);
            bagCount = Words("", bagHud.transform, 0, 0, 61, 61, 14); bagCount.enabled = false;
            hudPauseFrame = CombatButton("Pause", combatArtwork, "pause", 1583, 24, () => Presenter.TogglePause()).GetComponent<Image>();
            hudPauseGlyph = CombatArt("pause-glyph", combatArtwork, "pause", 1602, 41, 23, 29);
            combatSwapButton = LiveButton("Combat weapon swap", "", hud.transform, 126, 466, 28, 25,
                () => host.Arena.Player.TrySwapWeaponOrder());
            var swapCap = ApprovedKeycap(combatSwapButton.transform, "X", 0, 0, 28, 25, 13);
            combatSwapKey = swapCap.GetComponentInChildren<Text>();
            combatSwapRemaining = Words("", hud.transform, 116, 492, 48, 20, 11, true, CombatIvory);
            for (int w = 0; w < 2; w++)
            {
                string g = "weapon-" + w; float x = 52 + w * 370;
                var cluster = combatWeaponRoots[w] = Rect(g, combatArtwork, -CombatWeaponLeftShift - w * CombatWeaponGapReduction, 0, 1670, 942);
                cluster.gameObject.SetActive(false);
                weaponCards[w] = CombatArt("weapon-disc", cluster, g, x, 774, 142, 142);
                weaponIcons[w] = CombatArt("state-empty-weapon", cluster, g, x + 29, 802, 84, 84);
                combatUnarmedNames[w] = CombatWords("", cluster, g, x + 71, 830, 92, 30, 22, true, CombatIvory);
                // Behind the disc so its rim, the keycaps and the palette tray stay in front of the arc.
                durabilityArcImages[w] = Rect("Durability arc", cluster, 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
                durabilityArcImages[w].raycastTarget = false; durabilityArcImages[w].gameObject.SetActive(false);
                durabilityArcImages[w].transform.SetSiblingIndex(weaponCards[w].transform.GetSiblingIndex());
                durabilityText[w] = CombatWords("", cluster, g, x + 71, 912, 142, 22, 15, true, CombatIvory);
                combatSelected[w] = CombatArt("ring-idle", cluster, g, x + 7, 781, 128, 128);
                // The weapon disc is also the basic attack. One adjacent disc is enough for the special.
                var special = Rect(g + " special", cluster, -76, -4, 1670, 942);
                var specialDisc = combatSpecialDiscs[w] = CombatArt("weapon-disc", special, g, x + 214, 808, 82, 82);
                // Enlarge around the authored centre, then tuck the skill behind the basic disc.
                const float specialScale = 96f / 82;
                var specialCentre = specialDisc.rectTransform.anchoredPosition + new Vector2(specialDisc.rectTransform.sizeDelta.x / 2, -specialDisc.rectTransform.sizeDelta.y / 2);
                special.localScale = Vector3.one * specialScale;
                special.anchoredPosition += specialCentre * (1 - specialScale) + new Vector2(-10, 0);
                special.SetAsFirstSibling();
                combatSpecial[w] = CombatArt("state-unavailable", special, g, x + 227, 821, 56, 56);
                weaponShades[w] = CombatArt("ring-cooldown", special, g, x + 214, 808, 82, 82, "skill-progress");
                weaponShades[w].type = Image.Type.Filled; weaponShades[w].fillMethod = Image.FillMethod.Radial360;
                weaponShades[w].fillOrigin = (int)Image.Origin360.Top; weaponShades[w].fillClockwise = true;
                combatProgress[w] = CombatArt("ring-charge", special, g, x + 214, 808, 82, 82, "skill-progress");
                combatProgress[w].type = Image.Type.Filled; combatProgress[w].fillMethod = Image.FillMethod.Radial360;
                combatProgress[w].fillOrigin = (int)Image.Origin360.Top; combatProgress[w].fillClockwise = true;
                weaponSeconds[w] = CombatWords("", special, g, x + 255, 830, 76, 40, 25, true);
                // Keep the saved layout ID while lowering only the cooldown label.
                weaponSeconds[w].rectTransform.anchoredPosition += Vector2.down * 6f;
                combatChargePercent[w] = CombatWords("", special, g, x + 255, 863, 70, 22, 13, true, new Color32(247, 195, 76, 255));
                combatCharges[w] = CombatWords("", special, g, x + 282, 864, 48, 22, 13, true);
                var basicKey = Rect(g + " basic key", cluster, -108, -26, 1670, 942);
                // Keycaps stay the same size and remain in front of both discs.
                var specialKey = Rect(g + " special key", cluster, -86, -11, 1670, 942);
                for (int k = 0; k < 2; k++)
                {
                    var keyParent = k == 0 ? basicKey : specialKey;
                    combatKeyCaps[w, k] = CombatArt("button-frame", keyParent, g, x + 155 + k * 82, 786, 40, 33, contain: false);
                    combatKeys[w, k] = CombatWords("", keyParent, g, x + 175 + k * 82, 788, 60, 34, 23, true, JInk);
                }
                var palette = Rect(g + " palette", cluster, -76, 4, 1670, 942);
                combatTray[w] = CombatArt("palette-tray-5", palette, g, x + 72, 885, 235, 43, contain: false);
                for (int j = 0; j < 6; j++) combatDrops[w, j] = CombatArt("slot-empty", palette, g, x + 91 + j * 42, 889, 29, 35, "ink-" + j);
            }
        }
        HudWeapon CombatWeapon(int slot)
        {
            var state = host.Arena.Player.weapons[slot]; string id = host.Arena.catalog.forms[state.FormIndex].id;
            foreach (var w in combatBindings.weapons) if (w.id == id) return w; return null;
        }
        HudSkill CombatSkill(int slot, HudWeapon weapon)
        {
            if (weapon == null) return null;
            var state = host.Arena.Player.weapons[slot]; var form = host.Arena.catalog.forms[state.FormIndex];
            string id = form.specials != null && state.SpecialIndex < form.specials.Length ? form.specials[state.SpecialIndex].id : null;
            foreach (var skill in combatBindings.skills) if (skill.weaponId == weapon.id && (skill.runtimeId == id || skill.id == id)) return skill;
            // Launcher bindings use the actual gameplay ID, never an assumed special-array order.
            if (weapon.id == "launcher") return null;
            id = weapon.specials[Mathf.Clamp(state.SpecialIndex, 0, weapon.specials.Length - 1)];
            foreach (var skill in combatBindings.skills) if (skill.id == id) return skill; return null;
        }
        ForestCombatV2.SketchRunBinding SketchBinding => run ? run.Sketch : chapter2 ? chapter2.GetComponent<ForestCombatV2.SketchRunBinding>() : null;
        // Bar starts at game pixel (97, 91), just under the plaque's bottom edge (y 86) and its gauge's left end.
        const float RewardGaugeX = 95, RewardGaugeY = 89, RewardGaugeBuffDrop = 8;
        Image rewardGaugeKey;
        RawImage rewardGaugeImage, rewardEdgeGlow; Texture2D rewardGaugeTexture, rewardEdgeTexture;
        const float RewardEdgeRise = .15f, RewardEdgeFade = .75f, RewardEdgeBorder = .16f, RewardEdgeOpacity = .5f;
        static readonly Color RewardEdgeColour = new Color(1f, .8f, .24f);
        Texture2D RewardEdgeTexture()
        {
            if (rewardEdgeTexture) return rewardEdgeTexture;
            const int size = 64; var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f), v = y / (size - 1f), edge = Mathf.Min(u, 1 - u, v, 1 - v);
                pixels[y * size + x] = new Color(RewardEdgeColour.r, RewardEdgeColour.g, RewardEdgeColour.b, Mathf.Pow(Mathf.Clamp01((RewardEdgeBorder - edge) / RewardEdgeBorder), 2));
            }
            rewardEdgeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Reward edge glow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            rewardEdgeTexture.SetPixels(pixels); rewardEdgeTexture.Apply(false);
            return rewardEdgeTexture;
        }
        readonly Deviant.Presentation.Unity.RewardGaugeStrip rewardGauge = new Deviant.Presentation.Unity.RewardGaugeStrip();
        bool RewardGaugeShown => rewardGaugeImage && rewardGaugeImage.gameObject.activeSelf;
        void RefreshSketchGauge()
        {
            var sketch = SketchBinding; var state = sketch ? sketch.State : Sketch?.State;
            rewardGaugeImage.gameObject.SetActive(state != null);
            if (state == null) { rewardGaugeKey.gameObject.SetActive(false);rewardEdgeGlow.enabled = false;bankedRewardPrompt.SetActive(false);return; }
            if(!ReferenceEquals(inventoryPromptRun,state))
            {inventoryPromptRun=state;inventoryPromptSeen=false;}
            if(Presenter.IsOpen)inventoryPromptSeen=true;
            inventoryPromptWaiting=!inventoryPromptSeen&&state.Stage==1&&state.OffersOpened>0&&state.PendingOffer==null;
            bool fresh = !rewardGaugeTexture;
            if (fresh)
            {
                rewardGaugeTexture = new Texture2D(Deviant.Presentation.Unity.RewardGaugeStrip.Width, Deviant.Presentation.Unity.RewardGaugeStrip.Height, TextureFormat.RGBA32, false)
                { name = "Reward gauge", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                rewardGaugeImage.texture = rewardGaugeTexture;
            }
            string rewardLabel=KeyLabel(GameAction.OpenReward);
            rewardGaugeKey.gameObject.SetActive(state.Banked>0&&rewardLabel.Length>1);
            rewardGaugeKey.GetComponentInChildren<Text>().text=rewardLabel;FitApprovedKeycap(rewardGaugeKey.GetComponentInChildren<Text>());
            ((RectTransform)rewardGaugeKey.transform).anchoredPosition=new Vector2(state.Banked>=10?321:315,-90);
            if (rewardGauge.Draw(state.GaugeFraction, state.Banked, Time.unscaledTime, rewardLabel.Length>1?"":rewardLabel) || fresh)
            { rewardGaugeTexture.SetPixels32(rewardGauge.Pixels); rewardGaugeTexture.Apply(false); }
            float age = Time.unscaledTime - rewardGauge.FlashStartedAt;
            bool promptShown=(state.Banked>0||inventoryPromptWaiting)&&!Presenter.IsOpen&&!Presenter.Paused&&
                !(sketch&&sketch.RewardModalShown)&&!(shop&&shop.IsOpen)&&!(map&&map.IsOpen)&&!(augments&&augments.IsOpen);
            bankedRewardPrompt.SetActive(promptShown);
            bankedRewardKey.text=inventoryPromptWaiting?KeyLabel(GameAction.Inventory):rewardLabel;
            bankedRewardCaption.text=inventoryPromptWaiting?"인벤토리":"보상";
            float keyWidth=FitApprovedKeycap(bankedRewardKey);
            Place(bankedRewardFrame,0,3,keyWidth,25);
            float captionWidth=Mathf.Ceil(bankedRewardCaption.preferredWidth),promptWidth=keyWidth+12+captionWidth;
            Place(bankedRewardPrompt.transform,480-promptWidth/2,486,promptWidth,32);
            Place(bankedRewardCaption,keyWidth+12,0,captionWidth,32);
            float haloAlpha=inventoryPromptWaiting?0:RewardPromptGlowAlpha(age);
            Color promptColor=world && !chapter2 && world.useTeamSketchTerrain ? new Color32(91,96,100,255) : Color.white;
            bankedRewardCaption.color=bankedRewardKey.color=bankedRewardFrame.color=promptColor;
            foreach(var halo in new[]{bankedRewardCaptionGlow,bankedRewardKeyGlow,bankedRewardFrameGlow})
            {halo.enabled=haloAlpha>0;halo.effectColor=new Color(217/255f,203/255f,132/255f,haloAlpha);}
            bool glowing = age >= 0 && age < RewardEdgeRise + RewardEdgeFade;
            rewardEdgeGlow.enabled = glowing;
            if (glowing)
            {
                float rise = Mathf.Clamp01(age / RewardEdgeRise), fade = Mathf.Clamp01((age - RewardEdgeRise) / RewardEdgeFade);
                rewardEdgeGlow.color = new Color(1, 1, 1, RewardEdgeOpacity * rise * (1 - fade * fade));
            }
        }
        static float RewardPromptGlowAlpha(float age)
        {
            if(age<0||age>=5)return 0;
            return .25f+.6f*(.5f+.5f*Mathf.Cos(age*Mathf.PI*4));
        }
        void RefreshSketchDurability(int w)
        {
            var sketch = SketchBinding; var weapon = sketch && sketch.State != null ? sketch.State.Arsenal.Equipped(w) : null;
            bool number = ForestCombatV2.SketchRunBinding.DurabilityAsNumber;
            ShowDurabilityArc(w, number ? (float?)null : weapon != null ? weapon.DurabilityLeft / Mathf.Max(1, weapon.MaxDurability) : 0f);
            durabilityText[w].text = weapon != null && number ? $"{Mathf.CeilToInt(weapon.DurabilityLeft):N0}" : "";
        }
        // Centres the arc on the disc at whole pixels of the 960×540 canvas so its dots sit on the plaque's pixel grid.
        void ShowDurabilityArc(int w, float? fraction)
        {
            var image = durabilityArcImages[w];
            if (!image) return;
            image.gameObject.SetActive(fraction.HasValue);
            if (!fraction.HasValue) return;
            var canvas = (RectTransform)hud.transform;
            weaponCards[w].rectTransform.GetWorldCorners(durabilityCorners);
            Vector2 a = canvas.InverseTransformPoint(durabilityCorners[0]), b = canvas.InverseTransformPoint(durabilityCorners[2]);
            float width = Mathf.Abs(b.x - a.x), height = Mathf.Abs(b.y - a.y);
            int radius = Mathf.RoundToInt(Mathf.Min(width * 1012f / 999, height) * DiscArtRadius);
            var arc = durabilityArcs[w];
            bool fresh = arc == null || arc.Radius != radius;
            if (fresh)
            {
                arc = durabilityArcs[w] = new Deviant.Presentation.Unity.DurabilityArc(radius);
                if (durabilityArcTextures[w]) Destroy(durabilityArcTextures[w]);
                durabilityArcTextures[w] = new Texture2D(arc.Size, arc.Size, TextureFormat.RGBA32, false)
                { name = "Durability arc", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                image.texture = durabilityArcTextures[w];
            }
            if (arc.Draw(fraction.Value) || fresh) { durabilityArcTextures[w].SetPixels32(arc.Pixels); durabilityArcTextures[w].Apply(false); }
            var rect = image.rectTransform; var parent = (RectTransform)rect.parent;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            // The cluster lives in the scaled HUD art space: pick the size that is exactly arc.Size canvas pixels.
            rect.sizeDelta = new Vector2(arc.Size * canvas.lossyScale.x / parent.lossyScale.x, arc.Size * canvas.lossyScale.y / parent.lossyScale.y);
            rect.position = canvas.TransformPoint(new Vector2(Mathf.Round((a.x + b.x) / 2), Mathf.Round((a.y + b.y) / 2)));
        }
        void RefreshApprovedHud()
        {
            keyHelp.SetActive(KeyHelpShown && KeyHelpAvailable);
            if (!pixelHudBuilt) BuildPixelHud();
            var player = host.Arena.Player;
            // X order swaps ignore the equipment change cooldown, so this key shows only whether a swap is possible.
            bool canSwap = Sketch != null && Sketch.CanSwapOrder;
            combatSwapButton.gameObject.SetActive(Sketch != null);
            combatSwapRemaining.gameObject.SetActive(Sketch != null);
            combatSwapButton.interactable = canSwap;
            combatSwapKey.text = KeyLabel(GameAction.SwapWeaponOrder);
            float swapKeyWidth=Mathf.Max(28,combatSwapKey.text.Length*8+8);
            ((RectTransform)combatSwapKey.transform.parent).sizeDelta = combatSwapKey.rectTransform.sizeDelta = new Vector2(swapKeyWidth,25);
            combatSwapKey.color = canSwap ? CombatIvory : new Color(1, 1, 1, .3f);
            // Centre the key in the actual gap, including the first weapon's attached skill disc.
            var firstSkill = combatSpecialDiscs[0].rectTransform;
            var secondWeapon = weaponCards[1].rectTransform;
            float gapLeft = hud.transform.InverseTransformPoint(firstSkill.TransformPoint(new Vector3(firstSkill.rect.xMax, firstSkill.rect.center.y, 0))).x;
            float gapRight = hud.transform.InverseTransformPoint(secondWeapon.TransformPoint(new Vector3(secondWeapon.rect.xMin, secondWeapon.rect.center.y, 0))).x;
            float swapCentre = Mathf.Round((gapLeft + gapRight) * .5f);
            ((RectTransform)combatSwapButton.transform).anchoredPosition = new Vector2(swapCentre - swapKeyWidth / 2, -466);
            combatSwapRemaining.rectTransform.anchoredPosition = new Vector2(swapCentre - 24, -493);
            combatSwapRemaining.alignment = TextAnchor.UpperCenter;
            combatSwapRemaining.text = "↔";
            healthText.text = Mathf.CeilToInt(player.health).ToString(); healthMax.text = "/ " + Mathf.CeilToInt(player.maxHealth);
            RefreshHealthHitFeedback();
            RefreshHealthPlaque(player);
            goldText.text = (chapter2 ? chapter2.Economy.Gold : run?.Run?.Economy?.Gold ?? 0).ToString();
            pigmentText.text = Profile.Snapshot.Pigment.ToString();
            RefreshSketchGauge();
            bagCount.text = $"가방 {host.TotalStored}/{host.Inventory.Capacity}";
            for (int w = 0; w < 2; w++)
            {
                bool ready = player.IsWeaponSlotReady(w); var state = player.weapons[w]; var cd = state.Cooldown;
                combatWeaponRoots[w].gameObject.SetActive(ready);
                if (!ready) continue;
                bool unarmed = host.Arena.catalog.forms[state.FormIndex].id == "unarmed";
                var weapon = CombatWeapon(w); var skill = CombatSkill(w, weapon);
                // 맨손은 글자 대신 주먹 그림(weapon-unarmed)을 무기 원에 그린다.
                weaponIcons[w].sprite = CombatSprite(unarmed ? "weapon-unarmed" : ready ? skill?.presentationIcon ?? weapon?.icon : "state-empty-weapon");
                weaponIcons[w].enabled = weaponIcons[w].sprite;
                combatUnarmedNames[w].text = "";
                // With run inks (Arena.Sketch) RunInks sets the same arc and its own number below; this number stays empty there.
                if (Sketch == null) RefreshSketchDurability(w);
                else durabilityText[w].text = "";
                combatSpecial[w].sprite = CombatSprite(skill?.icon ?? "state-unavailable");
                combatSpecial[w].transform.parent.gameObject.SetActive(!unarmed);
                combatKeys[w, 1].transform.parent.gameObject.SetActive(!unarmed);
                combatTray[w].transform.parent.gameObject.SetActive(!unarmed);
                combatSelected[w].sprite = player.weapons.ActiveSlot == w && ready ? pixelRingSelected[w] : pixelRingIdle[w];
                float left = ready && cd.Capacity > 0 && cd.Available == 0 ? Mathf.Max(0, cd.NextReady - host.Arena.Now) : 0;
                weaponShades[w].enabled = left > 0; weaponShades[w].fillAmount = cd.Period > 0 ? Mathf.Clamp01(left / cd.Period) : 0;
                weaponSeconds[w].text = CooldownSeconds(left); weaponIcons[w].color = Color.white;
                combatSpecial[w].color = left > 0 ? new Color(1, 1, 1, .3f) : Color.white;
                var cast = player.currentCast; bool active = player.weapons.ActiveSlot == w;
                bool inkCharge = active && player.InkCharging;
                bool charging = ready && active && cast && !cast.isBasic && (inkCharge || cast.ChargeActive && !cast.ChargeRelease.HasValue);
                float progress = inkCharge ? player.InkChargeFraction : charging && cast.ChargePolicy != null ? Mathf.Clamp01(cast.Age / Mathf.Max(.001f, cast.ChargePolicy.Maximum)) : 0;
                combatProgress[w].enabled = charging; combatProgress[w].fillAmount = progress;
                combatChargePercent[w].text = charging ? Mathf.RoundToInt(progress * 100) + "%" : "";
                combatCharges[w].text = ready && cd.Capacity > 1 ? cd.Available + "/" + cd.Capacity : "";
                combatKeys[w, 0].text = KeyLabel(w == 0 ? GameAction.Weapon1Basic : GameAction.Weapon2Basic);
                combatKeys[w, 1].text = KeyLabel(w == 0 ? GameAction.Weapon1Special : GameAction.Weapon2Special);
                var palette = shop ? shop.Growth?.Equipped(shop.GrowthWeapon(w)) : null;
                int n = palette != null ? Mathf.Clamp(palette.Slots.Count, 3, 5) : 3;
                var sketchWeapon = Sketch?.Equipped(w);
                if (Sketch != null) n = sketchWeapon?.Slots.Count ?? 0;
                // A fixed five-slot tray keeps both weapon groups still as the palette grows.
                for (int j = 0; j < 6; j++)
                {
                    string id = "weapon-" + w + ".ink-" + j;
                    var slot = palette != null && j < palette.Slots.Count ? palette.Slots[j] : null;
                    combatDrops[w, j].sprite = DropArt(slot?.Part, slot?.Color) ?? CombatSprite(slot != null ? "drop-" + slot.Color : "slot-empty");
                    combatDrops[w, j].color = slot?.Part == null ? new Color(1, 1, 1, .4f) : Color.white;
                    pixelDropColour[w, j] = null;
                    if (Sketch != null)
                    {
                        var mounted = sketchWeapon != null && j < sketchWeapon.Slots.Count ? sketchWeapon.Slots[j] : null;
                        combatDrops[w, j].sprite = mounted?.Ink != null ? RunInkIcon(mounted.Ink) : CombatSprite("slot-empty");
                        combatDrops[w, j].color = mounted?.Ink == null ? new Color(1, 1, 1, .4f) : Color.white;
                        // Pixel blobs (CombatHudPixels): a mounted ink paints its colour, an empty slot outlines its own colour.
                        pixelDropColour[w, j] = mounted == null ? null : mounted.Ink != null ? "ink:" + mounted.Ink.color : "empty:" + mounted.Color;
                    }
                    CombatPlace(combatDrops[w, j].rectTransform, id, 143 + w * 370 + j * 42, 889, 29, 35);
                    // Item drops have square padded canvases; match their visible height to the tight colourless drop.
                    var drop = combatDrops[w, j];
                    if (drop.sprite && Mathf.Approximately(drop.sprite.rect.width, drop.sprite.rect.height))
                    {
                        var cell = drop.rectTransform.sizeDelta;
                        var expanded = cell * 1.35f;
                        drop.rectTransform.sizeDelta = expanded;
                        // preserveAspect top-aligns the square art at our top-left pivot; centre its drawn square, not the taller rect.
                        drop.rectTransform.anchoredPosition += new Vector2((cell.x - expanded.x) / 2, (expanded.x - cell.y) / 2);
                    }
                    combatDrops[w, j].rectTransform.anchoredPosition += new Vector2((5 - n) * 20.25f - 8, 0);
                    combatDrops[w, j].gameObject.SetActive(j < n && (!combatEdits.TryGetValue(id, out var edit) || !edit.hidden));
                }
                RefreshSketchDurability(w, sketchWeapon);
            }
            RefreshPixelHud();
            TrackWeaponBreaks();
        }
        void RefreshHealthPlaque(Deviant.Combat.CombatPlayer player)
        {
            if (healthInkPlayer != player) { healthPlaque.Ink.Reset(); healthInkPlayer = player; healthInkHits = -1; }
            // Skill guard shield while the guard lasts, plus every ink shield (run inks included) as combat absorbs them.
            var arena = host.Arena;
            float shield = (arena.Now < player.guardUntil ? Mathf.Max(0, player.shield) : 0) + arena.ReadInkShield();
            // Run-ink shields also drain over time: only a real accepted hit flashes and scatters the shield.
            int hits = player.ReceivedHitCount; bool struck = healthInkHits >= 0 && hits > healthInkHits; healthInkHits = hits;
            float age = healthHitAt < 0 ? float.PositiveInfinity : Time.unscaledTime - healthHitAt;
            float flash = age < HealthHitFlashSeconds ? Mathf.Clamp01((HealthHitFlashSeconds - age) / .095f) : 0;
            healthPlaque.Draw(player.health, player.maxHealth, shield, Time.unscaledTime, struck, flash);
            healthFill.fillAmount = healthPlaque.Ink.SolidFraction;
            if (!healthPlaqueTexture)
            {
                healthPlaqueTexture = new Texture2D(Deviant.Presentation.Unity.HealthPlaque.Width, Deviant.Presentation.Unity.HealthPlaque.Height, TextureFormat.RGBA32, false)
                { name = "Health plaque", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                healthPlaqueImage.texture = healthPlaqueTexture;
            }
            healthPlaqueTexture.SetPixels32(healthPlaque.Pixels); healthPlaqueTexture.Apply(false);
        }
        void ResetHealthHitFeedback()
        {
            healthPlaque?.Ink.Reset(); healthInkPlayer = null; healthInkHits = -1;
            if (healthPlaqueTexture) { Destroy(healthPlaqueTexture); healthPlaqueTexture = null; }
            healthHitAt=-1;healthHitCount=-1;healthHitPlayer=null;
            if(combatHealthRoot)combatHealthRoot.anchoredPosition=healthRestPosition;
            if(healthHitFlash)healthHitFlash.gameObject.SetActive(false);
        }
        void RefreshHealthHitFeedback()
        {
            var player=host.Arena.Player;
            int hits=player.ReceivedHitCount;
            // This count belongs to committed hits; healing or assigning HP cannot create a flash.
            if(healthHitPlayer!=player||healthHitCount<0||hits<healthHitCount)
            {healthHitPlayer=player;healthHitAt=-1;}
            else if(hits>healthHitCount)healthHitAt=Time.unscaledTime;
            healthHitCount=hits;
            float age=healthHitAt<0?float.PositiveInfinity:Time.unscaledTime-healthHitAt;
            if(age>=HealthHitShakeSeconds)
            {combatHealthRoot.anchoredPosition=healthRestPosition;healthHitFlash.gameObject.SetActive(false);return;}
            float envelope=Mathf.Pow(1-age/HealthHitShakeSeconds,2);
            // Absolute offsets cannot drift. A new hit restarts one bounded pulse instead of adding shakes.
            combatHealthRoot.anchoredPosition=healthRestPosition+new Vector2(
                Mathf.Cos(age*105)*HealthHitShakePixels*1670/960,
                Mathf.Sin(age*132)*HealthHitShakePixels*.55f*942/540)*envelope;
            healthHitFlash.fillAmount=healthFill.fillAmount;
            healthHitFlash.color=new Color(1,1,1,healthFill.color.a*Mathf.Clamp01((HealthHitFlashSeconds-age)/.095f));
            healthHitFlash.gameObject.SetActive(age<HealthHitFlashSeconds);
        }
    }
}
