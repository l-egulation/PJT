# 장비 M1 등급 연속성 보정 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `progression-rebalance-v1`의 희귀·영웅 M1 요구량을 상향해 노말 1강부터 전설 29강까지 M1이 감소하지 않게 한다.

**Architecture:** 콘텐츠는 이중 기장 구조다. `tools/content-validator/src/buildProgressionRebalance.ts`가 `splitBracketTotal(총량)`으로 29행 배열을 만들고 `equipment.json`·`progression.json`·`manifest.json` 체크섬까지 **생성**한다. `tools/content-validator/src/validate.ts`는 같은 규칙을 `bracketSteps`로 **독립 재계산**해 대조한다. 따라서 JSON을 손으로 고치지 않고 생성기 입력을 바꾼 뒤 재생성하며, validator의 기대값을 별도로 올려 두 장부가 일치하는지 검증한다.

**Tech Stack:** Kotlin(Gradle, JUnit5), TypeScript(pnpm, tsx, vitest, ajv), JSON 콘텐츠 팩

## Global Constraints

- 변경 대상은 M1뿐이다. M2·M3·M4 배열과 쌀 스케줄, 승급 stage gate, 능력치 규칙은 건드리지 않는다.
- 전설·신화 등급은 범위 밖이다. `LEGENDARY` 배열은 그대로 둔다.
- `progression-rebalance-v1`을 제자리 수정한다. 새 콘텐츠 버전을 만들지 않는다.
- 희귀 M1 총량 `3700`, 영웅 M1 총량 `10300`. 생성기의 `splitBracketTotal`과 validator의 `bracketSteps`가 같은 배열을 만든다.
- 확정 배열 (감자·고구마·옥수수 각각, 강화레벨 1~29):
  - `RARE.M1` = `74`×10, `111`×10, `205`×4, `206`×5
  - `EPIC.M1` = `206`×10, `309`×10, `572`×7, `573`×2
- 희귀→영웅 승급 `M1` = `206` (희귀 29강 M1). `M2` 23, `M3` 1, 쌀 1,770은 유지.
- `stage.03-10` 최초 클리어의 승급 스타터는 희귀→영웅 승급 비용과 정확히 같아야 한다. 따라서 스타터 `M1`도 `206`. `stage.01-10`은 노말 29강 M1이 74로 유지되므로 변경 없다.
- 최초 클리어 M1 지원은 강화 비용의 고정 비율을 유지한다. 2장 희귀 `30%` = `6,660`, 3장 희귀 `20%` = `4,440`, 4장 영웅 `30%` = `18,540`. 1장은 노말 비용이 그대로라 변경 없다.
- 각 장 1~9 스테이지의 배분은 `round(총량 × i / 45)`다.
- 콘텐츠 JSON과 `manifest.json`을 직접 편집하지 않는다. 생성기를 고치고 재생성한다.
- 같은 수치가 생성기와 validator 양쪽에 이중으로 들어 있다. 한쪽만 고치면 검증이 실패한다.
- pnpm은 PATH에 없다. 루트 스크립트(`corepack pnpm content:validate`)는 내부에서 `pnpm --filter`를 다시 호출해 Windows에서 해석에 실패하므로, `corepack pnpm --filter @hanjjak/content-validator validate` 형태로 직접 호출한다.

**콘텐츠 재생성 명령** (저장소 루트에서):

```bash
cd tools/content-validator && corepack pnpm exec tsx -e "import('./src/buildProgressionRebalance.ts').then(m => m.writeProgressionPackages('../../packages/game-content')).then(() => console.log('REGEN_OK'), e => { console.error('FAIL', e); process.exit(1); })" && cd ../..
```

이 명령은 변경 전 클린 트리에서 no-op임이 확인됐다. 즉 재생성 후 나타나는 diff는 전부 이번 변경의 결과다.

---

### Task 1: 승인 수치를 상향하고 기존 콘텐츠가 거부되는지 확인 — 완료 (`595f4e10`)

validator의 `approvedMaterialSchedules` 총량과 `expectedPromotions.RARE.materials.M1`, Kotlin `EquipmentRulesTest`의 강화·승급·6부위 합계 기대값을 상향했다. 콘텐츠는 아직 옛 값이라 두 검사가 실패하는 RED 상태다.

