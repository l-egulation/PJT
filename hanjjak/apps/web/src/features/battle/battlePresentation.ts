import type { BattleCycleResult, RewardLine } from "./api";

export type ExperienceProgress = {
  current: number;
  required: number;
  percent: number;
};
export type BattleRetryPresentation = {
  secondsRemaining: number;
  failureReason: string;
};
export type BattleFailureAnalysis = {
  title: string;
  reason: string;
  recommendation: string;
};
export function battleStageProgressLabel(bossOnly: boolean | undefined, defeatedNormals: number): string | null {
  if (bossOnly === undefined) return null;
  return bossOnly ? "보스" : `${defeatedNormals} / 20`;
}

export function battleRetryProgressLabel(bossOnly: boolean | undefined): string | null {
  if (bossOnly === undefined) return null;
  return bossOnly ? "보스 재도전" : "일반 몬스터 0/20 재시작";
}

export function battleFailureAnalysis(result: BattleCycleResult, bossOnly: boolean): BattleFailureAnalysis {
  if (result.failureCode === "TIME_LIMIT") return {
    title: "보스 제한시간 초과",
    reason: "보스에게 준 피해 속도가 제한시간을 넘겼습니다.",
    recommendation: "무기 공격력·방어 관통·공격 스킬을 먼저 강화해 보세요.",
  };
  if (result.failureCode === "PLAYER_DIED" && !bossOnly && result.defeatedNormals < 20) return {
    title: "일반 구간에서 전투 불능",
    reason: `${result.defeatedNormals + 1}번째 몬스터를 상대하던 중 HP가 0이 됐습니다.`,
    recommendation: "갑옷·투구로 최대 HP를 높이거나 공격력을 올려 피격 횟수를 줄여 보세요.",
  };
  if (result.failureCode === "PLAYER_DIED") return {
    title: "보스전에서 전투 불능",
    reason: "보스의 공격을 버티지 못해 HP가 0이 됐습니다.",
    recommendation: "갑옷·투구를 강화하고 보스 패턴 전에 전투를 끝낼 공격력도 함께 점검해 보세요.",
  };
  return {
    title: "전투를 완료하지 못함",
    reason: "이번 전투가 정상적으로 완료되지 않았습니다.",
    recommendation: "장비와 스킬 구성을 점검한 뒤 다시 도전해 보세요.",
  };
}

export function battleRetryPresentation(failureCode: string | null | undefined, retryAt: number | null): BattleRetryPresentation | null {
  if (!failureCode || retryAt === null) return null;
  const failureReason = failureCode === "PLAYER_DIED" ? "플레이어 사망" : failureCode === "TIME_LIMIT" ? "보스 제한시간 초과" : "전투 실패";
  return { failureReason, secondsRemaining: 1 };
}


export function experienceProgress(level: number, totalExperience: number): ExperienceProgress {
  if (level >= 500) return { current: 0, required: 0, percent: 100 };
  const required = Math.max(1, level * 1_000);
  const levelStart = 500 * level * (level - 1);
  const current = Math.max(0, Math.min(required, totalExperience - levelStart));
  return { current, required, percent: Math.round(current / required * 1_000) / 10 };
}

export function mergeRewardLines(current: RewardLine[], incoming: RewardLine[]): RewardLine[] {
  const merged = new Map(current.map((line) => [line.itemId, { ...line }]));
  for (const line of incoming) {
    const previous = merged.get(line.itemId);
    if (!previous) {
      merged.set(line.itemId, { ...line });
      continue;
    }
    merged.set(line.itemId, {
      itemId: line.itemId,
      requestedQuantity: previous.requestedQuantity + line.requestedQuantity,
      grantedQuantity: previous.grantedQuantity + line.grantedQuantity,
      discardedQuantity: previous.discardedQuantity + line.discardedQuantity,
      skippedQuantity: (previous.skippedQuantity ?? 0) + (line.skippedQuantity ?? 0),
    });
  }
  return [...merged.values()];
}

export function checkpointComplete(defeatedNormals: number, checkpoint: number): boolean {
  return defeatedNormals >= checkpoint;
}
