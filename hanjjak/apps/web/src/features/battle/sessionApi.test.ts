import { describe, expect, it, vi } from "vitest";
import { battleSessionApi, type BattleSessionStarted } from "./sessionApi";

const started: BattleSessionStarted = {
  battleSessionId: "battle-session-1",
  battleToken: "battle-token-1",
  status: "ACTIVE",
  stageId: "stage.01-01",
  idleMode: "AUTO_PROGRESS",
  repeatStageId: null,
  logicalTickBasis: 0,
  tickDurationMilliseconds: 100,
  durationMilliseconds: 2_000,
  completableAt: "2026-09-08T00:00:02Z",
  input: {
    contentVersion: "enemy-v1-applied",
    seed: 1,
    player: { attack: 500, maxHp: 1200, penetration: 100 },
    normal: { hp: 100, attack: 10, defense: 20 },
    boss: { hp: 500, attack: 20, defense: 20 },
    skills: { heavyBasisPoints: 0, dotTotalBasisPoints: 0, hasteBasisPoints: 0, basicAmplificationBasisPoints: 0, criticalChanceBasisPoints: 0, allDamageBasisPoints: 0, permanentHasteBasisPoints: 0, permanentBasicAmplificationBasisPoints: 0, buffDurationBonusTicks: 0, activeOrder: [] },
    normalCount: 20,
    bossTimeLimitTicks: null,
    scheduledStrikes: {},
  },
};

describe("battle session api", () => {
  it("starts a server battle session under the active game session", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: started }), { status: 200 }));

    await expect(battleSessionApi.start("stage.01-01", "game-session-1")).resolves.toEqual(started);

    const [, init] = fetchMock.mock.calls[0]!;
    expect(init).toMatchObject({ method: "POST", credentials: "include", body: JSON.stringify({ stageId: "stage.01-01" }) });
    expect((init!.headers as Record<string, string>)["X-Game-Session-Id"]).toBe("game-session-1");
    fetchMock.mockRestore();
  });

  it("settles a confirmed monster without sending reward values", async () => {
    const checkpoint = { logicalTick: 12, defeatedNormals: 1, playerHp: 1190 };
    const result = { battleSessionId: started.battleSessionId, throughEnemyIndex: 1, settlements: [] };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: result }), { status: 200 }));

    await expect(battleSessionApi.settle(started, "game-session-1", 1, checkpoint, "00000000-0000-4000-8000-000000000001")).resolves.toEqual(result);

    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe(`/api/v1/battles/sessions/${started.battleSessionId}/settlements`);
    expect((init!.headers as Record<string, string>)["Idempotency-Key"]).toBe("00000000-0000-4000-8000-000000000001");
    expect(JSON.parse(String(init!.body))).toEqual({ throughEnemyIndex: 1, renderingCheckpoint: checkpoint });
    fetchMock.mockRestore();
  });

  it("completes using opaque battle authority without sending rewards", async () => {
    const result = { battleSessionId: started.battleSessionId, status: "COMPLETED", stageId: started.stageId, idleMode: "AUTO_PROGRESS", repeatStageId: null, nextStageId: "stage.01-02", battle: { success: true, failureCode: null, remainingHp: 1200, defeatedNormals: 20, elapsedTicks: 20 }, reward: { rewards: [], usedSlots: 0, maxSlots: 200, isFull: false }, progression: { experienceGained: 75, riceGained: 30, levelBefore: 1, levelAfter: 1, experienceBefore: 0, experienceAfter: 75, experienceToNextLevel: 925, riceBalance: 30 }, settlements: [], predictionMatched: null };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: result }), { status: 200 }));

    await expect(battleSessionApi.complete(started, "game-session-1", null, { logicalTick: 20, defeatedNormals: 20, playerHp: 1200 })).resolves.toEqual(result);

    const [, init] = fetchMock.mock.calls[0]!;
    expect((init!.headers as Record<string, string>)["X-Battle-Token"]).toBe("battle-token-1");
    expect(JSON.parse(String(init!.body))).toEqual({ predictedHash: null, renderingCheckpoint: { logicalTick: 20, defeatedNormals: 20, playerHp: 1200 } });
    fetchMock.mockRestore();
  });
});
