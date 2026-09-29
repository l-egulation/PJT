import type { AppScreen } from "../../navigationState";
import blacksmithPoses from "./assets/blacksmith-poses.png";
import dungeonKnightPoses from "./assets/dungeon-knight-poses.png";
import gemWizardPoses from "./assets/gem-wizard-poses.png";
import marketMerchantPoses from "./assets/market-merchant-poses.png";
import skillTrainerPoses from "./assets/skill-trainer-poses.png";
import stylistPoses from "./assets/stylist-poses.png";
import type { TutorialStep } from "./tutorialContent";

/*
 * 검토용 미리보기 카탈로그(tutorialPreviewCatalog)와 런타임 카탈로그는 일부러 나눠 둔다.
 * 미리보기는 고정 fixture 를 주입한 화면을 설명하므로 `tutorial-equip-gem`, `preview-2-head`
 * 같은 검토 전용 대상과 "연습용 2,000쌀" 같은 대사를 쓴다. 실제 계정에는 그런 보상도,
 * 그런 id 도 없다. 런타임은 실제 화면에 늘 존재하는 대상만 가리키고, 재화를 소비하는
 * 단추(제작·강화·뽑기·합성)는 설명만 하고 대신 눌러 주지 않는다.
 * 근거: docs/70-plans/progressive-onboarding/content-quest-tutorial-map.md 10절
 * "뽑기·합성·구매·등록의 자동 실행"을 하지 않는다.
 */
export type TutorialRuntimeSurface = Extract<AppScreen, "battle" | "equipment" | "skills" | "gems" | "dungeon" | "cosmetics" | "market">;

export type TutorialRuntimeStep = TutorialStep & {
  surface: TutorialRuntimeSurface;
  /* target 은 사용자가 강조된 대상을 직접 눌러야 넘어간다. 화면을 여는 단추에만 쓴다. */
  advanceMode?: "dialogue" | "target";
};

export type TutorialRuntimeChapterId =
  | "blacksmith"
  | "skill-trainer"
  | "dungeon-knight"
  | "gem-wizard"
  | "stylist"
  | "market-merchant";

export type TutorialRuntimeChapter = {
  id: TutorialRuntimeChapterId;
  /* 계정별 진행 상태 저장 키. 대사가 바뀌어도 이미 끝낸 계정을 되돌리지 않도록 버전을 붙인다. */
  tutorialId: string;
  label: string;
  characterSrc: string;
  characterAlt: string;
  /* 낮을수록 먼저 실행한다. 권장 순서는 tutorial-chapters-and-costumes.md 를 따른다. */
  priority: number;
  steps: TutorialRuntimeStep[];
};

