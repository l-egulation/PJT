# main 기반 치장 뽑기 재구현 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `origin/main`의 현재 코지 웹 구조에 치장 뽑기 독립 화면을 다시 연결하고 상단 직접 진입과 캐릭터 내부 진입 모두를 기능 완결형으로 제공한다.

**Architecture:** 기존 `AppScreen = ManagementScreen | "cosmetics"` 경계를 유지한다. `main.tsx`는 현재 코지 공용 HUD를 보존하면서 `cosmetics`만 독립 화면으로 렌더링하고, `features/cosmetics`는 API adapter·TanStack Query·mutation orchestration·표시 컴포넌트로 서버 확정 상태를 표시한다. 서버 도메인·migration·계약은 변경하지 않는다.

**Tech Stack:** React 19, TypeScript, Vite, TanStack Query, Vitest, Testing Library, Playwright/Chromium smoke, pnpm workspace.

## Global Constraints

- 규칙은 `docs/30-domain/cosmetics/ssot.md`만 소유한다.
- 비용·확률·카탈로그는 `docs/60-content/cosmetics/mvp-v1.md`를 따른다.
- API·검증 범위는 `docs/70-plans/cosmetic-gacha/requirements.md`를 따른다.
- 치장 자체·미등록 재고는 거래·양도·예약하지 않는다.
- 클라이언트는 확률·결과·신규/중복을 재계산하지 않고 서버 응답을 표시한다.
- draw는 `count`만, registration은 `mode`만, equipment는 `cosmeticId`만, selector-box open은 `cosmeticId`만 body에 넣는다.
- 불확실한 command 오류는 같은 idempotency key로만 재확인한다.
- 실제 치장 이미지와 외형 합성 렌더러는 범위에서 제외한다.
- 프로젝트 전체 테스트는 마지막 한 번만 실행하고, 중간에는 변경 범위 focused 검증만 실행한다.

---

## File Map

- Modify: `apps/web/src/navigationState.ts` — `PRIMARY_NAV_ITEMS`에 `cosmetics` 진입을 추가하고 저장 화면 목록과 분리
- Modify: `apps/web/src/main.tsx` — 현재 main 코지 HUD 구조에 치장 화면 분기와 캐릭터 callback 연결
- Modify: `apps/web/src/navigationState.test.ts` — 직접 진입·저장 목록 계약
- Modify: `apps/web/src/features/cosmetics/api.ts` — 서버 DTO/endpoint가 main 계약과 일치하는지 보완
- Modify: `apps/web/src/features/cosmetics/CosmeticsScreen.tsx` — query/mutation orchestration 및 기능 화면
- Modify: `apps/web/src/features/cosmetics/CosmeticGachaBoard.tsx` — 배너·비용·draw action·확률 진입
- Modify: `apps/web/src/features/cosmetics/CosmeticProbabilityDialog.tsx` — authoritative probability 표시
- Modify: `apps/web/src/features/cosmetics/CosmeticDrawReveal.tsx` — draw 결과 순서·신규/중복 표시
- Modify: `apps/web/src/features/cosmetics/CosmeticManagementPanel.tsx` — collection·registration·equipment·milestone·box 관리
- Modify: `apps/web/src/features/cosmetics/presentation.ts` — 오류·결과 표시 helper
- Modify: `apps/web/src/features/cosmetics/cosmetics.css` — 독립 화면 스타일과 반응형 layout
- Modify: `apps/web/src/features/cosmetics/CosmeticsScreen.test.tsx` — 화면 인수 테스트
- Modify: `apps/web/src/features/cosmetics/CosmeticGachaBoard.test.tsx` — 배너·확률·확인·결과 테스트
- Modify: `apps/web/src/features/character/CharacterWindow.test.tsx` — 캐릭터 callback 회귀
- Modify: `apps/web/src/features/character/CosmeticPanels.test.tsx` — 내부 진입 회귀
- Modify: `docs/wiki/06-delivery/tasks/K-client-ux/k-21-cosmetics-vertical-slice.md` — 구현·검증 증거 동기화

---

## Task 1: Restore navigation contract

**Files:**
- Modify: `apps/web/src/navigationState.ts`
- Modify: `apps/web/src/navigationState.test.ts`
- Modify: `apps/web/src/main.tsx`

**Interfaces:**
- Produces `PRIMARY_NAV_ITEMS` containing `{ screen: "cosmetics", label: "치장 뽑기" }`.
- Keeps `MANAGEMENT_SCREENS` free of `"cosmetics"`.
- Keeps `showScreen(next: ManagementScreen)` for existing HUD routes and uses a separate typed path for `AppScreen` where the cosmetics callback needs it.

- [ ] **Step 1: Write the failing test**

