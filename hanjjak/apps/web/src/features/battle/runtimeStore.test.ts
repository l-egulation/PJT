import { beforeEach, describe, expect, it, vi } from "vitest";
import { battleApi, type BattleCycleResponse, type ChapterAutoRunResponse } from "./api";
import { useBattleRuntimeStore } from "./runtimeStore";
import type { BattleEnemySettlement, BattleSessionResult, BattleSessionStarted } from "./sessionApi";

const cycle: BattleCycleResponse = {
  battle: { success: true, failureCode: null, remainingHp: 900, defeatedNormals: 20, elapsedTicks: 120 },
  reward: { rewards: [], usedSlots: 1, maxSlots: 200, isFull: false },
  progression: { experienceGained: 75, riceGained: 30, levelBefore: 1, levelAfter: 1, experienceBefore: 0, experienceAfter: 75, experienceToNextLevel: 925, riceBalance: 30 },
  firstClearReward: null,
};

const chapter: ChapterAutoRunResponse = {
  chapter: 1,
  runs: [],
  stoppedReason: "CHAPTER_COMPLETE",
  stoppedStageId: "stage.01-10",
  rewards: [],
  progression: null,
  finalInventory: null,
};

const session: BattleSessionStarted = {
  battleSessionId: "battle-session-1",
  battleToken: "battle-token-1",
  status: "ACTIVE",
  stageId: "stage.01-04",
  idleMode: "REPEAT_STAGE",
  repeatStageId: "stage.01-04",
  logicalTickBasis: 0,
  tickDurationMilliseconds: 100,
  durationMilliseconds: 1_000,
  completableAt: new Date().toISOString(),
  input: { contentVersion: "v1", seed: 1, player: { attack: 100, maxHp: 1_000, penetration: 0 }, normal: { hp: 10, attack: 1, defense: 0 }, boss: { hp: 20, attack: 2, defense: 0 }, skills: { heavyBasisPoints: 0, dotTotalBasisPoints: 0, hasteBasisPoints: 0, basicAmplificationBasisPoints: 0, criticalChanceBasisPoints: 0, allDamageBasisPoints: 0, permanentHasteBasisPoints: 0, permanentBasicAmplificationBasisPoints: 0, buffDurationBonusTicks: 0, activeOrder: [] }, normalCount: 20, bossTimeLimitTicks: null, scheduledStrikes: {} },
};

const sessionResult: BattleSessionResult = {
  battleSessionId: session.battleSessionId,
  status: "COMPLETED",
  stageId: session.stageId,
  nextStageId: "stage.01-04",
  idleMode: "REPEAT_STAGE",
  repeatStageId: "stage.01-04",
  battle: cycle.battle,
  reward: { ...cycle.reward, rewards: [{ itemId: "POTATO_M1", requestedQuantity: 2, grantedQuantity: 2, discardedQuantity: 0 }] },
  progression: cycle.progression,
  predictionMatched: true,
  settlements: [],
  firstClearReward: null,
};

