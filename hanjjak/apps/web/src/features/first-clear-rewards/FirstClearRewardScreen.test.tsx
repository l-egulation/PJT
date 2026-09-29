// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, expect, it, vi } from "vitest";
import { FirstClearRewardScreen } from "./FirstClearRewardScreen";
import { FirstClearRewardApiError, firstClearRewardsApi, type PendingFirstClearRewardPage } from "./api";

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const page: PendingFirstClearRewardPage = {
  count: 1,
  rewards: [{
    rewardId: "reward-1", stageId: "stage.01-05", rewardVersion: "v1", firstClear: true,
    riceGranted: 100, grantedItems: [{ itemId: "POTATO_M1", displayName: "감자 조각", quantity: 2 }],
    pendingItems: [{ itemId: "SKILL_BOOK_RARE", displayName: "희귀 스킬북", quantity: 1 }], unlockedSkillId: "active_heavy",
    itemStatus: "PENDING", requiredSlots: 3, availableSlots: 1, missingSlots: 2,
  }],
};

function renderScreen(client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })) {
  return { client, ...render(<QueryClientProvider client={client}><FirstClearRewardScreen /></QueryClientProvider>) };
}

it("renders server slot facts and pending item quantities", async () => {
  vi.spyOn(firstClearRewardsApi, "list").mockResolvedValue(page);
  renderScreen();

  await waitFor(() => expect(screen.getByText("1-5")).toBeTruthy());
  expect(screen.getByText("보상 버전 v1")).toBeTruthy();
  expect(screen.getByText("희귀 스킬북 × 1")).toBeTruthy();
  expect(screen.getByText("필요 슬롯 3 · 현재 여유 1 · 부족 2")).toBeTruthy();
});

it("removes a claimed row and invalidates authoritative account queries", async () => {
  vi.spyOn(firstClearRewardsApi, "list").mockResolvedValueOnce(page).mockResolvedValue({ rewards: [], count: 0 });
  vi.spyOn(firstClearRewardsApi, "claim").mockResolvedValue(page.rewards[0]!);
  const { client } = renderScreen();
  const invalidated = vi.spyOn(client, "invalidateQueries");

  await waitFor(() => expect(screen.getByRole("button", { name: "전부 수령" })).toBeTruthy());
  fireEvent.click(screen.getByRole("button", { name: "전부 수령" }));

  await waitFor(() => expect(screen.getByText("받을 첫 클리어 보상이 없습니다.")).toBeTruthy());
  for (const queryKey of [["first-clear-rewards"], ["inventory"], ["equipment"], ["skills"], ["auth", "session"]]) {
    expect(invalidated).toHaveBeenCalledWith({ queryKey });
  }
});

it("keeps the reward row and describes capacity recovery after a rejected claim", async () => {
  vi.spyOn(firstClearRewardsApi, "list").mockResolvedValue(page);
  vi.spyOn(firstClearRewardsApi, "claim").mockRejectedValue(new FirstClearRewardApiError(409, "INVENTORY_CAPACITY_EXCEEDED", [
    { field: "requiredSlots", code: "INVENTORY_CAPACITY_EXCEEDED", messageKey: "error.inventory.capacity.exceeded", value: 6 },
    { field: "availableSlots", code: "INVENTORY_CAPACITY_EXCEEDED", messageKey: "error.inventory.capacity.exceeded", value: 1 },
    { field: "missingSlots", code: "INVENTORY_CAPACITY_EXCEEDED", messageKey: "error.inventory.capacity.exceeded", value: 5 },
  ]));
  renderScreen();

  await waitFor(() => expect(screen.getByRole("button", { name: "전부 수령" })).toBeTruthy());
  fireEvent.click(screen.getByRole("button", { name: "전부 수령" }));

  // 알림은 화면을 밀지 않도록 팝업으로 뜬다. 보상 목록은 그 뒤에 그대로 남는다.
  await waitFor(() => expect(screen.getByRole("dialog").textContent).toContain("부족한 슬롯 5칸"));
  expect(screen.getByRole("dialog").textContent).toContain("필요 슬롯 6개");
  expect(screen.getByText("필요 슬롯 6 · 현재 여유 1 · 부족 5")).toBeTruthy();
  expect(screen.getByText("1-5")).toBeTruthy();
  expect(screen.getByRole("button", { name: "전부 수령" })).toBeTruthy();
});

it("reuses one claim key after an uncertain response", async () => {
  vi.spyOn(firstClearRewardsApi, "list").mockResolvedValue(page);
  const claim = vi.spyOn(firstClearRewardsApi, "claim")
    .mockRejectedValueOnce(new TypeError("network lost"))
    .mockResolvedValueOnce(page.rewards[0]!);
  renderScreen();

  const button = await screen.findByRole("button", { name: "전부 수령" });
  fireEvent.click(button);
  await waitFor(() => expect(claim).toHaveBeenCalledTimes(1));
  await waitFor(() => expect((button as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(button);
  await waitFor(() => expect(claim).toHaveBeenCalledTimes(2));

  expect(claim.mock.calls[0]![1]).toBe(claim.mock.calls[1]![1]);
});
