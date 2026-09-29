---
doc_kind: task
owner_domain: delivery
task_code: 'B-13'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '구현'
verification_status: '부분 검증'
deadline: ''
source_url: ''
notion_id: ''
snapshot_date: '2026-09-12'
---

# B-13 서버 기준 오프라인 보상

> 정책은 [성장 SSOT](../../../../30-domain/progression/ssot.md), [계정·저장 SSOT](../../../../30-domain/player/ssot.md), [Networking](../../../../50-architecture/networking.md)을 따른다.

## 완료 기준

서버 UTC의 마지막 heartbeat·로그아웃 시각과 첫 재로그인 시각으로 오프라인 경과를 계산하고, 1분 이상부터 최대 480분까지 현재 스테이지 1분 자동전투 기대 보상의 50%를 쌀·경험치·주력 재료로 한 번만 수령한다.

## 증거 링크

- 구현: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/OfflineRewardService.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/GameSessionService.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/application/OfflineRewardSettings.kt`
- 저장: `apps/game-api/src/main/resources/db/migration/V50__offline_rewards.sql`
- 계약·클라이언트: `packages/contracts/offline-rewards.tsp`, `packages/client-sdk/src/index.ts`, `apps/web/src/features/runtime/gameSessionClient.ts`, `apps/web/src/features/runtime/RuntimeLifecycle.tsx`, `apps/web/src/features/runtime/OfflineRewardPanel.tsx`. `offlineSeconds`는 점진 배포 호환을 위해 선택 필드로 두고, 구형 응답에서는 웹이 `eligibleSeconds`로 대체한다.
- 검증: `OfflineRewardPolicyTest`, `AccountFlowIntegrationTest`, `gameSessionClient.test.ts`, `OfflineRewardPanel.test.ts`. 정책 테스트는 59초 미지급·60초 지급·분 단위 내림·480분 상한·50% 지급을 검증하고, 통합 테스트는 현재 스테이지 기준과 1분 미만 보상 없음 시나리오를 검증한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 구현 |
| 검증 | 부분 검증 |

## 현재 전체 계정 적용 설정

```text
OFFLINE_REWARD_TEST_ALL_ACCOUNTS=true
OFFLINE_REWARD_TEST_REWARD_MULTIPLIER=0.5
OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS=0
OFFLINE_REWARD_TEST_BUCKET_SECONDS=60
OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS=28800
```

- 실제 오프라인 경과시간은 서버 UTC의 마지막 heartbeat 또는 명시적 로그아웃 시각부터 첫 재로그인 시각까지다.
- 1분 미만이면 보상 작업을 취소하며 UI도 표시하지 않는다.
- 인정 시간은 실제 경과시간을 1분 단위로 내림하고 최대 480분(8시간)으로 제한한다.
- 보상률은 복귀 시점의 현재 스테이지를 1분 자동전투했을 때의 기대 쌀·경험치·주력 재료의 50%다.
- UI는 실제 오프라인 경과시간과 보상 적용시간을 함께 표시한다.
- 같은 탭의 새로고침은 저장된 게임 세션을 heartbeat로 재개하고, 저장값이 없는 새 문서도 1분 미만이면 서버가 기존 활성 세션을 반환하므로 오프라인 보상을 만들지 않는다.

## 구현·검증 기록

- 2026-09-13: 로그아웃에서도 `offline_job`을 유지하고 재로그인 시 결과를 고정하도록 구현했다.
- 2026-09-14: 새로고침 세션 재개, 서버 타임스탬프 기반 실제/적용 시간 UI와 점진 배포 호환을 구현했다.
- 2026-09-14 정책 변경: 1분 미만 미지급, 1분 단위 내림, 최대 480분, 복귀 시점 현재 스테이지 1분 기대 보상의 50%로 전환했다.