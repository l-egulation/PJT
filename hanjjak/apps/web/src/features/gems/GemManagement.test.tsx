// @vitest-environment happy-dom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { calculateBatchPlan, FusionPanel, GemManagement, PresetEditor, sortGemsByLevel } from "./GemManagement";
import type { GemState, GemSummary } from "./api";

afterEach(() => {
  cleanup();
  window.localStorage.clear();
});

function gem(gemId: string, level: number, option: GemSummary["option"] = "FLAT_ATTACK"): GemSummary {
  return { gemId, level, option, optionName: `${option} ${gemId}`, value: level * 8, locked: false, reservedForSale: false, equippedPresets: [] };
}

function state(gems: GemSummary[]): GemState {
  return { unlocked: true, tickets: 3, secondsUntilNextTicket: 600, todayBoss: "SURVIVAL", gemBoxQuantity: 2, gems, presets: {}, lockedPresets: [], contentVersion: "v1" };
}

describe("GemManagement inventory", () => {
  it("lays the owned gems out as a grid sorted by the highest level", () => {
    const gems = Array.from({ length: 25 }, (_, index) => gem(`gem-${String(index).padStart(2, "0")}`, index % 5 + 1));
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity } } });
    client.setQueryData(["gems"], state(gems));
    render(<QueryClientProvider client={client}><GemManagement /></QueryClientProvider>);

    expect(document.querySelectorAll(".gem-tile:not(.blank)")).toHaveLength(25);
    expect(document.querySelector(".gem-tile:not(.blank)")?.textContent).toContain("Lv.5");
    expect(document.querySelector(".gem-inventory-detail")?.textContent).toContain("5레벨");
    client.clear();
  });

  it("keeps preset edits as a draft until save and filters the inventory", () => {
    const attack = gem("attack", 3);
    const hp = gem("hp", 2, "FLAT_HP");
    const haste = gem("haste", 4, "HASTE");
    const data = { ...state([attack, hp, haste]), presets: { MAIN: [attack] } };
    const onSave = vi.fn();
    render(<PresetEditor data={data} busy={false} onSave={onSave} />);

    expect(screen.getByText("현재 적용 효과")).toBeDefined();
    fireEvent.click(screen.getByRole("button", { name: /^2레벨 FLAT_HP hp/ }));
    expect(onSave).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "프리셋 저장" }));
    expect(onSave).toHaveBeenCalledWith(["MAIN"], ["attack", "hp"]);

    fireEvent.click(screen.getByRole("button", { name: "특수" }));
    expect(document.querySelectorAll(".gem-tile:not(.blank)")).toHaveLength(1);
    expect(document.querySelector(".gem-inventory-detail")?.textContent).toContain("HASTE haste");
  });

  it("shows three default presets, renames them, and links combat contexts", () => {
    const data = state([]);
    render(<PresetEditor data={data} busy={false} riceBalance={20_000} onSave={vi.fn()} />);

    expect(screen.getByRole("tab", { name: /프리셋 1/ })).toBeDefined();
    expect(screen.getByRole("tab", { name: /프리셋 2/ })).toBeDefined();
    expect(screen.getByRole("tab", { name: /프리셋 3/ })).toBeDefined();
    expect(screen.getByRole("button", { name: "프리셋 4 열기" })).toBeDefined();
    expect(screen.queryByRole("tab", { name: "프리셋 5" })).toBeNull();
    expect(document.querySelectorAll(".gem-preset-tabs > *")).toHaveLength(4);

    fireEvent.click(screen.getByRole("button", { name: "프리셋 1 이름 수정" }));
    fireEvent.change(screen.getByRole("textbox", { name: "프리셋 1 이름" }), { target: { value: "보스용" } });
    fireEvent.keyDown(screen.getByRole("textbox", { name: "프리셋 1 이름" }), { key: "Enter" });
    expect(screen.getByRole("tab", { name: /보스용/ })).toBeDefined();

    fireEvent.click(screen.getByRole("button", { name: "프리셋 4 열기" }));
    expect(screen.getByRole("dialog", { name: "프리셋 열기 확인" }).textContent).toContain("5,000 쌀");
    fireEvent.click(screen.getByRole("button", { name: "아니오" }));
    expect(screen.queryByRole("dialog", { name: "프리셋 열기 확인" })).toBeNull();

    fireEvent.click(screen.getByRole("tab", { name: "프리셋 2" }));
    fireEvent.click(screen.getByRole("button", { name: "메인 전투" }));
    expect(screen.getByRole("button", { name: "메인 전투" }).getAttribute("aria-pressed")).toBe("true");
    fireEvent.click(screen.getByRole("tab", { name: "보스용" }));
    expect(screen.getByRole("button", { name: "메인 전투" }).getAttribute("aria-pressed")).toBe("false");
  });

  it("switches between the preset and fusion pages and closes the window", () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity } } });
    client.setQueryData(["gems"], state([gem("one", 2), gem("two", 2), gem("three", 2)]));
    const onClose = vi.fn();
    render(<QueryClientProvider client={client}><GemManagement onClose={onClose} /></QueryClientProvider>);

    fireEvent.click(screen.getByRole("tab", { name: /합성/ }));
    expect(screen.getByRole("heading", { name: "보석 합성" })).toBeDefined();
    fireEvent.click(screen.getByRole("button", { name: "보석 창 닫기" }));
    expect(onClose).toHaveBeenCalledOnce();
    client.clear();
  });

  it("uses stable gem ids to break ties in either level direction", () => {
    const gems = [gem("b", 2), gem("a", 2), gem("c", 4)];
    expect(sortGemsByLevel(gems, "desc").map((item) => item.gemId)).toEqual(["c", "a", "b"]);
    expect(sortGemsByLevel(gems, "asc").map((item) => item.gemId)).toEqual(["a", "b", "c"]);
  });
});

