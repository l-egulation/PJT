# Task 11 Verification Report

Date: 2026-09-14

## Deterministic distribution and economy

`apps/balance-lab` generated `build/reports/balance/raid-mvp-v1-working.json` from seeds `1..1000` per reference, attack-Q0, HP-Q0, and penetration-Q0 build. The report records P10/P50/P90 damage and survival, grade counts, B success rate, five-minute cap count, and active populations 36/72/144/1,000.

Reference: damage `19,871/19,871/19,871`; survival `590/590/590` ticks; B `1,000/1,000 (100.0%)`; grades `B=1,000`; five-minute cap `0`.

Attack Q0: damage `18,480/18,480/18,480`; survival `590/590/590`; B `0/1,000`; grades `C=1,000`; five-minute cap `0`.

HP Q0: damage `18,968/18,968/18,968`; survival `560/560/560`; B `0/1,000`; grades `C=1,000`; five-minute cap `0`.

Penetration Q0: damage `18,479/18,479/18,479`; survival `590/590/590`; B `0/1,000`; grades `C=1,000`; five-minute cap `0`.

Population seal contributions for 36/72/144/1,000 active participants are `8,388/16,776/33,552/233,000`; success probabilities against 50,000 are `0/0/0/100%`. Account/day is `21 tickets, 12 gem boxes, 21,750 rice`; server/day is respectively `983/577/1,017,500`, `1,955/1,153/2,027,300`, `3,899/2,305/4,016,100`, and `29,111/17,011/29,861,000`. Rank distributions are `1/9/26/0`, `1/9/62/0`, `1/9/90/44`, `1/9/90/900`; auto-claim backlog is `0/0/0/800`. Three participation confirmations supply exactly `15 tickets, 9 gem boxes, 15,000 rice` before rank rewards.

## Named load scenarios

`RaidLongRunIntegrationTest` is the only enabled named scenario and executes the deterministic 1,000-seed replay. `RaidLongRunDatabaseLoadTest` and `RaidSettlementConcurrencyTest` are Docker-gated, compile-only scaffolding: they retain schema assertions and placeholder HTTP/load setup, but do not provide live burst, contention, ranking, or replay-storm evidence.

## Verification lanes

- `cmd.exe /d /c gradlew.bat :apps:balance-lab:test --tests com.hanjjak.balancelab.RaidBalanceTest -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1`: BUILD SUCCESSFUL (1m41s).
- `cmd.exe /d /c gradlew.bat :apps:balance-lab:raidBalance -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1`: BUILD SUCCESSFUL (22s).
- `cmd.exe /d /c gradlew.bat :packages:sim-core:test -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1`: BUILD SUCCESSFUL (19s).
- `cmd.exe /d /c gradlew.bat :apps:game-api:testClasses -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1`: BUILD SUCCESSFUL (1m02s).
`RaidLongRunIntegrationTest` deterministic replay: BUILD SUCCESSFUL; 1 enabled test executed (1,000 seeds replayed twice). Database load and contention classes remain compile-only scaffolding because Docker/PostgreSQL is unavailable; their schema assertions and placeholder HTTP/load setup were not run against a live database.
- TypeSpec contracts build/test and OpenAPI assertions: passed (3.34s).
- Content validator: 12/12 tests and validate passed (2.74s).
- Web: 70 files/405 tests, typecheck, and Vite build passed (13.02s); expected API `ECONNREFUSED` noise on localhost:3000 was emitted.

`modules:raid:test` was attempted once. 36 tests completed; 9 `JdbcRaidRepositoryTest` cases failed at `JdbcRaidRepositoryTest.kt:327` with `IllegalStateException` because the Docker/Testcontainers PostgreSQL fixture was unavailable. Docker-dependent settlement metrics (transaction latency, lock wait, deadlocks/retries, duplicate count) and actual browser smoke remain unobserved. No Docker troubleshooting or repeated attempts were made.

## Server deployment attempt

- `main` received `55cd30de` after the raid migrations were renumbered to V56/V57/V58 to avoid collisions with main-owned V53/V54.
- GitLab main pipeline for the current deployment commit remains `pending`; required verify jobs have no assigned runner despite the project runners being registered. Image and `deploy-production` jobs therefore did not start. The earlier pipeline that failed on the pre-fix migration collision is historical evidence only; the collision was fixed before the current main commit.
- Local Kubernetes has no configured context, and local Docker reports that `dockerDesktopLinuxEngine` is unavailable. No deployment job was bypassed.
- The selected rollout scope is code deployment with the raid feature locked. Content remains `working`; PostgreSQL/browser runtime validation and production deployment remain pending.


No raid policy, reward table, Flyway version, or content authority was changed. Raid content remains `working`; task verification remains partial where PostgreSQL/browser evidence is unavailable.
