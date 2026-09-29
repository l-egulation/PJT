import type { CSSProperties } from "react";
import type { CombatRenderingEvent } from "../battle/sessionApi";
import type { GemDungeonCombatEvent } from "./api";
export type SkillVfxKind = "basic-target" | "heavy-target" | "dot-target" | "haste-caster" | "basic-amp-caster";
export type BasicStrikeDirection = "up-right" | "up-left";

export type SkillVfx = {
  kind: SkillVfxKind;
  frame: number;
  frameCount: number;
  loop?: boolean;
  expiring?: boolean;
  impact?: boolean;
  direction?: BasicStrikeDirection;
};

export type BattleSkillVfx = {
  caster: SkillVfx[];
  target: SkillVfx[];
};

export const SKILL_VFX_BASE_URL = "/assets/effects/skills-v1";

const skillVfxAssets: Partial<Record<SkillVfxKind, string>> = {
  "heavy-target": "active-heavy-target-8f.png?v=20260911",
  "dot-target": "active-dot-target-8f.png",
  "haste-caster": "haste-caster-8f.png",
  "basic-amp-caster": "active-basic-amp-caster-8f.png",
};

const developedIntroFrames = [0, 2, 5, 7] as const;
export const HEAVY_VFX_DURATION_TICKS = 12;
export const BASIC_VFX_DURATION_TICKS = 3;

function developedIntroFrame(fourFrameIndex: number): number {
  return developedIntroFrames[Math.max(0, Math.min(3, fourFrameIndex))] ?? 0;
}

function latestMatching(events: GemDungeonCombatEvent[], predicate: (event: GemDungeonCombatEvent) => boolean) {
  return events.findLast(predicate);
}

function activeCast(events: GemDungeonCombatEvent[], playedTicks: number, skillId: string, durationTicks: number) {
  const event = latestMatching(events, (candidate) => candidate.type === "SKILL_CAST" && candidate.skillId === skillId);
  if (!event) return undefined;
  const age = playedTicks - event.tick;
  return age >= 0 && age < durationTicks ? { event, age } : undefined;
}

/** Derives presentation-only VFX from the authoritative combat event replay. */
export function skillVfxForEvents(events: GemDungeonCombatEvent[], playedTicks: number): BattleSkillVfx {
  const caster: SkillVfx[] = [];
  const target: SkillVfx[] = [];

  const heavy = latestMatching(events, (event) => event.type === "PLAYER_HIT" && event.skillId === "active_heavy");
  if (heavy) {
    const age = playedTicks - heavy.tick;
    if (age >= 0 && age < HEAVY_VFX_DURATION_TICKS) {
      target.push({ kind: "heavy-target", frame: developedIntroFrame(age), frameCount: 8 });
    }
  }

  const dotCast = latestMatching(events, (event) => event.type === "SKILL_CAST" && event.skillId === "active_dot");
  if (dotCast) {
    const dotAge = playedTicks - dotCast.tick;
    const lastHit = latestMatching(events, (event) => event.type === "DOT_HIT" && event.skillId === "active_dot" && event.tick >= dotCast.tick);
    const hitAge = lastHit ? playedTicks - lastHit.tick : Number.POSITIVE_INFINITY;
    if ((dotAge >= 0 && dotAge < 50) || (hitAge >= 0 && hitAge <= 2)) {
      target.push({ kind: "dot-target", frame: Math.max(0, dotAge) % 8, frameCount: 8, loop: true, impact: hitAge >= 0 && hitAge <= 2 });
    }
  }

  const haste = activeCast(events, playedTicks, "active_haste", 50);
  if (haste) caster.push({ kind: "haste-caster", frame: haste.age % 8, frameCount: 8, loop: true, expiring: haste.age >= 20 });

  const amplification = activeCast(events, playedTicks, "active_basic_amp", 50);
  if (amplification) caster.push({ kind: "basic-amp-caster", frame: amplification.age % 8, frameCount: 8, loop: true, expiring: amplification.age >= 20 });

  return { caster, target };
}

