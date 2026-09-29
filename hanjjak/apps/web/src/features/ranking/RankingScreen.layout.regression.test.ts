// @vitest-environment node

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

describe("RankingScreen layout regressions", () => {
  it("anchors podium characters to the podium artwork and renders equipped appearance layers", () => {
    const component = readFileSync(resolve(process.cwd(), "src/features/ranking/RankingScreen.tsx"), "utf8");
    const css = readFileSync(resolve(process.cwd(), "src/features/ranking/RankingScreen.css"), "utf8");
    expect(component).toContain("ranking-podium-platform");
    // 색만 돌린 빈 칸이 아니라 치장 탭과 같은 레이어 조합을 쓴다.
    expect(component).toContain("cosmeticEquipmentFromAppearance(entry.appearance)");
    expect(component).not.toContain("ranking-cosmetic-layer");
    expect(css).toContain("aspect-ratio: 1760 / 912");
    expect(css).toContain(".ranking-podium-rank.rank-1 { z-index: 4; left: 50%; bottom: 59%");
    expect(css).toContain(".rank-1 .ranking-character-wrap { transform: translateX(9%) scale(1.98)");
  });

  it("uses only the panel artwork separators and keeps the current-user highlight inside its row", () => {
    const css = readFileSync(resolve(process.cwd(), "src/features/ranking/RankingScreen.css"), "utf8");
    const rowRule = css.match(/\.ranking-list li \{[^}]+\}/)?.[0] ?? "";
    const mineRule = css.match(/\.ranking-list li\.is-mine \{[^}]+\}/)?.[0] ?? "";
    expect(rowRule).not.toContain("border-bottom");
    // 내 줄도 다른 줄과 같은 자리에 선다. 색으로만 구분한다.
    expect(mineRule).toContain("margin: 0");
    expect(mineRule).toContain("background:");
    expect(mineRule).not.toContain("margin-inline: -");
  });

  it("places the current player in the artwork footer and keeps narrow tabs inside the frame", () => {
    const component = readFileSync(resolve(process.cwd(), "src/features/ranking/RankingScreen.tsx"), "utf8");
    const css = readFileSync(resolve(process.cwd(), "src/features/ranking/RankingScreen.css"), "utf8");
    expect(component).not.toContain("<em>나</em>");
    expect(component).toContain('<span className="ranking-row-rank"><span>{myRank}</span></span>');
    expect(css).toContain(".ranking-my-position { position: absolute;");
    expect(css).toContain("width: calc(100% - 24px)");
  });
});
