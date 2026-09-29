import type { BattleEnemySettlement, BattleRenderingCheckpoint, BattleSessionResult, BattleSessionStarted, CombatRenderingEvent } from "./sessionApi";
import { battleSessionApi } from "./sessionApi";

export type AutoBattleHandlers = {
  gameSessionId: () => string | null;
  stageId: () => string;
  onStarted: (session: BattleSessionStarted) => void;
  onProgress: (checkpoint: BattleRenderingCheckpoint) => void;
  onEvents?: (events: CombatRenderingEvent[]) => void;
  onSettled?: (settlements: BattleEnemySettlement[]) => void;
  onCompleted: (result: BattleSessionResult, retryAt: number | null) => void;
  onStopped: () => void;
  onError: (error: Error) => void;
};

const BATTLE_HEARTBEAT_INTERVAL_MS = 30_000;
const MIN_RENDER_INTERVAL_MS = 50;
/* 스테이지를 열자마자 달리는 모습만 한참 보인다는 말을 들었다. 붙는 데 드는 시간을 줄인다. */
export const ENCOUNTER_APPROACH_DURATION_MS = 300;
export const REGULAR_BOSS_INTRO_DURATION_MS = 2_800;
export const FINAL_BOSS_INTRO_DURATION_MS = 4_200;
export const BATTLE_RENDER_TIME_SCALE = 1.5;
export const DIRECT_ATTACK_IMPACT_DELAY_MS = 320;
const BATTLE_COMPLETION_RETRY_MS = 250;
const MAX_BATTLE_COMPLETION_RETRIES = 20;
const MAX_BATTLE_SETTLEMENT_RETRIES = 20;
const BATTLE_FAILURE_RETRY_DELAY_MS = 1_000;

export function bossIntroDurationMs(stageId: string): number {
  return stageId.endsWith("-10") ? FINAL_BOSS_INTRO_DURATION_MS : REGULAR_BOSS_INTRO_DURATION_MS;
}

export class AutoBattleClient {
  private session: BattleSessionStarted | null = null;
  private heartbeatTimer: number | undefined;
  private eventTimers: number[] = [];
  private transitionTimer: number | undefined;
  private retryStageId: string | null = null;
  private settlementQueue: Promise<void> = Promise.resolve();
  private lastCheckpoint: BattleRenderingCheckpoint | null = null;
  private settlementKeys = new Map<number, `${string}-${string}-${string}-${string}-${string}`>();
  private deliveredSettlementIds = new Set<string>();
  private stopped = true;

  constructor(private readonly handlers: AutoBattleHandlers) {}

  get active(): BattleSessionStarted | null {
    return this.session;
  }
  get currentStageId(): string | null {
    return this.session?.stageId ?? this.retryStageId;
  }

  async start(): Promise<void> {
    if (!this.stopped) return;
    this.stopped = false;
    await this.startNext();
  }

  async stop(keepalive = false): Promise<void> {
    if (this.stopped && !this.session && !this.retryStageId) return;
    this.stopped = true;
    this.clearTimers();
    const session = this.session;
    const checkpoint = this.lastCheckpoint;
    const gameSessionId = this.handlers.gameSessionId();
    this.session = null;
    this.retryStageId = null;
    this.handlers.onStopped();
    if (session && gameSessionId) {
      await this.settlementQueue.catch(() => undefined);
      await battleSessionApi.abort(session, gameSessionId, checkpoint, keepalive).catch(() => undefined);
    }
  }

  private async startNext(stageId = this.handlers.stageId()): Promise<void> {
    if (this.stopped) return;
    const gameSessionId = this.handlers.gameSessionId();
    if (!gameSessionId) {
      this.fail(new Error("GAME_SESSION_NOT_ACTIVE"));
      return;
    }
    try {
      const session = await battleSessionApi.start(stageId, gameSessionId);
      if (this.stopped) {
        await battleSessionApi.abort(session, gameSessionId).catch(() => undefined);
        return;
      }
      this.session = session;
      this.retryStageId = null;
      this.lastCheckpoint = { logicalTick: session.logicalTickBasis, defeatedNormals: 0, playerHp: session.input.player.maxHp };
      this.settlementQueue = Promise.resolve();
      this.settlementKeys.clear();
      this.deliveredSettlementIds.clear();
      this.handlers.onStarted(session);
      const renderingDuration = this.scheduleProgress(session);
      this.heartbeatTimer = globalThis.setInterval(() => void this.heartbeat(session), BATTLE_HEARTBEAT_INTERVAL_MS) as unknown as number;
      this.transitionTimer = globalThis.setTimeout(
        () => void this.complete(session),
        Math.max(MIN_RENDER_INTERVAL_MS, session.durationMilliseconds, renderingDuration + MIN_RENDER_INTERVAL_MS),
      ) as unknown as number;
    } catch (error) {
      this.fail(error instanceof Error ? error : new Error("BATTLE_SESSION_START_FAILED"));
    }
  }

