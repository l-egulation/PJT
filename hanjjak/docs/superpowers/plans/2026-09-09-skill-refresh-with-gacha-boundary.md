# Skill Refresh With Gacha Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the approved multi-grade skill progression, skillbook, combat-profile, API, and web contracts without modifying cosmetic-gacha behavior or ownership.

**Architecture:** Versioned skill content feeds pure `SkillRules`; `SkillService` owns transactional enhance, promote, loadout, and skill-only idempotency. `SkillProfileProvider` adapts persisted skill state to sim-core, while the web renders server-provided action summaries. Cosmetic HMAC entropy, commands, audits, migrations, content, and UI remain independent.

**Tech Stack:** Kotlin 2.2, Spring Boot 3.5, Spring JDBC, Flyway, PostgreSQL/Testcontainers, React 19, TypeScript 7, TanStack Query, Vitest.

## Global Constraints

- Work only in `codex/cosmetic-gacha-main-refresh`.
- Preserve every existing uncommitted cosmetic-gacha change.
- Do not modify `modules/cosmetics`, `apps/web/src/features/cosmetics`, cosmetic controllers/configuration, or cosmetic migrations.
- Skill randomness uses `SkillRollSource`; it never consumes cosmetic `DrawEntropy` or reproduction metadata.
- Skill idempotency remains in `skill_command_record` with the `skill\0...` fingerprint namespace.
- Existing V13 is immutable; add a new skill migration for result versioning.
- Follow `docs/30-domain/character/skills/ssot.md` and `packages/game-content/versions/v1/skills/skills.json` values.
- Every production change follows red-green-refactor.

---

### Task 1: Versioned Skill Content and Pure Rules

**Files:**
- Create: `packages/game-content/schema/skills.schema.json`
- Create: `packages/game-content/versions/v1/skills/skills.json`
- Modify: `packages/game-content/versions/v1/manifest.json`
- Create: `modules/skills/src/main/kotlin/com/hanjjak/skills/domain/SkillContent.kt`
- Modify: `modules/skills/src/main/kotlin/com/hanjjak/skills/domain/Skills.kt`
- Create: `modules/skills/src/test/kotlin/com/hanjjak/skills/domain/SkillRulesTest.kt`

**Interfaces:**
- Produces: `SkillContentLoader.load(): SkillContent`, `SkillRules(content)`, and grade-aware cost, book, effect, access, and profile methods.
- Consumes: `SkillGrade`, `SkillState`, `SkillProfile`, and static item IDs `skillbook:<skillId>:<grade>`.

- [ ] Copy only the rule/content tests into the target worktree.
- [ ] Run `./gradlew :modules:skills:test --tests com.hanjjak.skills.domain.SkillRulesTest` and confirm failure because content/rules are absent.
- [ ] Add schema, versioned JSON, strict loader, and pure rules using the approved SSOT values.
- [ ] Update the v1 manifest checksum for the new files.
- [ ] Re-run the focused rule test and confirm all cases pass.

### Task 2: Transactional Multi-Grade Skill Commands

