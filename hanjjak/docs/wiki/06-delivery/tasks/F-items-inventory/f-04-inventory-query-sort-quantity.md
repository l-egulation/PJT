---
doc_kind: task
owner_domain: delivery
task_code: 'F-04'
task_area: 'F 드롭·인벤토리'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '진행 중'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/00306c8781b082bf921e01bc3e174358'
notion_id: '00306c87-81b0-82bf-921e-01bc3e174358'
snapshot_date: '2026-08-28'
---

# F-04 인벤토리 조회·정렬·수량 처리 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [아이템](../../../../30-domain/items/ssot.md)를 따른다.

## 완료 기준

획득 아이템이 합산되고 분류·정렬되며 사용·판매 시 정확히 차감된다.

## 선행 작업

B-04

## 비고

2026-09-11 인벤토리 아이템 탭 UI 개편에서 한 페이지 조회 수를 12칸으로 조정하고, 기존 cursor 기반 이전·다음 이동과 분류·정렬 API 연결을 유지했다. 목록 첫 항목 자동 선택과 nullable 슬롯 키 대체 처리를 추가했으며 실제 인증 API 브라우저 재검증은 남아 있어 진행 중·부분 검증 상태를 유지한다.

2026-09-08 최신 main 통합: `origin/main`의 `60b6ff2`까지 fast-forward한 뒤 기존 미커밋 변경을 충돌 없이 복원했다. 팀의 V20~V22 뒤에 이어지도록 인벤토리 이관·거래소 보관·영구 보석 칸 마이그레이션을 각각 V23/V24/V25로 재번호했다. 아래 V21~V23 검증 기록은 재번호 전 이력이다. 기존 `hanjjak`, `hanjjak_local_verify`와 팀 DB는 보존했고 신규 격리 DB `hanjjak_main_inventory_verify`만 사용했다. 안전 백업 stash는 유지하며 커밋·push·배포는 하지 않았다.

2026-09-08 실연결 검증 추가: 격리 로컬 DB `hanjjak_local_verify`에 V21~V23 적용 및 실제 서버 기동 성공. 기존 `hanjjak` DB와 팀 DB는 변경하지 않았다. 아래 미적용·인증 API 인수 미완료 기록은 이 추가 검증 전 이력이다. 던전 전체 흐름과 갱신 경합 등 잔여 범위 때문에 상태는 유지한다.

2026-09-08 최신 변경: 판매 등록은 인벤토리에서 실제 차감하고 거래소에서 보관한다. 보석은 영구 칸 ID와 칸 전체 잠금을 저장하며 잠금 변경·부분 소비 후 다른 칸으로 자동 재배치하지 않는다. 아래 판매 예약 표시·개별 선택 설명은 이전 구현 이력이다. V22/V23은 임시 Docker DB에서만 검증하며 기존 로컬·팀 DB에는 미적용이다.

2026-09-08 목록 후속 구현: `InventoryQueryService`가 재료와 속성 일치 보석을 칸별로 분할하고 `slotId`, 개별 ID·판매 예약 상태의 `members`를 반환한다. 종류별 상세 합계는 유지한다. 재료 예약량은 집계 상태를 칸 순서로 배분한 표시이며 물리적 칸 예약을 저장하는 것은 아니다. cursor는 목록 의미 변경에 맞춰 v2로 변경했다. 아래의 목록 미구현 기록은 이전 시점 이력이다. 실제 인증 API 연결 브라우저 인수가 남아 진행 중·부분 검증을 유지한다.

