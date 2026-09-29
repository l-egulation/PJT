import { renderToStaticMarkup } from "react-dom/server";
import { expect, it } from "vitest";
import { CharacterAbilityTab } from "./CharacterAbilityTab";

it("shows the four-panel character summary with server totals", () => {
  const html = renderToStaticMarkup(<CharacterAbilityTab data={{ nickname: "용사", level: 3, experience: 3_600, combatPower: 12_345, cosmeticsUnlocked: true, contentVersion: "v1", stats: [{ statId: "attack", label: "공격력", unit: "POINTS", total: 143, base: 100, additional: 43, calculation: "기본 100 → 상시 43 → 합계 143", sources: [
    { sourceId: "base", label: "레벨", category: "BASE", value: 100, unit: "POINTS", applied: true, reason: null },
    { sourceId: "equipment", label: "장비", category: "EQUIPMENT", value: 43, unit: "POINTS", applied: true, reason: null },
    { sourceId: "gem-low", label: "낮은 보석", category: "GEM", value: 500, unit: "BASIS_POINTS", applied: false, reason: "높은 동일 옵션 적용" },
  ] }] }} />);
  expect(html).toContain("용사");
  expect(html).toContain("최대 HP");
  expect(html).toContain("공격력");
  expect(html).toContain("관통력");
  expect(html).toContain("12,345");
  expect(html).toContain("143");
  expect(html).not.toContain('role="tooltip"');
  expect(html).not.toContain('aria-describedby=');
  expect(html).not.toContain("높은 동일 옵션 적용");
  expect(html).toContain("20%");
  expect(html).toContain('aria-valuemax="3000"');
  expect(html).toContain('aria-valuenow="600"');
  expect(html).toContain("나무검을 든 한짝 캐릭터");
});

const baseData = { nickname: "용사", level: 3, experience: 3_600, cosmeticsUnlocked: true, contentVersion: "v1", stats: [] };

it("shows the combat power on its plaque under the experience bar", () => {
  const html = renderToStaticMarkup(<CharacterAbilityTab data={baseData} combatPower={118_200} />);
  expect(html).toContain("118,200");
  expect(html.indexOf("LV.3")).toBeLessThan(html.indexOf("전투력"));
  expect(html.indexOf("EXP")).toBeLessThan(html.indexOf("전투력"));
});

/* 랭킹 조회가 아직이어도 명패를 비워 두지 않는다. 서버가 준 값을 먼저 쓰고,
   그마저 없으면 능력치 합으로 센다. 0 이나 빈 자리는 어느 쪽으로도 보이지 않는다. */
it("falls back to the server value, then to the stat total, rather than showing nothing", () => {
  const served = renderToStaticMarkup(<CharacterAbilityTab data={{ ...baseData, combatPower: 9_100 }} combatPower={null} />);
  expect(served).toContain("9,100");

  const summed = renderToStaticMarkup(<CharacterAbilityTab data={{ ...baseData, stats: [
    { statId: "attack", label: "공격력", unit: "POINTS" as const, total: 143, base: 100, additional: 43, calculation: "", sources: [] },
  ] }} />);
  expect(summed).toContain("143");
  expect(summed).not.toContain(">0<");
});
