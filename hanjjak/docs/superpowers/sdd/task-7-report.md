# Task 7 evidence: atomic raid confirmation and claims

## Implemented

- `RaidRules` owns the exact eight personal rewards and four success/failure rank bands, including failure REST `6/4/5,600`.
- `RaidService.confirmAttempt` materializes RUNNING confirmation from the persisted server timeline prefix and uses the full stored result for RESULT_HELD. Zero damage is rejected before asset/raid mutation.
- Transaction order is account → session/state/slot/attempt/contribution → wallet → cosmetic ticket → inventory. Preflight runs before credit; caller-lock asset methods do not reacquire account or increment `stateVersion`.
- A successful confirmation credits ticket/rice/gem boxes, persists confirmed result/contribution/slot/attempt, resumes a live handoff, increments account stateVersion once, publishes Outbox, and saves command replay atomically.
- `claimReward` reads claim identity, preflights asset locks, then locks/validates the claim after assets. Credit, CLAIMED transition, stateVersion, Outbox, and command replay commit together; failure leaves CLAIMABLE.
- No production no-op reward port or no-op cosmetic ticket credit remains.

## TDD and focused evidence

- RED: `RaidRewardServiceTest` failed before `RaidRules` existed.
- GREEN: `RaidRewardServiceTest` exhaustively checks all 8 personal grades and all success/failure bands; Task 6 lifecycle tests remain part of the affected suite.
- Real PostgreSQL `RaidRewardIntegrationTest` contains 15 scenarios and passes from ASCII `Y:`:
  - positive tick 0, tick 49, tick 50, and held terminal-result materialization;
  - live exact-handoff resume and terminalizer-versus-confirm race;
  - zero-damage refusal;
  - capacity, wallet, ticket, late inventory, contribution, and Outbox rollback;
  - concurrent same-key confirmation and claim once-only;
  - complete multi-claim response state and reused claim-key rejection.
- Review acceptance command selecting reward rules, all 15 PostgreSQL reward scenarios, and Spring lifecycle transaction tests completed `BUILD SUCCESSFUL` in 51s with 43 executed tasks.
- Final affected bundle ran full raid, wallet, inventory, and cosmetics module tests plus reward/lifecycle/Spring/migration integration selections: `BUILD SUCCESSFUL` in 1m 25s, 49 executed Gradle tasks.

## Status

Task 7 confirmation/claims are implemented and focused-verified. L-03 remains `구현 중` / `부분 검증` because Task 8 daily settlement and rank materialization remain.
