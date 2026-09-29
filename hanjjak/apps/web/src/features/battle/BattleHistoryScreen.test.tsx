import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderToStaticMarkup } from "react-dom/server";
import { expect, it } from "vitest";
import { BattleHistoryScreen, battleHistoryDuration, battleHistoryGainSummary, battleHistoryResultEvents, battleHistoryResultLabel, battleHistoryStageEnteredAt, battleHistoryTargetLabel } from "./BattleHistoryScreen";
import type { BattleHistoryEvent } from "./api";

const failure: BattleHistoryEvent = {
  eventId: "event-1",
  type: "STAGE_FAILED",
  occurredAt: "2026-09-09T00:00:00Z",
  stageId: "stage.01-07",
  dungeonId: null,
  resultCode: "BOSS_TIME_LIMIT_EXCEEDED",
  messageKey: "battle.history.boss.time.limit.exceeded",
  contentVersion: "enemy-v1-applied",
  combatSnapshot: { attack: 120, maxHp: 500, penetration: 32, stageEnteredAt: "2026-09-08T23:59:35Z", remainingHp: 100, defeatedNormals: 17, lastEnemyRemainingHp: 44, elapsedTicks: 250, experienceGained: 17, riceGained: 17, rewards: [] },
};

it("presents server-owned failure codes and snapshots without recalculation", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(["battle-history"], [failure]);

  const html = renderToStaticMarkup(<QueryClientProvider client={client}><BattleHistoryScreen /></QueryClientProvider>);

  expect(html).toContain("시간 제한 초과");
  expect(html).toContain("1-7");
  expect(html).not.toContain("공격력");
  expect(html).not.toContain("최대 HP");
  expect(html).not.toContain("캐릭터가 쓰러졌습니다");
  expect(html).toContain("스테이지 입장");
  expect(html).toContain("25.0초");
  expect(html).toContain("남은 HP");
  expect(html).toContain("100");
  expect(html).toContain("17 / 20");
  expect(html).toContain("마지막 몬스터 남은 HP");
  expect(html).toContain("44");
  expect(html).toContain("실패 이유");
  expect(html).toContain("경험치");
  expect(html).toContain("+17");
  expect(html).toContain("강화 재료");
  expect(html).toContain("쌀");
  expect(battleHistoryDuration(625)).toBe("1분 2.5초");
  expect(battleHistoryResultLabel(failure)).toBe("시간 제한 초과");
  expect(battleHistoryTargetLabel(failure)).toBe("1-7");
  client.clear();
});

it("shows boss progress instead of zero of twenty for stage 1-10 history", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(["battle-history"], [{ ...failure, stageId: "stage.01-10", combatSnapshot: { ...failure.combatSnapshot!, defeatedNormals: 0, normalCount: 0 } }]);
  const html = renderToStaticMarkup(<QueryClientProvider client={client}><BattleHistoryScreen /></QueryClientProvider>);

  expect(html).toContain("<dt>진행</dt><dd>보스전</dd>");
  expect(html).not.toContain("0 / 20");
  client.clear();
});

it("lists every granted clear reward with elapsed time and remaining HP", () => {
  const clear: BattleHistoryEvent = {
    eventId: "event-clear",
    type: "STAGE_CLEARED",
    occurredAt: "2026-09-09T00:01:00Z",
    stageId: "stage.01-03",
    dungeonId: null,
    resultCode: "STAGE_CLEARED",
    messageKey: "battle.history.stage.cleared",
    contentVersion: null,
    combatSnapshot: {
      attack: 150,
      maxHp: 600,
      penetration: 40,
      stageEnteredAt: "2026-09-08T23:59:57.500Z",
      remainingHp: 421,
      defeatedNormals: 20,
      lastEnemyRemainingHp: 0,
      elapsedTicks: 625,
      experienceGained: 25,
      riceGained: 30,
      rewards: [
        { itemId: "POTATO_M1", displayName: "감자 M1", quantity: 20 },
        { itemId: "skillbook:active_dot:normal", displayName: "노말 마! 쫄이나 비법서", quantity: 1 },
      ],
    },
  };
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(["battle-history"], [clear]);

  const html = renderToStaticMarkup(<QueryClientProvider client={client}><BattleHistoryScreen /></QueryClientProvider>);
  expect(html).toContain("경험치");
  expect(html).toContain("+25");

  expect(html).toContain("1분 2.5초");
  expect(html).toContain("421");
  expect(html).toContain("쌀");
  expect(html).toContain("+30");
  expect(html).toContain("감자 M1");
  expect(html).toContain("20개");
  expect(html).toContain("노말 마! 쫄이나 비법서");
  expect(html).toContain("1개");
  expect(battleHistoryGainSummary(clear)).toEqual({ experience: 25, enhancementMaterials: 20, rice: 30 });
  expect(html).not.toContain("보스를 처치하고 다음 전투를 준비합니다");
  client.clear();
});

it("shows only completed stage results and derives entry time for legacy snapshots", () => {
  const entered: BattleHistoryEvent = { ...failure, eventId: "entered", type: "STAGE_ENTERED", resultCode: "STAGE_ENTERED", combatSnapshot: null };
  const returned: BattleHistoryEvent = { ...failure, eventId: "returned", type: "RETURNED", resultCode: "PLAYER_DIED_RESTARTED", combatSnapshot: null };
  const legacy = { ...failure, eventId: "legacy", combatSnapshot: { ...failure.combatSnapshot!, stageEnteredAt: undefined } };

  expect(battleHistoryResultEvents([entered, failure, returned])).toEqual([failure]);
  expect(battleHistoryStageEnteredAt(legacy)).toBe("2026-09-08T23:59:35.000Z");
});

it("presents an explicit empty history state", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(["battle-history"], []);
  const html = renderToStaticMarkup(<QueryClientProvider client={client}><BattleHistoryScreen /></QueryClientProvider>);
  expect(html).toContain("아직 완료된 전투 기록이 없습니다.");
  client.clear();
});