---

### Task 2: 생성기 입력을 상향하고 콘텐츠를 재생성한다

**Files:**
- Modify: `tools/content-validator/src/validate.ts` (`expectedFirstClear`의 `chapter === 3 && number === 10` 분기)
- Modify: `tools/content-validator/src/buildProgressionRebalance.ts:148` (`buildProgressionEquipmentContent`)
- Modify: `tools/content-validator/src/buildProgressionRebalance.ts:139` (`promotions`의 `RARE` 행)
- Modify: `tools/content-validator/src/buildProgressionRebalance.ts:89` (`firstClearFor`의 챕터 3 스테이지 10 분기)
- Regenerate (직접 편집 금지): `packages/game-content/versions/progression-rebalance-v1/equipment/equipment.json`, `.../progression/progression.json`, `.../manifest.json`

**Interfaces:**
- Consumes: Task 1이 확정한 승인 기대값 — 희귀 M1 총량 3,700, 영웅 M1 총량 10,300, 승급 M1 206.
- Produces: 런타임이 로드하는 최종 비용과 보상. Task 3의 문서와 Task 4의 회귀가 이 값을 인용한다.

- [ ] **Step 1: validator의 승급 스타터 기대값 상향**

`validate.ts`의 `expectedFirstClear`에서 챕터 3 스테이지 10 분기의 M1 수량만 바꾼다. 쌀 1,770과 M2 23, M3 1, `GEM_BOX` 30은 그대로 둔다.

```ts
    if (chapter === 3) return { rice: 1_770, items: [...expectedRewardMaterials(1, 206), ...expectedRewardMaterials(2, 23), ...expectedRewardMaterials(3, 1), { itemId: "GEM_BOX", quantity: 30 }], directSkillId: null };
```

- [ ] **Step 2: 생성기의 강화 M1 총량 상향**

`buildProgressionRebalance.ts:148`의 `buildProgressionEquipmentContent` 반환문에서 희귀·영웅 M1 총량만 바꾼다. `splitBracketTotal(500)` → `splitBracketTotal(3700)`, `splitBracketTotal(150)` → `splitBracketTotal(10300)`. M2·M3 총량과 `NORMAL`·`LEGENDARY`는 그대로 둔다.

```ts
enhancementMaterials: { NORMAL: { M1: normalMaterialSteps() }, RARE: { M1: splitBracketTotal(3700), M2: splitBracketTotal(400) }, EPIC: { M1: splitBracketTotal(10300), M2: splitBracketTotal(120), M3: splitBracketTotal(120) }, LEGENDARY: legacyMaterialSteps("LEGENDARY") }
```

- [ ] **Step 3: 생성기의 승급 M1 상향**

`buildProgressionRebalance.ts:139`의 `promotions` 함수 `RARE` 행에서 `rebalance` 쪽 M1만 바꾼다. `rebalance`가 거짓일 때의 `{ M3: 10 }` 분기와 stage gate, 쌀은 그대로 둔다.

```ts
    RARE: { requiredStageId: rebalance ? "stage.03-10" : "stage.02-10", materials: rebalance ? { M1: 206, M2: 23, M3: 1 } : { M3: 10 }, rice: rebalance ? 1_770 : 5_000, resultGrade: "EPIC" },
```

- [ ] **Step 4: 생성기의 승급 스타터 보상 상향**

`buildProgressionRebalance.ts:89`의 `firstClearFor` 챕터 3 스테이지 10 분기에서 M1 수량만 바꾼다.

```ts
    if (chapter === 3) return { rice: 1_770, items: [...rewardMaterials(1, 206), ...rewardMaterials(2, 23), ...rewardMaterials(3, 1), { itemId: "GEM_BOX", quantity: 30 }], directSkillId: null };
```

- [ ] **Step 5: 콘텐츠 재생성**

Run: 위 **콘텐츠 재생성 명령**

Expected: `REGEN_OK`

- [ ] **Step 6: 재생성이 의도한 파일만 바꿨는지 확인**

Run: `git status --short -uno`

