export type SkillActionKind = "UNLOCK" | "ENHANCE" | "PROMOTE" | "LOCKED" | "COMPLETE";
export type SkillBookRequirement = { itemId: string; displayName: string; requiredQuantity: number; availableQuantity: number };
export type SkillActionSummary = { kind: SkillActionKind; targetGrade: string | null; targetLevel: number | null; books: SkillBookRequirement[]; riceCost: number; successBasisPoints: number; executable: boolean; disabledReason: string | null };
export type SkillSummary = { skillId: string; name: string; active: boolean; unlocked: boolean; grade: string | null; gradeName: string | null; level: number; equippedSlot: number | null; effectText: string; action: SkillActionSummary };
export type SkillState = { skills: SkillSummary[]; activeLoadout: string[]; riceBalance: number };
export type SkillEnhanceResult = { skill: SkillSummary; success: boolean; state: SkillState };
export type SkillCommand = { skillId: string; kind: "enhance" | "promote"; key: string };
type Envelope<T> = { data: T };
type ErrorEnvelope = { code?: string; messageKey?: string };
export class SkillApiError extends Error { constructor(public status: number, public code: string) { super(code); } }
async function json<T>(response: Response): Promise<T> { const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope; if (!response.ok) throw new SkillApiError(response.status, body.code || body.messageKey || `HTTP_${response.status}`); return body.data; }
function commandHeaders(key: string): HeadersInit { return { Accept: "application/json", "Content-Type": "application/json", "Idempotency-Key": key }; }
export const skillsApi = {
  state: () => fetch("/api/v1/skills", { credentials: "include", headers: { Accept: "application/json" } }).then(json<SkillState>),
  command: (command: SkillCommand) => fetch(`/api/v1/skills/${encodeURIComponent(command.skillId)}/${command.kind}`, { method: "POST", credentials: "include", headers: commandHeaders(command.key), body: "{}" }).then(json<SkillEnhanceResult>),
  enhance: (skillId: string, key: string) => skillsApi.command({ skillId, kind: "enhance", key }),
  promote: (skillId: string, key: string) => skillsApi.command({ skillId, kind: "promote", key }),
  updateLoadout: (skillIds: string[], key: string) => fetch("/api/v1/skills/loadout", { method: "PUT", credentials: "include", headers: commandHeaders(key), body: JSON.stringify({ skillIds }) }).then(json<SkillState>),
};
export function isUncertainSkillCommandError(error: unknown): boolean { return !(error instanceof SkillApiError) || error.status >= 500; }
