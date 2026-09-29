---
doc_kind: task
owner_domain: delivery
task_code: K-22
planning_status: 완료
development_status: 완료
verification_status: 부분 검증
source: repository-implementation-sync
snapshot_date: '2026-09-14'
---

# K-22 캐릭터 창 풀스택 구현

## 완료 기준

[승인 설계](../../../../70-plans/character-window/design.md)의 창·능력치 출처·치장·도감 사용자 흐름을 서버와 연결하고 검증 증거를 남긴다.

## 정책 정본

- [UX](../../../../30-domain/player/ux/ssot.md)
- [전투](../../../../30-domain/combat/ssot.md)
- [치장](../../../../30-domain/cosmetics/ssot.md)

## 선행 작업

B-01, K-21, J-13의 기존 기반을 사용한다.

## 상태와 증거

합의한 캐릭터 창의 화면 구조와 서버 능력치·치장·도감 연동, 66종 번호형 fallback 카드, 등급별 성급과 generic 세트 효과 표시를 구현했다. 실제 명칭·이미지·외형 렌더러와 실DB·실서버 인수 검증은 남아 있어 개발 완료·부분 검증으로 관리한다.

### 구현 위치

- [캐릭터 창](../../../../../apps/web/src/features/character/CharacterWindow.tsx), [창 헤더·조회 상태](../../../../../apps/web/src/features/character/CharacterWindowChrome.tsx), [탭 패널](../../../../../apps/web/src/features/character/CosmeticPanels.tsx), [전용 능력치 탭](../../../../../apps/web/src/features/character/CharacterAbilityTab.tsx), [전용 아늑한 픽셀 에셋](../../../../../apps/web/src/features/character/assets-cozy-pixel/README.md), [공용 기본 나무검 캐릭터 프레임](../../../../../apps/web/public/assets/chapters/chapter-01-sushi/hero/rest_01.png), [메인 진입](../../../../../apps/web/src/main.tsx)
- [능력치 API](../../../../../apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsController.kt), [일관된 조회](../../../../../apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsService.kt), [공통 계산](../../../../../apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculator.kt)
- [치장 카탈로그·조회](../../../../../modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/application/CosmeticsService.kt), [멱등 명령·상태 버전](../../../../../modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/application/CosmeticsCommandService.kt)
- [전투 입력 잠금](../../../../../modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt), [영구 효과·공속 시뮬레이션](../../../../../packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt)
- [능력치 계약](../../../../../packages/contracts/character.tsp), [치장 계약](../../../../../packages/contracts/cosmetics.tsp), [생성 OpenAPI](../../../../../packages/contracts/generated/openapi/openapi.yaml)

### 2026-09-07 검증

- 콘텐츠 validator 테스트 3건과 실제 콘텐츠 검증 성공: 66종, 11세트, 등급별 세트 수 `4:3:3:1`, 각 세트 6부위, 번호형 ID, nullable 표시 데이터, 등급별 임계값.
- `corepack pnpm --filter @hanjjak/contracts build` 성공.
- 웹 테스트 30건, typecheck, production build 성공. 번호형 fallback, 이미지 준비 상태, 캐릭터 dialog, 능력치, 치장·도감, 마일스톤·선택 상자 API와 컴포넌트를 포함한다.
- `C:/hanjjak-main` ASCII 경로에서 inventory·cosmetics·game-api·sim-core clean 후 `:modules:cosmetics:test :apps:game-api:test :packages:sim-core:test --no-configuration-cache` 성공.
- [계산 테스트](../../../../../apps/game-api/src/test/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculatorTest.kt): 기본 분류·최종 반올림·특수 보석 중복 제외·상한·일시 버프 제외·generic 치장 효과.
- [전투 회귀](../../../../../packages/sim-core/src/test/kotlin/com/hanjjak/sim/PermanentBonusTest.kt): 영구 공속·기본공격 증가가 액티브 없이 적용되고 공속 소수 간격을 보존한다.
- 실제 PostgreSQL migration, 인증 HTTP, 실제 Chromium E2E와 동시 요청은 이번 완료 게이트에서 실행하지 않았다.

