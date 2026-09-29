---
doc_kind: task
owner_domain: delivery
task_code: 'J-11'
task_area: 'J 데이터·테스트·배포'
task_type: '검증'
priority: 'P0'
target_chapters: '챕터 1, 챕터 2, 챕터 3'
planning_status: '완료'
development_status: '완료'
verification_status: '완료'
deadline: ''
source_url: 'https://app.notion.com/p/8d806c8781b0832fb12e81b1f4b28b51'
notion_id: '8d806c87-81b0-832f-b12e-81b1f4b28b51'
snapshot_date: '2026-08-28'
---

# J-11 5일·7일 진행 및 경제 지표 검증

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [데이터·이벤트 및 배포·운영](../../../../40-systems/event-system/ssot.md)를 따른다.

## 완료 기준

5일 P50은 누적 유효 전투 16시간 기준 레벨 44\~46·3챕터 중후반 도달 여부를 검증한다. 7일 P50은 누적 유효 전투 24시간 기준 제작·거래·쌀 잔존 지표를 검증한다. 주문서는 MVP 제외 항목이라 검증 대상에서 제외한다.

## 선행 작업

J-01\~J-06

## 비고

유효 전투 시간 기준은 [진행 SSOT](../../../../30-domain/progression/ssot.md)의 `T_target(L)=6(L-1)+(11/30)(L-1)^2` 목표식을 사용한다. 주문서 제외는 [아이템 SSOT](../../../../30-domain/items/ssot.md)와 [장비 SSOT](../../../../30-domain/items/equipment/ssot.md)를 따른다. `apps:balance-lab`의 P50 경제 시뮬레이터가 5일 레벨 밴드와 7일 영구 장비 해금·강화, 거래, 쌀 잔존 지표를 `build/reports/pre-kafka-j11-validation.json`에 산출한다.

## 증거 링크

- 기준 산출: `build/reports/pre-kafka-j11-validation.json`
- 전투 수치: `build/reports/balance/enemy-v1-working.csv`
- 검증: `Z:\gradlew.bat :apps:balance-lab:test :apps:balance-lab:run -x :modules:stage:jar -x :packages:sim-core:jar --no-daemon --no-configuration-cache --max-workers=1 --no-build-cache`로 영구 장비 모델 이관 후 시뮬레이터를 재검증했다. `Z:\gradlew.bat :modules:account:test :modules:events:test :apps:game-api:test --tests com.hanjjak.gameapi.AccountStorageIntegrationTest --tests com.hanjjak.gameapi.CraftingMarketE2ETest`와 `corepack pnpm --filter @hanjjak/contracts typecheck && corepack pnpm --filter @hanjjak/contracts build`도 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 완료 |
