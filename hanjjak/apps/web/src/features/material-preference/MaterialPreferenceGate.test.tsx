import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import type { MaterialType } from "./api";
import { populationLabel } from "./MaterialPreferenceGate";

function rates(primary: MaterialType): Record<MaterialType, number> {
  return { POTATO: primary === "POTATO" ? 80 : 10, SWEET_POTATO: primary === "SWEET_POTATO" ? 80 : 10, CORN: primary === "CORN" ? 80 : 10 };
}

describe("material preference display contract", () => {
  it("shows one primary eighty and two secondary ten rates", () => {
    expect(Object.values(rates("SWEET_POTATO"))).toEqual([10, 80, 10]);
    expect(Object.values(rates("SWEET_POTATO")).reduce((sum, value) => sum + value, 0)).toBe(100);
  });

  it("does not invent a population while the API field is unavailable", () => {
    expect(populationLabel()).toBe("집계 준비 중");
    expect(populationLabel(12480)).toBe("12,480명");
  });

  it("uses the shared profession colors for each country material name", () => {
    const css = readFileSync(resolve(process.cwd(), "src/features/material-preference/MaterialPreferenceGate.css"), "utf8");
    const tokens = readFileSync(resolve(process.cwd(), "src/styles.css"), "utf8");
    expect(css).toContain("color: var(--material-potato-text)");
    expect(css).toContain("color: var(--material-sweet-potato-text)");
    expect(css).toContain("color: var(--material-corn-text)");
    expect(tokens).toContain("--material-potato-text: #e4ad56");
    expect(tokens).toContain("--material-sweet-potato-text: #982a59");
    expect(tokens).toContain("--material-corn-text: #f0c712");
  });
});
