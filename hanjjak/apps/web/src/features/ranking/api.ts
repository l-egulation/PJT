export type MaterialType = "POTATO" | "SWEET_POTATO" | "CORN";
export type RankingAppearance = {
  head: string | null;
  top: string | null;
  bottom: string | null;
  gloves: string | null;
  shoes: string | null;
  cape: string | null;
};
export type RankingEntry = {
  rank: number;
  overallRank: number;
  nickname: string;
  level: number;
  materialType: MaterialType;
  displayName: string;
  combatPower: number;
  appearance: RankingAppearance;
  updatedAt: string;
};
export type MaterialRanking = { materialType: MaterialType; displayName: string; entries: RankingEntry[] };
export type CombatPowerRanking = {
  formulaVersion: string;
  generatedAt: string;
  sourceStateVersion: number;
  overallTop: RankingEntry[];
  specializations: MaterialRanking[];
  myEntry: RankingEntry | null;
};

type Envelope<T> = { data: T };

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & { code?: string; messageKey?: string };
  if (!response.ok) throw Object.assign(new Error(body.code || body.messageKey || `HTTP ${response.status}`), body);
  return body.data;
}

export const rankingApi = {
  combatPower: (limit = 20) => fetch(`/api/v1/rankings/combat-power?limit=${limit}`, {
    credentials: "include",
    headers: { Accept: "application/json" },
  }).then(json<CombatPowerRanking>),
};
