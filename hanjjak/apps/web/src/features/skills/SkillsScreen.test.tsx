// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, expect, it, vi } from "vitest";
import { skillsApi, type SkillState, type SkillSummary } from "./api";
import { SkillsScreen } from "./SkillsScreen";

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

it("renders the server-authored action with composite books and promotion controls", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  client.setQueryData(["skills"], {
    skills: [
      {
        skillId: "active_heavy", name: "한짝의 일격", active: true, unlocked: true,
        grade: "RARE", gradeName: "희귀", level: 10, equippedSlot: null,
        effectText: "공격력 455%",
        action: {
          kind: "PROMOTE", targetGrade: "EPIC", targetLevel: 1,
          books: [
            { itemId: "skillbook:active_heavy:normal", displayName: "노말 한짝의 일격 비법서", requiredQuantity: 4, availableQuantity: 4 },
            { itemId: "skillbook:active_heavy:rare", displayName: "희귀 한짝의 일격 비법서", requiredQuantity: 2, availableQuantity: 2 },
            { itemId: "skillbook:active_heavy:epic", displayName: "영웅 한짝의 일격 비법서", requiredQuantity: 1, availableQuantity: 0 },
          ], riceCost: 8_100, successBasisPoints: 10_000, executable: false, disabledReason: "INSUFFICIENT_SKILLBOOK",
        },
      },
      {
        skillId: "active_dot", name: "마! 쫄이나", active: true, unlocked: true,
        grade: "EPIC", gradeName: "영웅", level: 10, equippedSlot: null,
        effectText: "5초 총 865%",
        action: { kind: "LOCKED", targetGrade: "LEGENDARY", targetLevel: 1, books: [], riceCost: 16_200, successBasisPoints: 10_000, executable: false, disabledReason: "SKILL_GRADE_LOCKED" },
      },
    ],
    activeLoadout: [], riceBalance: 10_000,
  });

  const html = renderToStaticMarkup(<QueryClientProvider client={client}><SkillsScreen /></QueryClientProvider>);
  const root = document.createElement("div");
  root.innerHTML = html;
  const text = root.textContent ?? "";
  expect(html).toContain("희귀 → 영웅 +1 승급");
  expect(html).toContain("8,100");
  /* 필요한 값과 재료는 고른 스킬 하나에 대해서만 아래 칸에 나온다. 처음에는 첫 장이 골라져 있다. */
  expect(root.querySelectorAll(".skill-enhance-panel").length).toBe(1);
  /* 올릴 수 있는 것만 카드에 적는다. 못 올리는 이유까지 딱지로 붙이면 될 것이 묻힌다. */
  expect(Array.from(root.querySelectorAll(".skill-readiness")).map(node => node.textContent)).toEqual([]);
  expect(text).not.toContain("보유 4개");
  expect(Array.from(root.querySelectorAll(".skill-book-cost span")).map(node => node.textContent)).toEqual([
    "노말4 / 4",
    "희귀2 / 2",
    "영웅0 / 1",
  ]);
  expect(root.querySelector(".skill-book-cost")?.getAttribute("aria-label")).toContain("등급별 스킬북");
  expect(root.querySelector(".skill-enhance-panel .skill-success-rate")?.textContent).toBe("100%성공률");
  expect(root.querySelector(".skill-growth-card .skill-success-rate")).toBeNull();
  expect(root.querySelector(".skill-growth-card button.skill-promote-button, .skill-growth-card button.skill-enhance-button")).toBeNull();
  expect(root.querySelector(".skill-growth-card .skill-resource-cost")).toBeNull();
  expect(root.querySelectorAll(".skill-kind.is-active")).toHaveLength(2);
  expect(root.querySelector(".skill-auto-status, .skill-passive-state")).toBeNull();
  expect(root.querySelector('img[alt="한짝의 일격 스킬 아이콘"]')).not.toBeNull();
  expect(root.querySelector('[aria-label="한짝의 일격 스킬 아이콘 준비 중"]')).toBeNull();
});
it("blocks new loadout and growth commands after uncertain loadout and retries exact request", async () => {
  const skill: SkillSummary = { skillId: "active_heavy", name: "한짝의 일격", active: true, unlocked: true, grade: "RARE", gradeName: "희귀", level: 1, equippedSlot: null, effectText: "공격력", action: { kind: "ENHANCE", targetGrade: null, targetLevel: 2, books: [], riceCost: 1, successBasisPoints: 10000, executable: true, disabledReason: null } };
  const state: SkillState = { skills: [skill], activeLoadout: [], riceBalance: 10 };
  const next: SkillState = { ...state, activeLoadout: [skill.skillId], skills: [{ ...skill, equippedSlot: 1 }] };
  const key = "00000000-0000-4000-8000-000000000012";
  vi.spyOn(crypto, "randomUUID").mockReturnValue(key);
  const updateLoadout = vi.spyOn(skillsApi, "updateLoadout").mockRejectedValueOnce(new TypeError("connection lost")).mockResolvedValueOnce(next);
  const command = vi.spyOn(skillsApi, "command").mockResolvedValue({ skill, success: true, state: next });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  client.setQueryData(["skills"], state);
  render(<QueryClientProvider client={client}><SkillsScreen /></QueryClientProvider>);
  fireEvent.click(screen.getByRole("button", { name: "1번 슬롯에 스킬 장착" }));
  fireEvent.click(screen.getByRole("button", { name: "한짝의 일격 자동 사용 장착" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "같은 장착 요청 다시 확인" })).toBeTruthy());
  expect(screen.getByRole("button", { name: "강화" })).toHaveProperty("disabled", true);
  fireEvent.click(screen.getByRole("button", { name: "같은 장착 요청 다시 확인" }));
  await waitFor(() => expect(updateLoadout).toHaveBeenCalledTimes(2));
  expect(updateLoadout.mock.calls.map(call => call)).toEqual([[ [skill.skillId], key ], [ [skill.skillId], key ]]);
  // 저장 성공은 더 이상 문구로 알리지 않는다. 알림이 닫히고 명령이 다시 열리는 것으로 확인한다.
  await waitFor(() => expect(screen.queryByRole("button", { name: "같은 장착 요청 다시 확인" })).toBeNull());
  await waitFor(() => expect(screen.getByRole("button", { name: "강화" })).toHaveProperty("disabled", false));
  expect(command).not.toHaveBeenCalled();
});

