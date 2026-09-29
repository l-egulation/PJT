import { describe, expect, it } from "vitest";
import { ADVENTURER_TUTORIAL_STEPS, BATTLE_GUIDE_TUTORIAL_STEPS } from "./tutorialContent";

describe("adventurer tutorial content", () => {
  it("introduces the picture-in-picture control before finishing the first journey", () => {
    const pipStep = ADVENTURER_TUTORIAL_STEPS.find((step) => step.id === "picture-in-picture");

    expect(pipStep).toMatchObject({
      target: ".picture-in-picture-shell",
      targetLabel: "화면 한켠",
    });
  });
});

describe("battle guide content", () => {
  it("reuses the adventurer dialogue and keeps the picture-in-picture step last", () => {
    const guideIds = BATTLE_GUIDE_TUTORIAL_STEPS.map((step) => step.id);

    ADVENTURER_TUTORIAL_STEPS.forEach((step) => expect(guideIds).toContain(step.id));
    expect(guideIds.at(-1)).toBe("picture-in-picture");
    expect(new Set(guideIds).size).toBe(guideIds.length);
  });

  it("only points at panels the battle screen actually renders", () => {
    const battleTargets = [
      ".cozy-player-status",
      ".cozy-stage-progress",
      ".cozy-stage-actions",
      ".cozy-rewards",
      ".cozy-growth-nav",
      ".cozy-utility-nav",
      ".cozy-history",
      ".picture-in-picture-shell",
    ];

    BATTLE_GUIDE_TUTORIAL_STEPS.forEach((step) => {
      if (step.target) expect(battleTargets).toContain(step.target);
    });
  });
});