function mainEventAsDungeonEvent(event: CombatRenderingEvent, type: GemDungeonCombatEvent["type"]): GemDungeonCombatEvent {
  return {
    sequence: Number(event.eventId.replace(/\D/g, "")) || 0,
    tick: event.logicalTick,
    type,
    skillId: event.skillId,
    amount: event.damage ?? 0,
    playerHp: event.target === "PLAYER" ? event.hpAfter ?? event.hpBefore ?? 0 : 0,
    bossHp: event.target === "ENEMY" ? event.hpAfter ?? event.hpBefore ?? 0 : 0,
    critical: event.critical,
  };
}

/** Adapts main-battle rendering events to the shared skill presentation timeline. */
export function skillVfxForMainBattle(events: CombatRenderingEvent[], playedTicks: number, enemyIndex?: number | null): BattleSkillVfx {
  const basicImpacts = events.filter((event, index) =>
    event.type === "PLAYER_ATTACK_IMPACT"
    && events.findIndex((candidate) => candidate.eventId === event.eventId) === index,
  );
  const latestBasicImpactIndex = basicImpacts.findLastIndex((event) => enemyIndex == null || event.enemyIndex === enemyIndex);
  const latestDotCast = events.findLast((event) => event.type === "PLAYER_SKILL_CAST_STARTED" && event.skillId === "active_dot");
  const adapted = events.flatMap((event) => {
    if (event.type === "PLAYER_SKILL_IMPACT" && event.skillId === "active_heavy" && (enemyIndex == null || event.enemyIndex === enemyIndex)) return [mainEventAsDungeonEvent(event, "PLAYER_HIT")];
    if (event === latestDotCast) return [mainEventAsDungeonEvent(event, "SKILL_CAST")];
    if (event.type === "BUFF_STARTED" && (event.skillId === "active_haste" || event.skillId === "active_basic_amp")) return [mainEventAsDungeonEvent(event, "SKILL_CAST")];
    if (event.type === "DOT_TICK" && event.skillId === "active_dot" && (enemyIndex == null || event.enemyIndex === enemyIndex)) return [mainEventAsDungeonEvent(event, "DOT_HIT")];
    return [];
  });
  const result = skillVfxForEvents(adapted, playedTicks);
  const latestBasicImpact = basicImpacts[latestBasicImpactIndex];
  if (latestBasicImpact) {
    const age = playedTicks - latestBasicImpact.logicalTick;
    if (age >= 0 && age < BASIC_VFX_DURATION_TICKS) {
      result.target.unshift({
        kind: "basic-target",
        frame: 0,
        frameCount: 1,
        direction: latestBasicImpactIndex % 2 === 0 ? "up-right" : "up-left",
      });
    }
  }
  return result;
}

export function skillVfxSpriteStyle(effect: SkillVfx): CSSProperties {
  const asset = skillVfxAssets[effect.kind];
  if (!asset) return {};
  const frame = Math.max(0, Math.min(effect.frameCount - 1, effect.frame));
  return {
    backgroundImage: `url(${SKILL_VFX_BASE_URL}/${asset})`,
    backgroundSize: `${effect.frameCount * 100}% 100%`,
    backgroundPosition: effect.frameCount === 1 ? "center" : `${frame / (effect.frameCount - 1) * 100}% center`,
  };
}

export function SkillVfxSprite({ effect }: { effect: SkillVfx }) {
  const animationClass = effect.frameCount === 8 ? " skill-vfx-animated-8" : "";
  const loopClass = effect.loop ? " skill-vfx-looping-8" : "";
  const expiryClass = effect.expiring ? " skill-vfx-expiring" : "";
  const impactClass = effect.impact ? " skill-vfx-impact" : "";
  const directionClass = effect.direction ? ` skill-vfx-${effect.direction}` : "";
  return <span aria-hidden="true" className={`skill-vfx skill-vfx-${effect.kind}${animationClass}${loopClass}${expiryClass}${impactClass}${directionClass}`} style={skillVfxSpriteStyle(effect)} />;
}
