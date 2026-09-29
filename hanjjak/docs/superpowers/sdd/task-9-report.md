# Task 9: Wire Handwritten API Adapters and Game-API Integration

Implemented the handwritten raid HTTP adapter and composition-root query adapter. Task 10 web work remains out of scope.

## Delivered

- Added `/api/v1/raid` query and command routes with session authentication, UUID idempotency headers, command envelopes, and no-store responses.
- State responses now contain content-derived seal target/progress/success-pending values plus ranking and claims views.
- Ranking uses open-session contributions until settlement and immutable final ranks after settlement, with tie-safe cursors and current-account contribution/rank.
- Claims honor cursor pagination and expose claimable/claimed totals; command responses map confirmation to the contract's `RaidStateView`.
- Success envelopes read the account's persisted `stateVersion`; API errors normalize internal raid transition errors to declared contract codes and include nullable `details`.
- Added focused `RaidApiIntegrationTest` contract coverage for anonymous access, idempotency validation, authenticated state/ranking/claims envelopes, nullable fields, cursor handling, timeline serialization, settled/stale behavior, and start/retry/confirm/discard/practice command shapes. Authenticated cases are compile-only (`@Disabled`) because their shared Spring fixture requires Docker PostgreSQL.

## Verification

- `npm --prefix packages/contracts run test` — PASS; TypeSpec compilation and raid OpenAPI assertions passed.
- `cmd.exe /d /c gradlew.bat :apps:game-api:compileTestKotlin --no-daemon` — BUILD SUCCESSFUL after expanded `RaidApiIntegrationTest` coverage.
- `cmd.exe /d /c gradlew.bat :modules:raid:test --no-daemon` — task reached test execution; nine JDBC repository tests failed during TestDatabase/Docker setup at `JdbcRaidRepositoryTest.kt:327` because Docker is unavailable. No HTTP behavior pass is claimed.

## Commit

`5072f4e9 fix(raid): complete task 9 API contract`

