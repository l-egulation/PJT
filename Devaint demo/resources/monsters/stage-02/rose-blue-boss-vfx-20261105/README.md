# Rose·Blue 보스 VFX (다크 판타지, 2026-11-05 확정)

2스테이지 Rose·Blue 보스전의 투사체·전조·폭발·속박·압박 이펙트다. 사용자 검토를 거쳐 **다크 판타지 색**으로 확정했다. 게임은 `runtime/`만 쓴다.

## 폴더

| 경로 | 내용 |
|---|---|
| `runtime/*.png` | 게임용 시트 37종과 백색화 덮개 4종(`*-white`). 32px = 1u, 셀은 왼쪽 위부터 행 우선. 회오리 곁들임 네 장(3P5 두 장·2P4 Blue 두 장)만 반 해상도이고 `sheets.json`의 `scale`(2)로 2배로 그린다. |
| `runtime/sheets.json` | 시트마다 열·행·프레임 수, 셀 크기, 피벗(셀 왼쪽 위 기준 px), 프레임 시간(ms), 반복 여부. 메인의 Unity 바인딩이 이 값을 따른다. |
| `source/*.png`, `source/metadata.json` | 빛 화풍 원본 시트. 다크 판타지는 이것을 다시 칠한 결과다. |
| `source/inputs/` | 다른 작업에서 가져온 입력: Rose 화살 명중·화살비 착지(`rose-blue-projectiles` v1), 3페이즈 화살 명중(`rose-blue-phase3-arrow` v1), 확정 교환탄 충돌 섬광 v4(`exchange-flash-v4.json`에 원래 메타데이터). |
| `source/pixelfx/` | pixelfx(MIT)로 그린 효과 코드와 1배 격자 시트·Aseprite JSON. 2페이즈 패턴 4 압박 벽(`press-rose.js`, `press-blue.js`)과 3페이즈 유도 화살(`homing-bolt.js`)·전조 맺힘(`homing-form.js`)·열 폭발 기둥(`p3-pillar.js`)·화살 회오리 곁들임(`aoe-whirl.js`, 뒤·앞 두 장)과 2P4 양옆 압박 Blue 잉크 회오리(`ink-whirl.js`, 같은 코드를 압박 크기·Blue 색·한 덩어리 띠 옵션으로). 기둥만 빛 화풍으로 그려 다시 칠하고, 나머지는 처음부터 다크 판타지 색이다. |
| `source/tools/` | 빛 화풍 생성기와 `darkfantasy.mjs`(다시 칠하기), `export_runtime.mjs`(게임용 세트 출력). |
| `preview/stage2_all_patterns.html` | 2스테이지 전 패턴 시뮬레이션(그림 내장). 이펙트 버전(다크 판타지·이전 빛 화풍)과 저격 적중 시안(A·B·C)을 바꿔 볼 수 있다. |
| `preview/compare-dark-fantasy.html` | 빛 화풍 ↔ 다크 판타지 시트 비교. |

## 게임용 시트

