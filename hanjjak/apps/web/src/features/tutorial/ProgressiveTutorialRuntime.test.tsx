// @vitest-environment happy-dom
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import type { EquipmentState } from "../equipment/api";
import type { GemState } from "../gems/api";
import type { SkillState } from "../skills/api";
import { ProgressiveTutorialRuntime } from "./ProgressiveTutorialRuntime";
import { TutorialLauncher } from "./TutorialLauncher";
import { readTutorialProgress, tutorialProgressKey } from "./tutorialProgress";
import { runtimeChapterTutorialId, type TutorialRuntimeSurface } from "./tutorialRuntimeCatalog";

const equipmentState = vi.fn<() => Promise<EquipmentState>>();
const skillState = vi.fn<() => Promise<SkillState>>();
const gemState = vi.fn<() => Promise<GemState>>();
const characterStats = vi.fn<() => Promise<{ cosmeticsUnlocked: boolean }>>();

vi.mock("../equipment/api", () => ({ equipmentApi: { state: () => equipmentState() } }));
vi.mock("../skills/api", () => ({ skillsApi: { state: () => skillState() } }));
vi.mock("../gems/api", () => ({ gemsApi: { state: () => gemState() } }));
vi.mock("../character/api", () => ({ characterApi: { stats: () => characterStats() } }));

const BLACKSMITH = runtimeChapterTutorialId("blacksmith");
const SKILL_TRAINER = runtimeChapterTutorialId("skill-trainer");
const MARKET = runtimeChapterTutorialId("market-merchant");

function craftableEquipment(craftable: boolean): EquipmentState {
  return {
    riceBalance: 5_000,
    slots: [{
      slot: "WEAPON", slotName: "무기", unlocked: !craftable, growthComplete: false, enhance: null, promote: null,
      current: { grade: null, gradeName: null, enhancementLevel: 0, q: 0, stats: { attack: 0, maxHp: 0, penetration: 0 } },
      unlock: craftable
        ? { cost: { riceCost: 100, materials: [] }, result: { grade: "NORMAL", gradeName: "일반", enhancementLevel: 0, q: 1, stats: { attack: 5, maxHp: 0, penetration: 0 } }, statIncrease: { attack: 5, maxHp: 0, penetration: 0 }, executable: true, disabledReason: null }
        : null,
    }],
  };
}

function booksOwned(quantity: number): SkillState {
  return {
    riceBalance: 0, activeLoadout: [],
    skills: [{
      skillId: "skill.basic", name: "기본 일격", active: true, unlocked: false, grade: null, gradeName: null,
      level: 0, equippedSlot: null, effectText: "",
      action: { kind: "UNLOCK", targetGrade: null, targetLevel: 1, riceCost: 0, successBasisPoints: 10_000, executable: false, disabledReason: null, books: [{ itemId: "item.book", displayName: "비법서", requiredQuantity: 1, availableQuantity: quantity }] },
    }],
  };
}

function lockedGems(): GemState {
  return { unlocked: false, tickets: 0, secondsUntilNextTicket: 0, todayBoss: "SURVIVAL", gemBoxQuantity: 0, gems: [], presets: {}, lockedPresets: [], contentVersion: "v1" };
}

function withClient(node: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return <QueryClientProvider client={client}>{node}</QueryClientProvider>;
}

/* 조건 조회와 준비 확인 주기가 모두 끝날 때까지 시간을 흘려 준다. */
async function settle(times = 6) {
  for (let round = 0; round < times; round += 1) {
    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });
  }
}

function targetFor(route: string) {
  const nav = document.createElement("nav");
  nav.className = "cozy-growth-nav";
  const button = document.createElement("button");
  button.dataset.route = route;
  nav.append(button);
  document.body.append(nav);
  return button;
}

beforeEach(() => {
  vi.useFakeTimers();
  window.localStorage.clear();
  equipmentState.mockResolvedValue(craftableEquipment(false));
  skillState.mockResolvedValue(booksOwned(0));
  gemState.mockResolvedValue(lockedGems());
  characterStats.mockResolvedValue({ cosmeticsUnlocked: false });
});

afterEach(() => {
  cleanup();
  document.body.innerHTML = "";
  vi.useRealTimers();
  vi.clearAllMocks();
  window.localStorage.clear();
});

function renderRuntime(surface: TutorialRuntimeSurface | null, options: { accountId?: string; blocked?: boolean; onNavigate?: (next: TutorialRuntimeSurface) => void } = {}) {
  return render(withClient(<ProgressiveTutorialRuntime
    accountId={options.accountId ?? "account-a"}
    surface={surface}
    blocked={options.blocked ?? false}
    onNavigate={options.onNavigate ?? (() => {})}
  />));
}