describe("battle runtime", () => {
  beforeEach(() => {
    useBattleRuntimeStore.getState().reset();
  });

  it("keeps one current run and one latest result projection", () => {
    const runtime = useBattleRuntimeStore.getState();
    runtime.begin("cycle");
    expect(useBattleRuntimeStore.getState().running).toBe("cycle");

    runtime.completeCycle(cycle);
    expect(useBattleRuntimeStore.getState()).toMatchObject({ running: null, latestCycle: cycle, latestChapter: null });

    useBattleRuntimeStore.getState().begin("chapter");
    useBattleRuntimeStore.getState().completeChapter(chapter);
    expect(useBattleRuntimeStore.getState()).toMatchObject({ running: null, latestCycle: null, latestChapter: chapter });
  });

  it("projects a compatibility cycle first-clear reward into the notice state", () => {
    const firstClearReward = {
      rewardId: "cycle-reward-1", stageId: "stage.01-01", rewardVersion: "progression-rebalance-v1", firstClear: true,
      riceGranted: 1_000, grantedItems: [], pendingItems: [], unlockedSkillId: "active_heavy", itemStatus: "CLAIMED",
      requiredSlots: 0, availableSlots: 200, missingSlots: 0,
    };

    useBattleRuntimeStore.getState().completeCycle({ ...cycle, firstClearReward });

    expect(useBattleRuntimeStore.getState().firstClearReward).toEqual(firstClearReward);
  });

  it("projects a chapter run first-clear reward into the notice state", () => {
    const firstClearReward = {
      rewardId: "chapter-reward-1", stageId: "stage.01-10", rewardVersion: "progression-rebalance-v1", firstClear: true,
      riceGranted: 1_000, grantedItems: [], pendingItems: [], unlockedSkillId: null, itemStatus: "CLAIMED",
      requiredSlots: 0, availableSlots: 200, missingSlots: 0,
    };
    const chapterWithReward: ChapterAutoRunResponse = {
      ...chapter,
      runs: [{ stageId: "stage.01-10", battle: cycle.battle, reward: cycle.reward, progression: cycle.progression, firstClearReward }],
    };

    useBattleRuntimeStore.getState().completeChapter(chapterWithReward);

    expect(useBattleRuntimeStore.getState().firstClearReward).toEqual(firstClearReward);
  });

  it("shares stage selection independently of result replacement", () => {
    useBattleRuntimeStore.getState().selectStage("stage.01-04");
    useBattleRuntimeStore.getState().completeCycle(cycle);

    expect(useBattleRuntimeStore.getState().selectedStageId).toBe("stage.01-04");
  });

  it("clears account-scoped battle state at session end", () => {
    useBattleRuntimeStore.getState().selectStage("stage.01-04");
    useBattleRuntimeStore.getState().completeCycle(cycle);

    useBattleRuntimeStore.getState().reset();

    expect(useBattleRuntimeStore.getState()).toMatchObject({ accountId: null, selectedStageId: "stage.01-01", connected: false, gameSessionId: null, running: null, latestCycle: null, latestChapter: null });
  });

  it("keeps the applied mode until the server advances the cycle boundary", async () => {
    const checkpoints = { load: async () => null, save: async () => undefined, clearRuntime: async () => undefined };
    useBattleRuntimeStore.setState({ accountId: "account-1", contentVersion: "v1", gameSessionId: "game-1", selectedStageId: "stage.01-04", idleMode: "AUTO_PROGRESS" });
    vi.spyOn(battleApi, "updateRepeatStage").mockResolvedValue({ idleMode: "REPEAT_STAGE", repeatStageId: "stage.01-04", appliesAfterCurrentCycle: true, stateVersion: 2 });

    await useBattleRuntimeStore.getState().setIdleMode("REPEAT_STAGE", "game-1", checkpoints);
    expect(useBattleRuntimeStore.getState()).toMatchObject({ idleMode: "AUTO_PROGRESS", pendingIdleMode: "REPEAT_STAGE", pendingRepeatStageId: "stage.01-04" });

    useBattleRuntimeStore.getState().completeBattleSession(sessionResult, null);
    expect(useBattleRuntimeStore.getState()).toMatchObject({ idleMode: "REPEAT_STAGE", repeatStageId: "stage.01-04", pendingIdleMode: null, selectedStageId: "stage.01-04" });
  });

  it("applies a pending mode when the active cycle is aborted", async () => {
    const checkpoints = { load: async () => null, save: async () => undefined, clearRuntime: async () => undefined };
    useBattleRuntimeStore.setState({ accountId: "account-1", contentVersion: "v1", gameSessionId: "game-1", selectedStageId: "stage.01-04", idleMode: "AUTO_PROGRESS" });
    vi.spyOn(battleApi, "updateRepeatStage").mockResolvedValue({ idleMode: "REPEAT_STAGE", repeatStageId: "stage.01-04", appliesAfterCurrentCycle: true, stateVersion: 2 });

    await useBattleRuntimeStore.getState().setIdleMode("REPEAT_STAGE", "game-1", checkpoints);
    useBattleRuntimeStore.getState().stopBattleSession();

    expect(useBattleRuntimeStore.getState()).toMatchObject({ idleMode: "REPEAT_STAGE", repeatStageId: "stage.01-04", pendingIdleMode: null });
  });

  it("keeps the server stage and accumulates confirmed residence rewards", () => {
    const first = { settlementId: "settlement-1" };
    const second = { settlementId: "settlement-2" };
    const settlement = {
      settlementId: "settlement-template",
      battleSessionId: session.battleSessionId,
      enemyIndex: 1,
      boss: false,
      settledAt: "2026-09-10T00:00:00Z",
      reward: sessionResult.reward,
      progression: sessionResult.progression,
    } satisfies BattleEnemySettlement;
    useBattleRuntimeStore.getState().startBattleSession(session);
    useBattleRuntimeStore.getState().applyBattleSettlements([{ ...settlement, ...first }]);
    useBattleRuntimeStore.getState().completeBattleSession(sessionResult, null);
    useBattleRuntimeStore.getState().startBattleSession({ ...session, battleSessionId: "battle-session-2" });
    useBattleRuntimeStore.getState().applyBattleSettlements([{ ...settlement, ...second, battleSessionId: "battle-session-2" }]);
    useBattleRuntimeStore.getState().completeBattleSession({ ...sessionResult, battleSessionId: "battle-session-2" }, null);

    expect(useBattleRuntimeStore.getState()).toMatchObject({ selectedStageId: "stage.01-04", lastCompletedStageId: "stage.01-04", residenceRiceGained: 60 });
    expect(useBattleRuntimeStore.getState().residenceRewards[0].grantedQuantity).toBe(4);
  });

  it("keeps a first-clear notice visible when auto-progress starts the next battle", () => {
    const firstClearReward = {
      rewardId: "reward-1", stageId: "stage.01-04", rewardVersion: "progression-rebalance-v1", firstClear: true,
      riceGranted: 100, grantedItems: [], pendingItems: [], unlockedSkillId: "active_heavy", itemStatus: "CLAIMED",
      requiredSlots: 0, availableSlots: 200, missingSlots: 0,
    };
    useBattleRuntimeStore.getState().startBattleSession(session);
    useBattleRuntimeStore.getState().completeBattleSession({ ...sessionResult, firstClearReward }, null);

    useBattleRuntimeStore.getState().startBattleSession({ ...session, battleSessionId: "battle-session-2", stageId: "stage.01-05" });

    expect(useBattleRuntimeStore.getState().firstClearReward).toEqual(firstClearReward);
  });
  it("applies each monster settlement once before cycle completion", () => {
    useBattleRuntimeStore.getState().startBattleSession(session);
    const settlement = {
      settlementId: "settlement-1",
      battleSessionId: session.battleSessionId,
      enemyIndex: 1,
      boss: false,
      settledAt: "2026-09-10T00:00:00Z",
      reward: { rewards: [{ itemId: "POTATO_M1", requestedQuantity: 10, grantedQuantity: 10, discardedQuantity: 0 }], usedSlots: 1, maxSlots: 200, isFull: false },
      progression: { experienceGained: 3, riceGained: 4, levelBefore: 1, levelAfter: 1, experienceBefore: 0, experienceAfter: 3, experienceToNextLevel: 997, riceBalance: 4 },
    } satisfies BattleEnemySettlement;

    useBattleRuntimeStore.getState().applyBattleSettlements([settlement]);
    useBattleRuntimeStore.getState().applyBattleSettlements([settlement]);

    expect(useBattleRuntimeStore.getState()).toMatchObject({ residenceRiceGained: 4, settledProgression: settlement.progression });
    expect(useBattleRuntimeStore.getState().residenceRewards[0].grantedQuantity).toBe(10);
  });


  it("keeps the active enemy hp between rendering event groups", () => {
    const runtime = useBattleRuntimeStore.getState();
    runtime.startBattleSession(session);
    runtime.applyBattleEvents([{
      eventId: "spawn", logicalTick: 0, type: "ENEMY_SPAWNED", actor: "ENEMY", target: null,
      enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: 10, hpAfter: 10, defeated: false,
    }]);
    runtime.applyBattleEvents([{
      eventId: "hit", logicalTick: 0, type: "PLAYER_ATTACK_IMPACT", actor: "PLAYER", target: "ENEMY",
      enemyIndex: 1, boss: false, skillId: null, damage: 4, critical: false, hpBefore: 10, hpAfter: 6, defeated: false,
    }]);
    runtime.applyBattleEvents([{
      eventId: "enemy-attack", logicalTick: 1, type: "ENEMY_ATTACK_STARTED", actor: "ENEMY", target: "PLAYER",
      enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: null, hpAfter: null, defeated: false,
    }]);
    runtime.applyBattleEvents([{
      eventId: "attack-start", logicalTick: 2, type: "PLAYER_BASIC_ATTACK_STARTED", actor: "PLAYER", target: "ENEMY",
      enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: null, defeated: false,
    } as import("./sessionApi").CombatRenderingEvent]);

    expect(useBattleRuntimeStore.getState()).toMatchObject({ enemyHp: 6, enemyMaxHp: 10, enemyIndex: 1, enemyBoss: false });
  });

  it("shares the failed-stage revival wait and resets progress on retry", () => {
    const failed = {
      ...sessionResult,
      battle: { success: false, failureCode: "PLAYER_DIED", remainingHp: 0, defeatedNormals: 7, elapsedTicks: 80 },
    } satisfies BattleSessionResult;
    useBattleRuntimeStore.getState().startBattleSession(session);
    useBattleRuntimeStore.getState().completeBattleSession(failed, 2_000);

    expect(useBattleRuntimeStore.getState()).toMatchObject({ selectedStageId: session.stageId, hp: 0, defeatedNormals: 7, retryAt: 2_000, battleSession: null });

    useBattleRuntimeStore.getState().startBattleSession({ ...session, battleSessionId: "battle-session-2" });
    expect(useBattleRuntimeStore.getState()).toMatchObject({ selectedStageId: session.stageId, hp: session.input.player.maxHp, defeatedNormals: 0, retryAt: null });
  });

  it("clears the failed-stage retry projection on session end", () => {
    useBattleRuntimeStore.setState({ connected: true, gameSessionId: "game-1", retryAt: 2_000 });

    useBattleRuntimeStore.getState().setConnection(false);

    expect(useBattleRuntimeStore.getState()).toMatchObject({ connected: false, gameSessionId: null, retryAt: null });
  });
});
