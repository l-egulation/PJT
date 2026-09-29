import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import type { GemDungeonChallenge, GemDungeonCombatEvent, GemSummary } from "./api";
import { BattlePlayback, combatAnimationKeys, GemsScreen, wholeEventSecond } from "./GemsScreen";
import { selectSafeBatch } from "./GemManagement";

describe("BattlePlayback", () => {
  it("shows combat event time as a whole second without decimals", () => {
    expect(wholeEventSecond(121)).toBe(12);
    expect(wholeEventSecond(9)).toBe(0);
  });

  it.each(["player", "boss"] as const)("keeps the %s fighter and damage keys unique on a hit frame", (side) => {
    const keys = Object.values(combatAnimationKeys(side, true, 2));
    expect(new Set(keys).size).toBe(keys.length);
  });

  it("renders every active skill effect as an independent caster or target overlay", () => {
    const now = 1_000_000;
    const combatEvent = (sequence: number, type: GemDungeonCombatEvent["type"], skillId: string | null): GemDungeonCombatEvent => ({
      sequence, tick: 0, type, skillId, amount: 1, playerHp: 100, bossHp: 100, critical: false,
    });
    const challenge: GemDungeonChallenge = {
      challengeId: "vfx-preview",
      boss: "BERSERK",
      stage: 1,
      status: "ACTIVE",
      minimumCompleteAt: new Date(now + 10_000).toISOString(),
      expiresAt: new Date(now + 20_000).toISOString(),
      rewardGemBoxes: 1,
      contentVersion: "v1",
      battle: {
        success: false,
        failureCode: "TIME_LIMIT",
        elapsedTicks: 100,
        remainingPlayerHp: 100,
        remainingBossHp: 100,
        events: [
          combatEvent(1, "SKILL_CAST", "active_dot"),
          combatEvent(2, "SKILL_CAST", "active_haste"),
          combatEvent(3, "SKILL_CAST", "active_basic_amp"),
          combatEvent(4, "PLAYER_HIT", "active_heavy"),
        ],
      },
    };
    const markup = renderToStaticMarkup(<BattlePlayback challenge={challenge} now={now} busy={false} onComplete={() => undefined} onRetry={() => undefined} onAbort={() => undefined} />);

    expect(markup).toContain("skill-vfx-heavy-target");
    expect(markup).toContain("skill-vfx-dot-target");
    expect(markup).toContain("skill-vfx-haste-caster");
    expect(markup).toContain("skill-vfx-basic-amp-caster");
    expect(markup).toContain("hero-crown-vfx");
    expect(markup).not.toContain("skill-vfx-dot-eye");
  });

  it("clears duration VFX after a completed replay while retaining the final combat frame", () => {
    const now = 1_000_000;
    const challenge: GemDungeonChallenge = {
      challengeId: "settled-vfx",
      boss: "BERSERK",
      stage: 1,
      status: "ACTIVE",
      minimumCompleteAt: new Date(now - 10_000).toISOString(),
      expiresAt: new Date(now + 5_000).toISOString(),
      rewardGemBoxes: 1,
      contentVersion: "v1",
      battle: {
        success: false,
        failureCode: "TIME_LIMIT",
        elapsedTicks: 150,
        remainingPlayerHp: 100,
        remainingBossHp: 100,
        events: [
          { sequence: 1, tick: 113, type: "SKILL_CAST", skillId: "active_basic_amp", amount: 0, playerHp: 100, bossHp: 100, critical: false },
          { sequence: 2, tick: 150, type: "DEFEAT", skillId: null, amount: 0, playerHp: 100, bossHp: 100, critical: false },
        ],
      },
    };

    const markup = renderToStaticMarkup(<BattlePlayback challenge={challenge} now={now} busy={false} onComplete={() => undefined} onRetry={() => undefined} onAbort={() => undefined} />);

    expect(markup).not.toContain("skill-vfx-basic-amp-caster");
    expect(markup).toContain("aria-label=\"보스 체력\"");
  });

  it("fills every dungeon skill slot from the player's loadout", () => {
    const now = 1_000_000;
    const event = (sequence: number, skillId: string): GemDungeonCombatEvent => ({ sequence, tick: 0, type: "SKILL_CAST", skillId, amount: 0, playerHp: 100, bossHp: 100, critical: false });
    const challenge: GemDungeonChallenge = {
      challengeId: "confirmed-skill-names", boss: "BERSERK", stage: 1, status: "ACTIVE",
      minimumCompleteAt: new Date(now + 10_000).toISOString(), expiresAt: new Date(now + 20_000).toISOString(), rewardGemBoxes: 1, contentVersion: "v1",
      battle: { success: false, failureCode: "TIME_LIMIT", elapsedTicks: 100, remainingPlayerHp: 100, remainingBossHp: 100, events: [
        event(1, "active_heavy"), event(2, "active_dot"), event(3, "active_haste"), event(4, "active_basic_amp"),
      ] },
    };
    const loadout = ["active_heavy", "active_dot", "active_haste", "active_basic_amp"];
    const markup = renderToStaticMarkup(<BattlePlayback challenge={challenge} now={now} busy={false} loadout={loadout} onComplete={() => undefined} onRetry={() => undefined} onAbort={() => undefined} />);

    expect(markup).toContain("한짝의 일격");
    expect(markup).toContain("마! 쫄이나");
    expect(markup).toContain("잘게 더 잘게!");
    expect(markup).toContain("화력 최대로!");
    expect(markup.match(/--skill-shell/g)).toHaveLength(4);
    expect(markup.match(/dungeon-skill empty/g)).toBeNull();
  });
});

