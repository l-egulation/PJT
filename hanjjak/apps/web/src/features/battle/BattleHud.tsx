import { useEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode, type RefObject } from "react";
import { useQuery } from "@tanstack/react-query";
import type { AccountIdentity } from "../auth/api";
import riceIcon from "../equipment/assets-cozy-pixel/currency-rice-gold-256.png";
import heartIcon from "../character/assets-cozy-pixel/heart-icon-128.png";
import sproutIcon from "../character/assets-cozy-pixel/sprout-icon-32.png";
import swordIcon from "../character/assets-stats-v2/icon-attack.png";
import profileIcons from "../profile/assets-cozy-pixel-v2/profile-info-icons-v3.png";
import cornKernelIcon from "../equipment/assets/material-ranks/corn-kernel-f.png";
import cornMiniIcon from "../equipment/assets/material-ranks/corn-mini-d.png";
import cornIcon from "../equipment/assets/material-ranks/corn-c.png";
import cornGoldenIcon from "../equipment/assets/material-ranks/corn-golden-b.png";
import cornLegendaryIcon from "../equipment/assets/material-ranks/corn-legendary-a.png";
import potatoFragmentIcon from "../equipment/assets/material-ranks/potato-fragment-f.png";
import potatoMiniIcon from "../equipment/assets/material-ranks/potato-mini-d.png";
import potatoIcon from "../equipment/assets/material-ranks/potato-c.png";
import potatoGoldenIcon from "../equipment/assets/material-ranks/potato-golden-b.png";
import potatoLegendaryIcon from "../equipment/assets/material-ranks/potato-legendary-a.png";
import sweetPotatoFragmentIcon from "../equipment/assets/material-ranks/sweet-potato-fragment-f.png";
import sweetPotatoMiniIcon from "../equipment/assets/material-ranks/sweet-potato-mini-d.png";
import sweetPotatoIcon from "../equipment/assets/material-ranks/sweet-potato-c.png";
import sweetPotatoGoldenIcon from "../equipment/assets/material-ranks/sweet-potato-golden-b.png";
import sweetPotatoLegendaryIcon from "../equipment/assets/material-ranks/sweet-potato-legendary-a.png";
import battleLogIcon from "../../shared/assets/cozy-hud-v1/icons/battle-log-scroll-clock.png";
import stageVictoryIcon from "../../shared/assets/cozy-hud-v1/icons/battle-event-victory.png";
import stageFailureIcon from "../../shared/assets/cozy-hud-v1/icons/battle-event-failure.png";
import bottomItemsIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-items.png";
import bottomSkillsIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-skills.png";
import bottomGemsIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-gems.png";
import chopstickHeadIcon from "../../shared/assets/cozy-hud-v1/icons/chopstick-head.png";
import monsterProgressIcon from "../../shared/assets/cozy-hud-v1/icons/monster-progress.png";
import monsterBossIcon from "../../shared/assets/cozy-hud-v1/icons/monster-progress-boss.png";
import rewardSkillbookIcon from "../../shared/assets/cozy-hud-v1/icons/reward-skillbook.png";
import rewardsEmptyIcon from "../../shared/assets/cozy-hud-v1/icons/rewards-empty.png";
import utilityCosmeticsButton from "./assets-utility-nav-v1/utility-cosmetics.png";
import utilityBattleReturnButton from "./assets-utility-nav-v1/utility-battle-return.png";
import utilityContentButton from "./assets-utility-nav-v1/utility-content.png";
import utilityMarketButton from "./assets-utility-nav-v1/utility-market.png";
import utilityRankingButton from "./assets-utility-nav-v1/utility-ranking.png";
import notificationMessageButton from "./assets-utility-nav-v1/notification-message-button.png";
import utilitySettingsButton from "./assets-utility-nav-v1/utility-settings.png";
import trophyIcon from "../../shared/assets/cozy-hud-v1/icons/nav-ranking.png";
import paperCloseButton from "../../shared/assets/cozy-paper-v2/close-button.png";
import contentTitlePlaque from "./assets-content-picker-v1/content-title-plaque.png";
import contentCloseButton from "./assets-content-picker-v1/content-close-button.png";
import dungeonCardArt from "./assets-content-picker-v1/dungeon-card.png";
import raidCardArt from "./assets-content-picker-v1/raid-card.png";
import contentMoveButton from "./assets-content-picker-v1/move-button.png";
import contentComingSoonButton from "./assets-content-picker-v1/coming-soon-button.png";
import contentDungeonArt from "../gems/assets-cozy-pixel-v2/icons/icon-gem-chest.png";
import contentRaidArt from "../character/assets-stats-v2/icon-attack.png";
import type { FirstClearRewardResult } from "../first-clear-rewards/api";
import type { BattleHistoryEvent, RewardLine, StageSummary } from "./api";
import { battleApi } from "./api";
import { BattleHistoryGains, battleHistoryDateTime, battleHistoryDuration, battleHistoryResultEvents, battleHistoryResultLabel, battleHistoryStageEnteredAt, battleHistoryTargetLabel } from "./BattleHistoryScreen";
import { mailApi } from "../mail/api";
import { giftMailOnly } from "../mail/MailScreen";
import { formatStageId } from "./stageLabel";
import { ChapterSelectScreen } from "./ChapterSelectScreen";
import { chapterOfStage } from "./chapterCatalog";
import "./BattleHud.css";

