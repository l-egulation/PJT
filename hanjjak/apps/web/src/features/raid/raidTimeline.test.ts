import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { expect, it } from "vitest";
import { scheduleRaidTimeline, type RaidCombatEvent } from "./raidTimeline";

type GoldenRaid = { tickDurationMilliseconds: number; events: RaidCombatEvent[] };

const golden = JSON.parse(readFileSync(resolve(import.meta.dirname, "../../../../../packages/sim-core/src/test/resources/golden/raid-combat-v1.json"), "utf8")) as GoldenRaid;

it("schedules the Kotlin-authored raid timeline without recalculating outcomes", () => {
  const scheduled = scheduleRaidTimeline(golden.events, golden.tickDurationMilliseconds);

  expect(scheduled).toHaveLength(golden.events.length);
  expect(scheduled[0]).toMatchObject({ sequence: 1, logicalTick: 0, type: "BATTLE_STARTED", atMilliseconds: 0 });
  expect(scheduled.find(event => event.type === "ESCALATION")).toMatchObject({ sequence: 20, logicalTick: 50, escalationStage: 1, atMilliseconds: 5_000 });
  expect(scheduled.at(-1)).toMatchObject({ sequence: 24, logicalTick: 60, type: "TIME_LIMIT_REACHED", atMilliseconds: 6_000 });
});

it("rejects unordered server events instead of inventing playback order", () => {
  expect(() => scheduleRaidTimeline([{ sequence: 2, logicalTick: 1, type: "PLAYER_HIT" }, { sequence: 1, logicalTick: 2, type: "BOSS_HIT" }], 100)).toThrow("INVALID_RAID_EVENT_SEQUENCE");
  expect(() => scheduleRaidTimeline([{ sequence: 1, logicalTick: 2, type: "PLAYER_HIT" }, { sequence: 2, logicalTick: 1, type: "BOSS_HIT" }], 100)).toThrow("INVALID_RAID_EVENT_TICK");
});
