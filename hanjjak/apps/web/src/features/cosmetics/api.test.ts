import { afterEach, expect, it, vi } from "vitest";
import { cosmeticsApi } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("claims milestones with the caller idempotency key", async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ data: { remainingClaimableCount: 0 } }), { status: 200, headers: { "Content-Type": "application/json" } }));
  vi.stubGlobal("fetch", fetchMock);
  await cosmeticsApi.claimMilestone("cosmetic-banner-01", 2, "same-key");
  expect(fetchMock).toHaveBeenCalledWith("/api/v1/cosmetic-gacha/banners/cosmetic-banner-01/milestone-claims", expect.objectContaining({
    method: "POST", headers: expect.objectContaining({ "Idempotency-Key": "same-key" }), body: JSON.stringify({ count: 2 }),
  }));
});

it("opens a selector box for the chosen cosmetic with the caller key", async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ data: { states: [] } }), { status: 200, headers: { "Content-Type": "application/json" } }));
  vi.stubGlobal("fetch", fetchMock);
  await cosmeticsApi.openSelectorBox("cosmetic-selector-box-01", "cosmetic-061", "same-key");
  expect(fetchMock).toHaveBeenCalledWith("/api/v1/cosmetic-selector-boxes/cosmetic-selector-box-01/open", expect.objectContaining({
    method: "POST", headers: expect.objectContaining({ "Idempotency-Key": "same-key" }), body: JSON.stringify({ cosmeticId: "cosmetic-061" }),
  }));
});

it("sends only count and preserves the draw idempotency key", async () => {
  const fetchMock = vi.fn().mockResolvedValue(Response.json({ data: { results: [] } }));
  vi.stubGlobal("fetch", fetchMock);
  await cosmeticsApi.draw("banner/one", 10, "draw-key");
  const [, init] = fetchMock.mock.calls[0];
  expect(init.method).toBe("POST");
  expect(init.body).toBe(JSON.stringify({ count: 10 }));
  expect(new Headers(init.headers).get("Idempotency-Key")).toBe("draw-key");
});

it("URL-encodes path identifiers and includes credentials", async () => {
  const fetchMock = vi.fn().mockResolvedValue(Response.json({ data: { banner: {}, pool: [] } }));
  vi.stubGlobal("fetch", fetchMock);
  await cosmeticsApi.detail("banner/one");
  expect(fetchMock).toHaveBeenCalledWith("/api/v1/cosmetic-gacha/banners/banner%2Fone", { credentials: "include" });
});
