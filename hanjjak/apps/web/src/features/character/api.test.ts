import { afterEach, describe, expect, it, vi } from "vitest";
import { QueryClient } from "@tanstack/react-query";
import { CharacterApiError, characterApi, executeCharacterCommand, executeCharacterCommands, isUncertainCommandError } from "./api";

afterEach(() => vi.unstubAllGlobals());

describe("character commands", () => {
  it("loads the authenticated character stat snapshot from the game API", async () => {
    const snapshot = {
      nickname: "한짝",
      level: 24,
      experience: 294720,
      cosmeticsUnlocked: false,
      contentVersion: "v1",
      stats: [{
        statId: "maxHp",
        label: "최대 HP",
        unit: "POINTS",
        total: 3680,
        base: 3041,
        additional: 639,
        sources: [],
        calculation: "3041 + 639",
      }],
      notices: [],
    };
    vi.stubGlobal("fetch", async (url: string, init: RequestInit) => {
      expect(url).toBe("/api/v1/character/stats");
      expect(init).toEqual({ credentials: "include" });
      return Response.json({ data: snapshot });
    });

    await expect(characterApi.stats()).resolves.toEqual(snapshot);
  });

  it("keeps an uncertain command protected from a second independently keyed action", () => {
    expect(isUncertainCommandError(new TypeError("connection lost"))).toBe(true);
    expect(isUncertainCommandError(new CharacterApiError(503, "UNAVAILABLE"))).toBe(true);
    expect(isUncertainCommandError(new CharacterApiError(409, "INSUFFICIENT_DUPLICATES"))).toBe(false);
  });
  it("reuses the command key and exact registration payload after a lost response", async () => {
    const calls: RequestInit[] = [];
    vi.stubGlobal("fetch", async (url: string, init: RequestInit) => {
      expect(url).toBe("/api/v1/cosmetics/hat%2Fone/registrations");
      calls.push(init);
      if (calls.length === 1) throw new TypeError("connection lost");
      return Response.json({ data: { states: [] } });
    });
    const command = { kind: "register", cosmeticId: "hat/one", key: "stable-key" } as const;
    await expect(characterApi.command(command)).rejects.toThrow();
    await characterApi.command(command);
    expect(calls.map(call => new Headers(call.headers).get("Idempotency-Key"))).toEqual(["stable-key", "stable-key"]);
    expect(calls.map(call => call.body)).toEqual(['{"mode":"UNTIL_NEXT_STAR"}', '{"mode":"UNTIL_NEXT_STAR"}']);
  });

  it("invalidates collection and stats when a sent command succeeds without a mounted subscriber", async () => {
    const client = new QueryClient();
    client.setQueryData(["character-stats"], { level: 1 });
    client.setQueryData(["cosmetic-collection"], { states: [] });
    vi.stubGlobal("fetch", async () => Response.json({ data: { states: [{ cosmeticId: "hat" }] } }));
    await executeCharacterCommand(client, { kind: "equip", slot: "HEAD", cosmeticId: null, key: "unequip" });
    expect(client.getQueryState(["character-stats"])?.isInvalidated).toBe(true);
    expect(client.getQueryState(["cosmetic-collection"])?.isInvalidated).toBe(true);
    client.clear();
  });

  it("sends a reset batch in order and invalidates character caches once", async () => {
    const client = new QueryClient();
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const requests: Array<{ url: string; cosmeticId: string | null }> = [];
    vi.stubGlobal("fetch", async (url: string, init: RequestInit) => {
      requests.push({ url, cosmeticId: JSON.parse(String(init.body)).cosmeticId });
      return Response.json({ data: { states: [] } });
    });
    await executeCharacterCommands(client, [
      { kind: "equip", slot: "HEAD", cosmeticId: null, key: "reset-head" },
      { kind: "equip", slot: "TOP", cosmeticId: null, key: "reset-top" },
    ]);
    expect(requests).toEqual([
      { url: "/api/v1/cosmetics/equipment/HEAD", cosmeticId: null },
      { url: "/api/v1/cosmetics/equipment/TOP", cosmeticId: null },
    ]);
    expect(invalidate).toHaveBeenCalledTimes(2);
    client.clear();
  });

  it("clears private cached state and ends the session on unauthorized response", async () => {
    const client = new QueryClient();
    client.setQueryData(["character-stats"], { nickname: "private" });
    client.setQueryData(["inventory"], { private: true });
    vi.stubGlobal("fetch", async () => Response.json({ code: "AUTH_REQUIRED" }, { status: 401 }));
    await expect(executeCharacterCommand(client, { kind: "register", cosmeticId: "hat", key: "key" })).rejects.toThrow();
    expect(client.getQueryData(["character-stats"])).toBeUndefined();
    expect(client.getQueryData(["inventory"])).toBeUndefined();
    expect(client.getQueryData(["auth", "session"])).toEqual({ authenticated: false, account: null });
    client.clear();
  });

  it("does not log out a newer session when an old command returns unauthorized", async () => {
    const client = new QueryClient();
    client.setQueryData(["auth", "session"], { authenticated: true, account: { accountId: "old" } });
    let finish!: (response: Response) => void;
    vi.stubGlobal("fetch", () => new Promise<Response>(resolve => { finish = resolve; }));
    const oldCommand = executeCharacterCommand(client, { kind: "register", cosmeticId: "hat", key: "old-command" });
    const newSession = { authenticated: true, account: { accountId: "new" } };
    client.setQueryData(["auth", "session"], newSession);
    client.setQueryData(["inventory"], { owner: "new" });
    finish(Response.json({ code: "AUTH_REQUIRED" }, { status: 401 }));
    await expect(oldCommand).rejects.toThrow();
    expect(client.getQueryData(["auth", "session"])).toEqual(newSession);
    expect(client.getQueryData(["inventory"])).toEqual({ owner: "new" });
    client.clear();
  });
});
