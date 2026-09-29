# Character Cosmetics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the character window and cosmetics/collection slice on the existing server structure, replacing the obsolete 30-item runtime draft with a validated 66-item placeholder catalog and exposing milestone and selector-box flows in the web UI.

**Architecture:** Keep `modules/cosmetics`, existing JDBC tables, command idempotency, and the current character dialog. Perform a clean content-contract cutover: stable numbered IDs, nullable display assets, grade-specific thresholds, stat-ID effects, and no collection-tier compatibility fields. Reuse the existing gacha, milestone, selector-box, registration, and equipment commands; add only the read models required by the UI.

**Tech Stack:** Kotlin 2.2, Spring Boot 3.5, Spring JDBC, TypeSpec, React 19, TanStack Query, Vitest, Testing Library, JSON Schema/Ajv.

## Global Constraints

- Catalog contains exactly 66 cosmetics and 11 six-slot sets: NORMAL 4, RARE 3, EPIC 3, LEGENDARY 1.
- Stable IDs are `cosmetic-001` through `cosmetic-066`; only those IDs are permanent.
- `displayName` and `imageUrl` are nullable; UI fallbacks are `치장 #001`, `세트 #01`, and `이미지 준비 중`.
- Draw costs are fixed content values: 5,000 rice for one draw and 50,000 rice for ten draws.
- Grade probabilities are NORMAL 500,000, RARE 350,000, EPIC 140,000, LEGENDARY 10,000 millionths.
- Star thresholds are grade-specific: NORMAL `1,480,720,1200,2400`; RARE `1,168,252,420,840`; EPIC `1,134,202,336,672`; LEGENDARY `1,2,3,5,10`.
- No collection tier, collection reward, or collection combat effect exists.
- No compatibility layer for the obsolete 30-item development catalog; development data may be reset before release.
- Actual art, actual names, marketplace linkage, PostgreSQL HTTP E2E, concurrency load tests, and browser E2E are outside this plan.
- Completion state after required tests: development complete, verification partial.

---

### Task 1: Propagate the approved content decision

**Files:**
- Modify: `docs/60-content/cosmetics/mvp-v1.md`
- Modify: `docs/80-decisions/README.md`
- Modify: `docs/70-plans/character-cosmetics-collection/integration-plan.md`
- Modify: `docs/70-plans/cosmetic-gacha/requirements.md`
- Modify: `docs/30-domain/cosmetics/ssot.md`

**Interfaces:**
- Consumes: approved design `docs/superpowers/specs/2026-09-07-character-cosmetics-implementation-first-design.md`.
- Produces: one policy baseline for Tasks 2–7: 66 IDs, 11 sets, fixed draw cost, grade thresholds, nullable display assets.

- [ ] **Step 1: Replace superseded policy text**

Update the content SSOT so it owns the following exact values and nothing else duplicates them:

```text
singleRiceCost = 5000
NORMAL sets = 4
RARE sets = 3
EPIC sets = 3
LEGENDARY sets = 1
cosmetic IDs = cosmetic-001..cosmetic-066
displayName = unresolved nullable metadata
imageUrl = unresolved nullable metadata
```

Remove the old `stage.02-10` five-minute price rule, 48-item/8-set catalog, concrete names, and aggregate eight-set totals. Preserve the six-slot invariant and grade-specific threshold table.

- [ ] **Step 2: Update decision status and requirement links**

Record a 2026-09-07 confirmed decision that supersedes the 2026-09-02 catalog size/names/cost basis while preserving the draw, registration, set-star, milestone, and selector-box rules. Update plan/requirement documents to link to the content SSOT instead of repeating obsolete values.

- [ ] **Step 3: Verify document consistency**

Run:

```bash
git diff --check
grep -R "48종\|8세트\|stage.02-10.*5분\|노릇노릇 전집" docs/30-domain/cosmetics docs/60-content/cosmetics docs/70-plans/character-cosmetics-collection docs/70-plans/cosmetic-gacha docs/80-decisions/README.md
```

Expected: `git diff --check` exits 0; grep returns only historical/superseded context explicitly labeled as such, not current policy.

- [ ] **Step 4: Commit**

```bash
git add docs/30-domain/cosmetics/ssot.md docs/60-content/cosmetics/mvp-v1.md docs/70-plans/character-cosmetics-collection/integration-plan.md docs/70-plans/cosmetic-gacha/requirements.md docs/80-decisions/README.md
git commit -m "docs: confirm numbered cosmetics catalog"
```

---

