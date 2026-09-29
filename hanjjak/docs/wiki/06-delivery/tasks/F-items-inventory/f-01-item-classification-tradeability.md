---
doc_kind: task
owner_domain: delivery
task_code: 'F-01'
task_area: 'F 드롭·인벤토리'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/1ab06c8781b083069b5281add9d862ce'
notion_id: '1ab06c87-81b0-8306-9b52-81add9d862ce'
snapshot_date: '2026-08-28'
---

# F-01 아이템 ID·분류·거래 속성 정의

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [아이템](../../../../30-domain/items/ssot.md)를 따른다.

## 완료 기준

감자·고구마·옥수수 M1~M4의 `계열_세대` ID를 포함해 재료, 스킬북, 장비의 ID와 거래·중첩·귀속 속성이 확정된다.

## 선행 작업

A-03

## 비고

재료 M1~M4, 스킬북, 보석, 보석함, 치장 선택 상자의 ID와 거래·중첩 속성을 아이템 카탈로그에 구현했다. 2026-09-04 치장 선택 상자를 별도 저장소에서 공용 200슬롯 인벤토리로 옮기면서 표시 분류를 [아이템 SSOT](../../../../30-domain/items/ssot.md)의 다섯 분류와 일치시키고 미사용 `OTHER` 분류를 제거했다. 상자 정의는 치장 콘텐츠의 세트에서 파생하므로 카탈로그가 상자 식별자를 다시 정의하지 않는다.

남은 범위는 두 가지다. 완료 기준이 함께 요구하는 장비 ID는 이 카탈로그가 아니라 장비 도메인이 소유하며 인벤토리 슬롯을 사용하지 않는다. 이 경계는 결정 로그에 등록되지 않았으므로 완료 판정 전에 확인이 필요하다. 그리고 아이템 정의가 아직 콘텐츠 파이프라인 밖의 코드 상수여서 `content:validate` 검증 대상이 아니다.

## 증거 링크

