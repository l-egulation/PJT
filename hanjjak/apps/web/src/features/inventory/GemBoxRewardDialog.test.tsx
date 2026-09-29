// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { GemBoxRewardDialog } from "./InventoryScreen";
import type { GemOption, GemSummary } from "../gems/api";

afterEach(cleanup);

let sequence = 0;
function gem(level: number, option: GemOption, optionName: string, value: number): GemSummary {
  sequence += 1;
  return { gemId: `gem-${sequence}`, level, option, optionName, value, locked: false, reservedForSale: false, equippedPresets: [] };
}

describe("보석함 결과 알림", () => {
  it("같은 보석을 묶어 수량으로 세고 높은 레벨부터 보여준다", () => {
    const gems = [
      gem(1, "FLAT_ATTACK", "고정 공격력", 60),
      gem(1, "FLAT_ATTACK", "고정 공격력", 60),
      gem(3, "FLAT_HP", "고정 최대 HP", 1_200),
      gem(1, "HASTE", "공격속도", 90),
    ];
    render(<GemBoxRewardDialog gems={gems} onClose={vi.fn()} />);

    expect(screen.getByRole("dialog", { name: "보석 4개를 얻었습니다" })).toBeDefined();
    const rows = [...document.querySelectorAll(".gem-box-reward li")];
    expect(rows).toHaveLength(3);
    expect(rows[0]?.textContent).toContain("Lv.3");
    expect(rows[0]?.textContent).toContain("고정 최대 HP");
    expect(rows.find((row) => row.textContent?.includes("고정 공격력"))?.textContent).toContain("x2");
    expect(rows.find((row) => row.textContent?.includes("공격속도"))?.textContent).not.toContain("x1");
  });

  it("확인과 Escape로 닫는다", () => {
    const onClose = vi.fn();
    render(<GemBoxRewardDialog gems={[gem(2, "FLAT_ATTACK", "고정 공격력", 120)]} onClose={onClose} />);

    fireEvent.keyDown(window, { key: "Escape" });
    expect(onClose).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole("button", { name: "확인" }));
    expect(onClose).toHaveBeenCalledTimes(2);
  });
});
