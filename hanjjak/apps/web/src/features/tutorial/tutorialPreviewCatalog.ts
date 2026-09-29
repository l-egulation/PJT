import adventurerPoses from "./assets/adventurer-poses.png";
import blacksmithPoses from "./assets/blacksmith-poses.png";
import dungeonKnightPoses from "./assets/dungeon-knight-poses.png";
import gemWizardPoses from "./assets/gem-wizard-poses.png";
import marketMerchantPoses from "./assets/market-merchant-poses.png";
import skillTrainerPoses from "./assets/skill-trainer-poses.png";
import stylistPoses from "./assets/stylist-poses.png";
import { ADVENTURER_TUTORIAL_STEPS, type TutorialStep } from "./tutorialContent";

export type TutorialPreviewSurface =
  | "battle"
  | "inventory"
  | "equipment"
  | "skills"
  | "dungeon"
  | "gems"
  | "gems-fusion"
  | "cosmetics"
  | "character-cosmetics"
  | "character-gallery"
  | "market-buy"
  | "market-sell"
  | "market-history"
  | "ranking"
  | "settings";

export type TutorialPreviewStep = TutorialStep & {
  surface: TutorialPreviewSurface;
  advanceMode?: "dialogue" | "target";
};

export type TutorialPreviewChapter = {
  id: string;
  label: string;
  shortLabel: string;
  characterSrc: string;
  characterAlt: string;
  steps: TutorialPreviewStep[];
};

