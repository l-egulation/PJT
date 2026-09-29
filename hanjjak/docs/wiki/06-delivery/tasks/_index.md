---
doc_kind: task-index
owner_domain: delivery
authority_level: ssot-for-task-status
snapshot_date: '2026-08-28'
---

# A–L 작업 색인

Notion 식별자를 가진 작업 87개와 저장소에서 추가한 작업 10개, 총 97개를 관리한다. 상태·완료 기준·증거는 각 작업 문서가 소유하고, 규칙은 도메인 SSOT가 소유한다. 운영 배포의 Compose→K3s 전환은 J-08~J-10의 기존 식별자를 유지한다.

작업은 변경되지 않는 `A-01` 형식의 식별자를 유지하며, 디렉터리와 파일명에는 사람이 의미를 파악할 수 있는 영문 slug를 함께 쓴다. 예: `B-account-storage/b-01-signup-login.md`.

## 작업 흐름

A 배포 범위 → B 계정·저장 → C A/B/C 전문 → D 스테이지 → E 자동전투·스탯 → F 드롭·인벤토리 → G 장비·제작·강화 → H 스킬·자동 사용 → I 거래소·경제 → J 데이터·테스트·배포 → K UI·UX → L 레이드

| 영역 | 작업 색인 |
| --- | --- |
| A 배포 범위 | [A 배포 범위 작업](./A-release-scope/_index.md) |
| B 계정·저장 | [B 계정·저장 작업](./B-account-storage/_index.md) |
| C A/B/C 전문 | [C A/B/C 전문 작업](./C-professions/_index.md) |
| D 스테이지 | [D 스테이지 작업](./D-stages/_index.md) |
| E 자동전투·스탯 | [E 자동전투·스탯 작업](./E-auto-combat/_index.md) |
| F 드롭·인벤토리 | [F 드롭·인벤토리 작업](./F-items-inventory/_index.md) |
| G 장비·제작·강화 | [G 장비·제작·강화 작업](./G-equipment-crafting/_index.md) |
| H 스킬·자동 사용 | [H 스킬·자동 사용 작업](./H-skills/_index.md) |
| I 거래소·경제 | [I 거래소·경제 작업](./I-marketplace-economy/_index.md) |
| J 데이터·테스트·배포 | [J 데이터·테스트·배포 작업](./J-delivery-operations/_index.md) |
| K 클라이언트 UI·UX | [K 클라이언트 UI·UX 작업](./K-client-ux/_index.md) |
| L 레이드 | [L 레이드 작업](./L-raid/_index.md) |

