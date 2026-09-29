export type MaterialType = "POTATO" | "SWEET_POTATO" | "CORN";
export type DropRates = Record<MaterialType, number>;
export type MaterialOption = { materialType: MaterialType; displayName: string; dropRates: DropRates };
export type MaterialPreferenceState = { selected: boolean; primaryMaterialType: MaterialType | null; options: MaterialOption[]; populationCounts: Partial<Record<MaterialType, number>> };
export type MaterialPreferenceSelection = { selected: true; primaryMaterialType: MaterialType; dropRates: DropRates };

type Envelope<T> = { data: T; stateVersion: number };
type ErrorDetail = { field?: string; code: string; messageKey: string; value?: unknown };
type ErrorEnvelope = { code?: string; messageKey?: string; details?: ErrorDetail[] | null };

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope;
  if (!response.ok) throw Object.assign(new Error(body.code || `HTTP ${response.status}`), body);
  return body.data;
}

export const materialPreferenceApi = {
  get: () => fetch("/api/v1/material-preference", { credentials: "include" }).then(json<MaterialPreferenceState>),
  select: (materialType: MaterialType) => fetch("/api/v1/material-preference", {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
    body: JSON.stringify({ materialType }),
  }).then(json<MaterialPreferenceSelection>),
  population: () => fetch("/api/v1/material-preference/population", { headers: { Accept: "application/json" } }).then(json<Partial<Record<MaterialType, number>>>)
};
