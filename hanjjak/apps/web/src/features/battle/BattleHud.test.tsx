import { renderToStaticMarkup } from "react-dom/server";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { expect, it } from "vitest";
import { battleRemainingSeconds, battleRemainingSecondsAt, BattleHistoryPopoverItem, BattlePlayerStatus, BattleRewards, BattleStageProgress, BattleUtilityNavigation, CozySharedNavigation, rewardMeta } from "./BattleHud";

/* 메뉴가 우편함을 살펴 점을 찍으므로 시험에도 질의 상자가 있어야 한다. */
const markupWithQuery = (element: ReactElement) => renderToStaticMarkup(
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{element}</QueryClientProvider>,
);

const stages = [{
  stageId: "stage.01-07",
  unlocked: true,
  clearCount: 2,
  contentVersion: "test",
  isCurrent: true,
  isRepeatTarget: false,
  repeatEligible: true,
  normalMonsterIds: [],
  bossMonsterId: null,
  backgroundId: null,
  bossOnly: false,
}];

it("counts down the server-authored battle duration in whole seconds", () => {
  expect(battleRemainingSeconds(34_000, 100, 127)).toBe(21);
  expect(battleRemainingSeconds(34_000, 100, 400)).toBe(0);
});

it("shows normal-stage progress without a session timer", () => {
  const html = renderToStaticMarkup(<BattleStageProgress stages={stages} selectedStageId="stage.01-07" remainingSeconds={null} defeatedNormals={13} bossOnly={false} bossPhase={false} stageComplete={false} idleMode="AUTO_PROGRESS" idleModeSaving={false} repeatEligible onSelectStage={() => undefined} onChangeMode={() => undefined} />);
  expect(html).toContain("STAGE 1-7");
  expect(html).not.toContain("남은 제한시간");
  expect(html).toContain("13 / 20");
  expect(html).toContain('aria-valuenow="13"');
  expect(html).toContain("width:65%");
});

it("moves live boss HP into a dedicated bar below the stage controls", () => {
  const html = renderToStaticMarkup(<BattleStageProgress stages={stages} selectedStageId="stage.01-07" remainingSeconds={4} defeatedNormals={20} bossOnly={false} bossPhase stageComplete={false} idleMode="AUTO_PROGRESS" idleModeSaving={false} repeatEligible bossHealth={{ name: "게살군함", hp: 517, maxHp: 782 }} onSelectStage={() => undefined} onChangeMode={() => undefined} />);
  expect(html).toContain("게살군함");
  expect(html).toContain('aria-label="게살군함 보스 체력"');
  expect(html).toContain('aria-valuenow="517"');
  expect(html).toContain('aria-valuemax="782"');
});
it("shows the limit only during a timed boss phase", () => {
  const html = renderToStaticMarkup(<BattleStageProgress stages={stages} selectedStageId="stage.01-07" remainingSeconds={4.9} defeatedNormals={20} bossOnly={false} bossPhase stageComplete={false} idleMode="AUTO_PROGRESS" idleModeSaving={false} repeatEligible onSelectStage={() => undefined} onChangeMode={() => undefined} />);
  expect(html).toContain("남은 제한시간");
  expect(html).toContain("4초");
  expect(html).not.toContain("4.9초");
});

it("presents stage 10 as a final-boss-only encounter", () => {
  const finalStage = [{ ...stages[0], stageId: "stage.03-10", normalMonsterIds: [], bossMonsterId: "futomaki-king", bossOnly: true }];
  const html = renderToStaticMarkup(<BattleStageProgress stages={finalStage} selectedStageId="stage.03-10" remainingSeconds={12} defeatedNormals={0} bossOnly bossPhase stageComplete={false} idleMode="AUTO_PROGRESS" idleModeSaving={false} repeatEligible={false} onSelectStage={() => undefined} onChangeMode={() => undefined} />);

  expect(html).toContain("최종 보스");
  expect(html).toContain('aria-label="최종 보스 처치 진행"');
  expect(html).toContain('aria-valuemax="1"');
  expect(html).not.toContain("0 / 20");
});

it("shows at most four reward rows at once and keeps additional rows pageable", () => {
  const html = renderToStaticMarkup(<BattleRewards stageId="stage.01-07" rice={1284} rewards={[
    { itemId: "SWEET_POTATO_M1", requestedQuantity: 12, grantedQuantity: 12, discardedQuantity: 0 },
    { itemId: "CORN_M1", requestedQuantity: 3, grantedQuantity: 3, discardedQuantity: 0 },
    { itemId: "POTATO_M1", requestedQuantity: 7, grantedQuantity: 7, discardedQuantity: 0 },
    { itemId: "SKILL_BOOK", requestedQuantity: 1, grantedQuantity: 1, discardedQuantity: 0 },
  ]} />);
  expect((html.match(/<li>/g) ?? [])).toHaveLength(4);
  expect(html).toContain("5종 획득");
  expect(html).toContain("1 / 2");
  expect(html).toContain("고구마 한 조각");
  expect(html).toContain("옥수수 한 알");
});

