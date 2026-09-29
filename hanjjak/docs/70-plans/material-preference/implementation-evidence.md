---
doc_kind: reference
owner_domain: specialization
status: partial-verified
verified_at: 2026-09-02
---

# 주력 재료 선택·드롭 구현 증거

규칙 정본은 [주력 재료 SSOT](../../30-domain/character/ssot.md), 아이템·보상 정본은 [아이템 SSOT](../../30-domain/items/ssot.md), working 수치는 [MVP 주력 재료 드롭표 v1](../../60-content/items/material-drops-mvp-v1.md)이다. 이 문서는 구현 위치와 검증 결과만 기록한다.

## 구현 위치

- API 계약: `packages/contracts/material-preference.tsp`, `packages/contracts/main.tsp`
- 선택 도메인·API·영속화: `modules/account`, `apps/game-api/src/main/resources/db/migration/V7__material_preference.sql`
- 게임 API 진입 차단: `modules/account/src/main/kotlin/com/hanjjak/account/api/MaterialSelectionGate.kt`
- 드롭 콘텐츠: `packages/game-content/versions/v1/drops/material-drops.json`, `packages/game-content/schema/material-drops.schema.json`
- 결정론 추첨·서버 지급: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/DropTable.kt`, `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/BattleRewardService.kt`, `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/InventoryRewardService.kt`
- 웹 선택 차단 화면: `apps/web/src/features/material-preference`, `apps/web/src/features/material-preference/assets`, `apps/web/src/features/material-preference/assets-cozy-pixel`, `apps/web/src/shared/assets/onboarding-cozy-pixel`, `apps/web/src/main.tsx`
- 실행 상태: [C-03](../../wiki/06-delivery/tasks/C-professions/c-03-profession-exclusive-production.md), [C-04](../../wiki/06-delivery/tasks/C-professions/c-04-profession-selection-persistence.md), [F-03](../../wiki/06-delivery/tasks/F-items-inventory/f-03-profession-material-drops.md), [F-05](../../wiki/06-delivery/tasks/F-items-inventory/f-05-atomic-reward-idempotency.md), [K-04](../../wiki/06-delivery/tasks/K-client-ux/k-04-auth-first-entry-ui.md)

## 검증된 범위

- 선택 전 조회의 세 선택지와 각 80:10:10 서버 계산 비율
- 감자·고구마·옥수수 최초 선택, 같은 선택 멱등 성공, 다른 선택 409 거절, 잘못된 enum 422 거절
- DB 계정별 기본 키를 통한 선택 하나 보장과 재접속 조회 복원
- 미선택 계정의 전용 선택 API·인증 API 외 `/api/v1/**` 서버 진입 차단
- 일반 적 20마리와 보스의 25회 독립 추첨 기회, 챕터별 누적 세대 가중치와 미해금 세대 제외
- 세대 선택 뒤 80:10:10 계열 경계값, 같은 seed·콘텐츠·입력의 결과 재현
- 전투 보상 결과·인벤토리 수량·스테이지 클리어의 단일 트랜잭션 처리와 같은 전투 key 재시도 중복 방지
- PostgreSQL/Flyway에서 선택 저장, 전투 1회 기록, 인벤토리 합계와 재시도 후 스테이지 클리어 횟수 1을 확인
- 1440×1000 브라우저에서 세 선택지·비율·변경 불가 안내를 확인했다.
- 2026-09-07에는 주방 배경·종이 카드 기반 첫 진입 화면과 감자·고구마·옥수수 투명 이미지 자산을 적용했다. 1280×720에서 신규 계정 생성, 세 선택지와 서버 비율 표시, 고구마 선택 후 다시 선택, 감자 확정, 메인 진입, 새로고침 후 선택 화면 생략을 실제 game-api로 확인했다.
- 2026-09-08에는 `material-board-v2.png`, `material-card-frame-v2.png`, `material-confirm-panel-v2.png`, `material-primary-button-v2.png`, `material-secondary-button-v2.png`, `material-selected-seal-v2.png`를 첫 진입 화면에 적용했다. 모든 자산의 투명 채널을 확인하고 데스크톱·390×844 배치, 카드 선택 인장, 선택 확인 패널과 재선택 동작을 브라우저에서 확인했다.
- 2026-09-09에는 아늑한 픽셀 온보딩 에셋팩의 공통 배경·종이·버튼, 단순화한 세 나라 삽화, 인구 아이콘과 대형 잉크 도장을 적용했다. 나라 이름과 인구를 삽화 위로 옮기고, 이미지 아래에는 서버 제공 재료 비율만 남겼다. 현재 API에 인구가 없으면 `집계 준비 중`을 표시하고 선택 값은 만들지 않는다. 최종 선택 모달의 취소·확정, Escape 닫기, 선택 API 호출 시점을 유지했으며 웹 `typecheck`, 테스트 78개, 프로덕션 빌드와 로컬 모의 API 브라우저 스모크가 통과했다.
- 2026-09-09 기준 이미지와 실제 렌더의 비율 차이를 재검수해 제목·나라 헤더·세 나라 삽화·80:10:10 아이콘·확정 버튼을 확대하고 수직 위치를 보정했다. 선택한 감자 나라의 대형 도장이 이미지 위에 겹치고 비선택 두 나라가 감광되는 것을 1200×703 브라우저에서 다시 확인했다.
- 2026-09-09에는 확대 transform으로 나라 삽화의 시각적 경계가 재료 행을 침범하던 문제를 보정했다. 감자·고구마·옥수수 삽화의 배율과 수직 위치를 개별 조정하고 재료 행의 위치를 내렸으며, 1200×731 브라우저에서 세 나라 모두 삽화와 재료 아이콘이 분리되어 표시되는 것을 확인했다.
- 2026-09-09 사용자 피드백에 따라 재료 아이콘 크기를 줄이고 80%와 10%의 상대 크기 차이는 유지했다. 선택 확정 버튼을 위로 당겼으며 `감자`·`고구마`·`옥수수`에만 국가별 강조색을 적용하고 `의 나라`는 공통 갈색으로 표시되도록 수정했다.
- 2026-09-09에는 나라 선택 3열과 열 내부 재료 아이콘 행의 표시 폭을 각각 줄여 가로 간격을 좁혔다. 재료 행·선택 확정 버튼·버튼 라벨을 단계별로 위로 이동하고 1200×731 브라우저에서 세 묶음의 중앙 정렬과 버튼 라벨 위치를 확인했다.
- 2026-09-09에는 제목 아래 장식선을 제거하고 나라 삽화와 재료 행을 서로 가까이 이동했다. 선택 확정 버튼 라벨은 아래로 되돌려 프레임 중앙에 맞췄으며 1200×731 브라우저에서 세 나라 모두 간격과 정렬을 다시 확인했다.
- 2026-09-09 기준 초안과의 시각 비교에 따라 제목에 얇은 외곽 획을 더하고 검정·빨강·국가별 강조색을 밝게 조정했다. 제목 양옆에는 프로젝트의 픽셀 새싹 에셋을 재사용하고 선택 확정 글자 양옆에는 작은 반짝임을 추가했으며, 버튼 위치를 위로 조정해 화면 하단의 빈 공간을 줄였다.
- 2026-09-08에는 도메인·API 변경 없이 첫 진입 화면의 표현을 국가 선택으로 바꿨다. `감자의 나라`는 `POTATO`, `고구마의 나라`는 `SWEET_POTATO`, `옥수수의 나라`는 `CORN`을 그대로 전송하며 카드에서 대표 재료와 80:10:10 서버 응답을 확인할 수 있다.
- 2026-09-08에는 기존 재료 바구니 그림을 `potato-country-v1.png`, `sweet-potato-country-v1.png`, `corn-country-v1.png`로 교체했다. 각 자산은 투명 RGBA 이미지이며 감자·고구마·옥수수 경작지와 건축물로 국가를 표현한다. 브라우저에서 카드 내부 표시, 선택 인장과 비선택 카드 명도 변화, 고구마의 나라 확정 후 메인 진입을 확인했다.
- 2026-09-08에는 국가별 큰 카드 프레임을 제거하고 국가 이름 명패, 투명 국가 풍경, 대표 재료·획득 비율 명패를 서로 분리해 배치했다. 화면 표기는 `감자 나라`·`고구마 나라`·`옥수수 나라`로 조정했으며 API 매핑과 비율 데이터는 유지했다.
- 2026-09-08에는 대표 재료 표시에 `potato-item-pixel-192.png`, `sweet-potato-item-pixel-192.png`, `corn-item-pixel-192.png`를 적용했다. 세 파일은 실제 192×192 RGBA 투명 PNG이며 CSS 픽셀 렌더링을 사용한다. 국가 선택 묶음의 위치, 이름 명패와 확인 패널 높이, 정보 명패 좌측 항목의 글자 크기도 함께 보정했다.
- 픽셀 재료 아이콘은 정보 명패 오른쪽 고정 영역에 배치해 종횡비가 다른 아이콘도 텍스트 행의 위치에 영향을 주지 않는다. 정보 명패와 좌측 항목 글자는 확대하고 국가 풍경은 축소해 요소 사이 간격을 확보했다.
- 국가 풍경은 `potato-country-v2.png`, `sweet-potato-country-v2.png`, `corn-country-v2.png`로 교체했다. 일반 성과 밭을 축소한 구도 대신 재료 모양의 집·지붕·길을 중심으로 크게 구성해 작은 카드에서도 감자·고구마·옥수수 국가가 구분된다.
- 국가 선택 화면은 `material-country-background-vintage.png`를 사용하는 오래된 동네 주방 배경으로 전환했다. 국가별 큰 카드와 명패 이미지는 화면 구성에서 제거하고, 세 국가 풍경을 타일 벽에 직접 배치한 뒤 재료 아이콘·서버 획득 비율을 하나의 공용 정보 띠에 표시한다. 선택 도장은 CSS 기반 평면 붉은 잉크 표현으로 바꿨으며 기존 API 요청과 80:10:10 데이터 흐름은 변경하지 않았다. 1088×731 브라우저에서 선택 전·감자 선택 후 화면과 재선택·확정 버튼을 확인했고 웹 `typecheck`, 25개 테스트, 프로덕션 빌드가 통과했다.
- 국가 풍경과 설명이 겹치지 않도록 공용 정보 띠를 불투명한 상위 레이어로 고정하고, 확인 영역은 같은 종이 표면·폭·구분선을 사용해 바로 아래에 연결했다. 선택 여부에 따른 국가 풍경 감광은 유지하면서 정보와 조작부의 가독성은 영향을 받지 않는다.
- 국가 선택 제목의 임시 CSS 선 아이콘을 `material-vintage-bowl-stamp.png` 실제 투명 PNG 도장으로 교체했다. 인증 화면의 그릇 도장과 같은 시각 언어를 사용하며 브라우저에서 제목과 밑줄 사이의 크기·간격을 확인했다.
- 국가별 하단 정보 영역은 불투명 종이 면과 세로 구분선을 제거해 조리대 배경이 그대로 보이도록 했다. `대표 재료`·`획득 비율` 라벨 대신 192×192 픽셀 재료 아이콘과 서버가 내려준 주력·보조 재료의 80:10:10 퍼센트를 국가별 중앙에 표시한다.
- 제목 아래의 중복 변경 불가 문구는 제거하고, 국가를 고른 뒤 하단에 표시되는 주력 재료·변경 불가 안내를 확대했다. `확정하기`는 중앙 최종 확인 모달을 열며 `취소` 또는 `이 나라로 확정`을 제공한다. 선택 API는 모달의 최종 확정 동작에서만 호출하며 Escape 키로도 처리 중이 아닐 때 모달을 닫을 수 있다.
- 획득 비율의 픽셀 재료 아이콘과 퍼센트 크기를 확대하고, 실제 주력 재료의 80%는 보조 재료의 10%보다 더 크게 강조한다. 세 묶음은 국가별 영역 안에서 중앙 정렬한다.
- 세 국가의 재료 표시 순서는 모두 감자·고구마·옥수수로 고정한다. 선택한 국가에 따라 대응 재료의 비율만 80%로 바뀌며, 강조 크기도 고정 위치가 아닌 실제 주력 재료에 적용한다.
- `./gradlew.bat test`, `pnpm typecheck`, `pnpm test`, `pnpm build`, `pnpm content:validate` 통과
- 실제 PostgreSQL API 스모크에서 미선택 차단, 선택, 다른 값 409, 동일 key 재시도와 DB 중복 방지를 확인했다.
- 2026-09-07 `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/AccountFlowIntegrationTest.kt`에 전체 Flyway schema와 서로 다른 DB 트랜잭션의 동시 선택 검증을 추가했다. 테스트 소스 컴파일은 통과했고 로컬 Docker 서비스 장애로 4개 Testcontainers 테스트는 자동 건너뛰었다.
- 2026-09-08 Docker Desktop의 손상된 Windows AF_UNIX 소켓 경로를 백업해 엔진을 복구한 뒤 `AccountFlowIntegrationTest`를 실제 PostgreSQL Testcontainers 환경에서 실행해 통과했다. 실제 game-api API 세션과 브라우저에서도 신규 가입, 감자 나라 선택, 로그아웃, 재로그인 뒤 선택 복원과 첫 진입 화면 생략을 확인했다.
## 남은 범위

- 실패 주입을 통한 선택·보상 트랜잭션 롤백 통합 검증
- 드롭 콘텐츠는 `working` 상태이므로 밸런스 승격 검증
- F-05의 쌀과 모든 비재료 아이템 보상을 포괄하는 공통 보상 계층
- 인벤토리 조회 API와 전투 보상 획득 패널 연결