### 이전 Chromium 화면 검증

[브라우저 결과 JSON](../../../../70-plans/character-window/browser-qa.json)은 2026-09-04 API 전체를 테스트 응답으로 대체한 실제 Chromium 검증 12개와 페이지 오류 0개를 기록한다. 2026-09-07의 66종·마일스톤·선택 상자 변경에 대한 실제 브라우저 E2E로 해석하지 않는다.

### 2026-09-08 빈티지 UI 반영과 검증

- 캐릭터 dialog를 현재 인증·주력 선택 화면과 같은 종이 질감, 적갈색 강조선, 주방 배경 계열로 통일했다.
- 능력치 탭은 좌측 기본 한짝 캐릭터와 닉네임·레벨, 우측 합계·기본·추가 능력치로 재배치했다. 치장·도감의 기존 조회·미리보기·착용·해제·성급 명령 구조는 유지하면서 카드와 표 스타일을 같은 화면 언어로 맞췄다.
- 로컬 모의 API로 783×717 좁은 뷰포트에서 dialog 열기, 능력치 표시, 잠긴 치장·도감 탭, 닫기와 반응형 세로 배치를 확인했다.
- `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web test`(27개 파일·65개 테스트), `corepack pnpm --filter @hanjjak/web build`, `git diff --check`를 통과했다.

### 2026-09-09 캐릭터 창 재구성과 로컬 DB 호환

- 현재 인증·주력 선택 화면의 낡은 종이와 적갈색 인장 화풍에 맞춘 전용 투명 WebP 창 프레임을 제작하고, 제목·탭·닫기·조회 상태를 재사용 가능한 헤더 컴포넌트로 분리했다.
- 능력치 영역을 `항목 / 합계 / 기본 / 추가` 한 표로 정리하고, 좌측 캐릭터 초상은 내부 스크롤 중에도 닉네임·레벨과 전신이 보이도록 고정 배치했다.
- 로컬 PostgreSQL의 과거 Flyway V15 충돌로 `equipment_slot_state`가 없는 환경을 재현했다. 조회 전에 실제 테이블 존재 여부를 검사하여 장비 추가 능력치만 제외하고 기본 능력치 스냅샷을 제공하며, 누락 사실은 화면 안내로 표시한다.
- Spring Boot 8081 실서버에서 신규 가입·주력 재료 선택 후 `GET /api/v1/character/stats`가 `200 OK`, 능력치 8개와 장비 누락 안내를 반환함을 확인했다.
- 실제 Chromium 1265×710 뷰포트에서 캐릭터 창 열기, 전용 프레임 렌더링, 캐릭터 전신·닉네임·레벨, 탭 잠금 상태와 능력치 표를 확인했다.
- `corepack pnpm --filter @hanjjak/web typecheck`, 웹 테스트 29개 파일·76개, production build와 `:apps:game-api:compileKotlin`, `git diff --check`를 통과했다.

### 2026-09-09 능력치 탭 기준 시안 일치 작업