it("maps a granted skillbook item id to its catalog display name", () => {
  const html = renderToStaticMarkup(<BattleRewards stageId="stage.01-07" rice={0} rewards={[
    { itemId: "skillbook:active_dot:normal", requestedQuantity: 1, grantedQuantity: 1, discardedQuantity: 0 },
  ]} />);
  expect(html).toContain("노말 마! 쫄이나 비법서");
  expect(html).not.toContain("skillbook:active_dot:normal");
});

it("maps offline material item ids to player-facing labels", () => {
  expect(rewardMeta("POTATO_M1").label).toBe("감자 한 조각");
  expect(rewardMeta("POTATO_M1").label).not.toBe("POTATO_M1");
});

it("shows experience, enhancement material, and rice gains in a HUD history record", () => {
  const html = renderToStaticMarkup(<BattleHistoryPopoverItem event={{
    eventId: "history-1",
    type: "STAGE_FAILED",
    occurredAt: "2026-09-10T00:01:00Z",
    stageId: "stage.01-07",
    dungeonId: null,
    resultCode: "PLAYER_DEFEATED",
    messageKey: "battle.history.player.defeated",
    contentVersion: "enemy-v1-applied",
    combatSnapshot: {
      attack: 120,
      maxHp: 500,
      penetration: 32,
      remainingHp: 0,
      defeatedNormals: 17,
      lastEnemyRemainingHp: 44,
      elapsedTicks: 250,
      experienceGained: 170,
      riceGained: 85,
      rewards: [
        { itemId: "POTATO_M1", displayName: "감자 M1", quantity: 7 },
        { itemId: "CORN_M1", displayName: "옥수수 M1", quantity: 3 },
      ],
    },
  }} />);

  expect(html).toContain("전투 획득 요약");
  expect(html).toContain("경험치");
  expect(html).toContain("+170");
  expect(html).toContain("강화 재료");
  expect(html).toContain("+10개");
  expect(html).toContain("쌀");
  expect(html).toContain("+85");
  expect(html).toContain("감자 M1 +7");
  expect(html).toContain("옥수수 M1 +3");
});

it("keeps HP and EXP as equal-width live progress tracks", () => {
  const html = renderToStaticMarkup(<BattlePlayerStatus level={24} hp={2680} maxHp={3680} experience={18720} experienceRequired={30000} experiencePercent={62.4} />);
  expect(html).toContain('aria-label="HP"');
  expect(html).toContain('aria-valuenow="2680"');
  expect(html).toContain('aria-label="EXP"');
  expect(html).toContain('aria-valuenow="18720"');
  expect(html).toContain("62.4%");
});

it("reuses profile and both navigation groups on management screens", () => {
  const html = markupWithQuery(<CozySharedNavigation
    account={{ accountId: "account-1", characterId: "character-1", email: "test@example.com", nickname: "한짝", level: 24, experience: 18720, rice: 15820 }}
    activeRoute="market"
    onOpenProfile={() => undefined}
    onNavigate={() => undefined}
  />);

  expect(html).toContain('aria-label="내 정보 열기"');
  expect(html).toContain('aria-label="주요 관리 메뉴"');
  expect(html).toContain('aria-label="성장 메뉴"');
  expect(html).toContain('aria-current="page"');
  expect(html).toContain("15,820");
  expect(html).toContain("전투");
  expect(html).toContain("설정");
  expect(html).toContain("보석");
  /* 메시지함이 열려 이제 이 줄에 잠긴 자리는 치장 뽑기뿐이다. */
  expect(html).toContain("1-5 해금");
});

it("keeps the battle utility buttons in the approved six-item order", () => {
  const html = markupWithQuery(<BattleUtilityNavigation
    activeRoute="battle"
    gemContentUnlocked
    cosmeticContentUnlocked
    onNavigate={() => undefined}
  />);
  expect([...html.matchAll(/data-route="([^"]+)"/g)].map(match => match[1])).toEqual([
    "cosmetics", "content", "market", "ranking", "mail", "settings",
  ]);
  expect(html).not.toContain("첫 클리어 보상");
});

it("counts the stage timer down on the clock so it never climbs back up", () => {
  const deadline = "2026-09-13T00:01:00.000Z";
  const at = (iso: string) => battleRemainingSecondsAt(deadline, Date.parse(iso));
  expect(at("2026-09-13T00:00:12.000Z")).toBe(48);
  // 틱이 뒤로 가도 시계는 앞으로만 간다.
  expect(at("2026-09-13T00:00:12.900Z")).toBe(48);
  expect(at("2026-09-13T00:00:13.000Z")).toBe(47);
  expect(at("2026-09-13T00:02:00.000Z")).toBe(0);
  expect(battleRemainingSecondsAt(null, Date.now())).toBe(0);
  expect(battleRemainingSecondsAt("not a date", Date.now())).toBe(0);
});
