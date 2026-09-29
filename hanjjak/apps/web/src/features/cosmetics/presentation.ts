export type DrawResult = { cosmeticId: string; grade: string; isNew: boolean };

const ERROR_MESSAGES: Record<string, string> = {
  COSMETIC_SYSTEM_LOCKED: "치장 시스템은 1-5 최초 클리어 후 이용할 수 있습니다.",
  COSMETICS_LOCKED: "치장 시스템은 1-5 최초 클리어 후 이용할 수 있습니다.",
  GACHA_CONTENT_UNAVAILABLE: "현재 뽑기를 이용할 수 없습니다. 캐릭터 치장에서 도감·등록·착용을 이용해 주세요.",
  GACHA_BANNER_NOT_FOUND: "해당 치장 배너를 찾을 수 없습니다.",
  INVALID_DRAW_COUNT: "뽑기 횟수가 올바르지 않습니다.",
  INSUFFICIENT_GACHA_FUNDS: "뽑기에 필요한 티켓과 쌀이 부족합니다.",
  IDEMPOTENCY_KEY_REUSED: "이미 다른 요청에 사용된 키입니다. 새로고침 후 다시 시도해 주세요.",
  COSMETIC_NOT_FOUND: "치장을 찾을 수 없습니다.",
  COSMETIC_NOT_OWNED: "등록한 치장만 사용할 수 있습니다.",
  INVALID_REGISTRATION_MODE: "성급 등록 방식이 올바르지 않습니다.",
  COSMETIC_MAX_STAR: "이미 최대 성급에 도달한 치장입니다.",
  COSMETIC_ALREADY_MAX_STAR: "이미 최대 성급에 도달한 치장입니다.",
  INSUFFICIENT_UNREGISTERED_COSMETICS: "성급 등록 재료가 부족합니다.",
  INVALID_EQUIPMENT_SLOT: "외형 부위가 올바르지 않습니다.",
  COSMETIC_SLOT_MISMATCH: "선택한 치장의 외형 부위가 요청한 슬롯과 일치하지 않습니다.",
  MILESTONE_NOT_CLAIMABLE: "현재 수령할 수 있는 마일스톤 상자가 없습니다.",
  NO_CLAIMABLE_SELECTOR_BOX: "수령할 수 있는 선택 상자가 없습니다.",
  INVALID_CLAIM_QUANTITY: "선택 상자 수량이 올바르지 않습니다.",
  INVENTORY_CAPACITY_EXCEEDED: "선택 상자를 보관할 공간이 부족합니다.",
  SELECTOR_BOX_NOT_FOUND: "선택 상자를 찾을 수 없습니다.",
  SELECTOR_BOX_NOT_OWNED: "선택 상자를 보유하고 있지 않습니다.",
  INVALID_SELECTOR_COSMETIC: "선택 상자에서 고를 수 없는 치장입니다.",
  COSMETIC_NOT_IN_SELECTOR_BOX: "선택 상자에서 고를 수 없는 치장입니다.",
  COSMETIC_STATE_CONFLICT: "치장 상태가 변경되었습니다. 최신 상태를 확인해 주세요.",
  COSMETIC_SERVICE_UNAVAILABLE: "치장 요청 결과를 확인할 수 없습니다. 같은 요청으로 다시 확인해 주세요.",
};

export function cosmeticsErrorMessage(code: string): string {
  return ERROR_MESSAGES[code] ?? "치장 요청을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.";
}

export function summarizeDraw(results: readonly DrawResult[]): { newCount: number; duplicateCount: number } {
  const newCount = results.filter((result) => result.isNew).length;
  return { newCount, duplicateCount: results.length - newCount };
}