Expected: 정확히 다섯 줄 — `tools/content-validator/src/validate.ts`, `tools/content-validator/src/buildProgressionRebalance.ts`, `packages/game-content/versions/progression-rebalance-v1/equipment/equipment.json`, `.../progression/progression.json`, `.../manifest.json`. 다른 파일이 나오면 멈추고 보고한다.

- [ ] **Step 7: 생성된 배열이 확정 배열과 같은지 확인**

Run:

```bash
node -e "const m=require('./packages/game-content/versions/progression-rebalance-v1/equipment/equipment.json').enhancementMaterials; console.log('RARE.M1', m.RARE.M1.join(',')); console.log('EPIC.M1', m.EPIC.M1.join(','));"
```

Expected:

```text
RARE.M1 74,74,74,74,74,74,74,74,74,74,111,111,111,111,111,111,111,111,111,111,205,205,205,205,206,206,206,206,206
EPIC.M1 206,206,206,206,206,206,206,206,206,206,309,309,309,309,309,309,309,309,309,309,572,572,572,572,572,572,572,573,573
```

- [ ] **Step 8: validator 통과 확인**

Run: `corepack pnpm --filter @hanjjak/content-validator validate`

Expected: PASS — 오류 출력 없음

- [ ] **Step 9: validator 자체 테스트 통과 확인**

Run: `corepack pnpm --filter @hanjjak/content-validator test`

Expected: PASS — 24/24, drift 거부 테스트 포함

- [ ] **Step 10: Kotlin 테스트 통과 확인**

Run: `./gradlew :modules:equipment:test`

Expected: PASS

- [ ] **Step 11: M1 단조 증가와 스타터 일치를 직접 검증**

Run:

```bash
node -e "const e=require('./packages/game-content/versions/progression-rebalance-v1/equipment/equipment.json'); const m=e.enhancementMaterials; const all=[...m.NORMAL.M1,...m.RARE.M1,...m.EPIC.M1,...m.LEGENDARY.M1]; const bad=all.map((v,i)=>i&&v<all[i-1]?i:-1).filter(i=>i>0); console.log('감소 지점:', bad.length?bad:'없음', '| 범위:', all[0], '->', all[all.length-1]); const p=require('./packages/game-content/versions/progression-rebalance-v1/progression/progression.json').stages.find(s=>s.stageId==='stage.03-10').firstClear; const q=g=>p.items.find(i=>i.itemId==='POTATO_M'+g).quantity; console.log('스타터 M1/M2/M3:', q(1), q(2), q(3), '| 승급:', e.promotion.RARE.materials.M1, e.promotion.RARE.materials.M2, e.promotion.RARE.materials.M3, '| 쌀:', p.rice, e.promotion.RARE.rice);"
```

Expected:

```text
감소 지점: 없음 | 범위: 40 -> 1500
스타터 M1/M2/M3: 206 23 1 | 승급: 206 23 1 | 쌀: 1770 1770
```

- [ ] **Step 12: 커밋**

```bash
git add tools/content-validator/src/validate.ts tools/content-validator/src/buildProgressionRebalance.ts packages/game-content/versions/progression-rebalance-v1
git commit -m "balance(equipment): keep M1 non-decreasing across grade boundaries"
```

---

### Task 3: 문서를 새 수치로 갱신

**Files:**
- Modify: `docs/30-domain/items/equipment/ssot.md:32` (환산가치 총량 문장)
- Modify: `docs/30-domain/items/equipment/ssot.md:34-40` (6부위 합계 표)
- Modify: `docs/30-domain/items/equipment/ssot.md:50` (부위별 총량 문장)
- Modify: `docs/30-domain/items/equipment/ssot.md:58-61` (승급 표)
- Modify: `docs/60-content/stages/first-clear-rebalance.md:80` (승급 스타터 문장)

**Interfaces:**
- Consumes: Task 2가 확정한 콘텐츠 수치.
- Produces: 없음. 문서가 마지막 소비자다.

- [ ] **Step 1: 환산가치 총량 문장 교체**