export type BattleHudRoute = "battle" | "battleHistory" | "character" | "equipment" | "inventory" | "skills" | "gems" | "dungeon" | "raid" | "market" | "mail" | "ranking" | "arena" | "firstClearRewards" | "settings" | "cosmetics";

type NavigationProps = {
  onNavigate: (route: BattleHudRoute) => void;
  activeRoute?: BattleHudRoute;
  gemContentUnlocked?: boolean;
  cosmeticContentUnlocked?: boolean;
  raid?: { featureAvailable: boolean; unlockStageId: string; unlocked: boolean };
};

type UtilityIconName = "cosmetics" | "content" | "market" | "ranking" | "mail" | "settings";

const UTILITY_NAV: ReadonlyArray<{ id: UtilityIconName; route?: BattleHudRoute; label: string; icon: string; disabled?: boolean; fullArt?: boolean }> = [
  { id: "cosmetics", route: "cosmetics", label: "치장 뽑기", icon: utilityCosmeticsButton, fullArt: true },
  { id: "content", label: "콘텐츠", icon: utilityContentButton, fullArt: true },
  { id: "market", route: "market", label: "거래소", icon: utilityMarketButton, fullArt: true },
  { id: "ranking", route: "ranking", label: "랭킹", icon: utilityRankingButton, fullArt: true },
  { id: "mail", route: "mail", label: "메시지", icon: notificationMessageButton, fullArt: true },
  { id: "settings", route: "settings", label: "설정", icon: utilitySettingsButton, fullArt: true },
];

const BATTLE_RETURN_NAV = { route: "battle", label: "전투", icon: utilityBattleReturnButton } as const;

const GROWTH_NAV = [
  { route: "character", label: "캐릭터", icon: chopstickHeadIcon },
  { route: "equipment", label: "장비", icon: swordIcon },
  { route: "inventory", label: "아이템", icon: bottomItemsIcon },
  { route: "skills", label: "스킬", icon: bottomSkillsIcon },
  { route: "gems", label: "보석", icon: bottomGemsIcon },
] as const;

function ContentCardArt({ kind }: { kind: "dungeon" | "raid" }) {
  return <img className="cozy-content-card-art" src={kind === "dungeon" ? contentDungeonArt : contentRaidArt} alt="" aria-hidden="true" />;
}

