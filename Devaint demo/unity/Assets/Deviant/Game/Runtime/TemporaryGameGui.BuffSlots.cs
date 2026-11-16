using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // 버프칸(docs/design/ui/screens.md의 버프칸):캐릭터 HP 바 아래(한 줄 6칸)와 보스 체력바 아래(기절·속박).
    // 칸마다 아이콘 위에 같은 그림 모양의 어두운 덮개를 얹고, 남은 내부 쿨타임 비율만큼 12시에서 반시계로 채운다.
    // 시간이 흐르면 덮개가 12시부터 시계방향으로 지워진다. 효과가 지속 중인 동안은 덮개 대신 BuffSlotGlow(사용자 선택 D)가
    // 아이콘 안의 잉크 빛 수면을 남은 지속 비율만큼 내리고, 발동·종료·준비 순간을 짧게 알린다.
    public sealed partial class TemporaryGameGui
    {
        sealed class BuffSlotView
        {
            public GameObject Root; public Image Icon, Badge, Shade; public Text Seconds; public string Tooltip;
            public RawImage Fx; public Texture2D FxTexture; public readonly Deviant.Presentation.Unity.BuffSlotGlow Glow = new Deviant.Presentation.Unity.BuffSlotGlow();
        }
        // 모두 960×540 캔버스 기준. 칸 크기는 아이콘 원본과 같은 24라서 캔버스 배율(1080p에서 2배,
        // 정수배로만 키우는 픽셀 화면)이 그대로 아이콘 배율이 된다. 좌표도 정수로 두어 화소 경계에 맞춘다.
        // 캐릭터칸: 도트 체력판(HealthPlaque, 276×60, 960×540의 49, 32) 아래, 게이지 왼쪽 끝(x 95) 밑에서 한 줄 6칸(184, 게이지 길이 200 안).
        // 체력판 바로 밑에 보상 게이지 줄이 보이면 그 아래로 RewardGaugeBuffDrop(8)만큼 내린다.
        const int PlayerBuffColumns = 6;
        const float PlayerBuffX = 95, PlayerBuffY = 96, PlayerBuffSize = 24, PlayerBuffGap = 8;
        // 보스칸: 보스 체력바(290, 18, 380×41) 바로 아래.
        const float BossBuffY = 45, BossBuffSize = 24, BossBuffGap = 6;
        const float BossBuffRowHeight = 32;
        static readonly Color BuffShadeColor = new Color(.03f, .02f, .05f, .68f);
        readonly List<BuffSlotView> playerBuffViews = new List<BuffSlotView>(), bossBuffViews = new List<BuffSlotView>();
        readonly List<BuffSlot> buffScratch = new List<BuffSlot>();
        readonly Dictionary<string, Sprite> buffSprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, bool[]> buffMasks = new Dictionary<string, bool[]>();
        // 지속 연출 잉크 색(어두움 → 흰색): 아이콘 그림의 주된 색 계열을 따른다.
        static readonly Color32[] BuffInkGreen = BuffRamp(0x0f2a18, 0x2a6a33, 0x4fae5c, 0x7ee08a, 0xc4f5cb, 0xf4fff5),
            BuffInkRed = BuffRamp(0x3a0f16, 0x8a1f22, 0xd23c35, 0xff6b5e, 0xffb3aa, 0xfff1ee),
            BuffInkGold = BuffRamp(0x3a2a10, 0x7a5f1c, 0xc19b26, 0xffd54a, 0xffeca0, 0xfffbe8),
            BuffInkBlue = BuffRamp(0x0c1a33, 0x124f8a, 0x186cb7, 0x6cb8ff, 0xa9d5ff, 0xf0f8ff),
            BuffInkPurple = BuffRamp(0x1e0f2c, 0x4a2470, 0x7136a0, 0xa46ad6, 0xddbef6, 0xf8f0ff);
        RectTransform playerBuffRoot, bossBuffRoot, buffTooltip;
        Text buffTooltipText;

        Sprite BuffSprite(string id)
        {
            if (buffSprites.TryGetValue(id, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("BuffSlotIcons/" + id);
            if (!sprite) throw new InvalidOperationException("Buff slot icon missing: " + id);
            return buffSprites[id] = sprite;
        }
        static string BuffIconId(BuffSlotIcon icon)
        {
            switch (icon)
            {
                case BuffSlotIcon.MoveSpeed: return "move-speed";
                case BuffSlotIcon.Shield: return "shield";
                case BuffSlotIcon.Heal: return "heal";
                case BuffSlotIcon.Stun: return "stun";
                case BuffSlotIcon.Bind: return "bind";
                case BuffSlotIcon.Poison: return "poison";
                case BuffSlotIcon.Slow: return "slow";
                case BuffSlotIcon.BasicDamage: return "basic-damage";
                case BuffSlotIcon.BasicSpeed: return "basic-speed";
                case BuffSlotIcon.SkillCooldown: return "skill-cooldown";
                case BuffSlotIcon.SkillDamage: return "skill-damage";
                default: throw new ArgumentOutOfRangeException(nameof(icon));
            }
        }
        static Color32[] BuffRamp(params int[] hex)
        {
            var ramp = new Color32[hex.Length];
            for (int i = 0; i < hex.Length; i++) ramp[i] = new Color32((byte)(hex[i] >> 16), (byte)(hex[i] >> 8), (byte)hex[i], 255);
            return ramp;
        }
        static Color32[] BuffInk(BuffSlotIcon icon)
        {
            switch (icon)
            {
                case BuffSlotIcon.Shield: case BuffSlotIcon.Heal: return BuffInkGreen;
                case BuffSlotIcon.BasicDamage: case BuffSlotIcon.BasicSpeed: return BuffInkRed;
                case BuffSlotIcon.MoveSpeed: return BuffInkGold;
                case BuffSlotIcon.SkillCooldown: case BuffSlotIcon.SkillDamage: return BuffInkBlue;
                default: return BuffInkPurple;
            }
        }
        // 아이콘(+배지) 그림의 불투명 칸(24×24, 위에서 아래). 가져오기 설정이 읽기 불가여도 GPU 복사로 읽고, 실패하면 칸 전체를 쓴다.
        bool[] BuffMask(string icon, string badge)
        {
            string key = icon + "+" + badge;
            if (buffMasks.TryGetValue(key, out var mask)) return mask;
            mask = new bool[Deviant.Presentation.Unity.BuffSlotGlow.Icon * Deviant.Presentation.Unity.BuffSlotGlow.Icon];
            bool any = false;
            foreach (var id in new[] { icon, badge })
            {
                if (id == null) continue;
                var alpha = SpriteAlpha(BuffSprite(id));
                if (alpha == null) continue;
                for (int i = 0; i < mask.Length; i++) mask[i] |= alpha[i]; any = true;
            }
            if (!any) mask = null;
            return buffMasks[key] = mask;
        }
        static bool[] SpriteAlpha(Sprite sprite)
        {
            const int size = Deviant.Presentation.Unity.BuffSlotGlow.Icon;
            try
            {
                var texture = sprite.texture; var rect = sprite.textureRect;
                int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height);
                Color32[] pixels;
                if (texture.isReadable) pixels = texture.GetPixels32();
                else
                {
                    var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                    var previous = RenderTexture.active;
                    Texture2D copy = null;
                    try
                    {
                        Graphics.Blit(texture, rt); RenderTexture.active = rt;
                        copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                        copy.ReadPixels(new UnityEngine.Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply(false);
                        pixels = copy.GetPixels32();
                    }
                    finally
                    {
                        RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
                        if (copy) Destroy(copy);
                    }
                }
                var mask = new bool[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int sx = Mathf.FloorToInt(rect.x + (x + .5f) * w / size), sy = Mathf.FloorToInt(rect.y + rect.height - (y + .5f) * h / size);
                        mask[y * size + x] = pixels[sy * texture.width + sx].a >= 128;
                    }
                return mask;
            }
            catch (Exception) { return null; }
        }
        static string BuffTriggerId(BuffSlotTrigger trigger)
        {
            switch (trigger)
            {
                case BuffSlotTrigger.Basic: return "trigger-basic";
                case BuffSlotTrigger.Skill: return "trigger-skill";
                case BuffSlotTrigger.Switch: return "trigger-switch";
                case BuffSlotTrigger.Dash: return "trigger-dash";
                case BuffSlotTrigger.Status: return "trigger-status";
                default: return null;
            }
        }
        void BuildBuffSlots()
        {
            playerBuffRoot = Rect("Player buff slots", hud.transform, 0, 0, 960, 540);
            bossBuffRoot = Rect("Boss buff slots", bossHud.transform, 0, BossBuffY, 380, BossBuffSize);
        }
        BuffSlotView MakeBuffSlot(Transform parent, float size, int fontSize)
        {
            var root = Rect("Buff slot", parent, 0, 0, size, size);
            Image Layer(string name)
            {
                var image = Rect(name, root, 0, 0, size, size).gameObject.AddComponent<Image>();
                image.raycastTarget = false; image.preserveAspect = true; return image;
            }
            var view = new BuffSlotView { Root = root.gameObject, Icon = Layer("Icon"), Badge = Layer("Trigger") };
            // 지속 연출 층: 아이콘보다 사방 Margin 만큼 크다(별빛·빛 알갱이가 칸 밖으로 조금 나간다).
            int margin = Deviant.Presentation.Unity.BuffSlotGlow.Margin, fxSize = Deviant.Presentation.Unity.BuffSlotGlow.Width;
            view.Fx = Rect("Active", root, -margin * size / 24f, -margin * size / 24f, fxSize * size / 24f, fxSize * size / 24f).gameObject.AddComponent<RawImage>();
            view.Fx.raycastTarget = false; view.Fx.enabled = false;
            view.Shade = Layer("Cooldown");
            view.Shade.color = BuffShadeColor; view.Shade.type = Image.Type.Filled; view.Shade.fillMethod = Image.FillMethod.Radial360;
            view.Shade.fillOrigin = (int)Image.Origin360.Top; view.Shade.fillClockwise = false;
            view.Seconds = Words("", root, 0, 0, size, size, fontSize, true, Color.white, TextAnchor.MiddleCenter);
            view.Seconds.raycastTarget = false;
            var outline = view.Seconds.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(1, -1);
            return view;
        }
        void DrawBuffSlots(List<BuffSlotView> views, RectTransform parent, List<BuffSlot> slots, float now, float size, float gap, int columns, Vector2 origin, int fontSize)
        {
            while (views.Count < slots.Count) views.Add(MakeBuffSlot(parent, size, fontSize));
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i]; bool shown = i < slots.Count; view.Root.SetActive(shown);
                if (!shown) continue;
                var slot = slots[i];
                view.Tooltip = slot.Tooltip;
                Place(view.Root.transform as RectTransform, origin.x + i % columns * (size + gap), origin.y + i / columns * (size + gap), size, size);
                var icon = BuffSprite(BuffIconId(slot.Icon));
                view.Icon.sprite = icon; view.Shade.sprite = icon;
                string badge = BuffTriggerId(slot.Trigger);
                view.Badge.gameObject.SetActive(badge != null);
                if (badge != null) view.Badge.sprite = BuffSprite(badge);
                float active = slot.ActiveRemaining(now);
                float dark = active > 0 ? 0 : slot.DarkFraction(now);
                view.Shade.enabled = dark > 0; view.Shade.fillAmount = dark;
                view.Seconds.text = dark > 0 ? Mathf.CeilToInt(slot.Remaining(now)).ToString() : "";
                DrawBuffGlow(view, slot, i, active, now, badge);
            }
        }
        void DrawBuffGlow(BuffSlotView view, BuffSlot slot, int index, float active, float now, string badge)
        {
            var glow = view.Glow;
            glow.Observe(index + ":" + slot.Icon + ":" + slot.Trigger, active, slot.ActiveDuration, slot.Remaining(now), now);
            bool shown = glow.Animating;
            view.Fx.enabled = shown;
            if (!shown) return;
            glow.Paint(BuffInk(slot.Icon), BuffMask(BuffIconId(slot.Icon), badge));
            if (!view.FxTexture)
            {
                view.FxTexture = new Texture2D(Deviant.Presentation.Unity.BuffSlotGlow.Width, Deviant.Presentation.Unity.BuffSlotGlow.Height, TextureFormat.RGBA32, false)
                { name = "Buff slot active", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                view.Fx.texture = view.FxTexture;
            }
            view.FxTexture.SetPixels32(glow.Pixels); view.FxTexture.Apply(false);
        }
        void DisposeBuffSlots()
        {
            foreach (var view in playerBuffViews) if (view.FxTexture) Destroy(view.FxTexture);
            foreach (var view in bossBuffViews) if (view.FxTexture) Destroy(view.FxTexture);
        }
        void RefreshBuffTooltip()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            BuffSlotView hovered = null;
            if (mouse != null && Application.isFocused && hud && hud.activeInHierarchy &&
                !(bag && bag.activeInHierarchy) && !(shopPanel && shopPanel.activeInHierarchy))
                hovered = playerBuffViews.Find(v => v.Root.activeInHierarchy && !string.IsNullOrEmpty(v.Tooltip) &&
                    RectTransformUtility.RectangleContainsScreenPoint((RectTransform)v.Root.transform, mouse.position.ReadValue()));
            if (hovered == null) { if (buffTooltip) buffTooltip.gameObject.SetActive(false); return; }
            ShowBuffTooltip((RectTransform)hovered.Root.transform, hovered.Tooltip);
        }

        void ShowBuffTooltip(RectTransform anchor, string description)
        {
            const float width = 330, margin = 8;
            if (!buffTooltip)
            {
                var panel = Rounded("Ink effect tooltip", stage, 0, 0, width, 40, 6, 1, JBubble, JRule);
                buffTooltip = panel.rectTransform;
                buffTooltipText = Words("", buffTooltip, 12, 10, width - 24, 20, 13, false, JInk);
                buffTooltipText.color = JInk;
                buffTooltipText.supportRichText = false;
                buffTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
                foreach (var graphic in buffTooltip.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            }
            if (buffTooltipText.text != description)
            {
                buffTooltipText.text = description;
                float height = buffTooltipText.preferredHeight;
                Place(buffTooltipText, 12, 10, width - 24, height);
                buffTooltip.sizeDelta = new Vector2(width, height + 20);
            }
            var point = stage.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.xMin, anchor.rect.yMin)));
            var bounds = stage.rect;
            float x = Mathf.Clamp(point.x, bounds.xMin + margin, bounds.xMax - width - margin);
            float top = Mathf.Clamp(point.y - margin, bounds.yMin + buffTooltip.rect.height + margin, bounds.yMax - margin);
            buffTooltip.anchoredPosition = new Vector2(x - bounds.xMin, top - bounds.yMax);
            buffTooltip.SetAsLastSibling(); buffTooltip.gameObject.SetActive(true);
        }
        // 보스칸이 보이면 그 아래에 놓이는 획득 알림·안내 문구를 한 줄만큼 내린다.
        float BossHudBottomShift => bossBuffRoot && bossBuffRoot.gameObject.activeInHierarchy && bossBuffViews.Exists(v => v.Root.activeSelf) ? BossBuffRowHeight : 0;
        void RefreshBuffSlots(CombatActor player, CombatActor bossActor, bool hasBoss)
        {
            float now = host.Arena.Now;
            buffScratch.Clear(); player?.BuffSlotSource?.Collect(buffScratch);
            DrawBuffSlots(playerBuffViews, playerBuffRoot, buffScratch, now, PlayerBuffSize, PlayerBuffGap, PlayerBuffColumns, new Vector2(PlayerBuffX, PlayerBuffY + (RewardGaugeShown ? RewardGaugeBuffDrop : 0)), 12);
            buffScratch.Clear(); if (hasBoss) BossBuffSlots.Collect(bossActor, buffScratch);
            DrawBuffSlots(bossBuffViews, bossBuffRoot, buffScratch, now, BossBuffSize, BossBuffGap, int.MaxValue, Vector2.zero, 12);
        }
    }
}