**Files:**
- Modify: `modules/skills/src/main/kotlin/com/hanjjak/skills/application/SkillService.kt`
- Create: `modules/skills/src/main/kotlin/com/hanjjak/skills/application/LegacySkillResultReader.kt`
- Modify: `modules/skills/src/main/kotlin/com/hanjjak/skills/api/SkillConfiguration.kt`
- Modify: `modules/skills/src/main/kotlin/com/hanjjak/skills/api/SkillController.kt`
- Create: `modules/skills/src/test/kotlin/com/hanjjak/skills/application/SkillRollSourceTest.kt`
- Create: `modules/skills/src/test/kotlin/com/hanjjak/skills/application/LegacySkillResultReaderTest.kt`
- Create: `apps/game-api/src/main/resources/db/migration/V33__version_skill_command_results.sql`
- Create: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/SkillProgressionIntegrationTest.kt`

**Interfaces:**
- Produces: `SkillRollSource`, `SecureSkillRollSource`, `SkillService.enhance`, `SkillService.promote`, `SkillService.updateLoadout`, and `POST /api/v1/skills/{skillId}/promote`.
- Consumes: `InventoryReservationService`, `ItemCatalog`, `SkillRules`, `JdbcClient`, and V13 skill tables.

- [ ] Add roll-source and legacy-reader tests first; run them and confirm missing-symbol failures.
- [ ] Add the integration test contract for atomic resource consumption, promotion, lock, idempotency replay, and rollback.
- [ ] Run focused module tests and compile the integration test; confirm failures from missing multi-grade APIs/result version.
- [ ] Implement injected secure skill rolls, server action summaries, transactional enhance/promote/loadout, and v1 command replay.
- [ ] Add only the new skill result-version migration; do not touch cosmetic migrations.
- [ ] Re-run module tests and compile the integration test to green.

### Task 3: Skillbook and Combat Integration

**Files:**
- Verify/modify only if required: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/domain/DropTable.kt`
- Verify/modify only if required: `modules/inventory/src/main/kotlin/com/hanjjak/inventory/infrastructure/StaticItemCatalog.kt`
- Verify/modify only if required: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/**`
- Test: `modules/inventory/src/test/kotlin/com/hanjjak/inventory/domain/DropTableTest.kt`
- Test: relevant `packages/sim-core` focused skill tests

**Interfaces:**
- Produces: skillbook item IDs shared by drops/catalog/progression and grade-aware `SkillProfile` values.
- Consumes: `SkillRules.profile(states, loadout)`; no cosmetic type or service.

- [ ] Run focused inventory and sim-core tests to establish the current contract.
- [ ] Add a failing test only if boss drop or grade-aware combat behavior is not already covered.
- [ ] Make the minimum skill-owned integration change required; leave already-correct code untouched.
- [ ] Re-run focused inventory and sim-core tests.

### Task 4: Server-Driven Skill Web Contract

**Files:**
- Modify: `apps/web/src/features/skills/api.ts`
- Modify: `apps/web/src/features/skills/api.test.ts`
- Modify: `apps/web/src/features/skills/SkillsScreen.tsx`
- Create/modify: `apps/web/src/features/skills/SkillsScreen.test.tsx`

**Interfaces:**
- Produces: `SkillActionSummary`, `SkillCommand`, stable same-key retry, promote/enhance/loadout calls, and server-driven action rendering.
- Consumes: `GET /api/v1/skills` and the three skill mutation endpoints.

- [ ] Copy/add API and screen behavior tests first; run focused Vitest and confirm failures against the simple contract.
- [ ] Restore grade-aware response types and explicit caller-owned idempotency keys.
- [ ] Render book requirements, rice, probability, target grade, locked/completed states, and uncertain-result retry.
- [ ] Re-run focused Vitest and typecheck.

### Task 5: Integration Evidence and Delivery Documentation

**Files:**
- Modify: `docs/wiki/06-delivery/tasks/H-skills/h-01-chapter-skill-catalog.md`
- Modify: `docs/wiki/06-delivery/tasks/H-skills/h-02-skillbook-drop-tradeability.md`
- Modify: `docs/wiki/06-delivery/tasks/H-skills/h-03-skill-learning-rice-cost.md`
- Modify: `docs/wiki/06-delivery/tasks/H-skills/h-04-skill-loadout-auto-use-priority.md`
- Modify: `docs/wiki/06-delivery/tasks/H-skills/h-05-skill-combat-validation.md`
- Modify: `docs/wiki/06-delivery/tasks/H-skills/_index.md`

**Interfaces:**
- Produces: evidence-backed implementation and verification state; no copied game policy.
- Consumes: actual command outputs and changed paths from Tasks 1–4.

- [ ] Run `./gradlew :modules:skills:test :modules:inventory:test :packages:sim-core:test :apps:game-api:test --tests '*SkillProgressionIntegrationTest'` with Docker/PostgreSQL available.
- [ ] Run `corepack pnpm --filter @hanjjak/web test -- src/features/skills/api.test.ts src/features/skills/SkillsScreen.test.tsx` and `corepack pnpm --filter @hanjjak/web typecheck`.
- [ ] Start the API and web, create/login a local account, and smoke the skills state endpoint and screen.
- [ ] Confirm no cosmetic-owned file changed relative to the preserved pre-skill worktree state.
- [ ] Update H-01–H-05 and the H index with only observed implementation and verification evidence.