export function BattleUtilityNavigation({ onNavigate, activeRoute, gemContentUnlocked, cosmeticContentUnlocked = false, raid = { featureAvailable: false, unlockStageId: "", unlocked: false } }: NavigationProps) {
  const [contentOpen, setContentOpen] = useState(false);
  /* 무엇이 왔는지는 열어 봐야 알지만, 왔다는 것만은 단추에서 바로 보여야 한다. */
  const mails = useQuery({ queryKey: ["mails"], queryFn: () => mailApi.list(), retry: false, staleTime: 30_000 });
  const unreadMail = giftMailOnly(mails.data?.items ?? []).filter((mail) => !mail.claimed).length;
  const hasBattleReturn = activeRoute && activeRoute !== "battle";
  const items = hasBattleReturn ? [BATTLE_RETURN_NAV, ...UTILITY_NAV] : UTILITY_NAV;

  useEffect(() => {
    if (!contentOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => event.key === "Escape" && setContentOpen(false);
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [contentOpen]);

  return <>
    <nav
      className={`cozy-utility-nav${hasBattleReturn ? " has-battle-return" : ""}`}
      aria-label="주요 관리 메뉴"
      style={{ "--cozy-utility-columns": items.length } as CSSProperties}
    >
      {items.map((item) => {
        if (!("id" in item)) return <button
          key={item.route}
          type="button"
          data-route={item.route}
          className={[item.route === activeRoute ? "active" : "", "battle-return", "has-full-art"].filter(Boolean).join(" ")}
          aria-current={item.route === activeRoute ? "page" : undefined}
          aria-label={item.label}
          onClick={() => onNavigate(item.route)}
        >
          <img src={item.icon} alt="" />
        </button>;

        const locked = item.route === "cosmetics" && !cosmeticContentUnlocked;
        const contentActive = item.id === "content" && (activeRoute === "dungeon" || activeRoute === "raid");
        const active = contentActive || item.route === activeRoute;
        const disabled = locked || item.disabled;
        return <button
          key={item.id}
          type="button"
          data-route={item.id}
          className={[active ? "active" : "", item.fullArt ? "has-full-art" : ""].filter(Boolean).join(" ") || undefined}
          aria-current={active ? "page" : undefined}
          aria-label={locked ? `${item.label} 1-5 해금` : item.disabled ? `${item.label} 준비 중` : item.label}
          disabled={disabled}
          title={locked ? `${item.label}: 1-5 최초 클리어 후 해금됩니다.` : item.disabled ? `${item.label}: 준비 중입니다.` : undefined}
          onClick={() => {
            if (disabled) return;
            if (item.id === "content") setContentOpen(true);
            else if (item.route) onNavigate(item.route);
          }}
        >
          <img className="cozy-utility-art" src={item.icon} alt="" />
          {item.id === "mail" && unreadMail > 0 && <span className="cozy-utility-dot" aria-hidden="true" />}
        </button>;
      })}
    </nav>
    {contentOpen && <div className="cozy-content-picker-backdrop" onMouseDown={(event) => event.target === event.currentTarget && setContentOpen(false)}>
      <section className="cozy-content-picker" role="dialog" aria-modal="true" aria-labelledby="cozy-content-picker-title">
        <header>
          <h2 id="cozy-content-picker-title" style={{ backgroundImage: `url(${contentTitlePlaque})` }}>콘텐츠 선택</h2>
          <button type="button" className="cozy-content-picker-close" onClick={() => setContentOpen(false)} aria-label="콘텐츠 선택 닫기">
            <img src={contentCloseButton} alt="" aria-hidden="true" />
          </button>
        </header>
        <div className="cozy-content-picker-grid">
          <button
            type="button"
            className="cozy-content-card is-dungeon"
            style={{ backgroundImage: `url(${dungeonCardArt})` }}
            disabled={gemContentUnlocked === false}
            onClick={() => { setContentOpen(false); onNavigate("dungeon"); }}
          >
            <ContentCardArt kind="dungeon" />
            <span className="cozy-content-card-copy"><strong>던전</strong><small>매일 도전하고 보석을 얻어요</small></span>
            <b className="cozy-content-card-action" style={{ backgroundImage: `url(${contentMoveButton})` }}>{gemContentUnlocked === false ? "1-5 클리어 후 열림" : "입장 가능"}</b>
          </button>
          {/* 레이드는 서버가 열어 줄 때만 들어간다. 아직 붙지 않은 서버에서는 준비 중으로 남는다. */}
          <button
            type="button"
            className="cozy-content-card is-raid"
            style={{ backgroundImage: `url(${raidCardArt})` }}
            disabled={!raid.featureAvailable || !raid.unlocked}
            onClick={() => { setContentOpen(false); onNavigate("raid"); }}
          >
            <ContentCardArt kind="raid" />
            <span className="cozy-content-card-copy"><strong>레이드</strong><small>강한 보스를 함께 상대해요</small></span>
            <b className="cozy-content-card-action" style={{ backgroundImage: `url(${raid.featureAvailable && raid.unlocked ? contentMoveButton : contentComingSoonButton})` }}>
              {!raid.featureAvailable ? "준비 중" : raid.unlocked ? "입장 가능" : `${raid.unlockStageId} 클리어 후 열림`}
            </b>
          </button>
        </div>
      </section>
    </div>}
  </>;
}

export function BattleGrowthNavigation({ onNavigate, activeRoute, gemContentUnlocked }: NavigationProps) {
  return <nav className="cozy-growth-nav cozy-paper" aria-label="성장 메뉴">
    {GROWTH_NAV.map((item, index) => {
      /* 조회가 끝나기 전 버튼을 잠가 버리면 정상 계정도 첫 클릭이 먹지 않는다.
         명시적으로 잠김을 확인한 경우에만 막고, 보석 화면 자체가 최종 상태를 안내한다. */
      const disabled = item.route === "gems" && gemContentUnlocked === false;
      const active = !disabled && (item.route === activeRoute || (activeRoute === undefined && index === 0));
      return <button
        key={item.route}
        type="button"
        data-route={item.route}
        className={active ? "active" : undefined}
        aria-current={active ? "page" : undefined}
        disabled={disabled}
        title={disabled ? `${item.label}: 1-5 최초 클리어 후 해금됩니다.` : undefined}
        onClick={() => !disabled && onNavigate(item.route)}
      >
        <img src={item.icon} alt="" />
        <span className="sr-only">{item.label}</span>
        {disabled && <small>1-5 해금</small>}
      </button>;
    })}
  </nav>;
}

export function BattleProfileSummary({ account, onOpen, triggerRef }: { account: AccountIdentity | null; onOpen: () => void; triggerRef?: RefObject<HTMLButtonElement | null> }) {
  return <button ref={triggerRef} type="button" className="cozy-profile-summary cozy-paper" onClick={onOpen} aria-label="내 정보 열기">
    <img className="cozy-profile-avatar" src={chopstickHeadIcon} alt="" />
    <span className="cozy-profile-copy">
      <strong>{account?.nickname ?? "한짝"}</strong>
      <small><span className="cozy-rice-coin" style={{ "--cozy-profile-icons": `url(${profileIcons})` } as CSSProperties} role="img" aria-label="쌀" />{(account?.rice ?? 0).toLocaleString()}</small>
    </span>
  </button>;
}

export function CozySharedNavigation({ account, activeRoute, onOpenProfile, onNavigate, profileTriggerRef, gemContentUnlocked, cosmeticContentUnlocked = false, topBarOnly = false, raid }: {
  account: AccountIdentity | null;
  activeRoute: BattleHudRoute;
  onOpenProfile: () => void;
  onNavigate: NavigationProps["onNavigate"];
  profileTriggerRef?: RefObject<HTMLButtonElement | null>;
  gemContentUnlocked?: boolean;
  cosmeticContentUnlocked?: boolean;
  topBarOnly?: boolean;
  raid?: NavigationProps["raid"];
}) {
  return <div className={`cozy-shared-navigation${topBarOnly ? " is-top-bar-only" : ""}`}>
    {!topBarOnly && <BattleProfileSummary account={account} onOpen={onOpenProfile} triggerRef={profileTriggerRef} />}
    <BattleUtilityNavigation activeRoute={activeRoute} onNavigate={onNavigate} gemContentUnlocked={gemContentUnlocked} cosmeticContentUnlocked={cosmeticContentUnlocked} raid={raid} />
    {!topBarOnly && <BattleGrowthNavigation activeRoute={activeRoute} onNavigate={onNavigate} gemContentUnlocked={gemContentUnlocked} />}
  </div>;
}

const HISTORY_LABELS: Record<BattleHistoryEvent["type"], string> = {
  STAGE_ENTERED: "스테이지 입장",
  STAGE_CLEARED: "승리",
  STAGE_FAILED: "패배",
  DUNGEON_ENTERED: "던전 입장",
  RETURNED: "전투 복귀",
};

function historyIcon(event: BattleHistoryEvent): string {
  if (event.type === "STAGE_CLEARED") return stageVictoryIcon;
  return stageFailureIcon;
}

export function BattleHistoryPopoverItem({ event }: { event: BattleHistoryEvent }) {
  const snapshot = event.combatSnapshot;
  const enteredAt = battleHistoryStageEnteredAt(event);
  const finalBossStage = event.stageId?.endsWith("-10") ?? false;
  return <li className={`is-${event.type.toLowerCase()}`}>
    <img src={historyIcon(event)} alt="" />
    <div>
      <header className="cozy-history-entry-header">
        <p><strong>{HISTORY_LABELS[event.type]}</strong><b>{battleHistoryTargetLabel(event)}</b></p>
        <time dateTime={event.occurredAt}>
          <small>{event.type === "STAGE_CLEARED" ? "클리어 시각" : "패배 시각"}</small>
          <b>{battleHistoryDateTime(event.occurredAt)}</b>
        </time>
      </header>
      {snapshot && <dl className="cozy-history-result" aria-label="전투 결과 요약">
        {enteredAt && <div><dt>입장</dt><dd>{battleHistoryDateTime(enteredAt)}</dd></div>}
        {snapshot.elapsedTicks !== undefined && <div><dt>소요</dt><dd>{battleHistoryDuration(snapshot.elapsedTicks)}</dd></div>}
        {snapshot.remainingHp !== undefined && <div><dt>남은 HP</dt><dd>{snapshot.remainingHp.toLocaleString()}</dd></div>}
        {event.type === "STAGE_FAILED" && <div><dt>원인</dt><dd>{battleHistoryResultLabel(event)}</dd></div>}
        {event.type === "STAGE_FAILED" && snapshot.defeatedNormals !== undefined && <div><dt>처치</dt><dd>{finalBossStage ? "일반 몬스터 없음" : `${snapshot.defeatedNormals.toLocaleString()} / 20`}</dd></div>}
        {event.type === "STAGE_FAILED" && snapshot.lastEnemyRemainingHp !== undefined && <div><dt>상대 HP</dt><dd>{snapshot.lastEnemyRemainingHp.toLocaleString()}</dd></div>}
      </dl>}
      {snapshot && <BattleHistoryGains event={event} compact />}
      {snapshot?.rewards && snapshot.rewards.length > 0 && <small className="cozy-history-reward-items">
        <b>지급 아이템</b>
        <span>{snapshot.rewards.filter((reward) => reward.quantity > 0).map((reward) => `${reward.displayName} +${reward.quantity.toLocaleString()}`).join(" · ")}</span>
      </small>}
    </div>
  </li>;
}

export function BattleHistoryPopover() {
  const [open, setOpen] = useState(false);
  const history = useQuery({ queryKey: ["battle-history"], queryFn: battleApi.history, retry: false, enabled: open });
  const results = battleHistoryResultEvents(history.data);

  return <div className={`cozy-history ${open ? "is-open" : ""}`}>
    <button type="button" className="cozy-history-trigger cozy-paper" aria-expanded={open} onClick={() => setOpen((value) => !value)}>
      <img src={battleLogIcon} alt="" />
      <span>전투 기록</span>
      <b aria-hidden="true"><i className={`cozy-chevron ${open ? "is-previous" : "is-next"}`} /></b>
    </button>
    {open && <section className="cozy-history-panel cozy-paper" aria-labelledby="cozy-history-title">
      <header>
        <div><h3 id="cozy-history-title">전투 기록</h3><span>최근 승패 기록 {results.length} / 50</span></div>
        {/* 다른 창과 같은 닫기 그림을 쓴다. */}
        <button type="button" className="cozy-history-close" onClick={() => setOpen(false)} aria-label="전투 기록 닫기">
          <img src={paperCloseButton} alt="" aria-hidden="true" />
        </button>
      </header>
      {history.isLoading && <p className="cozy-history-message" role="status">기록을 불러오는 중입니다.</p>}
      {history.error && <p className="cozy-history-message" role="alert">기록을 불러오지 못했습니다. <button type="button" onClick={() => history.refetch()}>다시 시도</button></p>}
      {history.data && results.length === 0 && <p className="cozy-history-message">아직 완료된 전투 기록이 없습니다.</p>}
      {results.length > 0 && <ol>
        {results.slice(0, 50).map((event) => <BattleHistoryPopoverItem key={event.eventId} event={event} />)}
      </ol>}
    </section>}
  </div>;
}

type StageProgressProps = {
  stages: StageSummary[];
  selectedStageId: string;
  remainingSeconds: number | null;
  defeatedNormals: number;
  bossOnly: boolean;
  bossPhase: boolean;
  stageComplete: boolean;
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  pendingIdleMode?: "AUTO_PROGRESS" | "REPEAT_STAGE" | null;
  idleModeSaving: boolean;
  repeatEligible: boolean;
  bossHealth?: { name: string; hp: number; maxHp: number } | null;
  onSelectStage: (stageId: string) => void;
  onChangeMode: (mode: "AUTO_PROGRESS" | "REPEAT_STAGE") => void;
};

export function battleRemainingSeconds(durationMilliseconds: number, tickDurationMilliseconds: number, elapsedTicks: number): number {
  const remainingMilliseconds = durationMilliseconds - Math.max(0, elapsedTicks) * Math.max(1, tickDurationMilliseconds);
  return Math.max(0, Math.floor(remainingMilliseconds / 1_000));
}

/**
 * 남은 시간은 전투 틱이 아니라 시계로 센다. 틱은 사건이 올 때만 움직이고 화면이
 * 예전 사건으로 되돌아가면 뒤로도 가서, 48초였다가 49초가 되는 일이 있었다.
 */
export function battleRemainingSecondsAt(completableAt: string | null | undefined, now: number): number {
  const deadline = completableAt ? Date.parse(completableAt) : Number.NaN;
  if (Number.isNaN(deadline)) return 0;
  return Math.max(0, Math.ceil((deadline - now) / 1_000));
}

/** 초 단위가 실제로 바뀔 때만 다시 그린다. */
export function useSecondsClock(active: boolean): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!active) return;
    setNow(Date.now());
    const timer = window.setInterval(() => setNow(Date.now()), 200);
    return () => window.clearInterval(timer);
  }, [active]);
  return now;
}
export function useCountdownSeconds(active: boolean, durationMilliseconds: number, resetKey: string | null | undefined): number | null {
  const now = useSecondsClock(active);
  const [deadline, setDeadline] = useState<number | null>(null);
  useEffect(() => {
    setDeadline(active ? Date.now() + Math.max(0, durationMilliseconds) : null);
  }, [active, durationMilliseconds, resetKey]);
  if (!active) return null;
  return Math.max(0, Math.ceil(((deadline ?? now + durationMilliseconds) - now) / 1_000));
}


