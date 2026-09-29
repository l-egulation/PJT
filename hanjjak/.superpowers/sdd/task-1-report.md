# Task 1 Report

## Status
Implemented and validated both immutable content package roots for Task 1. The legacy `v1` package remains applied/current-compatible, while `progression-rebalance-v1` is generated as a separate `working` package and is inactive until later tasks apply it.

## RED evidence
The required focused command was run before implementation:

```text
corepack pnpm --filter @hanjjak/content-validator exec vitest run src/buildProgressionRebalance.test.ts src/validate.test.ts
```

It failed as expected: the generator test suite could not import the absent `buildProgressionRebalance.ts`, and the new validator tests reported ENOENT for absent rebalance manifests/content. Existing validator tests passed during that RED run.

## GREEN evidence
The required package checks were run after implementation:

```text
corepack pnpm --filter @hanjjak/content-validator test
```

Result: 2 test files passed, 12 tests passed.

```text
corepack pnpm --filter @hanjjak/content-validator validate
```

Result: completed successfully; both version manifests were regenerated with stable SHA-256 checksums.

A focused typecheck also completed successfully:

```text
corepack pnpm --filter @hanjjak/content-validator typecheck
```

## Exact files
- `packages/game-content/schema/progression.schema.json`
- `packages/game-content/schema/equipment-balance.schema.json`
- `packages/game-content/schema/material-drops.schema.json`
- `tools/content-validator/src/buildProgressionRebalance.ts`
- `tools/content-validator/src/validate.ts`
- `tools/content-validator/src/validate.test.ts`
- `tools/content-validator/src/buildProgressionRebalance.test.ts`
- `packages/game-content/versions/v1/progression/progression.json`
- `packages/game-content/versions/v1/equipment/equipment.json`
- `packages/game-content/versions/v1/manifest.json`
- `packages/game-content/versions/progression-rebalance-v1/manifest.json`
- `packages/game-content/versions/progression-rebalance-v1/chapters/chapters.json`
- `packages/game-content/versions/progression-rebalance-v1/stages/stages.json`
- `packages/game-content/versions/progression-rebalance-v1/drops/material-drops.json`
- `packages/game-content/versions/progression-rebalance-v1/progression/progression.json`
- `packages/game-content/versions/progression-rebalance-v1/equipment/equipment.json`
- `packages/game-content/versions/progression-rebalance-v1/skills/skills.json`
- `packages/game-content/versions/progression-rebalance-v1/gem-dungeons/gem-dungeons.json`
- `packages/game-content/versions/progression-rebalance-v1/cosmetics/cosmetics.json`

## Self-review
- The SSOT first-clear arrays are encoded verbatim for chapters 1–4, with all 40 stage rows and levels limited to 1–79 in the rebalance progression payload.
- Chapter 1 uses guaranteed M1 quantities; chapters 2–4 retain chance-mode metadata and generation weights.
- Equipment arrays contain 29 enhancement steps, bracket allocation, monotonicity checks, and compatibility-only legacy Legendary values. Epic→Legendary rebalance promotion is gated at `stage.05-10`.
- Legacy and rebalance files use separate version roots. The rebalance manifest remains `working`.
- Validator checks schemas, version consistency, stage identity/counts, reward ratios, first-clear chapter-1 totals, drop modes, monotone equipment costs, and writes normalized checksums.
- No runtime/server/UI/cosmetic/gem behavior was changed beyond copying immutable content into the new version root.

## Concerns
The requested Task 1 brief does not provide a separate exhaustive item/skill catalog schema for cross-package reference checking; validation uses the existing schemas and the explicit first-clear identifiers. Later tasks own activation and runtime consumption of the new package.

## Commit
5a81fdc4 feat(content): add progression rebalance v1 package

## Reviewer blocker fixes
- Added RED regression coverage for the NORMAL per-slot schedule, committed manifest drift/read-only validation, unknown first-clear material/GEM_BOX item IDs, and unknown direct skill IDs.
- NORMAL rebalance enhancement materials now use 40 for levels 1–10, 60 for 11–20, and 74 for 21–29 (sum 1,666 per slot); RARE/EPIC retain generic 20/30/50 bracket allocation.
- `validateContent` now reads each committed manifest, compares version/authority, exact file list, checksum map, and normalized SHA-256 checksums without writing. `regenerateManifests` is the explicit write path used by `writeProgressionPackages`.
- Rebalance first-clear validation checks all material families M1–M5 plus GEM_BOX and every directSkillId against the copied skill catalog.

## Reviewer fix evidence
```text
corepack pnpm --filter @hanjjak/content-validator exec vitest run src/buildProgressionRebalance.test.ts src/validate.test.ts
2 test files passed; 10 tests passed.

corepack pnpm --filter @hanjjak/content-validator test
2 test files passed; 10 tests passed.

corepack pnpm --filter @hanjjak/content-validator typecheck
tsc --noEmit passed.

corepack pnpm --filter @hanjjak/content-validator validate
completed successfully with no manifest writes.

## Final validator invariant fix
- Added explicit approved first-clear schedules for every stage in chapters 1–4, including chapter-clear rewards and direct skill assignments. The XP formula and 1–79 level range are enforced for all 40 rows.
- Added exact NORMAL/RARE/EPIC/LEGENDARY enhancement material schedules, including 29-step per-generation totals and latest-level remainder placement, exact enhancement rice rows, and promotion material/rice costs plus stage gates.
- Added semantic tamper tests for chapter 2–4 rewards, RARE/EPIC material rows, enhancement rice, and promotion cost/gate drift. Each fixture regenerates its manifest before validation so failures cannot be explained by checksum detection.

## Final GREEN evidence
```text
corepack pnpm --filter @hanjjak/content-validator exec vitest run src/validate.test.ts
1 test file passed; 14 tests passed.

corepack pnpm --filter @hanjjak/content-validator test
2 test files passed; 18 tests passed.

corepack pnpm --filter @hanjjak/content-validator typecheck
tsc --noEmit passed.

corepack pnpm --filter @hanjjak/content-validator validate
completed successfully with no manifest writes.
```
```
