export type RaidCombatEvent = {
  sequence: number;
  logicalTick: number;
  type: string;
  skillId?: string | null;
  actor?: "PLAYER" | "BOSS" | null;
  target?: "PLAYER" | "BOSS" | null;
  damage?: number | null;
  critical?: boolean | null;
  hpBefore?: number | null;
  hpAfter?: number | null;
  escalationStage?: number | null;
  bossAttack?: number | null;
  bossDefense?: number | null;
};

export type ScheduledRaidEvent = RaidCombatEvent & { atMilliseconds: number };

export function scheduleRaidTimeline(events: RaidCombatEvent[], tickDurationMilliseconds: number): ScheduledRaidEvent[] {
  if (!Number.isInteger(tickDurationMilliseconds) || tickDurationMilliseconds <= 0) throw new Error("INVALID_TICK_DURATION");
  let previousSequence = 0;
  let previousTick = -1;
  return events.map(event => {
    if (!Number.isInteger(event.sequence) || event.sequence <= previousSequence) throw new Error("INVALID_RAID_EVENT_SEQUENCE");
    if (!Number.isInteger(event.logicalTick) || event.logicalTick < previousTick) throw new Error("INVALID_RAID_EVENT_TICK");
    previousSequence = event.sequence;
    previousTick = event.logicalTick;
    return { ...event, atMilliseconds: event.logicalTick * tickDurationMilliseconds };
  });
}