### Task 2: Cut over the content schema and catalog

**Files:**
- Modify: `packages/game-content/schema/cosmetics.schema.json`
- Modify: `packages/game-content/versions/v1/cosmetics/cosmetics.json`
- Modify: `tools/content-validator/src/validate.ts`
- Modify: `tools/content-validator/src/validate.test.ts`
- Generated: `packages/game-content/versions/v1/manifest.json`

**Interfaces:**
- Produces JSON fields:
  - `singleRiceCost: 5000`
  - `gradeProbabilityMillionths: Record<CosmeticGrade, number>`
  - `starThresholdsByGrade: Record<CosmeticGrade, [number, number, number, number, number]>`
  - `cosmetics: Array<{ cosmeticId, displayName: string|null, imageUrl: string|null, grade, slot, setId }>`
  - `sets: Array<{ setId, displayName: string|null, bannerId: string|null, boxItemId: string|null, members, effects }>`
- Removes `collectionTiers` and the fixed 30-item/2-set constraints.

- [ ] **Step 1: Write failing validator tests**

Add fixtures/assertions that require:

```ts
expect(content.cosmetics).toHaveLength(66);
expect(content.sets).toHaveLength(11);
expect(countSetsByGrade(content)).toEqual({ NORMAL: 4, RARE: 3, EPIC: 3, LEGENDARY: 1 });
expect(content.cosmetics.map(item => item.cosmeticId)).toEqual(
  Array.from({ length: 66 }, (_, index) => `cosmetic-${String(index + 1).padStart(3, "0")}`),
);
expect(content.singleRiceCost).toBe(5000);
```

Also test that null `displayName`/`imageUrl` pass, missing/duplicate slots fail, malformed grade thresholds fail, and collection tiers are rejected as additional properties.

- [ ] **Step 2: Run validator tests and confirm failure**

Run:

```bash
corepack pnpm --filter @hanjjak/content-validator test
```

Expected: FAIL because the current schema requires 30 cosmetics, two sets, one global threshold list, and collection tiers.

- [ ] **Step 3: Replace schema and JSON catalog**

Use 11 sequential set IDs `cosmetic-set-01` through `cosmetic-set-11`. For the development content version, assign sequential six-item blocks and six slots in this exact order:

```ts
const slots = ["HEAD", "TOP", "BOTTOM", "GLOVES", "SHOES", "CAPE"];
const grades = [
  ...Array(4).fill("NORMAL"),
  ...Array(3).fill("RARE"),
  ...Array(3).fill("EPIC"),
  "LEGENDARY",
];
```

Only `cosmetic-set-11` has `bannerId: "cosmetic-banner-01"` and `boxItemId: "cosmetic-selector-box-01"`; other sets use null for those fields. Set every `displayName` and `imageUrl` to null. Add five cumulative effect rows per set using the approved existing grade effect progression; do not invent collection effects.

- [ ] **Step 4: Rewrite semantic validation**

`validateCosmeticReferences` must check exact numbered IDs, set grade counts, one grade per set, all cosmetics belonging to exactly one set, all six slots once per set, exactly one legendary banner/box mapping, grade-specific thresholds, and stat-effect units. Delete collection-tier validation and the rule that lower grades cannot belong to sets.

- [ ] **Step 5: Run content tests and validator**

Run:

```bash
corepack pnpm --filter @hanjjak/content-validator test
corepack pnpm --filter @hanjjak/content-validator validate
```

Expected: all tests pass; manifest checksum updates; validator exits 0.

- [ ] **Step 6: Commit**

```bash
git add packages/game-content/schema/cosmetics.schema.json packages/game-content/versions/v1/cosmetics/cosmetics.json packages/game-content/versions/v1/manifest.json tools/content-validator/src/validate.ts tools/content-validator/src/validate.test.ts
git commit -m "feat(content): add numbered cosmetics catalog"
```

---

### Task 3: Replace legacy cosmetics domain shapes

**Files:**
- Modify: `modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/domain/Cosmetics.kt`
- Modify: `modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/domain/CosmeticsRules.kt`
- Modify: `modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/infrastructure/JsonCosmeticsContent.kt`
- Modify: `modules/cosmetics/src/test/kotlin/com/hanjjak/cosmetics/domain/CosmeticsRulesTest.kt`
- Create: `modules/cosmetics/src/test/kotlin/com/hanjjak/cosmetics/infrastructure/JsonCosmeticsContentTest.kt`

