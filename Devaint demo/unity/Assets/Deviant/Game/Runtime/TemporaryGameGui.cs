using System;
using System.Collections.Generic;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using Deviant.UI.Unity;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deviant.Game
{
    // One device coordinator; domain presenters retain ownership of their pause/input leases.
    [DefaultExecutionOrder(-200)]
    public sealed partial class TemporaryGameGui : InventoryPanelView
    {
        // Warm ink tones that match the paper world (art review H-3); teal stays the player's selection colour.
        static readonly Color Ink = new Color32(40,32,26,255), Cell = new Color32(30,24,19,255),
            Cream = PaperGuiSkin.Text, Muted = PaperGuiSkin.Muted,
            Teal = PaperGuiSkin.Accent, Red = new Color32(190,87,83,255);
        InventoryRuntimeHost host;
        InventoryPresenter Presenter => host.Presenter;
        ForestCombatWorld world;
        Chapter1RunBinding run;
        CommonMaintenanceShop shop;
        Deviant.Chapter2.Chapter2World chapter2;
        Chapter1MaintenanceFacility facility;
        Chapter1StartingBlueprints blueprints;
        Chapter1BossEncounter boss;
        Chapter1SealChallenge sealChallenge;
        Chapter1KeyChest keyChest;
        ClockExecutionerEncounter clockRoom;
        Chapter1FullMap map;
        Chapter1Rewards rewards;
        Chapter1MapSelector developerMap;
        Chapter1ManualCheckpoint checkpoint;
        Deviant.Combat.CombatFeelFeedback feedback;
        Chapter1TitleScreen oldTitle;
        Font font,boldFont;
        RectTransform stage;
        GameObject dim, hud, bag, shopPanel, mapPanel, menu;
        GameObject ownedEvents;
        ScreenRouter router;
        IScreenHandle focused;
        GameObject focusedPanel;
        readonly Dictionary<GameObject, ScreenView> screens = new Dictionary<GameObject, ScreenView>();
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        IControlScope pointerScope;
        bool built, developerToolsResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterTitle()
        { SceneManager.sceneLoaded -= TitleLoaded; SceneManager.sceneLoaded += TitleLoaded; }
        static void TitleLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var title = root.GetComponentInChildren<Chapter1TitleScreen>();
                if (!title) continue;
                var gui = new GameObject("Temporary title GUI", typeof(RectTransform)).AddComponent<TemporaryGameGui>();
                gui.oldTitle = title; title.ExternalPresentation = true;
                gui.BuildRoot(); gui.InitJournal(); gui.BuildTitleBackdrop(); gui.BuildMenu(); gui.built = true;
                gui.BuildApprovedSettings();
                gui.wellTitle.UseSharedSettings(gui.approvedSettings,gui.OpenApprovedSettings,gui.CloseApprovedSettings);
                gui.RefreshTitle(); if (Chapter1RunHost.Ensure().ChapterTwoPending) EventSystem.current.SetSelectedGameObject(gui.menuPrimary.gameObject); else gui.wellTitle.FocusInitial(); break;
            }
        }

        public override void Bind(InventoryRuntimeHost runtime)
        {
            host = runtime;chapter2=FindAnyObjectByType<Deviant.Chapter2.Chapter2World>();if(chapter2&&chapter2.gameObject.scene!=runtime.gameObject.scene)chapter2=null;
            foreach (var candidate in FindObjectsByType<ForestCombatWorld>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == runtime.gameObject.scene) { world = candidate; break; }
            host.GetComponent<InventoryKeyboardInput>().ExternalInput = true;
            BuildRoot(); router = new ScreenRouter(host.Arena.Controls); InitJournal();
            BuildHud(); BuildBag(); BuildShop(); BuildMap(); BuildAltar(); BuildAugment(); BuildMenu(); BuildRunPalette();
            DiscoverChapter(); built = true; Refresh();
        }

        void BuildRoot()
        {
            font = Resources.Load<Font>("PixelInventory/Fonts/Galmuri11");
            boldFont = Resources.Load<Font>("PixelInventory/Fonts/Galmuri11-Bold");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font.material && font.material.mainTexture) font.material.mainTexture.filterMode = FilterMode.Point;
            if (boldFont && boldFont.material && boldFont.material.mainTexture) boldFont.material.mainTexture.filterMode = FilterMode.Point;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100; canvas.pixelPerfect = true;
            scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; FitToWorld();
            gameObject.AddComponent<GraphicRaycaster>();
            if (!EventSystem.current)
            {
                ownedEvents = new GameObject("Temporary GUI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEvents.transform.SetParent(transform,false);
            }
            var shade = new GameObject("Modal shade", typeof(RectTransform),typeof(Image)); shade.transform.SetParent(transform,false);
            var sr = (RectTransform)shade.transform; sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0,0,0,.35f); dim = shade; dim.SetActive(false);
            stage = Rect("Safe 960x540 canvas", transform,0,0,960,540); stage.anchorMin = stage.anchorMax = stage.pivot = Vector2.one*.5f; stage.anchoredPosition = Vector2.zero;
        }

        void DiscoverChapter()
        {
            if(chapter2){DiscoverChapterTwo();return;}
            if (!world) return;
            run = world.GetComponent<Chapter1RunBinding>(); shop = CommonMaintenanceShop.For(world.GetComponent<Chapter1MaintenanceShop>());
            facility = world.GetComponent<Chapter1MaintenanceFacility>(); blueprints = world.GetComponent<Chapter1StartingBlueprints>();
            boss = world.GetComponent<Chapter1BossEncounter>(); clockRoom = world.GetComponent<ClockExecutionerEncounter>(); map = world.FullMap; rewards = world.GetComponent<Chapter1Rewards>(); augments = world.GetComponent<AugmentRewardChoice>();
            sealChallenge = world.GetComponent<Chapter1SealChallenge>(); keyChest = world.GetComponent<Chapter1KeyChest>();
            world.ExternalPresentation = true;
            if (world.diagnostics) world.diagnostics.ExternalHud = true;
            if (run) run.ExternalPresentation = true;
            if (shop) { shop.ExternalPresentation = true; shop.ExternalInput = true; }
            if (facility) facility.ExternalPresentation = true;
            if (blueprints) blueprints.ExternalPresentation = true;
            if (boss) boss.ExternalPresentation = true;
            if (rewards) rewards.ExternalPresentation = true;
            if (augments) augments.ExternalPresentation = true;
            if (map) { map.ExternalPresentation = true; map.ExternalInput = true; }
            if(!developerToolsResolved&&world.Ready){developerMap=FindAnyObjectByType<Chapter1MapSelector>();developerToolsResolved=true;}
            if(!checkpoint)checkpoint=world.GetComponent<Chapter1ManualCheckpoint>();
            if(!feedback&&host&&host.Arena)feedback=host.Arena.GetComponent<Deviant.Combat.CombatFeelFeedback>();
            if(developerMap)developerMap.ExternalPresentation=true;
            if(checkpoint)checkpoint.ExternalPresentation=true;
            if(feedback)feedback.ExternalHud=true;
        }

        CanvasScaler scaler;
        // The 960x540 HUD uses the world picture's own frame (VillagePixelViewport: whole-number enlargement,
        // centred), so it never spreads past the picture onto the letterbox when the window is not an exact multiple.
        // Measured on what the canvas draws into: the screen, or a capture camera's render texture. Public so a
        // capture that retargets the canvas can refit before rendering, as the scaler only applies it in Update.
        public void FitToWorld()
        {
            var canvas = GetComponent<Canvas>(); var target = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera ? canvas.worldCamera.targetTexture : null;
            var frame = Stage1VillageExperiment.VillagePixelViewport.CalculateDisplayRect(target ? target.width : Screen.width, target ? target.height : Screen.height);
            if (!scaler || frame.width <= 0) return;
            scaler.scaleFactor = frame.width / 960f; if (canvas) canvas.scaleFactor = scaler.scaleFactor;
            FitApprovedMenus(target?target.width:Screen.width,target?target.height:Screen.height);
            if (wellTitle) wellTitle.FitToCanvas();
        }

        void Update()
        {
            if (!built) return;
            FitToWorld();
            if (GuardLoadingUi()) return;
            TickShortcutEdit();
            SyncGuiInput();
            TickUiEffects();
            if (village) { HubUpdate(); return; }
            if (!host)
            {
                if(shortcutPanel){shortcutPanel.SetActive(shortcutPanelOpen);if(shortcutPanelOpen)shortcutPanel.transform.SetAsLastSibling();}
                if (wellTitle && (wellTitle.SettingsOpen||shortcutPanelOpen) && settingsInputFrame!=Time.frameCount && Controls && Controls.TakeBack()) Back();
                RefreshTitle();
                return;
            }
            DiscoverChapter(); GuardPointer(); TickAltarClose();
            if(chapter2&&chapter2.ShowingResult){Presenter.Close();if(Presenter.Paused)Presenter.TogglePause();if(shop)shop.Close();if(map)map.SetOpen(false);Refresh();return;}
            TickControls();
            if (run && run.ShowingResult)
            {
                HideControls(); CloseControlsWindow(); Presenter.Close(); if (Presenter.Paused) Presenter.TogglePause();
                if (shop) shop.Close(); if (map) map.SetOpen(false); Refresh(); return;
            }
            if (MaintenanceRoomPresentation.IsCapturing(host.Arena)) { Refresh(); return; }
            if (SketchBinding && SketchBinding.FirstRunTutorialShowing) { Refresh(); return; }
            if(systemTutorialReplay && systemTutorialReplay.IsOpen)
            {if(host.Arena.Player.Dead)systemTutorialReplay.Dismiss();Refresh();return;}
            // The first-run notebook owns every key while it is open (Back turns its page instead of pausing).
            RefreshFirstRunNotebook();
            if (firstRunNotebook || departureCardScope != null) { Refresh(); return; }
            // The signpost's window of moves owns every key while it is open.
            if (ControlsWindowOpen) { if (host.Arena.Player.Dead) CloseControlsWindow(); else TickControlsWindow(); Refresh(); return; }
            var sketchModal = run && run.Sketch ? run.Sketch : chapter2 ? chapter2.GetComponent<SketchRunBinding>() : null;
            if (sketchModal && sketchModal.RewardModalShown) { Refresh(); return; }
            var input = Controls;
            if (input && !host.Arena.Player.Dead)
            {
                if (TakeBack()) Back();
                else if (augments && augments.IsOpen) { Refresh(); return; }
                else if (shop && shop.IsOpen) { if (input.Take(GameAction.Interact)) shop.Close(); }
                else if (Altar && Altar.IsOpen) { if (input.Take(GameAction.Interact)) { Altar.Close(); Refresh(); } }
                else if (map && map.IsOpen)
                {
                    if (input.Take(GameAction.Map)) { input.SuppressGameplay(); map.SetOpen(false); }
                    else if (input.Take(GameAction.MapFit)) FitMapExplored();
                    else if (input.Take(GameAction.MapFocus)) FocusMap();
                    else if (input.Take(GameAction.MapZoomIn)) ZoomMap(1.25f);
                    else if (input.Take(GameAction.MapZoomOut)) ZoomMap(.8f);
                    ScrollMapLegend();
                    var pan = input.Read(GameAction.MapPan);
                    if (map.Viewport != null) map.Viewport.Pan(new Vector2(-pan.X, pan.Y) * (320 * Time.unscaledDeltaTime), MapSize);
                }
                else if (Presenter.IsOpen)
                { if (input.Take(GameAction.Inventory)) { input.SuppressGameplay(); Presenter.Close(); } else if (input.Take(GameAction.Equip)) EquipBagSelected(); }
                else if (!Presenter.Paused)
                {
                    // Sketch runs open the same bag with Tab; an open reward choice keeps Tab for its comparison bag and has
                    // already returned above (sketchModal.RewardModalShown).
                    if (input.Take(GameAction.Inventory)) { input.SuppressGameplay(); Presenter.Open(); }
                    else if (KeyHelpAvailable && input.Take(GameAction.Help)) { KeyHelpShown = !KeyHelpShown; keyHelp.SetActive(KeyHelpShown); }
                    else if (input.Take(GameAction.Map) && map) { input.SuppressGameplay(); map.SetOpen(true); }
                    // The signpost's strip of moves closes with F wherever the hero stands.
                    else if (ControlsShowing && input.Take(GameAction.Interact)) { input.SuppressGameplay(); HideControls(); }
                    else if (input.Pressed(GameAction.Interact))
                    {
                        bool handled = Altar && Altar.TryOpen() || chapter2 && chapter2.TryUseExit() || TryShelfInteract() || shop && shop.TryOpen();
                        if (handled) { input.Take(GameAction.Interact); input.SuppressGameplay(); Refresh(); }
                    }
                }
            }
        }

        // Loading owns input, including fixed menu actions that bypass arena combat gates.
        bool GuardLoadingUi()
        {
            if (!ChapterSceneFade.Busy) return false;
            var input = Controls;
            if (input)
            {
                input.Poll(); input.SuppressGameplay(); input.TakeBack();
                input.Take(GameAction.Inventory); input.Take(GameAction.Map); input.Take(GameAction.Help);
                input.Take(GameAction.Submit); input.Take(GameAction.Advance); input.Take(GameAction.Interact);
                input.Take(GameAction.Equip); input.Take(GameAction.SwapWeaponOrder);
            }
            if (inputUI) inputUI.SetPanel(null);
            return true;
        }

        void GuardPointer()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) { pointerScope?.Dispose(); pointerScope = null; return; }
            if (!mouse.leftButton.wasPressedThisFrame || !EventSystem.current) return;
            hits.Clear(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() }, hits);
            if (hits.Count == 0) return;
            pointerScope?.Dispose();
            IControlRequests controls = host ? host.Arena.Controls : village ? village.World.arena.Controls : null;
            if (controls == null) return;
            pointerScope = controls.OpenScope("temporary GUI pointer press");
            pointerScope.BlockInput(InputChannels.Combat | InputChannels.Interaction);
        }
        public void Back()
        {
            Controls?.SuppressGameplay();
            if(shortcutEditClosedFrame==Time.frameCount)return;
            if(editingBinding>=0){CancelShortcutEdit();return;}
            if (settingsDraftQuestion) { CloseSettingsDraftQuestion(); return; }
            if (settingsDraftClosedFrame == Time.frameCount) return;
            if (shortcutResetQuestion) { CloseShortcutResetQuestion(); return; }
            if (shortcutResetClosedFrame == Time.frameCount) return;
            if (abandonRunQuestion) { CloseAbandonRunQuestion(); return; }
            if (abandonRunClosedFrame == Time.frameCount) return;
            if(shortcutsInSettings&&shortcutPanelOpen){CloseApprovedSettings();return;}
            if (shortcutPanelOpen)
            {
                CloseShortcuts();
                PresentationSfx.PlaySelected("sfx_ui_cancel");
                if(oldTitle){shortcutPanel.SetActive(false);OpenApprovedSettings();return;}
                if (village) RefreshHub(); else Refresh();
                return;
            }
            if(approvedSettings&&approvedSettings.activeInHierarchy){CloseApprovedSettings();return;}
            if (village) { HubBack(); return; }
            if (!host || run && run.ShowingResult || chapter2&&chapter2.ShowingResult) return;
            var book=shop&&shop.IsOpen?nativeShop:Presenter.IsOpen?nativeBag:null;
            if(book!=null&&(book.Selected!=Guid.Empty||book.Action!=BookAction.Inspect)){NativeCancel(book);book.Key=null;PresentationSfx.PlaySelected("sfx_ui_cancel");Refresh();return;}
            if (shop && shop.IsOpen && growthOpen) {growthOpen=false;}
            else if (shop && shop.IsOpen) shop.Close();
            else if (Altar && Altar.IsOpen) Altar.Close();
            else if (augments && augments.IsOpen) return;
            else if (map && map.IsOpen) map.SetOpen(false);
            else if (Presenter.IsOpen) Presenter.Close();
            else { Presenter.TogglePause(); Refresh(); return; }
            PresentationSfx.PlaySelected("sfx_ui_cancel");
            Refresh();
        }

        bool audioBagOpen, audioPaused;

        public override void Refresh()
        {
            if (!built || !host || !host.Arena || !host.Arena.Player) return;
            if (Presenter.IsOpen && !audioBagOpen) PresentationSfx.PlaySelected("sfx_ui_inventory_open");
            if (!Presenter.Paused && audioPaused && !host.Arena.Player.Dead && !(run && run.ShowingResult) && !(chapter2 && chapter2.ShowingResult))
                PresentationSfx.PlaySelected("sfx_ui_resume");
            audioBagOpen = Presenter.IsOpen;
            audioPaused = Presenter.Paused;
            if(!shop || !shop.IsOpen)growthOpen=false;
            RefreshHud(); RefreshBag(); RefreshShop(); RefreshMap(); RefreshAltar(); RefreshAugment(); RefreshMenu(); RefreshRunPalette();
            RefreshSketchRewards();
            var active = chapter2&&chapter2.ShowingResult ? (chapter2.Complete && demoCompletion ? demoCompletion.gameObject : menu) : run && run.ShowingResult ? menu : ApprovedShopConfiguring ? bag : shop && shop.IsOpen ? (growthOpen?growthPanel:shopPanel) : Altar && Altar.IsOpen ? altarPanel : augments && augments.IsOpen ? augmentPanel : map && map.IsOpen ? mapPanel : Presenter.IsOpen ? bag : Presenter.Paused ? (shortcutPanelOpen && !shortcutsInSettings ? shortcutPanel : menu) : SketchRewardShowing ? sketchRewardPanel.gameObject : null;
            bool sharedReward = SketchBinding && SketchBinding.ShowingRewardChoice;
            dim.SetActive(active || sharedReward); hud.SetActive(!active&&!sharedReward&&!MaintenanceRoomPresentation.IsCapturing(host.Arena)&&!(chapter2&&chapter2.Boss&&chapter2.Boss.InBossCutscene));
            RefreshInkGuideBubble(hud.activeSelf&&!Presenter.Paused);
            RefreshDropHover(hud.activeSelf&&!Presenter.Paused);
            // The journal screens sit on the design's warm veil, the pause menu on its darker one.
            dim.GetComponent<Image>().color = active == menu && !(run && run.ShowingResult) && !(chapter2 && chapter2.ShowingResult) ? PauseVeil : active || sharedReward ? Veil : new Color(0,0,0,.35f);
            if (demoCompletion && active == demoCompletion.gameObject) dim.GetComponent<Image>().color = new Color(0, 0, 0, .65f);
            shopGoldChip.SetActive(active == shopPanel || active == growthPanel);
            FocusPanel(active);
            RefreshScreenBackdrop();
            RevealVisibleResult(active);
        }

        void RevealVisibleResult(GameObject active)
        {
            bool resultVisible = active == menu || demoCompletion && active == demoCompletion.gameObject;
            if (resultVisible && (run && run.ShowingResult || chapter2 && chapter2.ShowingResult))
                host.Arena.GetComponent<Deviant.Integration.PlayerDeathPresentation>()?.RevealResult();
        }
        void FocusPanel(GameObject panel)
        {
            if (focusedPanel == panel) { router?.Poll(); SyncGuiInput(); return; }
            Controls?.SuppressGameplay();
            if (shortcutPanelOpen && focusedPanel == shortcutPanel) CloseShortcuts();
            focused?.Dispose(); focused = null; focusedPanel = panel;
            foreach (var pair in screens) pair.Key.SetActive(false);
            if (!panel) { SyncGuiInput(); return; }
            panel.SetActive(true);
            // The underlying inventory/shop/map/result owns its domain control leases.
            if (router != null && !router.TryOpen("temporary GUI " + panel.name, screens[panel], new ScreenPolicy(10,InputChannels.None,false,false),out focused,out var error))
                Debug.LogError("Temporary GUI focus: " + error, this);
            SyncGuiInput();
        }
        void RegisterScreen(GameObject panel, Button first, [System.Runtime.CompilerServices.CallerMemberName] string replayOwner = null)
        {
            Deviant.Replay.Unity.ReplayUiTarget.Attach(panel, "screen." + replayOwner, true);
            var view = panel.AddComponent<ScreenView>();
            view.Configure(panel,panel.AddComponent<CanvasGroup>(),EventSystem.current,first,null,true,true);
            screens.Add(panel,view); panel.SetActive(false);
        }

        void OnDisable()
        {
            if (shelfTagLayer) shelfTagLayer.gameObject.SetActive(false);
            if (buffTooltip) buffTooltip.gameObject.SetActive(false);
            CloseSettingsDraftQuestion(false);
            CloseAbandonRunQuestion(false);
            if(testingDisplay){Screen.SetResolution(savedWidth,savedHeight,savedMode);testingDisplay=false;}
            sketchRewardScope?.Dispose(); sketchRewardScope = null;
            CloseControlsWindow();
            if(systemTutorialReplay)systemTutorialReplay.Dismiss();
            CloseShortcuts();
            ResetHealthHitFeedback();
            if (inputUI) inputUI.Unbind();
            if (village) DisposeVillage();
            focused?.Dispose(); focused = null; focusedPanel = null;
            pointerScope?.Dispose(); pointerScope = null;
            if (host)
            {
                Presenter.Close(); if (Presenter.Paused) Presenter.TogglePause();
                var keyboard = host.GetComponent<InventoryKeyboardInput>(); if (keyboard) keyboard.ExternalInput = false;
            }
            if (map) { map.SetOpen(false); map.ExternalPresentation = map.ExternalInput = false; }
            if (shop) { shop.Close(); shop.ExternalPresentation = shop.ExternalInput = false; }
            if(chapter2){chapter2.ExternalInput=false;chapter2.ExternalPresentation=false;}
            if (world) { world.ExternalPresentation = false; if(world.diagnostics) world.diagnostics.ExternalHud = false; }
            if (run) run.ExternalPresentation = false; if (facility) facility.ExternalPresentation = false;
            if (blueprints) blueprints.ExternalPresentation = false; if (boss) boss.ExternalPresentation = false;
            if (rewards) rewards.ExternalPresentation = false; if (oldTitle) oldTitle.ExternalPresentation = false;
            if(developerMap)developerMap.ExternalPresentation=false;
            if(checkpoint)checkpoint.ExternalPresentation=false;
            if(feedback)feedback.ExternalHud=false;
        }
        void OnEnable() { if (built && host) { host.GetComponent<InventoryKeyboardInput>().ExternalInput = true; DiscoverChapter(); } }
        void OnDestroy() { DisposeBuffSlots(); DisposeLiveRename(); if(approvedInactiveArt)Destroy(approvedInactiveArt); router?.Dispose(); if (ownedEvents) Destroy(ownedEvents); DisposeArtisanBubble(); DisposeInkGuide(); DisposeDropHover(); DisposeWeaponBreaks(); DisposeWorldShop(); }

        // Frames use the warm ink copy (name-warm) when one exists (art review H-3, 2026-10-30); originals stay as delivered.
        Sprite Sprite(string name)
        {
            var frame = PaperGuiSkin.Frame(name); if(frame) return frame;
            if (!sprites.TryGetValue(name,out var result))
            {
                var pixel = Resources.Load<Sprite>("TemporaryGUI/Pixel/"+name);
                var warm = pixel ? null : Resources.Load<Sprite>("TemporaryGUI/"+name+"-warm");
                result = pixel ? pixel : warm ? warm : Resources.Load<Sprite>("TemporaryGUI/"+name); sprites[name] = result;
            }
            return result;
        }
        RectTransform Rect(string name, Transform parent,float x,float y,float w,float h)
        {
            var rt = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent,false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0,1); rt.anchoredPosition = new Vector2(x,-y); rt.sizeDelta = new Vector2(w,h); return rt;
        }
        Image Picture(string name, Transform parent,float x,float y,float w,float h, Color? color = null, bool raycast = false)
        {
            var image = Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>(); image.sprite = Sprite((name=="panel"||name=="card") && IsHud(parent)?"hud":name); image.color = color ?? Color.white;
            image.raycastTarget = raycast; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = PaperGuiSkin.Frame(name)?1:2; return image;
        }
        Text Label(string text,Transform parent,float x,float y,float w,float h,int size=14,Color? color=null)
        {
            var label = Rect("Text",parent,x,y,w,h).gameObject.AddComponent<Text>(); label.font = size>=16&&boldFont?boldFont:font; label.text = text;
            // Galmuri11 is a 12px font. Fractional scaling drops entire bitmap strokes.
            label.fontSize = UseApprovedTheme(parent)?size:size>=30?36:size>=18?24:12;
            if(UseApprovedTheme(parent)&&Deviant.UI.Theme.ApprovedTheme.Font)label.font=Deviant.UI.Theme.ApprovedTheme.Font;
            label.color = color ?? (IsHud(parent)?PaperGuiSkin.Paper:Cream); label.raycastTarget = false; label.alignment = TextAnchor.MiddleLeft;
            if(IsHud(parent) && color.HasValue)
            { if(color.Value==Muted)label.color=new Color32(202,199,177,255);else if(color.Value==Teal)label.color=new Color32(151,222,199,255); }
            if(UseApprovedTheme(parent))label.color=ApprovedTextColor(label.color);
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        readonly Dictionary<string,int> replayButtonCounters = new Dictionary<string,int>();
        Button Button(string text,Transform parent,float x,float y,float w,float h,Action action,bool primary=false,
            [System.Runtime.CompilerServices.CallerMemberName] string replayOwner = null)
        {
            var image = Picture(primary?"button-selected":"button",parent,x,y,w,h,null,true); image.name = text;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            // Build-scoped IDs never contain display text or user-entered values.
            replayButtonCounters.TryGetValue(replayOwner, out var replayOrdinal);
            replayButtonCounters[replayOwner] = replayOrdinal + 1;
            var replayTarget = Deviant.Replay.Unity.ReplayUiTarget.Attach(button.gameObject, "gui." + replayOwner + ".button." + replayOrdinal);
            var colors = button.colors; colors.highlightedColor = new Color(.92f,1f,.96f); colors.pressedColor = new Color(.73f,.86f,.8f); colors.selectedColor = Color.white; colors.disabledColor = new Color(.85f,.85f,.85f); button.colors = colors;
            var label = Label(text,image.transform,4,0,w-8,h,h<24?11:13); label.alignment = TextAnchor.MiddleCenter;
            if(UseApprovedTheme(parent))image.gameObject.AddComponent<ApprovedActionVisual>();else image.gameObject.AddComponent<PaperGuiButtonStyle>();
            button.onClick.AddListener(()=>{ if(!button.interactable)return; replayTarget.Activated(); action(); if(host) Refresh(); else if(village) RefreshHub(); }); return button;
        }
        static bool IsHud(Transform parent)
        { for(var p=parent;p;p=p.parent)if(p.name=="Gameplay HUD")return true;return false; }
        GameObject Panel(string title,float x,float y,float w,float h,out Button close)
        {
            var panel = Picture("panel",stage,x,y,w,h,null,true).gameObject; panel.name = title;
            Label(title,panel.transform,20,10,w-90,34,18); close = Button("×",panel.transform,w-48,10,30,30,Back);
            Picture("",panel.transform,18,52,w-36,1,Muted); return panel;
        }
    }
}
