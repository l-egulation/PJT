import { afterEach, describe, expect, it, vi } from "vitest";
import { gemDungeonsApi, gemsApi } from "./api";

afterEach(() => vi.restoreAllMocks());

describe("gemsApi", () => {
  it("loads gem state", async () => {
    const state = { unlocked: false, tickets: 0, secondsUntilNextTicket: 0, todayBoss: "SURVIVAL", gemBoxQuantity: 0, gems: [], presets: {}, contentVersion: "gem-v1-draft" };
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: state }) }));
    await expect(gemsApi.state()).resolves.toEqual(state);
    expect(fetch).toHaveBeenCalledWith("/api/v1/gems", { credentials: "include", headers: { Accept: "application/json" } });
  });

  it("opens boxes with idempotency", async () => {
    vi.stubGlobal("crypto", { randomUUID: () => "00000000-0000-4000-8000-000000000020" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: { granted: [], state: {} } }) }));
    await gemsApi.openBoxes(1);
    expect(fetch).toHaveBeenCalledWith("/api/v1/gems/boxes/open", expect.objectContaining({ method: "POST", headers: expect.objectContaining({ "Idempotency-Key": "00000000-0000-4000-8000-000000000020" }), body: JSON.stringify({ quantity: 1 }) }));
  });

  it("updates any preset and executes a reviewed safe fusion", async () => {
    vi.stubGlobal("crypto", { randomUUID: () => "00000000-0000-4000-8000-000000000023" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: {} }) }));

    await gemsApi.updatePreset("ARMORED", ["gem-1"]);
    expect(fetch).toHaveBeenLastCalledWith("/api/v1/gems/presets/ARMORED", expect.objectContaining({ method: "PUT", body: JSON.stringify({ gemIds: ["gem-1"] }) }));
    await gemsApi.previewFusion({ mode: "SAFE_BATCH", level: 2, selections: [{ option: "FLAT_ATTACK", quantity: 6 }] });

    expect(fetch).toHaveBeenLastCalledWith("/api/v1/gem-fusions/preview", expect.objectContaining({ method: "POST", body: JSON.stringify({ mode: "SAFE_BATCH", level: 2, selections: [{ option: "FLAT_ATTACK", quantity: 6 }] }) }));

    await gemsApi.fuse("SAFE_BATCH", ["gem-1", "gem-2", "gem-3"]);
    expect(fetch).toHaveBeenLastCalledWith("/api/v1/gem-fusions", expect.objectContaining({ method: "POST", body: JSON.stringify({ mode: "SAFE_BATCH", gemIds: ["gem-1", "gem-2", "gem-3"] }) }));
  });

  it("starts a lifecycle dungeon challenge with idempotency", async () => {
    vi.stubGlobal("crypto", { randomUUID: () => "00000000-0000-4000-8000-000000000021" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: { challengeId: "challenge-1" } }) }));
    await gemDungeonsApi.start();
    expect(fetch).toHaveBeenCalledWith("/api/v1/gem-dungeons/challenges", expect.objectContaining({
      method: "POST",
      headers: expect.objectContaining({ "Idempotency-Key": "00000000-0000-4000-8000-000000000021" }),
      body: JSON.stringify({}),
    }));
  });

  it("requests a selected boss for an authorized test account", async () => {
    vi.stubGlobal("crypto", { randomUUID: () => "00000000-0000-4000-8000-000000000022" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: { challengeId: "challenge-2", boss: "BERSERK" } }) }));
    await gemDungeonsApi.today("BERSERK");
    expect(fetch).toHaveBeenLastCalledWith("/api/v1/gem-dungeons/today?boss=BERSERK", expect.objectContaining({ credentials: "include" }));
    await gemDungeonsApi.start("BERSERK");
    expect(fetch).toHaveBeenLastCalledWith("/api/v1/gem-dungeons/challenges", expect.objectContaining({ body: JSON.stringify({ boss: "BERSERK" }) }));
  });
});
