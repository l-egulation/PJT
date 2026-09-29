import { useEffect, useState } from "react";
import type { RaidAttempt } from "./api";
import { scheduleRaidTimeline } from "./raidTimeline";

function time(ms: number) { const seconds = Math.max(0, Math.ceil(ms / 1000)); return `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`; }
export function RaidBattle({ attempt, attemptsStarted, now = Date.now, onRetry, onConfirm, onDiscard, busy, uncertainCommand }: { attempt: RaidAttempt; attemptsStarted?: number; now?: () => number; onRetry: () => void; onConfirm: () => void; onDiscard: () => void; busy: boolean; uncertainCommand?: { label: string; retry: () => void } }) {
  const [clock, setClock] = useState(now());
  useEffect(() => { const timer = window.setInterval(() => setClock(now()), 250); return () => window.clearInterval(timer); }, [now]);
  const timeline = scheduleRaidTimeline(attempt.renderingTimeline.events, attempt.renderingTimeline.tickDurationMilliseconds);
  const played = timeline.filter((event) => event.atMilliseconds <= Math.max(0, clock - Date.parse(attempt.startedAt)));
  const latestEscalation = played.filter((event) => event.type === "ESCALATION").at(-1);
  const result = attempt.terminalResult ?? attempt.currentResult;
  const held = attempt.status === "RESULT_HELD";
  const retriesRemain = (attemptsStarted ?? 1) < 3;
  const mayConfirm = held && result.damage > 0;
  return <section className="raid-battle" aria-label="레이드 전투">
    <header><div><span>봉인 레이드 · {attempt.slotOrdinal}번 슬롯</span><h2>{attempt.inputSnapshot.boss.displayName}</h2></div><strong aria-label="남은 시간">{time(Date.parse(attempt.completableAt) - clock)}</strong></header>
    <div className="raid-battle-stage"><div className="raid-battle-stat"><small>피해량</small><strong>피해량 {result.damage.toLocaleString()}</strong><span>등급 {result.grade}</span></div><div className="raid-battle-stat"><small>봉인 기여</small><strong>{result.sealContribution.toLocaleString()}</strong><span>{result.timeLimitReached ? "시간 종료" : result.playerDied ? "전투 불능" : "전투 진행 중"}</span></div></div>
    <p className="raid-escalation">{latestEscalation ? `상승 ${latestEscalation.escalationStage}단계 · 다음 상승은 서버 전투 기록에서 반영됩니다.` : "상승 정보는 서버 전투 기록에서 반영됩니다."}</p>
    <ol className="raid-battle-log" aria-label="서버 전투 기록">{played.slice(-4).reverse().map((event) => <li key={event.sequence}><time>{time(event.atMilliseconds)}</time><span>{event.type}</span>{event.damage != null && <strong>{event.damage.toLocaleString()}</strong>}</li>)}</ol>
    {held && <div className="raid-held-result"><strong>결과를 보관했습니다.</strong><p>연결이 끊겨도 결과는 보관됩니다.</p>{result.reward && <p>서버 확정 보상 · 티켓 {result.reward.cosmeticTickets} · 보석함 {result.reward.gemBoxes} · 쌀 {result.reward.rice.toLocaleString()}</p>}<div>{retriesRemain && <button type="button" disabled={busy} onClick={onRetry}>다시 도전</button>}{mayConfirm && <button type="button" disabled={busy} onClick={onConfirm}>결과 확정</button>}<button type="button" disabled={busy} onClick={onDiscard}>{result.reward ? "보상 없이 나가기" : "나가기"}</button>{uncertainCommand && <button type="button" disabled={busy} onClick={uncertainCommand.retry}>{uncertainCommand.label} 다시 시도</button>}</div></div>}
  </section>;
}
