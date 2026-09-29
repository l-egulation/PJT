// @vitest-environment node

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

describe("RankingScreen current player footer", () => {
  it("always renders the signed-in player's full ranking summary in the panel footer", () => {
    const component = readFileSync(resolve(process.cwd(), "src/features/ranking/RankingScreen.tsx"), "utf8");
    expect(component).toContain("{myEntry && <div className=\"ranking-my-position\"");
    expect(component).toContain("LV. {myEntry.level.toLocaleString()} · {myEntry.displayName}");
    expect(component).toContain("{formatPower(myEntry.combatPower)}");
    expect(component).not.toContain("myEntry && !includesMe");
  });
});