**Interfaces:**
- Produces `enum class EffectUnit { BASIS_POINTS, PERCENTAGE_POINTS, SECONDS }`.
- Produces `data class StatEffect(val statId: String, val value: Int, val unit: EffectUnit)`.
- Produces `CosmeticsContent.starThresholdsByGrade: Map<CosmeticGrade, List<Int>>`.
- `CosmeticDefinition.displayName` and catalog display names become nullable.
- Removes `CollectionTier`, `collectionTier`, `collectionEffect`, and `LEGACY_FLAT`.

- [ ] **Step 1: Rewrite rule tests to the new observable contract**

Test grade-specific thresholds:

```kotlin
assertEquals(2, CosmeticsRules.star(500, content.thresholds(CosmeticGrade.NORMAL)))
assertEquals(5, CosmeticsRules.star(10, content.thresholds(CosmeticGrade.LEGENDARY)))
```

Test that an 11-set snapshot uses each set’s six-member minimum star, returns no collection-tier fields, and sums effects by `statId + unit`. Test a null display name survives the catalog model without conversion to fake content.

- [ ] **Step 2: Run domain tests and confirm failure**

Run:

```bash
cmd.exe /c gradlew.bat :modules:cosmetics:test --tests "com.hanjjak.cosmetics.domain.CosmeticsRulesTest"
```

Expected: compilation/test failure because the old model exposes one global threshold list and flat three-axis effects.

- [ ] **Step 3: Implement the minimal model and rules cutover**

Use the definition grade to select thresholds in snapshot and registration:

```kotlin
val thresholds = content.starThresholdsByGrade.getValue(definition.grade)
val currentStar = star(state.registeredQuantity, thresholds)
```

Aggregate effects without losing units:

```kotlin
val totalEffects = setEffects.values.flatten()
    .groupBy { it.statId to it.unit }
    .map { (key, rows) -> StatEffect(key.first, rows.sumOf { it.value }, key.second) }
```

- [ ] **Step 4: Update JSON loader and loader tests**

Parse nullable metadata with `takeUnless(JsonNode::isNull)`, parse grade thresholds, nullable non-legendary banner/box IDs, and effect arrays. Reject content whose set membership, grade counts, thresholds, or one legendary mapping violate the plan.

- [ ] **Step 5: Run cosmetics tests**

Run:

```bash
cmd.exe /c gradlew.bat :modules:cosmetics:test
```

Expected: all cosmetics tests pass.

- [ ] **Step 6: Commit**

```bash
git add modules/cosmetics/src/main modules/cosmetics/src/test
git commit -m "refactor(cosmetics): use grade-aware catalog rules"
```

---

### Task 4: Align services, character stats, and API contracts

