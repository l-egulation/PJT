// @vitest-environment happy-dom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RankingScreen } from "./RankingScreen";
import type { MaterialType, RankingEntry } from "./api";

const emptyAppearance = { head: null, top: null, bottom: null, gloves: null, shoes: null, cape: null };
const entry = (overallRank: number, rank: number, nickname: string, materialType: MaterialType, combatPower: number, level: number): RankingEntry => ({
  rank,
  overallRank,
  nickname,
  level,
  materialType,
  displayName: materialType === "POTATO" ? "감자 전문" : materialType === "SWEET_POTATO" ? "고구마 전문" : "옥수수 전문",
  combatPower,
  appearance: emptyAppearance,
  updatedAt: "2026-09-09T00:00:00Z",
});

const overall = [
  entry(1, 1, "고구마장군", "SWEET_POTATO", 150_000, 42),
  entry(2, 1, "감자왕", "POTATO", 123_456, 35),
  entry(3, 1, "옥수수대장", "CORN", 110_000, 31),
  entry(10, 9, "옥수수맨", "CORN", 90_000, 26),
];

const rankingFixture = {
  formulaVersion: "combat-power-v1",
  generatedAt: "2026-09-09T00:00:00Z",
  sourceStateVersion: 4,
  overallTop: overall,
  specializations: [
    { materialType: "POTATO" as const, displayName: "감자 전문", entries: [entry(2, 1, "감자왕", "POTATO", 123_456, 35), entry(8, 2, "감자둘", "POTATO", 95_000, 27), entry(11, 3, "감자셋", "POTATO", 88_000, 24)] },
    { materialType: "SWEET_POTATO" as const, displayName: "고구마 전문", entries: [entry(1, 1, "고구마장군", "SWEET_POTATO", 150_000, 42)] },
    { materialType: "CORN" as const, displayName: "옥수수 전문", entries: [entry(3, 1, "옥수수대장", "CORN", 110_000, 31), entry(10, 9, "옥수수맨", "CORN", 90_000, 26)] },
  ],
  myEntry: overall[3],
};

vi.mock("../auth/api", () => ({ authApi: { session: () => Promise.resolve({ authenticated: true, account: { nickname: "옥수수맨" } }) } }));
vi.mock("./api", () => ({ rankingApi: { combatPower: () => Promise.resolve(rankingFixture) } }));

afterEach(cleanup);

function renderRanking() {
  const client = new QueryClient();
  client.setQueryData(["auth", "session"], { authenticated: true, account: { nickname: "옥수수맨" } });
  client.setQueryData(["combat-power-ranking"], rankingFixture);
  return render(<QueryClientProvider client={client}><RankingScreen /></QueryClientProvider>);
}

describe("RankingScreen", () => {
  it("renders the trophy tabs, a 2-1-3 podium and the ranking board without row thumbnails", () => {
    const view = renderRanking();
    expect(screen.getByRole("tab", { name: "전체" }).getAttribute("aria-selected")).toBe("true");
    const podium = screen.getByLabelText("전체 랭킹 1위부터 3위");
    const podiumArticles = within(podium).getAllByRole("article");
    expect(podiumArticles.map((article) => article.getAttribute("aria-label"))).toEqual(["2위 감자왕", "1위 고구마장군", "3위 옥수수대장"]);
    expect(podium.querySelectorAll(".ranking-place-badge img")).toHaveLength(3);
    // 기본 아트는 판 번호가 붙은 주소로 불러야 예전 노란 막대기가 캐시에서 나오지 않는다.
    expect(within(podium).getByAltText("고구마장군의 젓가락 캐릭터 rest 자세").getAttribute("src"))
      .toBe("/assets/chapters/chapter-04-sushi/hero/rest_01.png?v=basic-wooden-sword-v1-20260911");
    expect(screen.getByRole("heading", { name: "전체 랭킹" })).toBeTruthy();
    expect(view.container.textContent).not.toContain("전투력 기준");
    expect(screen.getByText("150,000")).toBeTruthy();
    const list = view.container.querySelector<HTMLElement>(".ranking-list");
    expect(list).not.toBeNull();
    expect(within(list!).getAllByText("전투력")).toHaveLength(overall.length);
    expect(list!.querySelectorAll(".ranking-row-rank img")).toHaveLength(3);
    expect(view.container.textContent).not.toContain("종합 전투력은 이렇게 계산됩니다");
  });

  it("switches both the podium and list to the selected material ranking", () => {
    renderRanking();
    fireEvent.click(screen.getByRole("tab", { name: "감자" }));
    expect(screen.getByRole("tab", { name: "감자" }).getAttribute("aria-selected")).toBe("true");
    expect(screen.getByLabelText("감자 랭킹 1위부터 3위")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "감자 랭킹" })).toBeTruthy();
    expect(screen.getAllByText("감자둘")).toHaveLength(2);
    expect(screen.queryByText("고구마장군")).toBeNull();
  });
});
