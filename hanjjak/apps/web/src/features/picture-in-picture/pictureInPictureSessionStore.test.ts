import { afterEach, describe, expect, it } from "vitest";
import type { BattleEnemySettlement, BattleSessionResult } from "../battle/sessionApi";
import { usePictureInPictureSessionStore } from "./pictureInPictureSessionStore";

const progression = { experienceGained: 1, riceGained: 2, levelBefore: 1, levelAfter: 1, experienceBefore: 0, experienceAfter: 1, experienceToNextLevel: 999, riceBalance: 2 };
const settlement = { settlementId: "settlement-1", battleSessionId: "battle-1", enemyIndex: 1, boss: false, settledAt: "2026-09-13T00:00:00Z", reward: { rewards: [{ itemId: "POTATO_M1", requestedQuantity: 3, grantedQuantity: 2, discardedQuantity: 1 }], usedSlots: 1, maxSlots: 200, isFull: false }, progression } satisfies BattleEnemySettlement;
const completion = { battleSessionId: "battle-1", status: "COMPLETED", stageId: "stage.01-01", nextStageId: "stage.01-02", idleMode: "AUTO_PROGRESS", repeatStageId: null, battle: { success: true, failureCode: null, remainingHp: 1, defeatedNormals: 20, elapsedTicks: 10 }, reward: settlement.reward, progression, settlements: [settlement], firstClearReward: null, predictionMatched: true } satisfies BattleSessionResult;

afterEach(() => usePictureInPictureSessionStore.getState().stop());

describe("picture-in-picture session counters", () => {
  it("counts only granted rewards after the PIP opens and deduplicates settlements", () => {
    usePictureInPictureSessionStore.getState().applySettlements([settlement]);
    expect(usePictureInPictureSessionStore.getState().gainedByItemId).toEqual({});
    usePictureInPictureSessionStore.getState().start();
    usePictureInPictureSessionStore.getState().applySettlements([settlement, settlement]);
    expect(usePictureInPictureSessionStore.getState().gainedByItemId.POTATO_M1).toBe(2);
  });

  it("counts each successful battle session once and resets on close", () => {
    usePictureInPictureSessionStore.getState().start();
    usePictureInPictureSessionStore.getState().applyCompletion(completion);
    usePictureInPictureSessionStore.getState().applyCompletion(completion);
    expect(usePictureInPictureSessionStore.getState().clearCount).toBe(1);
    usePictureInPictureSessionStore.getState().stop();
    expect(usePictureInPictureSessionStore.getState().clearCount).toBe(0);
  });
});
