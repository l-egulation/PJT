// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PresetEditor } from "./GemManagement";
import type { GemState } from "./api";

afterEach(() => {
  cleanup();
  window.localStorage.clear();
});

const data: GemState = {
  unlocked: true,
  tickets: 3,
  secondsUntilNextTicket: 0,
  todayBoss: "SURVIVAL",
  gemBoxQuantity: 0,
  gems: [],
  presets: {},
  lockedPresets: [],
  contentVersion: "test",
};

describe("gem preset visibility", () => {
  it("shows three defaults and only the next unlock action", () => {
    render(<PresetEditor data={data} busy={false} riceBalance={20_000} onSave={vi.fn()} />);

    expect(screen.getAllByRole("tab")).toHaveLength(3);
    const unlockButton = screen.getByRole("button", { name: "프리셋 4 열기" });
    expect(unlockButton.querySelector("span")?.textContent).toBe("");
    expect(unlockButton.querySelector("span")?.getAttribute("aria-hidden")).toBe("true");
    expect(screen.queryByRole("tab", { name: "프리셋 5" })).toBeNull();
    expect(document.querySelectorAll(".gem-preset-tabs > *")).toHaveLength(4);
  });

  it("opens the confirmation inside the single plus slot", () => {
    render(<PresetEditor data={data} busy={false} riceBalance={20_000} onSave={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "프리셋 4 열기" }));

    const dialog = screen.getByRole("dialog", { name: "프리셋 열기 확인" });
    expect(dialog.closest(".gem-preset-tab-shell.unlock")).not.toBeNull();
    expect(dialog.textContent).toContain("5,000 쌀");
  });

  it("moves the single unlock action to the preset immediately after the newly opened preset", () => {
    render(<PresetEditor data={data} busy={false} riceBalance={20_000} onSave={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "프리셋 4 열기" }));
    fireEvent.click(screen.getByRole("button", { name: "예" }));

    expect(screen.getByRole("tab", { name: "프리셋 4" })).toBeDefined();
    expect(screen.getByRole("button", { name: "프리셋 5 열기" })).toBeDefined();
    expect(screen.queryByRole("button", { name: "프리셋 4 열기" })).toBeNull();
    expect(screen.queryAllByRole("button", { name: /프리셋 \d+ 열기/ })).toHaveLength(1);
  });
});
