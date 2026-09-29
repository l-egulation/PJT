# Stage 1-10 Boss-Only Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make stage 1-10 start with exactly one boss and no normal monsters.

**Architecture:** Derive one `StageDefinition.bossOnly` invariant from the stage ID. Use it in the authoritative Kotlin battle input and stage API, then render a boss-only state in the existing cozy battle HUD.

**Tech Stack:** Kotlin, Kotlin test, React 19, TypeScript, Zustand, Vitest.

## Global Constraints

- Only `stage.01-10` uses `normalCount=0`.
- The other 39 stages keep 20 normal monsters and one boss.
- The server simulator remains authoritative for combat results.
- Preserve existing rendering events and stage content IDs.

---

### Task 1: Lock the battle contract

**Files:**
- Modify: `packages/sim-core/src/test/kotlin/com/hanjjak/sim/CombatSimulatorTest.kt`
- Modify: `modules/battle/src/test/kotlin/com/hanjjak/battle/application/StageBattleServiceTest.kt`

**Interfaces:**
- `CycleInput.normalCount` controls normal encounters.
- `StageBattleService.input("stage.01-10", ...)` produces `normalCount=0`.

- [x] Add a simulator test asserting the first event is `BOSS_SPAWNED`, no normal enemy is defeated, and `defeatedNormals == 0`.
- [x] Add a service test asserting 1-10 uses zero normal encounters while 1-1 keeps 20.
- [x] Run the focused tests and observe the intended failure before production changes.

### Task 2: Implement boss-only stage input

**Files:**
- Modify: `modules/stage/src/main/kotlin/com/hanjjak/stage/domain/Stage.kt`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/StageBattleService.kt`

**Interfaces:**
- `StageDefinition.bossOnly: Boolean`
- `CycleInput.normalCount = if (stage.bossOnly) 0 else 20`

- [x] Add the named stage invariant.
- [x] Use it when issuing the authoritative cycle input.
- [x] Keep simulator event order unchanged so zero normal encounters naturally emit the boss first.

### Task 3: Render the boss-only HUD

**Files:**
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/StageController.kt`
- Modify: `apps/web/src/features/battle/api.ts`
- Modify: `apps/web/src/features/battle/BattleHud.tsx`
- Modify: `apps/web/src/features/battle/BattleScreen.tsx`
- Modify: `apps/web/src/features/battle/PetBattleSurface.tsx`
- Modify: `apps/web/src/features/battle/BattleHistoryScreen.tsx`
- Modify: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleHistoryService.kt`
- Modify: `packages/contracts/main.tsp`
- Modify: `packages/contracts/battle-session.tsp`
- Modify: `apps/web/src/features/battle/BattleHud.css`
- Test: `apps/web/src/features/battle/BattleHud.test.tsx`
- Test: `apps/web/src/features/battle/battlePresentation.test.ts`
- Test: `apps/web/src/features/battle/PetBattleSurface.test.tsx`
- Test: `apps/web/src/features/battle/BattleHistoryScreen.test.tsx`
- Test: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/ChapterCombatE2ETest.kt`
- Test: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/BattleHistoryE2ETest.kt`

**Interfaces:**
- `StageSummary.bossOnly: Boolean` on the server and `boolean` in TypeScript.
- Boss-only HUD text: `보스 1마리`.

- [x] Expose the stage invariant through the stage summary API.
- [x] Replace the normal progress meter with a boss-only state when true.
- [x] Treat zero-normal deaths and retries as boss combat in user-facing copy.
- [x] Keep PIP and battle history on the same boss-only progress convention.
- [x] Verify the HUD omits `0 / 20` for 1-10.

### Task 4: Verify and publish

- [x] Run focused Kotlin tests and game API compilation from an ASCII path.
- [x] Run focused web tests, typecheck, and production build.
- [x] Validate content and visually inspect the boss-only HUD.
- [x] Synchronize the world/combat SSOT, decision log, and D-07/K-06 evidence.
- [x] Resolve the cherry-pick on `fix/stage-1-10-boss` and commit the feature.
