import { existsSync, readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const styles = readFileSync(new URL("./styles.css", import.meta.url), "utf8");

describe("global game font", () => {
  it("loads the shared Geekble Malang2 WOFF2 and exposes it through the UI font variable", () => {
    expect(styles).toContain('font-family: "Geekble Malang2"');
    expect(styles).toContain("./shared/assets/fonts/GeekbleMalang2WOFF2.woff2");
    expect(styles).toContain('--hanjjak-font-ui: "Geekble Malang2", "Malgun Gothic", sans-serif');
    expect(existsSync(new URL("./shared/assets/fonts/GeekbleMalang2WOFF2.woff2", import.meta.url))).toBe(true);
  });

  it("makes native form controls inherit the same font", () => {
    expect(styles).toContain("button, input, select, textarea { font: inherit; }");
    expect(styles).toContain('button[aria-label*="닫기"]');
  });
});