export function BattleStageProgress({ stages, selectedStageId, remainingSeconds, defeatedNormals, bossOnly, bossPhase, stageComplete, idleMode, pendingIdleMode, idleModeSaving, repeatEligible, bossHealth, onSelectStage, onChangeMode }: StageProgressProps) {
  /* 어느 챕터로 갈지는 챕터 화면이, 그 안 몇 번째 칸을 돌지는 아래 목록이 맡는다. */
  const [chapterOpen, setChapterOpen] = useState(false);
  const currentChapter = chapterOfStage(selectedStageId);
  const chapterStages = stages.filter((stage) => chapterOfStage(stage.stageId) === currentChapter);
  const finalBossStage = bossOnly;
  const displayedIdleMode = pendingIdleMode ?? idleMode;
  const progress = Math.max(0, Math.min(20, defeatedNormals));
  const progressMax = finalBossStage ? 1 : 20;
  const progressValue = finalBossStage ? Number(stageComplete) : progress;
  const bossMaxHp = Math.max(1, bossHealth?.maxHp ?? 1);
  const bossHp = Math.max(0, Math.min(bossHealth?.hp ?? 0, bossMaxHp));
  return <div className="cozy-stage-cluster">
    <section className="cozy-stage-progress cozy-paper" aria-label="현재 스테이지 진행">
      <div className="cozy-stage-heading">
        <strong>STAGE {formatStageId(selectedStageId)}</strong>
        {remainingSeconds !== null && <time aria-label={`보스 남은 제한시간 ${Math.max(0, Math.trunc(remainingSeconds))}초`}>
          <small>남은 제한시간</small>
          <b>{Math.max(0, Math.trunc(remainingSeconds))}초</b>
        </time>}
      </div>
      <div className={`cozy-stage-meter ${bossOnly ? "boss-only" : ""}`}>
        <img src={finalBossStage || bossPhase ? monsterBossIcon : monsterProgressIcon} alt="" />
        <div className="cozy-stage-track" role="progressbar" aria-label={finalBossStage ? "최종 보스 처치 진행" : "일반 몬스터 처치 진행"} aria-valuemin={0} aria-valuemax={progressMax} aria-valuenow={progressValue}>
          <i style={{ width: `${stageComplete ? 100 : finalBossStage ? 0 : progress / 20 * 100}%` }} />
        </div>
        <b>{finalBossStage ? "최종 보스" : `${progress} / 20`}</b>
      </div>
    </section>
    <div className="cozy-stage-actions">
      <button type="button" className="cozy-stage-picker-button" aria-haspopup="dialog" onClick={() => setChapterOpen(true)}>
        챕터 선택 <i className="cozy-chevron is-next" aria-hidden="true" />
      </button>
      <details className="cozy-stage-picker">
        <summary>스테이지 <i className="cozy-chevron is-next" aria-hidden="true" /></summary>
        <div className="cozy-stage-picker-panel">
          <ul>{chapterStages.map((stage) => <li key={stage.stageId}>
            <button
              type="button"
              className={stage.stageId === selectedStageId ? "is-current" : undefined}
              disabled={!stage.unlocked}
              aria-label={`${formatStageId(stage.stageId)} ${stage.unlocked ? `클리어 ${stage.clearCount}회` : "잠김"}`}
              onClick={() => onSelectStage(stage.stageId)}
            >{formatStageId(stage.stageId)}</button>
          </li>)}</ul>
        </div>
      </details>
      <button
        type="button"
        className={`cozy-stage-mode ${displayedIdleMode === "AUTO_PROGRESS" ? "active" : ""}`}
        aria-label="자동 진행"
        aria-pressed={displayedIdleMode === "AUTO_PROGRESS"}
        disabled={idleModeSaving}
        onClick={() => onChangeMode("AUTO_PROGRESS")}
      >
        <span>자동</span>
      </button>
      <button
        type="button"
        className={`cozy-stage-mode ${displayedIdleMode === "REPEAT_STAGE" ? "active" : ""}`}
        aria-label="스테이지 반복"
        aria-pressed={displayedIdleMode === "REPEAT_STAGE"}
        disabled={idleModeSaving || !repeatEligible}
        onClick={() => onChangeMode("REPEAT_STAGE")}
      >
        <span>반복</span>
      </button>
    </div>
    {chapterOpen && <ChapterSelectScreen
      stages={stages}
      selectedStageId={selectedStageId}
      onSelectStage={onSelectStage}
      onClose={() => setChapterOpen(false)}
    />}
    {bossHealth && <section className="cozy-boss-health" aria-label={`${bossHealth.name} 보스 체력 정보`}>
      <div className="cozy-boss-health-copy"><strong>{bossHealth.name}</strong><span>{bossHp.toLocaleString()} / {bossHealth.maxHp.toLocaleString()}</span></div>
      <div className="cozy-boss-health-track" role="progressbar" aria-label={`${bossHealth.name} 보스 체력`} aria-valuemin={0} aria-valuemax={bossMaxHp} aria-valuenow={bossHp}>
        <i style={{ width: `${bossHp / bossMaxHp * 100}%` }} />
      </div>
    </section>}
  </div>;
}