describe("FusionPanel", () => {
  it("shows the three-gem equation and reward after synthesis", async () => {
    const gems = [gem("one", 2), gem("two", 2), gem("three", 2)];
    const data = state(gems);
    const onFuse = vi.fn().mockResolvedValue({ consumedGemIds: gems.map((item) => item.gemId), granted: [gem("result", 3)], state: data });
    render(<FusionPanel data={data} busy={false} onFuse={onFuse} />);

    expect(screen.getByRole("heading", { name: "보석 합성" })).toBeDefined();
    expect(document.querySelectorAll(".gem-fusion-ingredient")).toHaveLength(3);
    expect(document.querySelector(".gem-glyph.mystery i")?.textContent).toBe("?");
    expect(document.body.textContent).not.toContain("확인 후 합성");

    screen.getAllByRole("button", { name: /^2레벨 FLAT_ATTACK/ }).forEach((button) => fireEvent.click(button));
    fireEvent.click(screen.getByRole("button", { name: /^합성$/ }));

    await waitFor(() => expect(screen.getByRole("dialog")).toBeDefined(), { timeout: 1500 });
    expect(onFuse).toHaveBeenCalledWith("MANUAL", ["one", "three", "two"]);
    expect(screen.getByText("3레벨 FLAT_ATTACK result 보석을 획득하였습니다!")).toBeDefined();
    fireEvent.keyDown(window, { key: "Escape" });
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("returns to an actionable state when synthesis fails", async () => {
    const gems = [gem("one", 2), gem("two", 2), gem("three", 2)];
    const onFuse = vi.fn().mockRejectedValue(new Error("fusion failed"));
    render(<FusionPanel data={state(gems)} busy={false} onFuse={onFuse} />);

    screen.getAllByRole("button", { name: /^2레벨 FLAT_ATTACK/ }).forEach((button) => fireEvent.click(button));
    fireEvent.click(screen.getByRole("button", { name: /^합성$/ }));

    await waitFor(() => expect(screen.getByRole("button", { name: /^합성$/ }).hasAttribute("disabled")).toBe(false));
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("calculates every lower-level fusion needed to reach the target", () => {
    const gems = [
      ...Array.from({ length: 9 }, (_, index) => gem(`level-1-${index}`, 1)),
      gem("level-2-a", 2),
      gem("level-2-b", 2),
    ];

    expect(calculateBatchPlan(gems, 3, new Set(["FLAT_ATTACK"]))).toEqual({
      sourceCount: 11,
      fusionCount: 4,
      targetResults: 1,
    });
  });

  it("lists one scrollable fusion step per gem level with the allowed gem types", () => {
    render(<FusionPanel data={state([])} busy={false} onFuse={vi.fn()} />);

    fireEvent.click(screen.getByRole("tab", { name: "일괄 합성" }));
    expect(document.querySelectorAll(".gem-batch-step")).toHaveLength(6);
    expect(document.querySelector(".gem-batch-step")?.textContent).toContain("Lv.1");
    expect(document.querySelectorAll(".gem-batch-step")[5]?.textContent).toContain("Lv.7");
    expect(document.querySelectorAll(".gem-batch-options-grid input")).toHaveLength(6);
  });

  /* 한 단계에서 나온 보석이 다음 단계의 재료가 된다. 1레벨 셋과 2레벨 셋이면
     1->2 로 하나가 늘어 2레벨이 넷이 되고, 그중 셋이 다시 3레벨 하나가 된다.
     그 이어 올리기는 서버가 한 번에 하므로 명령도 한 번만 나간다. */
  it("hands the whole climb to the server in one safe batch command", async () => {
    const inputs = [gem("a", 1), gem("b", 1), gem("c", 1), gem("d", 2), gem("e", 2), gem("f", 2)];
    const result = gem("result", 3);
    const onFuse = vi.fn().mockResolvedValue({ consumedGemIds: [], granted: [result], state: state([result]) });
    render(<FusionPanel data={state(inputs)} busy={false} onFuse={onFuse} />);

    fireEvent.click(screen.getByRole("tab", { name: "일괄 합성" }));
    expect(screen.getByText("소모 보석").parentElement?.textContent).toContain("6개");
    fireEvent.click(screen.getByRole("button", { name: "일괄 합성" }));

    await waitFor(() => expect(onFuse).toHaveBeenCalledTimes(1));
    expect(onFuse.mock.calls[0][0]).toBe("SAFE_BATCH");
    expect(onFuse.mock.calls[0][1]).toEqual([]);
    expect(onFuse.mock.calls[0][2]).toMatchObject({ targetLevel: 7 });
  });

  /* 목표를 낮추면 그 위 단계는 아예 셈하지 않는다. */
  it("stops at the level the player picked", async () => {
    const inputs = [gem("a", 1), gem("b", 1), gem("c", 1), gem("d", 2), gem("e", 2), gem("f", 2)];
    const onFuse = vi.fn().mockResolvedValue({ consumedGemIds: [], granted: [], state: state([]) });
    render(<FusionPanel data={state(inputs)} busy={false} onFuse={onFuse} />);

    fireEvent.click(screen.getByRole("tab", { name: "일괄 합성" }));
    /* 고른 것이 곧 적용이다. 따로 누를 단추가 없다. */
    fireEvent.click(screen.getByRole("radio", { name: "Lv.2" }));
    expect(document.querySelectorAll(".gem-batch-step")).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: "일괄 합성" }));
    await waitFor(() => expect(onFuse).toHaveBeenCalledTimes(1));
    expect(onFuse.mock.calls[0][2]).toMatchObject({ targetLevel: 2 });
  });
});