- 장식 프레임 중심의 창을 기준 시안과 같은 단일 종이 패널로 교체하고, 제목·세 탭·닫기 버튼과 구분선을 한 줄 헤더로 재배치했다.
- 좌측에 검을 든 한짝 전신, 닉네임·레벨·현재 레벨 경험치 막대를 배치하고, 우측에는 `항목 / 합계 / 레벨 / 장비` 기본 능력치 표와 전투·속도 효과 상세 능력치를 배치했다. 좁은 화면에서는 같은 정보 순서를 유지한 채 세로로 재배치한다.
- 경험치 표시에 필요한 누적 경험치를 캐릭터 능력치 응답과 TypeSpec·OpenAPI 계약에 추가했다. 능력치 합계와 출처 기여는 기존처럼 서버 응답을 표시하며 클라이언트에서 전투 값을 재계산하지 않는다.
- 인증 화면 캐릭터의 형태와 색을 유지한 검 장착 전용 투명 WebP 원화를 제작해 적용했다.
- 모의 API를 사용한 실제 브라우저에서 dialog 열기, 제목·탭·닫기, 캐릭터·경험치, 기본 능력치 표와 상세 능력치 렌더링을 확인했다. 웹 테스트 29개 파일·76개와 production build, TypeSpec 계약 build, game-api Kotlin compile을 통과했다.
- 후속 시각 검수에서 기존 능력치 패널을 전용 `CharacterAbilityTab` 컴포넌트로 교체했다. 사용자 제공 기본 `rest_01` 원화는 투명 여백을 정리하고 비율을 유지한 192×192 PNG로 적용했으며, 넓은 화면의 창 크기와 내부 비율을 기준 시안의 화면 점유율에 맞췄다.

### 2026-09-09 아늑한 픽셀 UI 전환

