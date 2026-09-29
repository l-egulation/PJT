// @vitest-environment happy-dom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import type { BattleEnemySettlement, BattleSessionResult } from "./sessionApi";
import { useBattleRuntimeStore } from "./runtimeStore";
import { usePictureInPictureSessionStore } from "../picture-in-picture/pictureInPictureSessionStore";

const clientMock = vi.hoisted(() => ({ options: null as Record<string, (...args: never[]) => unknown> | null }));

vi.mock("./autoBattleClient", () => ({
  AutoBattleClient: class {
    currentStageId = null;
    constructor(options: Record<string, (...args: never[]) => unknown>) { clientMock.options = options; }
    start = vi.fn();
    stop = vi.fn().mockResolvedValue(undefined);
  },
}));
vi.mock("./autoBattleControl", () => ({ registerAutoBattleClient: vi.fn() }));

import { AutoBattleRuntime } from "./AutoBattleRuntime";

const progression = { experienceGained: 1, riceGained: 2, levelBefore: 1, levelAfter: 1, experienceBefore: 0, experienceAfter: 1, experienceToNextLevel: 999, riceBalance: 2 };
const settlement = { settlementId: "settlement-pip", battleSessionId: "battle-pip", enemyIndex: 1, boss: false, settledAt: "2026-09-13T00:00:00Z", reward: { rewards: [{ itemId: "POTATO_M1", requestedQuantity: 3, grantedQuantity: 2, discardedQuantity: 1 }], usedSlots: 1, maxSlots: 200, isFull: false }, progression } satisfies BattleEnemySettlement;
const completion = { battleSessionId: "battle-pip", status: "COMPLETED", stageId: "stage.01-01", nextStageId: "stage.01-02", idleMode: "AUTO_PROGRESS", repeatStageId: null, battle: { success: true, failureCode: null, remainingHp: 1, defeatedNormals: 20, elapsedTicks: 10 }, reward: settlement.reward, progression, settlements: [settlement], firstClearReward: null, predictionMatched: true } satisfies BattleSessionResult;

beforeEach(() => {
  clientMock.options = null;
  usePictureInPictureSessionStore.getState().start();
});

afterEach(() => {
  cleanup();
  usePictureInPictureSessionStore.getState().stop();
  vi.restoreAllMocks();
});

it("wires settled rewards and successful completions into the active PiP session", async () => {
  vi.spyOn(useBattleRuntimeStore.getState(), "applyBattleSettlements").mockImplementation(() => undefined);
  vi.spyOn(useBattleRuntimeStore.getState(), "completeBattleSession").mockImplementation(() => undefined);
  const queryClient = new QueryClient();
  render(<QueryClientProvider client={queryClient}><AutoBattleRuntime /></QueryClientProvider>);
  await waitFor(() => expect(clientMock.options).not.toBeNull());

  clientMock.options?.onSettled([settlement] as never);
  clientMock.options?.onCompleted(completion as never, null as never);

  expect(usePictureInPictureSessionStore.getState()).toMatchObject({ clearCount: 1, gainedByItemId: { POTATO_M1: 2 } });
});