type RewardMeta = { label: string; icon?: string };

const MATERIAL_REWARD_META: Record<string, RewardMeta> = {
  POTATO_M1: { label: "감자 한 조각", icon: potatoFragmentIcon },
  POTATO_M2: { label: "미니 감자", icon: potatoMiniIcon },
  POTATO_M3: { label: "감자", icon: potatoIcon },
  POTATO_M4: { label: "황금 감자", icon: potatoGoldenIcon },
  POTATO_M5: { label: "전설 감자", icon: potatoLegendaryIcon },
  SWEET_POTATO_M1: { label: "고구마 한 조각", icon: sweetPotatoFragmentIcon },
  SWEET_POTATO_M2: { label: "미니 고구마", icon: sweetPotatoMiniIcon },
  SWEET_POTATO_M3: { label: "고구마", icon: sweetPotatoIcon },
  SWEET_POTATO_M4: { label: "황금 고구마", icon: sweetPotatoGoldenIcon },
  SWEET_POTATO_M5: { label: "전설 고구마", icon: sweetPotatoLegendaryIcon },
  CORN_M1: { label: "옥수수 한 알", icon: cornKernelIcon },
  CORN_M2: { label: "미니 옥수수", icon: cornMiniIcon },
  CORN_M3: { label: "옥수수", icon: cornIcon },
  CORN_M4: { label: "황금 옥수수", icon: cornGoldenIcon },
  CORN_M5: { label: "전설 옥수수", icon: cornLegendaryIcon },
};

