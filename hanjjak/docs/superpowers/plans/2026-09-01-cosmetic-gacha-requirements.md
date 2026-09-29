# Cosmetic Gacha Requirements Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Promote the approved cosmetic-gacha design into canonical domain rules, implementation requirements, product scope, cross-domain contracts, routing indexes, and an explicitly unresolved content-version boundary without inventing missing balance values.

**Architecture:** Use the repository's three-layer documentation model. `docs/30-domain/cosmetics/ssot.md` owns stable game rules, `docs/70-plans/cosmetic-gacha/requirements.md` owns implementation behavior and verification, and `docs/60-content/cosmetics/mvp-v1.md` owns the content-version readiness state and eventually the missing numeric tables. Existing product, account, item, combat, UX, networking, acceptance, decision, and source-map documents receive links and responsibility changes only; they do not duplicate cosmetics rules.

**Tech Stack:** Markdown, YAML front matter, repository-relative links, Git.

## Global Constraints

- This repository is in planning/design stage; create no source code, package manifest, database migration, deployment file, or executable.
- Follow `docs/wiki/00-meta/ssot-policy.md`: one fact has one owner document.
- Preserve meeting and Notion archives verbatim; never rewrite archive source text.
- Treat the approved design at `docs/superpowers/specs/2026-09-01-cosmetic-gacha-design.md` as the source for this propagation.
- Include cosmetic gacha in MVP and remove `치장·스킨·컬렉션` from the future-scope candidate list.
- Do not invent the common rice cost, four rarity probabilities, active cosmetic catalog, collection milestones, set membership catalog, or set-effect values.
- Record those unavailable values as an `unresolved` content-version blocker, not as defaults, examples, zeroes, or implementation constants.
- Stable rarity identifiers are `NORMAL`, `RARE`, `EPIC`, `LEGENDARY`.
- Draw counts are exactly `1` and `10`; ten draws cost exactly ten single draws and have no guarantee or discount.
- Probability units use integer millionths where `1,000,000 = 100%`.
- A common cosmetic ticket always pays first; one ticket pays one draw; rice pays remaining draws; users cannot override payment order.
- Cosmetic first acquisition is quantity `1`, star `1`; upgrade costs for target stars 2–5 are `1`, `2`, `3`, `4` duplicate copies per step.
- Collection levels provide progress only and no combat stats.
- Set effects accumulate every star-row increment from star 1 through the current set star.
- Selector-box entitlement repeats every 200 successful draws per pickup banner and never expires.
- Detailed HTTP status codes, common error envelope, authentication headers, audit retention duration, ticket supply sources, cosmetic equipping, and final UI layout remain out of scope.

---

### Task 1: Promote Product Scope and Decision Status

**Files:**
- Modify: `docs/superpowers/specs/2026-09-01-cosmetic-gacha-design.md:3-8`
- Modify: `docs/70-plans/mvp-release/requirements.md:10-36`
- Modify: `docs/70-plans/future-content/brief.md:9-19`
- Modify: `docs/80-decisions/README.md:15-47`

**Interfaces:**
- Consumes: User approval of `docs/superpowers/specs/2026-09-01-cosmetic-gacha-design.md` on 2026-09-01.
- Produces: Confirmed product-scope and design-decision records that later canonical documents can cite.

- [ ] **Step 1: Verify the approved design metadata**

Ensure the design front matter is exactly:

```yaml
---
doc_kind: design
owner_domain: cosmetics
status: approved
approved_at: 2026-09-01
---
```

- [ ] **Step 2: Add cosmetics to MVP scope**

Add one routing-level bullet after the gem-system bullets in `docs/70-plans/mvp-release/requirements.md`:

```markdown
- 1-5 최초 클리어로 해금되는 전설 세트별 상시 치장 픽업, 공용 뽑기권·쌀 결제, 치장 도감·1~5성 성장과 세트 효과
```

Do not copy probability, upgrade-cost, milestone, or API details into the MVP scope SSOT.

- [ ] **Step 3: Remove cosmetics from future scope**

Delete only this candidate bullet from `docs/70-plans/future-content/brief.md`:

```markdown
- 치장·스킨·컬렉션
```

Keep companion characters, raids, marketplace extensions, cloud operations, and Kafka candidates unchanged.

- [ ] **Step 4: Record confirmed and unresolved decisions**

Insert these rows in `docs/80-decisions/README.md` after the basic-currency decision and before material-preference decisions:

```markdown
| 치장 뽑기·성장 MVP 범위 | 2026-09-01 사용자 최종 설계 승인 | confirmed | [치장 뽑기 요구사항 설계](../superpowers/specs/2026-09-01-cosmetic-gacha-design.md)의 1-5 해금, 전설 세트별 상시 픽업, 티켓 우선 혼합 결제, 도감·1~5성·세트 효과와 200회 반복 선택 상자 구조를 MVP에 포함 |
| 치장 뽑기 콘텐츠 수치 | 2026-09-01 사용자 최종 설계 승인 | unresolved | 공통 쌀 비용, 4등급 확률, 활성 치장 목록, 도감 단계 임계값, 세트 구성과 1~5성 효과는 [MVP 치장 뽑기 콘텐츠 v1](../60-content/cosmetics/mvp-v1.md) 승인 전까지 구현 입력으로 사용하지 않음 |
```

- [ ] **Step 5: Verify scope propagation**

Read the affected sections and confirm:

- cosmetics appears once in MVP scope;
- cosmetics no longer appears as a future candidate;
- decision status distinguishes confirmed structure from unresolved content values;
- no archived meeting or Notion file changed.

- [ ] **Step 6: Commit product-scope propagation**

```bash
git add docs/superpowers/specs/2026-09-01-cosmetic-gacha-design.md docs/70-plans/mvp-release/requirements.md docs/70-plans/future-content/brief.md docs/80-decisions/README.md
git commit -m "docs: promote cosmetic gacha into MVP scope"
```

### Task 2: Establish the Cosmetics Domain SSOT

**Files:**
- Create: `docs/30-domain/cosmetics/README.md`
- Create: `docs/30-domain/cosmetics/ssot.md`
- Create: `docs/30-domain/cosmetics/features/_index.md`
- Create: `docs/30-domain/cosmetics/features/gacha.md`
- Create: `docs/30-domain/cosmetics/features/collection-growth.md`
- Modify: `docs/30-domain/README.md:5-18`

**Interfaces:**
- Consumes: Confirmed structural decisions from Task 1 and approved design sections 5–15.
- Produces: Canonical cosmetics rules referenced by requirements, content, account, combat, UX, networking, and acceptance documents.

- [ ] **Step 1: Create the cosmetics entry point**

Write `docs/30-domain/cosmetics/README.md` with only routing links:

```markdown
# 치장

- 규칙 정본: [치장 뽑기·도감·세트 SSOT](./ssot.md)
- 기능 경로: [치장 기능](./features/_index.md)
- 승인 설계: [치장 뽑기 시스템 요구사항 설계](../../superpowers/specs/2026-09-01-cosmetic-gacha-design.md)
- 요구사항 명세: [치장 뽑기 시스템 요구사항 명세서](../../70-plans/cosmetic-gacha/requirements.md)
- 콘텐츠 상태: [MVP 치장 뽑기 콘텐츠 v1](../../60-content/cosmetics/mvp-v1.md)
```

- [ ] **Step 2: Write the canonical cosmetics rules**

Create `docs/30-domain/cosmetics/ssot.md` with this front matter:

```yaml
---
doc_kind: ssot
owner_domain: cosmetics
authority_level: applied
---
```

Use these sections and ownership boundaries:

```markdown
# 치장 뽑기·도감·세트 SSOT

## 책임
## 해금
## 배너·등급·추첨
## 재화와 결제 순서
## 치장 보유 수량과 성급
## 도감
## 세트 구성·성급·효과
## 픽업 200회 마일스톤과 선택 상자
## 서버 권한·원자성·멱등성
## 전투 반영
## 불변 조건
## 문서 연결
```

The SSOT must state every stable rule from approved design sections 5–15, but must link rather than duplicate missing content tables. Include these exact formulas and stable tables:

```text
setStar = min(cosmeticStar of every set member)
claimableBoxCount = floor(totalSuccessfulDraws / 200) - claimedBoxCount
```

```markdown
| 목표 성급 | 해당 단계 추가 소비 수량 |
| ---: | ---: |
| 2 | 1 |
| 3 | 2 |
| 4 | 3 |
| 5 | 4 |
```

State explicitly:

- collection levels have no combat effect;
- set star rows are increments and rows 1 through current star are summed;
- released set membership is immutable;
- old unassigned cosmetics may join only a newly created set;
- cosmetics do not consume the 200-slot inventory;
- selector boxes do consume one stack slot only when no matching stack exists;
- ticket supply sources and cosmetic equipping are not owned here.

