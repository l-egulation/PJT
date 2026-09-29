// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { BattleGrowthNavigation, BattleStageProgress, BattleUtilityNavigation } from "./BattleHud";

/* 메뉴가 우편함을 살펴 점을 찍으므로 시험에도 질의 상자가 있어야 한다. */
function withQuery(element: ReactElement) {
  return <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{element}</QueryClientProvider>;
}

const stages = [{
  stageId: "stage.01-07",
  unlocked: true,
  clearCount: 2,
  contentVersion: "test",
  isCurrent: true,
  isRepeatTarget: false,
  repeatEligible: true,
  normalMonsterIds: [],
  bossMonsterId: null,
  backgroundId: null,
  bossOnly: false,
}];

afterEach(() => cleanup());

it("exposes automatic progress and stage repeat as direct mode controls", () => {
  const onChangeMode = vi.fn();
  render(<BattleStageProgress
    stages={stages}
    selectedStageId="stage.01-07"
    remainingSeconds={21}
    defeatedNormals={7}
    bossOnly={false}
    bossPhase={false}
    stageComplete={false}
    idleMode="AUTO_PROGRESS"
    idleModeSaving={false}
    repeatEligible
    onSelectStage={() => undefined}
    onChangeMode={onChangeMode}
  />);

  expect(screen.getByRole("button", { name: "자동 진행" }).getAttribute("aria-pressed")).toBe("true");
  fireEvent.click(screen.getByRole("button", { name: "스테이지 반복" }));
  expect(onChangeMode).toHaveBeenCalledWith("REPEAT_STAGE");
});

