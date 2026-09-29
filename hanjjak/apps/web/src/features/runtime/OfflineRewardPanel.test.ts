// @vitest-environment happy-dom
import { createElement } from "react";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, describe, expect, it, vi } from "vitest";
import { OfflineRewardPanel, formatDuration, offlineRewardLines, pickGreeting } from "./OfflineRewardPanel";
import type { OfflineRewardPending } from "./gameSessionClient";
import cornKernelIcon from "../equipment/assets/material-ranks/corn-kernel-f.png";
import cornMiniIcon from "../equipment/assets/material-ranks/corn-mini-d.png";
import * as gameSessionApi from "./gameSessionClient";
import { useBattleRuntimeStore } from "../battle/runtimeStore";

const base: OfflineRewardPending = {
  jobId: "job-1", status: "CLAIMABLE", stageId: "3-4",
  startedAt: "", lastHeartbeatAt: "", accrualEndedAt: null,
  eligibleSeconds: 2_820, offlineSeconds: 2_827, experienceGained: 0, riceGained: 0, rewards: [],
};

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  useBattleRuntimeStore.getState().reset();
});

function renderPanel() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(createElement(QueryClientProvider, { client }, createElement(OfflineRewardPanel)));
}

describe("offline reward greeting", () => {
  it("covers every greeting across the roll range", () => {
    const rolls = [0, 0.17, 0.34, 0.5, 0.67, 0.84].map(pickGreeting);
    expect(new Set(rolls).size).toBe(6);
    expect(rolls[0]).toBe("안녕하세요!");
    expect(rolls[5]).toBe("가지마세요ㅠ");
  });

  it("keeps a roll of exactly 1 inside the list", () => {
    expect(pickGreeting(1)).toBe("가지마세요ㅠ");
  });
});

describe("offline reward duration", () => {
  it("shows sub-minute accrual in seconds instead of zero minutes", () => {
    expect(formatDuration(1)).toBe("1초");
    expect(formatDuration(59)).toBe("59초");
    expect(formatDuration(60)).toBe("1분");
    expect(formatDuration(61)).toBe("1분 1초");
  });

  it("shows precise elapsed seconds for the offline and rewarded durations", () => {
    expect(formatDuration(37)).toBe("37초");
    expect(formatDuration(80)).toBe("1분 20초");
    expect(formatDuration(95)).toBe("1분 35초");
    expect(formatDuration(3_601)).toBe("1시간 0분 1초");
  });
});

describe("offline reward panel", () => {
  it("renders exact offline and rewarded durations from the server result", async () => {
    vi.spyOn(gameSessionApi, "fetchOfflineReward").mockResolvedValue({ ...base, offlineSeconds: 95, eligibleSeconds: 80 });
    useBattleRuntimeStore.getState().setConnection(true, "session-1");
    renderPanel();

    await waitFor(() => expect(screen.getByText("오프라인 시간 1분 35초 · 보상 적용 1분 20초")).toBeTruthy());
  });
});

describe("offline reward lines", () => {
  it("lists experience and rice ahead of the items, each with an icon", () => {
    const lines = offlineRewardLines({
      ...base, experienceGained: 1_200, riceGained: 800,
      rewards: [{ itemId: "POTATO_M3", quantity: 12 }, { itemId: "skillbook:active_heavy:rare", quantity: 1 }],
    });
    expect(lines.map((line) => line.label)).toEqual(["경험치", "쌀", "감자", "희귀 한짝의 일격 비법서"]);
    expect(lines.every((line) => Boolean(line.icon))).toBe(true);
  });

  it("matches each material generation to its distinct catalog artwork", () => {
    const lines = offlineRewardLines({
      ...base,
      rewards: [{ itemId: "CORN_M1", quantity: 6 }, { itemId: "CORN_M2", quantity: 1 }],
    });
    expect(lines.map(({ label, icon }) => ({ label, icon }))).toEqual([
      { label: "옥수수 한 알", icon: cornKernelIcon },
      { label: "미니 옥수수", icon: cornMiniIcon },
    ]);
  });

  it("drops the currency rows that stayed at zero", () => {
    const lines = offlineRewardLines({ ...base, riceGained: 800 });
    expect(lines.map((line) => line.key)).toEqual(["rice"]);
  });
});
