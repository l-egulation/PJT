import { useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { arenaApi, type ArenaBattle, type ArenaOpponent } from "./api";
import { NoticeDialog } from "../../shared/NoticeDialog";
import "./ArenaScreen.css";

function ArenaResult({ battle }: { battle: ArenaBattle }) {
  const [showLog, setShowLog] = useState(false);
  const events = useMemo(() => battle.events.filter((event) => event.type === "FIGHTER_SKILL_CAST" || event.type === "FIGHTER_HIT" || event.type === "FIGHTER_BASIC_ATTACK" || event.type === "ARENA_FIGHTER_DEFEATED"), [battle.events]);
  const won = battle.result.winnerId !== null && battle.result.winnerId !== battle.opponent.accountId;
  return <section className="arena-result" aria-label="아레나 전투 결과">
    <p className="arena-kicker">ARENA · HP ×{battle.hpMultiplier}</p>
    <h3>{battle.result.draw ? "무승부" : won ? "승리" : "패배"}</h3>
    <div className="arena-hp-result"><span>내 잔여 HP {battle.result.attackerRemainingHp.toLocaleString()}</span><span>상대 잔여 HP {battle.result.defenderRemainingHp.toLocaleString()}</span></div>
    <button type="button" onClick={() => setShowLog((value) => !value)}>{showLog ? "전투 로그 닫기" : "전투 로그 보기"}</button>
    {showLog && <ol className="arena-log">{events.map((event) => <li key={event.eventId}><strong>{event.actorId === battle.opponent.accountId.toString() ? battle.opponent.nickname : "나"}</strong> {event.skillId ?? "기본공격"}{event.damage != null ? ` · ${event.damage.toLocaleString()} 피해` : ""}</li>)}</ol>}
  </section>;
}

export function ArenaScreen() {
  const opponents = useQuery({ queryKey: ["arena-opponents"], queryFn: arenaApi.opponents });
  const [selected, setSelected] = useState<ArenaOpponent | null>(null);
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID());
  const battle = useMutation({ mutationFn: (opponentId: string) => arenaApi.battle(opponentId, idempotencyKey), onSuccess: () => setIdempotencyKey(crypto.randomUUID()) });
  return <section className="arena-screen" aria-labelledby="arena-title">
    <header><p className="arena-kicker">PVP ARENA</p><h2 id="arena-title">아레나</h2><p>상대와 스킬을 주고받는 서버 판정 자동전투</p></header>
    {!battle.data && <><div className="arena-opponents">{opponents.data?.map((opponent) => <button type="button" key={opponent.accountId} className={selected?.accountId === opponent.accountId ? "selected" : ""} onClick={() => setSelected(opponent)}><strong>{opponent.nickname}</strong><span>Lv.{opponent.level} · {opponent.rating.toLocaleString()}점</span></button>)}</div><button className="arena-start" type="button" disabled={!selected || battle.isPending} onClick={() => selected && battle.mutate(selected.accountId)}>{battle.isPending ? "전투 계산 중" : "대전 시작"}</button></>}
    {battle.isError && <NoticeDialog
      message="대전을 시작하지 못했습니다. 다시 시도해 주세요."
      actions={<button type="button" className="notice-dialog-primary" autoFocus onClick={() => battle.reset()}>닫기</button>}
    />}
    {battle.data && <ArenaResult battle={battle.data} />}
  </section>;
}