- 최신 검증 결과(2026-09-08): `CosmeticSelectorBoxMigrationTest` 5건, `CosmeticSelectorBoxIntegrationTest` 2건, `CosmeticSelectorBoxItemsTest` 1건이 함께 통과했다. 각 이관 테스트는 임시 PostgreSQL의 독립 스키마를 V19까지 구성한 뒤 기존 데이터를 넣고 실제 Flyway V20을 실행한다. 가득 찬 인벤토리의 기존 스택 수량 합산·예약 수량·획득 순서 보존, 199슬롯에서 새 상자 추가 및 수량 0 원본 무시, 수량 0 기존 스택의 용량 검사, 활성 빈 슬롯 예약에 따른 거절·롤백·해제 후 재시도, STACK_RIGHT 예약의 중복 슬롯 계산 방지를 확인했다. 아래 이전 기록에서 미완료로 남겼던 기존 데이터·예약 경계 이관과 HTTP 검증은 이 테스트 및 로컬 HTTP 검증으로 보완했다. 과거 V15가 이미 적용된 DB의 변환 절차와 팀 공용·운영 DB 이력 확인은 여전히 별도 작업이다. F-01 전체 상태는 비고의 미해결 범위 때문에 부분 구현·부분 검증을 유지한다.
- 이관 회귀 테스트: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/CosmeticSelectorBoxMigrationTest.kt`
- 2026-09-08 복구: `origin/main`의 `a70c3ea`를 통합하고 이관 번호를 V20으로 변경했다. 이관 용량 검사에 활성 빈 슬롯 예약을 포함하고, 새 마일스톤 조회를 인벤토리로 전환했다. 상자가 없는 세트는 카탈로그에서 제외하고 미정 표시명은 기존 클라이언트의 세트 번호 표시를 사용한다.
- 검증: 인벤토리·거래소·치장 모듈 테스트, game-api 빌드(앱 통합 테스트 제외)와 테스트 소스 컴파일, TypeSpec 생성 및 생성물 일치, 전체 typecheck, 웹 44건·콘텐츠 4건 테스트가 통과했다. 인벤토리 경계·중복 정의 회귀 테스트 3건을 추가했다.
- 최종 재검증: `CosmeticSelectorBoxItemsTest`와 game-api assemble이 통과했다. 최신 콘텐츠의 상자 매핑·표시명·분류·거래 속성을 확인했고 빌드 리소스에서 중복 V15가 제거되고 V20이 포함됨을 확인했다.
- `content:validate`는 통과했다. Windows CRLF로 던전 파일의 바이트 체크섬만 달라져 생성된 manifest의 해당 값을 되돌렸다. LF 정규화 해시가 원본 manifest와 일치함을 확인했다. 줄바꿈에 무관한 콘텐츠 CI 게이트 개선은 이번 범위 밖이다.
- DB 검증: 최초 실행은 Docker 미연결로 실패했으나, 2026-09-08 Docker 기동 후 `:apps:game-api:test --tests com.hanjjak.gameapi.CosmeticSelectorBoxIntegrationTest` 재실행이 성공했다. 임시 PostgreSQL에서 V20까지 적용하고 상자 수령·조회·개봉 및 용량 부족 시 마일스톤 롤백 2건을 검증했다. 기존 데이터·예약 경계 이관과 HTTP 종단 검증은 미완료다.
- 기존 개발 DB 확인: `infra/local/compose.yaml`의 `local-postgres-1`을 데이터 보존 상태로 시작하고 이력을 조회했다. `hanjjak` DB에는 과거 V15 `cosmetic selector box to inventory`가 이미 적용되어 현재 main의 V15와 충돌한다. 기존 DB에 새 이관, repair, 초기화는 수행하지 않았다. 별도 개발 DB를 사용하거나 백업 후 별도 복구 절차를 검토해야 한다.
- 로컬 HTTP 검증: 사용자 승인으로 같은 PostgreSQL 안에 `hanjjak_local_verify`를 생성했다. 공통 설정 변경 없이 실행 인자 `--spring.datasource.url=jdbc:postgresql://localhost:5432/hanjjak_local_verify --server.port=18081`로 서버를 실행하고 V20까지 19개 이관 적용 및 health UP을 확인했다. 회원가입·재료 선택 후 검증 계정에만 치장 해금과 400회 마일스톤 조건을 SQL로 준비했다. 실제 HTTP 상자 2개 수령·조회·1개 개봉과 동일 Idempotency-Key 재전송이 성공했다. DB에서 상자 잔량 1, 선택 치장 `cosmetic-061` 등록 수량 1을 확인했다. 해금까지의 플레이와 400회 뽑기는 이 검증에 포함하지 않았다. 검증 DB는 보존하며 기존 `hanjjak`과 팀 공용·운영 DB는 변경하지 않았다. 기존 데이터·예약 경계 이관 검증은 여전히 남아 있다.
- 복구 코드: `apps/game-api/src/main/resources/db/migration/V22__cosmetic_selector_box_to_inventory.sql`, `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/CosmeticSelectorBoxIntegrationTest.kt`, `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/CosmeticSelectorBoxItemsTest.kt`

- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/Inventory.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/infrastructure/StaticItemCatalog.kt`
- `modules/inventory/src/main/kotlin/com/hanjjak/inventory/application/InventoryPorts.kt`
- `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/CosmeticSelectorBoxItems.kt`
- `packages/contracts/inventory.tsp`
- `apps/web/src/features/inventory/InventoryScreen.tsx`
- 검증: 2026-09-04 `./gradlew :modules:inventory:test :modules:market:test :modules:cosmetics:test`와 `./gradlew :apps:game-api:build -x test` 성공. TypeSpec 재생성 결과가 `packages/contracts/generated/openapi/openapi.yaml`과 일치함을 확인했다. PostgreSQL 마이그레이션 실행과 브라우저 종단 검증은 실행하지 않았다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