  private scheduleProgress(session: BattleSessionStarted): number {
    let defeatedNormals = 0;
    let playerHp = session.input.player.maxHp;
    let renderingOffset = 0;
    let renderingDuration = 0;
    let normalSpawnCount = 0;
    let lastNormalDefeatAt = 0;
    let encounterReadyAt = 0;
    const groups = new Map<number, CombatRenderingEvent[]>();
    for (const event of session.renderingTimeline ?? []) {
      const isNormalSpawn = event.type === "ENEMY_SPAWNED";
      const isBossSpawn = event.type === "BOSS_SPAWNED";
      const baseAt = event.logicalTick * session.tickDurationMilliseconds * BATTLE_RENDER_TIME_SCALE + renderingOffset + eventRenderingDelay(event);
      const replacementReadyAt = (isNormalSpawn && normalSpawnCount > 0) || isBossSpawn ? lastNormalDefeatAt : 0;
      const at = Math.max(0, encounterReadyAt, baseAt, replacementReadyAt);
      groups.set(at, [...(groups.get(at) ?? []), event]);
      renderingDuration = Math.max(renderingDuration, at);
      if (event.type === "ENEMY_DEFEATED" && !event.boss) lastNormalDefeatAt = at;
      if (isNormalSpawn) encounterReadyAt = at;
      if (isNormalSpawn && normalSpawnCount++ === 0) renderingOffset += ENCOUNTER_APPROACH_DURATION_MS;
      if (isBossSpawn) renderingOffset += bossIntroDurationMs(session.stageId);
    }
    for (const [at, events] of groups) {
      const timer = globalThis.setTimeout(() => {
        if (this.session?.battleSessionId !== session.battleSessionId) return;
        for (const event of events) {
          if (event.type === "ENEMY_DEFEATED" && !event.boss) defeatedNormals++;
          if ((event.type === "PLAYER_HIT" || event.type === "PLAYER_DEFEATED") && event.hpAfter !== null) playerHp = event.hpAfter;
        }
        const checkpoint = { logicalTick: events.at(-1)?.logicalTick ?? 0, defeatedNormals, playerHp };
        this.lastCheckpoint = checkpoint;
        this.handlers.onEvents?.(events);
        this.handlers.onProgress(checkpoint);
        if (events.some((event) => event.type === "ENEMY_DEFEATED" && !event.boss)) {
          this.queueSettlement(session, checkpoint);
        }
      }, at) as unknown as number;
      this.eventTimers.push(timer);
    }
    return renderingDuration;
  }

  private queueSettlement(session: BattleSessionStarted, checkpoint: BattleRenderingCheckpoint): void {
    const key = this.settlementKeys.get(checkpoint.defeatedNormals) ?? crypto.randomUUID();
    this.settlementKeys.set(checkpoint.defeatedNormals, key);
    this.settlementQueue = this.settlementQueue.then(() => this.settle(session, checkpoint, key));
    void this.settlementQueue.catch((error) => this.fail(error instanceof Error ? error : new Error("BATTLE_SETTLEMENT_FAILED")));
  }

  private async settle(session: BattleSessionStarted, checkpoint: BattleRenderingCheckpoint, key: `${string}-${string}-${string}-${string}-${string}`, retries = 0): Promise<void> {
    const gameSessionId = this.handlers.gameSessionId();
    if (!gameSessionId || this.session?.battleSessionId !== session.battleSessionId) return;
    try {
      const result = await battleSessionApi.settle(session, gameSessionId, checkpoint.defeatedNormals, checkpoint, key);
      if (this.session?.battleSessionId !== session.battleSessionId) return;
      this.deliverSettlements(result.settlements);
    } catch (error) {
      const failure = error instanceof Error ? error : new Error("BATTLE_SETTLEMENT_FAILED");
      if (failure.message === "BATTLE_SETTLEMENT_NOT_READY" && retries < MAX_BATTLE_SETTLEMENT_RETRIES) {
        await new Promise<void>((resolve) => globalThis.setTimeout(resolve, BATTLE_COMPLETION_RETRY_MS));
        return this.settle(session, checkpoint, key, retries + 1);
      }
      throw failure;
    }
  }