- [ ] **Step 3: Create routing-only feature documents**

Write `docs/30-domain/cosmetics/features/_index.md`:

```markdown
---
doc_kind: feature-index
owner_domain: cosmetics
authority_level: routing-only
---

# 치장 기능 경로

| 기능 | 규칙 정본 | 구현·콘텐츠 경로 |
| --- | --- | --- |
| 상시 픽업·결제·200회 선택 상자 | [치장 SSOT](../ssot.md) | [요구사항 명세](../../../70-plans/cosmetic-gacha/requirements.md), [MVP 콘텐츠 v1](../../../60-content/cosmetics/mvp-v1.md) |
| 도감·치장 성급·세트 효과 | [치장 SSOT](../ssot.md) | [요구사항 명세](../../../70-plans/cosmetic-gacha/requirements.md), [MVP 콘텐츠 v1](../../../60-content/cosmetics/mvp-v1.md) |
```

Write `features/gacha.md` and `features/collection-growth.md` as routing-only files. Each must contain front matter, a title, one sentence saying it defines no rule or number, and links to the SSOT, requirements, and content-state document. Do not repeat costs, counts, formulas, states, or completion status.

- [ ] **Step 4: Register Cosmetics in the domain index**

Add this row in `docs/30-domain/README.md` after Gems:

```markdown
| Cosmetics | [Cosmetics SSOT](./cosmetics/ssot.md) | [Cosmetics features](./cosmetics/features/_index.md) |
```

- [ ] **Step 5: Verify SSOT isolation**

Confirm:

- every stable cosmetics rule exists in `ssot.md` once;
- routing files contain links only;
- no missing numeric content value appears as a guessed number;
- all relative links resolve.

- [ ] **Step 6: Commit the cosmetics domain**

```bash
git add docs/30-domain/README.md docs/30-domain/cosmetics
git commit -m "docs: establish cosmetic gacha domain rules"
```

### Task 3: Establish the Unresolved MVP Content Boundary

**Files:**
- Create: `docs/60-content/cosmetics/README.md`
- Create: `docs/60-content/cosmetics/mvp-v1.md`
- Modify: `docs/60-content/README.md:1-13`

**Interfaces:**
- Consumes: Cosmetics content schema constraints from `docs/30-domain/cosmetics/ssot.md`.
- Produces: A truthful content-readiness document that blocks implementation from using invented balance constants.

- [ ] **Step 1: Create the cosmetics content entry point**

Write `docs/60-content/cosmetics/README.md`:

```markdown
# Cosmetics Content

치장 해금·뽑기·수량·성급·도감·세트 규칙은 [치장 SSOT](../../30-domain/cosmetics/ssot.md)가 소유한다.

- 승인 설계: [치장 뽑기 시스템 요구사항 설계](../../superpowers/specs/2026-09-01-cosmetic-gacha-design.md)
- 요구사항 명세: [치장 뽑기 시스템 요구사항 명세서](../../70-plans/cosmetic-gacha/requirements.md)
- 콘텐츠 상태: [MVP 치장 뽑기 콘텐츠 v1](./mvp-v1.md)

`mvp-v1.md`가 `approved`로 승격되기 전에는 뽑기 비용·확률·활성 풀·도감 임계값·세트 효과를 구현 상수로 대체하지 않는다.
```

- [ ] **Step 2: Create an explicitly unresolved content-version document**

Create `docs/60-content/cosmetics/mvp-v1.md` with front matter:

```yaml
---
doc_kind: content-version
owner_domain: cosmetics-content
authority_level: candidate
status: unresolved
---
```

The body must not contain empty tables, fake sample values, zero defaults, `TBD`, or `TODO`. Record the state as prose and list the missing approvals:

```markdown
# MVP 치장 뽑기 콘텐츠 v1

## 현재 상태

이 콘텐츠 버전은 아직 활성 구현 입력이 아니다. 구조 규칙은 확정됐지만 아래 밸런스·카탈로그 값이 사용자 승인되지 않아 `unresolved` 상태다.

## 승인 전 필요한 값

- 모든 상시 픽업의 공통 1회 쌀 비용
- `NORMAL`, `RARE`, `EPIC`, `LEGENDARY` 등급 확률의 백만분율 정수 네 값
- 전체 활성 노말·희귀·영웅 치장 목록
- 모든 전설 세트와 불변 구성 치장 목록
- 전설 세트별 `bannerId`와 `boxItemId`
- 치장별 안정 `cosmeticId`, 표시 메타데이터와 등급
- 노말·희귀·영웅 치장의 세트 소속 또는 미소속 상태
- 엄격히 증가하는 도감 단계별 고유 등록 수 임계값
- 모든 세트의 1~5성 단계별 `attack`, `maxHp`, `defensePenetration` 증가량

## 승격 조건

1. 등급 확률 합계가 정확히 `1,000,000`이다.
2. 확률이 0보다 큰 등급마다 활성 치장이 한 개 이상 있다.
3. 모든 배너의 공통 하위 등급 풀, 쌀 비용과 전설 총확률이 같다.
4. 전설 세트·배너·선택 상자가 1:1:1로 연결된다.
5. 각 세트는 같은 등급의 고유 치장 두 개 이상으로 구성된다.
6. 모든 전설 치장은 한 전설 세트에 정확히 한 번 포함된다.
7. 각 세트에 1~5성 효과 행이 모두 존재하고 값은 0 이상의 정수다.
8. 도감 임계값은 엄격히 증가한다.
9. 사용자 검토 후 `status: approved`, `authority_level: working` 이상으로 승격한다.

## 구현 차단

이 문서가 승인되기 전에는 정상 뽑기 실행, 확률 공개, 도감 단계 계산과 세트 효과 계산을 완료했다고 판정할 수 없다.
```

- [ ] **Step 3: Register cosmetics content**

Add this bullet after Gems in `docs/60-content/README.md`:

```markdown
- [Cosmetics](./cosmetics/README.md)
```

- [ ] **Step 4: Verify unresolved-state honesty**

Confirm the content document:

- contains no guessed cost, probability, catalog, milestone, or effect value;
- explains why implementation is blocked;
- gives complete promotion criteria;
- preserves confirmed structural numbers such as probability total precision without pretending the distribution is known.

- [ ] **Step 5: Commit the content boundary**

```bash
git add docs/60-content/README.md docs/60-content/cosmetics
git commit -m "docs: define cosmetic content approval boundary"
```

### Task 4: Write the Implementation Requirements Specification

**Files:**
- Create: `docs/70-plans/cosmetic-gacha/requirements.md`
- Modify: `docs/70-plans/README.md:1-14`
- Modify: `docs/30-domain/cosmetics/README.md`
- Modify: `docs/60-content/cosmetics/README.md`

**Interfaces:**
- Consumes: Canonical rules from `docs/30-domain/cosmetics/ssot.md` and unresolved content state from `docs/60-content/cosmetics/mvp-v1.md`.
- Produces: Traceable implementation behavior, seven API purposes, data requirements, domain errors, and acceptance scenarios.

- [ ] **Step 1: Create the requirements document header and boundaries**

Use this front matter:

```yaml
---
doc_kind: requirements
owner_domain: cosmetics
authority_level: implementation-guidance
status: approved
approved_at: 2026-09-01
---
```

Start with these sections:

```markdown
# 치장 뽑기 시스템 요구사항 명세서

## 1. 목적
## 2. 기준 문서와 우선순위
## 3. 범위
## 4. 용어와 안정 식별자
## 5. 사용자 흐름
## 6. 기능 요구사항
## 7. API 요구사항
## 8. 데이터 요구사항과 불변 조건
## 9. 원자성·멱등성·동시성
## 10. 오류 처리 요구사항
## 11. 책임 경계
## 12. 검증 기준
## 13. 추적성 매트릭스
## 14. 미해결 콘텐츠 입력
```

The purpose must state that rules belong to the cosmetics SSOT and numeric/catalog values belong to the content-version document. The requirements document owns implementation scope and verification only.

- [ ] **Step 2: Define stable terms and enums**

Include exact definitions for:

- pickup banner;
- common cosmetic ticket;
- `registeredQuantity`;
- `cosmeticStar`;
- new versus duplicate result;
- collection registration and collection level;
- set, set star, active set effect;
- successful draw count, claimed-box count, claimable-box count;
- selector box;
- content version and audit reproduction token.

Include the exact rarity enum table and state that API/storage IDs remain stable while display text is localized.

- [ ] **Step 3: Write requirement groups with IDs and observable verification**