기존 문장의 `노말 9,996, 희귀 15,000, 영웅 22,500이며 희귀 이후 1.5배 증가한다`는 더 이상 성립하지 않는다. M1 비감소 제약이 희귀 총량을 끌어올리기 때문이다. 아래로 바꾼다.

```text
환산가치 `M1:M2:M3=1:5:25`에서 등급 총량은 노말 9,996, 희귀 34,200, 영웅 83,400이다. M1은 등급 경계에서 감소하지 않으며, 이 제약이 등급 총량 증가율을 결정한다.
```

- [ ] **Step 2: 6부위 합계 표의 M1 열 교체**

희귀 행 M1 `3,000` → `22,200`, 영웅 행 M1 `900` → `61,800`. M2·M3·쌀 열과 노말·전설·신화 행은 그대로 둔다.

```text
| 희귀 | 22,200 | 2,400 | — | — | — | 234,900 |
| 영웅 | 61,800 | 720 | 720 | — | — | 391,500 |
```

- [ ] **Step 3: 부위별 총량 문장 교체**

기존 `희귀 한 부위는 M1 500개·M2 400개, 영웅 한 부위는 M1 150개·M2 120개·M3 120개다`를 아래로 바꾼다. 뒤따르는 20%/30%/50% 분배 규칙 문장은 그대로 둔다.

```text
희귀 한 부위는 M1 3,700개·M2 400개, 영웅 한 부위는 M1 10,300개·M2 120개·M3 120개다.
```

- [ ] **Step 4: 승급 표의 희귀→영웅 M1 교체**

`| 희귀→영웅 | 28 | 23 | 1 | 1,770 |`을 아래로 바꾼다. 노말→희귀 행은 노말 29강 M1이 74로 유지되므로 변화가 없다.

```text
| 희귀→영웅 | 206 | 23 | 1 | 1,770 |
```

- [ ] **Step 5: 승급 스타터 문서 갱신**

`docs/60-content/stages/first-clear-rebalance.md:80`의 `승급 스타터는 M1 각 계열 28개, M2 각 계열 23개, M3 각 계열 1개와 쌀 1,770이다`에서 M1 수량만 바꾼다.

```text
승급 스타터는 M1 각 계열 206개, M2 각 계열 23개, M3 각 계열 1개와 쌀 1,770이다.
```

- [ ] **Step 6: 승급 규칙 문장이 여전히 성립하는지 확인**

장비 SSOT의 `승급은 ... 현재 등급 29→30강의 재료·쌀 비용에 다음 등급 1→2강이 요구하는 신규 상위 세대 재료의 10%를 추가한다`가 새 수치와 맞는지 확인한다. 희귀 29강 M1 206·M2 23에 영웅 1강 M3 2의 10%를 올림한 1을 더하면 `M1 206, M2 23, M3 1`이 된다. 문장 수정은 필요 없다.

- [ ] **Step 7: 커밋**

```bash
git add docs/30-domain/items/equipment/ssot.md docs/60-content/stages/first-clear-rebalance.md
git commit -m "docs(equipment): record M1 grade continuity totals"
```

---

### Task 4: 최초 클리어 M1 지원을 새 비용 비율로 재산출한다

최초 클리어 보상은 강화 비용의 고정 비율을 지원하는 체계다. [최초 클리어 콘텐츠](../../60-content/stages/first-clear-rebalance.md)가 1장 노말 약 80%, 2장 희귀 30%, 3장 희귀 20%, 4장 영웅 30%로 명시하며, 기존 수치가 옛 비용에 정확히 들어맞는다(2장 M1 3,000×0.30=900, 3장 3,000×0.20=600, 4장 900×0.30=270). 희귀·영웅 M1 비용이 올랐으므로 M1 지원도 같은 비율로 재산출한다. 1장은 노말 비용이 그대로라 변경 없다.

각 장의 1~9 스테이지는 `round(총량 × i / 45)` 선형 배분을 쓴다. 이 규칙이 기존 세 테이블을 정확히 재현함을 확인했다.

