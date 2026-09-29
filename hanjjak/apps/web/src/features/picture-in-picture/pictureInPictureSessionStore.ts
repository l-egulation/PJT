import { create } from "zustand";
import type { BattleEnemySettlement, BattleSessionResult } from "../battle/sessionApi";

type PictureInPictureSessionState = {
  active: boolean;
  clearCount: number;
  gainedByItemId: Record<string, number>;
  appliedSettlementIds: Set<string>;
  completedBattleSessionIds: Set<string>;
  start: () => void;
  stop: () => void;
  applySettlements: (settlements: BattleEnemySettlement[]) => void;
  applyCompletion: (result: BattleSessionResult) => void;
};

const emptySession = () => ({
  clearCount: 0,
  gainedByItemId: {},
  appliedSettlementIds: new Set<string>(),
  completedBattleSessionIds: new Set<string>(),
});

export const usePictureInPictureSessionStore = create<PictureInPictureSessionState>((set) => ({
  active: false,
  ...emptySession(),
  start: () => set({ active: true, ...emptySession() }),
  stop: () => set({ active: false, ...emptySession() }),
  applySettlements: (settlements) => set((state) => {
    if (!state.active) return {};
    const appliedSettlementIds = new Set(state.appliedSettlementIds);
    const gainedByItemId = { ...state.gainedByItemId };
    let changed = false;
    for (const settlement of settlements) {
      if (appliedSettlementIds.has(settlement.settlementId)) continue;
      appliedSettlementIds.add(settlement.settlementId);
      changed = true;
      for (const reward of settlement.reward.rewards) {
        gainedByItemId[reward.itemId] = (gainedByItemId[reward.itemId] ?? 0) + reward.grantedQuantity;
      }
    }
    return changed ? { appliedSettlementIds, gainedByItemId } : {};
  }),
  applyCompletion: (result) => set((state) => {
    if (!state.active || !result.battle.success || state.completedBattleSessionIds.has(result.battleSessionId)) return {};
    const completedBattleSessionIds = new Set(state.completedBattleSessionIds);
    completedBattleSessionIds.add(result.battleSessionId);
    return { completedBattleSessionIds, clearCount: state.clearCount + 1 };
  }),
}));