Add a test that imports `PRIMARY_NAV_ITEMS` and `MANAGEMENT_SCREENS` and asserts:

```tsx
it("exposes cosmetics as a direct entry without persisting it as a management screen", () => {
  expect(PRIMARY_NAV_ITEMS).toContainEqual({ screen: "cosmetics", label: "치장 뽑기" });
  expect(MANAGEMENT_SCREENS).not.toContain("cosmetics");
});
```

- [ ] **Step 2: Run test to verify it fails**

Run from the worktree:

```bash
pnpm --filter @hanjjak/web exec vitest run src/navigationState.test.ts
```

Expected: FAIL because `PRIMARY_NAV_ITEMS` is absent or does not contain the cosmetics entry.

- [ ] **Step 3: Write minimal implementation**

In `navigationState.ts`, define:

```ts
export const PRIMARY_NAV_ITEMS: Array<{ screen: AppScreen; label: string }> = [
  { screen: "battle", label: "전투" },
  { screen: "battleHistory", label: "전투 기록" },
  { screen: "inventory", label: "아이템" },
  { screen: "equipment", label: "장비" },
  { screen: "skills", label: "스킬" },
  { screen: "gems", label: "보석" },
  { screen: "market", label: "거래소" },
  { screen: "ranking", label: "랭킹" },
  { screen: "cosmetics", label: "치장 뽑기" },
];
```

Keep `MANAGEMENT_SCREENS` unchanged and import the navigation array in `main.tsx`. Preserve current main's `CozySharedNavigation` route filtering, existing header conditions, and `screen === "cosmetics"` render branch. If the current header is conditional on a shared HUD route, add the cosmetics button only to the existing non-shared management header rather than changing HUD components.

- [ ] **Step 4: Run test to verify it passes**

```bash
pnpm --filter @hanjjak/web exec vitest run src/navigationState.test.ts
```

Expected: PASS with no failures.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/navigationState.ts apps/web/src/navigationState.test.ts apps/web/src/main.tsx
git commit -m "feat: restore cosmetic gacha navigation"
```

---

## Task 2: Restore feature API and presentation contracts

**Files:**
- Modify: `apps/web/src/features/cosmetics/api.ts`
- Modify: `apps/web/src/features/cosmetics/presentation.ts`
- Modify: `apps/web/src/features/cosmetics/api.test.ts`
- Modify: `apps/web/src/features/cosmetics/presentation.test.ts`

**Interfaces:**
- `cosmeticsApi.banners(): Promise<Banner[]>`
- `cosmeticsApi.detail(bannerId: string): Promise<BannerDetail>`
- `cosmeticsApi.collection(): Promise<Collection>`
- `cosmeticsApi.catalog(): Promise<Catalog>`
- `cosmeticsApi.draw(bannerId: string, count: 1 | 10, key?: string): Promise<DrawResponse>`
- `cosmeticsApi.claimMilestone(bannerId: string, count: number, key: string): Promise<MilestoneResult>`
- `cosmeticsApi.openSelectorBox(boxItemId: string, cosmeticId: string, key: string): Promise<Collection>`
- `cosmeticsApi.register(cosmeticId: string, mode: "ONE" | "UNTIL_NEXT_STAR", key?: string): Promise<Collection>`
- `cosmeticsApi.equip(slot: string, cosmeticId: string | null, key?: string): Promise<Collection>`

- [ ] **Step 1: Write the failing tests**

Extend API tests with these observable assertions:

```ts
it("sends only count and preserves the draw idempotency key", async () => {
  const fetchMock = vi.fn().mockResolvedValue(Response.json({ data: { results: [] } }));
  vi.stubGlobal("fetch", fetchMock);
  await cosmeticsApi.draw("banner/one", 10, "draw-key");
  const [, init] = fetchMock.mock.calls[0];
  expect(init.method).toBe("POST");
  expect(init.body).toBe(JSON.stringify({ count: 10 }));
  expect(new Headers(init.headers).get("Idempotency-Key")).toBe("draw-key");
});
```

Add presentation assertions that `GACHA_CONTENT_UNAVAILABLE`, `COSMETIC_SYSTEM_LOCKED`, `INSUFFICIENT_GACHA_FUNDS`, and unknown errors map to non-empty Korean messages, and `summarizeDraw` counts only the server `isNew` field.

- [ ] **Step 2: Run focused tests**

```bash
pnpm --filter @hanjjak/web exec vitest run src/features/cosmetics/api.test.ts src/features/cosmetics/presentation.test.ts
```

Expected: FAIL for any missing endpoint/body/error helper; existing passing tests remain passing.

- [ ] **Step 3: Write minimal implementation**

Keep `json<T>` as the common envelope/error parser, use `encodeURIComponent` for path identifiers, set `credentials: "include"`, and build command headers from the caller key. Do not add client-side probability or ownership logic. Use one `DrawResult` helper:

```ts
export function summarizeDraw(results: DrawResult[]) {
  const newCount = results.filter((result) => result.isNew).length;
  return { newCount, duplicateCount: results.length - newCount };
}
```

- [ ] **Step 4: Run focused tests**

```bash
pnpm --filter @hanjjak/web exec vitest run src/features/cosmetics/api.test.ts src/features/cosmetics/presentation.test.ts
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/features/cosmetics/api.ts apps/web/src/features/cosmetics/api.test.ts apps/web/src/features/cosmetics/presentation.ts apps/web/src/features/cosmetics/presentation.test.ts
git commit -m "feat: restore cosmetic gacha client contract"
```

---

## Task 3: Implement banner, confirmation, probability, and result flow

**Files:**
- Modify: `apps/web/src/features/cosmetics/CosmeticsScreen.tsx`
- Modify: `apps/web/src/features/cosmetics/CosmeticGachaBoard.tsx`
- Modify: `apps/web/src/features/cosmetics/CosmeticProbabilityDialog.tsx`
- Modify: `apps/web/src/features/cosmetics/CosmeticDrawReveal.tsx`
- Modify: `apps/web/src/features/cosmetics/CosmeticGachaBoard.test.tsx`
- Modify: `apps/web/src/features/cosmetics/CosmeticsScreen.test.tsx`
- Modify: `apps/web/src/features/cosmetics/cosmetics.css`

**Interfaces:**
- `CosmeticGachaBoard` receives server `Banner[]` and `Catalog`, calls `onDraw(banner, 1 | 10)` and `onOpenProbabilities(bannerId)`.
- Confirmation state owns `{ bannerId, count, key }` until cancel or submit.
- `CosmeticDrawReveal` receives `DrawResponse` and `Catalog`, displays response order and `isNew` only.

- [ ] **Step 1: Write failing screen tests**

Add/retain tests covering:

```tsx
it("does not send draw before confirmation and preserves server results", async () => {
  const draw = vi.spyOn(cosmeticsApi, "draw").mockResolvedValue(drawResponse);
  renderScreen();
  fireEvent.click(screen.getByRole("button", { name: /1회 뽑기/ }));
  expect(draw).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: /취소/ }));
  expect(draw).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: /1회 뽑기/ }));
  fireEvent.click(screen.getByRole("button", { name: /확인/ }));
  expect(await screen.findByText("치장 #061")).toBeTruthy();
  expect(screen.getByText("신규")).toBeTruthy();
  expect(screen.getByText("중복")).toBeTruthy();
});

