import riceIcon from "../equipment/assets-cozy-pixel/currency-rice-gold-256.png";
import skillbookIcon from "../../shared/assets/cozy-hud-v1/icons/reward-skillbook.png";
import gemIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-gems.png";
import marketIcon from "./market-window-icon.png";
import potatoFragment from "../equipment/assets/material-ranks/potato-fragment-f.png";
import potatoMini from "../equipment/assets/material-ranks/potato-mini-d.png";
import potato from "../equipment/assets/material-ranks/potato-c.png";
import potatoGolden from "../equipment/assets/material-ranks/potato-golden-b.png";
import potatoLegendary from "../equipment/assets/material-ranks/potato-legendary-a.png";
import sweetFragment from "../equipment/assets/material-ranks/sweet-potato-fragment-f.png";
import sweetMini from "../equipment/assets/material-ranks/sweet-potato-mini-d.png";
import sweetPotato from "../equipment/assets/material-ranks/sweet-potato-c.png";
import sweetGolden from "../equipment/assets/material-ranks/sweet-potato-golden-b.png";
import sweetLegendary from "../equipment/assets/material-ranks/sweet-potato-legendary-a.png";
import cornKernel from "../equipment/assets/material-ranks/corn-kernel-f.png";
import cornMini from "../equipment/assets/material-ranks/corn-mini-d.png";
import corn from "../equipment/assets/material-ranks/corn-c.png";
import cornGolden from "../equipment/assets/material-ranks/corn-golden-b.png";
import cornLegendary from "../equipment/assets/material-ranks/corn-legendary-a.png";
import type { MarketInstrument } from "./api";

export { riceIcon, skillbookIcon, gemIcon, marketIcon };

export type MarketFamily = "POTATO" | "SWEET_POTATO" | "CORN";
export const MATERIAL_GRADES = ["F", "D", "C", "B", "A"];
export const MATERIAL_FAMILIES: Array<{ id: MarketFamily; label: string }> = [
  { id: "POTATO", label: "감자" }, { id: "SWEET_POTATO", label: "고구마" }, { id: "CORN", label: "옥수수" },
];
export const BOOK_GRADE_LABELS: Record<string, string> = { normal: "노말", rare: "희귀", epic: "영웅", legendary: "전설" };
const MATERIAL_ART: Record<MarketFamily, string[]> = {
  POTATO: [potatoFragment, potatoMini, potato, potatoGolden, potatoLegendary],
  SWEET_POTATO: [sweetFragment, sweetMini, sweetPotato, sweetGolden, sweetLegendary],
  CORN: [cornKernel, cornMini, corn, cornGolden, cornLegendary],
};

export function itemArt(itemId: string): string {
  const material = itemId.match(/^(POTATO|SWEET_POTATO|CORN)_M([1-5])$/);
  if (material) return MATERIAL_ART[material[1] as MarketFamily][Number(material[2]) - 1];
  if (itemId.startsWith("skillbook:")) return skillbookIcon;
  if (itemId.startsWith("gem:")) return gemIcon;
  return marketIcon;
}

export function instrumentArt(instrument: MarketInstrument): string {
  const art = itemArt(instrument.itemId);
  if (art !== marketIcon) return art;
  return instrument.category === "SKILL_BOOK" ? skillbookIcon : instrument.category === "GEM" ? gemIcon : marketIcon;
}

export function instrumentFamily(instrument: MarketInstrument): MarketFamily | null {
  return MATERIAL_FAMILIES.find(family => instrument.itemId.startsWith(`${family.id}_M`))?.id ?? null;
}

/** The short label that separates variants of the same item, e.g. "B등급" or "Lv.3 · 공격력 +12". */
export function instrumentVariant(instrument: MarketInstrument, gemValueText: (gem: { option: string; value: number }) => string): string {
  const material = instrument.itemId.match(/_M([1-5])$/);
  if (material) return `${MATERIAL_GRADES[Number(material[1]) - 1]}등급`;
  if (instrument.category === "SKILL_BOOK") return BOOK_GRADE_LABELS[instrument.attributes.grade] ?? instrument.displayName;
  const option = instrument.attributes.option === "attack_speed" ? "HASTE" : instrument.attributes.option?.toUpperCase();
  const value = Number(instrument.attributes.value);
  if (option && instrument.attributes.value != null && Number.isFinite(value)) return `Lv.${instrument.attributes.level} · ${gemValueText({ option, value })}`;
  return instrument.displayName;
}

export function instrumentSummary(instrument: MarketInstrument): string {
  const material = instrument.itemId.match(/_M([1-5])$/);
  if (material) return `강화에 사용하는 ${MATERIAL_GRADES[Number(material[1]) - 1]}등급 재료예요.`;
  if (instrument.category === "SKILL_BOOK") return "스킬을 배우고 올리는 비법서예요.";
  if (instrument.category === "GEM") return "장비에 끼워 능력을 올리는 보석이에요.";
  return "거래소에서 사고팔 수 있는 물품이에요.";
}

/*
 * 이름에서 등급·레벨과 분류를 뗀다. 그 둘은 이름표와 분류 탭이 따로 말해 주므로
 * 이름까지 다시 적으면 "1레벨 고정 공격력 보석 / Lv.1 +4"처럼 두 번 읽게 된다.
 *   스킬북  영웅 마! 쫄이나 비법서 → 마! 쫄이나
 *   보석    1레벨 고정 공격력 보석 → 고정 공격력
 *   재료    감자 한 조각          → 그대로
 */
export function instrumentShortName(instrument: MarketInstrument): string {
  if (instrument.category === "SKILL_BOOK") {
    const grade = BOOK_GRADE_LABELS[instrument.attributes.grade];
    const withoutGrade = grade && instrument.displayName.startsWith(`${grade} `)
      ? instrument.displayName.slice(grade.length + 1)
      : instrument.displayName;
    return withoutGrade.replace(/ 비법서$/, "") || withoutGrade;
  }
  if (instrument.category === "GEM") {
    const bare = instrument.displayName.replace(/^\d+레벨 /, "").replace(/ 보석$/, "");
    return bare || instrument.displayName;
  }
  return instrument.displayName;
}

/*
 * 이름표 색. 인벤토리 화면이 쓰는 등급 색과 같은 팔레트를 따른다. 같은 F등급이
 * 가방에서는 회색, 거래소에서는 다른 색이면 같은 물건으로 읽히지 않는다.
 * 보석은 등급 대신 레벨이 그 자리를 대신하므로 1~7을 그대로 색 단계로 쓴다.
 */
export function instrumentTagTone(instrument: MarketInstrument): string {
  const material = instrument.itemId.match(/_M([1-5])$/);
  if (material) return `is-grade-${MATERIAL_GRADES[Number(material[1]) - 1].toLowerCase()}`;
  if (instrument.category === "SKILL_BOOK") return `is-book-${instrument.attributes.grade ?? "normal"}`;
  if (instrument.category === "GEM") {
    const level = Number(instrument.attributes.level);
    return Number.isInteger(level) && level >= 1 && level <= 7 ? `is-gem-${level}` : "";
  }
  return "";
}