export const TUTORIAL_RUNTIME_CHAPTERS: TutorialRuntimeChapter[] = [
  {
    id: "blacksmith",
    tutorialId: "blacksmith-equipment-v1",
    label: "대장장이의 장",
    characterSrc: blacksmithPoses,
    characterAlt: "대장장이 복장을 입은 한짝",
    priority: 1,
    steps: [
      { id: "blacksmith-open", surface: "battle", target: ".cozy-growth-nav button[data-route=\"equipment\"]", targetLabel: "장비", advanceMode: "target", message: "이제 장비 쪽을 확인해 볼까? 아래 장비 버튼을 직접 눌러 봐!" },
      { id: "blacksmith-slots", surface: "equipment", target: ".equipment-loadout-stage", targetLabel: "장비 부위", message: "장비 창이 열렸어! 여섯 부위는 각각 따로 자라. 만들거나 올리고 싶은 부위를 눌러 골라 봐." },
      { id: "blacksmith-preview", surface: "equipment", target: ".equipment-upgrade-preview", targetLabel: "현재·다음", message: "고른 부위의 지금 상태와 다음 단계 수치를 여기서 나란히 비교할 수 있어." },
      { id: "blacksmith-materials", surface: "equipment", target: ".equipment-material-section", targetLabel: "필요 재료", message: "필요한 재료와 지금 가진 수량은 여기서 확인해. 모자라면 옆의 장터 단추로 바로 구하러 갈 수 있어." },
      { id: "blacksmith-rice", surface: "equipment", target: ".equipment-action-rice", targetLabel: "필요 쌀", message: "쌀도 함께 들어가. 보유한 쌀과 필요한 쌀을 여기서 비교하면 돼." },
      { id: "blacksmith-finish", surface: "equipment", target: ".equipment-enhance-button", targetLabel: "제작·강화", message: "재료와 쌀이 모두 준비되면 이 단추가 열려. 준비됐을 때 직접 눌러 장비를 키워 줘!" },
    ],
  },
  {
    id: "skill-trainer",
    tutorialId: "skill-trainer-skills-v1",
    label: "비법 수련가의 장",
    characterSrc: skillTrainerPoses,
    characterAlt: "비법 수련가 복장을 입은 한짝",
    priority: 2,
    steps: [
      { id: "skill-open", surface: "battle", target: ".cozy-growth-nav button[data-route=\"skills\"]", targetLabel: "스킬", advanceMode: "target", message: "비법서를 얻었구나! 아래 스킬 버튼을 직접 눌러 수련장을 열어 보자." },
      { id: "skill-welcome", surface: "skills", target: ".skills-heading", targetLabel: "비법 수련", message: "스킬 창이야. 여기서 새 스킬을 해금하고 성장시킬 수 있어." },
      { id: "skill-passive", surface: "skills", target: ".skill-growth-grid", targetLabel: "스킬 목록", message: "스킬 카드에서 액티브와 패시브 효과를 확인할 수 있어. 패시브는 장착하지 않아도 늘 적용돼!" },
      { id: "skill-slots", surface: "skills", target: ".skill-loadout", targetLabel: "자동 사용 순서", message: "액티브 스킬은 이곳에 장착한 순서대로 자동 사용돼. 빈 칸을 눌러 순서를 정해 보자." },
      { id: "skill-finish", surface: "skills", target: ".skill-enhance-panel", targetLabel: "강화·승급", message: "카드를 고르면 아래 칸에 필요한 비법서와 쌀이 나와. 다 모았다면 여기서 강화하거나 승급해 줘." },
    ],
  },
  {
    id: "dungeon-knight",
    tutorialId: "dungeon-knight-dungeon-v1",
    label: "던전 기사의 장",
    characterSrc: dungeonKnightPoses,
    characterAlt: "던전 기사 복장을 입은 한짝",
    priority: 3,
    steps: [
      { id: "dungeon-welcome", surface: "dungeon", target: ".dungeon-bookmarks", targetLabel: "오늘의 입장문", message: "오늘은 내가 던전 안내 기사야! 세 입장문 가운데 빛나는 문이 오늘 도전할 수 있는 던전이야." },
      { id: "dungeon-boss", surface: "dungeon", target: ".dungeon-keyart-wrap", targetLabel: "오늘의 보스", message: "우동은 생존형, 메추리알 장조림은 폭주형, 도토리묵은 장갑형 보스야." },
      { id: "dungeon-ticket", surface: "dungeon", target: ".dungeon-book-status", targetLabel: "입장권·단계", message: "도전 전에는 남은 입장권과 현재 도전 단계를 꼭 확인해 줘." },
      { id: "dungeon-finish", surface: "dungeon", target: ".dungeon-book-actions", targetLabel: "보상·도전", message: "예상 보상을 확인하고 도전 시작! 이미 이긴 단계는 입장권 한 장으로 소탕할 수 있어." },
    ],
  },
  {
    id: "gem-wizard",
    tutorialId: "gem-wizard-gems-v1",
    label: "보석 마법사의 장",
    characterSrc: gemWizardPoses,
    characterAlt: "보석 마법사 복장을 입은 한짝",
    priority: 4,
    steps: [
      { id: "gem-open", surface: "battle", target: ".cozy-growth-nav button[data-route=\"gems\"]", targetLabel: "보석", advanceMode: "target", message: "반짝이는 보석이 생겼네! 아래 보석 버튼을 직접 눌러 보석함을 열어 보자." },
      { id: "gem-welcome", surface: "gems", target: ".gems-heading", targetLabel: "보석함", message: "보석 창이야. 보유 보석함과 보석을 여기서 관리해." },
      { id: "gem-inventory", surface: "gems", target: ".gem-inventory-panel", targetLabel: "보유 보석", message: "오른쪽은 지금 가진 보석이야. 보석함이 남아 있다면 여기서 열어 새 보석을 꺼낼 수 있어." },
      { id: "gem-slots", surface: "gems", target: ".gem-slot-zone", targetLabel: "프리셋 슬롯", message: "왼쪽 슬롯에 보석을 올리면 장착돼. 전투 종류마다 다른 보석판을 만들어 둘 수 있어." },
      { id: "gem-save", surface: "gems", target: ".gem-save-button", targetLabel: "프리셋 저장", message: "조합을 바꿨다면 프리셋 저장을 눌러야 전투에 반영돼." },
      { id: "gem-finish", surface: "gems", target: ".gem-management-tabs", targetLabel: "합성 탭", message: "같은 레벨 보석이 세 개 모이면 합성 탭에서 한 단계 높은 보석으로 합칠 수 있어. 아껴 둘 보석은 잠금도 활용해 줘!" },
    ],
  },
  {
    id: "stylist",
    tutorialId: "stylist-cosmetics-v1",
    label: "스타일리스트의 장",
    characterSrc: stylistPoses,
    characterAlt: "스타일리스트 복장을 입은 한짝",
    priority: 5,
    steps: [
      { id: "style-welcome", surface: "cosmetics", target: ".cosmetic-screen-heading", targetLabel: "치장 뽑기", message: "새로운 옷이 열렸어! 치장 뽑기에서 한짝만의 멋진 모습을 찾아보자." },
      { id: "style-banner", surface: "cosmetics", target: ".cosmetic-banner-card", targetLabel: "뽑기 배너", message: "배너마다 나오는 치장과 확률이 달라. 무엇이 나올 수 있는지 미리 확인하고 고르면 돼." },
      { id: "style-cost", surface: "cosmetics", target: ".cosmetic-cost-buttons", targetLabel: "뽑기 비용", message: "1회와 10회 비용은 여기서 확인해. 뽑기권이 먼저 쓰이고 없으면 쌀이 들어가니, 누르기 전에 꼭 봐 줘." },
      { id: "style-finish", surface: "cosmetics", target: ".cosmetic-milestone", targetLabel: "마일스톤", message: "뽑을수록 마일스톤이 차올라. 새로 얻은 치장은 캐릭터 창의 옷장에서 바로 입어 볼 수 있어!" },
    ],
  },
  {
    id: "market-merchant",
    tutorialId: "market-merchant-market-v1",
    label: "장터 상인의 장",
    characterSrc: marketMerchantPoses,
    characterAlt: "장터 상인 복장을 입은 한짝",
    priority: 6,
    steps: [
      { id: "market-welcome", surface: "market", target: ".market-plaza-heading", targetLabel: "한짝 장터", message: "필요한 물건을 찾고 있구나! 거래소를 열었어. 먼저 거래할 품목을 골라 보자." },
      { id: "market-catalog", surface: "market", target: ".market-plaza-catalog", targetLabel: "거래 품목", message: "왼쪽 목록에서 품목을 고르면 지금 나와 있는 가격과 물량을 볼 수 있어." },
      { id: "market-side", surface: "market", target: ".market-plaza-side-toggle", targetLabel: "구매·판매", message: "구매와 판매는 여기서 바꿔. 수량과 단가를 정해 주문을 등록하고, 체결 전에는 취소도 가능해." },
      { id: "market-finish", surface: "market", target: ".market-plaza-tabs", targetLabel: "주문·수령함", message: "내 주문과 물품 수령함, 판매 정산까지 위 메뉴에서 확인해야 진짜 거래 완료야!" },
    ],
  },
];