it("blocks loadout changes while a growth result is uncertain", async () => {
  const skill: SkillSummary = { skillId: "active_heavy", name: "한짝의 일격", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 1, equippedSlot: null, effectText: "공격력", action: { kind: "ENHANCE", targetGrade: "NORMAL", targetLevel: 2, books: [], riceCost: 90, successBasisPoints: 7000, executable: true, disabledReason: null } };
  const state: SkillState = { skills: [skill], activeLoadout: [], riceBalance: 100 };
  vi.spyOn(crypto, "randomUUID").mockReturnValue("00000000-0000-4000-8000-000000000013");
  const command = vi.spyOn(skillsApi, "command").mockRejectedValueOnce(new TypeError("connection lost"));
  const updateLoadout = vi.spyOn(skillsApi, "updateLoadout").mockResolvedValue(state);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  client.setQueryData(["skills"], state);
  render(<QueryClientProvider client={client}><SkillsScreen /></QueryClientProvider>);
  fireEvent.click(screen.getByRole("button", { name: /강화$/ }));
  await waitFor(() => expect(screen.getByRole("button", { name: "같은 요청 다시 확인" })).toBeTruthy());
  const loadoutButton = screen.getByRole("button", { name: "1번 슬롯에 스킬 장착" });
  expect(loadoutButton.getAttribute("aria-disabled")).toBe("true");
  fireEvent.click(loadoutButton);
  expect(updateLoadout).not.toHaveBeenCalled();
  expect(command).toHaveBeenCalledTimes(1);
});

