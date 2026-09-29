import { expect, it } from "vitest";
import type { EquipmentState } from "../equipment/api";
import type { GemState } from "../gems/api";
import type { SkillState } from "../skills/api";
import {
  chapterForSurface,
  chapterStepsForSurface,
  runtimeChapter,
  runtimeChapterTutorialId,
  TUTORIAL_RUNTIME_CHAPTERS,
} from "./tutorialRuntimeCatalog";
import {
  isTutorialChapterConditionMet,
  selectNextTutorialChapter,
  tutorialTriggerQueries,
  type TutorialTriggerSnapshot,
} from "./tutorialTriggers";

function equipmentState(craftable: boolean): EquipmentState {
  return {
    riceBalance: 1_000,
    slots: [{
      slot: "WEAPON",
      slotName: "무기",
      unlocked: !craftable,
      current: { grade: null, gradeName: null, enhancementLevel: 0, q: 0, stats: { attack: 0, maxHp: 0, penetration: 0 } },
      unlock: craftable
        ? { cost: { riceCost: 100, materials: [] }, result: { grade: "NORMAL", gradeName: "일반", enhancementLevel: 0, q: 1, stats: { attack: 5, maxHp: 0, penetration: 0 } }, statIncrease: { attack: 5, maxHp: 0, penetration: 0 }, executable: true, disabledReason: null }
        : null,
      enhance: null,
      promote: null,
      growthComplete: false,
    }],
  };
}

function skillState(ownedBooks: number): SkillState {
  return {
    riceBalance: 0,
    activeLoadout: [],
    skills: [{
      skillId: "skill.basic", name: "기본 일격", active: true, unlocked: false, grade: null, gradeName: null,
      level: 0, equippedSlot: null, effectText: "",
      action: { kind: "UNLOCK", targetGrade: null, targetLevel: 1, riceCost: 0, successBasisPoints: 10_000, executable: false, disabledReason: null, books: [{ itemId: "item.book", displayName: "비법서", requiredQuantity: 1, availableQuantity: ownedBooks }] },
    }],
  };
}

function gemState(patch: Partial<GemState> = {}): GemState {
  return {
    unlocked: true, tickets: 3, secondsUntilNextTicket: 0, todayBoss: "SURVIVAL",
    gemBoxQuantity: 0, gems: [], presets: {}, lockedPresets: [], contentVersion: "v1",
    ...patch,
  };
}

const NOTHING_DONE: ReadonlySet<string> = new Set<string>();

it("holds every chapter back until its documented trigger actually happens", () => {
  const empty: TutorialTriggerSnapshot = { surface: "battle" };

  expect(isTutorialChapterConditionMet("blacksmith", { ...empty, equipment: equipmentState(false) })).toBe(false);
  expect(isTutorialChapterConditionMet("skill-trainer", { ...empty, skills: skillState(0) })).toBe(false);
  expect(isTutorialChapterConditionMet("dungeon-knight", { ...empty, gems: gemState({ unlocked: false }) })).toBe(false);
  expect(isTutorialChapterConditionMet("gem-wizard", { ...empty, gems: gemState() })).toBe(false);
  expect(isTutorialChapterConditionMet("stylist", { ...empty, cosmeticsUnlocked: false })).toBe(false);
});

it("opens each chapter on the state the chapter document names", () => {
  const empty: TutorialTriggerSnapshot = { surface: "battle" };

  expect(isTutorialChapterConditionMet("blacksmith", { ...empty, equipment: equipmentState(true) })).toBe(true);
  expect(isTutorialChapterConditionMet("skill-trainer", { ...empty, skills: skillState(1) })).toBe(true);
  expect(isTutorialChapterConditionMet("dungeon-knight", { ...empty, gems: gemState() })).toBe(true);
  expect(isTutorialChapterConditionMet("gem-wizard", { ...empty, gems: gemState({ gemBoxQuantity: 1 }) })).toBe(true);
  expect(isTutorialChapterConditionMet("stylist", { ...empty, cosmeticsUnlocked: true })).toBe(true);
});

it("only starts the chapter that describes the screen the player is on", () => {
  const unlockedEverywhere = {
    equipment: equipmentState(true),
    skills: skillState(1),
    gems: gemState({ gemBoxQuantity: 2 }),
    cosmeticsUnlocked: true,
  };

  expect(selectNextTutorialChapter({ surface: "dungeon", ...unlockedEverywhere }, NOTHING_DONE)?.chapter.id).toBe("dungeon-knight");
  expect(selectNextTutorialChapter({ surface: "cosmetics", ...unlockedEverywhere }, NOTHING_DONE)?.chapter.id).toBe("stylist");
  expect(selectNextTutorialChapter({ surface: "market", ...unlockedEverywhere }, NOTHING_DONE)?.chapter.id).toBe("market-merchant");
  expect(selectNextTutorialChapter({ surface: null, ...unlockedEverywhere }, NOTHING_DONE)).toBeNull();
});

