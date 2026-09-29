import { afterEach, describe, expect, it, vi } from "vitest";
import {
  AutoBattleClient,
  BATTLE_RENDER_TIME_SCALE,
  DIRECT_ATTACK_IMPACT_DELAY_MS,
  ENCOUNTER_APPROACH_DURATION_MS,
  FINAL_BOSS_INTRO_DURATION_MS,
  REGULAR_BOSS_INTRO_DURATION_MS,
} from "./autoBattleClient";
import { battleRemainingSeconds } from "./BattleHud";
import { battleSessionApi, type BattleSessionResult, type BattleSessionStarted } from "./sessionApi";

const started: BattleSessionStarted = {
  battleSessionId: "battle-1", battleToken: "token-1", status: "ACTIVE", stageId: "stage.01-01", idleMode: "AUTO_PROGRESS", repeatStageId: null, logicalTickBasis: 0,
  tickDurationMilliseconds: 100, durationMilliseconds: 1_000, completableAt: "2026-09-08T00:00:01.000Z",
  input: { contentVersion: "v1", seed: 1, player: { attack: 10, maxHp: 100, penetration: 0 }, normal: { hp: 1, attack: 0, defense: 0 }, boss: { hp: 1, attack: 0, defense: 0 }, skills: { heavyBasisPoints: 0, dotTotalBasisPoints: 0, hasteBasisPoints: 0, basicAmplificationBasisPoints: 0, criticalChanceBasisPoints: 0, allDamageBasisPoints: 0, permanentHasteBasisPoints: 0, permanentBasicAmplificationBasisPoints: 0, buffDurationBonusTicks: 0, activeOrder: [] }, normalCount: 20, bossTimeLimitTicks: null, scheduledStrikes: {} },
};
const completed: BattleSessionResult = {
  battleSessionId: "battle-1", status: "COMPLETED", stageId: "stage.01-01", idleMode: "AUTO_PROGRESS", repeatStageId: null, nextStageId: "stage.01-02",
  battle: { success: true, failureCode: null, remainingHp: 100, defeatedNormals: 20, elapsedTicks: 10 },
  reward: { rewards: [], usedSlots: 0, maxSlots: 200, isFull: false },
  progression: { experienceGained: 75, riceGained: 30, levelBefore: 1, levelAfter: 1, experienceBefore: 0, experienceAfter: 75, experienceToNextLevel: 925, riceBalance: 30 },
  firstClearReward: null,
  predictionMatched: null,
  settlements: [],
};

