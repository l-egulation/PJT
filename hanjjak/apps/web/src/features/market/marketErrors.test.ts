import { describe, expect, it } from "vitest";
import { marketErrorMessage } from "./marketErrors";

describe("market error messages", () => {
  it("shows rice shortage in user-facing Korean copy", () => {
    expect(marketErrorMessage(new Error("INSUFFICIENT_RICE"), "구매에 실패했습니다.")).toBe("쌀이 부족합니다.");
    expect(marketErrorMessage(new Error("insufficient_rice"), "구매에 실패했습니다.")).toBe("쌀이 부족합니다.");
  });

  it("replaces sold out backend code with action copy", () => {
    expect(marketErrorMessage(new Error("LISTING_SOLD_OUT"), "구매에 실패했습니다.")).toBe("구매 가능한 매물이 없습니다.");
  });

  it("never puts a raw backend code on screen — players cannot act on HTTP 403", () => {
    expect(marketErrorMessage(new Error("HTTP 403"), "구매에 실패했습니다.")).toBe("구매에 실패했습니다.");
    expect(marketErrorMessage(new Error("MARKET_LIST_CHANGED"), "구매에 실패했습니다.")).toBe("구매에 실패했습니다.");
    expect(marketErrorMessage("broken", "구매에 실패했습니다.")).toBe("구매에 실패했습니다.");
  });

  it("passes a server sentence through when it is already written for a player", () => {
    expect(marketErrorMessage(new Error("점검 중이라 거래를 받지 않습니다."), "구매에 실패했습니다.")).toBe("점검 중이라 거래를 받지 않습니다.");
  });
});