| 코드 | 작업명 | 영역 | 유형 | 우선순위 | 기획 | 개발 | 검증 | 문서 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A-05 | A-05 진행·성장 리밸런싱 | A 배포 범위 | 기획 | P0 | 완료 | 완료 | 부분 검증 | [열기](./A-release-scope/a-05-progression-rebalance.md) |
| A-01 | A-01 챕터 1\~4 배포 범위 동결 | A 배포 범위 | 기획 | P0 | 미확인 | 미확인 | 미확인 | [열기](./A-release-scope/a-01-freeze-mvp-scope.md) |
| A-02 | A-02 1차 배포 포함·제외 기능 확정 | A 배포 범위 | 기획 | P0 | 미확인 | 미확인 | 미확인 | [열기](./A-release-scope/a-02-define-mvp-inclusions-exclusions.md) |
| A-03 | A-03 챕터별 콘텐츠 매핑표 확정 | A 배포 범위 | 데이터 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./A-release-scope/a-03-finalize-chapter-content-map.md) |
| A-04 | A-04 1차 배포 인수 기준 작성 | A 배포 범위 | 검증 | P0 | 미확인 | 미확인 | 미확인 | [열기](./A-release-scope/a-04-define-release-acceptance-scenario.md) |
| B-01 | B-01 회원가입·로그인 흐름 구현 | B 계정·저장 | 개발 | P0 | 미확인 | 완료 | 완료 | [열기](./B-account-storage/b-01-signup-login.md) |
| B-02 | B-02 사용자·캐릭터 기본 데이터 모델 정의 | B 계정·저장 | 데이터 | P0 | 미확인 | 완료 | 완료 | [열기](./B-account-storage/b-02-user-character-data-model.md) |
| B-03 | B-03 스테이지 진행도 저장·복구 구현 | B 계정·저장 | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./B-account-storage/b-03-stage-progress-save-restore.md) |
| B-04 | B-04 인벤토리·영구 장비 상태 저장 구현 | B 계정·저장 | 개발 | P0 | 완료 | 완료 | 부분 검증 | [열기](./B-account-storage/b-04-inventory-equipment-save.md) |
| B-05 | B-05 저장 요청 중복·복구 정책 구현 | B 계정·저장 | 개발 | P1 | 미확인 | 부분 구현 | 부분 검증 | [열기](./B-account-storage/b-05-save-idempotency-recovery.md) |
| B-06 | B-06 단일 게임 상태 엔진·웹 화면 상태 공유 구현 | B 계정·저장 | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./B-account-storage/b-06-shared-game-state-window-sync.md) |
| B-07 | B-07 브라우저 로컬 실행 상태 복원 구현 | B 계정·저장 | 개발 | P0 | 미확인 | 완료 | 부분 검증 | [열기](./B-account-storage/b-07-local-sqlite-runtime-restore.md) |
| B-08 | B-08 서버 스테이지 진행·거래 권한과 로컬 성장·실행 상태 분리 구현 | B 계정·저장 | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./B-account-storage/b-08-local-server-data-authority.md) |
| B-09 | B-09 전투 성장 보상 지급 구현 | B 계정·저장 | 개발 | P0 | 부분 완료 | 완료 | 부분 검증 | [열기](./B-account-storage/b-09-battle-progression-reward-loop.md) |
| B-13 | B-13 서버 기준 오프라인 보상 | B 계정·저장 | 개발 | P0 | 완료 | 구현 | 부분 검증 | [열기](./B-account-storage/b-13-offline-reward.md) |
| C-01 | C-01 주력 재료 명칭·식별자 확정 | C 주력 재료 | 기획 | P0 | 완료 | 미확인 | 완료 | [열기](./C-professions/c-01-profession-names-materials.md) |
| C-02 | C-02 최초 주력 재료 선택 규칙·화면 정의 | C 주력 재료 | 기획 | P0 | 완료 | 완료 | 완료 | [열기](./C-professions/c-02-initial-profession-selection.md) |
| C-03 | C-03 주력 재료 80:10:10 드롭 비율 적용 | C 주력 재료 | 데이터 | P0 | 완료 | 완료 | 완료 | [열기](./C-professions/c-03-profession-exclusive-production.md) |
| C-04 | C-04 주력 재료 선택·영구 저장 구현 | C 주력 재료 | 개발 | P0 | 완료 | 완료 | 완료 | [열기](./C-professions/c-04-profession-selection-persistence.md) |
| D-01 | D-01 스테이지·챕터 데이터 스키마 정의 | D 스테이지 | 데이터 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./D-stages/d-01-stage-chapter-schema.md) |
| D-02 | D-02 챕터 1 스테이지 1-1\~1-10 데이터 작성 | D 스테이지 | 데이터 | P0 | 미확인 | 완료 | 완료 | [열기](./D-stages/d-02-chapter-1-stage-data.md) |
| D-03 | D-03 챕터 2 스테이지 2-1\~2-10 데이터 작성 | D 스테이지 | 데이터 | P0 | 미확인 | 완료 | 완료 | [열기](./D-stages/d-03-chapter-2-stage-data.md) |
| D-04 | D-04 챕터 3 스테이지 3-1~3-10 데이터 작성 | D 스테이지 | 데이터 | P0 | 미확인 | 완료 | 완료 | [열기](./D-stages/d-04-chapter-3-stage-data.md) |
| D-05 | D-05 파밍·보스 진행 규칙 정의 | D 스테이지 | 기획 | P0 | 미확인 | 미확인 | 미확인 | [열기](./D-stages/d-05-farming-boss-progression.md) |
| D-06 | D-06 챕터 1→2→3→4 해금·진행 검증 | D 스테이지 | 검증 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./D-stages/d-06-chapter-unlock-validation.md) |
| D-07 | D-07 일반 20마리·보스 1마리 연속 사이클 구현 | D 스테이지 | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./D-stages/d-07-normal-boss-cycle.md) |
| E-01 | E-01 자동 이동·조우·전투 사이클 구현 | E 자동전투·스탯 | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./E-auto-combat/e-01-auto-combat-cycle.md) |
| E-02 | E-02 공격·방어·HP 피해 계산 구현 | E 자동전투·스탯 | 개발 | P0 | 미확인 | 완료 | 부분 검증 | [열기](./E-auto-combat/e-02-damage-calculation.md) |
| E-04 | E-04 HP 유지·사이클 회복·실패 처리 구현 | E 자동전투·스탯 | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./E-auto-combat/e-04-hp-recovery-failure.md) |
| E-05 | E-05 챕터별 전투 수치 시뮬레이션 | E 자동전투·스탯 | 검증 | P1 | 완료 | 완료 | 완료 | [열기](./E-auto-combat/e-05-chapter-combat-simulation.md) |
| E-06 | E-06 장시간 자동전투 안정성 검증 | E 자동전투·스탯 | 검증 | P1 | 미확인 | 미확인 | 미확인 | [열기](./E-auto-combat/e-06-long-running-combat-stability.md) |
| F-01 | F-01 아이템 분류·거래 속성 정의 | F 드롭·인벤토리 | 기획 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./F-items-inventory/f-01-item-classification-tradeability.md) |
| F-03 | F-03 선택 전문 재료 단독 드롭 적용 | F 드롭·인벤토리 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./F-items-inventory/f-03-profession-material-drops.md) |
| F-04 | F-04 인벤토리 조회·정렬·수량 처리 구현 | F 드롭·인벤토리 | 개발 | P0 | 완료 | 진행 중 | 부분 검증 | [열기](./F-items-inventory/f-04-inventory-query-sort-quantity.md) |
| F-05 | F-05 보상 지급 원자성·중복 방지 구현 | F 드롭·인벤토리 | 개발 | P0 | 완료 | 진행 중 | 부분 검증 | [열기](./F-items-inventory/f-05-atomic-reward-idempotency.md) |
| F-06 | F-06 획득 결과·인벤토리 UI 검증 | F 드롭·인벤토리 | 검증 | P1 | 완료 | 진행 중 | 부분 검증 | [열기](./F-items-inventory/f-06-reward-inventory-ui-validation.md) |
| G-01 | G-01 장비 6부위·영구 성장 상태 스키마 정의 | G 장비·제작·강화 | 데이터 | P0 | 완료 | 완료 | 부분 검증 | [열기](./G-equipment-crafting/g-01-equipment-slot-state-schema.md) |
| G-02 | G-02 장비 최초 해금·등급 비용 정책 반영 | G 장비·제작·강화 | 데이터 | P0 | 완료 | 완료 | 부분 검증 | [열기](./G-equipment-crafting/g-02-chapter-equipment-recipes.md) |
| G-06 | G-06 영구 장비 적용과 구형 기능 제거 | G 장비·제작·강화 | 개발 | P1 | 완료 | 완료 | 부분 검증 | [열기](./G-equipment-crafting/g-06-equip-lock-dismantle-tradeability.md) |
| G-04 | G-04 영구 장비 해금·강화·승급 비용 차감 구현 | G 장비·제작·강화 | 개발 | P0 | 완료 | 완료 | 부분 검증 | [열기](./G-equipment-crafting/g-04-equipment-crafting-material-consumption.md) |
| G-07 | G-07 영구 장비 비용·성장 경제 검증 | G 장비·제작·강화 | 검증 | P1 | 완료 | 완료 | 부분 검증 | [열기](./G-equipment-crafting/g-07-crafting-enhancement-economy-validation.md) |
| G-08 | G-08 6부위 등급·강화·Q 반영 | G 장비·제작·강화 | 기획 | P0 | 완료 | 완료 | 부분 검증 | [열기](./G-equipment-crafting/g-08-enhancement-slots-stat-table.md) |
| G-09 | G-09 반복 재제작 대체와 영구 성장 검증 | G 장비·제작·강화 | 검증 | P1 | 완료 | 완료 | 부분 검증 | [열기](./G-equipment-crafting/g-09-equipment-replacement-balance.md) |
| H-01 | H-01 챕터 1\~4 스킬·스킬북 목록 확정 | H 스킬·자동 사용 | 데이터 | P1 | 부분 완료 | 부분 구현 | 부분 검증 | [열기](./H-skills/h-01-chapter-skill-catalog.md) |
| H-02 | H-02 스킬북 드롭·거래 속성 적용 | H 스킬·자동 사용 | 개발 | P1 | 완료 | 완료 | 부분 검증 | [열기](./H-skills/h-02-skillbook-drop-tradeability.md) |
| H-03 | H-03 스킬 성장·쌀 차감 구현 | H 스킬·자동 사용 | 개발 | P1 | 부분 완료 | 부분 구현 | 부분 검증 | [열기](./H-skills/h-03-skill-learning-rice-cost.md) |
| H-04 | H-04 스킬 장착·자동 사용 우선순위 구현 | H 스킬·자동 사용 | 개발 | P1 | 부분 완료 | 부분 구현 | 부분 검증 | [열기](./H-skills/h-04-skill-loadout-auto-use-priority.md) |
| H-05 | H-05 액티브·버프·패시브 전투 검증 | H 스킬·자동 사용 | 검증 | P2 | 완료 | 부분 구현 | 부분 검증 | [열기](./H-skills/h-05-skill-combat-validation.md) |
| I-01 | I-01 매수·매도 주문·체결·정산 데이터 모델 정의 | I 거래소·경제 | 데이터 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./I-marketplace-economy/i-01-listing-trade-settlement-model.md) |
| I-02 | I-02 매도 주문 등록·수정·취소·만료 구현 | I 거래소·경제 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./I-marketplace-economy/i-02-listing-search-cancel.md) |
| I-03 | I-03 매수·매도 IOC와 부분 체결 구현 | I 거래소·경제 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./I-marketplace-economy/i-03-instant-partial-purchase-settlement.md) |
| I-04 | I-04 판매 완료 수수료 10% 적용 | I 거래소·경제 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./I-marketplace-economy/i-04-sale-fee.md) |
| I-05 | I-05 주문 동시성·우선순위·멱등성 구현 | I 거래소·경제 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./I-marketplace-economy/i-05-concurrent-purchase-idempotency.md) |
| I-06 | I-06 양방향 호가·최근 체결 집계 | I 거래소·경제 | 데이터 | P1 | 완료 | 부분 구현 | 부분 검증 | [열기](./I-marketplace-economy/i-06-market-price-aggregation.md) |
| I-07 | I-07 주문 남용·자전거래·가격 이상 탐지 | I 거래소·경제 | 검증 | P1 | 부분 완료 | 미확인 | 미확인 | [열기](./I-marketplace-economy/i-07-wash-trading-price-anomaly-detection.md) |
| I-08 | I-08 매수 지정가·자동 체결·통합 수령 구현 | I 거래소·경제 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./I-marketplace-economy/i-08-buy-order-reservation.md) |
| J-01 | J-01 공통 게임·경제 이벤트 스키마 정의 | J 데이터·테스트·배포 | 데이터 | P0 | 완료 | 완료 | 완료 | [열기](./J-delivery-operations/j-01-game-economy-event-schema.md) |
| J-02 | J-02 전투·드롭·제작·거래 이벤트 수집 구현 | J 데이터·테스트·배포 | 개발 | P0 | 부분 완료 | 부분 구현 | 부분 검증 | [열기](./J-delivery-operations/j-02-domain-event-collection.md) |
| J-03 | J-03 경제 지표 집계 작업 구현 | J 데이터·테스트·배포 | 데이터 | P1 | 완료 | 구현 | 검증 | [열기](./J-delivery-operations/j-03-economy-metrics-aggregation.md) |
| J-04 | J-04 계정·저장 통합 테스트 | J 데이터·테스트·배포 | 검증 | P0 | 완료 | 완료 | 검증 | [열기](./J-delivery-operations/j-04-account-storage-integration-test.md) |
| J-05 | J-05 챕터 1\~4 전투 E2E 테스트 | J 데이터·테스트·배포 | 검증 | P0 | 완료 | 완료 | 검증 | [열기](./J-delivery-operations/j-05-chapter-combat-e2e-test.md) |
| J-06 | J-06 제작·강화·거래 E2E 테스트 | J 데이터·테스트·배포 | 검증 | P0 | 완료 | 완료 | 검증 | [열기](./J-delivery-operations/j-06-crafting-market-e2e-test.md) |
| J-07 | J-07 거래 동시성·부하 테스트 | J 데이터·테스트·배포 | 검증 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./J-delivery-operations/j-07-marketplace-concurrency-load-test.md) |
| J-08 | J-08 운영 빌드·CI/CD 구성 | J 데이터·테스트·배포 | 배포 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./J-delivery-operations/j-08-production-build-cicd.md) |
| J-09 | J-09 모니터링·백업·롤백 구성 | J 데이터·테스트·배포 | 배포 | P1 | 미확인 | 부분 구현 | 부분 검증 | [열기](./J-delivery-operations/j-09-monitoring-backup-rollback.md) |
| J-11 | J-11 성장·경제 지표 검증 | J 데이터·테스트·배포 | 검증 | P1 | 완료 | 완료 | 완료 | [열기](./J-delivery-operations/j-11-progression-economy-metrics-validation.md) |
| J-10 | J-10 운영 환경 배포 인수 테스트 | J 데이터·테스트·배포 | 검증 | P0 | 미확인 | 미확인 | 부분 검증 | [열기](./J-delivery-operations/j-10-production-acceptance-test.md) |
| J-12 | J-12 P10·P50·P90 투자 전략 대시보드 구현 | J 데이터·테스트·배포 | 데이터 | P1 | 미확인 | 미확인 | 미확인 | [열기](./J-delivery-operations/j-12-investment-strategy-dashboard.md) |
| J-13 | J-13 코드베이스 기반 구축 | J 데이터·테스트·배포 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./J-delivery-operations/j-13-codebase-foundation.md) |
| J-14 | J-14 보석 MVP 수직 슬라이스 구현 | J 데이터·테스트·배포 | 개발 | P1 | 부분 완료 | 부분 구현 | 부분 검증 | [열기·최신 main 통합·DB 재검증 증거](./J-delivery-operations/j-14-gems-mvp-vertical-slice.md#증거-링크) |
| J-16 | J-16 어드민 콘솔 인증·사용자·거래소 운영 | J 데이터·테스트·배포 | 개발 | P1 | 부분 완료 | 부분 구현 | 부분 검증 | [열기·전체 사용자/카탈로그 선택·단일 매물 UX 검증](./J-delivery-operations/j-16-admin-console-operations-planning.md#증거-링크) |
| K-01 | K-01 전체 화면 흐름·정보 구조 확정 | K 클라이언트 UI·UX | 기획 | P0 | 미확인 | 미확인 | 미확인 | [열기](./K-client-ux/k-01-screen-flow-information-architecture.md) |
| K-02 | K-02 공통 디자인 시스템·컴포넌트 정의 | K 클라이언트 UI·UX | 기획 | P0 | 부분 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-02-design-system-components.md) |
| K-03 | K-03 공통 레이아웃·내비게이션·상단 정보 구현 | K 클라이언트 UI·UX | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-03-layout-navigation-status.md) |
| K-04 | K-04 회원가입·로그인·첫 진입 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 미확인 | 완료 | 완료 | [열기](./K-client-ux/k-04-auth-first-entry-ui.md) |
| K-06 | K-06 자동전투 HUD·전투 피드백 구현 | K 클라이언트 UI·UX | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-06-auto-combat-hud-feedback.md) |
| K-07 | K-07 챕터·스테이지 선택과 진행도 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-07-chapter-stage-progress-ui.md) |
| K-08 | K-08 전투 결과·보상·실패 안내 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-08-combat-result-reward-failure-ui.md) |
| K-09 | K-09 인벤토리·아이템 상세 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-09-inventory-item-detail-ui.md) |
| K-10 | K-10 영구 장비 성장 카드 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 완료 | 부분 검증 | [열기](./K-client-ux/k-10-equipment-crafting-ui.md) |
| K-11 | K-11 영구 장비 강화·승급 UI 구현 | K 클라이언트 UI·UX | 개발 | P1 | 완료 | 완료 | 부분 검증 | [열기](./K-client-ux/k-11-enhancement-equipment-sale-ui.md) |
| K-12 | K-12 스킬 학습·장착·자동 사용 순서 UI 구현 | K 클라이언트 UI·UX | 개발 | P1 | 부분 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-12-skill-management-ui.md) |
| K-13 | K-13 거래소 품목 탐색·양방향 호가창 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-13-marketplace-search-price-ui.md) |
| K-14 | K-14 간편 거래·주문 관리·수령·정산 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-14-listing-management-settlement-ui.md) |
| K-15 | K-15 로딩·빈 상태·오류·확인·알림 패턴 구현 | K 클라이언트 UI·UX | 개발 | P0 | 미확인 | 미확인 | 미확인 | [열기](./K-client-ux/k-15-loading-empty-error-feedback.md) |
| K-16 | K-16 해상도·접근성·입력 사용성 검증 | K 클라이언트 UI·UX | 검증 | P1 | 미확인 | 미확인 | 미확인 | [열기](./K-client-ux/k-16-resolution-accessibility-input-validation.md) |
| K-18 | K-18 단일 탭 펫 UI·관리 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-18-pet-management-tray-windows.md) |
| K-19 | K-19 백그라운드 탭 진행·브라우저 수명주기 검증 | K 클라이언트 UI·UX | 검증 | P0 | 미확인 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-19-hidden-window-state-sync-validation.md) |
| K-20 | K-20 저피로 방치 UX 원칙 검증 | K 클라이언트 UI·UX | 검증 | P1 | 미확인 | 미확인 | 미확인 | [열기](./K-client-ux/k-20-low-fatigue-idle-ux-validation.md) |
| K-21 | K-21 치장 뽑기·도감 수직 슬라이스 구현 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 완료 | 부분 검증 | [열기](./K-client-ux/k-21-cosmetics-vertical-slice.md) |
| K-22 | K-22 캐릭터 창 풀스택 구현 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 완료 | 부분 검증 | [열기](./K-client-ux/k-22-character-window.md) |
| K-23 | K-23 브라우저 환경 설정 UI 구현 | K 클라이언트 UI·UX | 개발 | P1 | 부분 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-23-browser-settings-ui.md) |
| K-24 | K-24 장비 부족 재료 거래소 구매 UI 구현 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-24-equipment-shortage-market-purchase-ui.md) |
| K-25 | K-25 장비 성장 cozy-pixel UI 개편 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 완료 | 부분 검증 | [열기](./K-client-ux/k-25-equipment-growth-cozy-pixel-redesign.md) |
| K-26 | K-26 광장 채팅 가독성 UI 보정 | K 클라이언트 UI·UX | 개발 | P0 | 완료 | 완료 | 부분 검증 | [열기](./K-client-ux/k-26-chat-readability-ui.md) |
| K-27 | K-27 점진형 온보딩·캐릭터 대화 가이드 구현 | K 클라이언트 UI·UX | 개발 | P0 | 부분 완료 | 부분 구현 | 부분 검증 | [열기](./K-client-ux/k-27-progressive-onboarding-dialogue.md) |
| L-01 | L-01 레이드 전투·콘텐츠 수치 산출 | L 레이드 | 데이터 | P0 | 완료 | 완료 | 부분 검증 | [열기](./L-raid/l-01-raid-content-simulation.md) |
| L-02 | L-02 일일 세션·개인 도전 상태 구현 | L 레이드 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./L-raid/l-02-raid-session-attempt-persistence.md) |
| L-03 | L-03 정산·순위·보상 구현 | L 레이드 | 개발 | P0 | 완료 | 구현 완료 | 부분 검증 | [열기](./L-raid/l-03-raid-settlement-ranking-rewards.md) |
| L-04 | L-04 API·웹 수직 슬라이스 구현 | L 레이드 | 개발 | P0 | 완료 | 부분 구현 | 부분 검증 | [열기](./L-raid/l-04-raid-api-web-vertical-slice.md) |