| 시트 | 쓰는 곳 |
|---|---|
| `rose-arrow-flight` (+`-white`) | Rose 화살 전부(1P1 3연사, 2P1 스턴 화살, 2P2 고속 유도 단발), 2P3 교환탄(2배) |
| `rose-rain-arrow-flight`, `rose-rain-impact` | 1P3 화살비(발판 위 무작위 낙하)와 착지 |
| `rose-arrow-impact` | Rose 화살 명중(2배) |
| `snipe-hit` | 1P2 추적 저격 적중(시안 B 깨진 고리 확정) |
| `blue-orb-trail`, `blue-orb-body` (+`-white`) | 1P1 Blue 유도 구체, 2P3 Blue 교환 구체 |
| `orb-hit` | 1P1 구체가 기계에 닿을 때 |
| `trap-warn`, `trap-snap` | 1P2 함정 전조(발판당 하나), 사슬 속박(사슬 네 줄이 가운데 봉인 자물쇠로 모임) |
| `dive-root` | 함정·급강하에 붙잡힌 동안 발목 고리 |
| `machine-warn`, `machine-ink-blast`, `rain-warn` | 1P3 기계 발판 전조, 구역이 터지는 잉크 폭발, 나머지 발판 화살비 전조 |
| `sequence-warn-rose/blue`, `sequence-blast-rose/blue` | 1P4 순서 번호 홀수 Rose·짝수 Blue 전조와 폭발(Rose는 화살 베기·장미 개화, Blue는 고리 폭발) |
| `ink-charge`, `ink-fireball`, `ink-blast` | 2P1 Blue 차징, 잉크 파이어볼(2.5u), 4×4u 폭발 |
| `blue-vanish` | 2P2 급강하 전·착지 후 Blue 사라짐 |
| `dive-trail`, `dive-impact` | 2P2 급강하 잔상과 착지 파열 |
| `exchange-flash` | 2P3 교환탄 충돌 섬광(1.5배) |
| `ink-whirl-back`, `ink-whirl-front` | 2P4 양옆 압박 Blue 쪽 잉크 회오리(2026-11-05 확정). 3P5 회오리 곁들임을 반 폭 6.09u·높이 7.03u, 0.5초 전조·0.8초 확장·1.4초 끝, 남색→파랑(보라·빨강 없음), 띠 22가닥 한 덩어리로 그린 것. 도는 물체는 없다. 영역이 벽에서 넓어지는 동안 Unity가 피벗(영역 가운데 바닥)을 영역 가운데로 옮긴다. Rose 쪽 화살 영역은 `rose-rain-arrow-flight`·`rose-rain-impact`를 Unity 코드가 배치한다 |
| `press-rose`, `press-blue` | 이전 2P4 양옆 압박(Rose 가시 장미 덩굴 벽, Blue 잉크 해일). 2026-11-05에 화살 영역·잉크 회오리로 바뀌어 게임은 쓰지 않는다(원본 보존). 전조 0.5초·확장 0.8초·유지·사라짐 2.0초, 30fps 61프레임, 6열 격자. Blue는 좌우를 뒤집어 오른쪽 벽에 둔다. |
| `p3-arrow-flight` (+`-white`), `p3-arrow-impact`, `p3-blast` | 3페이즈 화살(튕김·부채꼴·순차 낙하·광역기 회오리), 명중(2배), 튕기는 화살 지연 폭발 |
| `homing-bolt`, `homing-form` | 3P1 유도 화살(보라 단색, 미늘 촉·가는 대·꼬리 깃, 판정 3×1u 중심 피벗, 반복)과 0.5초 전조 맺힘(조준 방향으로 돌려 그림). 비행 중에는 0.05·0.1·0.15초 전 자리에 같은 화살을 옅게(0.35·0.2·0.1) 잔상으로 겹친다(Unity 코드) |
| `p3-pillar` | 3P4 순차 낙하 화살의 열 폭발(폭 2.5u × 전체 높이 한 줄기 기둥, 피벗 = 박힌 화살 바닥) |
| `aoe-whirl-back`, `aoe-whirl-front` | 3P5 확장 광역기 화살 회오리의 곁들임(잉크 바람 띠·바닥 소용돌이·빨려 오르는 입자·불티). 전조 시작부터 2.8초, 뒤 장은 보스 뒤·앞 장은 플레이어 위. 회오리 화살 60발은 `p3-arrow-flight`를 Unity 코드가 돌린다 |

3페이즈 패턴 3(거대 석궁 낙하)은 2026-11-05 사용자 결정으로 패턴째 없앴다.

## 다크 판타지 다시 칠하기

`source/tools/darkfantasy.mjs`: 색 계열을 나눠 5단 어두운 팔레트로 다시 칠한다(장밋빛 → 핏빛 와인, 파랑 → 심연 남색, 흰색·보라 → 공허 보라). 흰 심은 아주 밝은 곳만 남기고, 어두운 반투명은 25% 더 진하게, 바깥에 검붉은 그림자 테두리(기본 2·4px, 1px로 그린 화살·저격 적중은 1·2px)를 두른다.

## 다시 만들기

이 폴더에서:

```sh
node source/tools/export_runtime.mjs
```

빛 화풍 원본을 다시 그릴 때는 `source/tools/`의 생성기를 `source/`에서 실행한다(`node tools/gen.mjs` → `arrow.mjs` → `orb.mjs` → `p3arrow.mjs` → `burst.mjs` → `trap.mjs` → `platform.mjs` → `snipe.mjs` → `inkball.mjs`; gen이 `metadata.json`을 새로 쓰므로 이 순서). 압박 벽·3페이즈 효과는 pixelfx 저장소의 `src/fx/roseblue/`에 `source/pixelfx/*.js`를 두고 `node tools/render.mjs roseblue/press-rose roseblue/press-blue roseblue/homing-bolt roseblue/homing-form roseblue/p3-pillar --sheet --bg none`, 회오리 곁들임은 `node tools/render.mjs roseblue/aoe-whirl --sheet --bg none --opts '{"layer":"back"}'`(앞 장은 `"front"`, 2P4 Blue는 `roseblue/ink-whirl`로 같은 방법)로 다시 만들어 `source/pixelfx/`에 같은 이름으로 둔다(`p3-pillar.png`는 다시 칠하기 전 빛 화풍 원본).
