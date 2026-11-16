using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Run;
using Deviant.UI.Presentation;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Starting village screens: pigment HUD, the shared artisan workbench and the record. Screens only show ProgressionService state and send its commands; the workbench scope owns the
    // pause and input block while a screen is open and returns only its own lease.
    public sealed partial class TemporaryGameGui
    {
        StartingVillageWorld village;
        GameObject hubHud, workbench, record, hubOpen;
        Text hubPigment, hubPrompt, hubInteractKey, benchTitle, benchName, benchEffect, benchWhen, benchStatus, benchMessage;
        Image benchFlash, benchCard, benchStatusLock;
        RectTransform benchItems, benchPreview, benchDetails;
        Button benchClose;
        const float BenchDetailsHeight=262, BenchDetailsWidth=ColumnW-16;
        Button benchBuy;
        IControlScope hubScope;
        int hubClosedFrame = -1;
        string hubBuilding, hubSelected, hubMessage = "";
        bool hubPaused, departing, benchDirty = true;
        float flashStarted = -1;
        static readonly string[] BlueprintNames = { "검", "망치", "발사기" };
        static readonly string[] BlueprintArt = { "sword", "hammer", "launcher" };

        static ProgressionService Profile => Chapter1ProfileHost.Service;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterVillage()
        { SceneManager.sceneLoaded -= VillageLoaded; SceneManager.sceneLoaded += VillageLoaded; }
        static void VillageLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var lobby = root.GetComponentInChildren<StartingVillageWorld>();
                if (!lobby) continue;
                var gui = new GameObject("Starting village GUI", typeof(RectTransform)).AddComponent<TemporaryGameGui>();
                gui.village = lobby; lobby.ExternalPresentation = true;
                // The old IMGUI HUD (key line, HP bars) drew for the first frames until the lobby finished building
                // (playtest video 2026-11-06, title -> village): hand the screen to this GUI from the very first frame.
                foreach (var w in scene.GetRootGameObjects()) foreach (var combat in w.GetComponentsInChildren<ForestCombatWorld>(true))
                { combat.ExternalPresentation = true; if (combat.diagnostics) combat.diagnostics.ExternalHud = true; }
                // The world picture too: until TemporaryGuiWorldOutput's first LateUpdate the viewport draws it with IMGUI,
                // which lies over every canvas (the boot cover included), and the scene's first frame lasts ~0.2 s.
                foreach (var w in scene.GetRootGameObjects()) foreach (var view in w.GetComponentsInChildren<Stage1VillageExperiment.VillagePixelViewport>(true))
                    view.ExternalPresentation = true;
                lobby.StationRequested += gui.OpenStation;
                gui.BuildRoot(); gui.BuildVillage(); gui.BuildMenu(); gui.BuildBootCover(); gui.built = true;
                // Like chapter 1 (InventoryGameBinding): the native 960x540 world goes into a uGUI canvas below the
                // GUI instead of the viewport's IMGUI draw, which would otherwise cover the village screens.
                var output = new GameObject("Village world output"); SceneManager.MoveGameObjectToScene(output, scene);
                gui.worldOutput = output.AddComponent<TemporaryGuiWorldOutput>();
                break;
            }
        }

        TemporaryGuiWorldOutput worldOutput;
        public bool WorldBehindGui => worldOutput && worldOutput.isActiveAndEnabled;
        IControlRequests VillageControls => village.World.arena.Controls;

        GameObject hubCurrency;
        Image hubToastPlate, hubPromptPlate;
        Text hubToast;

        void BuildVillage()
        {
            InitJournal(); dim.GetComponent<Image>().color = Veil;
            BuildStationCards(); BuildVisits();
            hubHud = Rect("Village HUD", stage, 0, 0, 960, 540).gameObject;
            // Reuse the combat pigment icon without a paper background or border.
            var currency = Rect("Village currency", hubHud.transform, 16, 14, 140, 36); hubCurrency = currency.gameObject;
            var pigmentIcon = Rect("Village pigment icon", currency.transform, 0, 2, 32, 32).gameObject.AddComponent<Image>();
            pigmentIcon.sprite = CombatSprite("pigment-currency"); pigmentIcon.preserveAspect = true; pigmentIcon.raycastTarget = false;
            hubPigment = Words("", currency.transform, 44, 0, 120, 36, 22, true, CorePaper);
            var outline=hubPigment.gameObject.AddComponent<Outline>();outline.effectColor=new Color32(24,27,30,255);outline.effectDistance=new Vector2(1.5f,1.5f);
            var currencyCanvas=currency.gameObject.AddComponent<Canvas>();currencyCanvas.overrideSorting=true;currencyCanvas.sortingLayerID=GetComponent<Canvas>().sortingLayerID;currencyCanvas.sortingOrder=GetComponent<Canvas>().sortingOrder+1;
            var pauseArt=Rect("Pause",hubHud.transform,944-HudButtonPixels,14,HudButtonPixels,HudButtonPixels).gameObject.AddComponent<Image>();
            pauseArt.sprite=HudButtonSprite(Deviant.Presentation.Unity.HudPixelKit.ButtonGlyph.Pause);
            pauseArt.raycastTarget=true;
            var pause=pauseArt.gameObject.AddComponent<Button>();
            pause.transition = Selectable.Transition.None; pause.onClick.AddListener(HubPause);
            // A short note at the bottom (refusals and the confirmation hint) and the F prompt above the ground line.
            hubToastPlate = Face("card", hubHud.transform, 280, 474, 400, 48, 10, 4);
            hubToast = Words("", hubToastPlate.transform, 0, 0, 400, 48, 16, true, new Color32(40, 35, 26, 255), TextAnchor.MiddleCenter);
            hubPromptPlate = Face("primary", hubHud.transform, 416, 400, 128, 36, 14, 9, true);
            var cap = Rounded("F", hubPromptPlate.transform, 8, 6, 24, 24, 2, 0, new Color32(245, 236, 216, 255), default);
            hubInteractKey = Words("F", cap.transform, 0, 0, 24, 24, 14, true, JDark, TextAnchor.MiddleCenter);
            hubPrompt = Words("", hubPromptPlate.transform, 42, 0, 200, 36, 16, true, JPaperText);
            BuildWorkbench(); BuildRecord(); BuildStory(); BuildOpening(); BuildSky();
        }

        // --- Screens ---------------------------------------------------------------------------------------------------

        void BuildWorkbench()
        {
            workbench = Book("장인의 공방", true, out benchTitle, out var close);
            BuildBenchTabs();
            benchItems = JournalScroll("공방 트리", workbench.transform, LeftX, ListY, ColumnW, 326);
            benchName = Heading(workbench.transform, "", RightX, TopY);
            benchName.horizontalOverflow=HorizontalWrapMode.Wrap;Place(benchName,RightX,TopY,ColumnW,36);
            benchDetails=JournalScroll("공방 설명",workbench.transform,RightX,ListY,ColumnW,BenchDetailsHeight);
            benchCard = Card(benchDetails, 0, 0, BenchDetailsWidth, 134);
            benchPreview = Rect("미리보기", benchCard.transform, 0, 0, ColumnW, 134);
            benchPreview.localScale=Vector3.one*(BenchDetailsWidth/ColumnW);
            benchFlash = Fill(benchCard.transform, BenchDetailsWidth / 2 - 32, 14, 64, 64, Color.white); benchFlash.type = Image.Type.Simple; benchFlash.enabled = false;
            benchEffect = Paragraph(benchDetails, 0, 144, BenchDetailsWidth, 16, 23, JInk);
            benchWhen = Paragraph(benchDetails,0,180,BenchDetailsWidth,13,20,JMuted);
            benchStatusLock = Glyph(LockGlyph, benchDetails, 1, 220, 11, 13, JWarning);
            benchStatus = Paragraph(benchDetails,19,218,BenchDetailsWidth-19,14,21,JWarning);
            benchMessage = Paragraph(workbench.transform,RightX,344,ColumnW,13,18,JWarning);
            Place(benchMessage,RightX,344,ColumnW,28);
            BuildBenchScrollbar();
            benchClose=close;
            benchBuy = Action("", workbench.transform, RightX, 376, ColumnW, 36, ()=>{if(BenchSelectedNode?.Foreign==true)(BenchSelectedButton as WorkshopNodeButton)?.Submit?.Invoke();else BuySelected();}, true);
            // Ready state: 해금, the pigment icon and the price centred as one line; other states use the label alone.
            var price = Rect("Price", benchBuy.transform, 0, 0, ColumnW, 36);
            ApprovedKeycap(price,"F",0,7,21,22,12).name="Keycap";
            Words("해금", price, 0, 0, 40, 36, 16, true, JPaperText).name = "Word";
            Glyph(CombatSprite("pigment-currency"),price,0,8,20,20,Color.white).name="Glyph";
            Words("", price, 0, 0, 40, 36, 16, true, JPaperText).name = "Cost";
            var ruleColor=new Color32(151,171,177,110);
            var columnRule=Fill(workbench.transform,400,TopY,.5f,374,ruleColor);
            columnRule.name="Workshop column rule";columnRule.raycastTarget=false;
            var footerRule=Fill(workbench.transform,LeftX,416,RightX+ColumnW-LeftX,.5f,ruleColor);
            footerRule.name="Workshop footer rule";footerRule.raycastTarget=false;
            var controls=Rect("Workshop controls",workbench.transform,LeftX,426,RightX+ColumnW-LeftX,22);
            ApprovedKeycap(controls,"Q / E",0,0,44,22,12);
            Words("줄기 전환",controls,52,0,120,22,12,false,JMuted);
            ApprovedKeycap(controls,"W / S",RightX-LeftX,0,44,22,12);
            Words("설명 스크롤",controls,RightX-LeftX+52,0,150,22,12,false,JMuted);
            RegisterScreen(workbench, close);
        }

        void BuildBenchScrollbar()
        {
            var viewport=(RectTransform)benchDetails.parent;var scroll=viewport.GetComponent<ScrollRect>();
            if(scroll.verticalScrollbar)return;
            var track=Rect("설명 스크롤",viewport,ColumnW-5,0,5,BenchDetailsHeight);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
            var handle=Rect("Handle",track,0,0,5,BenchDetailsHeight).gameObject.AddComponent<Image>();handle.color=new Color32(151,171,177,200);
            handle.rectTransform.sizeDelta=Vector2.zero;handle.rectTransform.anchoredPosition=Vector2.zero;
            bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }

        void BuildRecord()
        {
            BuildRecordArchive();
        }

        void OpenStation(StartingVillageWorld.Station station) => OpenStation(station, false);

        // scripted: the opening opens it itself (not the player's F).
        void OpenStation(StartingVillageWorld.Station station, bool scripted)
        {
            // The F that closed a screen (or finished the story) this frame must not reopen the station underneath.
            if (hubOpen || hubPaused || (!scripted && Time.frameCount == hubClosedFrame)) return;
            if (!scripted && !village.StationOpen(station)) return;
            if (!scripted && OpeningStation(station)) return;
            // The well has nothing to show: it only takes a carried village ink (playtest 2026-11-13).
            if (station.Id == StartingVillageWorld.ReturnWell) { if (!scripted) TryPourWellInk(); return; }
            // During a visit the stand takes the piece (the director has it); otherwise it shows the pieces.
            if (station.Id == StartingVillageWorld.VillagePalette) return;
            switch (station.Id)
            {
                case StartingVillageWorld.ServiceHub:
                case StartingVillageWorld.PaletteBench:
                case StartingVillageWorld.SupplyCounter:
                case StartingVillageWorld.ReclaimBench: OpenBench(StartingVillageWorld.ServiceHub); break;
                case StartingVillageWorld.RecordDesk: OpenRecordArchive(); break;
                case StartingVillageWorld.DepartureSign: DepartAtSign(); break;
                case StartingVillageWorld.DepartureTable: OpenDeparture(); break;
            }
        }

        // The departure book starts from the last confirmed choice, from the table.
        void OpenDeparture()
        {
            DepartAtSign();
        }

        void OpenBench(string building)
        {
            if (!Profile.Snapshot.ArtisanWorkshopOpen) return;
            hubBuilding = StartingVillageWorld.ServiceHub; hubSelected = CurrentTier(BenchLines(hubBuilding)[0]).id;
            benchBranch = WorkshopTreeLayout.BranchOf(Profile.Catalog, Profile.Catalog.Find(hubSelected));
            hubMessage = ""; benchDirty = true; OpenHubPanel(workbench);
            FocusBenchTab();
            SetBenchContentFocus();
        }

        // One village lease pauses the lobby and blocks combat/interaction input while any screen is open.
        void OpenHubPanel(GameObject panel)
        {
            if (panel && panel != hubOpen)
                PresentationSfx.PlaySelected(panel == record ? "sfx_story_page_turn" : "sfx_ui_station_open");
            hubOpen = panel;
            if (hubScope == null)
            {
                var scope = VillageControls.OpenScope("starting village screen");
                try { scope.BlockInput(InputChannels.Combat | InputChannels.Interaction); scope.PauseSimulation(); hubScope = scope; }
                catch { scope.Dispose(); throw; }
            }
            RefreshHub();
        }

        void CloseHubPanel()
        {
            if (hubPaused) PresentationSfx.PlaySelected("sfx_ui_resume");
            else if (hubOpen) PresentationSfx.PlaySelected("sfx_ui_cancel");
            if (hubOpen == record) { StopRecordTransition(); OpeningScreenClosed(StartingVillageWorld.RecordDesk); }
            if (hubOpen == workbench)
            {
                OpeningScreenClosed(StartingVillageWorld.ServiceHub);
                OpeningScreenClosed(StartingVillageWorld.PaletteBench);
                OpeningScreenClosed(StartingVillageWorld.SupplyCounter);
                OpeningScreenClosed(StartingVillageWorld.ReclaimBench);
            }
            hubClosedFrame = Time.frameCount;
            Controls?.SuppressGameplay();
            hubOpen = null; hubPaused = false; hubScope?.Dispose(); hubScope = null; RefreshHub();
        }

        // Until the opening's first check has run (and its notebook is up) the village is not shown: a few frames of the
        // sketch village flashed between the title's black and the notebook (playtest video 2026-11-06).
        Image bootCover; float bootCoverAt;
        void BuildBootCover()
        {
            var go = new GameObject("Boot cover", typeof(RectTransform), typeof(Image)); go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            bootCover = go.GetComponent<Image>(); bootCover.color = new Color32(0x1c, 0x18, 0x14, 255); bootCover.raycastTarget = false;
            bootCoverAt = Time.unscaledTime;
        }
        void TickBootCover()
        {
            if (!bootCover) return;
            if (storyChecked && (notebookOpen || !openingActive) || Time.unscaledTime - bootCoverAt > BootCoverMaxSeconds) { Destroy(bootCover.gameObject); bootCover = null; }
        }
        const float BootCoverMaxSeconds = 4;

        void HubUpdate()
        {
            if (GuardLoadingUi()) return;
            if (village.World) { village.World.ExternalPresentation = true; if (village.World.diagnostics) village.World.diagnostics.ExternalHud = true; }
            TickBootCover();
            if (!village.Ready || village.Departing) return;
            // The village owns the HUD, just as DiscoverChapter does in the combat scene.
            if (village.World.diagnostics) village.World.diagnostics.ExternalHud = true;
            if (village.World.arena.TryGetComponent(out Deviant.Combat.CombatFeelFeedback townFeedback)) townFeedback.ExternalHud = true;
            if (router == null) router = new ScreenRouter(VillageControls);
            GuardPointer();
            CheckFirstVisitStory();
            village.WellResting = openingActive && visitPiece == 0 && !returnPlaying;
            village.WellHasInk = WellInkReady;
            TickSky();
            var k = Keyboard.current;
            TickVisit();
            if (TickVillageInkModal()) { RefreshHub(); return; }
            if (storyActive) { StoryInput(k); RefreshHub(); return; }
            if (!openingActive && !hubOpen && !hubPaused && !CheckFirstReturn()) CheckVisit();
            if (openingActive && OpeningUpdate(k)) { RefreshHub(); return; }
            if (hubOpen == record) { TickRecordInput(); RefreshHub(); return; }
            if (TickBenchTabInput()) { RefreshHub(); return; }
            if (Controls)
            {
                if (TakeBack()) Back();
                else if (hubOpen) Controls.Take(GameAction.Interact); // F belongs to the focused uGUI button.
            }
            RefreshHub();
        }

        void HubBack()
        {
            Controls?.SuppressGameplay();
            if (notebookOpen) return;
            if (storyActive) { SkipStory(); return; }
            if (hubOpen == record && RecordDetailShowing) { ShowRecordList(); RefreshHub(); return; }
            if (hubOpen || hubPaused) { CloseHubPanel(); return; }
            hubPaused = true; OpenHubPanel(null);
        }
        // The pause control stays on the HUD over an open book, as in the design; it pauses from there too.
        void HubPause()
        {
            if (storyActive || hubPaused) return;
            if (hubOpen) CloseHubPanel();
            hubPaused = true; OpenHubPanel(null);
        }

        // The pigment chip waits until the artisan has told what magic pigment is: its visit after the first run (the chip
        // appears on its line), or the first piece visit. Saves from before that scene opened the supply office already.
        bool PigmentKnown(ProfileSnapshot s) => pigmentRevealed || s.HasSeen(FirstRunVisitStory) || s.HasSeen(VisitStory(1))
            || s.HasSeen(StationOpenedStory(StartingVillageWorld.SupplyCounter));

        void RefreshHub()
        {
            if (!built || !village || !village.Ready) return;
            var snapshot = Profile.Snapshot;
            hubCurrency.SetActive(PigmentKnown(snapshot));
            if (hubPigment.text != snapshot.Pigment.ToString())
            {
                hubPigment.text = snapshot.Pigment.ToString();
                ((RectTransform)hubCurrency.transform).sizeDelta = new Vector2(52 + Width(hubPigment), 36);
            }
            // The bottom guidance toast is not shown any more (user 2026-11-09).
            hubToastPlate.gameObject.SetActive(false);
            RefreshStationCards();
            // The name cards over the stations carry the names. The bottom 「F 시설 이름」 prompt that stood in when they were
            // hidden (it popped up at the departure sign after F) is not shown any more (playtest 2026-11-06).
            var near = hubOpen || hubPaused ? null : village.NearestStation;
            hubPromptPlate.gameObject.SetActive(false);
            hubInteractKey.text = KeyLabel(GameAction.Interact);
            float capWidth = Mathf.Max(24, Width(hubInteractKey) + 10);
            ((RectTransform)hubInteractKey.transform.parent).sizeDelta = new Vector2(capWidth, 24);
            hubInteractKey.rectTransform.sizeDelta = new Vector2(capWidth, 24);
            hubPrompt.rectTransform.anchoredPosition = new Vector2(capWidth + 18, 0);
            if (near != null)
            {
                hubPrompt.text = near.Name; float width = capWidth + 18 + Width(hubPrompt) + 16;
                Place(hubPromptPlate, Mathf.Round(480 - width / 2), 400, width, 36);
            }
            if (hubOpen == workbench) RefreshBench();
            else if (hubOpen == record) RefreshRecord();
            if (hubPaused && !hubOpen) RefreshHubMenu();
            var active = hubOpen ? hubOpen : hubPaused ? (shortcutPanelOpen && !shortcutsInSettings ? shortcutPanel : menu) : null;
            dim.SetActive(active && hubOpen != record);
            dim.GetComponent<Image>().color = hubPaused && !hubOpen ? PauseVeil : Veil;
            hubHud.SetActive(hubOpen != record && (!openingActive || OpeningInVillage) && !VillageInkModalShowing && !storyActive && !(hubPaused && !hubOpen));
            RefreshSky();
            FocusPanel(active);
            RefreshScreenBackdrop();
        }

        void RefreshHubMenu()
        {
            menuTitle.text = "일시정지"; menuTitle.color = Cream; menuStats.text = "시작 마을";
            menuPrimary.GetComponentInChildren<Text>().text = "계속하기";
            menuSecondary.GetComponentInChildren<Text>().text = "타이틀로"; menuSecondary.gameObject.SetActive(true);
            LayoutMenu();
        }

        static string Effect(ProgressionUnlock u) => u.effect.kind switch
        {
            ProgressionIds.PaletteSlotCount => $"두 무기의 팔레트가 모두 {u.effect.value}칸으로 출발합니다.",
            ProgressionIds.FirstSlotWildcard => $"무기 {u.effect.weaponSlot} 팔레트의 첫 칸이 무색으로 출발합니다. 어느 색 잉크든 넣을 수 있습니다.",
            ProgressionIds.StartingGoldBonus => $"새 런을 시작할 때 출발 골드에 {u.effect.amount}G를 더합니다. 가장 높은 단계 하나만 적용합니다.",
            ProgressionIds.DestroyedInkGoldBonus => $"보유한 색상 잉크를 파괴할 때마다 {u.effect.amount}G를 받습니다.",
            ProgressionIds.AttackBonusPercent => $"두 무기의 공격력이 {u.effect.value}% 오릅니다. 정비소 캐릭터 강화와 더해 적용합니다.",
            ProgressionIds.MaxHealthBonus => $"최대 체력이 {u.effect.value} 늘어난 채로 출발합니다.",
            ProgressionIds.BagSlotCount => $"모든 잉크가 함께 쓰는 가방이 {u.effect.value}칸으로 출발합니다.",
            ProgressionIds.MoveSpeedBonusPercent => $"걷기, 달리기 속도가 {u.effect.value}% 오릅니다. 대시와 점프는 그대로이며 노랑 잉크의 이동속도와 더해 적용합니다.",
            ProgressionIds.DashCharges => $"대시를 {u.effect.value}번 이어 쓸 수 있습니다. 첫 대시부터 1초가 지나면 {u.effect.value}번이 한꺼번에 다시 찹니다.",
            ProgressionIds.InkPartUnlock => $"「{InkName(u)}」 잉크가 필드, 엘리트 드롭, 정비소 판매대, 보스 제단 후보에 나오기 시작합니다.",
            ProgressionIds.InkBundleUnlock => $"잉크 {u.effect.parts.Length}종을 함께 해금합니다. 다음 런부터 보상, 상점 후보에 등장합니다.",
            ProgressionIds.AugmentBundleUnlock => $"증강 {u.effect.parts.Length}종을 함께 해금합니다. 다음 런부터 정비소 증강 후보에 등장합니다.",
            ProgressionIds.SharedRerollCount => $"새 런마다 공용 리롤 {u.effect.value}회를 받습니다. 잉크, 무기, 증강, 상점이 함께 사용합니다.",
            ProgressionIds.RecoveryExtraUses => $"각 챕터의 충전소를 {u.effect.value}회 더 이용할 수 있습니다. 추가 이용은 회당 150G입니다.",
            ProgressionIds.ReviveCount => $"한 런에 {u.effect.value}회, 최대 체력의 {u.effect.healthPercent}%로 부활합니다.",
            ProgressionIds.WeaponBreakGold => $"내구도가 소진된 무기 한 자루마다 {u.effect.value}G를 회수합니다. 교체, 판매는 지급하지 않습니다.",
            ProgressionIds.ShopExtraItems => $"상점의 무기와 잉크 진열을 각각 {u.effect.value}개 늘립니다.",
            ProgressionIds.StartingRewardCount => "새 런 출발 시 무기 보상 1회를 추가로 받습니다. 일반 처치 보상은 그대로 유지됩니다.",
            ProgressionIds.StartingInkRewardCount => "새 런 출발 시 열린 잉크 3종 중 1종을 골라 런 도감에 등록합니다. 출발 무기 보상보다 먼저 나옵니다.",
            ProgressionIds.KillGoldBonusPercent => $"몬스터 처치 골드가 {u.effect.value}% 증가합니다. 소수 증가분도 런 안에서 누적합니다.",
            _ => "",
        };
        static string When(ProgressionUnlock u) => "적용 " + (u.effect.kind switch
        {
            ProgressionIds.PaletteSlotCount => "다음 새 런부터 두 팔레트 모두",
            ProgressionIds.FirstSlotWildcard => $"다음 새 런부터 무기 {u.effect.weaponSlot}만",
            ProgressionIds.StartingGoldBonus => "새 런마다 한 번",
            ProgressionIds.DashCharges => "마을 즉시 / 다음 새 런",
            ProgressionIds.InkBundleUnlock or ProgressionIds.AugmentBundleUnlock or ProgressionIds.SharedRerollCount or
                ProgressionIds.RecoveryExtraUses or ProgressionIds.ReviveCount or ProgressionIds.WeaponBreakGold or ProgressionIds.ShopExtraItems or
                ProgressionIds.StartingRewardCount or ProgressionIds.StartingInkRewardCount or ProgressionIds.KillGoldBonusPercent => "다음 새 런부터",
            ProgressionIds.AttackBonusPercent or ProgressionIds.MaxHealthBonus or ProgressionIds.BagSlotCount
                or ProgressionIds.MoveSpeedBonusPercent or ProgressionIds.InkPartUnlock => "다음 새 런부터",
            _ => "구매 후 실제 파괴마다",
        });
        static int RequiredChapter(ProgressionUnlock unlock)
        {
            var building = Profile.Catalog.Building(unlock.building);
            int GateChapter(string gate) => gate == ProgressionIds.Chapter2Gate ? 2 : gate == ProgressionIds.BossGate ? 1 : 0;
            return Math.Max(unlock.openAtPieces,
                Math.Max(GateChapter(unlock.requiredClear), GateChapter(building?.requiredClear)));
        }

        static string CompactPrerequisite(string id)
        {
            var u = Profile.Catalog.Find(id);
            if (u == null) return "선행 항목\n해금";
            if (!string.IsNullOrEmpty(u.node) && WorkshopTreeLayout.Levels(Profile.Catalog, u).Count > 1) return u.node + "\n완성";
            int tier = 1;
            for (var at = Profile.Catalog.FirstTier(u); at != null && at.id != u.id; at = Profile.Catalog.NextTier(at)) tier++;
            return u.effect.kind switch
            {
                ProgressionIds.BagSlotCount => $"가방\n{u.effect.value}칸 해금",
                ProgressionIds.PaletteSlotCount => $"팔레트\n{u.effect.value}칸 해금",
                ProgressionIds.AttackBonusPercent => $"공격력\n{tier}단계",
                ProgressionIds.MaxHealthBonus => $"최대 체력\n{tier}단계",
                ProgressionIds.MoveSpeedBonusPercent => $"이동속도\n{tier}단계",
                ProgressionIds.StartingGoldBonus => $"지원금\n{tier}단계",
                ProgressionIds.SharedRerollCount => $"공용 리롤\n{tier}단계",
                ProgressionIds.KillGoldBonusPercent => $"처치 골드\n{tier}단계",
                ProgressionIds.ShopExtraItems => $"상점 진열\n{tier}단계",
                ProgressionIds.AugmentBundleUnlock or ProgressionIds.InkBundleUnlock => u.name.Replace(" 증강 묶음", "\n증강 묶음").Replace(" 잉크 묶음", "\n잉크 묶음"),
                ProgressionIds.FirstSlotWildcard => $"무기 {u.effect.weaponSlot}\n첫 칸 무색",
                _ => InkName(u).Length > 4 ? InkName(u).Substring(0, 4) + "\n" + InkName(u).Substring(4) : InkName(u),
            };
        }

        static string CompactCondition(UnlockStatus status) => status.State switch
        {
            UnlockState.LockedUntilFirstRun => RequiredChapter(status.Unlock) > 0 ? $"{RequiredChapter(status.Unlock)}챕터\n클리어" : "첫 런\n완료",
            UnlockState.LockedUntilBossClear or UnlockState.LockedUntilChapter2Clear or UnlockState.LockedUntilPieces
                => $"{RequiredChapter(status.Unlock)}챕터\n클리어",
            UnlockState.MissingPrerequisite => CompactPrerequisite(status.MissingPrerequisite),
            _ => "",
        };

        // Read requirements from the same catalog as purchases; display all unmet conditions without changing eligibility.
        static string Reason(UnlockStatus status)
        {
            if (!Locked(status.State)) return "";
            var unlock = status.Unlock; var building = Profile.Catalog.Building(unlock.building); var snapshot = Profile.Snapshot;
            var gates = new[] { unlock.requiredClear, building?.requiredClear };
            bool MissingGate(string gate) => gate == ProgressionIds.FirstRunGate ? !snapshot.FirstRunSettled
                : gate == ProgressionIds.BossGate ? !snapshot.BossFirstClear
                : gate == ProgressionIds.Chapter2Gate && snapshot.ChaptersCleared < 2;
            var conditions = new List<string>();
            int chapter = RequiredChapter(unlock);
            if (chapter > 0 && (Profile.PalettePieces < chapter || gates.Any(MissingGate))) conditions.Add($"{chapter}챕터 클리어 필요");
            else if (gates.Contains(ProgressionIds.FirstRunGate) && !snapshot.FirstRunSettled) conditions.Add("첫 런을 마치고 돌아오면 열립니다");
            // A tier of several in a node is named by its node, which must be complete ("최대 체력 I 완성 필요").
            foreach (string id in unlock.prerequisites ?? Array.Empty<string>())
                if (!snapshot.Has(id))
                {
                    var pre = Profile.Catalog.Find(id);
                    conditions.Add(pre == null ? id + " 해금 필요" : WorkshopTreeLayout.Levels(Profile.Catalog, pre).Count > 1 ? WorkshopTreeLayout.NameOf(pre) + " 완성 필요" : pre.name + " 해금 필요");
                }
            return string.Join("\n", conditions);
        }
        static string ButtonText(UnlockStatus status) => status.State switch
        {
            UnlockState.Owned => "해금 완료",
            UnlockState.LockedUntilFirstRun or UnlockState.LockedUntilBossClear or UnlockState.LockedUntilChapter2Clear or UnlockState.LockedUntilPieces or UnlockState.MissingPrerequisite => CompactCondition(status).Replace("\n", " ") + " 필요",
            UnlockState.NotEnoughPigment => $"필요 안료 {status.Unlock.cost}개",
            UnlockState.PricePending => "가격 미정",
            _ => "",
        };
        static bool Locked(UnlockState state) => state == UnlockState.LockedUntilFirstRun || state == UnlockState.LockedUntilBossClear || state == UnlockState.LockedUntilChapter2Clear || state == UnlockState.LockedUntilPieces || state == UnlockState.MissingPrerequisite;

        // A line of tiers (palette 4 -> 5 slots, attack 1 -> 4) takes one row showing its next tier to buy, or its last
        // tier once all are owned; deeper tiers stay out of the list.
        // "잉크 해금: 집중 공격" -> "집중 공격".
        static string InkName(ProgressionUnlock u) { int at = u.name.IndexOf(':'); return at >= 0 ? u.name.Substring(at + 1).Trim() : u.name; }
        // The part's own journal icon, or its colour's drop icon for parts without one.
        static string InkFace(ProgressionUnlock u)
        {
            string part = u.effect.part ?? "", colour = part.Split('_').Length > 1 ? part.Split('_')[1] : "";
            return Art("parts/" + part) ? "parts/" + part : ColourDrops.TryGetValue(colour, out var drop) ? "parts/" + drop : "pigment";
        }
        static ProgressionUnlock[] BenchLines(string building) =>
            Profile.Catalog.Unlocks.Where(u => (building == StartingVillageWorld.ServiceHub ? u.building == "palette_atelier" || u.building == "supply_office" || u.building == "reclaim_workshop" : u.building == building) && Profile.Catalog.FirstTier(u) == u).ToArray();
        static ProgressionUnlock CurrentTier(ProgressionUnlock unlock)
        {
            unlock = Profile.Catalog.FirstTier(unlock);
            for (var next = Profile.Catalog.NextTier(unlock); next != null && Profile.Snapshot.Has(unlock.id); next = Profile.Catalog.NextTier(unlock)) unlock = next;
            return unlock;
        }

        void RefreshBench()
        {
            if (benchTitle.text != "장인의 공방") { benchTitle.text = "장인의 공방"; FitTitle(benchTitle); }
            RefreshBenchTree();
            var selected = Profile.Catalog.Find(hubSelected); var current = Profile.StatusOf(hubSelected);
            // Tier progress remains inside the tree node; the detail heading is the name alone.
            benchName.text = WorkshopTreeLayout.NameOf(selected);
            if (benchPreview.name != "미리보기 " + selected.id + current.State) { benchPreview.name = "미리보기 " + selected.id + current.State; DrawUnlockPreview(selected); }
            string detailKey=selected.id+":"+current.State;
            if(benchDetails.name!=detailKey){benchDetails.name=detailKey;benchDetails.parent.GetComponent<ScrollRect>().verticalNormalizedPosition=1;}
            benchEffect.text = Effect(selected);
            benchWhen.text = When(selected);
            bool shortOfPigment=current.State==UnlockState.NotEnoughPigment;
            var shortageColor=new Color32(235,105,105,255);
            benchStatus.text=shortOfPigment?$"보유 {Profile.Snapshot.Pigment} / 필요 {selected.cost}개 · {Math.Max(0,selected.cost-Profile.Snapshot.Pigment)}개 부족":Reason(current);
            benchStatus.color=shortOfPigment?shortageColor:JWarning;
            bool hasReason = !string.IsNullOrEmpty(benchStatus.text);
            float previewHeight=((RectTransform)benchCard.transform).sizeDelta.y*(BenchDetailsWidth/ColumnW);
            float y=previewHeight+16,effectH=Mathf.Max(23,LineCount(benchEffect)*23);
            Place(benchEffect,0,y,BenchDetailsWidth,effectH);y+=effectH+14;
            float whenH=Mathf.Max(20,LineCount(benchWhen)*20);Place(benchWhen,0,y,BenchDetailsWidth,whenH);y+=whenH+16;
            float reasonH=hasReason?Mathf.Max(21,LineCount(benchStatus)*21):0;
            Place(benchStatus,19,y,BenchDetailsWidth-19,reasonH);benchStatusLock.enabled=hasReason&&!shortOfPigment;Place(benchStatusLock,1,y+4,11,13);
            benchDetails.sizeDelta=new Vector2(ColumnW,Mathf.Max(BenchDetailsHeight,y+reasonH+12));
            bool foreign=BenchSelectedNode?.Foreign==true;
            bool ready = current.CanBuy&&!foreign;
            benchBuy.interactable = ready||foreign; Paint(benchBuy, true);
            benchBuy.GetComponentInChildren<Text>().text = ready||foreign||shortOfPigment ? "" : ButtonText(current);
            var price = benchBuy.transform.Find("Price"); price.gameObject.SetActive(ready||foreign||shortOfPigment);
            if (ready||foreign||shortOfPigment)
            {
                var word = price.Find("Word").GetComponent<Text>(); var cost = price.Find("Cost").GetComponent<Text>(); cost.text = selected.cost.ToString();cost.color=shortOfPigment?shortageColor:CoreGold;word.color=shortOfPigment?shortageColor:CoreGold;
                word.text=foreign?"선행 항목 보기":shortOfPigment?"필요 안료":"해금";price.Find("Glyph").gameObject.SetActive(!foreign);cost.gameObject.SetActive(!foreign);
                var focused=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                bool prompt=(ready||foreign)&&!benchTabsFocused&&focused&&(focused==BenchSelectedButton?.gameObject||focused==benchBuy.gameObject);
                var keycap=price.Find("Keycap");keycap.gameObject.SetActive(prompt);
                float wordW = Width(word), costW = Width(cost), total = wordW + (foreign?0:4+20+4+costW)+(prompt?29:0), x = Mathf.Round(ColumnW / 2 - total / 2);
                if(prompt){Place(keycap, x,7,21,22);x+=29;}
                Place(word, x, 0, wordW + 2, 36); ((RectTransform)price.Find("Glyph")).anchoredPosition = new Vector2(x + wordW + 4, -8); Place(cost, x + wordW + 28, 0, costW + 2, 36);
            }
            benchMessage.text = Deviant.UI.Theme.ApprovedInkText.Display(hubMessage);
            NavigateBenchTree(ready);
            AnimateFlash();
        }

        // 지금 → 해금 후 for palette unlocks; the gold line for the supply and reclaim bonuses.
        void DrawUnlockPreview(ProgressionUnlock unlock)
        {
            ClearGrowth(benchPreview);
            var snapshot = Profile.Snapshot;
            int slots = CurrentSlots(snapshot); bool w1 = Owns(snapshot, 1), w2 = Owns(snapshot, 2);
            float height = 92;
            switch (unlock.effect.kind)
            {
                case ProgressionIds.PaletteSlotCount:
                    CompareRows(slots, Math.Max(slots, unlock.effect.value), new[] { (0, w1, w1), (1, w2, w2) }); height = 134; break;
                case ProgressionIds.FirstSlotWildcard:
                    int weapon = unlock.effect.weaponSlot - 1; bool owned = weapon == 0 ? w1 : w2;
                    CompareRows(slots, slots, new[] { (weapon, owned, true) });
                    Color[] inks = { new Color32(174, 51, 46, 255), new Color32(186, 139, 34, 255), new Color32(35, 102, 64, 255), new Color32(18, 84, 174, 255), new Color32(105, 39, 120, 255) };
                    for (int i = 0; i < 5; i++)
                    {
                        Glyph(Disc(18), benchPreview, 113 + i * 26, 84, 18, 18, new Color32(101, 94, 81, 255));
                        Glyph(Disc(16), benchPreview, 114 + i * 26, 85, 16, 16, inks[i]);
                    }
                    height = 120; break;
                case ProgressionIds.StartingGoldBonus:
                    Benefit(false, "출발 골드", $"{Highest(snapshot, unlock.effect.kind, e => e.amount)}G{Arrow}{unlock.effect.amount}G"); break;
                case ProgressionIds.AttackBonusPercent:
                    Benefit(false, "공격력", $"+{Highest(snapshot, unlock.effect.kind, e => e.value)}%{Arrow}+{unlock.effect.value}%", "sword"); break;
                case ProgressionIds.MaxHealthBonus:
                    Benefit(false, "최대 체력", $"+{Highest(snapshot, unlock.effect.kind, e => e.value)}{Arrow}+{unlock.effect.value}", null); break;
                case ProgressionIds.BagSlotCount:
                    Benefit(false, "가방", $"{Math.Max(BaseBagSlots, Highest(snapshot, unlock.effect.kind, e => e.value))}칸{Arrow}{unlock.effect.value}칸", "bag"); break;
                case ProgressionIds.MoveSpeedBonusPercent:
                    Benefit(false, "이동속도", $"+{Highest(snapshot, unlock.effect.kind, e => e.value)}%{Arrow}+{unlock.effect.value}%", "parts/ink2_yellow_01"); break;
                case ProgressionIds.DashCharges:
                    Benefit(false, "대시", $"{Math.Max(ProgressionIds.BaseDashCharges, Highest(snapshot, unlock.effect.kind, e => e.value))}회{Arrow}{unlock.effect.value}회", "parts/ink3_yellow_03"); break;
                case ProgressionIds.InkPartUnlock:
                    Benefit(false, "새 잉크", InkName(unlock), InkFace(unlock)); break;
                case ProgressionIds.InkBundleUnlock:
                case ProgressionIds.AugmentBundleUnlock:
                    Benefit(false, unlock.effect.kind == ProgressionIds.InkBundleUnlock ? "잉크 묶음" : "증강 묶음", unlock.effect.parts.Length + "종 함께 개방"); break;
                case ProgressionIds.SharedRerollCount:
                case ProgressionIds.RecoveryExtraUses:
                case ProgressionIds.ReviveCount:
                case ProgressionIds.ShopExtraItems:
                case ProgressionIds.StartingRewardCount:
                case ProgressionIds.StartingInkRewardCount:
                    Benefit(false, unlock.name, Highest(snapshot, unlock.effect.kind, e => e.value) + Arrow + unlock.effect.value); break;
                case ProgressionIds.WeaponBreakGold:
                    Benefit(false, "무기 파괴 회수", Highest(snapshot, unlock.effect.kind, e => e.value) + "G" + Arrow + unlock.effect.value + "G"); break;
                case ProgressionIds.KillGoldBonusPercent:
                    Benefit(false, "처치 골드", "+" + Highest(snapshot, unlock.effect.kind, e => e.value) + "%" + Arrow + "+" + unlock.effect.value + "%"); break;
                default:
                    Benefit(true, "잉크 1개 파괴", "+" + unlock.effect.amount + "G"); break;
            }
            ((RectTransform)benchCard.transform).sizeDelta = new Vector2(ColumnW, height);
        }
        // hub-polish.css .hub-compare: columns 28 px icon | now slots | 18 px arrow | after slots, 8 px gaps, centred in
        // the card; header 지금 / 해금 후 over the slot columns, rows 42 px apart.
        void CompareRows(int now, int after, (int weapon, bool wildNow, bool wildAfter)[] rows)
        {
            float pitch=Mathf.Min(30,(ColumnW-74)/Mathf.Max(1,now+after));
            float nowW = now * pitch - 4, afterW = after * pitch - 4, gridW = 28 + 8 + nowW + 8 + 18 + 8 + afterW, x = Mathf.Round(ColumnW / 2 - gridW / 2);
            float nowX = x + 36, arrowX = nowX + nowW + 8, afterX = arrowX + 26;
            Words("지금", benchPreview, nowX, 18, nowW, 14, 12, true, JMuted, TextAnchor.MiddleCenter);
            Words("해금 후", benchPreview, afterX, 18, afterW, 14, 12, true, JMuted, TextAnchor.MiddleCenter);
            for (int r = 0; r < rows.Length; r++)
            {
                float y = 40 + r * 42;
                Face(BlueprintArt[rows[r].weapon], benchPreview, x, y + 3, 28, 28);
                PreviewSlots(benchPreview, now, rows[r].wildNow, nowX, y,pitch);
                Words("→", benchPreview, arrowX, y, 18, 34, 16, false, JMuted, TextAnchor.MiddleCenter);
                PreviewSlots(benchPreview, after, rows[r].wildAfter, afterX, y,pitch);
            }
        }
        const string Arrow = " <size=16><color=#847761>→</color></size> ";
        static int Highest(ProfileSnapshot s, string kind, Func<ProgressionEffect, int> field) => Profile.Catalog.Highest(kind, s.Has, field);
        // The run's bag before any bag tier: the shared bag of the weapon-growth data (5 slots).
        const int BaseBagSlots = 5;
        // hub-polish.css .benefit-line: a 56 px coin (after an unknown slot and arrow for the reclaim), then a small
        // caption over the 22 px value, centred in the card. Character upgrades swap the coin for the sword art or,
        // with no face art (null), the heart glyph.
        void Benefit(bool reclaim, string caption, string value, string face = "coin")
        {
            var small = Words(caption, benchPreview, 0, 23, 200, 16, 12, false, JMuted);
            var strong = Words(value, benchPreview, 0, 38, 220, 29, 22, true, JInk);
            float lead = reclaim ? 61 : 0, textW = Mathf.Max(Width(small), Width(strong)), total = lead + 56 + 16 + textW, x = Mathf.Round(ColumnW / 2 - total / 2);
            if (reclaim)
            {
                PreviewSlots(benchPreview, 1, false, x, 29);
                Words("→", benchPreview, x + 36, 29, 15, 34, 16, false, JMuted, TextAnchor.MiddleCenter);
            }
            if (face != null) Face(face, benchPreview, x + lead, 18, 56, 56);
            else Glyph(HeartGlyph, benchPreview, x + lead + 6, 24, 44, 40, HeartRed);
            small.rectTransform.anchoredPosition = new Vector2(x + lead + 72, -23); strong.rectTransform.anchoredPosition = new Vector2(x + lead + 72, -38);
        }
        // A start palette's colours are drawn at departure: unknown slots show "?", the wildcard slot reads 무색.
        void PreviewSlots(Transform parent, int slots, bool wildcard, float x, float y,float pitch=30)
        {
            for (int i = 0; i < slots; i++)
            {
                var slot = Face("slot", parent, x + i * pitch, y, 26, 34, 4, 3);slot.rectTransform.localScale=new Vector3(Mathf.Min(1,(pitch-4)/26),1,1);
                if (i == 0 && wildcard)
                {
                    Color line = new Color32(116, 108, 90, 255);
                    Fill(slot.transform, 0, 0, 26, 2, line); Fill(slot.transform, 0, 32, 26, 2, line); Fill(slot.transform, 0, 2, 2, 30, line); Fill(slot.transform, 24, 2, 2, 30, line);
                    Words("무색", slot.transform, 0, 0, 26, 34, 10, true, new Color32(41, 37, 27, 255), TextAnchor.MiddleCenter);
                }
                else Words("?", slot.transform, 0, 0, 26, 34, 15, true, JSlotMark, TextAnchor.MiddleCenter);
            }
        }
        static int CurrentSlots(ProfileSnapshot s) => Profile.Catalog.Unlocks.Where(u => u.effect.kind == ProgressionIds.PaletteSlotCount && s.Has(u.id)).Select(u => u.effect.value).DefaultIfEmpty(Profile.Catalog.BaseSlots).Max();
        static bool Owns(ProfileSnapshot s, int weapon) => Profile.Catalog.Unlocks.Any(u => u.effect.kind == ProgressionIds.FirstSlotWildcard && u.effect.weaponSlot == weapon && s.Has(u.id));

        void BuySelected()
        {
            if (hubOpen != workbench) return;
            // Success shows itself (button, list state, burst); only a refusal needs words.
            // A bought tier hands the selection to the next tier of the same node; a completed node keeps its last tier.
            if (Profile.TryPurchase(hubSelected, out var state)) { hubMessage = ""; flashStarted = Time.unscaledTime; PresentationSfx.Play(PresentationSfx.Unlock); hubSelected = BenchNodeCurrent(hubSelected); }
            else hubMessage = Profile.LastSaveError != null && state == UnlockState.Available ? "저장하지 못해 구매를 취소했습니다." : "";
            benchDirty = true; benchPreview.name = "";
        }

        // Purchase-complete burst (effects/hub-unlock, 8 frames x 80 ms, unscaled because the lobby is paused).
        void AnimateFlash()
        {
            if (flashStarted < 0) { benchFlash.enabled = false; return; }
            int frame = Mathf.FloorToInt((Time.unscaledTime - flashStarted) / .08f);
            if (frame >= 8) { flashStarted = -1; benchFlash.enabled = false; return; }
            benchFlash.sprite = Resources.Load<Sprite>("Hub/effects/unlock/" + (frame + 1).ToString("00")); benchFlash.enabled = benchFlash.sprite;
            benchFlash.transform.SetAsLastSibling();
        }

        // The sign starts one unarmed run; there is no village blueprint selection.
        void DepartAtSign()
        {
            if (departing || village.Departing || DepartureLocked) return;
            RunStartPlan plan;
            try { plan = Profile.PlanUnarmedRun(); }
            catch (ArgumentException error) { Debug.LogWarning("Departure refused: " + error.Message); RefreshHub(); return; }
            departing = true;
            var host = Chapter1RunHost.Ensure();
            host.PrepareNewRun(plan);
            CloseHubPanel();
            // Departure stops HubUpdate, so GuardPointer cannot release this click's combat lock next frame.
            pointerScope?.Dispose(); pointerScope = null;
            if (village) village.StartDeparture(() => host.EnterPreparedRun(true));
            else host.EnterPreparedRun();
        }

        // A .card of label / bold value rows split by 1 px rules (4 px under a row, 4 px over the next); an empty list
        // shows one muted line. Returns the card's bottom.
        float Table(RectTransform parent, float y, (string label, string value)[] rows, int size, float rowH, string empty)
        {
            float step = rowH + 9, height = rows.Length == 0 ? 46 : 28 + rows.Length * rowH + (rows.Length - 1) * 9;
            var card = Card(parent, 0, y, ColumnW, height);
            if (rows.Length == 0) { Words(empty, card.transform, 14, 14, 320, 18, 12, false, JMuted); return y + height; }
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0) Fill(card.transform, 14, 14 + i * step - 5, 320, 1, JRule);
                Words(rows[i].label, card.transform, 14, 14 + i * step, 200, rowH, size);
                Words(rows[i].value, card.transform, 14, 14 + i * step, 320, rowH, size, true, JInk, TextAnchor.MiddleRight);
            }
            return y + height;
        }

        void VillageMenuPrimary() { if (hubPaused) CloseHubPanel(); }
        void VillageMenuSecondary() => OpenTitleReturnQuestion(() => { CloseHubPanel(); Chapter1RunHost.Ensure().GoToTitle(); });

        void DisposeVillage()
        {
            StopRecordTransition();
            FinishVillageInk(false);
            hubScope?.Dispose(); hubScope = null; hubOpen = null; hubPaused = false; DisposeOpening();
            skyTalkScope?.Dispose(); skyTalkScope = null; skyKeeperLine = -1;
            if (village)
            {
                village.StationRequested -= OpenStation; village.ExternalPresentation = false;
                if (village.World && village.World.diagnostics) village.World.diagnostics.ExternalHud = false;
            }
        }
    }
}
