using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Deviant.Control.Contracts;
using Deviant.UI.Presentation;
using Deviant.UI.Contracts;
using Deviant.UI.Unity;
using UnityEngine.EventSystems;
namespace Deviant.UI
{
    [DefaultExecutionOrder(-150)]
    public class BattleHudView : MonoBehaviour
    {
        public MonoBehaviour dataSource;
        public TMP_Text hpText,expText,levelText,coinText,gemText,weaponText,capacityText,bagText,regionText;
        public PreservedGauge hpGauge,expGauge;
        public UnityEngine.UI.Button pauseButton,escapeButton,bagButton,mixButton,closeButton;
        public UnityEngine.UI.Image[] inserts,drops;
        public Sprite[] insertRed,insertBlue;
        public Sprite dropRed,dropBlue;
        public TMP_FontAsset koreanHudFont;
        public GameObject dialog;
        public TMP_Text dialogTitle,dialogBody;
        public RectTransform loadoutGroup,inventoryGroup;
        public UnityEvent inventoryRequested=new UnityEvent(),pauseChanged=new UnityEvent();
        public bool Paused => presenter != null && presenter.Paused;
        public int BagRequests {get;private set;}
        IControlRequests controls;
        ScreenRouter screens;
        InputAction back;
        EventSystem events;
        ScreenView screenView;
        ScreenInputBinding screenInput;
        int consumedFrame = -1;
        bool pointerCaptured, renderedPause;
        HudDialog renderedDialog;
        HudControlPresenter presenter;
        IBattleHudDataSource bound;
        System.Action openInventory, closeInventory;
        System.Func<bool> inventoryIsOpen;
        System.Func<bool> externalEscapeOwner;
        public void BindInventory(System.Action open, System.Func<bool> isOpen, System.Action close, System.Func<bool> ownsEscape = null)
        { openInventory = open; inventoryIsOpen = isOpen; closeInventory = close; externalEscapeOwner = ownsEscape; RefreshBackOwner(); }
        void RefreshBackOwner()
        {
            if (screenInput) screenInput.enabled = !(externalEscapeOwner?.Invoke() == true || inventoryIsOpen?.Invoke() == true);
        }
        void Awake()
        {
            // Explicitly pair the atlas material with the font; old prefabs retain legacy presets.
            if (!koreanHudFont) koreanHudFont = Resources.Load<TMP_FontAsset>("UI/KoreanHUD");
            if (koreanHudFont)
                foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                { text.font = koreanHudFont; text.fontSharedMaterial = koreanHudFont.material; }
            // Region names can be longer than the original six-character layout example.
            regionText.enableAutoSizing = true; regionText.fontSizeMin = 12;
            regionText.textWrappingMode = TextWrappingModes.NoWrap;
            regionText.overflowMode = TextOverflowModes.Ellipsis;
            bound=dataSource as IBattleHudDataSource;
            pauseButton.onClick.AddListener(TogglePause);escapeButton.onClick.AddListener(TogglePause);
            bagButton.onClick.AddListener(OpenBag);closeButton.onClick.AddListener(CloseDialog);
            // Retired mixing/currency/capacity widgets remain serialized artwork only.
            mixButton.gameObject.SetActive(false);
            coinText.gameObject.SetActive(false);gemText.gameObject.SetActive(false);
            capacityText.gameObject.SetActive(false);
            foreach(var image in inserts)image.enabled=false;
            foreach(var image in drops)image.enabled=false;
            dialog.SetActive(false);Refresh();
        }
        public void Bind(IBattleHudDataSource source){bound=source;Refresh();}
        public void BindControls(IControlRequests requests, ScreenRouter router, InputAction backAction, EventSystem eventSystem)
        {
            if (requests == null || router == null || backAction == null || backAction.type != InputActionType.Button || !eventSystem)
                throw new System.ArgumentException("Explicit HUD controls, shared router, existing Back Button action and EventSystem are required.");
            ReleaseControls();
            controls = requests; screens = router; back = backAction; events = eventSystem;
            try { if (isActiveAndEnabled) ConnectControls(); }
            catch { ReleaseControls(); controls = null; screens = null; back = null; events = null; throw; }
        }
        public void UnbindControls(IControlRequests expected, ScreenRouter expectedRouter)
        {
            if (!ReferenceEquals(controls, expected) || !ReferenceEquals(screens, expectedRouter)) return;
            ReleaseControls(); controls = null; screens = null; back = null; events = null;
        }
        void ConnectControls()
        {
            if (presenter != null || controls == null) return;
            var group = dialog.GetComponent<CanvasGroup>();
            if (!group) group = dialog.AddComponent<CanvasGroup>();
            if (!screenView) screenView = gameObject.AddComponent<ScreenView>();
            if (!screenInput) screenInput = gameObject.AddComponent<ScreenInputBinding>();
            screenView.Configure(dialog, group, events, closeButton, null, true, true);
            presenter = new HudControlPresenter(controls, screens, screenView);
            screenInput.BackProcessed += BackProcessed;
            screenInput.Configure(screens, back);
            RefreshBackOwner();
            RenderDialog();
        }
        void ReleaseControls()
        {
            if (screenInput) { screenInput.Unbind(); screenInput.BackProcessed -= BackProcessed; }
            presenter?.Dispose(); presenter = null;
            consumedFrame = -1; pointerCaptured = false;
            renderedDialog = HudDialog.None; renderedPause = false;
            if (dialog) dialog.SetActive(false);
        }
        void OnEnable() { if (controls != null) ConnectControls(); }
        void Update()
        {
            RefreshBackOwner();
            screens?.Poll();
            if (presenter != null)
            {
                var mouse = Mouse.current;
                bool over = mouse != null && PointerOverButtons(mouse.position.ReadValue());
                if (mouse != null && mouse.leftButton.isPressed && over) pointerCaptured = true;
                if (mouse != null && mouse.leftButton.wasReleasedThisFrame && pointerCaptured)
                { consumedFrame = Time.frameCount; pointerCaptured = false; }
                presenter.SetPointerBlocked(consumedFrame == Time.frameCount || mouse != null && mouse.leftButton.isPressed && (over || pointerCaptured));
                if (renderedDialog != presenter.Dialog || renderedPause != Paused) RenderDialog();
            }
            Refresh();
        }
        bool PointerOverButtons(Vector2 position)
        {
            var visibility = GetComponent<CanvasGroup>();
            return (!visibility || visibility.alpha > 0 && visibility.blocksRaycasts) &&
                (Over(pauseButton, position) || Over(escapeButton, position) || Over(bagButton, position) || Over(mixButton, position) || Over(closeButton, position));
        }
        static bool Over(UnityEngine.UI.Button button, Vector2 position) => button && button.gameObject.activeInHierarchy &&
            button.isActiveAndEnabled && button.IsInteractable() && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, position);
        void ConsumeFrame()
        {
            consumedFrame = Time.frameCount;
            presenter.SetPointerBlocked(true);
        }
        void BackProcessed(ScreenBackResult result)
        {
            if (presenter == null || !isActiveAndEnabled) return;
            ConsumeFrame();
            presenter.BackProcessed(result);
            RenderDialog();
        }
        public void Refresh()
        {
            if(bound==null)bound=dataSource as IBattleHudDataSource;
            if(bound==null)return;var s=bound.Read();
            hpText.text=$"{s.hp} / {s.maxHP}";expText.text=$"{s.exp} / {s.maxEXP}";
            levelText.text=$"Lv.{s.level}";
            weaponText.text=s.weaponName;
            bagText.text=$"가방 {s.bagUsed} / {s.bagCapacity}";regionText.text=s.region;
            hpGauge.Set(s.hp,s.maxHP);expGauge.Set(s.exp,s.maxEXP);
            // Keep all approved artwork at native proportions. On narrow canvases, move the bag group above the loadout.
            var canvas=(RectTransform)transform;
            inventoryGroup.anchoredPosition=new Vector2(-20,canvas.rect.width<1400?163:27);
        }
        public void TogglePause()
        {
            if (presenter == null || !isActiveAndEnabled) return;
            ConsumeFrame(); presenter.TogglePause(); RenderDialog();
        }
        public void OpenBag()
        {
            if (presenter == null || !isActiveAndEnabled) return;
            if (openInventory != null) { ConsumeFrame(); BagRequests++; openInventory(); inventoryRequested.Invoke(); return; }
            ConsumeFrame(); BagRequests++; presenter.OpenBag(); RenderDialog(); inventoryRequested.Invoke();
        }
        void RenderDialog()
        {
            bool paused = Paused;
            if (renderedPause != paused) { renderedPause = paused; pauseChanged.Invoke(); }
            renderedDialog = presenter == null ? HudDialog.None : presenter.Dialog;
            if (presenter == null || presenter.Dialog == HudDialog.None) return;
            if (presenter.Dialog == HudDialog.Pause) ShowDialog("일시정지","닫기를 선택하면 계속합니다.");
            else
            {
                var s=bound.Read();
                ShowDialog("가방",$"가방 {s.bagUsed} / {s.bagCapacity}\n데모 데이터 / 상세 인벤토리 연결 대기");
            }
        }
        void ShowDialog(string title,string body){dialogTitle.text=title;dialogBody.text=body;}
        public void CloseDialog()
        {
            if (inventoryIsOpen != null && inventoryIsOpen()) { closeInventory?.Invoke(); return; }
            if (presenter == null || !isActiveAndEnabled) return;
            ConsumeFrame(); presenter.CloseDialog(); RenderDialog();
        }
        void OnDisable()
        {
            ReleaseControls();
        }
    }
}
