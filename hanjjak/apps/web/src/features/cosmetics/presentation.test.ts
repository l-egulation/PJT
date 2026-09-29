import { describe, expect, it } from "vitest";
import { cosmeticsErrorMessage, summarizeDraw, type DrawResult } from "./presentation";

describe("cosmetics presentation", () => {
  it("maps stable and unknown server error codes to non-empty Korean copy", () => {
    for (const code of ["GACHA_CONTENT_UNAVAILABLE", "COSMETIC_SYSTEM_LOCKED", "INSUFFICIENT_GACHA_FUNDS", "UNKNOWN_SERVER_CODE"]) {
      const message = cosmeticsErrorMessage(code);
      expect(message).not.toBe("");
      expect(message).toMatch(/[가-힣]/);
    }
  });

  it("counts only the server isNew flag", () => {
    const results: DrawResult[] = [
      { cosmeticId: "a", grade: "LEGENDARY", isNew: true },
      { cosmeticId: "a", grade: "LEGENDARY", isNew: false },
      { cosmeticId: "b", grade: "EPIC", isNew: true },
    ];
    expect(summarizeDraw(results)).toEqual({ newCount: 2, duplicateCount: 1 });
  });
  it("maps additional server mutation errors to specific Korean guidance", () => {
    expect(cosmeticsErrorMessage("COSMETIC_MAX_STAR")).toContain("최대");
    expect(cosmeticsErrorMessage("MILESTONE_NOT_CLAIMABLE")).toContain("마일스톤");
    expect(cosmeticsErrorMessage("INVALID_SELECTOR_COSMETIC")).toContain("선택");
  });
  it("maps wrong-slot equipment errors to specific guidance", () => {
    const message = cosmeticsErrorMessage("COSMETIC_SLOT_MISMATCH");
    expect(message).toContain("부위");
    expect(message).not.toContain("처리하지 못했습니다");
  });
});
