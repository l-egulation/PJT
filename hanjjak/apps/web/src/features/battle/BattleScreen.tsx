import { useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import { useQuery } from "@tanstack/react-query";
import { authApi } from "../auth/api";
import { runtimeCheckpoints } from "../runtime/checkpoints";
import { SkillVfxSprite, skillVfxForMainBattle, type SkillVfx } from "../gems/skillVfx";
import { skillsApi } from "../skills/api";
import { skillCooldowns } from "./skillCooldown";
import { BattleSkillCooldowns } from "./BattleSkillCooldowns";
import { battleApi } from "./api";
import { battleRetryPresentation, battleRetryProgressLabel, battleStageProgressLabel, experienceProgress } from "./battlePresentation";
import type { CombatRenderingEvent } from "./sessionApi";
import { useBattleRuntimeStore } from "./runtimeStore";
import { formatStageId } from "./stageLabel";
import { backgroundAsset, damagePresentation, damageText, heroMotionForEncounter, heroSkillMotion, MONSTER_NAMES, monsterMotion, monsterRenderKey, monsterSheet, monsterSpriteMeta, stageMonsterFor, type ChapterOneMonsterId, type HeroMotion, type MonsterMotion } from "./battleVisuals";
import regularBossAura from "../../shared/assets/cozy-hud-v1/boss-warning/regular-boss-aura.png";
import regularBossNameRibbon from "../../shared/assets/cozy-hud-v1/boss-warning/regular-boss-name-ribbon.png";
import finalBossCrest from "../../shared/assets/cozy-hud-v1/boss-warning/final-boss-crest.png";
import { ConnectionLostDialog } from "./ConnectionLostDialog";
import "./BattleScreen.css";
import {
  BattleGrowthNavigation,
  BattleHistoryPopover,
  BattlePlayerStatus,
  BattleProfileSummary,
  BattleRewards,
  useCountdownSeconds,
  BattleStageProgress,
  BattleUtilityNavigation,
  type BattleHudRoute,
  FirstClearRewardNotice,
} from "./BattleHud";
import { TutorialLauncher } from "../tutorial/TutorialLauncher";
import { ChatPanel } from "../chat/ChatPanel";
import { gemsApi } from "../gems/api";
import { characterApi } from "../character/api";
import { bossIntroDurationMs } from "./autoBattleClient";
import { PlayerBattleCharacter } from "../gems/PlayerBattleCharacter";
import { cosmeticsApi } from "../cosmetics/api";
import { raidApi } from "../raid/api";
function ActorHealthBar({ label, hp, maxHp }: { label: string; hp: number; maxHp: number }) {
  const safeMax = Math.max(1, maxHp);
  const safeHp = Math.max(0, Math.min(hp, safeMax));
  const percent = maxHp > 0 ? safeHp / safeMax * 100 : 0;
  return <div className="actor-health-bar" role="progressbar" aria-label={`${label} 체력`} aria-valuemin={0} aria-valuemax={safeMax} aria-valuenow={safeHp}>
    <span><b>{label}</b><small>{safeHp.toLocaleString()} / {maxHp.toLocaleString()}</small></span>
    <i><em style={{ width: `${percent}%` }} /></i>
  </div>;
}

export function ChapterOneMonster({ id, motion, boss, hp, maxHp, skillVfx }: { id: ChapterOneMonsterId; motion: MonsterMotion; boss: boolean; hp: number; maxHp: number; skillVfx: SkillVfx[] }) {
  const sprite = monsterSpriteMeta(id);
  const contentCenterX = ((sprite.contentLeft ?? 0) + (sprite.contentRight ?? sprite.cellSize)) / 2;
  const visualHeight = (sprite.contentBottom - sprite.contentTop) * sprite.displayScale;
  const visualWidth = ((sprite.contentRight ?? sprite.cellSize) - (sprite.contentLeft ?? 0)) * sprite.displayScale;
  const footOverflow = (sprite.contentBottom - sprite.cellSize + sprite.groundFromBottom) * sprite.displayScale;
  const spriteStyle = {
    "--monster-sheet": `url(${monsterSheet(id)})`,
    "--monster-cell": `${sprite.cellSize}px`,
    "--monster-sheet-width": `${sprite.cellSize * 4}px`,
    "--monster-sheet-height": `${sprite.cellSize * 5}px`,
    "--monster-scale": sprite.displayScale,
    "--monster-ground": `${sprite.groundFromBottom}px`,
    "--monster-health-bottom": `${visualHeight - footOverflow + 10}px`,
    "--monster-shadow-bottom": `${-footOverflow - 12}px`,
    "--monster-shadow-width": `${Math.min(190, Math.max(62, visualHeight * 0.88))}px`,
    "--monster-vfx-center-x": `calc(50% + ${(contentCenterX - sprite.cellSize / 2) * sprite.displayScale}px)`,
    "--monster-vfx-center-bottom": `${visualHeight / 2 - footOverflow}px`,
    "--monster-heavy-vfx-size": `${Math.min(420, Math.max(140, visualHeight * 1.35))}px`,
    "--monster-dot-vfx-size": `${Math.min(320, Math.max(110, visualHeight * 1.05))}px`,
    "--monster-basic-vfx-size": `${Math.min(390, Math.max(88, Math.max(visualWidth, visualHeight) * 1.08))}px`,
  } as CSSProperties;
  return <div className={`battle-monster motion-${motion} ${boss ? "is-boss" : ""}`} style={spriteStyle}>
    <div className="monster-visual-anchor">
      {!boss && <ActorHealthBar label={MONSTER_NAMES[id]} hp={hp} maxHp={maxHp} />}
      <div className="monster-shadow" aria-hidden="true" />
      <div className="monster-sprite" role="img" aria-label={`${boss ? "보스 " : ""}${MONSTER_NAMES[id]}`} />
      {skillVfx.map((effect) => <SkillVfxSprite key={effect.kind} effect={effect} />)}
    </div>
  </div>;
}


export function PersistentBattleField({ effects }: { effects: SkillVfx[] }) {
  return <div className="persistent-battle-field" aria-hidden="true">{effects.map((effect) => <SkillVfxSprite key={effect.kind} effect={effect} />)}</div>;
}

const DOT_FIELD_FIXED_BOTTOM = 138;
const DOT_FIELD_FIXED_SIZE = 200;

export function persistentDotFieldLayout(): { bottom: number; size: number } {
  return {
    bottom: DOT_FIELD_FIXED_BOTTOM,
    size: DOT_FIELD_FIXED_SIZE,
  };
}

const DAMAGE_DISPLAY_FIXED_BOTTOM = 170;

export function persistentDamageLayout(): { bottom: number } {
  return { bottom: DAMAGE_DISPLAY_FIXED_BOTTOM };
}

export type DamageDisplayLane = "regular" | "dot";
export type DamageStackEntry = { event: CombatRenderingEvent; stackLevel: 0; lane: DamageDisplayLane };

export function damageDisplayLane(event: CombatRenderingEvent): DamageDisplayLane {
  return damagePresentation(event) === "dot" ? "dot" : "regular";
}

export function appendDamageStackEntries(current: DamageStackEntry[], incoming: CombatRenderingEvent[]): DamageStackEntry[] {
  return incoming.reduce((entries, event) => {
    const lane = damageDisplayLane(event);
    return [...entries.filter((entry) => entry.lane !== lane), { event, stackLevel: 0 as const, lane }];
  }, current);
}

export function consumeNewDamageEvents(events: CombatRenderingEvent[], seenEventIds: Set<string>): CombatRenderingEvent[] {
  const incoming = events.filter((event) => damagePresentation(event) !== undefined && !seenEventIds.has(event.eventId));
  incoming.forEach((event) => seenEventIds.add(event.eventId));
  return incoming;
}

export function resetDamageEventTracking(seenEventIds: Set<string>, releaseTimers: Map<string, number>): void {
  seenEventIds.clear();
  releaseTimers.clear();
}

export function PersistentDamageStack({ entries }: { entries: DamageStackEntry[] }) {
  return <div className="persistent-damage-stack" aria-hidden="true">
    {entries.map(({ event, lane }) => {
      const kind = damagePresentation(event);
      return kind && <b key={event.eventId} className={`monster-damage damage-${kind} damage-lane-${lane}`} data-event-id={event.eventId} data-damage-lane={lane}><span>{damageText(event.damage ?? 0)}</span></b>;
    })}
  </div>;
}

export function BossArrivalWarning({ monsterId, stageId: _stageId, finalBoss }: { monsterId: ChapterOneMonsterId; stageId: string; finalBoss: boolean }) {
  const name = MONSTER_NAMES[monsterId];
  const sprite = monsterSpriteMeta(monsterId);
  const contentLeft = sprite.contentLeft ?? 0;
  const contentRight = sprite.contentRight ?? sprite.cellSize;
  const contentWidth = Math.max(1, contentRight - contentLeft);
  const contentHeight = Math.max(1, sprite.contentBottom - sprite.contentTop);
  const portraitFill = 1.04;
  const portraitZoom = Math.min(1.7, Math.max(1.3, contentWidth / contentHeight * 1.08));
  const portraitScale = Math.min(sprite.cellSize * portraitFill / contentWidth, sprite.cellSize * portraitFill / contentHeight) * portraitZoom;
  const contentCenterX = sprite.warningPortrait?.focusX ?? (contentLeft + contentRight) / 2;
  const contentCenterY = sprite.warningPortrait?.focusY ?? (sprite.contentTop + sprite.contentBottom) / 2;
  const portraitFrame = sprite.warningPortrait?.frame ?? 0;
  const spriteStyle = {
    "--boss-sheet": `url(${monsterSheet(monsterId)})`,
    "--boss-portrait-size": `${portraitScale * 100}%`,
    "--boss-portrait-left": `${(0.5 - contentCenterX / sprite.cellSize * portraitScale) * 100}%`,
    "--boss-portrait-top": `${(0.5 - contentCenterY / sprite.cellSize * portraitScale) * 100}%`,
    "--boss-portrait-frame-x": `${portraitFrame / 3 * 100}%`,
  } as CSSProperties;
  return <aside className={`boss-arrival-warning ${finalBoss ? "is-final" : "is-midboss"}`} role="status" aria-live="assertive" aria-label={`${name} 보스 등장`}>
    <div className="boss-warning-vignette" aria-hidden="true" />
    <div className="boss-warning-content">
      <p className="boss-warning-kicker">{finalBoss ? "FINAL BOSS" : "WARNING"}</p>
      <div className="boss-warning-portrait" aria-hidden="true">
        <span className="boss-warning-crop"><span className="boss-warning-sprite" style={spriteStyle} /></span>
        <img className="boss-warning-aura" src={finalBoss ? finalBossCrest : regularBossAura} alt="" />
      </div>
      <div className="boss-warning-nameplate">
        <img src={regularBossNameRibbon} alt="" />
        <strong>{name}</strong>
      </div>
    </div>
  </aside>;
}

export type BossArrivalWarningState = { eventId: string; monsterId: ChapterOneMonsterId; finalBoss: boolean };

export function useBossArrivalWarning(
  renderingEvents: CombatRenderingEvent[],
  selectedStageId: string,
  normalMonsterIds?: string[],
  bossMonsterId?: string | null,
): BossArrivalWarningState | null {
  const [bossWarning, setBossWarning] = useState<BossArrivalWarningState | null>(null);
  const warningReleaseTimer = useRef<number | undefined>(undefined);
  const lastBossWarningEventId = useRef<string | null>(null);
  const bossSpawnEvent = [...renderingEvents].reverse().find((event) => event.type === "BOSS_SPAWNED");

  useEffect(() => {
    if (!bossSpawnEvent || bossSpawnEvent.eventId === lastBossWarningEventId.current) return;
    lastBossWarningEventId.current = bossSpawnEvent.eventId;
    const warningMonsterId = stageMonsterFor(selectedStageId, bossSpawnEvent.enemyIndex ?? 21, true, normalMonsterIds, bossMonsterId);
    const finalBoss = selectedStageId.endsWith("-10");
    clearTimeout(warningReleaseTimer.current);
    setBossWarning({ eventId: bossSpawnEvent.eventId, monsterId: warningMonsterId, finalBoss });
    warningReleaseTimer.current = globalThis.setTimeout(() => setBossWarning(null), bossIntroDurationMs(selectedStageId)) as unknown as number;
  }, [bossMonsterId, bossSpawnEvent, normalMonsterIds, selectedStageId]);

  useEffect(() => () => clearTimeout(warningReleaseTimer.current), []);

  return bossWarning;
}

export function BattleScreen({ onNavigate, onOpenProfile }: { onNavigate: (route: BattleHudRoute) => void; onOpenProfile: () => void }) {
  const [activeHeroAction, setActiveHeroAction] = useState<{ eventId: string; motion: HeroMotion } | null>(null);
  const [vfxEvents, setVfxEvents] = useState<CombatRenderingEvent[]>([]);
  const [visibleDamageEntries, setVisibleDamageEntries] = useState<DamageStackEntry[]>([]);
  const actionReleaseTimer = useRef<number | undefined>(undefined);
  const damageReleaseTimers = useRef(new Map<string, number>());
  const seenDamageEventIds = useRef(new Set<string>());
  const session = useQuery({ queryKey: ["auth", "session"], queryFn: authApi.session, retry: false });
  const stages = useQuery({ queryKey: ["stages"], queryFn: battleApi.stages });
  const gemState = useQuery({
    queryKey: ["gems"],
    queryFn: gemsApi.state,
    retry: false,
    refetchInterval: (query) => query.state.data?.unlocked === false ? 5_000 : false,
  });
  const appearance = useQuery({ queryKey: ["cosmetic-collection"], queryFn: cosmeticsApi.collection, retry: false });
  const raidState = useQuery({ queryKey: ["raid"], queryFn: raidApi.state, retry: false, staleTime: 5_000 });
  const cosmeticCatalog = useQuery({ queryKey: ["cosmetic-catalog"], queryFn: cosmeticsApi.catalog, retry: false, staleTime: Number.POSITIVE_INFINITY });
  const characterStats = useQuery({ queryKey: ["character-stats"], queryFn: characterApi.stats, retry: false });
  const skills = useQuery({ queryKey: ["skills"], queryFn: skillsApi.state, retry: false });
  const selectedStageId = useBattleRuntimeStore((state) => state.selectedStageId);
  const selectStage = useBattleRuntimeStore((state) => state.selectStage);
  const idleMode = useBattleRuntimeStore((state) => state.idleMode);
  const pendingIdleMode = useBattleRuntimeStore((state) => state.pendingIdleMode);
  const pendingRepeatStageId = useBattleRuntimeStore((state) => state.pendingRepeatStageId);
  const repeatStageId = useBattleRuntimeStore((state) => state.repeatStageId);
  const idleModeSaving = useBattleRuntimeStore((state) => state.idleModeSaving);
  const idleModeError = useBattleRuntimeStore((state) => state.idleModeError);
  const setIdleMode = useBattleRuntimeStore((state) => state.setIdleMode);
  const connected = useBattleRuntimeStore((state) => state.connected);
  const battleSession = useBattleRuntimeStore((state) => state.battleSession);
  const renderingEvents = useBattleRuntimeStore((state) => state.renderingEvents);
  const battleError = useBattleRuntimeStore((state) => state.battleError);
  const retryAt = useBattleRuntimeStore((state) => state.retryAt);
  const clearBattleError = useBattleRuntimeStore((state) => state.clearBattleError);
  const hp = useBattleRuntimeStore((state) => state.hp);
  const maxHp = useBattleRuntimeStore((state) => state.maxHp);
  const enemyHp = useBattleRuntimeStore((state) => state.enemyHp);
  const enemyMaxHp = useBattleRuntimeStore((state) => state.enemyMaxHp);
  const runtimeEnemyIndex = useBattleRuntimeStore((state) => state.enemyIndex);
  const runtimeEnemyBoss = useBattleRuntimeStore((state) => state.enemyBoss);
  const defeatedNormals = useBattleRuntimeStore((state) => state.defeatedNormals);
  const latestCycle = useBattleRuntimeStore((state) => state.latestCycle);
  const lastCompletedStageId = useBattleRuntimeStore((state) => state.lastCompletedStageId);
  const residenceStageId = useBattleRuntimeStore((state) => state.residenceStageId);
  const residenceRewards = useBattleRuntimeStore((state) => state.residenceRewards);
  const residenceRiceGained = useBattleRuntimeStore((state) => state.residenceRiceGained);
  const settledProgression = useBattleRuntimeStore((state) => state.settledProgression);
  const firstClearReward = useBattleRuntimeStore((state) => state.firstClearReward);
  const gameSessionId = useBattleRuntimeStore((state) => state.gameSessionId);
  const account = session.data?.account ?? null;
  const currentStage = useMemo(() => stages.data?.find((stage) => stage.stageId === selectedStageId), [selectedStageId, stages.data]);
  const bossWarning = useBossArrivalWarning(renderingEvents, selectedStageId, currentStage?.normalMonsterIds, currentStage?.bossMonsterId);
  const level = settledProgression?.levelAfter ?? latestCycle?.progression.levelAfter ?? account?.level ?? 1;
  const totalExperience = settledProgression?.experienceAfter ?? latestCycle?.progression.experienceAfter ?? account?.experience ?? 0;
  const xp = experienceProgress(level, totalExperience);
  const safeHp = maxHp > 0 ? Math.max(0, hp) : 0;
  const fighting = connected && battleSession !== null;
  const bossOnly = currentStage?.bossOnly ?? selectedStageId.endsWith("-10");
  const bossPhase = bossOnly || defeatedNormals >= 20;
  const stageProgressLabel = battleStageProgressLabel(bossOnly, defeatedNormals);
  const retryProgressLabel = battleRetryProgressLabel(bossOnly);
  const latestResult = latestCycle?.battle ?? null;
  const latestEvent = renderingEvents.at(-1);
  const playedTick = latestEvent?.logicalTick ?? vfxEvents.at(-1)?.logicalTick ?? 0;
  /*
   * 사건 목록은 최근 160개만 남는다. 보스가 나타난 사건이 그 창 밖으로 밀려나면
   * 제한시간이 전투 한복판에서 사라졌다. 한 번 나왔으면 그 전투가 끝날 때까지 기억한다.
   */
  const bossSpawnedIn = useRef<string | null>(null);
  if (!bossPhase) bossSpawnedIn.current = null;
  else if ([...vfxEvents, ...renderingEvents].some((event) => event.type === "BOSS_SPAWNED")) {
    bossSpawnedIn.current = battleSession?.battleSessionId ?? selectedStageId;
  }
  const bossSpawned = bossSpawnedIn.current !== null && bossSpawnedIn.current === (battleSession?.battleSessionId ?? selectedStageId);
  const timedBossPhase = bossPhase && bossSpawned && !bossWarning && battleSession?.input.bossTimeLimitTicks != null;
  const remainingBattleSeconds = useCountdownSeconds(
    timedBossPhase,
    (battleSession?.input.bossTimeLimitTicks ?? 0) * (battleSession?.tickDurationMilliseconds ?? 100),
    battleSession?.battleSessionId,
  );
  const enemyIndex = latestEvent?.enemyIndex ?? runtimeEnemyIndex ?? (bossOnly ? 1 : Math.min(21, defeatedNormals + 1));
  const approaching = latestEvent?.type === "ENEMY_SPAWNED" || latestEvent?.type === "BOSS_SPAWNED";
  const stageOpening = approaching && enemyIndex === 1;
  const skillVfx = skillVfxForMainBattle(vfxEvents, playedTick, enemyIndex);
  /* 자동 사용 칸에 올려 둔 액티브 스킬만 보여준다. 재사용 시간은 전투 사건에서 되읽는다. */
  const activeSkillOrder = (skills.data?.activeLoadout ?? []).filter(skillId => skills.data?.skills.some(skill => skill.skillId === skillId && skill.active && skill.unlocked));
  const skillNames = Object.fromEntries((skills.data?.skills ?? []).map(skill => [skill.skillId, skill.name]));
  const activeCooldowns = skillCooldowns(activeSkillOrder, vfxEvents, playedTick, battleSession?.tickDurationMilliseconds ?? 100);
  const fieldVfx = skillVfx.target.filter((effect) => effect.kind === "dot-target");
  const monsterVfx = skillVfx.target.filter((effect) => effect.kind !== "dot-target");
  const isBoss = latestEvent?.boss ?? runtimeEnemyBoss ?? bossPhase;
  const monsterId = stageMonsterFor(selectedStageId, enemyIndex, isBoss, currentStage?.normalMonsterIds, currentStage?.bossMonsterId);
  const currentMonsterKey = monsterRenderKey(enemyIndex, isBoss);
  const monsterSprite = monsterSpriteMeta(monsterId);
  const monsterContentCenterX = ((monsterSprite.contentLeft ?? 0) + (monsterSprite.contentRight ?? monsterSprite.cellSize)) / 2;
  const dotFieldLayout = persistentDotFieldLayout();
  const damageLayout = persistentDamageLayout();
  const fieldStyle = {
    "--battle-field-bottom": `${dotFieldLayout.bottom}px`,
    "--battle-field-size": `${dotFieldLayout.size}px`,
    "--battle-damage-bottom": `${damageLayout.bottom}px`,
    "--battle-damage-x": `${(monsterContentCenterX - monsterSprite.cellSize / 2) * monsterSprite.displayScale}px`,
  } as CSSProperties;
  const currentHeroMotion = activeHeroAction?.motion ?? heroMotionForEncounter(renderingEvents, fighting, enemyIndex);
  const currentMonsterMotion = monsterMotion(renderingEvents, fighting);
  const damageEvent = [...renderingEvents].reverse().find((event) => damagePresentation(event) !== undefined);
  const playerActionEvent = [...renderingEvents].reverse().find((event) => event.type === "PLAYER_BASIC_ATTACK_STARTED" || event.type === "PLAYER_SKILL_CAST_STARTED");
  const retry = battleRetryPresentation(latestResult?.failureCode, retryAt);
  const profileAccount = account ? { ...account, rice: settledProgression?.riceBalance ?? latestCycle?.progression.riceBalance ?? account.rice } : null;
  const idleModeStatus = idleModeSaving
    ? "방치 모드를 저장하는 중입니다."
    : idleModeError
      ? `방치 모드를 저장하지 못했습니다. (${idleModeError})`
      : pendingIdleMode
        ? `현재 ${idleMode === "AUTO_PROGRESS" ? "자동 진행" : `${formatStageId(repeatStageId ?? selectedStageId)} 반복`} · 다음 사이클부터 ${pendingIdleMode === "AUTO_PROGRESS" ? "자동 진행" : `${formatStageId(pendingRepeatStageId ?? selectedStageId)} 반복`}`
        : `현재 ${idleMode === "AUTO_PROGRESS" ? "자동 진행" : `${formatStageId(repeatStageId ?? selectedStageId)} 반복`}`;

  useEffect(() => {
    if (renderingEvents.length === 0) return;
    setVfxEvents((current) => [...current, ...renderingEvents].slice(-160));
  }, [renderingEvents]);

  useEffect(() => {
    setVfxEvents([]);
  }, [battleSession?.battleSessionId]);

  useEffect(() => {
    damageReleaseTimers.current.forEach((timer) => clearTimeout(timer));
    resetDamageEventTracking(seenDamageEventIds.current, damageReleaseTimers.current);
    setVisibleDamageEntries([]);
  }, [battleSession?.battleSessionId]);

  useEffect(() => {
    if (playerActionEvent) {
      clearTimeout(actionReleaseTimer.current);
      setActiveHeroAction({
        eventId: playerActionEvent.eventId,
        motion: playerActionEvent.type === "PLAYER_BASIC_ATTACK_STARTED" ? "strike1" : heroSkillMotion(playerActionEvent.skillId),
      });
      return;
    }
    if (damageEvent) {
      clearTimeout(actionReleaseTimer.current);
      /* 공격 동작은 100ms 간격 네 프레임이다. 400ms 를 다 보여 준 뒤에 놓는다.
         예전 140ms 는 두 번째 프레임에서 끊겨 한짝의 일격이 번쩍이고 사라졌다. */
      actionReleaseTimer.current = globalThis.setTimeout(() => setActiveHeroAction(null), 420) as unknown as number;
      return;
    }
    if (renderingEvents.some((event) => event.type === "PLAYER_DEFEATED" || event.type === "BATTLE_FAILED")) {
      clearTimeout(actionReleaseTimer.current);
      setActiveHeroAction(null);
    }
  }, [damageEvent, playerActionEvent, renderingEvents]);

  useEffect(() => {
    const incomingDamageEvents = consumeNewDamageEvents(renderingEvents, seenDamageEventIds.current);
    if (incomingDamageEvents.length === 0) return;

    setVisibleDamageEntries((current) => appendDamageStackEntries(current, incomingDamageEvents));
    incomingDamageEvents.forEach((event) => {
      const timer = globalThis.setTimeout(() => {
        damageReleaseTimers.current.delete(event.eventId);
        setVisibleDamageEntries((current) => current.filter((entry) => entry.event.eventId !== event.eventId));
      }, 900) as unknown as number;
      damageReleaseTimers.current.set(event.eventId, timer);
    });
  }, [renderingEvents]);

  useEffect(() => () => {
    clearTimeout(actionReleaseTimer.current);
    damageReleaseTimers.current.forEach((timer) => clearTimeout(timer));
    resetDamageEventTracking(seenDamageEventIds.current, damageReleaseTimers.current);
  }, []);

  const changeMode = (next: "AUTO_PROGRESS" | "REPEAT_STAGE") => {
    if (!gameSessionId || idleModeSaving || (next === "REPEAT_STAGE" && !currentStage?.repeatEligible)) return;
    void setIdleMode(next, gameSessionId, runtimeCheckpoints, selectedStageId);
  };

  const changeStage = (nextStageId: string) => {
    selectStage(nextStageId);
  };

  return <section className="battle-screen battle-screen-live" aria-labelledby="battle-title">
    <h2 id="battle-title" className="sr-only">한짝 자동전투</h2>

    {stages.isLoading && <div className="battle-message">스테이지 정보를 불러오는 중입니다.</div>}
    {stages.error && <div className="battle-message error" role="alert"><strong>스테이지를 불러오지 못했습니다.</strong><button onClick={() => stages.refetch()}>다시 시도</button></div>}
    {battleError && <ConnectionLostDialog reason={battleError} onReconnect={clearBattleError} />}

    {stages.data && <div className="battle-stage-shell cozy-battle-stage-shell">
      <BattleProfileSummary account={profileAccount} onOpen={onOpenProfile} />
      <BattleHistoryPopover />
      <TutorialLauncher />
      <BattleStageProgress
        stages={stages.data}
        selectedStageId={selectedStageId}
        remainingSeconds={remainingBattleSeconds}
        defeatedNormals={defeatedNormals}
        bossOnly={bossOnly}
        bossPhase={bossPhase}
        stageComplete={!fighting && Boolean(latestResult?.success) && lastCompletedStageId === selectedStageId}
        idleMode={idleMode}
        pendingIdleMode={pendingIdleMode}
        idleModeSaving={idleModeSaving || !gameSessionId}
        repeatEligible={Boolean(currentStage?.repeatEligible)}
        bossHealth={!bossWarning && isBoss && enemyMaxHp > 0 ? { name: MONSTER_NAMES[monsterId], hp: enemyHp, maxHp: enemyMaxHp } : null}
        onSelectStage={changeStage}
        onChangeMode={changeMode}
      />
      {(idleModeSaving || idleModeError || pendingIdleMode) && <span className={`cozy-idle-status ${idleModeError ? "error" : ""}`} role="status">{idleModeStatus}</span>}
      <BattleUtilityNavigation onNavigate={onNavigate} gemContentUnlocked={gemState.data?.unlocked} cosmeticContentUnlocked={characterStats.data?.cosmeticsUnlocked === true} raid={raidState.data ? { featureAvailable: raidState.data.featureAvailable, unlockStageId: raidState.data.unlockStageId, unlocked: raidState.data.unlocked } : undefined} />

      <div className={`battle-kitchen ${bossWarning ? "has-boss-warning" : ""}`} style={{ "--battle-background": `url(${backgroundAsset(currentStage?.backgroundId, selectedStageId)})` } as CSSProperties}>
        <div className="kitchen-backdrop" aria-hidden="true" />
        <p className={`battle-connection ${connected ? "connected" : ""} ${retry ? "retrying" : ""}`} role="status"><i />{!connected ? "게임 실행 세션 복구 중" : fighting ? `${formatStageId(selectedStageId)} 전투 진행 중` : retry ? `${retry.failureReason} — ${retry.secondsRemaining}초 후 ${formatStageId(selectedStageId)} 재시작` : "다음 전투 준비 중"}</p>
        <div className={`battle-floor ${approaching ? "is-approaching" : fighting ? "is-engaged" : ""} ${stageOpening ? "is-stage-opening" : ""}`} aria-label="자동전투 장면">
          <div className="hero-position"><PlayerBattleCharacter appearance={appearance.data} catalog={cosmeticCatalog.data} motion={currentHeroMotion} actionEventId={activeHeroAction?.eventId} skillVfx={skillVfx.caster} /></div>
          <div className="monster-party">
            <div className="persistent-battle-field-anchor" style={fieldStyle}>
              <PersistentBattleField effects={fieldVfx} />
              <PersistentDamageStack entries={visibleDamageEntries} />
            </div>
            <ChapterOneMonster key={currentMonsterKey} id={monsterId} motion={currentMonsterMotion} boss={isBoss} hp={enemyHp} maxHp={enemyMaxHp} skillVfx={monsterVfx} />
          </div>
        </div>

        {retry && <div className="battle-retry-notice" role="status" aria-live="polite">
          <strong>{retry.failureReason}</strong>
          <span>{retry.secondsRemaining}초 후 부활 · {formatStageId(selectedStageId)} {retryProgressLabel}</span>
        </div>}

        <BattleRewards stageId={residenceStageId ?? currentStage?.stageId ?? selectedStageId} rice={residenceRiceGained} rewards={residenceRewards} />
        {firstClearReward && <FirstClearRewardNotice reward={firstClearReward} onClose={() => useBattleRuntimeStore.setState({ firstClearReward: null })} />}
        <BattleSkillCooldowns cooldowns={activeCooldowns} names={skillNames} tickDurationMilliseconds={battleSession?.tickDurationMilliseconds ?? 100} />
        <BattlePlayerStatus level={level} hp={safeHp} maxHp={maxHp} experience={xp.current} experienceRequired={xp.required} experiencePercent={xp.percent} />
        <BattleGrowthNavigation onNavigate={onNavigate} gemContentUnlocked={gemState.data?.unlocked} />
      </div>
      {bossWarning && <BossArrivalWarning key={bossWarning.eventId} monsterId={bossWarning.monsterId} stageId={selectedStageId} finalBoss={bossWarning.finalBoss} />}
      <ChatPanel />
    </div>}
  </section>;
}