  private deliverSettlements(settlements: BattleEnemySettlement[]): void {
    const unseen = settlements.filter((settlement) => !this.deliveredSettlementIds.has(settlement.settlementId));
    if (unseen.length === 0) return;
    unseen.forEach((settlement) => this.deliveredSettlementIds.add(settlement.settlementId));
    this.handlers.onSettled?.(unseen);
  }

  private async heartbeat(session: BattleSessionStarted): Promise<void> {
    const gameSessionId = this.handlers.gameSessionId();
    if (!gameSessionId || this.session?.battleSessionId !== session.battleSessionId) return;
    try {
      await battleSessionApi.heartbeat(session, gameSessionId);
    } catch (error) {
      this.fail(error instanceof Error ? error : new Error("BATTLE_SESSION_HEARTBEAT_FAILED"));
    }
  }

  private async complete(session: BattleSessionStarted, notReadyRetries = 0): Promise<void> {
    const gameSessionId = this.handlers.gameSessionId();
    if (!gameSessionId || this.session?.battleSessionId !== session.battleSessionId) return;
    try {
      await this.settlementQueue;
      this.clearTimers();
      const checkpoint = this.lastCheckpoint ?? {
        logicalTick: session.logicalTickBasis,
        defeatedNormals: 0,
        playerHp: session.input.player.maxHp,
      };
      const result = await battleSessionApi.complete(session, gameSessionId, null, checkpoint);
      if (this.session?.battleSessionId !== session.battleSessionId) return;
      this.session = null;
      const retryAt = result.battle.success ? null : Date.now() + BATTLE_FAILURE_RETRY_DELAY_MS;
      this.deliverSettlements(result.settlements);
      this.handlers.onCompleted(result, retryAt);
      if (this.stopped) return;
      if (result.battle.success) {
        await this.startNext();
      } else {
        this.retryStageId = result.stageId;
        this.transitionTimer = globalThis.setTimeout(() => void this.startNext(result.stageId), BATTLE_FAILURE_RETRY_DELAY_MS) as unknown as number;
      }
    } catch (error) {
      if (this.stopped || this.session?.battleSessionId !== session.battleSessionId) return;
      const failure = error instanceof Error ? error : new Error("BATTLE_SESSION_COMPLETE_FAILED");
      if (failure.message === "BATTLE_SESSION_NOT_READY" && notReadyRetries < MAX_BATTLE_COMPLETION_RETRIES) {
        this.transitionTimer = globalThis.setTimeout(() => void this.complete(session, notReadyRetries + 1), BATTLE_COMPLETION_RETRY_MS) as unknown as number;
        return;
      }
      this.fail(failure);
    }
  }

  private fail(error: Error): void {
    this.stopped = true;
    this.clearTimers();
    this.session = null;
    this.retryStageId = null;
    this.lastCheckpoint = null;
    this.settlementKeys.clear();
    this.deliveredSettlementIds.clear();
    this.handlers.onError(error);
  }

  private clearTimers(): void {
    clearInterval(this.heartbeatTimer);
    for (const timer of this.eventTimers) clearTimeout(timer);
    clearTimeout(this.transitionTimer);
    this.heartbeatTimer = undefined;
    this.eventTimers = [];
    this.transitionTimer = undefined;
  }
}

function eventRenderingDelay(event: CombatRenderingEvent): number {
  if (event.type === "PLAYER_ATTACK_IMPACT" || event.type === "PLAYER_SKILL_IMPACT") return DIRECT_ATTACK_IMPACT_DELAY_MS;
  if (event.type === "ENEMY_DEFEATED") return 380;
  if (event.type === "ENEMY_ATTACK_STARTED") return 420;
  if (event.type === "PLAYER_HIT") return 480;
  if (event.type === "PLAYER_DEFEATED" || event.type === "BATTLE_FAILED" || event.type === "BATTLE_CYCLE_COMPLETED") return 520;
  return 0;
}