**Files:**
- Modify: `tools/content-validator/src/validate.ts` (`firstClearTables`의 `chapter2M1`·`chapter3M1`·`chapter4M1`)
- Modify: `tools/content-validator/src/buildProgressionRebalance.ts` (`firstClear`의 `chapter2M1`·`chapter3M1`·`chapter4M1`)
- Modify: `docs/60-content/stages/first-clear-rebalance.md` (2·3·4장 표의 `M1 각 계열` 열과 합계 행, 그리고 각 장 도입 문장의 계수)
- Regenerate (직접 편집 금지): `packages/game-content/versions/progression-rebalance-v1/progression/progression.json`, `.../manifest.json`

**Interfaces:**
- Consumes: Task 2가 확정한 6부위 M1 총량 — 희귀 22,200, 영웅 61,800.
- Produces: 비율이 복원된 최초 클리어 보상. Task 5의 회귀가 확인한다.

- [ ] **Step 1: validator 테이블 교체**

`validate.ts`의 `firstClearTables`에서 세 배열만 바꾼다. `chapter1M1`, 모든 `M2`·`M3`·`Rice` 배열은 그대로 둔다.

```ts
  chapter2M1: [148, 296, 444, 592, 740, 888, 1_036, 1_184, 1_332],
  chapter3M1: [99, 197, 296, 395, 493, 592, 691, 789, 888],
  chapter4M1: [412, 824, 1_236, 1_648, 2_060, 2_472, 2_884, 3_296, 3_708],
```

- [ ] **Step 2: 기존 콘텐츠가 거부되는지 확인**

Run: `corepack pnpm --filter @hanjjak/content-validator validate`

Expected: FAIL — `first-clear schedule mismatch: stage.02-01`

- [ ] **Step 3: 생성기 테이블 교체**

`buildProgressionRebalance.ts`의 `firstClear` 상수에서 같은 세 배열을 Step 1과 동일한 값으로 바꾼다. 나머지 배열은 그대로 둔다.

- [ ] **Step 4: 콘텐츠 재생성**

Run: 위 **콘텐츠 재생성 명령**

Expected: `REGEN_OK`

- [ ] **Step 5: 재생성이 의도한 파일만 바꿨는지 확인**

Run: `git status --short -uno`

Expected: 정확히 네 줄 — `tools/content-validator/src/validate.ts`, `tools/content-validator/src/buildProgressionRebalance.ts`, `packages/game-content/versions/progression-rebalance-v1/progression/progression.json`, `.../manifest.json`. `equipment.json`이 나오면 안 된다. 다른 파일이 나오면 멈추고 보고한다.

- [ ] **Step 6: validator 통과 확인**

Run: `corepack pnpm --filter @hanjjak/content-validator validate`

Expected: PASS — 오류 출력 없음

- [ ] **Step 7: validator 자체 테스트 통과 확인**

Run: `corepack pnpm --filter @hanjjak/content-validator test`

Expected: PASS. 생성기 단위 테스트(`buildProgressionRebalance.test.ts`)가 옛 최초 보상 수치를 핀으로 들고 있으면 같은 값으로 갱신하고, 무엇을 왜 바꿨는지 보고에 적는다.

- [ ] **Step 8: 장별 지원 비율이 복원됐는지 직접 검증**

Run:

```bash
node -e "const j=require('./packages/game-content/versions/progression-rebalance-v1/progression/progression.json'); const sum=(ch,last)=>j.stages.filter(s=>s.stageId.startsWith('stage.0'+ch+'-')&&+s.stageId.slice(-2)<=last).reduce((a,s)=>a+((s.firstClear&&s.firstClear.items.find(i=>i.itemId==='POTATO_M1')||{quantity:0}).quantity),0); const r=22200,e=61800; console.log('2장 1-9 M1', sum(2,9), '희귀 대비', (sum(2,9)/r*100).toFixed(1)+'%'); console.log('3장 1-9 M1', sum(3,9), '희귀 대비', (sum(3,9)/r*100).toFixed(1)+'%'); console.log('4장 1-9 M1', sum(4,9), '영웅 대비', (sum(4,9)/e*100).toFixed(1)+'%');"
```

Expected:

```text
2장 1-9 M1 6660 희귀 대비 30.0%
3장 1-9 M1 4440 희귀 대비 20.0%
4장 1-9 M1 18540 영웅 대비 30.0%
```

