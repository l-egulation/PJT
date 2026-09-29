import { afterEach, describe, expect, it, vi } from "vitest";
import { FirstClearRewardApiError, firstClearRewardsApi } from "./api";

afterEach(() => vi.restoreAllMocks());

describe("first-clear rewards API", () => {
  it("lists only pending first-clear rewards from the contract path", async () => {
    const page = { rewards: [], count: 0 };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: page }), { status: 200 }));

    await expect(firstClearRewardsApi.list()).resolves.toEqual(page);

    expect(fetchMock).toHaveBeenCalledWith("/api/v1/first-clear-rewards", {
      credentials: "include",
      headers: { Accept: "application/json" },
    });
  });
  it("claims a pending reward with an idempotency key", async () => {
    const reward = { rewardId: "reward-1" };
    const key = "00000000-0000-4000-8000-000000000001";
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ data: reward }), { status: 200 }));

    await expect(firstClearRewardsApi.claim("reward-1", key)).resolves.toEqual(reward);

    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe("/api/v1/first-clear-rewards/reward-1/claim");
    expect(init).toMatchObject({ method: "POST", credentials: "include", body: "{}" });
    expect((init!.headers as Record<string, string>)["Idempotency-Key"]).toBe(key);
  });
  it("preserves structured capacity details", async () => {
    const response = {
      code: "INVENTORY_CAPACITY_EXCEEDED",
      details: [{ field: "missingSlots", code: "INVENTORY_CAPACITY_EXCEEDED", messageKey: "error.inventory.capacity.exceeded", value: 4 }],
    };
    vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 409 }));

    const error = await firstClearRewardsApi.claim("reward-1", "00000000-0000-4000-8000-000000000001")
      .catch((caught: unknown) => caught);

    expect(error).toMatchObject({ status: 409, code: "INVENTORY_CAPACITY_EXCEEDED" });
    expect((error as FirstClearRewardApiError).number("missingSlots")).toBe(4);
  });
});
