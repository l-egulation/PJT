// @vitest-environment happy-dom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { RaidScreen } from "./RaidScreen";
import type { RaidState } from "./api";

const state: RaidState = {
  featureAvailable: true,
  unlockStageId: "stage.01-02",
  unlocked: true,
  session: { sessionId: "session", contentVersion: "v1", rewardVersion: "r1", settlesAt: "2026-09-14T08:30:00.000Z", status: "OPEN", sealTarget: 50_000, sealedContribution: 34_500, sealProgressBasisPoints: 6900, sealSuccessScheduled: true, serverNow: "2026-09-14T08:00:00.000Z" },
  slots: [
    { ordinal: 1, status: "CONFIRMED", attemptsStarted: 3, attemptsRemaining: 0, currentAttemptId: null, resultHeld: false },
    { ordinal: 2, status: "AVAILABLE", attemptsStarted: 0, attemptsRemaining: 3, currentAttemptId: null, resultHeld: false },
    { ordinal: 3, status: "AVAILABLE", attemptsStarted: 0, attemptsRemaining: 3, currentAttemptId: null, resultHeld: false },
  ],
  currentAttempt: null,
  ranking: { sessionId: "session", settled: false, currentRank: 121, currentSealContribution: 34500, topEntries: [{ rank: 100, nickname: "동점친구", sealContribution: 34500 }], nextCursor: null, totalEligibleAccounts: 121 },
  claims: { items: [{ claimId: "claim", sessionId: "session", kind: "DAILY_RANK", status: "CLAIMABLE", reward: { cosmeticTickets: 1, gemBoxes: 2, rice: 300 }, createdAt: "2026-09-14T08:30:00.000Z", claimedAt: null }], claimableCount: 1, claimedCount: 0, nextCursor: null },
};

function renderRaid() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: state }) }));
  return render(<QueryClientProvider client={client}><RaidScreen now={() => Date.parse("2026-09-14T08:00:00.000Z")} /></QueryClientProvider>);
}
afterEach(() => { vi.unstubAllGlobals(); });

it("renders server-authoritative seal, KST settlement countdown, slots, claims, and outside-top rank", async () => {
  renderRaid();
  expect(await screen.findByText("34,500 / 50,000")).toBeTruthy();
  expect(screen.getByText("봉인 성공 예정")).toBeTruthy();
  expect(screen.getByText("00:30:00")).toBeTruthy();
  expect(screen.getByText("1번 슬롯 · 확정됨")).toBeTruthy();
  expect(screen.getByText("시작 3회 · 남음 0회")).toBeTruthy();
  expect(screen.getByText("내 순위 121위")).toBeTruthy();
  expect(screen.getByText("TOP 100 밖 · 내 기여 34,500")).toBeTruthy();
  expect(screen.getByRole("button", { name: "보상 받기" })).toBeTruthy();
});

it("renders one reward start action for the current available slot", async () => {
  renderRaid();
  const rewardStarts = await screen.findAllByRole("button", { name: "보상 도전" });
  expect(rewardStarts).toHaveLength(1);
  expect(rewardStarts[0]?.closest("article")?.querySelector("h3")?.textContent).toBe("2번 슬롯 · 대기");
});

it("uses server slot states to expose practice only after all reward slots are terminal", async () => {
  state.slots = state.slots.map((slot) => ({ ...slot, status: "CONFIRMED", attemptsRemaining: 0 }));
  renderRaid();
  expect(await screen.findByRole("button", { name: "연습 전투" })).toBeTruthy();
  state.slots[1] = { ...state.slots[1], status: "AVAILABLE", attemptsRemaining: 3 };
});

it("shows loading, authentication, and content failures with a recovery action", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 401, json: async () => ({ code: "AUTHENTICATION_REQUIRED" }) }));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><RaidScreen /></QueryClientProvider>);
  expect(screen.getByText("레이드 정보를 불러오는 중입니다.")).toBeTruthy();
  expect(await screen.findByText("로그인이 필요합니다.")).toBeTruthy();
});