export const TUTORIAL_RUNTIME_IDS: readonly string[] = TUTORIAL_RUNTIME_CHAPTERS.map((chapter) => chapter.tutorialId);

const RUNTIME_SURFACES: readonly TutorialRuntimeSurface[] = ["battle", "equipment", "skills", "gems", "dungeon", "cosmetics", "market"];

/** 자동 실행 대상 화면인지 가린다. 나머지 화면에서는 어떤 장도 새로 열지 않는다. */
export function tutorialRuntimeSurface(screen: AppScreen): TutorialRuntimeSurface | null {
  return RUNTIME_SURFACES.includes(screen as TutorialRuntimeSurface) ? screen as TutorialRuntimeSurface : null;
}

export function runtimeChapter(id: TutorialRuntimeChapterId): TutorialRuntimeChapter {
  const chapter = TUTORIAL_RUNTIME_CHAPTERS.find((entry) => entry.id === id);
  if (!chapter) throw new Error(`알 수 없는 런타임 튜토리얼 장: ${id}`);
  return chapter;
}

/** 장 id 로 계정별 진행 상태 저장 키를 찾는다. */
export function runtimeChapterTutorialId(id: TutorialRuntimeChapterId): string {
  return runtimeChapter(id).tutorialId;
}

/*
 * 한 장은 자기 첫 단계의 화면에서만 열리지 않는다. 사용자가 성장 메뉴를 먼저 눌러
 * 그 화면에 들어와도, 그 화면을 설명하는 단계부터 이어서 연다. 전투 화면에서 시작하는
 * 장은 `장비 버튼을 눌러 봐` 같은 안내 단계가 앞에 붙어 있고, 이미 그 화면에 있는
 * 사용자에게는 그 단계가 필요 없다.
 */
