// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { RaidBattle } from "./RaidBattle";
import type { RaidAttempt } from "./api";

afterEach(() => cleanup());

const running: RaidAttempt = {
  attemptId: "attempt", sessionId: "session", slotOrdinal: 1, mode: "REWARD", status: "RUNNING",
  inputSnapshot: { contentVersion: "v1", rewardVersion: "r1", seed: 1, player: { level: 12, maxHp: 1000, attack: 100, defense: 0, defensePenetration: 0, skillIds: [], skillLoadout: [], mainGemPreset: "MAIN", cosmeticEffectIds: [] }, boss: { bossId: "seal", displayName: "봉인 보스", contentVersion: "v1", initialAttack: 10, initialDefense: 2, maxTicks: 3000, tickDurationMilliseconds: 100, escalationIntervalTicks: 50, escalationBasisPoints: 500 }, capturedAt: "2026-09-14T08:00:00.000Z" },
  tickDurationMilliseconds: 100, durationMilliseconds: 300000, startedAt: "2026-09-14T08:00:00.000Z", completableAt: "2026-09-14T08:05:00.000Z", endedAt: null,
  currentResult: { damage: 1234, grade: "B", sealContribution: 900, endedAt: null, playerDied: false, timeLimitReached: false, reward: null }, terminalResult: null,
  renderingTimeline: { tickDurationMilliseconds: 100, durationMilliseconds: 300000, events: [{ sequence: 1, logicalTick: 0, type: "PLAYER_IMPACT", actor: "PLAYER", target: "BOSS", damage: 1234, critical: false, hpBefore: null, hpAfter: null, escalationStage: null, bossAttack: null, bossDefense: null }, { sequence: 2, logicalTick: 50, type: "ESCALATION", actor: null, target: null, damage: null, critical: null, hpBefore: null, hpAfter: null, escalationStage: 1, bossAttack: 12, bossDefense: 2 }] },
};

it("plays only server authored damage, grade, escalation and remaining time", () => {
  render(<RaidBattle attempt={running} now={() => Date.parse("2026-09-14T08:00:05.000Z")} onRetry={vi.fn()} onConfirm={vi.fn()} onDiscard={vi.fn()} busy={false} />);
  expect(screen.getByText("피해량 1,234")).toBeTruthy();
  expect(screen.getByText("등급 B")).toBeTruthy();
  expect(screen.getByText("상승 1단계 · 다음 상승은 서버 전투 기록에서 반영됩니다.")).toBeTruthy();
  expect(screen.getByText("04:55")).toBeTruthy();
});

it("offers retry below the third attempt and removes it on the third", () => {
  const onRetry = vi.fn();
  const { rerender } = render(<RaidBattle attempt={{ ...running, status: "RESULT_HELD", terminalResult: { ...running.currentResult, reward: { cosmeticTickets: 1, gemBoxes: 1, rice: 1 } } }} attemptsStarted={2} now={() => Date.parse("2026-09-14T08:05:00.000Z")} onRetry={onRetry} onConfirm={vi.fn()} onDiscard={vi.fn()} busy={false} />);
  fireEvent.click(screen.getByRole("button", { name: "다시 도전" }));
  expect(onRetry).toHaveBeenCalledOnce();
  rerender(<RaidBattle attempt={{ ...running, status: "RESULT_HELD", terminalResult: { ...running.currentResult, reward: { cosmeticTickets: 1, gemBoxes: 1, rice: 1 } } }} attemptsStarted={3} now={() => Date.parse("2026-09-14T08:05:00.000Z")} onRetry={onRetry} onConfirm={vi.fn()} onDiscard={vi.fn()} busy={false} />);
  expect(screen.queryByRole("button", { name: "다시 도전" })).toBeNull();
});

it("holds reconnect result and uses same-key retry when a command outcome is uncertain", () => {
  const retry = vi.fn();
  render(<RaidBattle attempt={{ ...running, status: "RESULT_HELD", terminalResult: { ...running.currentResult, reward: null } }} attemptsStarted={3} now={() => Date.parse("2026-09-14T08:05:00.000Z")} onRetry={vi.fn()} onConfirm={vi.fn()} onDiscard={retry} busy={false} uncertainCommand={{ label: "나가기", retry }} />);
  expect(screen.getByText("연결이 끊겨도 결과는 보관됩니다.")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "나가기 다시 시도" }));
  expect(retry).toHaveBeenCalledOnce();
});
