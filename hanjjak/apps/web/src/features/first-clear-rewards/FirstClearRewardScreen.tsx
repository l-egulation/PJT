import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FirstClearRewardApiError, firstClearRewardsApi, isUncertainFirstClearRewardError, type FirstClearRewardResult } from "./api";
import { formatStageId } from "../battle/stageLabel";
import { NoticeDialog } from "../../shared/NoticeDialog";
import "./FirstClearRewardScreen.css";

function itemList(items: FirstClearRewardResult["pendingItems"]) {
  return items.length ? items.map((item) => `${item.displayName} × ${item.quantity}`).join(", ") : "없음";
}

export type FirstClearRewardDataSource = Pick<typeof firstClearRewardsApi, "list" | "claim">;
type FirstClearRewardScreenProps = { dataSource?: FirstClearRewardDataSource };

type RewardPage = { rewards: FirstClearRewardResult[]; count: number };

export function FirstClearRewardScreen({ dataSource = firstClearRewardsApi }: FirstClearRewardScreenProps = {}) {
  const queryClient = useQueryClient();
  const [capacityFailure, setCapacityFailure] = useState<{ requiredSlots: number; availableSlots: number; missingSlots: number } | null>(null);
  const [claimFailure, setClaimFailure] = useState<string | null>(null);
  const claimKeys = useRef(new Map<string, `${string}-${string}-${string}-${string}-${string}`>());
  const rewards = useQuery({ queryKey: ["first-clear-rewards"], queryFn: dataSource.list, retry: false });
  const claim = useMutation({
    mutationFn: (reward: FirstClearRewardResult) => {
      const key = claimKeys.current.get(reward.rewardId) ?? crypto.randomUUID();
      claimKeys.current.set(reward.rewardId, key);
      return dataSource.claim(reward.rewardId, key);
    },
    retry: false,
    onSuccess: async (_claimed, reward) => {
      setCapacityFailure(null);
      setClaimFailure(null);
      claimKeys.current.delete(reward.rewardId);
      queryClient.setQueryData<RewardPage>(["first-clear-rewards"], (current) => current && {
        rewards: current.rewards.filter((entry) => entry.rewardId !== reward.rewardId),
        count: Math.max(0, current.count - 1),
      });
      await Promise.all([
        ["first-clear-rewards"],
        ["inventory"],
        ["equipment"],
        ["skills"],
        ["auth", "session"],
      ].map((queryKey) => queryClient.invalidateQueries({ queryKey })));
    },
    onError: (error, reward) => {
      if (!isUncertainFirstClearRewardError(error)) claimKeys.current.delete(reward.rewardId);
      if (error instanceof FirstClearRewardApiError && error.code === "INVENTORY_CAPACITY_EXCEEDED") {
        setClaimFailure(null);
        const requiredSlots = error.number("requiredSlots") ?? reward.requiredSlots;
        const availableSlots = error.number("availableSlots") ?? reward.availableSlots;
        const missingSlots = error.number("missingSlots") ?? reward.missingSlots;
        queryClient.setQueryData<RewardPage>(["first-clear-rewards"], (current) => current && {
          ...current,
          rewards: current.rewards.map((entry) => entry.rewardId === reward.rewardId
            ? { ...entry, requiredSlots, availableSlots, missingSlots }
            : entry),
        });
        setCapacityFailure({ requiredSlots, availableSlots, missingSlots });
        return;
      }
      setCapacityFailure(null);
      setClaimFailure(error instanceof Error ? error.message : "FIRST_CLEAR_REWARD_CLAIM_FAILED");
      if (error instanceof FirstClearRewardApiError && ["FIRST_CLEAR_REWARD_ALREADY_CLAIMED", "FIRST_CLEAR_REWARD_NOT_FOUND"].includes(error.code)) {
        void rewards.refetch();
      }
    },
  });

  if (rewards.isLoading) return <section className="first-clear-reward-screen" aria-busy="true"><p>첫 클리어 보상을 불러오는 중입니다.</p></section>;
  if (rewards.error) return <section className="first-clear-reward-screen"><p role="alert">첫 클리어 보상을 불러오지 못했습니다. <button type="button" onClick={() => rewards.refetch()}>다시 시도</button></p></section>;

  const entries = rewards.data?.rewards ?? [];
  return <section className="first-clear-reward-screen" aria-labelledby="first-clear-rewards-title">
    {capacityFailure && <NoticeDialog
      title="공간 부족"
      message={`인벤토리 공간이 부족합니다. 필요 슬롯 ${capacityFailure.requiredSlots}개 · 현재 여유 ${capacityFailure.availableSlots}칸 · 부족한 슬롯 ${capacityFailure.missingSlots}칸.`}
      detail="공간을 확보한 후 다시 시도해 주세요."
      actions={<button type="button" className="notice-dialog-primary" autoFocus onClick={() => setCapacityFailure(null)}>닫기</button>}
    />}
    {claimFailure && <NoticeDialog
      message={`보상을 수령하지 못했습니다. ${claimFailure} 확인 후 다시 시도해 주세요.`}
      actions={<button type="button" className="notice-dialog-primary" autoFocus onClick={() => setClaimFailure(null)}>닫기</button>}
    />}
    {entries.length === 0 ? <p className="first-clear-reward-empty">받을 첫 클리어 보상이 없습니다.</p> : <ol>
      {entries.map((reward) => <li key={reward.rewardId}>
        <header><strong>{formatStageId(reward.stageId)}</strong><span>보상 버전 {reward.rewardVersion}</span></header>
        <dl>
          <div><dt>즉시 지급</dt><dd>쌀 {reward.riceGranted.toLocaleString()} · {itemList(reward.grantedItems)}</dd></div>
          <div><dt>보관함 지급</dt><dd>{itemList(reward.pendingItems)}</dd></div>
          <div><dt>인벤토리</dt><dd>필요 슬롯 {reward.requiredSlots} · 현재 여유 {reward.availableSlots} · 부족 {reward.missingSlots}</dd></div>
          {reward.unlockedSkillId && <div><dt>스킬 해금</dt><dd>{reward.unlockedSkillId}</dd></div>}
        </dl>
        <button type="button" disabled={claim.isPending} onClick={() => claim.mutate(reward)}>{claim.isPending ? "수령 중" : "전부 수령"}</button>
      </li>)}
    </ol>}
  </section>;
}
