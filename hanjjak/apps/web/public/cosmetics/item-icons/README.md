# 치장 부위 아이콘

`art/char/hero-motions-4f/equip/<세트 폴더>/item-icons`의 부위 아이콘을 256×256으로, `frames/rest_01.png` 완성 프레임을 464×336으로 줄여 옮긴 에셋이다. 원본은 수정하지 않았다.

- `<슬러그>-head|top|bottom|gloves|shoes|cape.png`: 부위 아이콘. `bottom`은 원본의 `weapon` 아이콘이며 화면에서 무기로 표시한다.
- `<슬러그>-set.png`: 세트 전신 프리뷰. 완성 프레임이 없는 `bamboo-spear`, `sushi`에는 없다.

세트 슬러그와 콘텐츠 세트 ID의 연결은 [`cosmetic-art.ts`](../../../src/features/cosmetics/cosmetic-art.ts)에 둔다. 콘텐츠가 `imageUrl`을 내려주기 시작하면 그 값이 이 에셋보다 우선한다.

| 세트 ID | 등급 | 슬러그 | 원본 폴더 |
| --- | --- | --- | --- |
| `cosmetic-set-01` | NORMAL | `leather-guard` | 가죽경갑 |
| `cosmetic-set-02` | NORMAL | `scrap-knight` | 고물기사 |
| `cosmetic-set-03` | NORMAL | `bamboo-spear` | 죽창무사 |
| `cosmetic-set-04` | NORMAL | `nurse` | 간호사 |
| `cosmetic-set-05` | RARE | `chef` | 요리사 |
| `cosmetic-set-06` | RARE | `fishbread` | 붕어빵 |
| `cosmetic-set-07` | RARE | `yakgwa` | 약과 |
| `cosmetic-set-08` | EPIC | `sushi` | 스시야 |
| `cosmetic-set-09` | EPIC | `mage` | 마법사 |
| `cosmetic-set-10` | EPIC | `turtle-guardian` | 거북이수호자 |
| `cosmetic-set-11` | LEGENDARY | `angel` | 천사 |

전설 세트 무기는 원본 3종 중 `weapon-feather-sword.png`를 썼다. 원본에 남은 `무지개떡`, `악마`, `청운`, `싸피후드집업`, `기본나무검`, `레전드 무기 4종`은 현재 콘텐츠 세트가 11개뿐이라 아직 쓰지 않는다.