it("removes an equipped active skill when its automatic-use slot is selected", async () => {
  const skill: SkillSummary = { skillId: "active_dot", name: "마! 쫄이나", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: 1, effectText: "지속 피해", action: { kind: "ENHANCE", targetGrade: "NORMAL", targetLevel: 8, books: [], riceCost: 630, successBasisPoints: 3000, executable: true, disabledReason: null } };
  const state: SkillState = { skills: [skill], activeLoadout: [skill.skillId], riceBalance: 1_000 };
  const next: SkillState = { ...state, activeLoadout: [], skills: [{ ...skill, equippedSlot: null }] };
  const key = "00000000-0000-4000-8000-000000000014";
  vi.spyOn(crypto, "randomUUID").mockReturnValue(key);
  const updateLoadout = vi.spyOn(skillsApi, "updateLoadout").mockResolvedValue(next);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  client.setQueryData(["skills"], state);
  render(<QueryClientProvider client={client}><SkillsScreen /></QueryClientProvider>);
  fireEvent.click(screen.getByRole("button", { name: "1번 슬롯의 마! 쫄이나 장착 해제" }));
  await waitFor(() => expect(updateLoadout).toHaveBeenCalledWith([], key));
});

it("re-equips a skill after unequip even when the per-skill slot field is stale", async () => {
  const skill: SkillSummary = { skillId: "active_dot", name: "마! 쫄이나", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: 1, effectText: "지속 피해", action: { kind: "ENHANCE", targetGrade: "NORMAL", targetLevel: 8, books: [], riceCost: 630, successBasisPoints: 3000, executable: true, disabledReason: null } };
  const state: SkillState = { skills: [skill], activeLoadout: [], riceBalance: 1_000 };
  const next: SkillState = { ...state, activeLoadout: [skill.skillId] };
  const key = "00000000-0000-4000-8000-000000000015";
  vi.spyOn(crypto, "randomUUID").mockReturnValue(key);
  const updateLoadout = vi.spyOn(skillsApi, "updateLoadout").mockResolvedValue(next);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  client.setQueryData(["skills"], state);
  render(<QueryClientProvider client={client}><SkillsScreen /></QueryClientProvider>);
  fireEvent.click(screen.getByRole("button", { name: "1번 슬롯에 스킬 장착" }));
  fireEvent.click(screen.getByRole("button", { name: "마! 쫄이나 자동 사용 장착" }));
  await waitFor(() => expect(updateLoadout).toHaveBeenCalledWith([skill.skillId], key));
});

it("moves the enhance panel to whichever skill the player picks", async () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const heavy: SkillSummary = {
    skillId: "active_heavy", name: "한짝의 일격", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 2, equippedSlot: null,
    effectText: "공격력 455%",
    action: { kind: "ENHANCE", targetGrade: null, targetLevel: 3, books: [], riceCost: 1_000, successBasisPoints: 10_000, executable: true, disabledReason: null },
  };
  const passive: SkillSummary = {
    skillId: "passive_critical", name: "회심의 간", active: false, unlocked: true, grade: "RARE", gradeName: "희귀", level: 4, equippedSlot: null,
    effectText: "치명타 +19%p",
    action: { kind: "ENHANCE", targetGrade: null, targetLevel: 5, books: [], riceCost: 210, successBasisPoints: 5_000, executable: false, disabledReason: "INSUFFICIENT_SKILLBOOK" },
  };
  client.setQueryData(["skills"], { skills: [heavy, passive], activeLoadout: [], riceBalance: 10_000 } satisfies SkillState);

  render(<QueryClientProvider client={client}><SkillsScreen /></QueryClientProvider>);

  const panel = () => document.querySelector(".skill-enhance-panel");
  expect(panel()?.getAttribute("aria-label")).toBe("한짝의 일격 강화");
  expect(panel()?.querySelector(".skill-enhance-label")?.textContent).toBe("필요 재료");
  expect(panel()?.querySelector("button")?.textContent).toBe("강화하기");

  fireEvent.click(screen.getByRole("button", { name: "회심의 간 강화 대상으로 고르기" }));

  await waitFor(() => expect(panel()?.getAttribute("aria-label")).toBe("회심의 간 강화"));
  expect(panel()?.querySelector(".skill-success-rate b")?.textContent).toBe("50%");
  expect((panel()?.querySelector("button") as HTMLButtonElement).disabled).toBe(true);
  /* 패시브는 파란 태그, 액티브는 주황 태그로 갈린다. */
  expect(document.querySelectorAll(".skill-kind.is-passive")).toHaveLength(1);
});
