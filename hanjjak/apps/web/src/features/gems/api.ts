export type GemOption = "FLAT_ATTACK" | "FLAT_HP" | "ATTACK_PERCENT" | "FLAT_PENETRATION" | "CRITICAL_CHANCE" | "HASTE";
export type GemPreset = "MAIN" | "SURVIVAL" | "BERSERK" | "ARMORED";
/** slotId는 가방 칸 번호다. 잠금은 이 칸에 걸리므로, 서버가 내려줄 때만 잠금 단추를 쓸 수 있다. */
export type GemSummary = { gemId: string; level: number; option: GemOption; optionName: string; value: number; locked: boolean; reservedForSale: boolean; equippedPresets: GemPreset[]; slotId?: string };
export type GemState = { unlocked: boolean; tickets: number; secondsUntilNextTicket: number; todayBoss: GemPreset; gemBoxQuantity: number; gems: GemSummary[]; presets: Partial<Record<GemPreset, GemSummary[]>>; lockedPresets: GemPreset[]; contentVersion: string };
export type GemOpenResult = { granted: GemSummary[]; state: GemState };
export type GemPresetUpdateResult = { preset: GemPreset; mainBattleRestarted: boolean; state: GemState };
export type GemFusionMode = "MANUAL" | "SAFE_BATCH";
export type GemFusionSelection = { option: GemOption; quantity: number };
export type GemFusionPreview = { consumedGemIds: string[]; consumption: Array<{ option: GemOption; optionName: string; quantity: number }>; fusionCount: number; inputLevel: number; resultLevel: number };
export type GemFusionResult = { consumedGemIds: string[]; granted: GemSummary[]; state: GemState };
export type GemDungeonChallengeStatus = "ACTIVE" | "SUCCEEDED" | "FAILED" | "ABORTED" | "EXPIRED";
export type GemDungeonEventType = "BATTLE_START" | "SKILL_CAST" | "PLAYER_HIT" | "DOT_HIT" | "BOSS_HIT" | "SURVIVAL_STRIKE" | "VICTORY" | "DEFEAT";
export type GemDungeonCombatEvent = { sequence: number; tick: number; type: GemDungeonEventType; skillId: string | null; amount: number; playerHp: number; bossHp: number; critical: boolean };
export type GemDungeonCombatResult = { success: boolean; failureCode: string | null; elapsedTicks: number; remainingPlayerHp: number; remainingBossHp: number; events: GemDungeonCombatEvent[] };
export type GemDungeonChallenge = { challengeId: string; boss: GemPreset; stage: number; status: GemDungeonChallengeStatus; minimumCompleteAt: string; expiresAt: string; rewardGemBoxes: number; contentVersion: string; battle: GemDungeonCombatResult };
export type GemDungeonToday = { boss: GemPreset; tickets: number; secondsUntilNextTicket: number; progress: { boss: GemPreset; highestClearedStage: number }[]; nextChallengeStage: number | null; sweepStage: number | null; activeChallenge: GemDungeonChallenge | null; testBossSelectionEnabled: boolean };
export type GemDungeonCompletion = { challenge: GemDungeonChallenge; state: GemState };
export type GemDungeonSweepResult = { boss: GemPreset; stage: number; count: number; rewardGemBoxes: number; state: GemState };
type Envelope<T> = { data: T };
type ErrorEnvelope = { code?: string; messageKey?: string };
async function json<T>(response: Response): Promise<T> { const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope; if (!response.ok) throw Object.assign(new Error(body.code || body.messageKey || `HTTP ${response.status}`), body); return body.data; }
function commandHeaders(): HeadersInit { return { Accept: "application/json", "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() }; }
export const gemsApi = {
  lockSlot: (slotId: string, locked: boolean) => fetch(`/api/v1/gems/slots/${encodeURIComponent(slotId)}/lock`, { method: "PUT", credentials: "include", headers: commandHeaders(), body: JSON.stringify({ locked }) }).then(json<GemState>),
  state: () => fetch("/api/v1/gems", { credentials: "include", headers: { Accept: "application/json" } }).then(json<GemState>),
  openBoxes: (quantity: number) => fetch("/api/v1/gems/boxes/open", { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify({ quantity }) }).then(json<GemOpenResult>),
  updatePreset: (preset: GemPreset, gemIds: string[]) => fetch(`/api/v1/gems/presets/${preset}`, { method: "PUT", credentials: "include", headers: commandHeaders(), body: JSON.stringify({ gemIds }) }).then(json<GemPresetUpdateResult>),
  previewFusion: (request: { mode: GemFusionMode; level?: number; gemIds?: string[]; selections?: GemFusionSelection[] }) => fetch("/api/v1/gem-fusions/preview", { method: "POST", credentials: "include", headers: { Accept: "application/json", "Content-Type": "application/json" }, body: JSON.stringify(request) }).then(json<GemFusionPreview>),
  fuse: (mode: GemFusionMode, gemIds: string[], options?: { targetLevel: number; allowedOptions: GemOption[] }) => fetch("/api/v1/gem-fusions", { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify({ mode, gemIds, ...options }) }).then(json<GemFusionResult>),
};

export const gemDungeonsApi = {
  today: (boss?: GemPreset) => fetch(`/api/v1/gem-dungeons/today${boss ? `?boss=${boss}` : ""}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<GemDungeonToday>),
  start: (boss?: GemPreset) => fetch("/api/v1/gem-dungeons/challenges", { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify(boss ? { boss } : {}) }).then(json<GemDungeonChallenge>),
  complete: (challengeId: string) => fetch(`/api/v1/gem-dungeons/challenges/${challengeId}/complete`, { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify({}) }).then(json<GemDungeonCompletion>),
  abort: (challengeId: string) => fetch(`/api/v1/gem-dungeons/challenges/${challengeId}/abort`, { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify({}) }).then(json<GemDungeonCompletion>),
  sweep: (count: number) => fetch("/api/v1/gem-dungeons/sweeps", { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify({ count }) }).then(json<GemDungeonSweepResult>),
};