**Files:**
- Modify: `modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/application/CosmeticsService.kt`
- Modify: `modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/application/CosmeticGachaService.kt`
- Modify: `modules/cosmetics/src/main/kotlin/com/hanjjak/cosmetics/application/CosmeticsWallet.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/JdbcCosmeticsWallet.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/AccountBattleStatsProvider.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/AccountBattleSkillProvider.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculator.kt`
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsService.kt`
- Modify: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`
- Modify: `packages/contracts/cosmetics.tsp`
- Modify: `packages/contracts/main.tsp`
- Generated: `packages/contracts/generated/openapi/openapi.yaml`
- Test: `modules/cosmetics/src/test/kotlin/com/hanjjak/cosmetics/application/CosmeticsServiceTest.kt`
- Test: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculatorTest.kt`
- Test: `packages/sim-core/src/test/kotlin/com/hanjjak/sim/PermanentBonusTest.kt`

**Interfaces:**
- `CosmeticsService.register` selects thresholds from the cosmetic definition grade.
- `CosmeticGachaService.banners` emits only sets with non-null `bannerId` and `boxItemId`.
- Produces a milestone read model containing `totalSuccessfulDraws`, `claimedBoxCount`, `claimableBoxCount`, `drawsUntilNextBox`, `boxItemId`, and `ownedBoxQuantity`.
- TypeSpec makes display names nullable and effects generic by stable stat ID.

- [ ] **Step 1: Add failing service and calculation tests**

Cover:

```kotlin
assertEquals(1, gacha.banners(accountId).size)
assertEquals(5_000, gacha.banners(accountId).single().singleRiceCost)
assertEquals(1, milestone.claimableBoxCount)
assertEquals(0, milestone.drawsUntilNextBox)
```

Also assert NORMAL registration uses the normal thresholds, LEGENDARY uses legendary thresholds, percentage effects reach the character stat calculator, and permanent haste/basic-attack effects still reach combat without active buffs.

- [ ] **Step 2: Run focused tests and confirm failure**

Run:

```bash
cmd.exe /c gradlew.bat :modules:cosmetics:test :apps:game-api:test :packages:sim-core:test
```

Expected: failure against the legacy flat model and missing milestone query.

- [ ] **Step 3: Add milestone reads to the existing wallet adapter**

Add one read method to `CosmeticsWallet`, implemented in `JdbcCosmeticsWallet`, that reads `cosmetic_banner_progress` and `cosmetic_selector_box`. Keep claim/open writes unchanged. Calculate:

```text
claimableBoxCount = floor(totalSuccessfulDraws / 200) - claimedBoxCount
drawsUntilNextBox = 200 - (totalSuccessfulDraws mod 200), except 0 when claimableBoxCount > 0
```

Return the read model from banner summary/detail so the client needs no local milestone arithmetic.

- [ ] **Step 4: Align character and combat effects**

Map `attackPercent`, `maxHpPercent`, `defensePenetrationPercent`, `criticalChancePoint`, `attackSpeedPercent`, `basicAttackDamagePercent`, and `buffDurationSeconds` into the existing shared character calculation. Reject unknown IDs rather than silently treating them as zero. Preserve round-half-up and combat caps from the combat SSOT.

- [ ] **Step 5: Update TypeSpec and regenerate OpenAPI**

Replace legacy models with nullable names, generic effects, set-effect arrays/maps, no collection-tier fields, and milestone/box quantities. Run:

```bash
corepack pnpm --filter @hanjjak/contracts build
```

Expected: TypeSpec compiles and generated OpenAPI changes only through generation.

- [ ] **Step 6: Run focused server tests**

Run:

```bash
cmd.exe /c gradlew.bat :modules:cosmetics:test :apps:game-api:test :packages:sim-core:test
```

Expected: all focused tests pass.

- [ ] **Step 7: Commit**

```bash
git add modules/cosmetics apps/game-api/src/main apps/game-api/src/test packages/sim-core packages/contracts
git commit -m "feat(cosmetics): align catalog and character effects"
```

---

### Task 5: Finish the character-window fallback UI

**Files:**
- Modify: `apps/web/src/features/character/api.ts`
- Modify: `apps/web/src/features/character/CharacterWindow.tsx`
- Modify: `apps/web/src/features/character/CosmeticPanels.tsx`
- Modify: `apps/web/src/features/character/character.css`
- Modify: `apps/web/src/features/character/CharacterWindow.test.tsx`
- Modify: `apps/web/src/features/character/StatsPanel.test.tsx`
- Modify: `apps/web/src/main.tsx`

**Interfaces:**
- Consumes nullable `displayName`, nullable `imageUrl`, generic effects, and collection responses from Task 4.
- Produces helpers:

```ts
export const cosmeticFallbackName = (id: string) => `치장 #${id.slice(-3)}`;
export const setFallbackName = (id: string) => `세트 #${id.slice(-2)}`;
```

- [ ] **Step 1: Add failing component tests**

Assert that a null-named `cosmetic-001` renders `치장 #001`, a null-named `cosmetic-set-01` renders `세트 #01`, missing image renders `이미지 준비 중`, and the stable ID remains visible separately. Retain tests for dialog focus, initial stats tab, preview-versus-equip, lock state, content mismatch, and uncertain command retry.

- [ ] **Step 2: Run character tests and confirm failure**

Run:

```bash
corepack pnpm --filter @hanjjak/web test -- src/features/character
```

Expected: failure because current TypeScript types require names and panels render names directly.

- [ ] **Step 3: Implement fallback cards without fake assets**

Use semantic text placeholders, not generated URLs or image data. Keep preview local until explicit equip. Render generic effect rows by unit and keep all totals server-authored.

- [ ] **Step 4: Run character tests, typecheck, and build**

Run:

```bash
corepack pnpm --filter @hanjjak/web test -- src/features/character
corepack pnpm --filter @hanjjak/web typecheck
corepack pnpm --filter @hanjjak/web build
```