it("skips the go-to-this-screen step when the player is already on that screen", () => {
  const onBattle = selectNextTutorialChapter({ surface: "battle", equipment: equipmentState(true) }, NOTHING_DONE);
  expect(onBattle?.chapter.id).toBe("blacksmith");
  expect(onBattle?.startIndex).toBe(0);
  expect(onBattle?.chapter.steps[onBattle.startIndex].surface).toBe("battle");

  const onEquipment = selectNextTutorialChapter({ surface: "equipment", equipment: equipmentState(true) }, NOTHING_DONE);
  expect(onEquipment?.chapter.id).toBe("blacksmith");
  expect(onEquipment?.startIndex).toBe(1);
  expect(onEquipment?.chapter.steps[onEquipment.startIndex].surface).toBe("equipment");

  const onSkills = selectNextTutorialChapter({ surface: "skills", skills: skillState(1) }, NOTHING_DONE);
  expect(onSkills?.chapter.id).toBe("skill-trainer");
  expect(onSkills?.startIndex).toBe(1);

  const onGems = selectNextTutorialChapter({ surface: "gems", gems: gemState({ gemBoxQuantity: 1 }) }, NOTHING_DONE);
  expect(onGems?.chapter.id).toBe("gem-wizard");
  expect(onGems?.startIndex).toBe(1);
});

it("still holds a screen-entered chapter back until its condition is met", () => {
  expect(selectNextTutorialChapter({ surface: "equipment", equipment: equipmentState(false) }, NOTHING_DONE)).toBeNull();
  expect(selectNextTutorialChapter({ surface: "skills", skills: skillState(0) }, NOTHING_DONE)).toBeNull();
  expect(selectNextTutorialChapter({ surface: "gems", gems: gemState() }, NOTHING_DONE)).toBeNull();
});

it("replays only the steps that belong to the screen being looked at", () => {
  const blacksmith = runtimeChapter("blacksmith");

  expect(chapterStepsForSurface(blacksmith, "equipment").map((step) => step.id)).toEqual([
    "blacksmith-slots", "blacksmith-preview", "blacksmith-materials", "blacksmith-rice", "blacksmith-finish",
  ]);
  expect(chapterStepsForSurface(blacksmith, "equipment").every((step) => step.advanceMode !== "target")).toBe(true);
  expect(chapterForSurface("market")?.id).toBe("market-merchant");
  expect(chapterForSurface("battle")?.id).toBe("blacksmith");
});

it("runs simultaneously eligible chapters one at a time in the recommended order", () => {
  const snapshot: TutorialTriggerSnapshot = {
    surface: "battle",
    equipment: equipmentState(true),
    skills: skillState(1),
    gems: gemState({ gemBoxQuantity: 1 }),
  };

  const first = selectNextTutorialChapter(snapshot, NOTHING_DONE);
  expect(first?.chapter.id).toBe("blacksmith");

  const afterFirst = selectNextTutorialChapter(snapshot, new Set([first!.chapter.tutorialId]));
  expect(afterFirst?.chapter.id).toBe("skill-trainer");

  const afterSecond = selectNextTutorialChapter(snapshot, new Set([first!.chapter.tutorialId, afterFirst!.chapter.tutorialId]));
  expect(afterSecond?.chapter.id).toBe("gem-wizard");

  expect(selectNextTutorialChapter(snapshot, new Set(TUTORIAL_RUNTIME_CHAPTERS.map((chapter) => chapter.tutorialId)))).toBeNull();
});

it("stops asking the server for state the finished chapters no longer need", () => {
  expect(tutorialTriggerQueries(NOTHING_DONE)).toEqual({ equipment: true, skills: true, gems: true, cosmetics: true });

  const done = new Set([
    runtimeChapterTutorialId("blacksmith"),
    runtimeChapterTutorialId("dungeon-knight"),
    runtimeChapterTutorialId("gem-wizard"),
  ]);
  expect(tutorialTriggerQueries(done)).toEqual({ equipment: false, skills: true, gems: false, cosmetics: true });
});

it("never points a runtime step at a preview-only fixture target", () => {
  const targets = TUTORIAL_RUNTIME_CHAPTERS.flatMap((chapter) => chapter.steps.map((step) => step.target ?? ""));

  expect(targets.some((target) => target.includes("tutorial-equip-gem"))).toBe(false);
  expect(targets.some((target) => target.includes("tutorial-fusion-gem"))).toBe(false);
  expect(targets.some((target) => target.includes("preview-"))).toBe(false);
});
