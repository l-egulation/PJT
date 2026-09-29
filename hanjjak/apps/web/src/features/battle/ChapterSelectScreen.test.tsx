// @vitest-environment happy-dom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { ChapterSelectScreen, chapterStates } from "./ChapterSelectScreen";
import { CHAPTER_SHOP_WIDTH_PERCENT, chapterOfStage, chapterShopPlacement } from "./chapterCatalog";
import type { StageSummary } from "./api";

function stage(stageId: string, unlocked: boolean, clearCount: number): StageSummary {
  return {
    stageId, unlocked, clearCount, contentVersion: "test", isCurrent: false, isRepeatTarget: false,
    repeatEligible: true, normalMonsterIds: [], bossMonsterId: null, backgroundId: null, bossOnly: false,
  };
}

const chapterStage = (chapter: number, number: number, unlocked: boolean, cleared: boolean) =>
  stage(`stage.${String(chapter).padStart(2, "0")}-${String(number).padStart(2, "0")}`, unlocked, cleared ? 2 : 0);

afterEach(cleanup);

const stages = [
  ...Array.from({ length: 10 }, (_, index) => chapterStage(1, index + 1, true, true)),
  ...Array.from({ length: 10 }, (_, index) => chapterStage(2, index + 1, index < 4, index < 2)),
];

describe("chapterOfStage", () => {
  it("reads the chapter out of the stage id", () => {
    expect(chapterOfStage("stage.01-07")).toBe(1);
    expect(chapterOfStage("stage.04-10")).toBe(4);
  });
});

describe("chapterShopPlacement", () => {
  /* 1~5는 아래층 왼쪽부터, 6~10은 위층 왼쪽부터다. 그림이 정해 준 배치다. */
  it("puts the first five downstairs and the rest upstairs", () => {
    expect(chapterShopPlacement(1).top).toBeCloseTo(chapterShopPlacement(5).top);
    expect(chapterShopPlacement(6).top).toBeCloseTo(chapterShopPlacement(10).top);
    expect(chapterShopPlacement(6).top).toBeLessThan(chapterShopPlacement(1).top);
    expect(chapterShopPlacement(1).left).toBeLessThan(chapterShopPlacement(5).left);
    expect(chapterShopPlacement(1).left).toBeCloseTo(chapterShopPlacement(6).left);
  });

  it("keeps every shop inside the arcade wall", () => {
    for (let chapter = 1; chapter <= 10; chapter += 1) {
      const { left } = chapterShopPlacement(chapter);
      expect(left + CHAPTER_SHOP_WIDTH_PERCENT).toBeLessThanOrEqual(100);
    }
  });
});

describe("chapterStates", () => {
  it("opens a chapter when any of its stages is open and clears it only when every stage is cleared", () => {
    const states = chapterStates(stages);
    expect(states[0]).toMatchObject({ unlocked: true, cleared: true, clearedStages: 10 });
    expect(states[1]).toMatchObject({ unlocked: true, cleared: false, clearedStages: 2 });
  });

  it("leaves a chapter without any stage locked", () => {
    expect(chapterStates(stages)[4]).toMatchObject({ unlocked: false, cleared: false });
  });
});

describe("ChapterSelectScreen", () => {
  it("follows the picked chapter in the footer and hands back the stage that was chosen", () => {
    const onSelectStage = vi.fn();
    const onClose = vi.fn();
    render(<ChapterSelectScreen stages={stages} selectedStageId="stage.01-07" onSelectStage={onSelectStage} onClose={onClose} />);

    expect(screen.getByRole("button", { name: "1챕터 과일가게" }).getAttribute("aria-pressed")).toBe("true");
    fireEvent.click(screen.getByRole("button", { name: "2챕터 음료" }));
    expect(screen.getByText("CHAPTER 2")).toBeTruthy();
    expect(screen.getByText("음료")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "챕터 이동" }));
    fireEvent.click(screen.getByRole("button", { name: "2-3 클리어 0회" }));
    expect(onSelectStage).toHaveBeenCalledWith("stage.02-03");
    expect(onClose).toHaveBeenCalled();
  });

  /* 가게 그림은 10칸 다 있다. 아직 못 갈 뿐인 챕터는 이름을 그대로 두고 누르지만
     못하게 한다. 몹·보스 에셋이 아직 없는 6챕터 패스트푸드만 "준비 중"이다. */
  it("will not let a locked chapter be entered", () => {
    render(<ChapterSelectScreen stages={stages} selectedStageId="stage.01-07" onSelectStage={() => undefined} onClose={() => undefined} />);

    fireEvent.click(screen.getByRole("button", { name: "7챕터 중식당 잠김" }));
    expect(screen.getByRole("button", { name: "챕터 이동" }).hasAttribute("disabled")).toBe(true);

    fireEvent.click(screen.getByRole("button", { name: "6챕터 패스트푸드 잠김" }));
    expect(screen.getByRole("button", { name: "준비 중" }).hasAttribute("disabled")).toBe(true);
  });
});