- [ ] **Step 9: 최초 클리어 콘텐츠 문서 표 갱신**

`docs/60-content/stages/first-clear-rebalance.md`의 2·3·4장 표에서 `M1 각 계열` 열의 아홉 값과 합계 행을 Step 1의 값으로 바꾼다. M2·M3·쌀·보석함 열과 1장 표는 그대로 둔다. 2장 도입 문장의 `M1 각 계열 \`20i\``도 `\`148i\``로 바꾼다. 3·4장 도입 문장에는 M1 계수가 없으므로 수정할 곳이 없다. 각 장의 비율 문장(30%·20%·30%)은 그대로 성립하므로 바꾸지 않는다.

- [ ] **Step 10: 커밋**

```bash
git add tools/content-validator/src/validate.ts tools/content-validator/src/buildProgressionRebalance.ts packages/game-content/versions/progression-rebalance-v1 docs/60-content/stages/first-clear-rebalance.md
git commit -m "balance(progression): rescale first-clear M1 support to raised costs"
```

---

### Task 5: 회귀와 실제 앱 확인

**Files:**
- 변경 없음. 검증과 증거 기록만 수행한다.
- Modify: `docs/wiki/06-delivery/tasks/A-release-scope/a-05-progression-rebalance.md` (증거 절)
- Modify: `docs/70-plans/progression-rebalance/verification.md` (환산가치 총량 검증 문장)

**Interfaces:**
- Consumes: Task 1~4의 결과물 전체.
- Produces: 없음.

- [ ] **Step 1: 장비·인벤토리 서버 테스트 회귀**

Run: `./gradlew :modules:equipment:test :modules:inventory:test`

Expected: PASS

- [ ] **Step 2: 콘텐츠·타입 회귀**

Run: `corepack pnpm --filter @hanjjak/content-validator validate && corepack pnpm -r typecheck`

Expected: PASS

- [ ] **Step 3: 로컬 서버·웹 기동**

Run: `./gradlew :apps:game-api:bootRun`, 별도 터미널에서 `corepack pnpm --filter @hanjjak/web dev`

Expected: 기동 성공

- [ ] **Step 4: 강화 화면 수치 육안 확인**

희귀·영웅 장비의 강화 화면에서 필요 재료를 확인한다.

Expected: 희귀 +1 → 한 조각 각 `74`, 미니 각 `8`. 영웅 +1 → 한 조각 각 `206`, 미니 각 `2`, C등급 각 `2`.

- [ ] **Step 5: 검증 메모의 폐기된 수치 정정**

`docs/70-plans/progression-rebalance/verification.md`의 `환산가치 \`1:5:25\`에서 노말 9,996, 희귀 15,000, 영웅 22,500과 희귀 이후 1.5배 증가가 일치함을 확인했다` 문장은 옛 비용을 검증한 기록이라 더 이상 성립하지 않는다. 아래로 바꾼다.

```text
- 환산가치 `1:5:25`에서 노말 9,996, 희귀 34,200, 영웅 83,400이 일치함을 확인했다. M1 비감소 제약을 도입하며 기존의 희귀 이후 1.5배 증가 규칙은 폐기했다.
```

- [ ] **Step 6: 확인 결과 기록**

`a-05-progression-rebalance.md`의 증거 절에 이번 보정과 확인 경로를 한 줄로 추가한다.

```bash
git add docs/wiki/06-delivery/tasks/A-release-scope/a-05-progression-rebalance.md docs/70-plans/progression-rebalance/verification.md
git commit -m "docs(equipment): record M1 continuity verification"
```

---

## 보류 항목

- **전설 구간.** 영웅 29강 M1 573에서 전설 1강 1,250으로 2.2배 오르므로 연속성은 깨지지 않는다. 다만 환산가치로는 큰 단차가 남는다. 리밸런스 v1 설계의 제외 항목이므로 이번 범위에 넣지 않는다.
- **balance-lab 재시뮬레이션.** M1 소비가 크게 늘어 진행 곡선이 달라진다. 10,000계정 시뮬레이션은 A-05가 이미 미완으로 추적 중이므로 그 작업에서 함께 수행한다.
