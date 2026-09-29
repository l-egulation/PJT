import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider, useQuery, useQueryClient } from "@tanstack/react-query";
import { AuthGate } from "./features/auth/AuthGate";
import { authApi } from "./features/auth/api";
import { AutoBattleRuntime } from "./features/battle/AutoBattleRuntime";
import { CozySharedNavigation } from "./features/battle/BattleHud";
import { BattleScreen } from "./features/battle/BattleScreen";
import { BattleHistoryScreen } from "./features/battle/BattleHistoryScreen";
import { CharacterWindow } from "./features/character/CharacterWindow";
import { CosmeticsScreen } from "./features/cosmetics/CosmeticsScreen";
import { EquipmentWindow } from "./features/equipment/EquipmentScreen";
import { GemsScreen } from "./features/gems/GemsScreen";
import { gemsApi } from "./features/gems/api";
import { characterApi } from "./features/character/api";
import { InventoryWindow } from "./features/inventory/InventoryScreen";
import { FirstClearRewardScreen } from "./features/first-clear-rewards/FirstClearRewardScreen";
import { MarketPlaza, type MarketMode } from "./features/market/MarketPlaza";
import { MailScreen } from "./features/mail/MailScreen";
import { ServerResetNotice } from "./features/notices/ServerResetNotice";
import { selectMarketItemForSale } from "./features/market/marketUiState";
import { RankingScreen } from "./features/ranking/RankingScreen";
import { ArenaScreen } from "./features/arena/ArenaScreen";
import { RaidScreen } from "./features/raid/RaidScreen";
import { raidApi } from "./features/raid/api";
import { MaterialPreferenceGate } from "./features/material-preference/MaterialPreferenceGate";
import { PictureInPictureController } from "./features/picture-in-picture/PictureInPictureController";
import { OfflineRewardPanel } from "./features/runtime/OfflineRewardPanel";
import { ProfileScreen, type ProfileTab } from "./features/profile/ProfileScreen";
import { RuntimeLifecycle } from "./features/runtime/RuntimeLifecycle";
import { applyBrowserSettings, loadBrowserSettings } from "./features/settings/browserSettings";
import { SkillWindow } from "./features/skills/SkillsScreen";
import { AdventurerTutorial } from "./features/tutorial/AdventurerTutorial";
import { ProgressiveTutorialRuntime } from "./features/tutorial/ProgressiveTutorialRuntime";
import { tutorialRuntimeSurface, type TutorialRuntimeSurface } from "./features/tutorial/tutorialRuntimeCatalog";
import {
  keepsBattleStageMounted,
  usesTopBarOnlyNavigation,
  openCharacterGachaFrom,
  openCosmeticsFrom,
  PRIMARY_NAV_ITEMS,
  readStoredScreen,
  SCREEN_STORAGE_KEY,
  isRaidRouteAvailable,
  resolveScreenForRaidAvailability,
  usesLegacyAppHeader,
  usesCozySharedNavigation,
  type AppScreen,
  type ManagementScreen,
} from "./navigationState";
import "./styles.css";
import "./app-shell.css";


const queryClient = new QueryClient();