it("keeps stage 1-10 repeat disabled", () => {
  const onChangeMode = vi.fn();
  render(<BattleStageProgress
    stages={[{ ...stages[0], stageId: "stage.01-10", repeatEligible: false, bossOnly: true }]}
    selectedStageId="stage.01-10"
    remainingSeconds={0}
    defeatedNormals={0}
    bossOnly
    bossPhase
    stageComplete={false}
    idleMode="AUTO_PROGRESS"
    idleModeSaving={false}
    repeatEligible={false}
    onSelectStage={() => undefined}
    onChangeMode={onChangeMode}
  />);

  const repeat = screen.getByRole("button", { name: "스테이지 반복" });
  expect((repeat as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(repeat);
  expect(onChangeMode).not.toHaveBeenCalled();
});

it("shows a next-cycle mode reservation as selected after the first click", () => {
  render(<BattleStageProgress
    stages={stages}
    selectedStageId="stage.01-07"
    remainingSeconds={21}
    defeatedNormals={7}
    bossOnly={false}
    bossPhase={false}
    stageComplete={false}
    idleMode="AUTO_PROGRESS"
    pendingIdleMode="REPEAT_STAGE"
    idleModeSaving={false}
    repeatEligible
    onSelectStage={() => undefined}
    onChangeMode={() => undefined}
  />);

  expect(screen.getByRole("button", { name: "자동 진행" }).getAttribute("aria-pressed")).toBe("false");
  expect(screen.getByRole("button", { name: "스테이지 반복" }).getAttribute("aria-pressed")).toBe("true");
});

it("opens the content picker while dungeon is locked and raid is coming soon", () => {
  const onNavigate = vi.fn();
  render(withQuery(<BattleUtilityNavigation onNavigate={onNavigate} gemContentUnlocked={false} />));

  fireEvent.click(screen.getByRole("button", { name: "콘텐츠" }));
  expect(screen.getByRole("dialog", { name: "콘텐츠 선택" })).toBeTruthy();

  const dungeon = screen.getByRole("button", { name: /^던전/ });
  const raid = screen.getByRole("button", { name: /^레이드/ });
  const mailbox = screen.getByRole("button", { name: "메시지" });

  expect((dungeon as HTMLButtonElement).disabled).toBe(true);
  expect((raid as HTMLButtonElement).disabled).toBe(true);
  expect((mailbox as HTMLButtonElement).disabled).toBe(false);

  fireEvent.click(dungeon);
  fireEvent.click(raid);
  expect(onNavigate).not.toHaveBeenCalled();
});

it("uses the exact server availability and unlocked fields for the raid card", () => {
  const onNavigate = vi.fn();
  const raidCard = () => document.querySelector(".cozy-content-card.is-raid") as HTMLButtonElement;
  const openPicker = () => fireEvent.click(screen.getByRole("button", { name: "콘텐츠" }));

  const { rerender } = render(withQuery(<BattleUtilityNavigation onNavigate={onNavigate} raid={{ featureAvailable: false, unlockStageId: "1-2", unlocked: false }} />));
  openPicker();
  expect(raidCard().disabled).toBe(true);
  expect(raidCard().textContent).toContain("준비 중");

  rerender(withQuery(<BattleUtilityNavigation onNavigate={onNavigate} raid={{ featureAvailable: true, unlockStageId: "1-2", unlocked: false }} />));
  expect(raidCard().disabled).toBe(true);
  expect(raidCard().textContent).toContain("1-2 클리어 후 열림");

  rerender(withQuery(<BattleUtilityNavigation onNavigate={onNavigate} raid={{ featureAvailable: true, unlockStageId: "1-2", unlocked: true }} />));
  expect(raidCard().disabled).toBe(false);
  fireEvent.click(raidCard());
  expect(onNavigate).toHaveBeenCalledWith("raid");
});

it("keeps the battle return in the same first slot on every page", () => {
  const onNavigate = vi.fn();
  // 랭킹만 전투 단추를 맨 뒤로 보내 둘째 줄에 떨어뜨려 놓았었다. 이제 어느 화면이든 같은 자리다.
  for (const route of ["ranking", "market", "dungeon", "cosmetics", "firstClearRewards"] as const) {
    const { container, unmount } = render(withQuery(<BattleUtilityNavigation activeRoute={route} onNavigate={onNavigate} />));
    const navigation = container.querySelector(".cozy-utility-nav");
    const labels = Array.from(navigation?.querySelectorAll("button") ?? []).map((button) => button.getAttribute("aria-label"));
    expect(labels).toEqual(["전투", "치장 뽑기 1-5 해금", "콘텐츠", "거래소", "랭킹", "메시지", "설정"]);
    expect(container.querySelector(".cozy-utility-alert-dot")).toBeNull();
    expect(navigation?.className).not.toContain("ranking-layout");
    unmount();
  }
  render(withQuery(<BattleUtilityNavigation activeRoute="ranking" onNavigate={onNavigate} />));
  fireEvent.click(screen.getByRole("button", { name: "전투" }));
  expect(onNavigate).toHaveBeenCalledWith("battle");
});

it("sizes the utility grid from the rendered item count", () => {
  const { container, rerender } = render(withQuery(<BattleUtilityNavigation onNavigate={() => undefined} />));
  const navigation = container.querySelector<HTMLElement>(".cozy-utility-nav");
  expect(navigation?.style.getPropertyValue("--cozy-utility-columns")).toBe("6");

  rerender(withQuery(<BattleUtilityNavigation activeRoute="dungeon" onNavigate={() => undefined} />));
  expect(navigation?.style.getPropertyValue("--cozy-utility-columns")).toBe("7");
  expect(navigation?.querySelectorAll("button")).toHaveLength(7);
});

it("keeps gems usable while unlock state is loading and locks only when explicitly reported", () => {
  const onNavigate = vi.fn();
  render(<BattleGrowthNavigation activeRoute="market" onNavigate={onNavigate} />);

  const gems = screen.getByRole("button", { name: "보석" });
  expect((gems as HTMLButtonElement).disabled).toBe(false);

  fireEvent.click(gems);
  expect(onNavigate).toHaveBeenCalledWith("gems");

  const { unmount } = render(<BattleGrowthNavigation activeRoute="market" onNavigate={onNavigate} gemContentUnlocked={false} />);
  const locked = screen.getByRole("button", { name: "보석 1-5 해금" });
  expect((locked as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(locked);
  unmount();

  fireEvent.click(screen.getByRole("button", { name: "장비" }));
  expect(onNavigate).toHaveBeenCalledWith("equipment");
});

it("enables dungeon and gems when the server reports gem content unlocked", () => {
  const onNavigate = vi.fn();
  render(withQuery(<>
    <BattleUtilityNavigation onNavigate={onNavigate} gemContentUnlocked />
    <BattleGrowthNavigation onNavigate={onNavigate} gemContentUnlocked />
  </>));

  fireEvent.click(screen.getByRole("button", { name: "콘텐츠" }));
  const dungeon = screen.getByRole("button", { name: /^던전/ });
  const gems = screen.getByRole("button", { name: "보석" });
  expect((dungeon as HTMLButtonElement).disabled).toBe(false);
  expect(dungeon.textContent).toContain("입장 가능");
  expect((gems as HTMLButtonElement).disabled).toBe(false);

  fireEvent.click(dungeon);
  fireEvent.click(gems);
  expect(onNavigate).toHaveBeenNthCalledWith(1, "dungeon");
  expect(onNavigate).toHaveBeenNthCalledWith(2, "gems");
});
it("navigates directly to cosmetic gacha from the growth menu", () => {
  const onNavigate = vi.fn();
  render(withQuery(<BattleUtilityNavigation activeRoute="market" onNavigate={onNavigate} cosmeticContentUnlocked />));

  fireEvent.click(screen.getByRole("button", { name: "치장 뽑기" }));
  expect(onNavigate).toHaveBeenCalledWith("cosmetics");
});

it("disables cosmetic gacha until the server reports the cosmetic system unlocked", () => {
  const onNavigate = vi.fn();
  render(withQuery(<BattleUtilityNavigation activeRoute="market" onNavigate={onNavigate} />));

  const cosmetics = screen.getByRole("button", { name: "치장 뽑기 1-5 해금" });
  expect((cosmetics as HTMLButtonElement).disabled).toBe(true);

  fireEvent.click(cosmetics);
  expect(onNavigate).not.toHaveBeenCalled();
});
