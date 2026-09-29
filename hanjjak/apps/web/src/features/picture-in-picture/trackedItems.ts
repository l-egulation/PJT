import potatoF from "../equipment/assets/material-ranks/potato-fragment-f.png";
import potatoD from "../equipment/assets/material-ranks/potato-mini-d.png";
import potatoC from "../equipment/assets/material-ranks/potato-c.png";
import potatoB from "../equipment/assets/material-ranks/potato-golden-b.png";
import potatoA from "../equipment/assets/material-ranks/potato-legendary-a.png";
import sweetPotatoF from "../equipment/assets/material-ranks/sweet-potato-fragment-f.png";
import sweetPotatoD from "../equipment/assets/material-ranks/sweet-potato-mini-d.png";
import sweetPotatoC from "../equipment/assets/material-ranks/sweet-potato-c.png";
import sweetPotatoB from "../equipment/assets/material-ranks/sweet-potato-golden-b.png";
import sweetPotatoA from "../equipment/assets/material-ranks/sweet-potato-legendary-a.png";
import cornF from "../equipment/assets/material-ranks/corn-kernel-f.png";
import cornD from "../equipment/assets/material-ranks/corn-mini-d.png";
import cornC from "../equipment/assets/material-ranks/corn-c.png";
import cornB from "../equipment/assets/material-ranks/corn-golden-b.png";
import cornA from "../equipment/assets/material-ranks/corn-legendary-a.png";
import activeHeavy from "../skills/assets-cozy-pixel/skill-active-heavy.png";
import activeDot from "../skills/assets-cozy-pixel/skill-active-dot.png";
import activeHaste from "../skills/assets-cozy-pixel/skill-active-haste.png";
import activeBasicAmp from "../skills/assets-cozy-pixel/skill-active-basic-amp.png";
import passiveCritical from "../skills/assets-cozy-pixel/skill-passive-critical.png";
import passiveAllDamage from "../skills/assets-cozy-pixel/skill-passive-all-damage.png";

export type TrackedItem = { itemId: string; group: "강화 재료" | "스킬북"; grade: string; name: string; icon: string };

const materialFamilies = [
  ["POTATO", ["감자 한 조각", "미니 감자", "감자", "황금 감자", "전설 감자"], [potatoF, potatoD, potatoC, potatoB, potatoA]],
  ["SWEET_POTATO", ["고구마 한 조각", "미니 고구마", "고구마", "황금 고구마", "전설 고구마"], [sweetPotatoF, sweetPotatoD, sweetPotatoC, sweetPotatoB, sweetPotatoA]],
  ["CORN", ["옥수수 한 알", "미니 옥수수", "옥수수", "황금 옥수수", "전설 옥수수"], [cornF, cornD, cornC, cornB, cornA]],
] as const;
const materialGrades = ["F", "D", "C", "B", "A"] as const;

const skillFamilies = [
  ["active_heavy", "한짝의 일격 비법서", activeHeavy],
  ["active_dot", "마! 쫄이나 비법서", activeDot],
  ["active_haste", "잘게 더 잘게! 비법서", activeHaste],
  ["active_basic_amp", "화력 최대로! 비법서", activeBasicAmp],
  ["passive_critical", "회심의 간 비법서", passiveCritical],
  ["passive_all_damage", "오늘의 특선 비법서", passiveAllDamage],
] as const;
const skillGrades = [["normal", "노말"], ["rare", "희귀"], ["epic", "영웅"], ["legendary", "전설"]] as const;

export const PIP_TRACKED_ITEMS: TrackedItem[] = [
  ...materialFamilies.flatMap(([family, names, icons]) => names.map((name, index) => ({
    itemId: `${family}_M${index + 1}`,
    group: "강화 재료" as const,
    grade: materialGrades[index],
    name,
    icon: icons[index],
  }))),
  ...skillFamilies.flatMap(([skillId, name, icon]) => skillGrades.map(([gradeId, grade]) => ({
    itemId: `skillbook:${skillId}:${gradeId}`,
    group: "스킬북" as const,
    grade,
    name,
    icon,
  }))),
];

export const DEFAULT_PIP_TRACKED_ITEM_IDS = [
  "POTATO_M1",
  "POTATO_M4",
  "skillbook:active_heavy:normal",
  "skillbook:active_dot:rare",
];
