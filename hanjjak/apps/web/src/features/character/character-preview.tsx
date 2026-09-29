import React, { createRef } from "react";
import { createRoot } from "react-dom/client";
import { CharacterAbilityTab } from "./CharacterAbilityTab";
import { CharacterWindowHeader } from "./CharacterWindowChrome";
import type { CharacterStat, CharacterStats } from "./api";
import "../../styles.css";
import "./character.css";

function stat(statId: string, label: string, total: number, level: number, equipment: number): CharacterStat {
  return { statId, label, unit: "FLAT", total, base: level, additional: equipment, calculation: `${level} + ${equipment}`, sources: [
    { sourceId: "level", label: "레벨", category: "BASE", value: level, unit: "FLAT", applied: true, reason: null },
    { sourceId: "equipment", label: "장비", category: "EQUIPMENT", value: equipment, unit: "FLAT", applied: true, reason: null },
  ] };
}

const data: CharacterStats = { nickname: "한짝", level: 24, experience: 294720, combatPower: 15_820, cosmeticsUnlocked: false, contentVersion: "preview", stats: [
  stat("maxHp", "최대 HP", 3680, 3041, 639), stat("attack", "공격력", 151, 151, 0), stat("penetration", "명중", 45, 45, 0),
  stat("criticalChance", "치명타 확률", 5, 5, 0), stat("basicAttackDamage", "치명타 피해", 200, 200, 0), stat("allDamage", "피해량 증가", 0, 0, 0),
  stat("attackSpeed", "공격속도", 1, 1, 0), stat("buffDuration", "버프 지속시간", 0, 0, 0),
] };

document.body.style.minHeight = "100vh";
document.body.style.margin = "0";
document.body.style.background = "radial-gradient(circle at 50% 35%, #7a5c3f, #2b1a10 72%)";
document.body.style.display = "grid";
document.body.style.placeItems = "center";
const closeButton = createRef<HTMLButtonElement>();

createRoot(document.getElementById("root")!).render(<dialog open className="character-window" aria-labelledby="character-title" style={{ position: "fixed", inset: "50% auto auto 50%", margin: 0, transform: "translate(-50%, -50%)" }}>
  <CharacterWindowHeader activeTab="stats" closeButton={closeButton} data={data} onClose={() => undefined} onTabChange={() => undefined} />
  <div className="character-body" role="tabpanel" id="character-panel-stats" aria-labelledby="character-tab-stats"><CharacterAbilityTab data={data} /></div>
</dialog>);
