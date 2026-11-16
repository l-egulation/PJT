using System;
using Deviant.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Deviant.Inventory.Unity
{
    public sealed partial class TemporaryInventoryView : InventoryPanelView
    {
        // Assign these from a final prefab to reuse the same view/commands, or implement InventoryPanelView.
        public GameObject window;
        public Text detail, message, capacity, toastLabel;
        public Image toastIcon;
        public GameObject toastRoot;
        public Button closeButton, equipButton, unequipButton, exchangeButton;
        public Button[] bagButtons, equipmentButtons, filterButtons, weaponButtons;
        public Text[] weaponLabels;
        public Font font;
        InventoryRuntimeHost host;
        InventoryPresenter presenter;
        Canvas canvas;
        bool ownsFont;
        GameObject ownedEventSystem;
        readonly Color panel = new Color32(48, 28, 57, 250), cell = new Color32(74, 47, 81, 255);
        readonly Color cream = new Color32(255, 231, 185, 255), lilac = new Color32(176, 113, 164, 255);
        readonly Color teal = new Color32(21, 157, 165, 255), selected = new Color32(203, 153, 100, 255);
        public override void Bind(InventoryRuntimeHost runtime)
        {
            host = runtime; presenter = host.Presenter;
            pixelTheme = host.Theme;
            if (window && !pixelPlayHud) pixelTheme = null; // An independently authored legacy view uses its own widget layout.
            if (pixelTheme) font = pixelTheme.regularFont;
            if (!font)
            {
                ownsFont = true;
                font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 18);
            }
            if (!window) { if (pixelTheme) BuildPixel(); else Build(); }
            if (!EventSystem.current)
            {
                ownedEventSystem = new GameObject("Inventory EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
            }
            if (pixelTheme)
            {
                foreach (var face in new[] { pixelTheme.regularFont, pixelTheme.boldFont })
                    if (face.material && face.material.mainTexture) face.material.mainTexture.filterMode = FilterMode.Point;
                foreach (var text in GetComponentsInChildren<Text>(true)) text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            closeButton.onClick.AddListener(presenter.Close);
            equipButton.onClick.AddListener(() => { presenter.EquipSelected(); Refresh(); });
            unequipButton.onClick.AddListener(() => { presenter.UnequipSelected(); Refresh(); });
            exchangeButton.onClick.AddListener(() => { host.ExchangeSelected(); Refresh(); });
            for (int i = 0; i < bagButtons.Length; i++)
            {
                int index = i;
                bagButtons[i].onClick.AddListener(() => { int slot = BagIndex(index); if (slot >= 0) presenter.SelectBag(slot); Refresh(); });
            }
            for (int i = 0; i < equipmentButtons.Length; i++)
            {
                int index = i;
                equipmentButtons[i].onClick.AddListener(() => { presenter.SelectEquipment(index / 7, index % 7); Refresh(); });
            }
            for (int i = 0; i < filterButtons.Length; i++)
            { int index = i; filterButtons[i].onClick.AddListener(() => { presenter.SetFilter(index == 0 ? (InkKind?)null : (InkKind)(index - 1)); Refresh(); }); }
            for (int i = 0; i < weaponButtons.Length; i++)
            { int index = i; weaponButtons[i].onClick.AddListener(() => { presenter.SelectWeapon(index); Refresh(); }); }
            Refresh();
        }
        void Update()
        {
            if (presenter == null) return;
            Refresh();
        }
        int BagIndex(int visible)
        {
            var slots = host.Inventory.VisibleSlots(presenter.Filter);
            return visible < slots.Count ? slots[visible] : -1;
        }
        public override void Refresh()
        {
            if (presenter == null) return;
            window.SetActive(presenter.IsOpen);
            if (presenter.IsOpen)
            {
                capacity.text = $"잉크 가방   {host.Inventory.Used} / {host.Inventory.Capacity}";
                message.text = presenter.Message + (host.Arena.Player.EditingAllowed ? "" : host.Arena.Player.MaintenanceSite != null ? "\n지금은 가방 확인만 가능합니다. 정비소에서 장착하세요." : "\n지금은 가방 확인만 가능합니다. 전투 종료 후 장착하세요.");
                for (int i = 0; i < bagButtons.Length; i++)
                {
                    int slot = BagIndex(i); var item = slot >= 0 ? host.Inventory.BagAt(slot) : null;
                    Paint(bagButtons[i], item, slot >= 0 && presenter.SelectedBag == slot, false);
                    bagButtons[i].interactable = slot >= 0;
                }
                for (int i = 0; i < equipmentButtons.Length; i++)
                {
                    int w = i / 7, s = i % 7; var item = host.Inventory.EquippedAt(w, s);
                    Paint(equipmentButtons[i], item, presenter.SelectedEquipmentSlot == s && presenter.SelectedWeapon == w, host.Inactive(item, w));
                }
                for (int w = 0; w < weaponLabels.Length; w++)
                {
                    var weapon = host.Arena.Player.weapons[w];
                    weaponLabels[w].text = pixelTheme ? $"무기 {w + 1} / {host.WeaponName(w)}" : $"무기 {w + 1} / {host.WeaponName(w)}\n기본 피해 +{weapon.BasicBonus * 100:0}%   공속 ×{weapon.Speed:0.0}";
                    weaponButtons[w].image.color = presenter.SelectedWeapon == w ? teal : cell;
                }
                for (int i = 0; i < filterButtons.Length; i++) filterButtons[i].image.color =
                    (!presenter.Filter.HasValue ? i == 0 : i == (int)presenter.Filter.Value + 1) ? teal : cell;
                var picked = presenter.Selected;
                detail.text = picked == null ? "잉크를 선택하세요.\n같은 종류도 각각 한 칸을 사용합니다." :
                    picked.Definition.Name + "\n" + picked.Definition.Description +
                    (presenter.SelectedEquipmentSlot >= 0 && host.Inactive(picked, presenter.SelectedWeapon) ? "\n비활성: " + host.InactiveReason(picked, presenter.SelectedWeapon) + "\n호환 형태, 기술에서 다시 활성화됩니다." : "");
                equipButton.interactable = picked != null && presenter.SelectedBag >= 0 && host.Arena.Player.EditingAllowed;
                unequipButton.gameObject.SetActive(false); // Equipped ink is permanent; there is no unequip.
                exchangeButton.gameObject.SetActive(host.PendingExchange);
                exchangeButton.interactable = picked != null && presenter.SelectedBag >= 0;
            }
            var toastRect = (RectTransform)toastRoot.transform;
            toastRect.anchorMin = toastRect.anchorMax = toastRect.pivot = presenter.IsOpen ? new Vector2(.5f, 1) : new Vector2(1, 1);
            toastRect.anchoredPosition = presenter.IsOpen ? new Vector2(0, -18) : new Vector2(-24, -160);
            toastRoot.SetActive(presenter.ToastVisible);
            if (presenter.Toast != null)
            {
                toastIcon.enabled = true;
                var item = presenter.Toast.Definition; var art = host.Content.Find(item.Id);
                toastLabel.text = "잉크 획득\n" + item.Name;
                toastIcon.sprite = art.icon ? art.icon : host.FallbackIcon; toastIcon.color = art.tint;
            }
            else if (presenter.ToastVisible) { toastLabel.text = presenter.ToastMessage; toastIcon.enabled = false; }
            if (pixelTheme) RefreshPixel();
        }
        void Paint(Button button, InkInstance item, bool chosen, bool inactive)
        {
            if (pixelTheme) { PaintPixel(button, item, chosen, inactive); return; }
            button.image.color = chosen ? selected : cell;
            var label = button.GetComponentInChildren<Text>();
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            label.text = item == null ? "+" : (inactive ? "비활성" : Short(item.Definition));
            icon.enabled = item != null;
            if (item != null) { var art = host.Content.Find(item.Definition.Id); icon.sprite = art.icon ? art.icon : host.FallbackIcon; icon.color = inactive ? Color.gray : art.tint; }
        }
        static string Short(InkDefinition item) => item.Kind == InkKind.Form ? "형태" : item.Kind == InkKind.Special ? "특수" :
            item.Value == 1 ? "피해" : item.Value == 2 ? "특수피해" : item.Value == 3 ? "공속" : "쿨감";
        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (!EventSystem.current)
            {
                ownedEventSystem = new GameObject("Inventory EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
            }
            var center = Make("Ink inventory", transform, 0, 0, 1510, 740);
            center.anchorMin = center.anchorMax = center.pivot = Vector2.one * .5f; center.anchoredPosition = Vector2.zero;
            window = center.gameObject; Panel(center, panel); Border(center);
            Label("잉크 정비", center, 28, 16, 800, 54, 32);
            closeButton = ButtonAt("×", center, 1430, 18, 54, 48);
            equipmentButtons = new Button[14]; weaponButtons = new Button[2]; weaponLabels = new Text[2];
            for (int w = 0; w < 2; w++)
            {
                float y = 100 + w * 250;
                var card = Make("Weapon " + w, center, 25, y, 770, 230); Panel(card, cell * new Color(1, 1, 1, .6f)); Border(card);
                weaponButtons[w] = ButtonAt("", card, 15, 14, 735, 63);
                weaponLabels[w] = weaponButtons[w].GetComponentInChildren<Text>(); weaponLabels[w].alignment = TextAnchor.MiddleLeft;
                Label("무기", card, 17, 90, 70, 60, 18);
                Label("형태", card, 95, 88, 80, 28, 17); Label("능력치", card, 205, 88, 420, 28, 17); Label("특수", card, 665, 88, 70, 28, 17);
                for (int s = 0; s < 7; s++)
                {
                    float x = s == 0 ? 95 : s == 6 ? 665 : 205 + (s - 1) * 84;
                    equipmentButtons[w * 7 + s] = Slot(card, x, 122, 72);
                }
            }
            capacity = Label("", center, 835, 85, 600, 42, 25);
            filterButtons = new Button[4]; string[] filters = { "전체", "형태", "능력치", "특수" };
            for (int i = 0; i < 4; i++) filterButtons[i] = ButtonAt(filters[i], center, 835 + i * 156, 138, 142, 40);
            bagButtons = new Button[host.Inventory.Capacity];
            for (int i = 0; i < bagButtons.Length; i++) bagButtons[i] = Slot(center, 835 + (i % 5) * 125, 192 + (i / 5) * 99, 88);
            var details = Make("Selected ink", center, 835, 497, 630, 112); Panel(details, cell); Border(details);
            detail = Label("", details, 12, 6, 606, 100, 19);
            equipButton = ButtonAt("선택 무기에 장착 [E]", center, 835, 622, 300, 44); equipButton.image.color = teal;
            unequipButton = ButtonAt("장착 해제", center, 1155, 622, 310, 44);
            exchangeButton = ButtonAt("선택 잉크 ↔ 바닥 잉크 교환", center, 835, 678, 630, 42);
            message = Label("", center, 28, 612, 760, 108, 17);
            // Notification lives outside the window and remains visible while inventory is closed.
            var toast = Make("Loot notification", transform, 0, 0, 310, 90);
            toast.anchorMin = toast.anchorMax = new Vector2(1, 1); toast.pivot = new Vector2(1, 1); toast.anchoredPosition = new Vector2(-24, -108);
            toastRoot = toast.gameObject; Panel(toast, panel); Border(toast);
            toastIcon = Make("Icon", toast, 12, 17, 50, 50).gameObject.AddComponent<Image>(); toastIcon.preserveAspect = true;
            toastLabel = Label("", toast, 75, 8, 220, 76, 20);
        }
        RectTransform Make(string name, Transform parent, float x, float y, float width, float height)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); return r;
        }
        void Panel(RectTransform at, Color color) { var image = at.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; }
        void Border(RectTransform at) { var border = at.gameObject.AddComponent<Outline>(); border.effectColor = lilac; border.effectDistance = new Vector2(2, -2); }
        Text Label(string value, Transform parent, float x, float y, float w, float h, int size)
        {
            if (pixelTheme) return PixelLabel(value, parent, x, y, w, h, size);
            var text = Make("Label", parent, x, y, w, h).gameObject.AddComponent<Text>(); text.text = value; text.font = font;
            text.fontSize = size; text.color = cream; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; return text;
        }
        Button ButtonAt(string text, Transform parent, float x, float y, float w, float h)
        {
            var at = Make(text.Length == 0 ? "Button" : text, parent, x, y, w, h); var image = at.gameObject.AddComponent<Image>(); image.color = cell;
            var button = at.gameObject.AddComponent<Button>(); button.targetGraphic = image; Border(at);
            Label(text, at, 4, 0, w - 8, h, 18).alignment = TextAnchor.MiddleCenter; return button;
        }
        Button Slot(Transform parent, float x, float y, float size)
        {
            var button = ButtonAt("+", parent, x, y, size, size);
            var label = button.GetComponentInChildren<Text>(); label.rectTransform.anchoredPosition = new Vector2(2, -(size - 23)); label.rectTransform.sizeDelta = new Vector2(size - 4, 22); label.fontSize = 13;
            var icon = Make("Icon", button.transform, 8, 4, size - 16, size - 26).gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
            return button;
        }
        void OnDisable() { presenter?.Close(); }
        void OnDestroy() { if (ownsFont && font) Destroy(font); }
    }
}
