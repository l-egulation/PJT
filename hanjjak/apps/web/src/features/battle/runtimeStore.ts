import { create } from "zustand";
import { battleApi, type BattleCycleResponse, type ChapterAutoRunResponse, type ProgressionRewardResult, type RewardLine } from "./api";
import type { BattleEnemySettlement, BattleRenderingCheckpoint, BattleSessionResult, BattleSessionStarted, CombatRenderingEvent } from "./sessionApi";
import type { FirstClearRewardResult } from "../first-clear-rewards/api";
import type { ServerRuntimeSnapshot } from "../runtime/api";
import { restoreRuntime } from "../runtime/reconcileRuntime";
import type { IdleMode, RuntimeCheckpoint, RuntimeCheckpointStore } from "../../shared/persistence/runtimeCheckpointDb";
import { mergeRewardLines } from "./battlePresentation";

export type BattleRunKind = "cycle" | "chapter";

export type BattleRuntimeState = {
  accountId: string | null;
  stateVersion: number;
  contentVersion: string | null;
  selectedStageId: string;
  idleMode: IdleMode;
  pendingIdleMode: IdleMode | null;
  pendingRepeatStageId: string | null;
  repeatStageId: string | null;
  idleModeSaving: boolean;
  idleModeError: string | null;
  hp: number;
  maxHp: number;
  enemyHp: number;
  enemyMaxHp: number;
  enemyIndex: number;
  enemyBoss: boolean;
  defeatedNormals: number;
  residenceStageId: string | null;
  residenceRewards: RewardLine[];
  residenceRiceGained: number;
  settledProgression: ProgressionRewardResult | null;
  firstClearReward: FirstClearRewardResult | null;
  appliedSettlementIds: Set<string>;
  residenceSessionId: string | null;
  connected: boolean;
  gameSessionId: string | null;
  battleSession: BattleSessionStarted | null;
  renderingEvents: CombatRenderingEvent[];
  autoBattleEnabled: boolean;
  battleError: string | null;
  retryAt: number | null;
  running: BattleRunKind | null;
  latestCycle: BattleCycleResponse | null;
  lastCompletedStageId: string | null;
  latestChapter: ChapterAutoRunResponse | null;
  restore: (server: ServerRuntimeSnapshot, checkpoints: RuntimeCheckpointStore) => Promise<void>;
  setConnection: (connected: boolean, gameSessionId?: string | null) => void;
  setIdleMode: (idleMode: IdleMode, gameSessionId: string, checkpoints: RuntimeCheckpointStore, repeatStageId?: string) => Promise<void>;
  startBattleSession: (session: BattleSessionStarted) => void;
  updateBattleProgress: (checkpoint: BattleRenderingCheckpoint) => void;
  applyBattleEvents: (events: CombatRenderingEvent[]) => void;
  applyBattleSettlements: (settlements: BattleEnemySettlement[]) => void;
  completeBattleSession: (result: BattleSessionResult, retryAt: number | null, checkpoints?: RuntimeCheckpointStore) => void;
  stopBattleSession: () => void;
  failBattleSession: (message: string) => void;
  clearBattleError: () => void;
  selectStage: (stageId: string) => void;
  begin: (kind: BattleRunKind) => void;
  completeCycle: (result: BattleCycleResponse, checkpoints?: RuntimeCheckpointStore) => void;
  completeChapter: (result: ChapterAutoRunResponse, checkpoints?: RuntimeCheckpointStore) => void;
  finish: () => void;
  reset: () => void;
};

const initialState = {
  accountId: null,
  stateVersion: 0,
  contentVersion: null,
  selectedStageId: "stage.01-01",
  idleMode: "AUTO_PROGRESS" as IdleMode,
  pendingIdleMode: null,
  pendingRepeatStageId: null,
  repeatStageId: null,
  idleModeSaving: false,
  idleModeError: null,
  hp: 0,
  maxHp: 0,
  enemyHp: 0,
  enemyMaxHp: 0,
  enemyIndex: 1,
  enemyBoss: false,
  defeatedNormals: 0,
  residenceStageId: null,
  residenceRewards: [],
  residenceRiceGained: 0,
  settledProgression: null,
  firstClearReward: null,
  appliedSettlementIds: new Set<string>(),
  residenceSessionId: null,
  connected: false,
  gameSessionId: null,
  battleSession: null,
  renderingEvents: [],
  autoBattleEnabled: false,
  battleError: null,
  retryAt: null,
  running: null,
  latestCycle: null,
  lastCompletedStageId: null,
  latestChapter: null,
};