it("stays closed while the chapter condition is not met", async () => {
  targetFor("equipment");
  renderRuntime("battle");
  await settle();

  expect(screen.queryByRole("dialog")).toBeNull();
});

it("opens the blacksmith chapter once the first equipment piece becomes craftable", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  targetFor("equipment");
  renderRuntime("battle");
  await settle();

  expect(screen.getByRole("dialog")).toBeTruthy();
  expect(screen.getByText("대장장이의 장")).toBeTruthy();
  expect(screen.getAllByRole("dialog")).toHaveLength(1);
});

it("does not open again for an account that already finished or closed it", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  window.localStorage.setItem(tutorialProgressKey("account-a", BLACKSMITH), JSON.stringify({ status: "DISMISSED", updatedAt: "2026-09-15T00:00:00.000Z" }));
  targetFor("equipment");

  renderRuntime("battle");
  await settle();

  expect(screen.queryByText("대장장이의 장")).toBeNull();
});

it("records completion so a reload does not replay the chapter", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  targetFor("equipment");
  const view = renderRuntime("battle");
  await settle();

  fireEvent.click(screen.getByText("건너뛰기"));
  expect(readTutorialProgress(window.localStorage, "account-a", BLACKSMITH)).toBe("DISMISSED");
  expect(screen.queryByRole("dialog")).toBeNull();

  view.unmount();
  renderRuntime("battle");
  await settle();
  expect(screen.queryByRole("dialog")).toBeNull();
});

it("keeps progress separated per account", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  window.localStorage.setItem(tutorialProgressKey("account-a", BLACKSMITH), JSON.stringify({ status: "COMPLETED", updatedAt: "2026-09-15T00:00:00.000Z" }));
  targetFor("equipment");

  renderRuntime("battle", { accountId: "account-b" });
  await settle();

  expect(screen.getByText("대장장이의 장")).toBeTruthy();
});

it("runs simultaneously unlocked chapters one after another", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  skillState.mockResolvedValue(booksOwned(3));
  targetFor("equipment");
  targetFor("skills");

  renderRuntime("battle");
  await settle();
  expect(screen.getByText("대장장이의 장")).toBeTruthy();
  expect(screen.queryByText("비법 수련가의 장")).toBeNull();

  fireEvent.click(screen.getByText("건너뛰기"));
  await settle();

  expect(screen.getByText("비법 수련가의 장")).toBeTruthy();
  expect(screen.getAllByRole("dialog")).toHaveLength(1);
});

it("waits while another overlay owns the screen", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  targetFor("equipment");
  const blocker = document.createElement("div");
  blocker.className = "offline-reward-panel";
  document.body.append(blocker);

  renderRuntime("battle");
  await settle();
  expect(screen.queryByRole("dialog")).toBeNull();

  blocker.remove();
  await settle();
  expect(screen.getByText("대장장이의 장")).toBeTruthy();
});

it("stays closed while the character or profile window is open", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  targetFor("equipment");

  renderRuntime("battle", { blocked: true });
  await settle();

  expect(screen.queryByRole("dialog")).toBeNull();
});

it("waits for a late target instead of opening against an empty screen", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  renderRuntime("battle");
  await settle();
  expect(screen.queryByRole("dialog")).toBeNull();

  targetFor("equipment");
  await settle();
  expect(screen.getByText("대장장이의 장")).toBeTruthy();
});

it("moves the player to the screen the next step describes", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  const button = targetFor("equipment");
  const clicked = vi.fn();
  button.addEventListener("click", clicked);
  const onNavigate = vi.fn();

  renderRuntime("battle", { onNavigate });
  await settle();

  fireEvent.click(screen.getByRole("button", { name: /장비 직접 선택/ }));

  expect(clicked).toHaveBeenCalledTimes(1);
  expect(onNavigate).toHaveBeenCalledWith("equipment");
});

it("closes without recording progress when the player leaves the screen", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  targetFor("equipment");
  const view = renderRuntime("battle");
  await settle();
  expect(screen.getByText("대장장이의 장")).toBeTruthy();

  view.rerender(withClient(<ProgressiveTutorialRuntime accountId="account-a" surface="market" blocked={false} onNavigate={() => {}} />));
  await settle();

  expect(screen.queryByText("대장장이의 장")).toBeNull();
  expect(readTutorialProgress(window.localStorage, "account-a", BLACKSMITH)).toBeNull();
});

it("greets the trader on the first visit to the market screen", async () => {
  const heading = document.createElement("header");
  heading.className = "market-plaza-heading";
  document.body.append(heading);

  renderRuntime("market");
  await settle();

  expect(screen.getByText("장터 상인의 장")).toBeTruthy();
  fireEvent.click(screen.getByText("건너뛰기"));
  expect(readTutorialProgress(window.localStorage, "account-a", MARKET)).toBe("DISMISSED");
});

