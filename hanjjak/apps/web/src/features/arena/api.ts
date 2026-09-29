export type ArenaOpponent = { accountId: string; nickname: string; rating: number; level: number };
export type ArenaEvent = { eventId: string; logicalTick: number; type: string; actorId: string | null; targetId: string | null; skillId: string | null; damage: number | null; critical: boolean; hpBefore: number | null; hpAfter: number | null };
export type ArenaBattle = {
  battleId: string;
  opponent: ArenaOpponent;
  result: { winnerId: string | null; loserId: string | null; draw: boolean; elapsedTicks: number; attackerRemainingHp: number; defenderRemainingHp: number };
  events: ArenaEvent[];
  hpMultiplier: number;
};

type Envelope<T> = { data: T };

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & { code?: string };
  if (!response.ok) throw new Error(body.code || `HTTP ${response.status}`);
  return body.data;
}

export const arenaApi = {
  opponents: () => fetch("/api/v1/arena/opponents", { credentials: "include", headers: { Accept: "application/json" } }).then(json<ArenaOpponent[]>),
  battle: (opponentId: string, idempotencyKey = crypto.randomUUID()) => fetch("/api/v1/arena/battles", {
    method: "POST",
    credentials: "include",
    headers: { Accept: "application/json", "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ opponentId }),
  }).then(json<ArenaBattle>),
};
