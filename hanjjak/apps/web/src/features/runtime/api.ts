export type ServerRuntimeSnapshot = {
  accountId: string;
  stateVersion: number;
  contentVersion: string;
  currentStageId: string;
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  repeatStageId: string | null;
  maxHp: number;
};

type Envelope<T> = { data: T };
type ErrorEnvelope = { code?: string };

export async function fetchRuntimeState(): Promise<ServerRuntimeSnapshot> {
  const response = await fetch("/api/v1/runtime-state", {
    credentials: "include",
    headers: { Accept: "application/json" },
  });
  const body = await response.json().catch(() => ({})) as Envelope<ServerRuntimeSnapshot> & ErrorEnvelope;
  if (!response.ok) throw new Error(body.code || `HTTP ${response.status}`);
  return body.data;
}
