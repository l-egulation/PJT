# Gem Content Documentation Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synchronize the user-approved gem growth, gem box, ticket, daily boss, reward, and scaling decisions into the repository SSOT and content documents, then publish an `lsh` → `main` merge request.

**Architecture:** Keep rules in `docs/30-domain/gems/ssot.md`, draft numeric content in `docs/60-content/gems/mvp-v1.md`, and cross-domain authority or routing in the existing owner documents. Preserve `docs/60-content/enemies/mvp-v1.md` as the no-gem regression baseline and record gem-aware 3-1~4-10 rebalancing as a separate follow-up content version.

**Tech Stack:** Markdown SSOT/content documents, Git, GitLab push options

**Spec:** `docs/70-plans/gem-content/design.md`

## Global Constraints

- Do not create implementation source code, package manifests, database migrations, or deployment files.
- Do not duplicate domain rules in routing-only documents.
- Gems unlock at 3-1; gem boxes are bound while resulting gems are tradeable.
- Ticket rewards and deductions are server-authoritative, idempotent, and based on actual elapsed time.
- Preserve the current no-gem enemy v1 table until a separate gem-aware simulation produces a replacement.
- Mark approved mechanics separately from working numeric content and unresolved runtime implementation contracts.

---

### Task 1: Establish gem SSOT and numeric content ownership

**Files:**
- Modify: `docs/30-domain/gems/ssot.md`
- Create: `docs/60-content/gems/mvp-v1.md`
- Modify: `docs/60-content/gems/README.md`

**Interfaces:**
- Consumes: `docs/70-plans/gem-content/design.md`
- Produces: canonical gem mechanics and a versioned draft numeric table for all later links

- [ ] **Step 1: Replace unresolved gem mechanics with approved rules in the gem SSOT**

Record 3-1 unlock, six shared slots, 1~7 synthesis, level-specific option pools, duplicate-special highest-only semantics, gem box tradeability, ticket regeneration, fixed boss rotation, independent boss progress, first-clear rewards, sweep rewards, and server-time/idempotency invariants.

- [ ] **Step 2: Add draft gem content v1**

Create the fixed and special option values, gem box probabilities, reward table, stage target levels/Q, reference gem loadouts, and formulas. Mark absolute dungeon boss stats and the gem-aware enemy table as simulation outputs rather than invented constants.

- [ ] **Step 3: Route the gem content README to the new content file**

Link the gem SSOT, approved design, and `mvp-v1.md`; state that the content remains working until absolute boss values and the gem-aware main-stage table pass simulation.

- [ ] **Step 4: Verify Task 1 ownership and values**

Run:

```bash
rg -n "3-1|85%|8시간|생존형.*폭주형.*장갑형|30%|레벨 6|레벨 7" \
  docs/30-domain/gems/ssot.md docs/60-content/gems/mvp-v1.md
```

Expected: every approved mechanic and numeric table is present in the gem owner documents.

### Task 2: Synchronize adjacent domain and architecture boundaries

**Files:**
- Modify: `docs/30-domain/items/ssot.md`
- Modify: `docs/30-domain/combat/ssot.md`
- Modify: `docs/30-domain/player/ssot.md`
- Modify: `docs/30-domain/player/ux/ssot.md`
- Modify: `docs/60-content/schema.md`

**Interfaces:**
- Consumes: gem SSOT and gem content v1 from Task 1
- Produces: consistent tradeability, combat aggregation, server authority, UX, and content placement boundaries

- [ ] **Step 1: Update item tradeability**

State that gems are tradeable and gem boxes are account-bound. Keep option, synthesis, and dungeon behavior owned by the gem SSOT.

- [ ] **Step 2: Update combat aggregation boundary**

Link the gem SSOT/content for fixed and special stat aggregation, and explicitly retain enemy v1 as the no-gem regression baseline pending a gem-aware content version.

- [ ] **Step 3: Update account and server-time authority**

Record gem tickets, recharge timestamps, boss-specific clear progress, and first-clear claims as server-authoritative state. Keep local display state non-authoritative.

- [ ] **Step 4: Update client UX responsibilities**

Require display of current daily boss, independent progress, ticket count/recharge time, next challenge, cleared-stage sweep selection, first-clear reward, and sweep reward without moving game rules into the UX document.

- [ ] **Step 5: Update content schema placement**

Add gem boxes, gem definitions, dungeon schedules, stage targets, rewards, and future gem-aware enemy content to versioned game content ownership.

- [ ] **Step 6: Verify Task 2 has no authority contradictions**

Run:

```bash
rg -n "보석함|입장권|서버|회귀 기준|소탕" \
  docs/30-domain/items/ssot.md \
  docs/30-domain/combat/ssot.md \
  docs/30-domain/player/ssot.md \
  docs/30-domain/player/ux/ssot.md \
  docs/60-content/schema.md
```

