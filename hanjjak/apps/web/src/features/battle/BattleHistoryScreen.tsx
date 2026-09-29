import { useQuery } from "@tanstack/react-query";
import { battleApi, type BattleHistoryEvent, type BattleHistoryEventType } from "./api";
import { battleStageProgressLabel } from "./battlePresentation";
import { formatStageId } from "./stageLabel";
import "./BattleHistoryScreen.css";

const typeLabels: Record<BattleHistoryEventType, string> = {
  STAGE_ENTERED: "스테이지 입장",
  STAGE_CLEARED: "승리",
  STAGE_FAILED: "패배",
  DUNGEON_ENTERED: "던전 입장",
  RETURNED: "전투 복귀",
};

const resultLabels: Record<string, string> = {
  STAGE_ENTERED: "스테이지 입장",
  STAGE_CLEARED: "승리",
  PLAYER_DEFEATED: "캐릭터 사망",
  PLAYER_DEFEATED_RESTARTED: "캐릭터 사망",
  BOSS_TIME_LIMIT_EXCEEDED: "시간 제한 초과",
  BOSS_TIME_LIMIT_EXCEEDED_RESTARTED: "시간 제한 초과",
  PLAYER_DIED: "캐릭터 사망",
  TIME_LIMIT: "시간 제한 초과",
  DUNGEON_ENTERED: "던전 도전을 시작했습니다.",
  DUNGEON_CLEARED: "던전 도전을 완료하고 복귀했습니다.",
  DUNGEON_ABORTED: "던전 도전을 중단하고 복귀했습니다.",
  SURVIVAL_FAILED: "생존형 보스 도전에 실패하고 복귀했습니다.",
  BOSS_NOT_DEFEATED: "보스를 제한시간 안에 처치하지 못해 복귀했습니다.",
};

export function battleHistoryResultLabel(event: BattleHistoryEvent): string {
  return resultLabels[event.resultCode] ?? `결과 코드: ${event.resultCode}`;
}

export function battleHistoryTargetLabel(event: BattleHistoryEvent): string {
  if (event.stageId) return formatStageId(event.stageId);
  if (event.dungeonId) {
    const [, boss = "dungeon", stage = ""] = event.dungeonId.split(".");
    const bossLabel = { survival: "생존형", berserk: "폭주형", armored: "장갑형" }[boss] ?? boss;
    return `${bossLabel} 보석 던전 ${stage}단계`;
  }
  return "전투";
}

export function battleHistoryDuration(elapsedTicks: number): string {
  const totalTenths = Math.max(0, Math.trunc(elapsedTicks));
  const minutes = Math.floor(totalTenths / 600);
  const seconds = Math.floor((totalTenths % 600) / 10);
  const tenths = totalTenths % 10;
  return minutes > 0 ? `${minutes}분 ${seconds}.${tenths}초` : `${seconds}.${tenths}초`;
}

export function battleHistoryResultEvents(events: BattleHistoryEvent[] | undefined): BattleHistoryEvent[] {
  return (events ?? []).filter((event) => event.type === "STAGE_CLEARED" || event.type === "STAGE_FAILED");
}

export function battleHistoryStageEnteredAt(event: BattleHistoryEvent): string | null {
  if (event.combatSnapshot?.stageEnteredAt) return event.combatSnapshot.stageEnteredAt;
  if (event.combatSnapshot?.elapsedTicks === undefined) return null;
  return new Date(new Date(event.occurredAt).getTime() - event.combatSnapshot.elapsedTicks * 100).toISOString();
}

