---
doc_kind: task
owner_domain: delivery
task_code: 'A-05'
task_area: 'A 배포 범위'
task_type: '기획'
priority: 'P0'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
source: 'user-discussion'
created_at: '2026-09-10'
---

# A-05 진행·성장 리밸런싱

## 책임

차기 리밸런싱의 설계 확정·콘텐츠 산출·구현·인수 상태를 소유한다. 정책은 [설계 진입점](../../../../70-plans/progression-rebalance/README.md)의 책임 SSOT를 따른다. 신규 저장소 작업으로 Notion 원문 식별자는 부여하지 않는다.

## 상태와 범위

기획 완료, 개발 완료, 검증 부분 검증. `progression-rebalance-v1`의 1~4장·1~79레벨 콘텐츠, 버전 선택, 장비 비용, 최초 클리어 저장·지급·수령함, 기존 계정 backfill 명령, 전투 응답과 웹 화면을 구현했다. 적용 콘텐츠와 기본 런타임 버전은 `progression-rebalance-v1`이며, 300계정 축소 시뮬레이션·격리 PostgreSQL·실제 브라우저로 신규 계정의 즉시 지급 경로를 확인했다. 10,000계정 시뮬레이션, live pending·공간 부족·수령·동시성, backfill apply/replay와 전체 회귀 suite는 아직 완료하지 않았다. 5~10장·80레벨 이후·전설·신화·4-10 신규 보상·레이드 보상은 후속 범위다.

## 완료 기준

- `progression-rebalance-v1`의 1~4장 콘텐츠·계약·저장·서버·클라이언트를 구현한다.
- 신규 최초 클리어·기존 계정 소급·pending 수령의 멱등성·동시성·복원을 검증한다.
- [검증 메모](../../../../70-plans/progression-rebalance/verification.md)의 P50·P90 진행, 기준 빌드 성공률, 승급 시점과 시장 표본을 검증한다.
- 구현·시뮬레이션·PostgreSQL·브라우저 증거를 기록하고서만 개발·검증 상태를 갱신한다.

## 선행 및 영향 작업

[A-03](./a-03-finalize-chapter-content-map.md), [G-07](../G-equipment-crafting/g-07-crafting-enhancement-economy-validation.md), [G-09](../G-equipment-crafting/g-09-equipment-replacement-balance.md)의 기존 증거는 보존한다. 이번 구현의 현재 상태와 차기 검증 공백은 이 작업이 소유하며, 기존 작업의 완료 상태를 이번 리밸런싱의 완료 증거로 재해석하지 않는다.

## 증거

