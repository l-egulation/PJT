// @vitest-environment happy-dom
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { DungeonSelection, secondsUntilNextDungeonSlot } from "./GemsScreen";
import { FusionPanel, PresetEditor } from "./GemManagement";
import type { GemDungeonToday, GemState, GemSummary } from "./api";
const gemStyles = readFileSync(resolve(process.cwd(), "src/features/gems/gem-management.css"), "utf8");

const cssRule = (selector: string) => {
  const start = gemStyles.indexOf(`${selector} {`);
  if (start < 0) return "";
  return gemStyles.slice(start + selector.length + 2, gemStyles.indexOf("}", start));
};

afterEach(cleanup);

const gem = (gemId: string, level: number): GemSummary => ({ gemId, level, option: "FLAT_ATTACK", optionName: "고정 공격력", value: level * 3, locked: false, reservedForSale: false, equippedPresets: [] });
const state = (gems: GemSummary[]): GemState => ({ unlocked: true, tickets: 2, secondsUntilNextTicket: 999, todayBoss: "SURVIVAL", gemBoxQuantity: 0, gems, presets: {}, lockedPresets: [], contentVersion: "test" });

describe("gem UI regressions", () => {
  it("opens in the same window box as the bag and gear windows", () => {
    const inventoryStyles = readFileSync(resolve(process.cwd(), "src/features/inventory/InventoryScreen.css"), "utf8");
    const box = (css: string, selector: string) => {
      const start = css.indexOf(selector);
      const body = css.slice(start, css.indexOf("}", start));
      return { width: body.match(/[^-]width:([^;]+)/)?.[1], height: body.match(/[^-]height:([^;]+)/)?.[1] };
    };
    expect(box(gemStyles, ".gem-management-dialog {")).toEqual(box(inventoryStyles, ".inventory-window {"));
    expect(cssRule(".gem-management-dialog::backdrop")).toContain("backdrop-filter: blur(3px) saturate(.92)");
  });

  it("shows a live-aligned recharge and first-clear reward quantity", () => {
    const now = Date.parse("2026-09-12T04:37:11Z");
    const dungeon: GemDungeonToday = { boss: "SURVIVAL", tickets: 2, secondsUntilNextTicket: 999, progress: [], nextChallengeStage: 1, sweepStage: null, activeChallenge: null, testBossSelectionEnabled: false };
    render(<DungeonSelection dungeon={dungeon} busy={false} now={now} onSelectBoss={() => undefined} onStart={() => undefined} onSweep={() => undefined} />);

    expect(secondsUntilNextDungeonSlot(now)).toBe(22 * 60 + 49);
    expect(screen.getByText("충전까지 00:22:49")).toBeDefined();
    expect(screen.getByText("최초 클리어 시")).toBeDefined();
    expect(screen.getByText(/보석함/).parentElement?.textContent).toContain("X 10개");
  });

  it("disables the sweep button while sweeping is locked", () => {
    const dungeon: GemDungeonToday = { boss: "SURVIVAL", tickets: 3, secondsUntilNextTicket: 0, progress: [], nextChallengeStage: 1, sweepStage: null, activeChallenge: null, testBossSelectionEnabled: false };
    render(<DungeonSelection dungeon={dungeon} busy={false} onSelectBoss={() => undefined} onStart={() => undefined} onSweep={() => undefined} />);

    expect(screen.getByRole("button", { name: "소탕 잠김" })).toHaveProperty("disabled", true);
  });

  it("keeps the preset tabs, slots and inventory grid inside the approved layout", () => {
    const data = { ...state([gem("equipped", 1)]), gemBoxQuantity: 1, presets: { MAIN: [gem("equipped", 1)] } };
    render(<PresetEditor data={data} busy={false} riceBalance={20_000} onSave={() => undefined} />);

    expect(cssRule(".gem-management-window .gem-slot-zone")).toContain("grid-template-columns: repeat(3,minmax(0,1fr))");
    expect(cssRule(".gem-management-window .gem-slot-zone")).toContain("grid-template-rows: repeat(2,minmax(0,1fr))");
    expect(cssRule(".gem-management-window .gem-tile-grid")).toContain("grid-template-columns: repeat(4,minmax(0,1fr))");
    expect(cssRule(".gem-management-window .gem-tile-grid")).toContain("overflow-y: auto");
    expect(cssRule(".gem-management-window .gem-preset-tabs")).toContain("overflow-x: auto");

    expect(screen.queryByRole("button", { name: "보석함 1개 열기" })).toBeNull();
    expect(screen.getAllByRole("tab")).toHaveLength(3);
    const unlockButton = screen.getByRole("button", { name: "프리셋 4 열기" });
    expect(unlockButton.querySelector("span")?.getAttribute("aria-hidden")).toBe("true");
    expect(gemStyles).toContain(".gem-management-window .gem-preset-plus > span::before");
    expect(gemStyles).toContain("background: currentColor");
    expect(document.querySelectorAll(".gem-preset-tabs > *")).toHaveLength(4);
    expect(document.querySelectorAll(".gem-slot-card")).toHaveLength(6);
    expect(document.querySelector(".gem-slot-card.next")?.textContent).toContain("다음 장착");
  });

  it("keeps the batch step list scrollable so all six level steps stay reachable", async () => {
    const input = [gem("a", 1), gem("b", 1), gem("c", 1)];
    const levelTwo = gem("result", 2);
    const onFuse = vi.fn().mockResolvedValue({ consumedGemIds: input.map((item) => item.gemId), granted: [levelTwo], state: state([levelTwo]) });
    render(<FusionPanel data={state(input)} busy={false} onFuse={onFuse} />);

    fireEvent.click(screen.getByRole("tab", { name: "일괄 합성" }));
    expect(document.querySelectorAll(".gem-batch-step")).toHaveLength(6);
    expect(document.querySelectorAll(".gem-batch-step.disabled")).toHaveLength(5);
    expect(cssRule(".gem-management-window .gem-batch-steps")).toContain("overflow-y: auto");

    fireEvent.click(screen.getByRole("button", { name: "일괄 합성" }));

    await waitFor(() => expect(onFuse).toHaveBeenCalledWith("SAFE_BATCH", [], expect.objectContaining({ targetLevel: 7 })));
    await waitFor(() => expect(screen.getByRole("dialog").textContent).toContain("2레벨 고정 공격력 보석을 획득하였습니다"), { timeout: 1500 });
  });
});