describe("auto battle client", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("completes one server-timed cycle and starts the next", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-08T00:00:00.000Z"));
    const start = vi.spyOn(battleSessionApi, "start")
      .mockResolvedValueOnce(started)
      .mockImplementation(() => Promise.withResolvers<BattleSessionStarted>().promise);
    vi.spyOn(battleSessionApi, "complete").mockResolvedValue(completed);
    const onStarted = vi.fn();
    const onCompleted = vi.fn();
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => completed.nextStageId, onStarted, onProgress: vi.fn(), onCompleted, onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await vi.advanceTimersByTimeAsync(1_000);

    expect(onStarted).toHaveBeenCalledWith(started);
    expect(onCompleted).toHaveBeenCalledWith(completed, null);
    expect(start).toHaveBeenCalledTimes(2);
    await client.stop();
  });

  it("retries a server-not-ready completion without stopping automatic battle", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-08T00:00:00.000Z"));
    const start = vi.spyOn(battleSessionApi, "start")
      .mockResolvedValueOnce(started)
      .mockImplementation(() => Promise.withResolvers<BattleSessionStarted>().promise);
    const complete = vi.spyOn(battleSessionApi, "complete")
      .mockRejectedValueOnce(new Error("BATTLE_SESSION_NOT_READY"))
      .mockResolvedValueOnce(completed);
    const onCompleted = vi.fn();
    const onError = vi.fn();
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => completed.nextStageId, onStarted: vi.fn(), onProgress: vi.fn(), onCompleted, onStopped: vi.fn(), onError });

    await client.start();
    await vi.advanceTimersByTimeAsync(1_000);

    expect(complete).toHaveBeenCalledTimes(1);
    expect(onError).not.toHaveBeenCalled();
    expect(client.active).toBe(started);

    await vi.advanceTimersByTimeAsync(250);

    expect(complete).toHaveBeenCalledTimes(2);
    expect(onCompleted).toHaveBeenCalledWith(completed, null);
    expect(onError).not.toHaveBeenCalled();
    expect(start).toHaveBeenCalledTimes(2);
    await client.stop();
  });

  it("waits one second before retrying the failed stage", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-08T00:00:00.000Z"));
    const failed = {
      ...completed,
      nextStageId: started.stageId,
      battle: { success: false, failureCode: "PLAYER_DIED", remainingHp: 0, defeatedNormals: 7, elapsedTicks: 10 },
    } satisfies BattleSessionResult;
    const start = vi.spyOn(battleSessionApi, "start")
      .mockResolvedValueOnce(started)
      .mockImplementation(() => Promise.withResolvers<BattleSessionStarted>().promise);
    vi.spyOn(battleSessionApi, "complete").mockResolvedValue(failed);
    const onCompleted = vi.fn();
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => failed.nextStageId, onStarted: vi.fn(), onProgress: vi.fn(), onCompleted, onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await vi.advanceTimersByTimeAsync(1_000);

    expect(onCompleted).toHaveBeenCalledWith(failed, Date.now() + 1_000);
    expect(start).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(999);
    expect(start).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1);
    expect(start).toHaveBeenNthCalledWith(2, started.stageId, "game-1");
    await client.stop();
  });

  it("cancels a pending failed-stage retry when the runtime stops", async () => {
    vi.useFakeTimers();
    const failed = {
      ...completed,
      nextStageId: started.stageId,
      battle: { success: false, failureCode: "PLAYER_DIED", remainingHp: 0, defeatedNormals: 7, elapsedTicks: 10 },
    } satisfies BattleSessionResult;
    const start = vi.spyOn(battleSessionApi, "start")
      .mockResolvedValueOnce(started)
      .mockImplementation(() => Promise.withResolvers<BattleSessionStarted>().promise);
    vi.spyOn(battleSessionApi, "complete").mockResolvedValue(failed);
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => failed.nextStageId, onStarted: vi.fn(), onProgress: vi.fn(), onCompleted: vi.fn(), onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await vi.advanceTimersByTimeAsync(1_000);
    await client.stop(true);
    await vi.advanceTimersByTimeAsync(1_000);

    expect(start).toHaveBeenCalledTimes(1);
  });

  it("aborts the active cycle on lifecycle stop", async () => {
    vi.spyOn(battleSessionApi, "start").mockResolvedValue(started);
    const abort = vi.spyOn(battleSessionApi, "abort").mockResolvedValue({ battleSessionId: started.battleSessionId, status: "ABORTED", lastHeartbeatAt: started.completableAt });
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => started.stageId, onStarted: vi.fn(), onProgress: vi.fn(), onCompleted: vi.fn(), onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await client.stop(true);

    expect(abort).toHaveBeenCalledWith(started, "game-1", { logicalTick: 0, defeatedNormals: 0, playerHp: 100 }, true);
  });

  it("settles a normal monster when its defeat event is replayed", async () => {
    vi.useFakeTimers();
    const renderingTimeline = [
      { eventId: "spawn", logicalTick: 0, type: "ENEMY_SPAWNED", actor: "ENEMY", target: null, enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: 1, hpAfter: 1, defeated: false },
      { eventId: "defeated", logicalTick: 0, type: "ENEMY_DEFEATED", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: 0, hpAfter: 0, defeated: true },
    ];
    const settlement = {
      settlementId: "settlement-1",
      battleSessionId: started.battleSessionId,
      enemyIndex: 1,
      boss: false,
      settledAt: "2026-09-10T00:00:00Z",
      reward: { rewards: [], usedSlots: 0, maxSlots: 200, isFull: false },
      progression: { experienceGained: 3, riceGained: 1, levelBefore: 1, levelAfter: 1, experienceBefore: 0, experienceAfter: 3, experienceToNextLevel: 997, riceBalance: 1 },
    };
    vi.spyOn(battleSessionApi, "start").mockResolvedValue({ ...started, durationMilliseconds: 5_000, renderingTimeline });
    const settle = vi.spyOn(battleSessionApi, "settle").mockResolvedValue({ battleSessionId: started.battleSessionId, throughEnemyIndex: 1, settlements: [settlement] });
    vi.spyOn(battleSessionApi, "abort").mockResolvedValue({ battleSessionId: started.battleSessionId, status: "ABORTED", lastHeartbeatAt: started.completableAt });
    const onSettled = vi.fn();
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => started.stageId, onStarted: vi.fn(), onProgress: vi.fn(), onSettled, onCompleted: vi.fn(), onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await vi.advanceTimersByTimeAsync(1_030);

    expect(settle).toHaveBeenCalledOnce();
    expect(settle.mock.calls[0]?.slice(0, 4)).toEqual([expect.objectContaining({ battleSessionId: started.battleSessionId }), "game-1", 1, { logicalTick: 0, defeatedNormals: 1, playerHp: 100 }]);
    expect(onSettled).toHaveBeenCalledWith([settlement]);
    await client.stop();
  });

  it("shows encounter approach before replaying a same-tick attack", async () => {
    vi.useFakeTimers();
    const renderingTimeline = [
      { eventId: "spawn", logicalTick: 0, type: "ENEMY_SPAWNED", actor: "ENEMY", target: null, enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: 1, hpAfter: 1, defeated: false },
      { eventId: "attack-start", logicalTick: 0, type: "PLAYER_BASIC_ATTACK_STARTED", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: null, hpAfter: null, defeated: false },
      { eventId: "attack", logicalTick: 0, type: "PLAYER_ATTACK_IMPACT", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: 1, critical: false, hpBefore: 1, hpAfter: 0, defeated: true },
    ];
    vi.spyOn(battleSessionApi, "start").mockResolvedValue({ ...started, durationMilliseconds: 0, renderingTimeline });
    vi.spyOn(battleSessionApi, "complete").mockResolvedValue(completed);
    const onEvents = vi.fn();
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => started.stageId, onStarted: vi.fn(), onProgress: vi.fn(), onEvents, onCompleted: vi.fn(), onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await vi.advanceTimersByTimeAsync(0);
    expect(onEvents).toHaveBeenLastCalledWith([renderingTimeline[0]]);

    await vi.advanceTimersByTimeAsync(ENCOUNTER_APPROACH_DURATION_MS - 1);
    expect(onEvents).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(onEvents).toHaveBeenLastCalledWith([renderingTimeline[1]]);

    await vi.advanceTimersByTimeAsync(DIRECT_ATTACK_IMPACT_DELAY_MS - 1);
    expect(onEvents).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(1);
    expect(onEvents).toHaveBeenLastCalledWith([renderingTimeline[2]]);

    await client.stop();
    vi.useRealTimers();
  });

  it("replaces later normal monsters at the defeat frame without another approach delay", async () => {
    vi.useFakeTimers();
    const renderingTimeline = [
      { eventId: "spawn-1", logicalTick: 0, type: "ENEMY_SPAWNED", actor: "ENEMY", target: null, enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: 1, hpAfter: 1, defeated: false },
      { eventId: "attack-1", logicalTick: 0, type: "PLAYER_ATTACK_IMPACT", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: 1, critical: false, hpBefore: 1, hpAfter: 0, defeated: true },
      { eventId: "defeat-1", logicalTick: 0, type: "ENEMY_DEFEATED", actor: "PLAYER", target: "ENEMY", enemyIndex: 1, boss: false, skillId: null, damage: null, critical: false, hpBefore: 0, hpAfter: 0, defeated: true },
      { eventId: "spawn-2", logicalTick: 0, type: "ENEMY_SPAWNED", actor: "ENEMY", target: null, enemyIndex: 2, boss: false, skillId: null, damage: null, critical: false, hpBefore: 1, hpAfter: 1, defeated: false },
      { eventId: "attack-start-2", logicalTick: 1, type: "PLAYER_BASIC_ATTACK_STARTED", actor: "PLAYER", target: "ENEMY", enemyIndex: 2, boss: false, skillId: null, damage: null, critical: false, hpBefore: null, hpAfter: null, defeated: false },
    ];
    vi.spyOn(battleSessionApi, "start").mockResolvedValue({ ...started, durationMilliseconds: 5_000, renderingTimeline });
    vi.spyOn(battleSessionApi, "abort").mockResolvedValue({ battleSessionId: started.battleSessionId, status: "ABORTED", lastHeartbeatAt: started.completableAt });
    vi.spyOn(battleSessionApi, "settle").mockResolvedValue({ battleSessionId: started.battleSessionId, throughEnemyIndex: 1, settlements: [] });
    const onEvents = vi.fn();
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => started.stageId, onStarted: vi.fn(), onProgress: vi.fn(), onEvents, onCompleted: vi.fn(), onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await vi.advanceTimersByTimeAsync(ENCOUNTER_APPROACH_DURATION_MS + 379);
    expect(onEvents.mock.calls.flatMap(([events]) => events).map((event) => event.eventId)).not.toContain("spawn-2");
    await vi.advanceTimersByTimeAsync(1);
    const replacementFrame = onEvents.mock.calls.at(-1)?.[0];
    expect(replacementFrame?.map((event: { eventId: string }) => event.eventId)).toEqual(["defeat-1", "spawn-2", "attack-start-2"]);

    await client.stop();
  });

  it("does not render the boss spawn before the final normal defeat frame", async () => {
    vi.useFakeTimers();
    const renderingTimeline = [
      { eventId: "spawn-normal", logicalTick: 0, type: "ENEMY_SPAWNED", actor: "ENEMY", target: null, enemyIndex: 20, boss: false, skillId: null, damage: null, critical: false, hpBefore: 1, hpAfter: 1, defeated: false },
      { eventId: "defeat-normal", logicalTick: 10, type: "ENEMY_DEFEATED", actor: "PLAYER", target: "ENEMY", enemyIndex: 20, boss: false, skillId: null, damage: null, critical: false, hpBefore: 0, hpAfter: 0, defeated: true },
      { eventId: "spawn-boss", logicalTick: 9, type: "BOSS_SPAWNED", actor: "ENEMY", target: null, enemyIndex: 21, boss: true, skillId: null, damage: null, critical: false, hpBefore: 100, hpAfter: 100, defeated: false },
    ];
    vi.spyOn(battleSessionApi, "start").mockResolvedValue({ ...started, durationMilliseconds: 10_000, renderingTimeline });
    vi.spyOn(battleSessionApi, "abort").mockResolvedValue({ battleSessionId: started.battleSessionId, status: "ABORTED", lastHeartbeatAt: started.completableAt });
    vi.spyOn(battleSessionApi, "settle").mockResolvedValue({ battleSessionId: started.battleSessionId, throughEnemyIndex: 20, settlements: [] });
    const onEvents = vi.fn();
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => started.stageId, onStarted: vi.fn(), onProgress: vi.fn(), onEvents, onCompleted: vi.fn(), onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await vi.advanceTimersByTimeAsync(ENCOUNTER_APPROACH_DURATION_MS + 10 * 100 * BATTLE_RENDER_TIME_SCALE + 379);
    expect(onEvents.mock.calls.flatMap(([events]) => events).map((event) => event.eventId)).not.toContain("spawn-boss");
    await vi.advanceTimersByTimeAsync(1);
    expect(onEvents.mock.calls.at(-1)?.[0].map((event: { eventId: string }) => event.eventId)).toEqual(["defeat-normal", "spawn-boss"]);

    await client.stop();
  });

  it.each([
    ["stage.02-04", REGULAR_BOSS_INTRO_DURATION_MS],
    ["stage.02-10", FINAL_BOSS_INTRO_DURATION_MS],
  ])("does not replay combat during the %s boss intro", async (stageId, introDuration) => {
    vi.useFakeTimers();
    const renderingTimeline = [
      { eventId: "boss-spawn", logicalTick: 0, type: "BOSS_SPAWNED", actor: "ENEMY", target: null, enemyIndex: 21, boss: true, skillId: null, damage: null, critical: false, hpBefore: 100, hpAfter: 100, defeated: false },
      { eventId: "attack-start", logicalTick: 10, type: "PLAYER_BASIC_ATTACK_STARTED", actor: "PLAYER", target: "ENEMY", enemyIndex: 21, boss: true, skillId: null, damage: null, critical: false, hpBefore: null, hpAfter: null, defeated: false },
    ];
    vi.spyOn(battleSessionApi, "start").mockResolvedValue({ ...started, stageId, durationMilliseconds: 10_000, renderingTimeline });
    vi.spyOn(battleSessionApi, "abort").mockResolvedValue({ battleSessionId: started.battleSessionId, status: "ABORTED", lastHeartbeatAt: started.completableAt });
    const onEvents = vi.fn();
    const onProgress = vi.fn();
    const client = new AutoBattleClient({ gameSessionId: () => "game-1", stageId: () => stageId, onStarted: vi.fn(), onProgress, onEvents, onCompleted: vi.fn(), onStopped: vi.fn(), onError: vi.fn() });

    await client.start();
    await vi.advanceTimersByTimeAsync(0);
    expect(onEvents).toHaveBeenLastCalledWith([renderingTimeline[0]]);
    expect(battleRemainingSeconds(10_000, 100, onProgress.mock.lastCall?.[0].logicalTick ?? -1)).toBe(10);

    await vi.advanceTimersByTimeAsync(introDuration - 1);
    expect(onEvents).toHaveBeenCalledTimes(1);
    expect(battleRemainingSeconds(10_000, 100, onProgress.mock.lastCall?.[0].logicalTick ?? -1)).toBe(10);
    await vi.advanceTimersByTimeAsync(1);
    expect(onEvents).toHaveBeenCalledTimes(1);
    expect(battleRemainingSeconds(10_000, 100, onProgress.mock.lastCall?.[0].logicalTick ?? -1)).toBe(10);

    await vi.advanceTimersByTimeAsync(1_499);
    expect(onEvents).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(onEvents).toHaveBeenLastCalledWith([renderingTimeline[1]]);
    expect(battleRemainingSeconds(10_000, 100, onProgress.mock.lastCall?.[0].logicalTick ?? -1)).toBe(9);

    await client.stop();
  });
});