export function rewardMeta(itemId: string): RewardMeta {
  const material = MATERIAL_REWARD_META[itemId];
  if (material) return material;
  if (itemId.startsWith("POTATO")) return { label: "감자", icon: potatoIcon };
  if (itemId.startsWith("SWEET_POTATO")) return { label: "고구마", icon: sweetPotatoIcon };
  if (itemId.startsWith("CORN")) return { label: "옥수수", icon: cornIcon };
  if (itemId.startsWith("skillbook:")) {
    const [, skillId, gradeId] = itemId.split(":");
    const skillName: Record<string, string> = {
      active_heavy: "한짝의 일격",
      active_dot: "마! 쫄이나",
      active_haste: "잘게 더 잘게!",
      active_basic_amp: "화력 최대로!",
      passive_critical: "회심의 간",
      passive_all_damage: "오늘의 특선",
    };
    const gradeName: Record<string, string> = { normal: "노말", rare: "희귀", epic: "영웅", legendary: "전설" };
    const name = skillName[skillId];
    return { label: name ? `${gradeName[gradeId] ?? gradeId} ${name} 비법서` : "스킬 비법서", icon: rewardSkillbookIcon };
  }
  if (itemId.includes("SKILL") || itemId.includes("BOOK")) return { label: "스킬 비법서", icon: rewardSkillbookIcon };
  return { label: itemId };
}