- 구현 기준: `46d03ae3`, `e197dcf0`, `109ecf48`, `a74f25b5`, `c0406a18`. 콘텐츠·런타임·응답·레이아웃 보정까지 반영했다.
- 콘텐츠·검증 경로: `packages/game-content/versions/progression-rebalance-v1`, `tools/content-validator`, `apps/balance-lab`, [진행 CSV](../../../../70-plans/mvp-release/verification/progression-rebalance-v1.csv), [경제 JSON](../../../../70-plans/mvp-release/verification/progression-rebalance-v1-economy.json), [검증 메모](../../../../70-plans/progression-rebalance/verification.md).
- 서버·저장·계약 경로: `apps/game-api/src/main/resources/db/migration/V52__account_balance_version.sql`, `V53__first_clear_reward_inbox.sql`, `modules/battle`, `modules/equipment`, `modules/progression`, `packages/contracts/first-clear-rewards.tsp`, `packages/contracts/battle-session.tsp`.
- 웹 경로: `apps/web/src/features/first-clear-rewards`, `apps/web/src/features/battle`, `apps/web/src/navigationState.ts`, `apps/web/src/main.tsx`.
- 2026-09-12 자동 검증: `ProgressionRebalanceTest` 7개와 `EconomySimulationTest` 3개를 포함한 balance-lab 전체 테스트, 300계정 `progression-rebalance --write`, content-validator, TypeSpec compile, web 전체 58파일 331테스트·typecheck·production build가 통과했다.
- 2026-09-12 적용 콘텐츠 검증 보강: 기준 시간 표본과 account 경제 표본이 모두 `stages.json`의 실제 20일반+보스/보스 단독 편성을 `CombatSimulator`로 실행한다. 2026-09-14 재검증 기준 300개 기준 빌드 표본의 P50은 70.6·245.6·519.5·891.7분, P90은 70.7·245.7·519.7·891.9분이며 성장벽 12개 기준 빌드 성공률 90~100%, 부족 빌드 0%를 기록했다. 실제 보유 레벨·장비·최초 해금 스킬을 사용하는 account 경제 표본의 4-10 P50은 거래 이용 899.0분, 거래 미이용 1,168.8분, 유동성 부족 897.5분이다(보정 이전 M1 비용으로 측정한 값이며, 보정된 비용에서의 재측정은 10,000계정 재시뮬레이션 완료 후로 미룬다).
- 2026-09-12 PostgreSQL·브라우저: merge 전 격리 DB `hanjjak_progression_qa`에 당시 migration 45개를 v47까지 적용했다. 최신 main 통합 후 progression migration은 충돌 없는 `V52`·`V53`으로 재배치했다. 신규 가입·주력 재료 선택·자동전투에서 1-1·1-2 reward row가 stage별 한 건씩 생성되고 둘 다 `CLAIMED`였으며, 새 응답은 `grantedItems`·`pendingItems`를 포함하고 구형 `items`를 노출하지 않았다. 쌀·M1 세 계열과 액티브·패시브 스킬의 Normal 1 해금, 다음 stage 진행을 확인했다.
- 2026-09-12 rollout·backfill 회귀: migration trigger 선설치, rollout provisional account, active legacy battle session 전환 보류, `4-10` 무보상 clear, 실패 account 뒤 batch 전진, page 단위 dry-run과 bulk inventory snapshot을 PostgreSQL·단위 테스트로 검증했다.
- 2026-09-12 UI: 공용 `첫 클리어 보상` 진입, empty state, reload 후 화면 복원, 390×844와 1440×960에서 패널 전체 표시와 horizontal overflow 없음, first-clear completion 뒤 blank-screen 회귀 제거를 확인했다.
- 운영 배포 경로는 `infra/k8s/deploy.sh`가 migration 후 호환 game-api·web rollout/smoke를 완료하고 `backfill-progression-rewards --apply --until-complete --batch-size=100` Job을 실행한다. backfill 실패 시 새 API를 유지하고 resumable Job을 재시도한다.
- 2026-09-13 `StageFirstClearE2ETest` CI 보정: 테스트 전용 `PostgreSQLContainer` 강제 초기화를 제거하고 공용 `PostgresTestDatabase.schema("stage_first_clear")`를 사용해 CI에서는 `TEST_DATABASE_*` PostgreSQL 서비스, 로컬에서는 Testcontainers fallback을 사용하도록 통합했다. ASCII 경로에서 `:apps:game-api:test --tests com.hanjjak.gameapi.StageFirstClearE2ETest --rerun-tasks`의 최초 클리어 2개 E2E 시나리오가 통과했다.
- 2026-09-13 운영 배포의 progression backfill 실패를 단계적으로 복구했다. NetworkPolicy에 `backfill` DB ingress·egress가 빠져 최초 연결이 거부됐고, 다음 두 실행에서는 non-web context가 servlet `SecurityConfiguration`과 그 안의 `ApiSecurityProperties` 등록에 의존해 시작하지 못했다. 네트워크 정책을 보완하고 servlet 전용 설정을 조건부로 제한하며 공통 account 구성이 properties를 등록하도록 수정했다. 실제 backfill은 78계정 중 77계정을 `progression-rebalance-v1`으로 전환했고, 실행 중인 legacy 전투 1계정은 `deferredAccountIds`로 보존한 채 작업이 성공했다. 보류 계정은 세션 종료 뒤 같은 resumable backfill로 전환한다. 상세 job·배포 증거는 [J-08](../J-delivery-operations/j-08-production-build-cicd.md)에 기록했다.
- 2026-09-14 적 수치 보존 회귀: 최신 `main`의 임시 콘텐츠 루트 생성 경로에 `progression-rebalance-v1/stages/stages.json`·`cosmetics/cosmetics.json` 본문 비변경 assertion을 추가했다. 이번 변경은 적 HP·공격력·방어력, 경험치·쌀, 장비 비용, 재료 드롭 수치를 수정하지 않는다. content-validator와 300계정 결정론 시뮬레이션으로 기존 적용 수치와 성장벽 결과를 다시 검증했다.
- 미완료: Windows commit 한도 오류로 10,000계정 실행을 중단하고 300계정으로 축소했다. 운영 backfill 보류 1계정의 세션 종료 후 최종 전환, 전체 server suite와 live pending·수령 동시성 검증이 남아 있다. `/api/v1/chat/messages`의 기존 403/NPE는 이번 progression 범위와 분리된 미해결 회귀다.
- 2026-09-15 M1 등급 비감소 보정 재검증: `595f4e10`·`3a9f63b5`·`4c0023df`·`60703c4f`로 상향한 희귀·영웅 M1 총량, 승급·강화 재료, 재조정한 1~4장 최초 클리어 지원을 `:modules:equipment:test`(26건)·`:modules:inventory:test`(29건)와 content-validator로 재검증했으며, `pnpm -r typecheck`는 이번 변경과 무관하게 raid 콘텐츠 병합 이전부터 있던 `tools/content-validator`의 기존 타입 오류로 실패했다.
- 2026-09-15 파밍 비용 증가 수용: 밸런스 오너는 출고 콘텐츠와 불변 챕터 4 드롭표에서 도출한 지원 보상 제외 순수 파밍 M1(계열당, 6부위 완성 기준)이 희귀 1,500→11,100, 영웅 630→43,260으로 늘어난 것을 의도된 증가로 수용했다. 거래소를 통한 계열 간 교환을 전제하면 6부위 풀강 소요시간은 노말 2.8시간(변화 없음), 희귀 8.3시간(병목 M2)→15.4시간(병목 M1, 약 1.9배), 영웅 17.5시간(병목 M3)→60시간(병목 M1, 약 3.4배)으로 늘어나며, M1이 이제 노말·희귀·영웅 전 등급에서 단독 병목이 된 것이 이번 보정의 의도한 결과다. 분포 꼬리 확인은 A-05가 이미 추적 중인 10,000계정 재시뮬레이션에서 수행한다.