export function battleHistoryDateTime(value: string): string {
  return new Intl.DateTimeFormat("ko-KR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
}

export type BattleHistoryGainSummary = {
  experience: number;
  enhancementMaterials: number;
  rice: number;
};

const enhancementMaterialId = /^(?:POTATO|SWEET_POTATO|CORN)_M\d+$/;

export function battleHistoryGainSummary(event: BattleHistoryEvent): BattleHistoryGainSummary {
  const snapshot = event.combatSnapshot;
  return {
    experience: Math.max(0, snapshot?.experienceGained ?? 0),
    enhancementMaterials: (snapshot?.rewards ?? []).reduce(
      (total, reward) => total + (enhancementMaterialId.test(reward.itemId) ? Math.max(0, reward.quantity) : 0),
      0,
    ),
    rice: Math.max(0, snapshot?.riceGained ?? 0),
  };
}

export function BattleHistoryGains({ event, compact = false }: { event: BattleHistoryEvent; compact?: boolean }) {
  const gains = battleHistoryGainSummary(event);
  return <dl className={compact ? "cozy-history-gains" : "battle-history-gains"} aria-label="전투 획득 요약">
    <div><dt>경험치</dt><dd>+{gains.experience.toLocaleString()}</dd></div>
    <div><dt>강화 재료</dt><dd>+{gains.enhancementMaterials.toLocaleString()}개</dd></div>
    <div><dt>쌀</dt><dd>+{gains.rice.toLocaleString()}</dd></div>
  </dl>;
}

function BattleResultDetails({ event }: { event: BattleHistoryEvent }) {
  const snapshot = event.combatSnapshot;
  if (!snapshot || (event.type !== "STAGE_CLEARED" && event.type !== "STAGE_FAILED")) return null;
  const enteredAt = battleHistoryStageEnteredAt(event);
  const rewards = (snapshot.rewards ?? []).filter((reward) => reward.quantity > 0);
  return <>
    <dl className="battle-history-result" aria-label="전투 결과">
      {enteredAt && <div><dt>스테이지 입장</dt><dd>{battleHistoryDateTime(enteredAt)}</dd></div>}
      {snapshot.elapsedTicks !== undefined && <div><dt>소요 시간</dt><dd>{battleHistoryDuration(snapshot.elapsedTicks)}</dd></div>}
      {snapshot.remainingHp !== undefined && <div><dt>남은 HP</dt><dd>{snapshot.remainingHp.toLocaleString()}</dd></div>}
      {event.type === "STAGE_FAILED" && snapshot.defeatedNormals !== undefined && (snapshot.normalCount === 0
        ? <div><dt>진행</dt><dd>보스전</dd></div>
        : <div><dt>처치 몬스터</dt><dd>{battleStageProgressLabel(false, snapshot.defeatedNormals)}</dd></div>)}
      {event.type === "STAGE_FAILED" && <div><dt>실패 이유</dt><dd>{battleHistoryResultLabel(event)}</dd></div>}
      {event.type === "STAGE_FAILED" && snapshot.lastEnemyRemainingHp !== undefined && <div><dt>마지막 몬스터 남은 HP</dt><dd>{snapshot.lastEnemyRemainingHp.toLocaleString()}</dd></div>}
    </dl>
    <section className="battle-history-rewards" aria-label="획득 보상">
      <strong>전투 획득</strong>
      <BattleHistoryGains event={event} />
      {rewards.length > 0 && <div className="battle-history-reward-items">
        <span>지급 아이템</span>
        <ul>{rewards.map((reward) => <li key={reward.itemId}><span>{reward.displayName}</span><b>+{reward.quantity.toLocaleString()}개</b></li>)}</ul>
      </div>}
    </section>
  </>;
}

function HistoryItem({ event }: { event: BattleHistoryEvent }) {
  return <li className={`battle-history-item is-${event.type.toLowerCase()}`}>
    <div className="battle-history-mark" aria-hidden="true">{event.type === "STAGE_FAILED" ? "!" : event.type === "STAGE_CLEARED" ? "✓" : "•"}</div>
    <div className="battle-history-copy">
      <div><span>{typeLabels[event.type]}</span><strong>{battleHistoryTargetLabel(event)}</strong></div>
      <BattleResultDetails event={event} />
    </div>
    <time dateTime={event.occurredAt}>{event.type === "STAGE_CLEARED" ? "클리어" : "패배"} {battleHistoryDateTime(event.occurredAt)}</time>
  </li>;
}

export function BattleHistoryScreen() {
  const history = useQuery({ queryKey: ["battle-history"], queryFn: battleApi.history, retry: false });
  const results = battleHistoryResultEvents(history.data);

  return <section className="battle-history-screen" aria-labelledby="battle-history-title">
    <header className="battle-history-heading">
      <div><p className="eyebrow">SERVER BATTLE RECORD</p><h2 id="battle-history-title">전투 기록</h2><p>최근 스테이지 승리·패배 결과를 최신순으로 확인합니다.</p></div>
      <strong><span>{results.length}</span> / 50</strong>
    </header>
    {history.isLoading && <div className="battle-history-message" role="status">전투 기록을 불러오는 중입니다.</div>}
    {history.error && <div className="battle-history-message error" role="alert"><strong>전투 기록을 불러오지 못했습니다.</strong><button type="button" onClick={() => history.refetch()}>다시 시도</button></div>}
    {history.data && results.length === 0 && <div className="battle-history-message"><strong>아직 완료된 전투 기록이 없습니다.</strong><span>스테이지 전투가 끝나면 승리·패배 결과가 여기에 표시됩니다.</span></div>}
    {results.length > 0 && <ol className="battle-history-list">{results.map((event) => <HistoryItem key={event.eventId} event={event} />)}</ol>}
  </section>;
}
