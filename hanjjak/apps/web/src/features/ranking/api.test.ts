import { afterEach, describe, expect, it, vi } from "vitest";
import { rankingApi } from "./api";

afterEach(() => vi.restoreAllMocks());

describe("rankingApi", () => {
  it("loads the server-owned combat power ranking", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: { formulaVersion: "combat-power-v2", generatedAt: "2026-09-09T00:00:00Z", sourceStateVersion: 4, overallTop: [], specializations: [], myEntry: null } }) }));
    await expect(rankingApi.combatPower(20)).resolves.toMatchObject({ formulaVersion: "combat-power-v2", sourceStateVersion: 4, overallTop: [] });
    expect(fetch).toHaveBeenCalledWith("/api/v1/rankings/combat-power?limit=20", { credentials: "include", headers: { Accept: "application/json" } });
  });

  it("surfaces server errors without inventing ranking data", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 500, json: async () => ({ code: "RANKING_UNAVAILABLE" }) }));
    await expect(rankingApi.combatPower()).rejects.toThrow("RANKING_UNAVAILABLE");
  });
});
