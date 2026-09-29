import { describe, expect, it, vi } from "vitest";
import { materialPreferenceApi } from "./api";

describe("materialPreferenceApi", () => {
  it("queries the server-owned selection with session credentials", async () => {
    const state = { selected: false, primaryMaterialType: null, options: [] };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(
      new Response(JSON.stringify({ data: state, stateVersion: 1 }), { status: 200 }),
    );

    await expect(materialPreferenceApi.get()).resolves.toEqual(state);
    expect(fetchMock).toHaveBeenCalledWith("/api/v1/material-preference", { credentials: "include" });
    fetchMock.mockRestore();
  });

  it("selects once using an idempotency key and session credentials", async () => {
    const selection = {
      selected: true,
      primaryMaterialType: "POTATO" as const,
      dropRates: { POTATO: 80, SWEET_POTATO: 10, CORN: 10 },
    };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(
      new Response(JSON.stringify({ data: selection, stateVersion: 2 }), { status: 200 }),
    );

    await expect(materialPreferenceApi.select("POTATO")).resolves.toEqual(selection);
    const [, init] = fetchMock.mock.calls[0]!;
    expect(init).toMatchObject({ method: "POST", credentials: "include", body: JSON.stringify({ materialType: "POTATO" }) });
    expect((init!.headers as Record<string, string>)["Idempotency-Key"]).toMatch(/[0-9a-f-]{36}/i);
    fetchMock.mockRestore();
  });

  it("keeps the server conflict code for an immutable selection", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(
      new Response(JSON.stringify({ code: "MATERIAL_ALREADY_SELECTED" }), { status: 409 }),
    );

    await expect(materialPreferenceApi.select("CORN")).rejects.toThrow("MATERIAL_ALREADY_SELECTED");
    fetchMock.mockRestore();
  });
});
