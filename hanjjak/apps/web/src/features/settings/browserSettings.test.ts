import { describe, expect, it } from "vitest";
import { DEFAULT_BROWSER_SETTINGS, parseBrowserSettings } from "./browserSettings";

describe("browser settings", () => {
  it("uses defaults when storage is empty or malformed", () => {
    expect(parseBrowserSettings(null)).toEqual(DEFAULT_BROWSER_SETTINGS);
    expect(parseBrowserSettings("not-json")).toEqual(DEFAULT_BROWSER_SETTINGS);
  });

  it("restores valid values and clamps volume", () => {
    expect(parseBrowserSettings(JSON.stringify({ soundEnabled: false, volume: 130, powerSavingEnabled: true }))).toEqual({
      soundEnabled: false,
      volume: 100,
      powerSavingEnabled: true,
    });
  });

  it("fills missing or invalid fields from defaults", () => {
    expect(parseBrowserSettings(JSON.stringify({ soundEnabled: "yes", volume: 32.7 }))).toEqual({
      soundEnabled: true,
      volume: 33,
      powerSavingEnabled: false,
    });
  });
});
