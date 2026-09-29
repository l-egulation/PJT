import { describe, expect, it } from "vitest";
import { COSMETIC_RUNTIME_PACKAGES, runtimeLoadoutFor } from "./cosmeticRuntimePackages";

const fullSet = (slug: string) => Object.fromEntries(["HEAD", "TOP", "CAPE", "GLOVES", "SHOES", "BOTTOM"].map(s => [s, slug]));

describe("manifest-driven cosmetic sets", () => {
  it("maps a whole worn set to its manifest items", () => {
    for (const slug of Object.keys(COSMETIC_RUNTIME_PACKAGES)) {
      const resolved = runtimeLoadoutFor(fullSet(slug));
      expect(resolved, slug).not.toBeNull();
      expect(resolved!.runtimePackage.packageId).toBe(COSMETIC_RUNTIME_PACKAGES[slug].packageId);
      // 여섯 부위가 모두 그 세트의 아이템으로 채워져야 매니페스트 겹침 순서가 성립한다.
      expect(Object.values(resolved!.equipped).every(Boolean)).toBe(true);
    }
  });

  it("refuses a part-worn loadout so the draw order never loses a slot", () => {
    const partial = { ...fullSet("angel"), CAPE: undefined };
    expect(runtimeLoadoutFor(partial)).toBeNull();
  });

  it("refuses a mixed loadout", () => {
    expect(runtimeLoadoutFor({ ...fullSet("angel"), HEAD: "chef" })).toBeNull();
  });

  it("leaves sets without a manifest package to the per-file layer path", () => {
    expect(runtimeLoadoutFor(fullSet("chef"))).toBeNull();
    expect(runtimeLoadoutFor(fullSet("leather-guard"))).toBeNull();
  });
});
