import { useMemo } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useBattleRuntimeStore } from "../battle/runtimeStore";
import { rewardMeta } from "../battle/BattleHud";
import riceIcon from "../equipment/assets-cozy-pixel/currency-rice-gold-256.png";
import sproutIcon from "../character/assets-cozy-pixel/sprout-icon-32.png";
import { claimOfflineReward, fetchOfflineReward, type OfflineRewardPending } from "./gameSessionClient";
import { NoticeToast } from "../../shared/NoticeToast";
import "./OfflineRewardPanel.css";

const GREETINGS = ["안녕하세요!", "어서오세요!", "또 오셨군요!", "기다렸어요!", "왜 이제 오셨어요!", "가지마세요ㅠ"] as const;

export function pickGreeting(roll: number = Math.random()): string {
  const index = Math.floor(roll * GREETINGS.length);
  return GREETINGS[Math.min(GREETINGS.length - 1, Math.max(0, index))];
}

type RewardLine = { key: string; label: string; quantity: number; icon?: string };

export function offlineRewardLines(reward: OfflineRewardPending): RewardLine[] {
  const lines: RewardLine[] = [];
  if (reward.experienceGained > 0) lines.push({ key: "experience", label: "경험치", quantity: reward.experienceGained, icon: sproutIcon });
  if (reward.riceGained > 0) lines.push({ key: "rice", label: "쌀", quantity: reward.riceGained, icon: riceIcon });
  for (const item of reward.rewards) {
    const meta = rewardMeta(item.itemId);
    lines.push({ key: item.itemId, label: meta.label, quantity: item.quantity, icon: meta.icon });
  }
  return lines;
}

export function OfflineRewardPanel() {
  const queryClient = useQueryClient();
  const gameSessionId = useBattleRuntimeStore((state) => state.gameSessionId);
  const pending = useQuery({ queryKey: ["offline-reward", gameSessionId], queryFn: fetchOfflineReward, retry: false, enabled: gameSessionId !== null });
  const claim = useMutation({
    mutationFn: claimOfflineReward,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["offline-reward"] });
      void queryClient.invalidateQueries({ queryKey: ["auth", "session"] });
      void queryClient.invalidateQueries({ queryKey: ["inventory"] });
    },
  });
  const reward = pending.data;
  // 한 번 고른 인사는 이 보상이 남아 있는 동안 그대로 둔다. 렌더마다 다시 뽑으면 글자가 깜빡인다.
  const greeting = useMemo(() => pickGreeting(), [reward?.jobId]);
  const lines = useMemo(() => (reward ? offlineRewardLines(reward) : []), [reward]);
  if (pending.isLoading || pending.isError || !reward) return null;
  return <section className="offline-reward-panel" role="dialog" aria-labelledby="offline-reward-title">
    <h2 id="offline-reward-title">{greeting}</h2>
    <p className="offline-reward-panel__time">오프라인 시간 {formatDuration(reward.offlineSeconds)} · 보상 적용 {formatDuration(reward.eligibleSeconds)}</p>
    {lines.length > 0 && <ul className="offline-reward-panel__list">
      {lines.map((line) => <li key={line.key}>
        <i aria-hidden="true">{line.icon && <img src={line.icon} alt="" />}</i>
        <span>{line.label}</span>
        <strong>{line.quantity.toLocaleString()}</strong>
      </li>)}
    </ul>}
    {claim.error && <NoticeToast message="보상을 받지 못했습니다. 다시 시도해 주세요." tone="failure" onDone={() => claim.reset()} />}
    <button type="button" onClick={() => claim.mutate()} disabled={claim.isPending}>{claim.isPending ? "받는 중..." : "보상 받기"}</button>
  </section>;
}

export function formatDuration(seconds: number): string {
  const wholeSeconds = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(wholeSeconds / 3600);
  const minutes = Math.floor((wholeSeconds % 3600) / 60);
  const remainingSeconds = wholeSeconds % 60;
  if (hours > 0) return `${hours}시간 ${minutes}분${remainingSeconds > 0 ? ` ${remainingSeconds}초` : ""}`;
  if (minutes > 0) return `${minutes}분${remainingSeconds > 0 ? ` ${remainingSeconds}초` : ""}`;
  return `${remainingSeconds}초`;
}