function App() {
  const client = useQueryClient();
  const [screen, setScreen] = useState<AppScreen>(() => readStoredScreen(window.sessionStorage));
  const [profileOpen, setProfileOpen] = useState(false);
  const [profileInitialTab, setProfileInitialTab] = useState<ProfileTab>("profile");
  const profileTrigger = useRef<HTMLButtonElement>(null);
  const settingsTrigger = useRef<HTMLButtonElement>(null);
  const [characterOpen, setCharacterOpen] = useState(false);
  const characterTrigger = useRef<HTMLButtonElement>(null);
  const [characterInitialTab, setCharacterInitialTab] = useState<"stats" | "cosmetics">("stats");
  const [gachaReturn, setGachaReturn] = useState<AppScreen | null>(null);
  const [marketInitialMode, setMarketInitialMode] = useState<MarketMode>("buy");
  const contentMenu = useRef<HTMLDetailsElement>(null);
  const usesCozySharedHud = usesCozySharedNavigation(screen);
  const sharedSession = useQuery({
    queryKey: ["auth", "session"],
    queryFn: authApi.session,
    retry: false,
    enabled: usesCozySharedHud || screen === "battle",
    refetchInterval: usesCozySharedHud ? 5_000 : false,
  });
  const sharedAccount = sharedSession.data?.account ?? null;
  const sharedGemState = useQuery({
    queryKey: ["gems"],
    queryFn: gemsApi.state,
    retry: false,
    enabled: usesCozySharedHud,
  });
  const sharedCharacterStats = useQuery({
    queryKey: ["character-stats"],
    queryFn: characterApi.stats,
    retry: false,
    enabled: usesCozySharedHud,
  });
  const sharedRaidState = useQuery({
    queryKey: ["raid"],
    queryFn: raidApi.state,
    retry: false,
    enabled: usesCozySharedHud,
    staleTime: 5_000,
  });

  const displayedScreen = resolveScreenForRaidAvailability(screen, sharedRaidState.data);
  const displayedCozyRoute = usesCozySharedNavigation(displayedScreen) ? displayedScreen : undefined;

  useEffect(() => {
    if (screen === "raid" && sharedRaidState.isSuccess && !isRaidRouteAvailable(sharedRaidState.data)) setScreen("battle");
  }, [screen, sharedRaidState.data, sharedRaidState.isSuccess]);

  useEffect(() => {
    applyBrowserSettings(loadBrowserSettings());
  }, []);

  useEffect(() => {
    if (screen !== "cosmetics") window.sessionStorage.setItem(SCREEN_STORAGE_KEY, screen);
  }, [screen]);

  const openCharacter = () => {
    setCharacterInitialTab("stats");
    setCharacterOpen(true);
  };
  const showScreen = (next: ManagementScreen) => {
    contentMenu.current?.removeAttribute("open");
    if (next === "market") setMarketInitialMode("buy");
    setScreen(next);
  };
  const showAppScreen = (next: AppScreen) => {
    contentMenu.current?.removeAttribute("open");
    setScreen(next);
  };
  const openProfile = (tab: ProfileTab) => {
    setProfileInitialTab(tab);
    setProfileOpen(true);
  };
  const navigateFromBattleHud = (route: Parameters<React.ComponentProps<typeof BattleScreen>["onNavigate"]>[0]) => {
    if (route === "character") {
      openCharacter();
      return;
    }
    if (route === "settings") {
      openProfile("settings");
      return;
    }
    if (route === "raid") return;
    if (route === "cosmetics") {
      const next = openCosmeticsFrom(screen, gachaReturn);
      setGachaReturn(next.gachaReturn);
      showAppScreen(next.screen);
      return;
    }
    showScreen(route);
  };

  const leaveGacha = () => {
    showAppScreen(gachaReturn ?? "battle");
    setGachaReturn(null);
    setCharacterInitialTab("cosmetics");
    setCharacterOpen(true);
    void client.invalidateQueries({ queryKey: ["cosmetic-collection"] });
    void client.invalidateQueries({ queryKey: ["character-stats"] });
  };

  /* 점진형 튜토리얼이 다음 단계의 화면을 열 때 쓴다. 사용자가 메뉴를 누른 것과 같은 길을 탄다. */
  const openTutorialSurface = (next: TutorialRuntimeSurface) => {
    if (next === "cosmetics") {
      const target = openCosmeticsFrom(screen, gachaReturn);
      setGachaReturn(target.gachaReturn);
      showAppScreen(target.screen);
      return;
    }
    if (next === "market") setMarketInitialMode("buy");
    showAppScreen(next);
  };

  const sellInventoryItem = (itemId: string) => {
    selectMarketItemForSale(window.sessionStorage, itemId);
    setMarketInitialMode("sell");
    setScreen("market");
  };

  return (
    <AuthGate>
      <MaterialPreferenceGate>
        <main className={`game-shell ${displayedCozyRoute ? "cozy-management-shell" : ""} ${displayedScreen === "ranking" ? "cozy-ranking-shell" : ""} ${displayedScreen === "cosmetics" ? "cozy-cosmetics-shell" : ""}`}>
          <RuntimeLifecycle />
          <OfflineRewardPanel />
          <AutoBattleRuntime />
          {usesLegacyAppHeader(displayedScreen) && <header className="app-header app-shell-header">
            <h1>한짝</h1>
            <nav aria-label="관리 메뉴">
              {PRIMARY_NAV_ITEMS.map((item) => (
                <button
                  key={item.screen}
                  className={displayedScreen === item.screen ? "active" : ""}
                  onClick={() => {
                    if (item.screen === "cosmetics") {
                      const next = openCosmeticsFrom(screen, gachaReturn);
                      setGachaReturn(next.gachaReturn);
                      showAppScreen(next.screen);
                      return;
                    }
                    showAppScreen(item.screen);
                  }}
                >
                  {item.label}
                </button>
              ))}
              <details ref={contentMenu} className="content-menu">
                <summary aria-label="컨텐츠 메뉴 열기">컨텐츠<span aria-hidden="true">⌄</span></summary>
                <div className="content-menu-list">
                  <button type="button" aria-label="던전, 보석 던전과 현재 보스" onClick={() => showScreen("dungeon")}><strong>던전</strong><small>보석 던전과 현재 보스</small></button>
                  <button type="button" aria-label="레이드, 준비 중" disabled><strong>레이드</strong><small>준비 중</small></button>
                </div>
              </details>
              <button
                ref={profileTrigger}
                className={profileOpen && profileInitialTab === "profile" ? "active" : ""}
                onClick={() => openProfile("profile")}
                aria-haspopup="dialog"
              >
                마이페이지
              </button>
              <button
                ref={settingsTrigger}
                type="button"
                className={profileOpen && profileInitialTab === "settings" ? "active" : ""}
                onClick={() => openProfile("settings")}
                aria-haspopup="dialog"
              >설정</button>
            </nav>
          </header>}
          <PictureInPictureController visible={displayedScreen === "battle"} />
          <div hidden={!keepsBattleStageMounted(displayedScreen)}><BattleScreen onNavigate={navigateFromBattleHud} onOpenProfile={() => openProfile("profile")} /></div>
          {displayedCozyRoute && <CozySharedNavigation
            account={sharedAccount}
            activeRoute={displayedCozyRoute}
            onOpenProfile={() => openProfile("profile")}
            onNavigate={navigateFromBattleHud}
            profileTriggerRef={profileTrigger}
            gemContentUnlocked={sharedGemState.data?.unlocked}
            cosmeticContentUnlocked={sharedCharacterStats.data?.cosmeticsUnlocked === true}
            topBarOnly={usesTopBarOnlyNavigation(screen)}
            raid={sharedRaidState.data ? { featureAvailable: sharedRaidState.data.featureAvailable, unlockStageId: sharedRaidState.data.unlockStageId, unlocked: sharedRaidState.data.unlocked } : undefined}
          />}
          {displayedScreen === "battleHistory" && <BattleHistoryScreen />}
          {displayedScreen === "inventory" && <InventoryWindow open onClose={() => showScreen("battle")} onSell={sellInventoryItem} />}
          {displayedScreen === "equipment" && <EquipmentWindow open onClose={() => showScreen("battle")} />}
          {displayedScreen === "skills" && <SkillWindow open onClose={() => showScreen("battle")} />}
          {displayedScreen === "market" && <MarketPlaza initialMode={marketInitialMode} onClose={() => showScreen("battle")} />}
          {displayedScreen === "mail" && <MailScreen onClose={() => showScreen("battle")} />}
          {displayedScreen === "gems" && <GemsScreen mode="manage" onClose={() => showScreen("battle")} />}
          {displayedScreen === "dungeon" && <GemsScreen mode="dungeon" onOpenGems={() => showScreen("gems")} />}
          {displayedScreen === "raid" && <RaidScreen />}
          {displayedScreen === "arena" && <ArenaScreen />}
          {displayedScreen === "ranking" && <RankingScreen />}
          {displayedScreen === "firstClearRewards" && <div className="cozy-management-content first-clear-reward-content"><FirstClearRewardScreen /></div>}
          {displayedScreen === "cosmetics" && <CosmeticsScreen onClose={leaveGacha} />}
          {usesLegacyAppHeader(displayedScreen) && <nav className="character-launcher" aria-label="성장 메뉴">
            <button ref={characterTrigger} onClick={openCharacter} aria-haspopup="dialog">캐릭터</button>
          </nav>}
          <CharacterWindow
            open={characterOpen}
            initialTab={characterInitialTab}
            triggerRef={characterTrigger}
            onClose={() => setCharacterOpen(false)}
            onGacha={() => {
              const next = openCharacterGachaFrom(screen, gachaReturn);
              setGachaReturn(next.gachaReturn);
              setCharacterOpen(false);
              showAppScreen(next.screen);
            }}
          />
          <ServerResetNotice />
          <ProfileScreen
            open={profileOpen}
            triggerRef={profileInitialTab === "settings" ? settingsTrigger : profileTrigger}
            initialTab={profileInitialTab}
            onClose={() => setProfileOpen(false)}
          />
          {sharedAccount && <AdventurerTutorial accountId={sharedAccount.accountId} active={screen === "battle" && !profileOpen && !characterOpen} />}
          {sharedAccount && <ProgressiveTutorialRuntime
            accountId={sharedAccount.accountId}
            surface={tutorialRuntimeSurface(displayedScreen)}
            blocked={profileOpen || characterOpen}
            onNavigate={openTutorialSurface}
          />}
        </main>
      </MaterialPreferenceGate>
    </AuthGate>
  );
}

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </React.StrictMode>,
);