it("renders exact server probability fields", async () => {
  renderScreen();
  fireEvent.click(screen.getByRole("button", { name: /확률/ }));
  expect(await screen.findByText("10,000 / 6,000,000")).toBeTruthy();
  expect(screen.getByText("0.166667%")).toBeTruthy();
});
```

Add tests for locked `GACHA_CONTENT_UNAVAILABLE` rendering where collection management remains present, and for 5xx/network errors exposing same-request retry.

- [ ] **Step 2: Run focused tests to verify failure**

```bash
pnpm --filter @hanjjak/web exec vitest run src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx
```

Expected: FAIL for missing or incomplete UI behavior.

- [ ] **Step 3: Write minimal implementation**

Implement the screen using `useQuery` for banners, collection, catalog, and selected detail; use one `useMutation` for draw/claim/open/register/equip. Keep draw confirmation separate from command state. On success, update returned collection and invalidate banners, detail, and character stats. On uncertain errors, retain the exact command and expose only same-request retry. Render `GACHA_CONTENT_UNAVAILABLE` as a gacha-only notice while always rendering management when collection/catalog are available.

The board must show server banner order, ticket/rice balances, one/ten estimates, executable state, and probability button. Probability dialog must show `gradeProbabilityMillionths` and every pool item's authoritative fields. Result view must preserve `draw.results` order and use `isNew` directly.

Use responsive CSS with a three-column desktop board and one-column narrow layout. Avoid fixed widths that cause horizontal overflow. Do not add placeholder banners that pretend to be server content.

- [ ] **Step 4: Run focused tests to verify pass**

```bash
pnpm --filter @hanjjak/web exec vitest run src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/features/cosmetics
git commit -m "feat: restore cosmetic gacha web flow"
```

---

## Task 4: Preserve character entry and management flow

**Files:**
- Modify: `apps/web/src/features/character/CharacterWindow.tsx`
- Modify: `apps/web/src/features/character/CosmeticPanels.tsx`
- Modify: `apps/web/src/features/character/CharacterWindow.test.tsx`
- Modify: `apps/web/src/features/character/CosmeticPanels.test.tsx`

**Interfaces:**
- `CharacterWindow` keeps `onGacha(): void`.
- `CostumePanel` keeps `onGacha(): void`.
- Existing character collection/register/equipment query keys remain unchanged.

- [ ] **Step 1: Write failing regression test**

Assert that a rendered owned-costume panel contains a `치장 뽑기` button and invokes `onGacha` once when clicked. Assert that a character window opened with `initialTab="cosmetics"` still shows the locked 1-5 notice when `cosmeticsUnlocked` is false.

- [ ] **Step 2: Run focused tests**

```bash
pnpm --filter @hanjjak/web exec vitest run src/features/character/CharacterWindow.test.tsx src/features/character/CosmeticPanels.test.tsx
```

Expected: FAIL only if main has lost the callback or UI; otherwise document existing contract and do not duplicate behavior.

- [ ] **Step 3: Implement only required compatibility changes**

Keep existing character tab, query, idempotency command, and cache invalidation behavior. If `main` already satisfies the tests, make no production change. Do not move the gacha screen into the character modal.

- [ ] **Step 4: Run focused tests**

```bash
pnpm --filter @hanjjak/web exec vitest run src/features/character/CharacterWindow.test.tsx src/features/character/CosmeticPanels.test.tsx
```

Expected: PASS.

- [ ] **Step 5: Commit if changed**

```bash
git add apps/web/src/features/character
git commit -m "test: preserve character cosmetic entry"
```

If no production/test changes are needed because main already satisfies the contract, do not create an empty commit.

---

## Task 5: Synchronize delivery evidence

**Files:**
- Modify: `docs/wiki/06-delivery/tasks/K-client-ux/k-21-cosmetics-vertical-slice.md`

**Interfaces:**
- Task document remains the owner of implementation and verification status.
- Policy SSOT files remain unchanged.

- [ ] **Step 1: Update evidence after implementation**

Record the actual changed paths, exact focused test/typecheck/build commands and results, browser smoke dimensions/results, and explicitly unexecuted PostgreSQL/Spring HTTP/concurrency checks. Do not mark verification complete based on compile alone.

- [ ] **Step 2: Validate links and status fields**

Check only the changed documentation links and task front matter. Keep `verification_status: 부분 검증` unless all task-owned evidence criteria actually ran.

- [ ] **Step 3: Commit**

```bash
git add docs/wiki/06-delivery/tasks/K-client-ux/k-21-cosmetics-vertical-slice.md
git commit -m "docs: record cosmetic gacha rebuild evidence"
```

---

## Task 6: Full verification and browser smoke

**Files:**
- No source changes expected. If verification exposes a bug, return to the relevant task and add a failing test first.

- [ ] **Step 1: Run focused web suite**

```bash
pnpm --filter @hanjjak/web test
```

Expected: exit 0 and 0 failed tests.

- [ ] **Step 2: Run typecheck and build**

```bash
pnpm --filter @hanjjak/web typecheck
pnpm --filter @hanjjak/web build
```

Expected: both commands exit 0.

- [ ] **Step 3: Run content and contract checks**

```bash
pnpm contracts:build
pnpm content:validate
```

Expected: exit 0 with generated contracts/content valid.

- [ ] **Step 4: Run browser smoke**

Use the browser tool against a local Vite preview or existing app. Verify logged-in/mock-auth flow: top `치장 뽑기` button, banner balances/costs, probability dialog, confirmation cancel without POST, confirmed result order/new-duplicate labels, return to character collection, and no horizontal overflow at 1568px and narrow mobile width.

- [ ] **Step 5: Update evidence**

Append only actual observed results to K-21 and keep unexecuted real PostgreSQL/Spring HTTP/concurrency verification explicitly listed.

---

## Task 7: Review and submit MR

**Files:**
- No source changes expected unless review finds an issue.

- [ ] **Step 1: Inspect diff and changed paths**

```bash
git diff --check origin/main...HEAD
git status --short
```

Expected: no whitespace errors and only intended files changed.

- [ ] **Step 2: Request code review**

Dispatch a reviewer with base `origin/main`, current HEAD, the approved spec path, and the verification results. Fix all Critical/Important findings with a test-first change and rerun affected checks.

- [ ] **Step 3: Push branch**

```bash
git push -u origin feat/cosmetic-gacha-main-rebuild
```

- [ ] **Step 4: Create MR**

Use the repository's GitLab remote and create a merge request targeting `main`, with title:

```text
feat: restore cosmetic gacha from main
```

Description must include scope, server contract reuse, verification commands/results, browser smoke evidence, and explicit PostgreSQL/Spring HTTP/concurrency limitations.