- 최종 참고 시안의 밝은 크림 종이, 굵은 갈색 외곽선, 코랄 강조색, 둥근 능력치 행과 픽셀 아이콘으로 캐릭터 창을 다시 구성했다. 제목·세 탭·닫기, 왼쪽 캐릭터·레벨·경험치, 오른쪽 기본·상세 능력치의 기존 정보 구조와 서버 응답 표시 방식은 유지했다.
- 캐릭터 창 전용 이미지는 `apps/web/src/features/character/assets-cozy-pixel` 새 경로에서만 관리한다. 생성한 종이 질감, 새싹·하트·검·명중·신발 아이콘과 사용자 제공 원본에서 만든 192×192 기본 자세 이미지를 이 경로로 옮기고, 이전 캐릭터 프레임과 루트 캐릭터 이미지는 제거했다.
- 1280×720 실제 Chromium 미리보기에서 패널 전체, 캐릭터, 경험치, 세 기본 능력치와 상세 능력치가 한 화면에 잘리지 않고 표시되는 것을 확인했다.
- `corepack pnpm --filter @hanjjak/web typecheck`, 웹 테스트 30개 파일·77개, production build와 `git diff --check`를 통과했다.
- 후속 비율 검수에서 모달을 뷰포트 약 72%×76%, 캐릭터 열을 30%로 제한하고 192×192 원본을 192–268 CSS px 범위에서 픽셀 보간 없이 표시하도록 조정했다. 밝은 종이 프레임, 캐릭터 배경·바닥 그림자, 체크·강조·닫기 표식을 전용 에셋 경계 안에 조합했다.
- 기준 시안과의 재비교 후 둥근 `Jua` 한글 글꼴을 로컬 포함하고 제목·탭·섹션·수치·EXP 크기를 확대했다. 능력치 아이콘은 최신 고해상도 스프라이트의 실제 알파 영역으로 다시 분리하고, 코랄·노랑 강조선과 전용 경험치 막대를 적용해 작은 임시 아이콘과 낮은 대비 문제를 해소했다.
- 최종 피드백에 따라 하트·검·명중·신발을 굵은 코코아색 외곽선의 96px 전용 픽셀 에셋으로 다시 제작했다. 제목에는 프로필 v2의 잎사귀 에셋을 적용하고, 섹션 제목 옆 구분선과 핵심 능력치 설명문·호버 출처 팝업을 제거했다. 왼쪽 경험치 묶음은 레벨 바로 아래로 당겨 정보 밀도를 맞췄다.
- 최종 상태에서 웹 typecheck, 테스트 30개 파일·77개, production build와 `git diff --check`를 통과했고 1280×720 Chromium 캡처로 배치와 아이콘 표시를 재확인했다.
- 왼쪽 영역 후속 시안에 맞춰 제목 새싹과 코랄 강조 표식을 확대하고, 캐릭터 무대는 픽셀 수를 줄인 3:4 안뜰 배경으로 교체했다. 캐릭터·닉네임·레벨 사이 간격을 다시 맞추고 경험치 정보는 `EXP / 막대 / 현재·필요 경험치` 구조로 재배치했으며 별도 퍼센트 문구는 제거했다.
- 왼쪽 영역 조정 이후에도 웹 typecheck, 테스트 30개 파일·77개와 production build를 통과했으며 `git diff --check`에서 변경 파일의 공백 오류가 없음을 확인했다.
- 기본 능력치의 `합계 / 레벨 / 장비` 열 사이에 명시적인 가로 간격을 추가했다. 왼쪽 닉네임·레벨·EXP는 굵기를 높이고 닉네임을 확대했으며, 현재·필요 경험치는 크기를 줄여 경험치 막대 오른쪽 끝에 맞췄다.
- 닫기 버튼을 능력치·치장·도감 탭의 수직 중앙선에 맞춰 올렸다. 캐릭터 정보 열의 남는 높이 자동 분배를 제거하고 닉네임·레벨·경험치 위쪽 여백을 줄여 한 묶음으로 보이게 조정했다.
- 기준 시안과의 전체 비교에 따라 모달 폭을 넓히고 본문을 아래로 이동했다. 먹색과 코랄 색의 대비를 높이고 Jua 글꼴 두께를 강화했으며, 기본 능력치 행은 1·3행 베이지와 2행 밝은 크림이 교차하도록 구성했다.
- 사용자 제공 `젓키 레퍼런스.pptx` 내부 원본 이미지를 직접 확인해 팔레트를 보정했다. 종이 바탕은 기존 값과 차이가 작아 유지하고 먹색 `#22150F`, 코랄 `#EE304B`, 청색 `#02B1FE`를 기준으로 텍스트와 경험치 색을 조정했다. 1·3행은 옅은 웜 베이지, 2행은 종이색이 그대로 드러나게 했으며 캐릭터 아래 닉네임·레벨·경험치 간격은 원본 크롭에 맞춰 확대했다.
- 기본 능력치 강조 표식은 최대 HP와 명중에서 수치 왼쪽, 공격력에서 수치 오른쪽에 표시하도록 행별 위치를 분리했다.
- 캐릭터 아래 `한짝 / LV. 24 / EXP` 정보 사이의 세로 여백을 다시 줄여 하나의 상태 묶음으로 보이게 조정했다.
- 색상이 탁해 보이던 원인이었던 캐릭터 무대의 78% 투명도, 저명도 경험치 필터, 반투명 탭·능력치 행·상세 제목 배경과 50% 갈색 모달 배경막을 보정했다. 종이 질감은 유지하면서 무대 채도와 밝기를 높이고 코랄·청색·교차 행 배경을 불투명한 팔레트로 바꿔 기준 시안 수준의 대비를 확보했다.
- 메인 앱의 캐릭터 버튼은 기존 `CharacterWindow`를 열고, 창이 열린 동안 인증 쿠키를 포함해 `GET /api/v1/character/stats`를 조회한다. 응답의 닉네임·레벨·누적 경험치·치장 해금 여부·능력치 출처를 새 능력치 UI에 그대로 전달하는 계약 테스트를 추가했다. 독립 디자인 미리보기는 고정 시각 데이터이며 실제 게임 진입 경로와 구분한다.
- 로컬 로그인·주력 선택 미리보기 API에 같은 캐릭터 능력치 계약을 제공해 `GET /api/v1/character/stats`의 `200 OK`를 확인했다. 실제 메인 앱에서 캐릭터 버튼을 열어 API 응답의 `한짝`, `Lv. 24`, 현재 경험치 `18,720 / 24,000`, HP `3,680`, 공격력 `151`, 명중 `45`와 상세 능력치가 새 모달에 표시되는 것을 Chromium으로 검증했다. 캐릭터 API·창·능력치 테스트 3개 파일·8개와 웹 typecheck를 통과했다.
- 1280×720 Chromium 캡처에서 닫기 버튼과 탭의 중심선, 캐릭터 아래 정보 간격을 확인했고 production build와 `git diff --check`를 통과했다.
- 기존 9-slice 프레임 아래에 반복 종이 배경을 겹쳐 모서리가 사각형으로 채워지던 문제를 제거했다. 기준 시안의 밝은 크림색과 2–3px의 얇고 얕은 찢어진 픽셀 외곽, 아래 양끝의 작은 새싹·담쟁이 장식을 반영한 전체 패널 에셋 `modal-paper-decorated-v2.png`를 새로 만들고, 종이 바깥 픽셀의 알파가 0인지 확인한 뒤 모달 단일 배경으로 적용했다. 배경 확대율은 103%로 제한해 테두리와 가장자리 장식이 잘리지 않도록 했다.
- 후속 화면 검수에서 모달 높이와 상·하단 안쪽 여백을 늘려 헤더와 상세 능력치가 종이 외곽에 닿지 않게 했다. 선택된 탭은 코랄색 테두리와 글자만 유지하고 하단의 별도 빨간 밑줄은 제거했다.
- 헤더의 제목 열을 본문 왼쪽 캐릭터 열과 같은 31% 비율로 맞췄다. 능력치·치장·도감 탭 묶음은 본문의 세로 구분선에서 시작하며, 남은 오른쪽 영역 안에서 이전보다 짧은 폭으로 균등 배치된다.
- 2026-09-10 전투와 캐릭터 창의 기본 무기 외형을 통일했다. 능력치 탭은 이전 192px 노란 막대기 시안 대신 챕터 1 전투가 사용하는 `hero/rest_01.png`를 직접 참조하며, 사용자 제공 `기본나무검.zip`의 갈색 나무검 합성 프레임을 표시한다. 로컬 미리보기에서는 캐릭터 API 서버가 없어 오류 상태까지만 확인했으며, 데이터 성공 상태의 재캡처는 남아 있어 검증 상태를 부분 검증으로 유지한다.
- 2026-09-10 캐릭터 창의 바깥 종이 패널을 장비 창과 같은 최대 폭 `1320px`, 뷰포트 안전 여백 `32px` 기준으로 확대했다. 능력치 탭 내부 요소의 크기와 배치는 유지하고, 상·하단 여백을 맞바꿔 내용 전체를 아래로 `16px` 이동했다.
- 2026-09-10 능력치 본문을 추가로 아래에 배치하고 기본 능력치 표와 상세 능력치 제목 사이의 간격을 넓혔다. 왼쪽 캐릭터 무대는 캐릭터와 텍스트 크기를 유지한 채 배경 영역의 세로 길이만 확대했다.
- 2026-09-11 배포 캐시 보정: 캐릭터 능력치 탭의 기본 자세가 전투와 동일한 `heroFrame("rest", 0)` 경로를 사용하도록 단일화하고, 1년 `immutable` 캐시가 설정된 운영 환경에서도 교체된 나무검 프레임을 즉시 받도록 `basic-wooden-sword-v1-20260911` 버전 쿼리를 적용했다. 정적 렌더 회귀 테스트에서 캐릭터 탭과 전투가 같은 버전 URL을 사용하는지 확인했으며, 웹 전체 Vitest 293개 테스트, TypeScript typecheck와 Vite production build를 통과했다.

