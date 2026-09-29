# Cosmetic Gacha Main Refresh Implementation Plan

> **For agentic workers:** Implement task-by-task with test-first changes. Keep the server/API contracts from `apps/web/src/features/cosmetics/api.ts` unchanged.

**Goal:** Rebuild the cosmetic gacha screen from the latest `main` branch using the approved cosmetic SSOT and existing eight API contracts.

**Architecture:** Keep `CosmeticsScreen` as the route-level container and keep `cosmeticsApi` as the only server boundary. Add small presentational helpers inside the cosmetics feature for banner cards, draw confirmation, result grid, and collection cards; all server-calculated costs, probabilities, ownership, and effects remain display-only client data.

**Tech Stack:** React 19, TanStack Query, TypeScript, Vitest, existing Vite CSS.

## Global Constraints

- Do not change backend APIs, TypeSpec, database migrations, or server domain behavior.
- Preserve `cosmeticsApi` paths, request payloads, idempotency keys, and response types.
- Display server-provided costs/probabilities; never recalculate draw probability or payment eligibility.
- Preserve `reservedQuantity` and `availableUnregisteredQuantity` because they are part of the current `main` contract.
- Use fallback names `치장 #NNN`, set/banner fallback `치장 배너 #NN`, and `이미지 준비 중` when metadata is null.
- Keep uncertain network/5xx mutation results blocked until the same command is retried with the original key.
- No animation, image generation, or new navigation architecture.

---

### Task 1: Add failing cosmetic behavior tests

**Files:**
- Modify: `apps/web/src/features/cosmetics/CosmeticsScreen.test.tsx`
- Test: `apps/web/src/features/cosmetics/CosmeticsScreen.test.tsx`

**Interfaces:**
- Consumes existing `Banner`, `Collection`, `Catalog`, and `DrawResponse` shapes.
- Produces observable requirements for the rebuilt screen: confirmation dialog before draw, exact server probability display from detail data, ordered result cards, and uncertain-error retry lock.

- [ ] Add deterministic query fixtures including `cosmeticsApi.detail` data with server numerator/denominator/display probability fields. Extend only test-local fixture typing if current API types lack the optional detail response shape.
- [ ] Add a test that renders one banner and verifies `1회` and `10회` buttons open a confirmation dialog without calling `cosmeticsApi.draw` before confirmation.
- [ ] Add a test that confirms a draw and verifies the original count, banner ID, and generated idempotency key are sent through `cosmeticsApi.draw`, then renders ordered `신규`/`중복` results.
- [ ] Add a test that renders server-provided probability strings and exact fractions without deriving values in the component.
- [ ] Add a test that rejects a `CosmeticsApiError(503, ...)`, shows the uncertain state, disables new draw controls, and retries with the same command key.
- [ ] Run `corepack pnpm --filter @hanjjak/web exec vitest run src/features/cosmetics/CosmeticsScreen.test.tsx` from the fresh worktree and confirm the new assertions fail for the missing behavior.

### Task 2: Implement the refreshed gacha and collection UI

**Files:**
- Modify: `apps/web/src/features/cosmetics/CosmeticsScreen.tsx`
- Create: `apps/web/src/features/cosmetics/cosmetics.css`
- Modify: `apps/web/src/features/cosmetics/CosmeticsScreen.test.tsx`

**Interfaces:**
- Consumes `cosmeticsApi.banners`, `cosmeticsApi.detail`, `cosmeticsApi.collection`, `cosmeticsApi.catalog`, and existing mutation methods.
- Produces a single `CosmeticsScreen` with banner information, confirmation dialog, result grid, milestone/selector-box controls, collection/equipment controls, and retry behavior.

- [ ] Add a banner detail query keyed by `['cosmetic-banner-detail', bannerId]`; render each detail's server probability display and numerator/denominator exactly as returned.
- [ ] Replace direct draw execution with state `{ bannerId, count, key }`; open a native accessible dialog or equivalent modal on button click, and only call `cosmeticsApi.draw` after explicit confirmation.
- [ ] Render draw results as an ordered grid with fallback names, grade labels, and `신규`/`중복` badges; show new/duplicate totals and ticket/rice consumption from the response.
- [ ] Keep milestone claim and selector-box open actions, but put both behind explicit confirmation and preserve their caller-generated idempotency keys.
- [ ] Keep collection cards with registered/unregistered/reserved/available quantities, star state, registration buttons, preview-only state, equip/unequip actions, and server total effects.
- [ ] Keep uncertain error behavior: retain the original command, disable all state-changing controls, and expose a same-key retry button; deterministic errors expose dismiss/retry UI without changing server state.
- [ ] Add feature-scoped CSS for responsive banner cards, probability rows, confirmation dialog, ordered result grid, collection cards, and narrow viewports. Do not alter global layout or create placeholder art assets.
- [ ] Run the focused test and make all new tests pass before refactoring.

### Task 3: Verify the feature contract

**Files:**
- No source changes unless verification exposes a contract defect.

**Checks:**
- [ ] Run `corepack pnpm --filter @hanjjak/web exec vitest run src/features/cosmetics/api.test.ts src/features/cosmetics/CosmeticsScreen.test.tsx`.
- [ ] Run `corepack pnpm --filter @hanjjak/web typecheck`.
- [ ] Run `corepack pnpm --filter @hanjjak/web build`.
- [ ] Inspect the actual built web surface with the available browser QA path if the environment supports it; otherwise report that visual browser verification was unavailable.

### Task 4: Commit and publish the clean branch

**Files:**
- Commit only the fresh cosmetic feature implementation and its focused tests/styles/plan.

**Checks:**
- [ ] Run `git diff --check`, verify no unresolved markers, and verify `git status --short` contains only intended files.
- [ ] Commit with `feat: rebuild cosmetic gacha from main`.
- [ ] Push `codex/cosmetic-gacha-main-refresh` and report the branch and commit.