export function chapterStartIndex(chapter: TutorialRuntimeChapter, surface: TutorialRuntimeSurface | null): number {
  if (!surface) return -1;
  return chapter.steps.findIndex((step) => step.surface === surface);
}

/** 지금 화면을 설명하는 장. 화면마다 하나씩만 있다. */
export function chapterForSurface(surface: TutorialRuntimeSurface | null): TutorialRuntimeChapter | null {
  if (!surface) return null;
  return TUTORIAL_RUNTIME_CHAPTERS.find((chapter) => chapter.steps.some((step) => step.surface === surface)) ?? null;
}

/*
 * 다시 보기는 지금 보고 있는 화면의 단계만 재생한다. 화면을 옮기는 단계를 섞으면
 * 다시 보기가 사용자를 다른 화면으로 끌고 간다.
 */
export function chapterStepsForSurface(chapter: TutorialRuntimeChapter, surface: TutorialRuntimeSurface): TutorialRuntimeStep[] {
  return chapter.steps.filter((step) => step.surface === surface).map((step) => ({ ...step, advanceMode: "dialogue" }));
}

/*
 * 네이티브 `<dialog>` 를 `showModal()` 로 여는 화면의 뿌리. 그렇게 열린 창은 브라우저의
 * top layer 에 그려져서, 바깥에 둔 요소는 z-index 를 아무리 올려도 그 아래로 깔리고
 * 클릭도 받지 못한다. 이 세 화면에서는 안내 오버레이와 다시 보기 단추를 창 안에 심는다.
 * 나머지 화면은 평범한 칸이므로 `document.body` 에 그대로 얹는다. 거기에 굳이 심으면
 * 배경 그림에 쓰인 filter 때문에 화면 전체를 덮어야 할 오버레이가 그 칸 안에 갇힌다.
 */
export const TUTORIAL_MODAL_SCREEN_ROOTS: Partial<Record<TutorialRuntimeSurface, string>> = {
  equipment: ".equipment-screen",
  skills: ".skills-screen",
  gems: ".gem-management-screen",
};