export const TUTORIAL_PREVIEW_CHAPTERS: TutorialPreviewChapter[] = [
  {
    id: "adventurer",
    label: "모험가의 장",
    shortLabel: "모험가",
    characterSrc: adventurerPoses,
    characterAlt: "모험가 복장을 입은 한짝",
    steps: ADVENTURER_TUTORIAL_STEPS.map((step) => ({ ...step, surface: "battle" })),
  },
  {
    id: "blacksmith",
    label: "대장장이의 장",
    shortLabel: "대장장이",
    characterSrc: blacksmithPoses,
    characterAlt: "대장장이 복장을 입은 한짝",
    steps: [
      { id: "blacksmith-welcome", surface: "battle", target: '.cozy-growth-nav button[data-route="equipment"]', targetLabel: "장비", advanceMode: "target", message: "이제 장비 쪽을 확인해 볼까? 아래 장비 버튼을 직접 눌러 봐!" },
      { id: "blacksmith-select", surface: "equipment", target: ".equipment-slot-weapon", targetLabel: "무기 선택", advanceMode: "target", message: "좋아, 장비 창이 열렸어! 먼저 오른쪽 아래의 무기를 눌러 강화할 장비를 골라 보자." },
      { id: "blacksmith-supplies", surface: "equipment", target: ".equipment-action-rice", targetLabel: "연습용 재료", message: "연습용으로 2,000쌀과 이번 강화에 필요한 재료를 준비해 뒀어. 실제 보유량과 필요량은 여기서 비교하면 돼." },
      { id: "blacksmith-enhance", surface: "equipment", target: ".equipment-enhance-button", targetLabel: "직접 강화", advanceMode: "target", message: "준비 완료! 이제 강화 버튼을 직접 눌러서 무기를 한 단계 올려 보자." },
      { id: "blacksmith-finish", surface: "equipment", target: ".equipment-upgrade-preview", targetLabel: "강화 결과", message: "성공이야! 현재 장비와 다음 성장 수치를 비교하는 법도 익혔네. 앞으로도 재료와 쌀을 확인하고 강화하면 돼!" },
    ],
  },
  {
    id: "skill-trainer",
    label: "비법 수련가의 장",
    shortLabel: "비법 수련가",
    characterSrc: skillTrainerPoses,
    characterAlt: "비법 수련가 복장을 입은 한짝",
    steps: [
      { id: "skill-welcome", surface: "skills", target: ".skills-heading", targetLabel: "비법 수련", message: "비법서를 얻었구나! 스킬 창을 열었어. 여기서 새 스킬을 해금하고 성장시킬 수 있어." },
      { id: "skill-slots", surface: "skills", target: ".skill-loadout", targetLabel: "자동 사용 순서", message: "액티브 스킬은 이곳에 장착한 순서대로 자동 사용돼. 빈 칸을 눌러 순서를 정해 보자." },
      { id: "skill-passive", surface: "skills", target: ".skill-growth-grid", targetLabel: "스킬 목록", message: "스킬 카드에서 액티브와 패시브 효과를 확인할 수 있어. 패시브는 장착하지 않아도 늘 적용돼!" },
      { id: "skill-finish", surface: "skills", target: ".skill-enhance-panel", targetLabel: "강화·승급", message: "카드를 고르면 아래 칸에 필요한 비법서와 쌀이 나와. 다 모았다면 여기서 강화하거나 승급해 줘." },
    ],
  },
  {
    id: "dungeon-knight",
    label: "던전 기사의 장",
    shortLabel: "던전 기사",
    characterSrc: dungeonKnightPoses,
    characterAlt: "던전 기사 복장을 입은 한짝",
    steps: [
      { id: "dungeon-welcome", surface: "dungeon", target: ".dungeon-bookmarks", targetLabel: "오늘의 입장문", message: "오늘은 내가 던전 안내 기사야! 세 입장문 가운데 빛나는 문이 오늘 도전할 수 있는 던전이야." },
      { id: "dungeon-ticket", surface: "dungeon", target: ".dungeon-book-status", targetLabel: "입장권·단계", message: "도전 전에는 남은 입장권과 현재 도전 단계를 꼭 확인해 줘." },
      { id: "dungeon-boss", surface: "dungeon", target: ".dungeon-keyart-wrap", targetLabel: "오늘의 보스", message: "우동은 생존형, 메추리알 장조림은 폭주형, 도토리묵은 장갑형 보스야." },
      { id: "dungeon-finish", surface: "dungeon", target: ".dungeon-book-actions", targetLabel: "보상·도전", message: "예상 보상을 확인하고 도전 시작! 이미 이긴 단계는 입장권 한 장으로 소탕할 수 있어." },
    ],
  },
  {
    id: "gem-wizard",
    label: "보석 마법사의 장",
    shortLabel: "보석 마법사",
    characterSrc: gemWizardPoses,
    characterAlt: "보석 마법사 복장을 입은 한짝",
    steps: [
      { id: "gem-welcome", surface: "gems", target: ".gems-heading", targetLabel: "보석함", message: "반짝이는 보석함을 얻었네! 보석 창을 열었어. 보유 보석함과 보석을 여기서 관리해." },
      { id: "gem-equip", surface: "gems", target: '[data-gem-id="tutorial-equip-gem"]', targetLabel: "연습 보석 장착", advanceMode: "target", message: "연습용 보석을 준비했어. 오른쪽 보유 보석에서 직접 눌러 첫 슬롯에 장착해 봐!" },
      { id: "gem-save", surface: "gems", target: ".gem-save-button", targetLabel: "프리셋 저장", advanceMode: "target", message: "좋아! 이제 프리셋 저장을 눌러 방금 만든 조합을 보관하자." },
      { id: "gem-fusion-tab", surface: "gems", target: ".gem-management-tabs button:nth-of-type(2)", targetLabel: "합성 탭", advanceMode: "target", message: "이번에는 남는 보석을 합쳐 볼까? 위의 합성 탭을 직접 눌러 줘." },
      { id: "gem-fusion-one", surface: "gems-fusion", target: '[data-gem-id="tutorial-fusion-gem-1"]', targetLabel: "첫 번째 보석", advanceMode: "target", message: "같은 2레벨 보석 세 개를 차례로 고를 거야. 첫 번째 보석을 눌러 봐!" },
      { id: "gem-fusion-two", surface: "gems-fusion", target: '[data-gem-id="tutorial-fusion-gem-2"]', targetLabel: "두 번째 보석", advanceMode: "target", message: "잘했어. 같은 레벨의 두 번째 보석도 골라 줘." },
      { id: "gem-fusion-three", surface: "gems-fusion", target: '[data-gem-id="tutorial-fusion-gem-3"]', targetLabel: "세 번째 보석", advanceMode: "target", message: "마지막 세 번째 보석까지 선택하면 합성 준비 완료야!" },
      { id: "gem-fuse", surface: "gems-fusion", target: ".gem-fuse-button", targetLabel: "직접 합성", advanceMode: "target", message: "준비됐어. 합성 버튼을 직접 눌러 한 단계 높은 보석을 만들어 보자." },
      { id: "gem-result", surface: "gems-fusion", target: ".gem-reward-popover button", targetLabel: "합성 결과 확인", advanceMode: "target", message: "3레벨 보석이 완성됐어! 결과를 확인하고 확인 버튼을 직접 눌러 줘." },
      { id: "gem-finish", surface: "gems-fusion", target: ".gem-management-window", targetLabel: "보석 관리", message: "성공! 2레벨 보석 세 개가 3레벨 보석 하나로 합쳐졌어. 아껴 둘 보석은 잠금도 활용해 줘!" },
    ],
  },
  {
    id: "stylist",
    label: "스타일리스트의 장",
    shortLabel: "스타일리스트",
    characterSrc: stylistPoses,
    characterAlt: "스타일리스트 복장을 입은 한짝",
    steps: [
      { id: "style-welcome", surface: "cosmetics", target: ".cosmetic-screen-heading", targetLabel: "치장 뽑기", message: "새로운 옷이 열렸어! 치장 뽑기에서 한짝만의 멋진 모습을 찾아보자." },
      { id: "style-draw", surface: "cosmetics", target: ".cosmetic-cost-buttons button:first-child", targetLabel: "1회 뽑기", advanceMode: "target", message: "연습용 뽑기권 한 장을 준비했어. 1회 뽑기를 직접 눌러 새 치장을 받아 보자!" },
      { id: "style-result", surface: "cosmetics", target: ".cosmetic-result-panel", targetLabel: "뽑기 결과", message: "뽑기 성공! 새로 얻은 치장은 바로 옷장에서 입어 볼 수 있어." },
      { id: "style-wear", surface: "character-cosmetics", target: '[data-cosmetic-id="preview-2-head"]', targetLabel: "붕어빵 모자 착용", advanceMode: "target", message: "붕어빵 모자를 직접 눌러 한짝에게 입혀 봐. 선택하는 순간 모습이 바뀔 거야!" },
      { id: "style-finish", surface: "character-cosmetics", target: ".costume-preview-pane", targetLabel: "착용 결과", message: "정말 잘 어울린다! 여기서 부위별 치장을 갈아입고, 도감에서는 중복 등록과 세트 효과를 확인할 수 있어." },
    ],
  },
  {
    id: "market-merchant",
    label: "장터 상인의 장",
    shortLabel: "장터 상인",
    characterSrc: marketMerchantPoses,
    characterAlt: "장터 상인 복장을 입은 한짝",
    steps: [
      { id: "market-welcome", surface: "market-buy", target: ".market-plaza-heading", targetLabel: "한짝 장터", message: "필요한 물건을 찾고 있구나! 거래소를 열었어. 먼저 거래할 품목을 골라 보자." },
      { id: "market-buy", surface: "market-buy", target: ".market-plaza-trade", targetLabel: "구매 주문", message: "구매할 때는 현재 가격과 원하는 수량을 확인하고 최대 단가를 직접 정할 수 있어." },
      { id: "market-sell", surface: "market-sell", target: ".market-plaza-side-toggle", targetLabel: "구매·판매", message: "판매로 바꾸면 가진 수량과 가격을 정해 주문을 등록할 수 있어. 체결 전에는 취소도 가능해." },
      { id: "market-finish", surface: "market-history", target: ".market-plaza-ledger", targetLabel: "주문·수령·정산", message: "내 주문, 물품 수령함, 판매 정산과 체결 내역까지 확인해야 진짜 거래 완료야!" },
    ],
  },
  {
    id: "village-guide",
    label: "번외: 마을 안내",
    shortLabel: "마을 안내",
    characterSrc: adventurerPoses,
    characterAlt: "마을 안내를 맡은 모험가 한짝",
    steps: [
      { id: "village-welcome", surface: "battle", target: ".cozy-utility-nav", targetLabel: "마을 메뉴", message: "모험 중 궁금한 기능이 생겼어? 상단의 마을 메뉴와 편의 기능도 짧게 둘러보자!" },
      { id: "village-social", surface: "ranking", target: ".ranking-list-panel", targetLabel: "모험가 랭킹", message: "랭킹에서는 다른 모험가의 성장 기록을 볼 수 있어. 광장 채팅에서는 이야기도 나눌 수 있지." },
      { id: "village-pip", surface: "battle", target: ".picture-in-picture-shell", targetLabel: "화면 한켠", message: "화면 한켠 기능을 켜면 작은 창에서도 한짝의 모험을 계속 지켜볼 수 있어." },
      { id: "village-finish", surface: "settings", target: ".profile-settings-panel", targetLabel: "환경 설정", message: "소리와 화면, 알림 같은 설정은 마이페이지에서 바꿀 수 있어. 마을 안내는 여기까지야!" },
    ],
  },
];
