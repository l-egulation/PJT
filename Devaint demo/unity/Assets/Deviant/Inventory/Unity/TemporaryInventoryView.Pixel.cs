using Deviant.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Deviant.Inventory.Unity
{
    public sealed partial class TemporaryInventoryView
    {
        [SerializeField] PixelInventoryTheme pixelTheme;
        [SerializeField] Text pixelItemName, pixelItemKind, pixelPreview, pixelToastTitle, pixelToastKind, pixelHpText;
        [SerializeField] Image pixelItemIcon, pixelPlayerPreview, pixelHpFill;
        [SerializeField] Image[] pixelWeaponIcons, pixelQuickIcons;
        [SerializeField] Button[] pixelQuickButtons;
        [SerializeField] Text[] pixelQuickLabels;
        [SerializeField] GameObject pixelPlayHud, pixelPause;
        public bool UsesPixelTheme => pixelTheme;
        readonly Color pixelCream = new Color32(248, 227, 185, 255), pixelPink = new Color32(189, 121, 173, 255);
        readonly Color pixelDark = new Color32(43, 29, 51, 255), pixelCyan = new Color32(53, 185, 190, 255);

        Text PixelLabel(string text, Transform parent, float x, float y, float width, float height, int size, bool bold = false)
        {
            float scale = size / 11f;
            var rect = Make("Label", parent, x, y, width / scale, height / scale); rect.localScale = new Vector3(scale, scale, 1);
            var label = rect.gameObject.AddComponent<Text>(); label.font = bold && size >= 40 ? pixelTheme.boldFont : pixelTheme.regularFont;
            label.fontSize = 11; label.fontStyle = FontStyle.Normal; label.color = pixelCream;
            label.alignment = TextAnchor.MiddleLeft; label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow; label.raycastTarget = false; label.text = text;
            var shadow = label.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color32(18, 11, 24, 200); shadow.effectDistance = new Vector2(.5f, -.5f);
            if (label.font.material && label.font.material.mainTexture) label.font.material.mainTexture.filterMode = FilterMode.Point;
            return label;
        }
        Image PixelImage(string name, Transform parent, float x, float y, float width, float height, Sprite sprite, bool sliced = true)
        {
            var image = Make(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple; image.pixelsPerUnitMultiplier = .5f;
            if (!sliced) image.preserveAspect = true;
            return image;
        }
        Button PixelButton(string name, Transform parent, float x, float y, float width, float height, Sprite frame, string text = "", int fontSize = 22)
        {
            var image = PixelImage(name, parent, x, y, width, height, frame); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
            var label = PixelLabel(text, image.transform, 6, 0, width - 12, height, fontSize, true); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        Button PixelSlot(Transform parent, float x, float y, float width, float height)
        {
            var button = PixelButton("Ink slot", parent, x, y, width, height, pixelTheme.slotFrame, "+", 33);
            button.GetComponentInChildren<Text>().color = pixelPink;
            PixelImage("Icon", button.transform, 10, 8, width - 20, height - 16, null, false);
            return button;
        }
        void BuildPixel()
        {
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200; canvas.pixelPerfect = true;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1672, 941); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (!EventSystem.current)
            {
                ownedEventSystem = new GameObject("Pixel inventory EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
            }
            var root = Make("Ink inventory", transform, 0, 0, 1672, 941);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f; root.anchoredPosition = Vector2.zero; window = root.gameObject;
            PixelImage("Window frame", root, 112, 56, 1448, 760, pixelTheme.windowFrame);
            PixelImage("Window footer", root, 1072, 736, 488, 122, pixelTheme.windowFooter, false);
            if (pixelTheme.titleLogo) PixelImage("Inventory title artwork", root, 164, 85, 256, 64, pixelTheme.titleLogo, false);
            else PixelLabel("잉크 정비", root, 167, 81, 650, 66, 44, true);
            var headerLine = Make("Header rule", root, 440, 130, 1000, 2).gameObject.AddComponent<Image>(); headerLine.color = pixelPink; headerLine.raycastTarget = false;
            closeButton = PixelButton("Close", root, 1470, 85, 55, 53, pixelTheme.closeIcon ? pixelTheme.closeIcon : pixelTheme.buttonFrame, pixelTheme.closeIcon ? "" : "×", 33);
            equipmentButtons = new Button[14]; weaponButtons = new Button[2]; weaponLabels = new Text[2]; pixelWeaponIcons = new Image[2];
            float[] slotX = { 325, 424, 501, 572, 642, 713, 798 }, slotW = { 85, 70, 65, 64, 65, 69, 91 };
            for (int w = 0; w < 2; w++)
            {
                float dy = w * 273;
                var card = PixelButton("Weapon " + w, root, 147, 190 + dy, 773, 258, pixelTheme.cardFrame);
                weaponButtons[w] = card; weaponLabels[w] = card.GetComponentInChildren<Text>();
                // Position labels in reference coordinates; the card button itself remains the selection target.
                Object.Destroy(weaponLabels[w].gameObject);
                weaponLabels[w] = PixelLabel("", root, 182, 207 + dy, 680, 45, 33, true);
                var rule = Make("Weapon rule", root, 168, 258 + dy, 720, 2).gameObject.AddComponent<Image>(); rule.color = pixelPink; rule.raycastTarget = false;
                PixelImage("Weapon portrait frame", root, 173, 280 + dy, 136, 135, pixelTheme.slotFrame);
                pixelWeaponIcons[w] = PixelImage("Weapon icon", root, 188, 292 + dy, 106, 110, null, false);
                PixelLabel("형태", root, 345, 281 + dy, 90, 31, 22, true);
                PixelLabel("능력치", root, 550, 281 + dy, 150, 31, 22, true);
                PixelLabel("특수", root, 820, 281 + dy, 100, 31, 22, true);
                for (int s = 0; s < 7; s++) equipmentButtons[w * 7 + s] = PixelSlot(root, slotX[s], 321 + dy, slotW[s], 88);
            }
            var divider = Make("Column divider", root, 942, 166, 2, 580).gameObject.AddComponent<Image>(); divider.color = pixelPink; divider.raycastTarget = false;
            PixelLabel("잉크 가방", root, 979, 143, 350, 54, 33, true);
            capacity = PixelLabel("", root, 1384, 148, 128, 44, 33, true); capacity.alignment = TextAnchor.MiddleRight;
            filterButtons = new Button[4]; string[] names = { "전체", "형태", "능력치", "특수" };
            float[] filterX = { 980, 1115, 1247, 1395 }, filterW = { 120, 118, 133, 122 };
            for (int i = 0; i < 4; i++) filterButtons[i] = PixelButton(names[i], root, filterX[i], 205, filterW[i], 47, pixelTheme.buttonFrame, names[i]);
            bagButtons = new Button[host.Inventory.Capacity];
            for (int i = 0; i < bagButtons.Length; i++) bagButtons[i] = PixelSlot(root, 980 + i % 5 * 109, 269 + i / 5 * 106, 96, 95);
            PixelImage("Item details", root, 968, 587, 555, 158, pixelTheme.cardFrame);
            PixelImage("Selected icon frame", root, 991, 606, 116, 116, pixelTheme.slotFrame);
            pixelItemIcon = PixelImage("Selected item icon", root, 1006, 618, 86, 92, null, false);
            pixelItemName = PixelLabel("", root, 1124, 599, 380, 43, 33, true);
            pixelItemKind = PixelLabel("", root, 1124, 641, 375, 31, 22); pixelItemKind.color = pixelPink;
            detail = PixelLabel("", root, 1124, 671, 375, 60, 22);
            pixelPreview = PixelLabel("", root, 1124, 701, 375, 39, 33, true); pixelPreview.color = pixelCyan;
            equipButton = PixelButton("Equip", root, 1088, 756, 238, 54, pixelTheme.primaryButtonFrame, "장착 [E]", 22);
            unequipButton = PixelButton("Unequip", root, 1346, 756, 178, 54, pixelTheme.buttonFrame, "해제", 22);
            exchangeButton = PixelButton("Exchange ground", root, 176, 757, 480, 45, pixelTheme.buttonFrame, "선택 잉크 ↔ 바닥 잉크 교환", 22);
            message = PixelLabel("잉크 선택 → 슬롯 선택", root, 153, 759, 810, 44, 22);
            PixelLabel("같은 잉크도 한 칸씩 사용", root, 1280, 815, 254, 33, 18);
            // A live sprite preview, not a fake picture of equipped/owned items.
            pixelPlayerPreview = PixelImage("Current character preview", root, 700, 635, 256, 256, null, false);
            pixelPlayerPreview.rectTransform.pivot = Vector2.one * .5f;
            pixelPlayerPreview.rectTransform.anchoredPosition = new Vector2(828, -763);
            BuildPixelPlayHud();
            var toast = Make("Loot notification", transform, 0, 0, 430, 126);
            toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(1, 1); toast.anchoredPosition = new Vector2(-28, -168);
            toastRoot = toast.gameObject;
            PixelImage("Toast frame", toast, 0, 0, 430, 126, pixelTheme.cardFrame);
            PixelImage("Toast icon frame", toast, 16, 18, 88, 90, pixelTheme.slotFrame);
            toastIcon = PixelImage("Icon", toast, 29, 28, 62, 70, null, false);
            pixelToastTitle = PixelLabel("잉크 획득", toast, 122, 12, 280, 28, 22); pixelToastTitle.color = pixelPink;
            toastLabel = PixelLabel("", toast, 122, 42, 280, 43, 33, true);
            pixelToastKind = PixelLabel("", toast, 122, 86, 280, 27, 22);
        }
        void PaintPixel(Button button, InkInstance item, bool chosen, bool inactive)
        {
            button.image.sprite = chosen ? pixelTheme.selectedSlotFrame : pixelTheme.slotFrame; button.image.color = Color.white;
            var label = button.GetComponentInChildren<Text>(); label.text = item == null ? "+" : inactive ? "!" : "";
            label.color = inactive ? pixelPink : pixelPink;
            var icon = button.transform.Find("Icon").GetComponent<Image>(); icon.enabled = item != null;
            if (item != null) { icon.sprite = host.ItemSprite(item); icon.color = inactive ? new Color(1, 1, 1, .4f) : host.ItemTint(item); }
        }
        static string KindName(InkKind kind) => kind == InkKind.Form ? "형태" : kind == InkKind.Stat ? "능력치" : "특수";
        void RefreshPixel()
        {
            pixelPlayHud.SetActive(!presenter.IsOpen);
            pixelPause.SetActive(presenter.Paused && !presenter.IsOpen);
            if (presenter.IsOpen)
            {
                capacity.text = host.Inventory.Used + " / " + host.Inventory.Capacity;
                for (int w = 0; w < 2; w++)
                {
                    weaponButtons[w].image.sprite = presenter.SelectedWeapon == w ? pixelTheme.selectedCardFrame : pixelTheme.cardFrame;
                    weaponButtons[w].image.color = Color.white;
                    pixelWeaponIcons[w].sprite = pixelTheme.Weapon(host.Arena.catalog.forms[host.Arena.Player.weapons[w].FormIndex].id);
                }
                for (int i = 0; i < filterButtons.Length; i++)
                {
                    bool active = !presenter.Filter.HasValue ? i == 0 : i == (int)presenter.Filter.Value + 1;
                    filterButtons[i].image.sprite = active ? pixelTheme.primaryButtonFrame : pixelTheme.buttonFrame; filterButtons[i].image.color = Color.white;
                }
                var item = presenter.Selected;
                pixelItemName.text = item?.Definition.Name ?? "잉크를 선택하세요";
                pixelItemKind.text = item == null ? "" : KindName(item.Definition.Kind);
                pixelItemIcon.enabled = item != null;
                if (item != null) { pixelItemIcon.sprite = host.ItemSprite(item); pixelItemIcon.color = host.ItemTint(item); }
                bool inactive = item != null && host.Inactive(item, presenter.SelectedWeapon);
                detail.text = item == null ? "같은 종류도 개별 보관합니다." : inactive ? host.InactiveReason(item, presenter.SelectedWeapon) + "\n형태, 기술 변경 시 재활성" : item.Definition.Description;
                detail.rectTransform.sizeDelta = new Vector2(375 / 2f, (inactive || item == null ? 60 : 28) / 2f);
                if (inactive) pixelItemKind.text += " / 비활성";
                pixelPreview.text = inactive ? "" : host.SelectedPreview();
                if (item != null && item.Definition.Kind == InkKind.Special && pixelPreview.text.Length > 10) pixelPreview.text = "특수 기술 변경";
                equipButton.image.color = Color.white; unequipButton.image.color = Color.white;
                if (presenter.SelectedBag >= 0 && item != null) equipButton.interactable &= host.Inventory.Fits(item, presenter.SelectedWeapon, presenter.SuggestedSlot);
                FadeButton(equipButton); FadeButton(unequipButton);
                message.gameObject.SetActive(!host.PendingExchange);
                if (!host.Arena.Player.EditingAllowed) message.text = host.Arena.Player.MaintenanceSite != null ? "정비소 밖 / 가방 확인만 가능합니다" : "전투 중 / 가방 확인만 가능합니다";
                else if (presenter.Message == "잉크를 선택하고 장착할 슬롯을 누르세요.") message.text = "잉크 선택 → 슬롯 선택";
                exchangeButton.image.color = Color.white;
                var sprite = host.Arena.Player.visual ? host.Arena.Player.visual.sprite : null;
                pixelPlayerPreview.enabled = sprite && sprite.texture != Texture2D.whiteTexture && sprite.rect.width >= 32;
                if (pixelPlayerPreview.enabled) { pixelPlayerPreview.sprite = sprite; pixelPlayerPreview.transform.localScale = new Vector3(host.Arena.Player.facing, 1, 1); }
            }
            var toastRect = (RectTransform)toastRoot.transform;
            toastRoot.SetActive(presenter.ToastVisible && !presenter.IsOpen && !presenter.Paused);
            toastRect.anchorMin = toastRect.anchorMax = toastRect.pivot = presenter.IsOpen ? new Vector2(.5f, 1) : new Vector2(1, 1);
            toastRect.anchoredPosition = presenter.IsOpen ? new Vector2(0, -10) : new Vector2(-28, -168);
            if (presenter.Toast != null)
            {
                toastIcon.enabled = true; toastIcon.sprite = host.ItemSprite(presenter.Toast); toastIcon.color = host.ItemTint(presenter.Toast);
                pixelToastTitle.text = "잉크 획득"; toastLabel.text = presenter.Toast.Definition.Name;
                pixelToastKind.text = KindName(presenter.Toast.Definition.Kind) + " / 가방 " + host.Inventory.Used + " / " + host.Inventory.Capacity;
            }
            else if (presenter.ToastVisible)
            {
                toastIcon.enabled = true; toastIcon.sprite = pixelTheme.bag; toastIcon.color = Color.white;
                pixelToastTitle.text = "획득 보류"; toastLabel.text = "가방 공간 부족"; pixelToastKind.text = "I / 가방에서 수동 교환";
            }
            RefreshPixelPlayHud();
        }
        static void FadeButton(Button button)
        { var group = button.GetComponent<CanvasGroup>(); if (!group) group = button.gameObject.AddComponent<CanvasGroup>(); group.alpha = button.interactable ? 1 : .4f; }
        void BuildPixelPlayHud()
        {
            var root = Make("Pixel gameplay HUD", transform, 0, 0, 1672, 941); root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f; root.anchoredPosition = Vector2.zero;
            pixelPlayHud = root.gameObject;
            PixelImage("Health frame", root, 32, 24, 390, 118, pixelTheme.cardFrame);
            PixelImage("Portrait", root, 47, 39, 90, 87, pixelTheme.portrait, false);
            pixelHpText = PixelLabel("", root, 164, 39, 245, 44, 33, true);
            PixelImage("Health track", root, 157, 87, 239, 28, pixelTheme.slotFrame);
            pixelHpFill = Make("HP fill", root, 164, 94, 224, 13).gameObject.AddComponent<Image>(); pixelHpFill.color = new Color32(244, 76, 112, 255); pixelHpFill.raycastTarget = false;
            var pauseButton = PixelButton("Pause", root, 1563, 27, 84, 65, pixelTheme.buttonFrame, "Esc", 22);
            pauseButton.onClick.AddListener(presenter.TogglePause);
            pixelQuickButtons = new Button[2]; pixelQuickIcons = new Image[2]; pixelQuickLabels = new Text[2];
            for (int w = 0; w < 2; w++)
            {
                int slot = w; float x = 35 + w * 156;
                pixelQuickButtons[w] = PixelButton("Quick weapon", root, x, 748, 132, 127, pixelTheme.slotFrame);
                pixelQuickIcons[w] = PixelImage("Quick icon", pixelQuickButtons[w].transform, 12, 8, 108, 108, null, false);
                pixelQuickButtons[w].onClick.AddListener(() => { if (host.Arena.Player.selected != slot) host.Arena.Player.QueueTestInput(swap: true); });
                PixelImage("Switch keycap", root, x + 91, 848, 40, 40, pixelTheme.buttonFrame);
                PixelLabel("Q", root, x + 91, 851, 40, 35, 22, true).alignment = TextAnchor.MiddleCenter;
                pixelQuickLabels[w] = PixelLabel("", root, x, 886, 132, 31, 22); pixelQuickLabels[w].alignment = TextAnchor.MiddleCenter;
            }
            var bagButton = PixelButton("Bag", root, 1288, 824, 355, 82, pixelTheme.cardFrame);
            bagButton.onClick.AddListener(presenter.Toggle);
            PixelImage("Bag icon", bagButton.transform, 14, 11, 57, 59, pixelTheme.bag, false);
            pixelBagLabel = PixelLabel("", bagButton.transform, 90, 11, 245, 60, 22, true);
            var pauseRoot = Make("Pixel pause", transform, 0, 0, 570, 250);
            pauseRoot.anchorMin = pauseRoot.anchorMax = pauseRoot.pivot = Vector2.one * .5f; pauseRoot.anchoredPosition = Vector2.zero; pixelPause = pauseRoot.gameObject;
            PixelImage("Pause panel", pauseRoot, 0, 0, 570, 250, pixelTheme.windowFrame);
            PixelLabel("일시정지", pauseRoot, 34, 26, 500, 50, 33, true);
            var resume = PixelButton("Resume", pauseRoot, 115, 125, 340, 65, pixelTheme.primaryButtonFrame, "계속하기 [Esc]", 22);
            resume.onClick.AddListener(presenter.TogglePause);
        }
        [SerializeField] Text pixelBagLabel;
        void RefreshPixelPlayHud()
        {
            var player = host.Arena.Player;
            pixelHpText.text = Mathf.CeilToInt(player.health) + " / " + Mathf.CeilToInt(player.maxHealth);
            pixelHpFill.rectTransform.sizeDelta = new Vector2(224 * Mathf.Clamp01(player.health / Mathf.Max(1, player.maxHealth)), 13);
            pixelBagLabel.text = "가방 " + host.Inventory.Used + " / " + host.Inventory.Capacity + "   I";
            for (int w = 0; w < 2; w++)
            {
                pixelQuickIcons[w].sprite = pixelTheme.Weapon(host.Arena.catalog.forms[player.weapons[w].FormIndex].id);
                pixelQuickLabels[w].text = host.WeaponName(w);
                pixelQuickButtons[w].image.sprite = player.selected == w ? pixelTheme.selectedSlotFrame : pixelTheme.slotFrame;
            }
        }
    }
}