it("leaves automatic progress untouched when the player replays the guide by hand", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  skillState.mockResolvedValue(booksOwned(1));
  render(<TutorialLauncher />);

  fireEvent.click(screen.getByRole("button", { name: /튜토리얼 보기/ }));
  expect(screen.getByRole("dialog")).toBeTruthy();
  fireEvent.click(screen.getByText("닫기"));

  expect(readTutorialProgress(window.localStorage, "account-a", BLACKSMITH)).toBeNull();
  expect(readTutorialProgress(window.localStorage, "account-a", SKILL_TRAINER)).toBeNull();
});

it("starts at the screen step when the player walks into the screen before the guide", async () => {
  skillState.mockResolvedValue(booksOwned(1));
  const heading = document.createElement("header");
  heading.className = "skills-heading";
  document.body.append(heading);

  renderRuntime("skills");
  await settle();

  expect(screen.getByText("비법 수련가의 장")).toBeTruthy();
  expect(screen.getByText(/스킬 창이야/)).toBeTruthy();
  expect(screen.queryByText(/아래 스킬 버튼을 직접 눌러/)).toBeNull();
});

it("offers a replay button on every content screen but not on the battle screen", async () => {
  renderRuntime("market");
  await settle(1);
  expect(screen.getByRole("button", { name: "장터 상인의 장 다시 보기" })).toBeTruthy();

  cleanup();
  renderRuntime("battle");
  await settle(1);
  expect(screen.queryByRole("button", { name: /다시 보기/ })).toBeNull();
});

it("replays only the current screen's steps and leaves automatic progress alone", async () => {
  renderRuntime("gems");
  await settle(1);

  fireEvent.click(screen.getByRole("button", { name: "보석 마법사의 장 다시 보기" }));
  expect(screen.getByRole("dialog")).toBeTruthy();
  expect(screen.getByText(/보석 창이야/)).toBeTruthy();
  /* 화면을 옮기는 `보석 버튼을 눌러 봐` 단계는 다시 보기에 끼지 않는다. */
  expect(screen.queryByText(/아래 보석 버튼을 직접 눌러/)).toBeNull();

  fireEvent.click(screen.getByText("닫기"));
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(readTutorialProgress(window.localStorage, "account-a", runtimeChapterTutorialId("gem-wizard"))).toBeNull();
});

it("does not replay an automatic chapter that is already running", async () => {
  equipmentState.mockResolvedValue(craftableEquipment(true));
  const heading = document.createElement("header");
  heading.className = "equipment-heading";
  document.body.append(heading);
  const stage = document.createElement("div");
  stage.className = "equipment-loadout-stage";
  document.body.append(stage);

  renderRuntime("equipment");
  await settle();

  expect(screen.getAllByRole("dialog")).toHaveLength(1);
  expect(screen.getByText("대장장이의 장")).toBeTruthy();
});

/* 장비·스킬·보석 화면은 `<dialog open>` 그 자체다. 자기 창을 방해물로 세면 영영 안 열린다. */
function openSkillsWindow() {
  const dialogWindow = document.createElement("dialog");
  dialogWindow.setAttribute("open", "");
  dialogWindow.className = "skills-window";
  const screenRoot = document.createElement("section");
  screenRoot.className = "skills-screen";
  const heading = document.createElement("header");
  heading.className = "skills-heading";
  screenRoot.append(heading);
  dialogWindow.append(screenRoot);
  document.body.append(dialogWindow);
  return dialogWindow;
}

it("opens inside the window it is explaining instead of treating it as a blocker", async () => {
  skillState.mockResolvedValue(booksOwned(1));
  const dialogWindow = openSkillsWindow();

  renderRuntime("skills");
  await settle();

  expect(screen.getByText("비법 수련가의 장")).toBeTruthy();
  /* 창이 top layer 라서 바깥에 그리면 가려진다. 창 안에 심어야 보인다. */
  expect(dialogWindow.querySelector(".tutorial-layer")).toBeTruthy();
});

it("still waits for a window that is not the one being explained", async () => {
  skillState.mockResolvedValue(booksOwned(1));
  openSkillsWindow();
  const characterWindow = document.createElement("dialog");
  characterWindow.setAttribute("open", "");
  characterWindow.className = "character-window";
  document.body.append(characterWindow);

  renderRuntime("skills");
  await settle();
  expect(screen.queryByText("비법 수련가의 장")).toBeNull();

  characterWindow.remove();
  await settle();
  expect(screen.getByText("비법 수련가의 장")).toBeTruthy();
});