Use this ID structure; every row must contain one normative requirement and one observable verification statement:

```text
COS-UNLOCK-001..004
COS-BANNER-001..010
COS-PAY-001..009
COS-DRAW-001..014
COS-OWN-001..008
COS-UPGRADE-001..010
COS-COLLECTION-001..007
COS-SET-001..012
COS-MILESTONE-001..011
COS-BOX-001..009
COS-COMBAT-001..008
COS-AUDIT-001..006
COS-CONTENT-001..012
```

Required coverage by group:

- `COS-UNLOCK`: 1-5 first clear, atomic ten-ticket grant, once-only behavior, all cosmetic APIs blocked before unlock.
- `COS-BANNER`: no standard banner, permanent legendary-set banners, 1:1:1 mapping, identical common lower pools, banner detail and probability disclosure.
- `COS-PAY`: ticket-first mandatory order, one ticket per draw, rice fallback, counts 1/10 only, all-or-nothing insufficiency, query preview versus execution revalidation, non-negative integer balances.
- `COS-DRAW`: server-only results, rarity-first then equal-weight item choice, independent results, no guarantee or owned-item correction, ordered new/duplicate handling, atomic grant and response semantics.
- `COS-OWN`: separate collection storage, initial quantity/star, duplicate accumulation including after star 5, no 200-slot occupancy.
- `COS-UPGRADE`: target-star batch command, costs 1/2/3/4, summed cost, preserve one owned copy, one transaction, no probabilistic failure, query-provided target costs.
- `COS-COLLECTION`: every unique owned cosmetic counts, duplicates/stars do not, thresholds from active content, progress fields, no combat effect, latest-version recalculation.
- `COS-SET`: same-rarity unique members, at least two, max one set per cosmetic, immutable released membership, new-set assignment for old unassigned cosmetics, min-star rule, missing member means zero, incremental rows summed, all active sets summed.
- `COS-MILESTONE`: per-banner total successes, payment-independent counting, no reset, 200-repeat formula, total and claimed counters, permanent unclaimed rights, one/specified/all claims.
- `COS-BOX`: stackable 200-slot item, existing stack versus one new slot, atomic claim, one-at-a-time use, immutable member validation, same grant semantics, no draw-count increment.
- `COS-COMBAT`: allowlisted stat IDs, MVP three IDs, raw sum handoff, immediate runtime application, max-HP delta rules, alive minimum 1, dead non-revival, content-version update behavior.
- `COS-AUDIT`: content version, ordered outcomes, payment, before/after counters, server-only reproduction token, retention delegated to operations.
- `COS-CONTENT`: probability total, nonempty positive pools, equal lower pool, common cost and legendary probability, complete 1:1:1 mapping, all legendary acquisition paths, effect/threshold constraints, invalid-version blocking without auto-correction.

- [ ] **Step 4: Define the seven API purposes**

Use this exact API list and connect each row to requirement IDs:

```markdown
| ID | Method·Path | Purpose |
| --- | --- | --- |
| COS-API-001 | `GET /api/v1/cosmetic-gacha/banners` | 배너·재화·1회/10회 예상 결제·누적·미수령 수 조회 |
| COS-API-002 | `GET /api/v1/cosmetic-gacha/banners/{bannerId}` | 등장 풀·등급/개별 확률·콘텐츠 버전 조회 |
| COS-API-003 | `POST /api/v1/cosmetic-gacha/banners/{bannerId}/draws` | body의 `count`만으로 원자적 뽑기 실행 |
| COS-API-004 | `POST /api/v1/cosmetic-gacha/banners/{bannerId}/milestone-claims` | 1개·지정 수량·전체 선택 상자 수령 |
| COS-API-005 | `POST /api/v1/cosmetic-selector-boxes/{boxItemId}/open` | 상자 한 개와 선택 치장 한 개 교환 |
| COS-API-006 | `GET /api/v1/cosmetics/collection` | 보유·도감·세트·효과·승급 비용 조회 |
| COS-API-007 | `POST /api/v1/cosmetics/{cosmeticId}/upgrades` | `targetStar`까지 원자적 일괄 승급 |
```

State explicitly that `paymentType`, user ID, probability, result, and reward list are not accepted in draw input. Common idempotency key is a request header. No result-registration, collection-add, set-refresh, upgrade-preview, or gacha-history API is required for MVP.

- [ ] **Step 5: Define minimum data and transaction records**

