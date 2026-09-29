import shopFruit from "./assets-chapter-select-v1/shops/chapter-01-fruit-shop.png";
import shopDrink from "./assets-chapter-select-v1/shops/chapter-02-drink.png";
import shopCookie from "./assets-chapter-select-v1/shops/chapter-03-cookie.png";
import shopSushi from "./assets-chapter-select-v1/shops/chapter-04-sushi.png";
import shopJeon from "./assets-chapter-select-v1/shops/chapter-05-jeon.png";
import shopFastFood from "./assets-chapter-select-v1/shops/chapter-06-fast-food.png";
import shopChinese from "./assets-chapter-select-v1/shops/chapter-07-chinese-restaurant.png";
import shopBaseball from "./assets-chapter-select-v1/shops/chapter-08-baseball-food.png";
import shopSeongsimdang from "./assets-chapter-select-v1/shops/chapter-09-seongsimdang.png";
import shopSsafy from "./assets-chapter-select-v1/shops/chapter-10-ssafy-main-entrance.png";
import shopLocked from "./assets-chapter-select-v1/shops/chapter-locked-shop.png";
import selectedFruit from "./assets-chapter-select-v1/shops-selected/chapter-01-fruit-shop-selected.png";
import selectedDrink from "./assets-chapter-select-v1/shops-selected/chapter-02-drink-selected.png";
import selectedCookie from "./assets-chapter-select-v1/shops-selected/chapter-03-cookie-selected.png";
import selectedSushi from "./assets-chapter-select-v1/shops-selected/chapter-04-sushi-selected.png";
import selectedJeon from "./assets-chapter-select-v1/shops-selected/chapter-05-jeon-selected.png";
import selectedFastFood from "./assets-chapter-select-v1/shops-selected/chapter-06-fast-food-selected.png";
import selectedChinese from "./assets-chapter-select-v1/shops-selected/chapter-07-chinese-restaurant-selected.png";
import selectedBaseball from "./assets-chapter-select-v1/shops-selected/chapter-08-baseball-food-selected.png";
import selectedSeongsimdang from "./assets-chapter-select-v1/shops-selected/chapter-09-seongsimdang-selected.png";
import selectedSsafy from "./assets-chapter-select-v1/shops-selected/chapter-10-ssafy-main-entrance-selected.png";
import selectedLocked from "./assets-chapter-select-v1/shops-selected/chapter-locked-shop-selected.png";

export type ChapterEntry = {
  chapter: number;
  name: string;
  /** 이 챕터를 돌 만한 레벨. 스테이지 설계서의 적 레벨 구간을 그대로 쓴다. */
  levelRange: string | null;
  /** 그림도 설계서도 아직 없는 칸. 단추가 "준비 중"이 된다. */
  comingSoon?: boolean;
  art: string;
  selectedArt: string;
};

/*
 * 상가 배경은 640x360 한 장이고, 가게 열 자리는 그림 쪽에서 정해 준
 * `assets-chapter-select-v1/arcade-layout.json` 을 그대로 옮긴 값이다. 픽셀이
 * 아니라 비율로 두어야 창 크기가 달라져도 가게가 벽면 칸에 정확히 앉는다.
 */
const ARCADE_CANVAS = { width: 640, height: 360 };
const ARCADE_SHOP = { width: 112, height: 108 };
const ARCADE_COLUMNS = [40, 151, 262, 374, 492];
const ARCADE_ROWS = { upper: 63, lower: 177 };

export const CHAPTER_SHOP_WIDTH_PERCENT = (ARCADE_SHOP.width / ARCADE_CANVAS.width) * 100;
export const CHAPTER_SHOP_HEIGHT_PERCENT = (ARCADE_SHOP.height / ARCADE_CANVAS.height) * 100;

export function chapterShopPlacement(chapter: number): { left: number; top: number } {
  const index = chapter - 1;
  const upper = index >= 5;
  const column = ARCADE_COLUMNS[upper ? index - 5 : index] ?? ARCADE_COLUMNS[0];
  const row = upper ? ARCADE_ROWS.upper : ARCADE_ROWS.lower;
  return { left: (column / ARCADE_CANVAS.width) * 100, top: (row / ARCADE_CANVAS.height) * 100 };
}

/*
 * 2026-09-15 챕터 순서 개편. 가게 그림은 그대로고 어느 챕터가 어느 가게인지만
 * 바뀌었다. 난이도와 권장 레벨은 가게가 아니라 챕터 번호에 붙어 있어서, 성심당이
 * 1챕터에서 9챕터로 가면 그만큼 센 구간을 맡는다.
 *
 * 권장 레벨은 스테이지 콘텐츠의 `enemyLevel` 구간을 그대로 옮긴 값이다. 기준 레벨이
 * 글로벌 인덱스마다 `2i - 1` 이라 챕터 c 는 `20c - 19` 부터 `20c - 1` 까지다.
 * 6챕터 패스트푸드는 가게 그림만 있고 몹·보스 에셋이 아직 없어 "준비 중"으로 둔다.
 */
export const CHAPTERS: ChapterEntry[] = [
  { chapter: 1, name: "과일가게", levelRange: "1~19", art: shopFruit, selectedArt: selectedFruit },
  { chapter: 2, name: "음료", levelRange: "21~39", art: shopDrink, selectedArt: selectedDrink },
  { chapter: 3, name: "쿠키", levelRange: "41~59", art: shopCookie, selectedArt: selectedCookie },
  { chapter: 4, name: "스시", levelRange: "61~79", art: shopSushi, selectedArt: selectedSushi },
  { chapter: 5, name: "전", levelRange: "81~99", art: shopJeon, selectedArt: selectedJeon },
  { chapter: 6, name: "패스트푸드", levelRange: null, comingSoon: true, art: shopFastFood, selectedArt: selectedFastFood },
  { chapter: 7, name: "중식당", levelRange: "121~139", art: shopChinese, selectedArt: selectedChinese },
  { chapter: 8, name: "야구장 먹거리", levelRange: "141~159", art: shopBaseball, selectedArt: selectedBaseball },
  { chapter: 9, name: "성심당", levelRange: "161~179", art: shopSeongsimdang, selectedArt: selectedSeongsimdang },
  { chapter: 10, name: "싸피", levelRange: "181~199", art: shopSsafy, selectedArt: selectedSsafy },
];

/** 잠긴 챕터나 그림이 없는 챕터가 쓰는 공용 가게. */
export const LOCKED_CHAPTER_ART = { art: shopLocked, selectedArt: selectedLocked };

/** `stage.01-07` 의 앞 두 자리가 챕터다. */
export function chapterOfStage(stageId: string): number {
  return Number(stageId.replace("stage.", "").split("-")[0]) || 0;
}