2026-09-08 후속 사용자 승인으로 [아이템 SSOT](../../../../30-domain/items/ssot.md#인벤토리-슬롯과-수량)의 중첩 기준이 변경되어 개발을 진행 중으로 전환했다. 후속 구현에서 공용 슬롯 계산, 보석 속성별 `stack_key`, 지급·소비·단일/일괄 거래와 개봉·합성 사전 수용량 검사를 반영했다. 추첨 전 검사는 가능한 결과 그룹의 최대 슬롯 증가량을 계산한다. V21은 기존 보석 ID·개별 상태를 유지하고 새 슬롯 기준으로 초과 시 전체 롤백하도록 수정·검증했다. 연결된 보석 원본이 없는 기존 인스턴스는 속성을 추정하지 않고 개별 슬롯으로 유지한다. 그룹별 목록 API·UI는 아직 미구현이므로 완료로 올리지 않는다. V21은 임시 테스트 DB에서만 검증했으며 기존 로컬·팀 DB에는 미적용이다.

목록·상세·상태 조회, 스택·인스턴스 수량, 판매 예약·사용 가능 수량, 카테고리·허용 정렬·불투명 cursor 페이지네이션을 구현했다. 2026-09-08 단계 2에서 보석 개별 목록·생성·합성의 인벤토리 반영, 순증가 수용량 검사, 장비·스킬·던전 지급/소비 경유, 예약을 포함한 공용 용량 계산과 거래소 0수량 행 검사를 추가했다. Testcontainers PostgreSQL 통합 검증은 통과했으며 실제 브라우저 인수는 남아 있어 부분 검증을 유지한다.

지시서 INV-03의 스텁 전제는 최신 main과 다르다. V18과 GemDungeonService의 실제 도전·예약 흐름이 이미 있으므로 보류하지 않고 기존 예약 기능과 연결했다. 활성 STACK_RIGHT의 스택이 소진된 경우에도 점유를 유지하고 해당 아이템 재지급 시 중복 점유하지 않는 회귀를 검증했다. 던전 완료·만료·중단 전체 브라우저 시나리오는 별도 인수가 필요하다.

## 증거 링크

- 2026-09-11: `apps/web/src/features/inventory/api.ts`의 페이지 크기를 12로 조정하고 `InventoryScreen.tsx`의 분류·정렬·cursor 이동을 유지했다. 웹 typecheck 및 인벤토리 관련 Vitest 3파일 6건이 통과했다. 독립 QA 화면에서 분류 전환과 보석 칸 잠금·해제를 확인했으며 실제 인증 API 인수는 수행하지 않았다.

### 최신 Main 통합 검증

- 2026-09-08 후속 브라우저 인수: 구현 커밋 `9ff9053`을 웹 `http://127.0.0.1:5179`와 API `http://127.0.0.1:18082`에 연결해 신규 격리 DB 계정으로 검증했다. 동일 옵션 보석 3개가 한 칸에 표시되고 칸 전체 잠금이 새로고침 후에도 유지되며 해제된다. 개별 보석 목록·상태 요약은 표시하지 않는다.
- 옥수수 M4 989개 전량 등록 후 갱신된 인벤토리에서 해당 재료가 사라지고 거래소 보유량은 0, 활성 매물은 989개였다. 회수 후 인벤토리에 989개가 돌아왔다. 자동전투가 다른 재료를 지급하므로 전체 슬롯 수 대신 대상 재료의 출고·복원을 확인했다. 데스크톱 보석 상세 스크린샷에서 텍스트 겹침이 없고 확인한 브라우저 오류 로그는 0건이다.
- 위 후속 검증은 아래의 브라우저 미실행 기록 이후 결과다. 최신 통합 화면의 모바일, 던전 전체 흐름, 갱신 경합과 실제 배포는 여전히 미검증이다. 기존 DB·팀 DB는 변경하지 않았다.
- 신규 PostgreSQL DB `hanjjak_main_inventory_verify`에 V25까지 총 24개 마이그레이션 적용 및 API `http://127.0.0.1:18082` 기동 성공. 기존 DB의 마이그레이션 이력은 수정하지 않았다.
- `:modules:inventory:test :modules:market:test :apps:game-api:test :apps:game-api:bootJar` 성공. game-api 38개, inventory 17개, market 24개 테스트 모두 실패·오류·건너뜀 0개다. 테스트 종료 과정에서 임시 DB 연결 종료에 따른 스케줄러 오류 로그는 발생했으며 테스트 실패와 구분한다.
- 최신 main의 게임 세션 선행 조건을 `ChapterCombatE2ETest`의 만석 테스트에 반영하고, `CosmeticSelectorBoxMigrationTest`의 적용 목표를 해당 팀 마이그레이션 V22로 맞췄다. `GemInventoryMigrationTest`의 준비·중간·최종 버전도 V22~V25에 맞췄다.
- API 계약 생성, 웹 타입 검사·프로덕션 빌드 성공 및 웹 22파일 54테스트 통과.
- 신규 DB와 실제 인증 API로 거래소 출고·구매·만료·용량 부족 회수·재시도 중복 방지·보석 칸 잠금 HTTP 스모크 14개를 다시 통과했다. 조건 구성은 신규 검증 계정과 매물에 한정했다.
- 이번 통합 후 브라우저 인수는 재실행하지 않았다. 아래 5178/18081 브라우저 기록은 통합 전 별도 DB 검증 이력이며, 던전 전체 흐름·갱신 경합·실제 배포 검증은 남아 있어 진행 중·부분 검증 상태를 유지한다.

### 이전 검증 이력

- 실제 인증 HTTP 스모크 14개 검사 통과: 등록 후 슬롯 해제, 구매 시 판매자 이중 차감 방지, 만료 후 거래소 재고 유지, 만석 회수 실패 시 무변경, 공간 확보 후 잔량 회수, 등록·구매·회수 재요청 중복 방지, 동일 옵션 보석 묶음과 새 로그인 세션의 칸 잠금 유지. 격리 DB의 신규 검증 계정만 사용했고 만료 시간·만석 조건은 해당 계정/매물에 한해 SQL로 구성했다.
- 실제 브라우저(`127.0.0.1:5178`, API `127.0.0.1:18081`): 재료 989개 전량 등록 시 인벤토리에서 소멸하고 점유 2→1, 회수 후 989개 및 점유 2 복원 확인. 모의 QA 화면이 아닌 세션 인증 서버 연결 결과다.

- 최신 변경: `V25__market_inventory_escrow.sql`, `V26__persistent_gem_slots.sql`, `GemService.lockSlot`, `InventorySlots.instanceGroups`. `GemInventoryIntegrationTest`에 칸 전체 잠금·멱등 재시도·타 계정 차단·잠금 칸과 신규 지급 분리·거래소 출고·회수 초과 무변경 테스트 추가 및 통과.

- 목록 후속 검증: `:modules:inventory:test :apps:game-api:test` 성공. 재료 분할·예약 합계, 보석 값별 분리·개별 ID 보존·페이지 중복 방지 테스트를 추가했다.
- API 계약 생성, 웹 타입 검사·빌드, 웹 18파일 46테스트 통과. 환경의 pnpm 래퍼가 재설치를 시도해 중단되어 기존 설치된 로컬 실행 파일로 동일 작업을 수행했다.

- 후속 중첩 검증: `InventoryServicesTest`의 재료 상한·부분 보상·재시도·보석 그룹 경계 테스트 통과. `GemInventoryIntegrationTest` 9건, `GemInventoryMigrationTest` 6건 포함 game-api 전체 32건 실패·오류 없이 통과했다. 단일/일괄 거래 초과 무변경, 만석 기존 보석 그룹 개봉, 부분 그룹 합성 거절, 이관 속성·잠금 보존과 재료 초과 롤백을 포함한다.
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/InventorySlots.kt`
- 2026-09-08 후속 검증: 인벤토리·거래소·장비·치장 모듈 테스트와 game-api 테스트·bootJar 성공. 스킬 모듈은 독립 테스트 없음. 아래 단계 2 최초 검증 기록은 당시 기준의 이력이다.

- `apps/game-api/src/main/resources/db/migration/V23__gem_instances_to_inventory.sql`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/InventoryInstanceService.kt`
- `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/GemInventoryMigrationTest.kt`: 이관·기존 매핑·초과 롤백·ID 불일치 4건 통과.
- `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/GemInventoryIntegrationTest.kt`: 마지막 상자·초과 무변경·합성·개별 조회·예약 보호 5건 통과.
- 2026-09-08 검증: `:apps:game-api:test`, `:modules:inventory:test`, `:modules:market:test`, `:modules:equipment:test`, `:modules:cosmetics:test`, `:apps:game-api:bootJar` 성공. 스킬 모듈은 독립 테스트가 없으며 CraftingMarketE2ETest가 연계 경로를 검증한다. 기존 로컬 DB와 팀 DB에는 V21을 적용하지 않았다.

- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/InventoryQueryService.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/InventoryReservationService.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/api/InventoryController.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/infrastructure/JdbcInventoryRepository.kt`
- `apps/game-api/src/main/resources/db/migration/V6__inventory_and_reward_commands.sql`
- `packages/contracts/inventory.tsp`
- 검증: `./gradlew.bat :modules:inventory:test`, `pnpm --filter @hanjjak/contracts build`, `./gradlew.bat :apps:game-api:build -x test` 성공

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 진행 중 |
| 검증 | 부분 검증 |