### 2026-09-11 치장 보관함 UI 재구성

- 캐릭터 창의 치장 탭을 왼쪽 캐릭터 미리보기·6부위 착용 상태와 오른쪽 부위 필터·보유 치장 카드·적용 액션의 2열 옷장 구조로 재구성했다. 기존 미리보기 선택, 착용·해제, 치장 뽑기 진입과 서버 명령 계약은 유지했다.
- 실제 치장 이미지가 없는 현재 범위에서는 캐릭터·장비의 아늑한 픽셀 에셋을 부위별 fallback으로 재사용한다. 실제 `imageUrl`이 제공되면 같은 카드 프레임 안에서 콘텐츠 이미지가 우선 표시되며, 외형 합성 렌더러가 구현된 것으로 해석하지 않는다.
- `battle-hud-preview.html?character-tab=cosmetics`에 API를 호출하지 않는 8종 정적 치장 데이터를 추가해 선택 상태와 6부위 레이아웃을 반복 검수할 수 있게 했다. 1280×720 Chromium에서 모달 경계, 카드 선택 강조, 미리보기 상태 변경과 하단 착용 액션 노출을 확인했다.
- `pnpm --filter @hanjjak/web test CosmeticPanels.test.tsx CharacterWindow.test.tsx` — 2개 파일, 3개 테스트 통과. `pnpm --filter @hanjjak/web typecheck`와 Vite production build도 성공했다.