Specify these minimum server-owned records without prescribing physical tables:

- cosmetic catalog/version;
- set definition/version;
- banner definition/version;
- account cosmetic quantity and star;
- account common ticket balance;
- account per-banner successful draw total and claimed box total;
- stackable selector-box inventory quantity;
- idempotency result record;
- draw audit record;
- derived/rebuildable collection level and set-effect cache.

List atomic units for unlock, draw, upgrade, milestone claim, and selector-box open. For every unit, name all state that commits or rolls back together.

- [ ] **Step 6: Define stable domain error meanings**

Include these codes with `state change: none` for validation failures:

```text
AUTHENTICATION_REQUIRED
COSMETIC_SYSTEM_LOCKED
GACHA_BANNER_NOT_FOUND
GACHA_CONTENT_UNAVAILABLE
INVALID_DRAW_COUNT
INSUFFICIENT_GACHA_FUNDS
IDEMPOTENCY_KEY_REUSED
COSMETIC_NOT_FOUND
COSMETIC_NOT_OWNED
INVALID_TARGET_STAR
INSUFFICIENT_DUPLICATE_COSMETICS
NO_CLAIMABLE_SELECTOR_BOX
INVALID_CLAIM_QUANTITY
INVENTORY_CAPACITY_EXCEEDED
SELECTOR_BOX_NOT_FOUND
SELECTOR_BOX_NOT_OWNED
INVALID_SELECTOR_COSMETIC
COSMETIC_STATE_CONFLICT
COSMETIC_SERVICE_UNAVAILABLE
```

Delegate HTTP status and common envelope to `docs/50-architecture/networking.md`.

- [ ] **Step 7: Add verification and traceability**

Write acceptance scenarios for:

1. unlock and once-only ten tickets;
2. ticket-only, rice-only, and mixed single/ten draws;
3. ordered new/duplicate results and collection registration;
4. target-star upgrade and set-star change;
5. 200/400 draw claims and permanent unclaimed rights;
6. selector-box slot handling and chosen reward;
7. idempotent recovery after response loss;
8. concurrent draws and another rice consumer;
9. invalid content blocking;
10. immediate combat effect and HP adjustment.

Trace each requirement group to the cosmetics SSOT, content readiness document, API row, and verification section.

- [ ] **Step 8: Register the requirements route**

Add this bullet to `docs/70-plans/README.md`:

```markdown
- [Cosmetic Gacha Requirements](./cosmetic-gacha/requirements.md)
```

Confirm the previously created Cosmetics domain/content README links resolve to the new requirements file.

- [ ] **Step 9: Verify requirement count and coverage**

Count requirement rows by ID and compare against the ranges above. Confirm every approved design section 5–20 maps to at least one requirement group and no unresolved content value is invented.

- [ ] **Step 10: Commit the requirements specification**

```bash
git add docs/70-plans/README.md docs/70-plans/cosmetic-gacha/requirements.md docs/30-domain/cosmetics/README.md docs/60-content/cosmetics/README.md
git commit -m "docs: specify cosmetic gacha requirements"
```

### Task 5: Connect Cross-Domain Authority and Runtime Effects

**Files:**
- Modify: `docs/30-domain/player/ssot.md:13-47`
- Modify: `docs/30-domain/items/ssot.md:40-59`
- Modify: `docs/30-domain/combat/ssot.md:9-36,57-65`
- Modify: `docs/30-domain/player/ux/ssot.md:9-37`

**Interfaces:**
- Consumes: Cosmetics SSOT and requirements from Tasks 2 and 4.
- Produces: Explicit account, inventory, combat, and UI boundaries without copying cosmetics rules.

- [ ] **Step 1: Register server-owned cosmetics state**

Add one row to the account state table in `docs/30-domain/player/ssot.md`:

```markdown
| 치장 해금·공용 뽑기권·보유 수량·성급·배너별 누적·선택 상자 수령 상태 | 서버 권한 데이터 | 재화 차감·추첨·치장 지급·도감·세트 갱신과 멱등 결과의 근거 |
```

Extend the local-cache and security-boundary prose so cosmetic ownership, gacha results, and set effects are server-confirmed. Link to the Cosmetics SSOT instead of restating formulas or counts.

- [ ] **Step 2: Register selector-box inventory responsibility**

In `docs/30-domain/items/ssot.md`, add only the cross-domain inventory rule:

```markdown
- 치장 세트 선택 상자는 동일 아이템 수량으로 중첩하고, 기존 스택이 없을 때 공용 인벤토리 한 슬롯을 사용한다. 수령 자격·상자와 세트의 매핑·사용 결과는 [치장 SSOT](../cosmetics/ssot.md)가 소유한다.
```

Do not add cosmetics themselves to the 200-slot inventory; state that cosmetic ownership remains in the Cosmetics SSOT's separate collection state.

- [ ] **Step 3: Connect set effects to combat**

Update `docs/30-domain/combat/ssot.md` to link the Cosmetics SSOT beside Gems as a stat source. Add the runtime boundary:

```markdown
치장 세트 효과는 [치장 SSOT](../cosmetics/ssot.md)가 계산한 스탯 ID별 원시 증가량을 사용한다. 획득·승급·활성 콘텐츠 버전 변경의 실시간 반영과 현재 HP 조정은 치장 SSOT를 따르고, 다른 성장축과의 최종 합산 순서·상한은 이 문서가 소유한다.
```

Do not copy upgrade costs, set-star formulas, or milestone rules into combat.

- [ ] **Step 4: Add client UX responsibilities**

Update the expanded management-window list in `docs/30-domain/player/ux/ssot.md` to include cosmetic gacha, collection, and growth after 1-5 unlock.

Add concise UX principles:

- show current rice/tickets, one/ten expected ticket-first payment, rarity and individual probabilities;
- disable actions when the latest displayed state cannot pay, while treating the server as final authority;
- show server-confirmed ordered outcomes, new/duplicate labels, collection progress, set changes, consumed currencies, and remaining balances;
- block repeat clicks during commands without treating that as the concurrency guarantee;
- play animation only after server success; skipping/closing animation does not cancel the committed result;
- expose target-star cumulative duplicate cost and selector-box claim capacity errors;
- do not compute random results, set effects, or collection registration locally.

Link to Cosmetics SSOT and requirements instead of copying exact costs and formulas.

- [ ] **Step 5: Verify boundary consistency**

Confirm:

- account owns server authority, not cosmetics rules;
- items owns stack/slot mechanics, not milestone eligibility;
- combat owns final aggregation, not set composition;
- UX owns display/interactions, not result authority;
- all links resolve.

- [ ] **Step 6: Commit cross-domain contracts**

```bash
git add docs/30-domain/player/ssot.md docs/30-domain/items/ssot.md docs/30-domain/combat/ssot.md docs/30-domain/player/ux/ssot.md
git commit -m "docs: connect cosmetic gacha domain boundaries"
```

### Task 6: Register Source Map, Networking, and Acceptance Flow

**Files:**
- Modify: `docs/wiki/00-meta/source-map.md:5-32`
- Modify: `docs/50-architecture/networking.md:7-21`
- Modify: `docs/70-plans/mvp-release/acceptance.md:1-11`

**Interfaces:**
- Consumes: Canonical paths and API purposes from Tasks 2–5.
- Produces: Repository navigation and an end-to-end MVP acceptance route.

- [ ] **Step 1: Register cosmetics ownership in the source map**

Insert three rows after Gems:

```markdown
| 치장 뽑기·보유 수량·성급·도감·세트·200회 선택 상자 | [치장 SSOT](../../30-domain/cosmetics/ssot.md) | 2026-09-01 사용자 승인 설계, 치장 뽑기 요구사항 명세 |
| 치장 배너 비용·확률·활성 풀·도감 임계값·세트 효과 | [MVP 치장 뽑기 콘텐츠 v1](../../60-content/cosmetics/mvp-v1.md) | 치장 SSOT, 치장 뽑기 요구사항 설계; 현재 unresolved |
| 치장 뽑기 구현 범위·API·검증 | [치장 뽑기 시스템 요구사항 명세서](../../70-plans/cosmetic-gacha/requirements.md) | 치장 SSOT, MVP 치장 뽑기 콘텐츠 v1 |
```

- [ ] **Step 2: Add networking route ownership**

Add a `## 치장 뽑기 계약` section to `docs/50-architecture/networking.md` that lists the seven API purposes by link to the requirements document, not by copying DTOs. State:

- draw body contains only `count`;
- payment/result/probability are server-owned;
- state-changing APIs require common idempotency behavior;
- HTTP statuses and common envelope remain unresolved architecture contract items.