Expected: character tests pass, typecheck exits 0, production build succeeds.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/features/character apps/web/src/main.tsx
git commit -m "feat(web): finish character cosmetics window"
```

---

### Task 6: Add milestone and selector-box web flows

**Files:**
- Modify: `apps/web/src/features/cosmetics/api.ts`
- Modify: `apps/web/src/features/cosmetics/CosmeticsScreen.tsx`
- Create: `apps/web/src/features/cosmetics/CosmeticsScreen.test.tsx`
- Modify: `apps/web/src/features/cosmetics/api.test.ts`
- Modify: `apps/web/src/styles.css`

**Interfaces:**
- Consumes banner milestone state from Task 4.
- Adds client methods:

```ts
claimMilestone(bannerId: string, count: number, key: string): Promise<MilestoneResult>
openSelectorBox(boxItemId: string, cosmeticId: string, key: string): Promise<Collection>
```

- [ ] **Step 1: Add failing API and component tests**

Test exact paths, payloads, and same-key retry behavior. Component tests must cover 199/200 progress, one claimable box, claim-all, zero owned boxes, selecting one of six legendary cosmetics, successful open, invalid selection error, and cache invalidation for banner, collection, catalog, and character stats.

- [ ] **Step 2: Run cosmetics web tests and confirm failure**

Run:

```bash
corepack pnpm --filter @hanjjak/web test -- src/features/cosmetics
```

Expected: failure because milestone and selector-box read/actions are not rendered by the current screen.

- [ ] **Step 3: Implement milestone and box flows**

Display server-provided counts only. Generate an idempotency key once per submitted command and preserve it for uncertain retry. On success update/invalidate all affected caches. Use fallback names and image states from Task 5.

- [ ] **Step 4: Run cosmetics tests and web checks**

Run:

```bash
corepack pnpm --filter @hanjjak/web test -- src/features/cosmetics
corepack pnpm --filter @hanjjak/web test
corepack pnpm --filter @hanjjak/web typecheck
corepack pnpm --filter @hanjjak/web build
```

Expected: all web tests pass; typecheck and build succeed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/features/cosmetics apps/web/src/styles.css
git commit -m "feat(web): add cosmetic milestone and selector box"
```

---

### Task 7: Synchronize task evidence and run the release gate

**Files:**
- Modify: `docs/wiki/06-delivery/tasks/K-client-ux/k-21-cosmetics-vertical-slice.md`
- Modify: `docs/wiki/06-delivery/tasks/K-client-ux/k-22-character-window.md`
- Modify: `docs/wiki/06-delivery/tasks/K-client-ux/_index.md`
- Modify: `docs/wiki/06-delivery/tasks/_index.md`
- Modify: `docs/70-plans/character-cosmetics-collection/implementation-evidence.md`
- Modify: `docs/70-plans/character-window/implementation-plan.md`

**Interfaces:**
- Consumes verification output from Tasks 2–6.
- Produces task state `development_status: 완료`, `verification_status: 부분 검증` for the actually completed scope; preserves explicit E2E/art/marketplace exclusions.

- [ ] **Step 1: Run the full required verification**

Run from an ASCII-only worktree path if the Windows Korean path causes Gradle test-worker class loading failure:

```bash
corepack pnpm --filter @hanjjak/content-validator test
corepack pnpm --filter @hanjjak/content-validator validate
corepack pnpm --filter @hanjjak/contracts build
corepack pnpm --filter @hanjjak/web test
corepack pnpm --filter @hanjjak/web typecheck
corepack pnpm --filter @hanjjak/web build
cmd.exe /c gradlew.bat :modules:cosmetics:test :apps:game-api:test :packages:sim-core:test
git diff --check
```

Expected: every command exits 0. Do not claim full verification for checks not run.

- [ ] **Step 2: Update implementation evidence**

Record exact commands, test counts, changed contract/content paths, and observed results. Keep these remaining scopes explicit:

```text
PostgreSQL migration and authenticated HTTP E2E: not run
real Chromium E2E: not run
concurrent mutation verification: not run
actual names/images and composite renderer: not implemented
unregistered duplicate marketplace integration: not implemented
```

- [ ] **Step 3: Update task statuses and indices**

Set K-21 and K-22 development to complete only if all planned functionality exists. Keep verification partial. Ensure both K indices contain identical status values and feature routes contain links, not duplicated status text.

- [ ] **Step 4: Verify links and working tree**

Run the repository Markdown relative-link checker if present. Otherwise run the existing documentation check command documented by the repo, then:

```bash
git diff --check
git status --short
```

Expected: no whitespace errors; only intended files are changed.

- [ ] **Step 5: Commit**

```bash
git add docs/wiki/06-delivery/tasks docs/70-plans/character-cosmetics-collection/implementation-evidence.md docs/70-plans/character-window/implementation-plan.md
git commit -m "docs: record character cosmetics implementation"
```
