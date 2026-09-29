import { describe, expect, it, vi } from "vitest";
import { battleApi } from "./api";

describe("battleApi", () => {
  it("loads stage summaries with credentials", async () => {
    const stages = [{ stageId: "stage.01-01", unlocked: true, clearCount: 0, contentVersion: "enemy-v1-applied", isCurrent: true, isRepeatTarget: false, repeatEligible: true, normalMonsterIds: [], bossMonsterId: null, backgroundId: null, bossOnly: false }];
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: stages }), { status: 200 }));

    await expect(battleApi.stages()).resolves.toEqual(stages);

    expect(fetchMock).toHaveBeenCalledWith("/api/v1/stages", { credentials: "include", headers: { Accept: "application/json" } });
    fetchMock.mockRestore();
  });

  it("stores the server-authoritative repeat target", async () => {
    const response = { idleMode: "REPEAT_STAGE", repeatStageId: "stage.01-01", appliesAfterCurrentCycle: true, stateVersion: 2 };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: response }), { status: 200 }));

    await expect(battleApi.updateRepeatStage("stage.01-01", "game-session-1")).resolves.toEqual(response);

    const [, init] = fetchMock.mock.calls[0]!;
    expect(init).toMatchObject({ method: "PATCH", credentials: "include", body: JSON.stringify({ stageId: "stage.01-01" }) });
    expect((init!.headers as Record<string, string>)["X-Game-Session-Id"]).toBe("game-session-1");
    fetchMock.mockRestore();
  });

  it("runs a battle cycle as an idempotent command", async () => {
    const response = { battle: { success: true, failureCode: null, remainingHp: 1000, defeatedNormals: 20, elapsedTicks: 120 }, reward: { rewards: [], usedSlots: 0, maxSlots: 200, isFull: false }, progression: { experienceGained: 75, riceGained: 30, levelBefore: 1, levelAfter: 1, experienceBefore: 0, experienceAfter: 75, experienceToNextLevel: 925, riceBalance: 30 } };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: response }), { status: 200 }));

    await expect(battleApi.runCycle("stage.01-01", "game-session-1")).resolves.toEqual(response);

    const [, init] = fetchMock.mock.calls[0]!;
    expect(init).toMatchObject({ method: "POST", credentials: "include", body: JSON.stringify({ stageId: "stage.01-01" }) });
    expect((init!.headers as Record<string, string>)["Content-Type"]).toBe("application/json");
    expect((init!.headers as Record<string, string>)["Idempotency-Key"]).toMatch(/[0-9a-f-]{36}/i);
    expect((init!.headers as Record<string, string>)["X-Game-Session-Id"]).toBe("game-session-1");
    fetchMock.mockRestore();
  });

  it("runs a chapter auto-run as an idempotent command", async () => {
    const response = { chapter: 1, runs: [], stoppedReason: "NO_STAGE", stoppedStageId: null, rewards: [], progression: null, finalInventory: null };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: response }), { status: 200 }));

    await expect(battleApi.runChapter(1, "game-session-1")).resolves.toEqual(response);

    const [, init] = fetchMock.mock.calls[0]!;
    expect(fetchMock).toHaveBeenCalledWith("/api/v1/battles/chapters/1/auto-run", expect.any(Object));
    expect(init).toMatchObject({ method: "POST", credentials: "include" });
    expect((init!.headers as Record<string, string>)["Content-Type"]).toBe("application/json");
    expect((init!.headers as Record<string, string>)["Idempotency-Key"]).toMatch(/[0-9a-f-]{36}/i);
    expect((init!.headers as Record<string, string>)["X-Game-Session-Id"]).toBe("game-session-1");
    fetchMock.mockRestore();
  });

  it("throws server error codes for locked stages", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ code: "STAGE_LOCKED" }), { status: 422 }));

    await expect(battleApi.runCycle("stage.01-02", "game-session-1")).rejects.toThrow("STAGE_LOCKED");
    fetchMock.mockRestore();
  });

  it("loads server battle history and selected-stage failure", async () => {
    const event = { eventId: "event-1", type: "STAGE_FAILED", occurredAt: "2026-09-09T00:00:00Z", stageId: "stage.01-01", dungeonId: null, resultCode: "PLAYER_DEFEATED", messageKey: "battle.history.player.defeated", contentVersion: "enemy-v1-applied", combatSnapshot: { attack: 10, maxHp: 100, penetration: 5 } };
    const fetchMock = vi.spyOn(globalThis, "fetch")
      .mockResolvedValueOnce(new Response(JSON.stringify({ data: [event] }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ data: { latestFailure: event } }), { status: 200 }));

    await expect(battleApi.history()).resolves.toEqual([event]);
    await expect(battleApi.latestStageFailure("stage.01-01")).resolves.toEqual({ latestFailure: event });

    expect(fetchMock).toHaveBeenNthCalledWith(1, "/api/v1/battle-history", { credentials: "include", headers: { Accept: "application/json" } });
    expect(fetchMock).toHaveBeenNthCalledWith(2, "/api/v1/battle-history/stages/stage.01-01/latest-failure", { credentials: "include", headers: { Accept: "application/json" } });
    fetchMock.mockRestore();
  });
});
