export type GameSessionState = {
  gameSessionId: string;
  status: "ACTIVE" | "REPLACED" | "CLOSED" | "EXPIRED";
  heartbeatIntervalSeconds: number;
  expiresAfterSeconds: number;
};

type SessionEnvelope = { data: GameSessionState };
type ErrorEnvelope = { code?: string };
type SessionLostHandler = () => void;
type SessionStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

const SESSION_STORAGE_KEY = "hanjjak.game-session";

class GameSessionRequestError extends Error {
  constructor(readonly status: number, code: string) {
    super(code);
  }
}

export class GameSessionClient {
  private session: GameSessionState | null = null;
  private heartbeat: number | undefined;
  private opening: { accountId: string; generation: number; promise: Promise<GameSessionState> } | null = null;
  private onSessionLost: SessionLostHandler | null = null;
  private generation = 0;

  constructor(private readonly storage: SessionStorage | null = browserSessionStorage()) {}

  setSessionLostHandler(handler: SessionLostHandler | null): void {
    this.onSessionLost = handler;
  }

  async open(accountId: string): Promise<GameSessionState> {
    if (this.session) return this.session;
    const generation = this.generation;
    if (this.opening?.accountId === accountId && this.opening.generation === generation) return this.opening.promise;
    const stored = this.load(accountId);
    const promise = this.resumeOrOpen(stored, generation)
      .then((session) => {
        if (generation !== this.generation) throw new Error("GAME_SESSION_OPEN_CANCELLED");
        this.session = session;
        this.store(accountId, session);
        this.startHeartbeat(session.heartbeatIntervalSeconds);
        return session;
      })
      .finally(() => {
        if (this.opening?.promise === promise) this.opening = null;
      });
    this.opening = { accountId, generation, promise };
    return promise;
  }

  suspend(): void {
    this.generation += 1;
    this.stopHeartbeat();
    this.session = null;
  }

  invalidate(): void {
    this.suspend();
    this.clearStored();
    this.onSessionLost?.();
  }

  async close(): Promise<void> {
    const current = this.session;
    this.invalidate();
    if (current) await this.post(`/api/v1/game-sessions/${current.gameSessionId}/close`, true);
  }

  private startHeartbeat(intervalSeconds: number): void {
    this.stopHeartbeat();
    this.heartbeat = globalThis.setInterval(() => void this.sendHeartbeat(), intervalSeconds * 1000) as unknown as number;
  }

  private stopHeartbeat(): void {
    clearInterval(this.heartbeat);
    this.heartbeat = undefined;
  }
  private async sendHeartbeat(): Promise<void> {
    const current = this.session;
    const generation = this.generation;
    if (!current) return;
    try {
      const session = await this.post(`/api/v1/game-sessions/${current.gameSessionId}/heartbeat`);
      if (generation === this.generation && this.session?.gameSessionId === current.gameSessionId) this.session = session;
    } catch (error) {
      if (generation !== this.generation || this.session?.gameSessionId !== current.gameSessionId) return;
      if (error instanceof GameSessionRequestError && error.status === 409 && error.message === "GAME_SESSION_NOT_ACTIVE") this.invalidate();
      else {
        this.suspend();
        this.onSessionLost?.();
      }
    }
  }

  private async resumeOrOpen(stored: GameSessionState | null, generation: number): Promise<GameSessionState> {
    if (!stored) return this.post("/api/v1/game-sessions");
    try {
      return await this.post(`/api/v1/game-sessions/${stored.gameSessionId}/heartbeat`);
    } catch (error) {
      if (!(error instanceof GameSessionRequestError) || error.status !== 409 || error.message !== "GAME_SESSION_NOT_ACTIVE") throw error;
      if (generation !== this.generation) throw new Error("GAME_SESSION_OPEN_CANCELLED");
      this.clearStored();
      return this.post("/api/v1/game-sessions");
    }
  }

  private load(accountId: string): GameSessionState | null {
    try {
      const parsed = JSON.parse(this.storage?.getItem(SESSION_STORAGE_KEY) ?? "null") as { accountId?: string; session?: GameSessionState } | null;
      if (parsed?.accountId === accountId && parsed.session?.status === "ACTIVE") return parsed.session;
      this.storage?.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // Corrupt or unavailable browser state is disposable. The server remains authoritative.
    }
    return null;
  }

  private store(accountId: string, session: GameSessionState): void {
    try {
      this.storage?.setItem(SESSION_STORAGE_KEY, JSON.stringify({ accountId, session }));
    } catch {
      // Session persistence is best effort; the in-memory heartbeat remains authoritative.
    }
  }

  private clearStored(): void {
    try {
      this.storage?.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // The server still rejects stale sessions even if browser storage is unavailable.
    }
  }

  private async post(path: string, keepalive = false): Promise<GameSessionState> {
    const response = await fetch(path, {
      method: "POST",
      credentials: "include",
      headers: { "Accept": "application/json", "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
      body: "{}",
      keepalive,
    });
    const body = await response.json().catch(() => ({})) as SessionEnvelope & ErrorEnvelope;
    if (!response.ok) throw new GameSessionRequestError(response.status, body.code || `HTTP ${response.status}`);
    return body.data;
  }
}

function browserSessionStorage(): SessionStorage | null {
  try {
    return typeof window === "undefined" ? null : window.sessionStorage;
  } catch {
    return null;
  }
}

export const gameSessionClient = new GameSessionClient();
export type OfflineRewardPending = {
  jobId: string;
  status: "CLAIMABLE";
  stageId: string;
  startedAt: string;
  lastHeartbeatAt: string;
  accrualEndedAt: string | null;
  eligibleSeconds: number;
  offlineSeconds: number;
  experienceGained: number;
  riceGained: number;
  rewards: { itemId: string; quantity: number }[];
};

export async function fetchOfflineReward(): Promise<OfflineRewardPending | null> {
  const response = await fetch("/api/v1/offline-rewards/pending", { credentials: "include", headers: { Accept: "application/json" } });
  const body = await response.json().catch(() => ({})) as { data?: (Omit<OfflineRewardPending, "offlineSeconds"> & { offlineSeconds?: number }) | null; code?: string };
  if (!response.ok) throw new Error(body.code || `HTTP ${response.status}`);
  if (!body.data) return null;
  return { ...body.data, offlineSeconds: body.data.offlineSeconds ?? body.data.eligibleSeconds };
}

export async function claimOfflineReward(): Promise<unknown> {
  const response = await fetch("/api/v1/offline-rewards/claim", {
    method: "POST",
    credentials: "include",
    headers: { Accept: "application/json", "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
    body: "{}",
  });
  const body = await response.json().catch(() => ({})) as { data?: unknown; code?: string };
  if (!response.ok) throw new Error(body.code || `HTTP ${response.status}`);
  return body.data;
}