type RewardEntry = { key: string; label: string; quantity: number; icon?: string; warning?: string };

export function FirstClearRewardNotice({ reward, onClose }: { reward: FirstClearRewardResult; onClose?: () => void }) {
  const lines = [
    ...(reward.riceGranted > 0 ? [{ key: "rice", name: "쌀 코인", quantity: reward.riceGranted, icon: riceIcon, pending: false }] : []),
    ...reward.grantedItems.map((item) => ({ key: `granted:${item.itemId}`, name: item.displayName, quantity: item.quantity, icon: rewardMeta(item.itemId).icon, pending: false })),
    ...reward.pendingItems.map((item) => ({ key: `pending:${item.itemId}`, name: item.displayName, quantity: item.quantity, icon: rewardMeta(item.itemId).icon, pending: true })),
  ];
  const scroll = useRef<HTMLDivElement>(null);
  /* 칸이 넘치면 아래에 몇 개가 더 있는지 알려 준다 — 스크롤 막대만으로는 눈에 띄지 않는다. */
  const [below, setBelow] = useState(0);
  useEffect(() => {
    const box = scroll.current;
    if (!box) return;
    const measure = () => {
      const limit = box.scrollTop + box.clientHeight + 4;
      setBelow(Array.from(box.querySelectorAll("li")).filter((item) => item.offsetTop + item.offsetHeight > limit).length);
    };
    measure();
    box.addEventListener("scroll", measure, { passive: true });
    const observer = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(measure);
    observer?.observe(box);
    return () => { box.removeEventListener("scroll", measure); observer?.disconnect(); };
  }, [lines.length]);

  return <div className="cozy-first-clear-backdrop" onMouseDown={(event) => onClose && event.target === event.currentTarget && onClose()}>
    <section className="cozy-first-clear-notice" role="dialog" aria-modal="true" aria-labelledby="cozy-first-clear-title">
      {onClose && <button type="button" className="cozy-first-clear-close" aria-label="최초 클리어 보상 닫기" onClick={onClose}>
        <img src={paperCloseButton} alt="" aria-hidden="true" />
      </button>}
      <h2 id="cozy-first-clear-title"><img src={trophyIcon} alt="" aria-hidden="true" />최초 클리어 보상</h2>
      <p className="cozy-first-clear-stage">STAGE {formatStageId(reward.stageId)}</p>

      <p className="cozy-first-clear-summary">
        <span>총 <b>{lines.length}</b>종</span>
        {reward.unlockedSkillId && <><i aria-hidden="true" /><span><img src={rewardSkillbookIcon} alt="" aria-hidden="true" />새 스킬 해금</span></>}
      </p>

      <div className="cozy-first-clear-scroll" ref={scroll}>
        <ul className="cozy-first-clear-lines">{lines.map((line) => <li key={line.key} className={line.pending ? "is-pending" : undefined}>
          {line.icon ? <img src={line.icon} alt="" aria-hidden="true" /> : <i aria-hidden="true" />}
          <strong>{line.name}</strong>
          <b>+{line.quantity.toLocaleString()}</b>
          {line.pending && <small>받기함</small>}
        </li>)}
        {lines.length === 0 && <li className="is-empty"><strong>지급된 보상이 없습니다.</strong></li>}
        </ul>
      </div>
      {below > 0 && <p className="cozy-first-clear-more"><i className="cozy-chevron is-down" aria-hidden="true" />아래에 보상 {below}개 더</p>}

      <footer className="cozy-first-clear-foot">
        <p className="cozy-first-clear-bag">
          <img src={bottomItemsIcon} alt="" aria-hidden="true" />
          <span>가방 여유 <b>{reward.availableSlots.toLocaleString()}</b>칸<small>보상은 가방으로 바로 지급됩니다.</small></span>
        </p>
        {onClose && <span className="cozy-first-clear-actions">
          <button type="button" className="cozy-first-clear-cta" autoFocus onClick={onClose}>모두 받기</button>
          <button type="button" className="cozy-first-clear-later" onClick={onClose}>나중에</button>
        </span>}
      </footer>
    </section>
  </div>;
}