Expected: each adjacent document links to the gem owner and only states its own responsibility.

### Task 3: Update plan, decision, and SSOT routing state

**Files:**
- Modify: `docs/70-plans/gem-content/requirements.md`
- Modify: `docs/70-plans/mvp-release/requirements.md`
- Modify: `docs/80-decisions/README.md`
- Modify: `docs/wiki/00-meta/source-map.md`

**Interfaces:**
- Consumes: Tasks 1–2 owner documents
- Produces: accurate confirmed/working status and discoverable canonical paths

- [ ] **Step 1: Reduce gem-content remaining inputs to actual simulation work**

Replace resolved questions with links to the design and content v1. Keep only absolute dungeon boss values, gem-aware 3-1~4-10 enemy values, distribution verification, and implementation contracts as remaining work.

- [ ] **Step 2: Align MVP scope**

State that gems unlock at 3-1 and that the current release endpoint remains 4-10 with extensible post-MVP dungeon tiers.

- [ ] **Step 3: Resolve decision-log conflicts**

Mark ticket regeneration, no-offline-cap conflict, gem box tradeability, option pools, rewards, and boss progression mechanics as confirmed. Keep numeric content and gem-aware enemy recalculation working until simulation evidence exists.

- [ ] **Step 4: Add content ownership to the SSOT map**

Add `docs/60-content/gems/mvp-v1.md` as the owner of draft gem values and dungeon stage/reward inputs while retaining `docs/30-domain/gems/ssot.md` as the mechanics owner.

- [ ] **Step 5: Verify decision status and routing**

Run:

```bash
rg -n "보석.*confirmed|보석.*working|mvp-v1.md|3-1|4-10" \
  docs/80-decisions/README.md \
  docs/wiki/00-meta/source-map.md \
  docs/70-plans/gem-content/requirements.md \
  docs/70-plans/mvp-release/requirements.md
```

Expected: confirmed mechanics and working numeric outputs are visibly separated.

### Task 4: Validate the documentation set

**Files:**
- Verify: all modified Markdown files

**Interfaces:**
- Consumes: Tasks 1–3
- Produces: evidence that links, required values, formatting, and repository cleanliness are valid

- [ ] **Step 1: Scan for placeholders and stale unresolved rules**

Run:

```bash
rg -n "TBD|TODO|FIXME|정확한 해금 스테이지는 미확정|하루 1회 보상만|입장권.*unresolved" \
  docs/30-domain/gems docs/60-content/gems docs/70-plans/gem-content docs/80-decisions/README.md
```

Expected: no stale rule or placeholder appears; deliberately working simulation outputs are described without placeholders.

- [ ] **Step 2: Validate relative Markdown links**

Run an inline Node.js Markdown-link checker over `docs/**/*.md`, ignoring external URLs, anchors, and archive paths.

Expected: zero broken relative links in canonical non-archive documentation.

- [ ] **Step 3: Validate numeric invariants**

Run an inline Node.js assertion script that checks option probabilities sum to 100%, gem-box probabilities sum to 100%, first-clear rewards equal `9 + stage`, sweep rewards equal `floor(30%)`, and ticket capacity/recharge constants are present.

Expected: all assertions pass.

- [ ] **Step 4: Check Git formatting and intended changes**

Run:

```bash
git diff --check
git status --short
git diff --stat
git diff -- docs/30-domain/gems/ssot.md docs/60-content/gems/mvp-v1.md docs/80-decisions/README.md
```

Expected: no whitespace errors and only intended Markdown files are changed.

### Task 5: Commit, push, and create the merge request

**Files:**
- Commit: all approved documentation changes

**Interfaces:**
- Consumes: verified Task 4 working tree
- Produces: remote `lsh` commit and an `lsh` → `main` GitLab merge request

- [ ] **Step 1: Commit the synchronized documentation**

Run:

```bash
git add docs
git commit -m "docs: 보석 성장과 던전 콘텐츠 확정"
```

Expected: one documentation commit on `lsh`.

- [ ] **Step 2: Push and request a merge into main**

Run:

```bash
git push -u origin lsh \
  -o merge_request.create \
  -o merge_request.target=main \
  -o merge_request.title="docs: 보석 성장과 던전 콘텐츠 확정" \
  -o merge_request.description="보석 옵션·합성·보석함·입장권·일일 보스·보상·단계 스펙 초안을 SSOT와 콘텐츠 문서에 동기화합니다."
```

Expected: push succeeds and GitLab returns an existing or newly created merge request URL.

- [ ] **Step 3: Verify remote state**

Run:

```bash
git fetch --prune origin
git status --short --branch
git rev-list --left-right --count lsh...origin/lsh
git log -1 --oneline --decorate
```

Expected: clean worktree, `0 0` divergence from `origin/lsh`, and the new documentation commit at HEAD.
