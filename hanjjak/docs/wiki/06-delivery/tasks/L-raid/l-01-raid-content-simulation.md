---
doc_kind: task
owner_domain: delivery
task_code: 'L-01'
task_area: 'L 레이드'
task_type: '데이터'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: ''
notion_id: ''
snapshot_date: '2026-09-13'
---

# L-01 레이드 전투·콘텐츠 수치 산출

> 작업 상태와 완료 증거의 SSOT. 게임 규칙은 [레이드 SSOT](../../../../30-domain/raid/ssot.md)를 따른다.

## 완료 기준

결정론 개인 전투와 레이드 콘텐츠를 구현하고, 기준·부족 빌드 시뮬레이션으로 보스 절대 수치와 등급 피해 커트라인을 산출해 콘텐츠 검증을 통과한다.

## 선행 작업

E-02, E-05, G-08, H-05, J-14
## 증거 링크

- 승인 설계: `docs/superpowers/specs/2026-09-13-daily-seal-raid-design.md`
- 구현 계획: `docs/superpowers/plans/2026-09-13-daily-seal-raid.md`
- 결정론 전투와 레이드 수치 산출: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/RaidSimulator.kt`, `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/RaidBalance.kt`
- 기준 입력·콘텐츠·스키마: `apps/balance-lab/src/main/resources/raid-reference-build-v1.json`, `packages/game-content/versions/v1/raids/raids.json`, `packages/game-content/schema/raids.schema.json`
 - 검증: `cmd.exe /d /c gradlew.bat :apps:balance-lab:test --tests com.hanjjak.balancelab.RaidBalanceTest -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1` 통과(BUILD SUCCESSFUL, 1m 41s, 15 tasks). `:apps:balance-lab:raidBalance`도 통과(BUILD SUCCESSFUL, 22s)하고 `build/reports/balance/raid-mvp-v1-working.json`을 생성했다.

## Task 11 장기 분포·경제 보고서

기준 및 부족 빌드 각각 seed `1..1000`(빌드당 1,000개)을 동일한 `RaidSimulator.simulate` 입력으로 재생했다. 아래 값은 생성 보고서의 exact 값이며 damage 단위는 피해량, survival 단위는 ticks(100ms)다.

| 빌드 | damage P10/P50/P90 | survival P10/P50/P90 | 등급 분포 (PARTICIPATION/D/C/B/A/S/SS/SSS) | B 성공 | 5분 cap |
| --- | --- | --- | --- | --- | --- |
| reference | 19,871 / 19,871 / 19,871 | 590 / 590 / 590 | 0 / 0 / 0 / 1,000 / 0 / 0 / 0 / 0 | 1,000/1,000 (100.0%) | 0 |
| attack Q0 | 18,480 / 18,480 / 18,480 | 590 / 590 / 590 | 0 / 0 / 1,000 / 0 / 0 / 0 / 0 / 0 | 0/1,000 (0.0%) | 0 |
| HP Q0 | 18,968 / 18,968 / 18,968 | 560 / 560 / 560 | 0 / 0 / 1,000 / 0 / 0 / 0 / 0 / 0 | 0/1,000 (0.0%) | 0 |
| penetration Q0 | 18,479 / 18,479 / 18,479 | 590 / 590 / 590 | 0 / 0 / 1,000 / 0 / 0 / 0 / 0 / 0 | 0/1,000 (0.0%) | 0 |

`earlyDamage`/`lateDamage` remain 3,355/3,096 (reference), 3,120/2,880 (attack Q0), 3,355/2,193 (HP Q0), 3,120/2,879 (penetration Q0). The reference and all deficiencies are deterministic across replay; the working content remains unchanged.

### Population and supply projections

The report uses the reference grade mix and projects 3 reward confirmations per account. Per-account/day is `21 tickets, 12 gem boxes, 21,750 rice`; server/day includes rank rewards. Seal success is evaluated against the fixed 50,000 target.

| active | seal contribution | success probability | account/day (tickets/boxes/rice) | server/day (tickets/boxes/rice) | rank distribution (1/2–10/11–100/rest) | auto-claim backlog |
| ---: | ---: | ---: | --- | --- | --- | ---: |
| 36 | 8,388 | 0.0% | 21 / 12 / 21,750 | 983 / 577 / 1,017,500 | 1 / 9 / 26 / 0 | 0 |
| 72 | 16,776 | 0.0% | 21 / 12 / 21,750 | 1,955 / 1,153 / 2,027,300 | 1 / 9 / 62 / 0 | 0 |
| 144 | 33,552 | 0.0% | 21 / 12 / 21,750 | 3,899 / 2,305 / 4,016,100 | 1 / 9 / 90 / 44 | 0 |
| 1,000 | 233,000 | 100.0% | 21 / 12 / 21,750 | 29,111 / 17,011 / 29,861,000 | 1 / 9 / 90 / 900 | 800 |

Three participation confirmations alone supply exactly **15 tickets, 9 gem boxes, and 15,000 rice** before rank rewards. The projection is evidence only; no reward table or seal policy was changed.
## 검증 한계

`sim-core` 전체 테스트는 `:packages:sim-core:test`로 BUILD SUCCESSFUL(19s)했다. `modules:raid:test`는 36개 중 9개 `JdbcRaidRepositoryTest`가 `JdbcRaidRepositoryTest.kt:327`에서 `IllegalStateException`으로 실패했으며, PostgreSQL/Testcontainers 런타임 부재에 따른 실패다. 정책·콘텐츠 수치는 변경하지 않고 L-01 검증을 부분으로 유지한다. 실제 브라우저 smoke와 PostgreSQL settlement/claim 부하(지연·lock wait·deadlock/retry)는 Docker 부재로 관찰하지 못했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
