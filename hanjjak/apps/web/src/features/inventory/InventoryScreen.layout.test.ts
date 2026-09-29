import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const inventoryCss = readFileSync(new URL("./InventoryScreen.css", import.meta.url), "utf8");

describe("inventory viewport fit", () => {
  it("keeps the dialog inside the viewport instead of translating it past the bottom edge", () => {
    const dialogRule = inventoryCss.match(/\.inventory-window\s*\{[^}]+\}/)?.[0] ?? "";

    expect(dialogRule).toContain("width: var(--paper-width)");
    expect(dialogRule).toContain("height: var(--paper-min-height)");
    expect(dialogRule).toContain("overflow: hidden");
    expect(dialogRule).not.toContain("translateY");
  });

  it("lets the content row shrink within the fixed-height paper", () => {
    expect(inventoryCss).toMatch(/\.inventory-window \.inventory-screen\s*\{[^}]*height: 100%/s);
    expect(inventoryCss).toMatch(/\.inventory-v2-paper\s*\{[^}]*grid-template-rows: auto auto minmax\(0, 1fr\)/s);
    expect(inventoryCss).toMatch(/\.inventory-v2-layout\s*\{[^}]*min-height: 0[^}]*overflow: hidden/s);
  });
});
