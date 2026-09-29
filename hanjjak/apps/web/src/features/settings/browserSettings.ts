export const BROWSER_SETTINGS_STORAGE_KEY = "hanjjak.browser-settings.v1";

export type BrowserSettings = {
  soundEnabled: boolean;
  volume: number;
  powerSavingEnabled: boolean;
};

export const DEFAULT_BROWSER_SETTINGS: BrowserSettings = {
  soundEnabled: true,
  volume: 70,
  powerSavingEnabled: false,
};

export function parseBrowserSettings(raw: string | null): BrowserSettings {
  if (!raw) return { ...DEFAULT_BROWSER_SETTINGS };

  try {
    const value = JSON.parse(raw) as Partial<BrowserSettings> | null;
    if (!value || typeof value !== "object") return { ...DEFAULT_BROWSER_SETTINGS };

    return {
      soundEnabled: typeof value.soundEnabled === "boolean" ? value.soundEnabled : DEFAULT_BROWSER_SETTINGS.soundEnabled,
      volume: typeof value.volume === "number" && Number.isFinite(value.volume)
        ? Math.min(100, Math.max(0, Math.round(value.volume)))
        : DEFAULT_BROWSER_SETTINGS.volume,
      powerSavingEnabled: typeof value.powerSavingEnabled === "boolean"
        ? value.powerSavingEnabled
        : DEFAULT_BROWSER_SETTINGS.powerSavingEnabled,
    };
  } catch {
    return { ...DEFAULT_BROWSER_SETTINGS };
  }
}

export function loadBrowserSettings(): BrowserSettings {
  try {
    return parseBrowserSettings(globalThis.localStorage?.getItem(BROWSER_SETTINGS_STORAGE_KEY) ?? null);
  } catch {
    return { ...DEFAULT_BROWSER_SETTINGS };
  }
}

export function applyBrowserSettings(settings: BrowserSettings): void {
  if (typeof document === "undefined") return;
  document.documentElement.dataset.soundEnabled = String(settings.soundEnabled);
  document.documentElement.dataset.powerSaving = String(settings.powerSavingEnabled);
  document.documentElement.style.setProperty("--hanjjak-volume", String(settings.soundEnabled ? settings.volume / 100 : 0));
}

export function saveBrowserSettings(settings: BrowserSettings): void {
  try {
    globalThis.localStorage?.setItem(BROWSER_SETTINGS_STORAGE_KEY, JSON.stringify(settings));
  } catch {
    // Storage may be unavailable in private or restricted browser contexts.
  }
  applyBrowserSettings(settings);
}
