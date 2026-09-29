---
doc_kind: task
owner_domain: delivery
task_code: 'J-14'
task_area: 'J 데이터·테스트·배포'
task_type: '개발'
priority: 'P1'
target_chapters: '챕터 3, 챕터 4'
planning_status: '부분 완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: ''
notion_id: ''
snapshot_date: '2026-09-09'
---

# J-14 보석 MVP 수직 슬라이스 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [보석](../../../../30-domain/gems/ssot.md)를 따른다.

## 완료 기준

1-5 최초 클리어 이후 보석 입장권, 보석함 보상, 보석함 개봉, 보석 인스턴스, 메인 프리셋 장착, 3:1 합성, 전투 스탯 반영과 웹 관리 화면을 사용할 수 있다.

## 선행 작업

D-06, F-04, K-12

## 비고

2026-09-08 최신 main 통합 후 보석 마이그레이션 재번호와 신규 격리 DB 검증은 [F-04 통합 증거](../F-items-inventory/f-04-inventory-query-sort-quantity.md#최신-main-통합-검증)를 따른다. 아래 이전 번호·DB 검증 결과는 당시 이력으로 보존한다.

2026-09-09 후속 구현: 메인·생존형·폭주형·장갑형 4개 프리셋의 6슬롯 편집 API·웹 UI를 추가했다. 활성 보석 던전은 해당 보스 프리셋만 종료까지 잠그며, 메인 프리셋 변경은 서버의 활성 메인 전투 세션을 중단하고 웹 자동전투를 현재 스테이지의 최대 HP·일반 몬스터 0/20 상태로 즉시 다시 시작한다.

현재 구현은 1-5 최초 클리어 해금 게이트, 입장권, 보석함 개봉, 메인·생존형·폭주형·장갑형 4개 프리셋의 6슬롯 편집과 던전별 잠금, 수동 3:1 및 안전 일괄 합성, 전투 스탯 반영을 포함한다. 안전 일괄 합성은 `POST /api/v1/gem-fusions/preview`와 `POST /api/v1/gem-fusions`에서 실제 소비 ID·옵션별 수량·결과 수를 미리 보여 주고, 계정 소유의 미장착·미잠금·미판매 보석만 한 트랜잭션에서 소비·독립 추첨·지급한다. 보석 던전은 보스 3종 × 10단계의 적용 수치, 서버 결정론 전투, 순서화 사건 로그, 계정당 활성 도전 1개, `minimumCompleteAt` 이후 15초 완료 창, 단계별 최고 기록, 최초 클리어, 소탕, 도전 중 프리셋 스냅샷, 보상 수용량 예약과 웹 전투 HUD 재생까지 구현·스모크 검증했다. 전투 HUD는 서버 사건 로그를 양측 HP 바, 피격·피해 표시, 자동 스킬 슬롯, 진행 타임라인과 결과 오버레이로 재생하며 작은 화면과 동작 줄이기 환경에 대응한다. 자동 캐릭터 모션은 `rest`, `run2`, `strike1`, `thrust1`, `death1`을 실제 전투 사건에 맞춰 전환한다. 액티브 4종의 캐릭터·대상 VFX도 같은 사건 로그에서 재생하며 무기 치장 크기와 무관한 독립 오버레이를 사용한다. 날짜 기반 공통 보스 규칙은 유지하면서 별도 권한 테이블에 등록된 테스트 계정만 세 보스를 선택할 수 있는 검증 경로를 제공한다. 레이드는 정책대로 설명·일정·보상 없이 `준비 중` 잠금 카드만 노출한다. J-14에서 남은 항목은 보석 거래소 연동이다.

- 2026-09-13 프리셋 잠금 슬롯 표시 후속: 보석 프리셋은 기본 3개만 이름과 편집 버튼을 노출하고, 열린 프리셋 바로 다음 한 칸에만 `+` 해금 동작과 5,000쌀 확인창을 표시한다. 최대 10개까지 확보할 수 있도록 두 페이지의 슬롯 자리는 유지하되, 그 뒤 잠긴 슬롯은 이름·버튼·테두리를 렌더링하지 않는 빈 자리로 변경했다. 전용 회귀 테스트를 추가하고 웹 전체 Vitest 67개 파일·394개 테스트, TypeScript typecheck, Vite production build를 통과했다. 전용 웹폰트에서 전각 `＋` 글리프가 보이지 않는 화면을 확인해, 해금 아이콘은 폰트 문자가 아닌 CSS 가로·세로 막대로 렌더링하도록 교체했으며, 프리셋 해금 뒤 `+`가 바로 다음 번호로 한 칸만 이동하는 동작까지 검증했다.

- 2026-09-13 던전 정각·보석 합성 후속: 입장권 최대치를 3장으로 되돌리고, 계정별 마지막 소비 시각이 아니라 보스 교체와 같은 KST 매시 정각 경계에서 충전하도록 서버 정책을 통일했다. 기존 3장 초과 데이터는 상태 조회 시 3장으로 보정하며, 화면 체류 중 1초마다 보스 개방·입장권 카운트다운을 갱신하고 정각이 지나면 보석·던전 상태를 재조회한다. 예상 보상은 `보석함 X n개`로 표시하고 미클리어 도전에는 `최초 클리어 시` 도장을 붙인다. 일괄합성은 목표 레벨에 도달하지 못해도 가능한 낮은 단계 합성을 모두 실행하고 최종적으로 남은 생성 보석을 결과로 표시한다. 과거 가져오기·로컬 검수 데이터에서 `gem_instance`만 존재하는 보석은 소유권 잠금 후 원자적으로 정리해 `INSTANCE_NOT_AVAILABLE`로 전체 합성이 막히지 않게 했다. 수동 합성 슬롯은 아이콘·레벨·옵션 세로 중앙 정렬로 고정하고 프리셋 연필은 별도 박스 없이 우상단에 배치했다. 검증은 웹 테스트 64개 파일·371개, TypeScript typecheck, Vite production build, `GemDungeonPolicyTest`, gems 모듈 테스트와 game-api Kotlin 컴파일, `GemInventoryIntegrationTest`·`GemDungeonIntegrationTest` 합계 24개를 통과했다. 로컬 던전 화면에서 2초간 체류하며 남은 시간이 `00:16:30`에서 `00:16:28`로 흐르는 것과 보상 도장·수량 표기를 확인했다.
- 2026-09-13 보석 프리셋 시안 A 행 고정 보정: 전역 탭 버튼 규칙보다 낮던 연필·플러스 규칙의 CSS 특이도를 프리셋 페이지 범위로 높여 외곽 박스·배경·최소 높이를 제거하고 탭 셸 우상단 오프셋을 고정했다. 활성 탭에서도 대비되는 연필 색상을 유지하며 탭 이름에 우측 여백을 확보했다. 슬롯 영역은 2개 `minmax(0,1fr)` 행과 `align-content: stretch`를 사용하고 카드 최소 높이 제거·내부 숨김·옵션명 2줄 클램프로 전투 연결 칩과 적용 효과 저장 바 사이를 넘지 않게 했다. 보석함 개봉 버튼은 페이지 절대 배치에서 보유 보석 패널 헤더의 마지막 요소로 이동했다. 최고 클리어 단계가 없거나 API 응답에서 `sweepStage`가 생략된 경우 `소탕 잠김` 버튼은 실제 `disabled`·`aria-disabled` 상태를 함께 갖는다. `GemUiRegression.test.tsx`에 잠긴 소탕 비활성화, 연필 테두리·배경·오프셋·활성 대비, 슬롯 행 수용, 보석함 버튼 헤더 소속 회귀 검사를 추가했다. 웹 Vitest 64개 파일·373개 테스트, TypeScript typecheck, Vite production build를 통과했다. 실제 로그인 화면을 1440·1024·390px에서 확인해 연필의 계산 스타일이 `border-width: 0px`, 투명 배경, 탭 셸 기준 상·우측 6px 이내였고 슬롯 영역은 각 폭에서 `scrollHeight = clientHeight`, 연결 칩·적용 효과와 겹침 0px, 보석함 버튼은 우측 패널 헤더 내부, 가로 넘침 0px였다. 실제 누락 `sweepStage` 응답에서도 `소탕 잠김`, `disabled=true`, `aria-disabled=true`, opacity 0.48을 확인했다.
- 2026-09-13 프리셋 밀도·페이지 후속: 프리셋 선택 영역이 슬롯 영역을 아래로 밀지 않도록 버튼 높이를 40px로 줄이고 한 페이지를 5개 고정 열로 구성했다. 1페이지는 1~5번, 2페이지는 6~10번 프리셋을 표시하며 이전·다음 버튼과 `현재 / 전체` 페이지 표시를 제공한다. 잠긴 칸 중 다음 해금 대상 한 칸에는 별도 플러스 버튼을 렌더링하고 나머지는 자리만 유지해 항상 한 줄 5칸 정렬을 보존한다. 사용자 요청에 따라 우측 보유 보석 헤더의 `보석함 1개 열기` 버튼과 해당 개봉 mutation 연결을 제거했으며 보유 보석함 수량 표시는 상단 상태 영역에 유지한다.
- 2026-09-13 출시 전 원자성 보정: UI가 목표 레벨까지 레벨별 `SAFE_BATCH` 요청을 여러 번 보내던 경로를 제거하고, 목표 레벨과 허용 옵션을 한 번 전달하는 단일 서버 명령으로 교체했다. 서버는 계정 잠금 아래에서 각 단계의 소비·생성·다음 단계 재사용을 한 트랜잭션으로 처리하며 어느 단계에서 실패해도 전체를 롤백한다. 응답의 `granted`는 연쇄 종료 뒤 실제 남은 생성 보석 전체다. `GemInventoryIntegrationTest`에 1레벨 3개와 2레벨 2개가 한 명령에서 3레벨 1개로 끝나는 다단계 원자 합성 검증을 추가했다.

## 증거 링크

- 2026-09-13 보석 관리 B안 후속 UI: 직전 A안의 우측 4열×6행 페이지 목록을 폭 360px의 단일열 스크롤 목록으로 교체하고, 각 행에 56px 보석 아이콘·19px 레벨·16px 옵션명·15px 수치를 가로 배치했다. 스크롤바는 숨기고 전체·공격·생존·특수 필터와 레벨 오름·내림차순은 유지했다. 합성 탭은 우측 목록을 고정한 채 좌측 프리셋 영역만 수동/일괄 합성 작업대로 교체하며, 수동 3슬롯은 빈 상태에서 시작해 첫 선택과 레벨이 다른 보석 및 잠금·장착·판매 보석을 어둡게 비활성화한다. 일괄합성 옵션은 결과 목표 2~7레벨과 옵션 6종을 선택하고 낮은 레벨부터 단계별 3:1 합성 예정 횟수를 표시한 뒤 기존 `SAFE_BATCH` API를 순차 호출한다. 프리셋은 기본 3개·최대 10개, 인라인 연필/체크 이름 편집, 다음 한 칸만 보이는 플러스와 5,000쌀 확인창, 전투 유형별 단일 프리셋 연결, 확대된 6슬롯, 가로 효과 요약과 우측 저장 버튼으로 정리했다. 프리셋 이름·해금·전투 연결은 현재 브라우저 로컬 저장이며 서버 영구 저장·실제 쌀 차감·레이드 적용은 아직 구현되지 않았다. 던전 로테이션 서버 정책은 이미 KST 매시 정각 경계를 사용하므로 변경하지 않고 선택 화면에 `KST 매시 정각 교체`를 명시했다. 웹 전체 Vitest 63개 파일·369개 테스트, TypeScript typecheck, Vite production build와 `GemDungeonPolicyTest`를 통과했다. PostgreSQL 17.6 검수 DB·game-api·Vite를 연결해 `http://127.0.0.1:5173/` 및 프록시 인증 세션 API의 HTTP 200을 확인했다. 브라우저 자동화 런타임은 로컬 커널 에셋 오류로 열리지 않아 픽셀 단위 시각 검수는 사용자 확인으로 남긴다.

- 2026-09-13 보석 관리 A안 로컬 UI 반영: 보석 관리를 장비·인벤토리와 동일하게 메인 전투 화면을 유지한 `<dialog>` 오버레이로 전환하고, 기존 대비 최대 창 크기를 약 1.2배로 확장했다. 프리셋 제목 옆 외부 클릭 닫힘 도움말, 기본 3개·최대 10개 프리셋 표시, 12자 이름 편집, 5,000쌀 해금 미리보기, 우측 보유 보석 4열×6행·24개 페이지와 레벨 양방향 정렬을 추가했다. 사용자가 확정한 1–2·3–4·5–6·7레벨별 픽셀 보석 네 형태를 투명 PNG로 적용하고 옵션별 주황·노랑·빨강·초록·파랑·보라 색상 변형 및 레벨별 크기 차이를 렌더링한다. 합성 수식·검정 물음표 결과·합성/획득 애니메이션은 같은 에셋 렌더러를 사용한다. 현재 3–10번 사용자 프리셋 이름·구성·해금 표시는 로컬 브라우저 시안 상태이며, 서버의 전투 적용·지갑 차감 정본은 기존 `MAIN`·`SURVIVAL`·`BERSERK`·`ARMORED` 4종 계약을 유지한다. 따라서 신규 프리셋의 영구 저장·실제 5,000쌀 차감은 미구현이며 J-14 상태는 `부분 구현`을 유지한다. 변경 경로는 `apps/web/src/features/gems/GemManagement.tsx`, `GemsScreen.tsx`, `assets/gem-level-*.png`, `apps/web/src/navigationState.ts`, `apps/web/src/styles.css`다. 보석 관련 Vitest 3개 파일·26개 테스트, TypeScript typecheck와 Vite production build를 통과했다.

- 2026-09-12 해금 계정 보석 API 500 보정: `stage.01-05.first_cleared_at`과 던전 진행도는 정상 저장돼 있었지만, 충전 대기 중인 계정의 `GET /api/v1/gems`가 `GemService.refreshTickets()`에서 `java.time.Instant`를 PostgreSQL 파라미터로 직접 전달해 실패했다. UI는 응답을 받지 못해 던전·보석 버튼을 기본 잠금 상태로 표시했다. `last_ticket_at` 갱신 값을 JDBC가 명확히 처리하는 `java.sql.Timestamp`로 변환하고, 해금된 계정의 누적 입장권 충전과 시각 갱신 회귀 검사를 추가했다. 서버 해금 기준과 DB 스키마는 변경하지 않았다.
- 2026-09-12 1-5 클리어 직후 해금 캐시 갱신: 서버는 기존대로 `stage.01-05.first_cleared_at`을 해금 근거로 정상 저장·조회했지만, 웹 자동전투 완료 콜백이 `stages`·`inventory` 등만 무효화하고 공유 `gems` 쿼리를 갱신하지 않아 로그인 직후 캐시된 `unlocked=false`가 남는 원인을 확인했다. `AutoBattleRuntime`의 전투 완료 쿼리 무효화 목록에 `gems`를 추가해 1-5 완료 즉시 던전·보석 버튼과 화면이 서버 해금 상태를 다시 읽는다. 이미 잠금 캐시가 남은 로컬 화면도 복구되도록 잠긴 동안만 5초 간격으로 서버 상태를 재확인하고, 해금되면 폴링을 중지한다. 완료 콜백의 보석 상태 갱신 회귀 검사를 추가했고 웹 전체 62개 파일·356개 테스트, TypeScript typecheck와 production build를 통과했다. 서버 해금 정책과 DB 스키마는 변경하지 않았다.

- 2026-09-12 현재 착용 치장 전투 렌더링: 보석 던전의 하드코딩 천사 테스트 장비를 제거하고 `GET /api/v1/cosmetics/collection`의 현재 6부위 착용 상태를 공용 `PlayerBattleCharacter`에 전달한다. 메인 전투도 같은 렌더러와 같은 collection 캐시를 사용한다. 현재 적용 카탈로그에서 완성 프레임과 실제 ID가 함께 있는 `cosmetic-061`~`066` 완전 착용은 거북이 수호자 프레임으로 표시한다. 정적 검수 전용 붕어빵·요리사·무지개떡 완전 세트도 제공 프레임으로 표시한다. 부분 착용·혼합·미착용·지원되지 않는 실제 조합은 존재하지 않는 레이어를 합성하지 않고 기본 캐릭터를 사용하며 천사 세트로 대체하지 않는다. 공용 렌더러 테스트는 거북이 완전 세트, 지원되는 다른 완전 세트, 부분 착용, 미착용과 `angel-character-stage` 부재를 검증한다.

- 2026-09-12 무보석 7단계 로컬 계정 검증: 격리 베타 DB에 신규 계정을 만들고 레벨 40, 전 장비 영웅 +1, 일반 스킬 10, 보석 인스턴스·프리셋 0개로 구성했다. 실제 API 캐릭터 스냅샷은 공격력 530, 최대 HP 5,302, 방어 관통 494였고, 가속 베타 콘텐츠의 생존형·폭주형·장갑형 7단계를 각각 시작해 모두 성공 판정을 확인했다. 생존형은 종료 시 HP 829, 폭주형은 141틱, 장갑형은 124틱에 처치했으며 검증 도전은 중단 처리해 입장권과 보상을 소비하지 않았다. 기준 정책과 수치는 [가속 베타 프로파일](../../../../60-content/gems/beta-accelerated.md)을 따른다.

- 2026-09-12 가속 베타 운영 프로파일 분리: 보석 던전의 순환 주기·입장권 충전 주기·입장권 최대 보유량·보스 수치 콘텐츠를 하드코딩에서 `GemDungeonPolicy` 설정값으로 분리했다. 기본값은 정식 서비스 값(86,400초 순환, 28,800초 충전, 최대 3장, `gem-dungeons.json`)이고, `application-beta.yml`만 가속 값(3,600초 순환, 3,600초 충전, 최대 5장, `gem-dungeons-beta.json`)으로 덮어쓴다. 순환은 KST 정렬 시간 슬롯으로 일반화해 86,400초 슬롯이 기존 KST 일자 순환과 같은 보스를 열도록 했고, `GemRules.bossForSlot`은 `Math.floorMod`를 사용해 에포크 이전 슬롯에서도 안전하다. 입장권 상한 CHECK 제약은 `V52__gem_ticket_stock_policy.sql`에서 `tickets >= 0`으로 한 번만 넓히는 단방향 마이그레이션으로 바꿔, 프로파일 전환에 되돌릴 마이그레이션이 없다. 베타 보스 수치는 현재 `packages/sim-core` 기준으로 산출해 보스 3종 × 10단계 전부 1,000 seed 100% 성공, 바로 아래 단계 빌드는 27개 조합 모두 0~1.6%로 실패함을 확인했다. 산출 근거와 값은 `docs/60-content/gems/beta-accelerated.md`가 소유한다. 승격된 `gem-dungeon-v1-applied` 수치가 2026-09-08 `sim-core` 행동 예약 모델 변경 이후 현재 시뮬레이터와 최대 12.2% 어긋난다는 사실을 확인해 `docs/60-content/gems/mvp-v1.md`에 `unresolved`로 등록했다. 2026-09-12 로컬 적용 검증에서는 기존 DB의 과거 Flyway 체크섬 불일치를 보존하고 격리 DB `hanjjak_beta`를 생성해 PostgreSQL 17.6에서 당시 존재하던 마이그레이션까지 적용했다. `beta` 프로파일 API를 포트 8081에서 실행해 liveness·readiness `UP`을 확인했고, 웹은 `VITE_API_PROXY=http://127.0.0.1:8081`로 포트 5173에서 실행했다. 시간제 로테이션과 맞지 않던 웹의 `오늘의 보스` 문구를 `현재 보스`로 정리한 뒤 TypeScript typecheck와 `GemsScreen.test.tsx` 8개 테스트를 통과했으며, 소스 기준 콘텐츠 검증 테스트 7개와 실제 콘텐츠 검증 명령도 통과했다. `GemDungeonPolicyTest`는 테스트 클래스가 컴파일 산출물에 존재하고 `javap`으로 로드되는 것까지 확인했지만 Windows 한글 작업 경로에서 Gradle test worker가 해당 클래스를 찾지 못해 실행되지 않았으므로 이 단위 테스트 항목은 미해결로 남긴다.

- 2026-09-12 정식 기본값 승격 및 최종 재검증: `application.yml`과 `GemDungeonPolicy`의 기본값을 1시간 보스 순환, 1시간당 입장권 1장, 최대 5장, `gem-dungeons-v2.json`, `authority: applied`로 승격했다. 이전 정식 `gem-dungeons.json`과 승격 원본 `gem-dungeons-beta.json`은 식별자와 파일을 그대로 보존해 회귀 비교·명시적 롤백이 가능하다. 최신 `main`의 V50·V51과 충돌하지 않도록 신규 입장권 제약 마이그레이션을 `V52__gem_ticket_stock_policy.sql`로 확정했고 버전 중복 검사를 통과했다. Windows 한글 경로의 Gradle worker 문제는 저장소를 `Z:`로 매핑해 우회했으며 `GemDungeonPolicyTest`, `FlywayMigrationVersionTest`, `GemInventoryIntegrationTest`, `CombatSimulatorTest`, `DungeonCombatSimulatorTest`와 `bootJar`를 실제 실행해 통과했다.

- 2026-09-12 보석 관리 UI 전체 시안 이식: `GemManagement.tsx`, `GemsScreen.tsx`, `main.tsx`, `styles.css`에서 기존 세로형 관리 화면을 다른 관리 탭과 같은 독립 종이 창으로 전환했다. 상단 `프리셋`·`합성` 탭, 닫기와 보유 보석함 표시, 프리셋·인벤토리 양쪽 패널, 3열 × 2행 프리셋 슬롯, 현재 적용 효과 합계, 전체·공격·생존·특수 필터, 페이지당 6열 × 4행(24개) 보석과 레벨 오름·내림차순, 명시적 `프리셋 저장`과 보석함 개봉을 구현했다. 합성 화면은 사용자 노출 명칭을 `합성`·`일괄 합성`으로 정리하고 수동 입력 3개와 미확정 결과를 `보석 + 보석 + 보석 = 보석` 형태로 표시하며, 실제 성공 시 병합·결과 공개 애니메이션과 획득 대화상자를 표시한다. `GemManagement.test.tsx`에서 정렬·페이지 이동·초안 저장·효과 합계·필터·상단 화면 전환·닫기·합성 성공/실패 및 Escape 닫기를 검증했고, `GemsScreen.test.tsx`에서 전체 관리 셸을 검증했다. 웹 Vitest 60개 파일·344개 테스트, typecheck, production build와 `git diff --check`를 통과했으며, 실제 React 미리보기에서 종이 창·3×2 슬롯·현재 효과·필터·6열 카드의 반응형 배치를 확인했다.

- 2026-09-12 던전 화면 셸 전환: `navigationState.ts`, `main.tsx`, `GemsScreen.tsx`, `styles.css`에서 보석 던전 선택 화면을 메인 전투와 같은 전체 뷰포트 셸에 연결했다. 선택 중에는 공용 프로필·상단 메뉴·하단 성장 메뉴를 유지하고 책 배경을 화면 전체에 표시하며, 전투 도전이 활성화되면 공용 UI를 숨기고 던전 전투를 `100dvh` 최상위 레이어로 전환한다. 631px 높이 검수 화면에서도 양측 HP, 중앙 정수 초 타이머, 캐릭터·보스, 전투 기록, 스킬 4개, 진행률과 도전 포기 버튼이 한 화면에 들어오도록 저높이 레이아웃을 조정했다. `navigationState`·`GemsScreen` 집중 Vitest 2개 파일·20개 테스트, 웹 TypeScript 검사와 Vite 프로덕션 빌드를 통과했고, 독립 미리보기에서 선택 화면 공용 UI 유지와 전투 화면 공용 UI 제거를 브라우저로 확인했다. 보석 던전의 해금·일일 순환·입장권·15초 판정 규칙은 변경하지 않았다.

- 2026-09-12 보석 던전 확정 시안 런타임 조합: `GemsScreen.tsx`와 `styles.css`를 펼친 요리책형 선택 화면, 일일 개방 북마크 3종, 음식 보스별 키아트·전장·4×5 모션 시트, 상단 양측 HP·중앙 정수 초 타이머, 하단 전투 기록·스킬 카드·진행률·포기/결과 확정 동선으로 교체했다. 생존형·폭주형·장갑형은 각각 우동 몬스터·메추리알 장조림 몬스터·도토리묵 몬스터로 표시하되 서버의 KST 일일 순환, 입장권·단계·소탕·활성 도전 계약은 변경하지 않았다. 전투 기록은 100ms tick을 내림한 정수 초만 표시하며 `wholeEventSecond` 회귀 테스트를 추가했다. 백엔드가 없어도 선택 화면과 15초 전투 재생을 확인하도록 기존 `battle-hud-preview.html`에 `?route=dungeon`과 `?route=dungeon&battle=1`을 추가했고 612px 브라우저에서 상단 공용 메뉴, 책형 선택·보스 상세, HP·정수 초 타이머, 음식 보스, 가로 스크롤 스킬 카드, 진행률과 포기 버튼의 겹침을 교정했다. 웹 TypeScript 검사, Vitest 59개 파일·339개 테스트, Vite 프로덕션 빌드를 통과했다. Vitest는 비기동 백엔드 `localhost:3000` 연결 거부 로그를 출력했지만 테스트 실패는 없었다. 실제 인증 API를 포함한 다중 뷰포트 검수는 백엔드 미기동으로 보류해 상태는 `부분 구현`·`부분 검증`을 유지한다.

- 2026-09-11~12 보석 던전 시각 에셋·조합 시안: 사용자 확정 방향에 맞춰 `apps/web/src/features/gems/assets-cozy-pixel-v2/`에 선택용 펼친 책, 일일 던전 북마크 3종, 저채도 전투 배경 3종, 음식 보스 키아트·4×5 모션 시트 3종, 4×2 공격 이펙트 3종, 타이머·스킬 카드·결과 배지, 입장권·보석함·소탕·기믹 아이콘을 정리했다. `apps/web/src/features/gems/design-drafts/dungeon-composed-v1/`에는 던전 선택 화면과 우동 생존형 전투 화면의 조합 시안 2종을 보존했다. 가변 이름·시간·HP·쿨다운·요일·보상 수치는 구현 시 HTML/CSS 렌더링 대상으로 유지한다. PNG 27개와 JSON 메타데이터 5개를 검사해 보스 셀 280×280, 공격 이펙트 셀 443×443, 개별 아이콘 6개와 투명 배경 규격을 확인했다. 런타임 연결과 실제 뷰포트 모션 검수는 수행하지 않았으므로 작업 상태는 `부분 구현`·`부분 검증`을 유지한다.

- 2026-09-11 1-5 해금 기준 전환: `GemService`와 `GemDungeonService`가 `stage.01-05`의 `first_cleared_at`을 단일 해금 근거로 사용하도록 변경했다. 웹 HUD는 `/api/v1/gems`의 `unlocked`를 받아 던전·보석 버튼을 활성화하며, 미해금 상태에는 `1-5 해금`을 표시한다. 로컬 PostgreSQL 18.4 연결 API에서 1-5를 이미 클리어한 테스트 계정의 던전·보석 버튼 활성화를 확인했다. 웹 54개 파일·306개 테스트, typecheck, production build를 통과했다. Testcontainers 통합 테스트는 소스·테스트 컴파일까지 통과했으나 로컬 Docker 실행기를 찾지 못해 초기화 단계에서 실행되지 않았다.

- 2026-09-09 #6 프리셋·안전 합성 구현: `GemService.updatePreset`과 `GemFusionController`에 4프리셋 저장, 활성 던전 보스 프리셋 잠금, 메인 전투 세션 중단, 수동·안전 일괄 합성 미리보기/원자 실행을 추가했다. `GemManagement.tsx`에서 4개 6슬롯 편집, 잠금 안내, 수동 3개 선택, 옵션별 안전 수량과 실제 소비·예상 결과 확인을 제공한다. `GemInventoryIntegrationTest` 13개, 웹 Vitest 90개, 웹 typecheck·프로덕션 빌드, TypeSpec 계약 생성과 콘텐츠 검증을 통과했다. 격리 PostgreSQL 17.6과 실제 API·브라우저에서 같은 보석을 메인·생존형 프리셋에 각각 등록하고, 수동 3개 미리보기의 옵션별 `2+1` 소비·결과 1개, 안전 일괄 미리보기의 고정 공격력 3개 소비·결과 1개를 확인했다. 1440px와 390px 화면 모두 가로 넘침이 없었고, 모바일은 프리셋·슬롯을 2열로 표시했으며 브라우저 콘솔 오류가 없었다.
- 2026-09-09 Docker 기동 후 재검증: Docker Engine 29.7.2 연결을 확인하고 임시 Testcontainers PostgreSQL로 `:apps:game-api:test`를 재실행했다. XML 보고서 기준 46개 테스트가 실패·오류·건너뜀 없이 통과했다. 보석 던전 5개(영구 장비 입장 스냅샷·중단·만료 포함), 보석 인벤토리 11개, 보석·인벤토리 마이그레이션 9개 및 계정·전투·제작·거래소·치장 검증을 포함한다. 함께 요청한 inventory·equipment·market·account 단위 테스트 작업은 기존 성공 결과를 재사용했고 gems 모듈 자체 테스트는 NO-SOURCE다. 종료 과정에서 스케줄러의 임시 DB 연결 거부 로그가 발생했으나 Gradle은 BUILD SUCCESSFUL, 테스트 보고서는 모두 성공이다. 테스트 종료 수명주기 정리는 후속 개선 대상으로 남긴다. 운영 DB 및 과거 번호 적용 DB의 업그레이드는 실행하지 않았으며 push·배포도 수행하지 않았다. 아래 Docker 미기동으로 보류했던 최신 코드의 DB 테스트는 이번 실행으로 해소했다.
- 2026-09-09 0차 배포 후 통합 점검: 원격 `main`의 `806b22d`까지 67개 커밋을 fast-forward 반영하고 기존 중단·만료 테스트와 문서 변경을 충돌 없이 복원했다. `GemDungeonIntegrationTest`에 영구 장비 능력치의 던전 반영 및 입장 후 장비 변경과 무관한 스냅샷 유지 검증을 추가했다. 테스트 컴파일, 계정·장비·거래소 단위 테스트와 인벤토리 테스트 작업(기존 성공 결과 재사용), 웹 Vitest 80개·typecheck·프로덕션 빌드 및 `git diff --check` 통과. DB 통합 테스트는 Docker 엔진 연결 실패로 9개 클래스가 초기화에 실패했고 Docker 시작 명령도 응답하지 않아 종료했다. 신규 장비 스냅샷 및 기존 중단·만료 시나리오의 최신 DB 실행 검증은 보류한다. 최신 마이그레이션 번호 중복은 없지만 과거 V24/V25 적용 DB의 업그레이드 호환성을 검증한 것은 아니다. 현재 CI는 검증 작업을 비활성화하고 main 이미지 빌드·배포를 활성화한다. 운영 코드·마이그레이션·배포 설정·운영 DB는 변경하지 않았고 push·배포도 수행하지 않았다.
- 2026-09-08 병합 후 `origin/main`의 `730be1b` 기준: `GemDungeonIntegrationTest`에 중단·동일 요청 재시도·중단 후 완료 요청 및 만료 후 늦은 완료 요청 검증을 추가했다. 보상 예약 해제, 입장권 유지, 보석함·최초 클리어 미지급, 중단 후 새 도전 가능을 확인했다. 임시 Testcontainers PostgreSQL에서 해당 클래스 4개 테스트 통과. 만료 시각 구성의 DB 제약 오류를 테스트 준비 코드에서 수정한 후 재실행했다. 운영 코드·마이그레이션·배포 DB는 변경하지 않았다. 성공 보상·만석 예약·모바일 전체 흐름의 이번 재검증은 아직 수행하지 않았다.
- 2026-09-08 실연결 추가: 격리 DB `hanjjak_local_verify`에 V21~V23 적용 후 실제 인증 HTTP로 같은 옵션 보석 3개 묶음, 전체 잠금 저장·재조회·새 로그인 세션 유지·전체 해제 통과. 실제 브라우저에서도 묶음 선택·공통 옵션 1회·칸 잠금/해제 및 화면 이동 후 유지 확인. 기존 `hanjjak` 및 팀 DB는 미변경. 전체 프리셋 편집·합성 UI·보석 거래소 등 남은 범위는 그대로다.

- `V26__persistent_gem_slots.sql`, `GemService.lockSlot`, `GemController`, `packages/contracts/gems.tsp`, `InventoryScreen.tsx`. `GemInventoryIntegrationTest` 칸 전체 잠금·재시도·소유권·잠금 합성 차단·다른 칸 보존 테스트 통과. 기존 로컬·팀 DB에는 미적용.

- DB: `apps/game-api/src/main/resources/db/migration/V14__gems_mvp.sql`
- API: `modules/gems/src/main/kotlin/com/hanjjak/gems/application/GemService.kt`, `modules/gems/src/main/kotlin/com/hanjjak/gems/api/GemController.kt`, `modules/gems/src/main/kotlin/com/hanjjak/gems/api/GemFusionController.kt`
- 전투 반영: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/AccountBattleStatsProvider.kt`, `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/AccountBattleSkillProvider.kt`, `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/AccountGemDungeonPlayerProvider.kt`, `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsService.kt`, `modules/gems/src/main/kotlin/com/hanjjak/gems/application/GemService.kt`
- UI: `apps/web/src/features/gems/GemManagement.tsx`, `apps/web/src/features/gems/GemsScreen.tsx`, `apps/web/src/features/gems/api.ts`, `apps/web/src/features/battle/autoBattleControl.ts`
- 계약: `packages/contracts/gems.tsp`
- 던전 DB·API: `apps/game-api/src/main/resources/db/migration/V20__gem_dungeon_lifecycle.sql`, `apps/game-api/src/main/resources/db/migration/V21__gem_dungeon_test_access.sql`, `modules/gems/src/main/kotlin/com/hanjjak/gems/application/GemDungeonService.kt`, `modules/gems/src/main/kotlin/com/hanjjak/gems/api/GemDungeonController.kt`
- 던전 판정·테스트: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/DungeonCombatSimulator.kt`, `packages/sim-core/src/test/kotlin/com/hanjjak/sim/DungeonCombatSimulatorTest.kt`
- 던전 콘텐츠: `packages/game-content/versions/v1/gem-dungeons/gem-dungeons.json`, `packages/game-content/versions/v1/gem-dungeons/gem-dungeons-beta.json`, `packages/game-content/schema/gem-dungeons.schema.json`, `tools/content-validator/src/validate.ts`
- 운영 프로파일: `modules/gems/src/main/kotlin/com/hanjjak/gems/domain/GemDungeonPolicy.kt`, `modules/gems/src/main/kotlin/com/hanjjak/gems/api/GemConfiguration.kt`, `apps/game-api/src/main/resources/application.yml`, `apps/game-api/src/main/resources/application-beta.yml`, `apps/game-api/src/main/resources/db/migration/V52__gem_ticket_stock_policy.sql`, `modules/gems/src/test/kotlin/com/hanjjak/gems/domain/GemDungeonPolicyTest.kt`
- 던전 UI: `apps/web/src/features/gems/GemsScreen.tsx`, `apps/web/src/features/gems/GemsScreen.test.tsx`, `apps/web/src/features/gems/api.ts`
- 던전 천사 장비 런타임(2026-09-07): `apps/web/src/features/gems/AngelDungeonCharacter.tsx`, `apps/web/src/features/gems/angelEquipment.ts`, `apps/web/src/features/gems/angelEquipment.test.ts`, `apps/web/public/assets/characters/angel/runtime-equipment-v1/`. 928×672 고정 좌표계와 매니페스트의 프레임별 그리기 순서를 유지하며 6개 슬롯을 독립 장착하고 천사 무기 3종을 교체하는 테스트 UI를 전용 계정 경로에 연결했다.
- 던전 스킬 VFX 런타임(2026-09-09): `apps/web/src/features/gems/skillVfx.tsx`, `apps/web/src/features/gems/skillVfx.test.ts`, `apps/web/public/assets/effects/skills-v1/`. 단발 4프레임, 지속 피해 50틱과 10틱 간격의 밝은 피격 펄스, 버프 50틱을 서버 사건 시각에 맞춰 재생한다. 마! 쫄이나 눈 이펙트는 검증된 `thrust1` 928×672 프레임 좌표에 맞췄고 모든 이펙트는 무기 레이어에서 분리했다. 웹 typecheck, Vitest 30개 파일·81개 테스트와 Vite 프로덕션 빌드를 통과했다.
- 검증: 전체 Gradle 테스트 55개 작업 성공, TypeSpec 컴파일 성공, 웹 typecheck·프로덕션 빌드 성공, Vitest 20개 성공, 던전 JSON을 포함한 콘텐츠 스키마·참조·체크섬 검증 성공. 2026-09-04 전투 HUD와 계정 제한 보스 선택 경로 개선 후 gems 모듈·game-api 빌드, TypeSpec 컴파일, 웹 typecheck, Vitest 21개, Vite 프로덕션 빌드를 통과했다. Windows 한글 경로의 Gradle 증분 컴파일 경고는 비증분 컴파일로 자동 우회되어 빌드는 성공했다.
- HTTP 스모크(2026-09-04, 로컬 Docker PostgreSQL 17.6): 현재 Flyway V18인 던전 생명주기 마이그레이션을 적용해 생존형 1단계 시작(사건 9개), 즉시 완료 422 차단, 15초 후 `SUCCEEDED`, 보석함 10개 지급·입장권 3→2·최고 단계 기록, 1회 소탕 보석함 3개·입장권 2→1을 확인했다.
- 테스트 보스 선택 스모크(2026-09-04, 로컬 Docker PostgreSQL 17.6): 현재 Flyway V19인 테스트 접근 마이그레이션을 적용해 권한이 등록된 전용 계정에서 폭주형·장갑형 선택 응답과 `testBossSelectionEnabled=true`를 확인했다.
- 전투 HUD 회귀 검증(2026-09-05): 피격 본체와 피해 숫자의 React key 충돌을 재현하는 테스트가 수정 전 플레이어·보스 양쪽에서 실패하고 수정 후 통과했다. 실제 브라우저에서 폭주형·장갑형을 재실행해 각각 본체 1개, 아레나 높이 440px, 가로 넘침 없음, 중복 key 콘솔 오류 없음을 확인했다.
- 천사 장비 검증(2026-09-07): 원본과 대상의 아틀라스 14개·모션 시트 70개 SHA-256 일치, 기본 조합 `rest_01`은 기준 합성본 대비 브라우저 알파 합성 반올림에 따른 채널값 최대 2의 경계 픽셀 차이만 확인됐다. 5모션×4프레임·무기 3종·슬롯별 해제/복원을 실제 브라우저에서 확인했다. 웹 typecheck, Vite 프로덕션 빌드와 Vitest 28개를 통과했고 브라우저 요청 실패 및 애플리케이션 콘솔 오류가 없었다.
- 최신 `main` 병합 회귀 검증(2026-09-08): 영구 캐릭터 스탯 계산 경로를 보석 던전에도 공통 적용하고, 보스별 보석 프리셋을 `CharacterStatsService` 계산에 한 번만 전달하도록 연결했다. 영구 공격속도·기본공격 증폭·버프 지속시간을 던전 판정에도 반영하고 작은 공격속도 증가가 누적되도록 수정했다. 프리셋 스냅샷 중복 적용과 입장권 충전 경과시간 보존을 Docker PostgreSQL 통합 테스트로 추가 검증했다. Docker PostgreSQL을 사용한 전체 Gradle 테스트 70개 작업, TypeSpec 컴파일, 최신 `main` 병합 후 웹 typecheck·프로덕션 빌드와 Vitest 44개, 콘텐츠 검증 테스트 3개를 통과했다.
- 던전 밸런스 후보 산출: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/DungeonBalance.kt`
- 던전 수치 검증: `./gradlew.bat :apps:balance-lab:dungeonBalance` 성공, 보스 3종 × 10단계 × 1,000 seed 결과는 `docs/70-plans/mvp-release/verification/gem-dungeon-candidate.csv`에 보존. 사용자 승인된 보스 절대 수치는 `gem-dungeon-v1-applied`로 승격했다.
- 2026-09-12 가속 수치 정식 v2 승격: 기존 `gem-dungeon-v1-applied`의 `gem-dungeons.json`과 승격 입력 `gem-dungeon-beta-v1-accelerated`의 `gem-dungeons-beta.json`을 변경 없이 보존하고, 같은 검증 수치를 신규 `gem-dungeons-v2.json`·`gem-dungeon-v2-accelerated-applied`로 추가했다. 정식 기본 정책은 KST 1시간 보스 순환, 입장권 1시간 충전·최대 5장, 신규 v2 applied 콘텐츠로 전환했다. 환경변수로 이전 콘텐츠 파일과 주기를 다시 선택할 수 있어 과거 전투 재현과 롤백 경로를 유지한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 부분 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
