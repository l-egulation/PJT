using Deviant.Combat;
using Deviant.Input.Contracts;
using Deviant.Presentation.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace ForestCombatV2
{
    // A screen-space prompt keeps Korean text and the currency icon readable at every zoom.
    public sealed class StageRecoveryPrompt : MonoBehaviour
    {
        StageRecoveryStation station;
        CombatArena arena;
        Font font;
        Canvas canvas;
        RectTransform panel;
        Text key, binding, price, details, status, healAmount;
        Image coin, keycap;
        KeycapHoldOutline holdOutline;
        const float KeyWidth = 22, KeyHeight = 24;
        const float HealSeconds = 1;
        float healStartedAt = float.NegativeInfinity;
        public bool Visible => panel && panel.gameObject.activeSelf;
        public string Details => details ? details.text : "";
        public string PriceText => price ? price.text : "";
        public Sprite Coin => coin ? coin.sprite : null;
        public Sprite KeySprite => keycap ? keycap.sprite : null;
        public float BorderProgress => holdOutline ? holdOutline.Progress : 0;
        public bool HealAmountVisible => healAmount && healAmount.gameObject.activeSelf;
        public string HealAmountText => healAmount ? healAmount.text : "";

        public void Initialize(StageRecoveryStation owner, CombatArena combat)
        {
            station = owner; arena = combat;
            font = Resources.Load<Font>("PixelInventory/Fonts/Galmuri11");
            if (!font) throw new System.InvalidOperationException("Recovery prompt requires a Korean UI font.");
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90; canvas.pixelPerfect = true;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540); scaler.matchWidthOrHeight = .5f;
            panel = Rect("Recovery prompt", transform, new Vector2(256, 72), Vector2.zero);
            panel.pivot = new Vector2(.5f, 0);
            var keyRect = Rect("Interaction keycap", panel, new Vector2(KeyWidth, KeyHeight), Vector2.zero);
            keycap = keyRect.gameObject.AddComponent<Image>(); keycap.sprite = KeycapMarker.SpriteFor('F');
            keycap.preserveAspect = true; keycap.raycastTarget = false;
            binding = Label("Rebound key", new Vector2(80, KeyHeight), Vector2.zero, 12);
            binding.color = new Color32(40, 35, 26, 255);
            binding.gameObject.SetActive(false);
            var outlineRect = Rect("Keycap hold outline", panel, new Vector2(KeyWidth + 2, KeyHeight + 2), Vector2.zero);
            holdOutline = outlineRect.gameObject.AddComponent<KeycapHoldOutline>();
            holdOutline.color = station.Chapter == 1 ? new Color32(38, 146, 76, 255) : new Color32(107, 224, 161, 255);
            holdOutline.raycastTarget = false;
            key = Label("Hold hint", new Vector2(48, 16), Vector2.zero, 12);
            price = Label("Price", new Vector2(46, 16), Vector2.zero, 12);
            // The baked Galmuri atlas has no bold face. Repeat one pixel to the right for bold digits.
            var bold = price.gameObject.AddComponent<Shadow>();
            bold.effectDistance = Vector2.right; bold.effectColor = price.color; bold.useGraphicAlpha = true;
            var icon = Rect("Coin", panel, new Vector2(16, 16), Vector2.zero);
            coin = icon.gameObject.AddComponent<Image>();
            coin.sprite = Resources.Load<Sprite>("ApprovedCombatHUD/sprites/gold-coin");
            coin.preserveAspect = true; coin.raycastTarget = false;
            details = Label("Recovery details", new Vector2(248, 16), Vector2.zero, 12);
            status = Label("Status", new Vector2(248, 16), Vector2.zero, 12);
            healAmount = Label("Player heal amount", new Vector2(96, 16), Vector2.zero, 12, transform);
            healAmount.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 at)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = at; return rect;
        }
        Text Label(string name, Vector2 size, Vector2 at, int fontSize, Transform parent = null)
        {
            var text = Rect(name, parent ? parent : panel, size, at).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
            text.color = station.Chapter == 1 ? Color.black : Color.white; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (!panel) return;
            RefreshHealAmount();
            bool show = station && station.ShowPrompt && arena.ViewCamera;
            if (!show) { holdOutline.SetProgress(0); panel.gameObject.SetActive(false); return; }
            var point = CameraDisplayProjection.WorldToScreenPoint(arena.ViewCamera,
                station.Position + Vector2.up * (station.ArtHeight + .06f));
            if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height)
            { panel.gameObject.SetActive(false); return; }
            panel.gameObject.SetActive(true);
            string currentKey = PlayerControls.Label(arena.Player, GameAction.Interact);
            bool defaultKey = currentKey == "F";
            keycap.sprite = defaultKey ? KeycapMarker.SpriteFor('F') : null;
            keycap.preserveAspect = defaultKey;
            binding.gameObject.SetActive(!defaultKey); binding.text = defaultKey ? "" : currentKey;
            float keyWidth = defaultKey ? KeyWidth : Mathf.Max(KeyWidth, binding.preferredWidth + 8);
            keycap.rectTransform.sizeDelta = new Vector2(keyWidth, KeyHeight);
            binding.rectTransform.sizeDelta = new Vector2(keyWidth, KeyHeight);
            holdOutline.rectTransform.sizeDelta = new Vector2(keyWidth + 2, KeyHeight + 2);
            holdOutline.SetProgress(station.Holding ? station.HoldProgress : 0);
            key.text = "길게";
            price.text = station.Remaining > 0 ? station.Price.ToString() : "—";
            details.text = station.Remaining + "회 남음 / 최대 체력 " + station.HealPercent + "% 회복";
            status.text = station.PromptStatus;
            bool hasStatus = !string.IsNullOrEmpty(status.text);
            float extra = hasStatus ? 22 : 0;
            panel.sizeDelta = new Vector2(256, 72 + extra);
            float centre = panel.sizeDelta.y / 2;
            // Centre each visible pair by its actual content width. Rows have 6px of clear space.
            float hintWidth = Mathf.Ceil(key.preferredWidth);
            key.rectTransform.sizeDelta = new Vector2(hintWidth, 16);
            float keyLeft = -(keyWidth + 6 + hintWidth) / 2;
            float keyX = Mathf.Round(keyLeft + keyWidth / 2), keyY = 56 + extra - centre;
            keycap.rectTransform.anchoredPosition = binding.rectTransform.anchoredPosition =
                holdOutline.rectTransform.anchoredPosition = new Vector2(keyX, keyY);
            key.rectTransform.anchoredPosition = new Vector2(Mathf.Round(keyLeft + keyWidth + 6 + hintWidth / 2), keyY);
            float numberWidth = Mathf.Ceil(price.preferredWidth);
            price.rectTransform.sizeDelta = new Vector2(numberWidth, 16);
            float priceLeft = -(16 + 6 + numberWidth + 1) / 2; // Include the bold digit's extra pixel.
            float priceY = 30 + extra - centre;
            coin.rectTransform.anchoredPosition = new Vector2(Mathf.Round(priceLeft + 8), priceY);
            price.rectTransform.anchoredPosition = new Vector2(Mathf.Round(priceLeft + 22 + numberWidth / 2), priceY);
            details.rectTransform.anchoredPosition = new Vector2(0, 8 + extra - centre);
            status.rectTransform.anchoredPosition = new Vector2(0, 8 - centre);
            status.gameObject.SetActive(hasStatus);
            var canvasRect = (RectTransform)canvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            float halfWidth = panel.sizeDelta.x / 2;
            local.x = Mathf.Clamp(local.x, canvasRect.rect.xMin + halfWidth + 8, canvasRect.rect.xMax - halfWidth - 8);
            local.y = Mathf.Clamp(local.y, canvasRect.rect.yMin + 8, canvasRect.rect.yMax - panel.sizeDelta.y - 8);
            panel.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));
        }
        public void ShowHeal(float restored)
        {
            if (restored <= 0) return;
            healStartedAt = arena.Now;
            healAmount.text = "+" + restored.ToString("0.#") + " HP";
            RefreshHealAmount();
        }
        public void ClearHeal()
        {
            healStartedAt = float.NegativeInfinity;
            if (healAmount) { healAmount.text = ""; healAmount.gameObject.SetActive(false); }
        }
        void RefreshHealAmount()
        {
            if (!healAmount) return;
            float age = arena.Now - healStartedAt;
            bool show = station.isActiveAndEnabled && arena.Player && !arena.Player.Dead && arena.ViewCamera &&
                !arena.Controls.Current.SimulationPaused && age >= 0 && age < HealSeconds;
            if (!show) { healAmount.gameObject.SetActive(false); return; }
            var world = arena.Player.Position + Vector2.up * (2.25f + .4f * age / HealSeconds);
            var point = CameraDisplayProjection.WorldToScreenPoint(arena.ViewCamera, world);
            if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height)
            { healAmount.gameObject.SetActive(false); return; }
            healAmount.gameObject.SetActive(true);
            var colour = station.Chapter == 1 ? Color.black : Color.white;
            colour.a = 1 - Mathf.Clamp01((age - .65f) / .35f); healAmount.color = colour;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, point,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            healAmount.rectTransform.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));
        }
        public void Hide()
        {
            if (holdOutline) holdOutline.SetProgress(0);
            if (panel) panel.gameObject.SetActive(false);
            if (healAmount) healAmount.gameObject.SetActive(false);
        }
    }

    // Fill only the keycap perimeter clockwise from its top centre; the glyph remains unobscured.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KeycapHoldOutline : MaskableGraphic
    {
        public float Progress { get; private set; }
        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, Progress)) return;
            Progress = value; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Progress <= 0) return;
            var r = GetPixelAdjustedRect(); float l = r.xMin + 1, b = r.yMin + 1, t = r.yMax - 1, right = r.xMax - 1;
            float x = r.center.x, c = 2;
            var path = new[] { new Vector2(x,t), new Vector2(right-c,t), new Vector2(right,t-c),
                new Vector2(right,b+c), new Vector2(right-c,b), new Vector2(l+c,b), new Vector2(l,b+c),
                new Vector2(l,t-c), new Vector2(l+c,t), new Vector2(x,t) };
            float length = 0;
            for (int i = 1; i < path.Length; i++) length += Vector2.Distance(path[i-1],path[i]);
            float remaining = length * Progress;
            for (int i = 1; i < path.Length && remaining > 0; i++)
            {
                var a = path[i-1]; var delta = path[i]-a; float full = delta.magnitude;
                var end = a + delta * Mathf.Min(1, remaining/full);
                var normal = new Vector2(-delta.y,delta.x).normalized;
                int index = vh.currentVertCount;
                vh.AddVert(a-normal,color,Vector2.zero); vh.AddVert(a+normal,color,Vector2.zero);
                vh.AddVert(end+normal,color,Vector2.zero); vh.AddVert(end-normal,color,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2); vh.AddTriangle(index,index+2,index+3);
                remaining -= full;
            }
        }
    }
}