export function BattleRewards({ stageId: _stageId, rice, rewards }: { stageId: string; rice: number; rewards: RewardLine[] }) {
  const [page, setPage] = useState(0);
  const entries = useMemo<RewardEntry[]>(() => {
    const lines: RewardEntry[] = rice > 0 ? [{ key: "rice", label: "쌀", quantity: rice, icon: riceIcon }] : [];
    for (const reward of rewards) {
      const meta = rewardMeta(reward.itemId);
      lines.push({
        key: reward.itemId,
        label: meta.label,
        quantity: reward.grantedQuantity,
        icon: meta.icon,
        warning: reward.discardedQuantity > 0 || (reward.skippedQuantity ?? 0) > 0 ? "공간 부족으로 일부 미지급" : undefined,
      });
    }
    return lines;
  }, [rewards, rice]);
  const pageCount = Math.max(1, Math.ceil(entries.length / 4));
  const safePage = Math.min(page, pageCount - 1);
  const visible = entries.slice(safePage * 4, safePage * 4 + 4);

  return <details className="cozy-rewards cozy-paper" open>
    <summary>
      <span>획득</span>
      <b className="cozy-reward-count" aria-label={`${entries.length}종 획득`}>{entries.length}</b>
      <i aria-hidden="true" />
    </summary>
    {visible.length > 0 ? <ul>
      {visible.map((entry) => <li key={entry.key}>
        <span className="cozy-reward-icon">{entry.key === "rice"
          ? <span className="cozy-rice-coin" style={{ "--cozy-profile-icons": `url(${profileIcons})` } as CSSProperties} role="img" aria-label="쌀" />
          : entry.icon ? <img src={entry.icon} alt="" /> : <b aria-hidden="true">·</b>}</span>
        <strong>{entry.label}</strong>
        <em>+{entry.quantity.toLocaleString()}</em>
        {entry.warning && <small>{entry.warning}</small>}
      </li>)}
    </ul> : <div className="cozy-rewards-empty"><img src={rewardsEmptyIcon} alt="" /><span>아직 획득한 보상이 없습니다.</span></div>}
    {pageCount > 1 && <div className="cozy-reward-pager" aria-label="획득 목록 페이지">
      <button type="button" onClick={() => setPage((value) => (value - 1 + pageCount) % pageCount)} aria-label="이전 획득 목록"><i className="cozy-chevron is-previous" aria-hidden="true" /></button>
      <span>{safePage + 1} / {pageCount}</span>
      <button type="button" onClick={() => setPage((value) => (value + 1) % pageCount)} aria-label="다음 획득 목록"><i className="cozy-chevron is-next" aria-hidden="true" /></button>
    </div>}
  </details>;
}

function MeterRow({ icon, label, current, max, percent, value, tone }: { icon: string; label: string; current: number; max: number; percent: number; value: ReactNode; tone: "hp" | "exp" }) {
  return <div className="cozy-status-row">
    <img src={icon} alt="" />
    <b>{label}</b>
    <div className={`cozy-status-track ${tone}`} role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={Math.max(1, max)} aria-valuenow={Math.max(0, Math.min(current, max))}><i style={{ width: `${Math.max(0, Math.min(100, percent))}%` }} /></div>
    <span>{value}</span>
  </div>;
}

export function BattlePlayerStatus({ level, hp, maxHp, experience, experienceRequired, experiencePercent }: { level: number; hp: number; maxHp: number; experience: number; experienceRequired: number; experiencePercent: number }) {
  return <section className="cozy-player-status cozy-paper" aria-label="캐릭터 전투 상태">
    <div className="cozy-level"><small>LV.</small><strong>{level.toLocaleString()}</strong></div>
    <div className="cozy-status-bars">
      <MeterRow icon={heartIcon} label="HP" current={hp} max={maxHp} percent={maxHp > 0 ? hp / maxHp * 100 : 0} value={maxHp > 0 ? `${hp.toLocaleString()} / ${maxHp.toLocaleString()}` : "-"} tone="hp" />
      <MeterRow icon={sproutIcon} label="EXP" current={experience} max={experienceRequired} percent={experiencePercent} value={level >= 500 ? "MAX" : <><strong>{experiencePercent}%</strong> ({experience.toLocaleString()} / {experienceRequired.toLocaleString()})</>} tone="exp" />
    </div>
  </section>;
}
