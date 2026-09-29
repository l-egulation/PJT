import { afterEach, describe, expect, it, vi } from "vitest";
import { skillsApi } from "./api";

afterEach(() => vi.restoreAllMocks());

describe("skillsApi", () => {
  it("loads skill state", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: { skills: [], activeLoadout: [], riceBalance: 0 } }) }));
    await expect(skillsApi.state()).resolves.toEqual({ skills: [], activeLoadout: [], riceBalance: 0 });
  });

  it("sends promotion using the caller-provided idempotency key", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: { skill: {}, success: true, state: {} } }) }));
    await skillsApi.promote("active_heavy", "00000000-0000-4000-8000-000000000010");
    expect(fetch).toHaveBeenCalledWith("/api/v1/skills/active_heavy/promote", expect.objectContaining({ method: "POST", headers: expect.objectContaining({ "Idempotency-Key": "00000000-0000-4000-8000-000000000010" }) }));
  });
  it("sends loadout ids and caller-provided idempotency key", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: { skills: [], activeLoadout: ["active_heavy"], riceBalance: 0 } }) }));
    await skillsApi.updateLoadout(["active_heavy", "active_dot"], "00000000-0000-4000-8000-000000000011");
    expect(fetch).toHaveBeenCalledWith("/api/v1/skills/loadout", expect.objectContaining({ method: "PUT", headers: expect.objectContaining({ "Idempotency-Key": "00000000-0000-4000-8000-000000000011" }), body: JSON.stringify({ skillIds: ["active_heavy", "active_dot"] }) }));
  });
});
