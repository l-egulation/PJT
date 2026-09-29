import type { EquipmentState } from "../equipment/api";
import type { GemState } from "../gems/api";
import type { SkillState } from "../skills/api";
import {
  chapterStartIndex,
  TUTORIAL_RUNTIME_CHAPTERS,
  type TutorialRuntimeChapter,
  type TutorialRuntimeChapterId,
  type TutorialRuntimeSurface,
} from "./tutorialRuntimeCatalog";

/*
 * 장별 시작 계기는 docs/70-plans/progressive-onboarding/tutorial-chapters-and-costumes.md
 * 의 `시작 계기` 열이 정본이다. 조건은 모두 실제 계정 응답으로만 판단하고, 검토용 fixture
 * 나 미리보기 전용 id 는 쓰지 않는다.
 */
export type TutorialTriggerSnapshot = {
  /* 지금 열려 있는 화면. 장은 자기 첫 단계의 화면에서만 시작한다. */
  surface: TutorialRuntimeSurface | null;
  equipment?: EquipmentState | null;
  skills?: SkillState | null;
  gems?: GemState | null;
  cosmeticsUnlocked?: boolean | null;
};

/** 아직 만들지 않은 부위 가운데 지금 바로 제작할 수 있는 것이 있는가. */
function canCraftFirstEquipment(equipment: EquipmentState | null | undefined): boolean {
  return equipment?.slots.some((slot) => !slot.unlocked && slot.unlock?.executable === true) === true;
}

/** 어떤 스킬이든 실제로 손에 든 비법서가 한 권이라도 있는가. */
function ownsSkillBook(skills: SkillState | null | undefined): boolean {
  return skills?.skills.some((skill) => skill.action.books.some((book) => book.availableQuantity > 0)) === true;
}

/** 장착하거나 합성할 수 있는 보석, 또는 아직 열지 않은 보석함을 가지고 있는가. */
function ownsGemOrGemBox(gems: GemState | null | undefined): boolean {
  if (!gems?.unlocked) return false;
  return gems.gemBoxQuantity > 0 || gems.gems.length > 0;
}

export function isTutorialChapterConditionMet(
  id: TutorialRuntimeChapterId,
  snapshot: TutorialTriggerSnapshot,
): boolean {
  switch (id) {
    /* 최초 장비 제작 가능 시점. 문서의 "성장 제안이 필요한 첫 실패" 갈래는 아직 판정 기준이 없어 쓰지 않는다. */
    case "blacksmith": return canCraftFirstEquipment(snapshot.equipment);
    case "skill-trainer": return ownsSkillBook(snapshot.skills);
    /* 던전·치장은 1-5 최초 클리어로 열린다. 서버가 내려준 해금 상태만 믿는다. */
    case "dungeon-knight": return snapshot.gems?.unlocked === true;
    case "gem-wizard": return ownsGemOrGemBox(snapshot.gems);
    case "stylist": return snapshot.cosmeticsUnlocked === true;
    /* 거래소는 사용자가 직접 들어온 첫 방문 자체가 계기다. 화면 조건이 곧 시작 조건이다. */
    case "market-merchant": return true;
  }
}

/** 어느 장을, 몇 번째 단계부터 열지. */
export type TutorialChapterStart = { chapter: TutorialRuntimeChapter; startIndex: number };

/*
 * 여러 조건이 한꺼번에 충족돼도 한 번에 하나만 연다. 지금 보고 있는 화면을 설명하는
 * 단계가 있는 장만 후보가 되므로 화면이 1차 필터가 되고, 남은 후보는 권장 순서(priority)로
 * 고른다. 전투 화면이면 `장비 버튼을 눌러 봐` 단계부터, 사용자가 이미 그 화면에 들어와
 * 있으면 그 화면을 설명하는 단계부터 시작한다.
 */
export function selectNextTutorialChapter(
  snapshot: TutorialTriggerSnapshot,
  completedTutorialIds: ReadonlySet<string>,
): TutorialChapterStart | null {
  if (!snapshot.surface) return null;
  const eligible = TUTORIAL_RUNTIME_CHAPTERS
    .map((chapter) => ({ chapter, startIndex: chapterStartIndex(chapter, snapshot.surface) }))
    .filter((entry) => entry.startIndex >= 0)
    .filter((entry) => !completedTutorialIds.has(entry.chapter.tutorialId))
    .filter((entry) => isTutorialChapterConditionMet(entry.chapter.id, snapshot));
  if (eligible.length === 0) return null;
  return eligible.reduce((best, entry) => (entry.chapter.priority < best.chapter.priority ? entry : best));
}

/*
 * 조건 판정에 필요한 응답만 받아 온다. 이미 끝낸 장의 상태는 다시 물어보지 않는다.
 */
export function tutorialTriggerQueries(completedTutorialIds: ReadonlySet<string>): {
  equipment: boolean;
  skills: boolean;
  gems: boolean;
  cosmetics: boolean;
} {
  const pending = (id: TutorialRuntimeChapterId) => {
    const chapter = TUTORIAL_RUNTIME_CHAPTERS.find((entry) => entry.id === id);
    return chapter !== undefined && !completedTutorialIds.has(chapter.tutorialId);
  };
  return {
    equipment: pending("blacksmith"),
    skills: pending("skill-trainer"),
    gems: pending("dungeon-knight") || pending("gem-wizard"),
    cosmetics: pending("stylist"),
  };
}