- [ ] **Step 3: Extend the MVP acceptance skeleton**

Change the routing-only flow in `docs/70-plans/mvp-release/acceptance.md` to include:

```text
계정 생성 → 로그인 → 주력 재료 선택 → 1-1 시작 → 1-5 최초 클리어·치장 해금 → 초기 뽑기권으로 픽업 뽑기·도감 등록 확인 → 중복 치장 승급·세트 효과 확인 → 펫 창 자동 진행 → … → 4-10 클리어 → 거래 등록·구매·정산 확인
```

Add one paragraph linking detailed cosmetic-gacha acceptance to the requirements document and noting that numeric probability/effect acceptance is blocked until `mvp-v1.md` is approved. Do not copy numeric rules into the routing document.

- [ ] **Step 4: Verify navigation and scope**

From `docs/README.md`, follow links to:

- Domain → Cosmetics SSOT → requirements/content;
- Content → Cosmetics content state;
- Plans → Cosmetic Gacha Requirements;
- source map → all three responsibility owners;
- MVP acceptance → requirements.

- [ ] **Step 5: Commit integration routes**

```bash
git add docs/wiki/00-meta/source-map.md docs/50-architecture/networking.md docs/70-plans/mvp-release/acceptance.md
git commit -m "docs: route cosmetic gacha contracts and acceptance"
```

### Task 7: Validate Documentation Integrity

**Files:**
- Verify: all files changed in Tasks 1–6

**Interfaces:**
- Consumes: Complete documentation propagation.
- Produces: Evidence that the requirements package has valid paths, clean Markdown, no unintended implementation artifacts, and no fabricated content values.

- [ ] **Step 1: Check whitespace and patch integrity**

Run:

```bash
git diff --check HEAD~6..HEAD
```

Expected: no output and exit code 0.

If commit count differs because tasks were squashed, run `git diff --check <base-commit>..HEAD` using the pre-plan base commit.

- [ ] **Step 2: Verify changed file scope**

Run:

```bash
git status --short
```

Expected: no uncommitted files from this plan. Confirm no source-code, manifest, migration, database, deployment, or executable file was created.

- [ ] **Step 3: Verify every relative Markdown link**

Use the repository's Markdown-link checker if one is introduced before execution. This repository currently has no checked-in validator, so otherwise run a one-off read-only script that:

1. scans changed Markdown files;
2. extracts relative Markdown link targets;
3. strips anchors and URL-decodes paths;
4. resolves each path relative to the source file;
5. reports every missing target;
6. exits nonzero if any target is missing.

Expected: zero missing relative targets.

- [ ] **Step 4: Verify SSOT and routing separation**

Inspect these routing files:

```text
docs/30-domain/cosmetics/README.md
docs/30-domain/cosmetics/features/_index.md
docs/30-domain/cosmetics/features/gacha.md
docs/30-domain/cosmetics/features/collection-growth.md
docs/70-plans/mvp-release/acceptance.md
```

Expected: they contain links and flow only; no copied probability, upgrade-cost, milestone, set-effect, or API error tables.

- [ ] **Step 5: Verify unresolved content safety**

Inspect `docs/60-content/cosmetics/mvp-v1.md` and confirm:

- `status: unresolved`;
- no common rice cost has been invented;
- no four-grade distribution has been invented;
- no cosmetic or set catalog has been invented;
- no collection milestone or set-effect value has been invented;
- implementation blocking and promotion criteria are explicit.

- [ ] **Step 6: Verify requirements traceability**

Confirm every requirement ID prefix appears in the traceability matrix and every API row points to at least one requirement group. Confirm the acceptance scenarios cover unlock, payment, draw, ownership, upgrade, collection, sets, milestone, selector box, concurrency, audit, invalid content, and combat.

- [ ] **Step 7: Verify repository entry points**

Confirm all of these links resolve by reading them:

```text
docs/30-domain/README.md → docs/30-domain/cosmetics/ssot.md
docs/60-content/README.md → docs/60-content/cosmetics/README.md
docs/70-plans/README.md → docs/70-plans/cosmetic-gacha/requirements.md
docs/wiki/00-meta/source-map.md → cosmetics SSOT, content v1, requirements
```

- [ ] **Step 8: Record final validation commit only if fixes were required**

If validation required edits:

```bash
git add docs
git commit -m "docs: fix cosmetic gacha documentation links"
```

If no edits were required, do not create an empty commit.