export const useBattleRuntimeStore = create<BattleRuntimeState>((set, get) => ({
  ...initialState,
  async restore(server, checkpoints) {
    const restored = await restoreRuntime(server, checkpoints);
    set({ ...restored, pendingIdleMode: null, pendingRepeatStageId: null, idleModeSaving: false, idleModeError: null, connected: true, gameSessionId: null, battleSession: null, renderingEvents: [], enemyHp: 0, enemyMaxHp: 0, enemyIndex: 1, enemyBoss: false, autoBattleEnabled: false, battleError: null, retryAt: null, running: null, latestCycle: null, lastCompletedStageId: null, latestChapter: null, firstClearReward: null, residenceStageId: restored.selectedStageId, residenceRewards: [], residenceRiceGained: 0, residenceSessionId: null, settledProgression: null, appliedSettlementIds: new Set<string>() });
    await persistBestEffort(checkpoints, get());
  },
  setConnection: (connected, gameSessionId = null) => set({ connected, gameSessionId, battleSession: connected ? get().battleSession : null, autoBattleEnabled: connected ? get().autoBattleEnabled : false, retryAt: connected ? get().retryAt : null, running: connected ? get().running : null }),
  async setIdleMode(idleMode, gameSessionId, checkpoints, requestedRepeatStageId) {
    const stageId = idleMode === "REPEAT_STAGE" ? requestedRepeatStageId ?? get().selectedStageId : null;
    set({ idleModeSaving: true, idleModeError: null });
    try {
      const result = await battleApi.updateRepeatStage(stageId, gameSessionId);
      set((state) => ({
        stateVersion: result.stateVersion,
        idleMode: result.appliesAfterCurrentCycle ? state.idleMode : result.idleMode,
        repeatStageId: result.appliesAfterCurrentCycle ? state.repeatStageId : result.repeatStageId,
        pendingIdleMode: result.appliesAfterCurrentCycle && (result.idleMode !== state.idleMode || result.repeatStageId !== state.repeatStageId) ? result.idleMode : null,
        pendingRepeatStageId: result.appliesAfterCurrentCycle && (result.idleMode !== state.idleMode || result.repeatStageId !== state.repeatStageId) ? result.repeatStageId : null,
        idleModeSaving: false,
      }));
      await persistBestEffort(checkpoints, get());
    } catch (error) {
      set({ idleModeSaving: false, idleModeError: error instanceof Error ? error.message : "IDLE_MODE_UPDATE_FAILED" });
    }
  },
  startBattleSession: (battleSession) => set((state) => ({
    battleSession,
    autoBattleEnabled: true,
    battleError: null,
    retryAt: null,
    running: "cycle",
    selectedStageId: battleSession.stageId,
    idleMode: battleSession.idleMode,
    repeatStageId: battleSession.repeatStageId,
    pendingIdleMode: state.pendingIdleMode === battleSession.idleMode && state.pendingRepeatStageId === battleSession.repeatStageId ? null : state.pendingIdleMode,
    pendingRepeatStageId: state.pendingIdleMode === battleSession.idleMode && state.pendingRepeatStageId === battleSession.repeatStageId ? null : state.pendingRepeatStageId,
    hp: battleSession.input.player.maxHp,
    maxHp: battleSession.input.player.maxHp,
    enemyHp: 0,
    enemyMaxHp: 0,
    enemyIndex: 1,
    enemyBoss: false,
    defeatedNormals: 0,
    renderingEvents: [],
    residenceStageId: battleSession.stageId,
    residenceRewards: state.residenceStageId === battleSession.stageId ? state.residenceRewards : [],
    residenceRiceGained: state.residenceStageId === battleSession.stageId ? state.residenceRiceGained : 0,
    residenceSessionId: battleSession.battleSessionId,
    appliedSettlementIds: new Set<string>(),
    settledProgression: state.residenceStageId === battleSession.stageId ? state.settledProgression : null,
  })),
  updateBattleProgress: (checkpoint) => set({ hp: checkpoint.playerHp, defeatedNormals: checkpoint.defeatedNormals }),
  applyBattleEvents: (renderingEvents) => set((state) => {
    let enemyHp = state.enemyHp;
    let enemyMaxHp = state.enemyMaxHp;
    let enemyIndex = state.enemyIndex;
    let enemyBoss = state.enemyBoss;
    for (const event of renderingEvents) {
      if (event.type === "ENEMY_SPAWNED" || event.type === "BOSS_SPAWNED") {
        enemyHp = event.hpAfter ?? 0;
        enemyMaxHp = event.hpAfter ?? event.hpBefore ?? 0;
        enemyIndex = event.enemyIndex ?? enemyIndex;
        enemyBoss = event.boss;
      } else if (event.target === "ENEMY" && event.hpAfter != null) {
        enemyHp = event.hpAfter;
      }
    }
    return { renderingEvents, enemyHp, enemyMaxHp, enemyIndex, enemyBoss };
  }),
  applyBattleSettlements: (settlements) => set((state) => {
    const residenceSessionId = settlements[0]?.battleSessionId ?? state.residenceSessionId;
    const appliedSettlementIds = state.residenceSessionId === residenceSessionId
      ? new Set(state.appliedSettlementIds)
      : new Set<string>();
    const unseen = settlements.filter((settlement) => !appliedSettlementIds.has(settlement.settlementId));
    if (unseen.length === 0) return { residenceSessionId };
    const rewards = [...state.residenceRewards];
    let rice = state.residenceRiceGained;
    let progression = state.settledProgression;
    for (const settlement of unseen) {
      appliedSettlementIds.add(settlement.settlementId);
      rewards.splice(0, rewards.length, ...mergeRewardLines(rewards, settlement.reward.rewards));
      rice += settlement.progression.riceGained;
      progression = settlement.progression;
    }
    return {
      residenceSessionId,
      appliedSettlementIds,
      residenceRewards: rewards,
      residenceRiceGained: rice,
      settledProgression: progression,
    };
  }),
  completeBattleSession: (result, retryAt, checkpoints) => {
    const current = get();
    const sameResidence = current.residenceStageId === result.stageId;
    set({
      battleSession: null,
      renderingEvents: [],
      enemyHp: 0,
      enemyMaxHp: 0,
      latestCycle: { battle: result.battle, reward: result.reward, progression: result.progression, firstClearReward: result.firstClearReward },
      lastCompletedStageId: result.stageId,
      latestChapter: null,
      retryAt,
      running: null,
      selectedStageId: result.nextStageId,
      idleMode: result.idleMode,
      repeatStageId: result.repeatStageId,
      pendingIdleMode: null,
      pendingRepeatStageId: null,
      hp: result.battle.remainingHp,
      defeatedNormals: result.battle.defeatedNormals,
      residenceStageId: result.stageId,
      residenceRewards: sameResidence ? current.residenceRewards : [],
      residenceRiceGained: sameResidence ? current.residenceRiceGained : 0,
      settledProgression: result.progression,
      residenceSessionId: result.battleSessionId,
      firstClearReward: result.firstClearReward,
      appliedSettlementIds: current.appliedSettlementIds,
    });
    if (checkpoints) void persistBestEffort(checkpoints, get());
  },
  stopBattleSession: () => set((state) => ({
    battleSession: null,
    renderingEvents: [],
    enemyHp: 0,
    enemyMaxHp: 0,
    autoBattleEnabled: false,
    retryAt: null,
    running: null,
    idleMode: state.pendingIdleMode ?? state.idleMode,
    repeatStageId: state.pendingIdleMode ? state.pendingRepeatStageId : state.repeatStageId,
    pendingIdleMode: null,
    pendingRepeatStageId: null,
  })),
  failBattleSession: (battleError) => set((state) => ({
    battleSession: null,
    renderingEvents: [],
    enemyHp: 0,
    enemyMaxHp: 0,
    autoBattleEnabled: false,
    battleError,
    retryAt: null,
    running: null,
    idleMode: state.pendingIdleMode ?? state.idleMode,
    repeatStageId: state.pendingIdleMode ? state.pendingRepeatStageId : state.repeatStageId,
    pendingIdleMode: null,
    pendingRepeatStageId: null,
  })),
  clearBattleError: () => set({ battleError: null }),
  selectStage: (selectedStageId) => set((state) => state.selectedStageId === selectedStageId ? {} : {
    selectedStageId,
    defeatedNormals: 0,
    enemyHp: 0,
    enemyMaxHp: 0,
    residenceStageId: selectedStageId,
    residenceRewards: [],
    firstClearReward: null,
    residenceRiceGained: 0,
    settledProgression: null,
    appliedSettlementIds: new Set<string>(),
    residenceSessionId: null,
  }),
  begin: (running) => set({ running }),
  completeCycle: (latestCycle, checkpoints) => {
    set({ latestCycle, lastCompletedStageId: null, latestChapter: null, firstClearReward: latestCycle.firstClearReward, running: null, hp: latestCycle.battle.remainingHp, defeatedNormals: latestCycle.battle.defeatedNormals });
    if (checkpoints) void persistBestEffort(checkpoints, get());
  },
  completeChapter: (latestChapter, checkpoints) => {
    const battle = latestChapter.runs.at(-1)?.battle;
    const firstClearReward = latestChapter.runs.findLast((run) => run.firstClearReward != null)?.firstClearReward ?? null;
    set({
      latestChapter,
      latestCycle: null,
      lastCompletedStageId: null,
      running: null,
      selectedStageId: latestChapter.stoppedStageId ?? get().selectedStageId,
      hp: battle?.remainingHp ?? get().hp,
      defeatedNormals: battle?.defeatedNormals ?? get().defeatedNormals,
      firstClearReward,
    });
    if (checkpoints) void persistBestEffort(checkpoints, get());
  },
  finish: () => set({ running: null }),
  reset: () => set(initialState),
}));

async function persistBestEffort(checkpoints: RuntimeCheckpointStore, state: BattleRuntimeState): Promise<void> {
  if (!state.accountId || !state.contentVersion) return;
  const checkpoint: RuntimeCheckpoint = {
    accountId: state.accountId,
    schemaVersion: 1,
    contentVersion: state.contentVersion,
    idleMode: state.idleMode,
    stageId: state.selectedStageId,
    hp: state.hp,
    defeatedNormals: state.defeatedNormals,
    savedAt: new Date().toISOString(),
  };
  try {
    await checkpoints.save(checkpoint);
  } catch {
    // IndexedDB accelerates restore; the server snapshot remains authoritative.
  }
}