### 2026-09-13 치장 보관함 반응형 배포 전 검증

- 보관함 기본 4열 선언이 앞선 좁은 화면 2열 규칙을 CSS 순서상 덮어쓰던 원인을 제거했다. 최종 반응형 규칙을 기본 선언 뒤에 두어 `1080px` 이하에서는 2열을 유지한다.
- 마지막 행을 채우기 위해 4열 기준의 빈 카드를 DOM에 추가하던 처리를 제거했다. 화면 폭과 무관하게 실제 보유 치장만 렌더링하고 CSS Grid가 남은 행을 자연스럽게 배치한다.
- 사용자 요청 범위에 따라 이번 배포 브랜치의 거래소 화면 변경은 모두 `origin/main` 상태로 복원해 제외했다.
- 웹 전체 Vitest 65개 파일·384개 테스트, TypeScript typecheck와 Vite production build가 통과했다. 로컬 정적 미리보기에서 치장 창과 머리 부위 보유 카드 4개의 렌더링도 확인했다. 테스트 중 로컬 API `:3000` 연결 거부 진단 로그는 발생했지만 실패 테스트는 없었다.

### 2026-09-14 접힌 패널 능력치 UI·공통 종이 전환

- `outputs/character-stats-assets-v1`에서 확정한 탭·접힌 능력치 패널·아이콘을 제품 경로 `apps/web/src/features/character/assets-stats-v2`로 옮기고, 능력치 탭을 프로필·최대 HP·공격력·관통력 네 패널 구조로 교체했다. 상단의 `능력치 / 치장 / 도감` 탭도 선택·기본 상태 에셋을 사용한다.
- 캐릭터 응답에 서버 계산 전투력을 포함해 UI가 임의 합산하지 않게 했고, 캐릭터·장비·인벤토리·스킬·거래소·보석 창은 새 1280×720 공통 종이와 닫기 에셋, 1180×650 안전 영역을 공유한다.
- 1280×720 CSS viewport에서 캐릭터·스킬·보석·거래소 창 경계가 `50,35`에서 `1230,685` 안에 있고 내부 세로 스크롤이 생기지 않음을 확인했다. 웹 TypeScript 검사와 관련 5개 파일 15테스트가 통과했다. 실제 운영 API와 전체 치장 탭 회귀는 남아 있어 검증 상태는 유지한다.

### 남은 범위

- 최신 migration을 모두 적용한 빈 PostgreSQL 환경, DB 원자성·동시 요청 및 전체 세션 API 인수 검증은 남아 있다. 과거 로컬 DB의 Flyway V15 버전 충돌 자체를 자동 복구하지는 않는다.
- 실제 치장·세트 명칭, 이미지와 합성 렌더러는 미구현이다. 번호형 정보 카드와 `이미지 준비 중` fallback만 제공한다.
- 미등록 중복 치장 거래소 연결과 상세 감사 기록은 미구현이다.
- 기존 전투는 요청 단위 시뮬레이션이다. 지속 자동전투 세션의 다음 사이클 적용·전체 수명주기 인수를 완료한 것은 아니다.