it("separates gem management from the dungeon content route", () => {
  const client = new QueryClient();
  client.setQueryData(["gems"], { unlocked: true, tickets: 1, secondsUntilNextTicket: 600, todayBoss: "SURVIVAL", gemBoxQuantity: 2, gems: [], presets: {}, lockedPresets: [], contentVersion: "v1" });
  client.setQueryData(["gem-dungeons", "today", undefined], { boss: "SURVIVAL", tickets: 1, secondsUntilNextTicket: 600, progress: [], nextChallengeStage: 1, sweepStage: null, activeChallenge: null, testBossSelectionEnabled: false });
  const render = (mode: "manage" | "dungeon") => renderToStaticMarkup(<QueryClientProvider client={client}><GemsScreen mode={mode} /></QueryClientProvider>);

  expect(render("manage")).toContain("현재 적용 효과");
  expect(render("manage")).not.toContain("레이드 준비 중");
  expect(render("dungeon")).toContain("도전 시작");
  expect(render("dungeon")).toContain("레이드 준비 중");
  expect(render("dungeon")).toContain("KST 매시 정각 교체");
  expect(render("dungeon")).not.toContain("메인 프리셋");
  client.clear();
});

it("renders the full gem management preset shell", () => {
  const client = new QueryClient();
  client.setQueryData(["gems"], { unlocked: true, tickets: 1, secondsUntilNextTicket: 600, todayBoss: "SURVIVAL", gemBoxQuantity: 2, gems: [], presets: {}, lockedPresets: ["SURVIVAL"], contentVersion: "v1" });
  const markup = renderToStaticMarkup(<QueryClientProvider client={client}><GemsScreen mode="manage" /></QueryClientProvider>);

  expect(markup).toContain("프리셋 1");
  expect(markup).toContain("프리셋 2");
  expect(markup).toContain("프리셋 3");
  expect(markup).toContain("프리셋 4 열기");
  expect(markup).not.toContain("프리셋 5 열기");
  expect(markup).toContain("현재 적용 효과");
  expect(markup).toContain("보유 보석");
  expect(markup).toContain("프리셋 저장");
  expect(markup).toContain("합성");
  expect(markup).not.toContain("안전 합성");
  client.clear();
});

it("selects only the requested safe batch quantities", () => {
  const gem = (gemId: string, option: GemSummary["option"], locked = false, equippedPresets: GemSummary["equippedPresets"] = [], reservedForSale = false): GemSummary => ({ gemId, level: 2, option, optionName: option, value: 1, locked, reservedForSale, equippedPresets });
  const gems = [gem("attack-2", "FLAT_ATTACK"), gem("attack-1", "FLAT_ATTACK"), gem("locked", "FLAT_ATTACK", true), gem("equipped", "FLAT_HP", false, ["MAIN"]), gem("sale", "FLAT_HP", false, [], true), gem("hp-1", "FLAT_HP")];

  expect(selectSafeBatch(gems, 2, { FLAT_ATTACK: 2, FLAT_HP: 1 })).toEqual(["attack-1", "attack-2", "hp-1"]);
});
