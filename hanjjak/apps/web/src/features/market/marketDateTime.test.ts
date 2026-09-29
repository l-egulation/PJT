import { describe, expect, it } from "vitest";
import { formatMarketDateTime } from "./marketDateTime";

describe("market date and time", () => {
  it("uses one year-month-day and 24-hour minute format in Korea time", () => {
    expect(formatMarketDateTime("2026-09-12T09:40:00Z")).toBe("2026-09-12 18:40");
    expect(formatMarketDateTime("2026-09-12T17:40:00Z")).toBe("2026-09-13 02:40");
  });

  it("keeps explicit empty and invalid timestamp states", () => {
    expect(formatMarketDateTime(null)).toBe("없음");
    expect(formatMarketDateTime("not-a-date")).toBe("날짜 확인 중");
  });
});
