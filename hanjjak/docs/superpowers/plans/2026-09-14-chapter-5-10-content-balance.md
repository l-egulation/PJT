# 챕터 5-10 콘텐츠·밸런스 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 챕터 1-4 / 40스테이지로 하드코딩된 게임을 챕터 1-10 / 100스테이지로 확장하고, 라이브 밸런스 버전(`progression-rebalance-v1`)의 일반 몬스터 공격력을 HP 종속 모델로 교체한다.

**Architecture:** 적 수치는 `apps/balance-lab`의 생성기(`generatedStageRows()`)가 계산해 `stages.json`에 write하고, 진행·장비·드롭 밸런스는 `tools/content-validator`의 TS 생성기(`buildProgressionRebalance.ts`)가 생성한다. 따라서 "수치를 손으로 적는" 작업은 없다 — 두 생성기의 상한(40→100, 챕터 4→10)과 공식을 고치고 재실행하는 것이 구현의 본체다. 보스 HP·공격·강타는 생성기가 기준 빌드 대비 이진탐색으로 자동 산출한다.

**Tech Stack:** Kotlin (Gradle, JUnit/kotlin.test), TypeScript (pnpm workspace, vitest, Ajv 2020-12), JSON Schema, JSON 콘텐츠.

## Global Constraints

- 작업 위치: 워크트리 `C:\Users\SSAFY\Desktop\tpjt\S15P21B107\.worktrees\chapter-5-10`, 브랜치 `feat/stage-5-10-chapters`. 원본 체크아웃으로 `cd` 하지 않는다.
- `pnpm`은 PATH에 없다. 반드시 `corepack pnpm@10.33.2 <cmd>` 로 실행한다. 루트 스크립트 중 내부에서 bare `pnpm`을 부르는 것(`content:validate`)은 실패하므로 `corepack pnpm@10.33.2 --filter <pkg> <script>` 로 직접 호출한다.
- Gradle은 `./gradlew` (Windows에서는 Bash 툴로 `./gradlew`) 사용.
- 챕터당 스테이지 수는 10으로 고정. 글로벌 인덱스 `i = 10 × (chapter − 1) + number`, 최대 100.
- 기준 레벨 `level = 2i − 1` (스테이지 100 → 199). 플레이어 레벨 상한은 200.
- 레거시 `v1` 콘텐츠는 **동결**: 40스테이지 유지, 수치 변경 금지. 확장은 `progression-rebalance-v1`에만 적용한다.
- 재료 세대는 M1~M4 유지 (신규 세대 도입 없음).
- 성장벽 검사축 회전은 주기 3: 챕터 c의 패턴은 챕터 `((c−1) mod 3) + 1` 과 같다.
- 아트(테마·보스명·배경·스프라이트)는 이번 범위 밖 — 신규 스테이지는 프레젠테이션 필드를 생략한다(챕터 4와 동일, 클라이언트가 챕터 1로 무음 폴백).

---

### Task 1: 스테이지 도메인 상한 해제

**Files:**
- Modify: `modules/stage/src/main/kotlin/com/hanjjak/stage/domain/Stage.kt`
- Modify: `modules/stage/src/main/kotlin/com/hanjjak/stage/infrastructure/JsonStageCatalog.kt:28`
- Test: `modules/stage/src/test/kotlin/com/hanjjak/stage/domain/StageTest.kt`

**Interfaces:**
- Produces: `StageId.MAX_CHAPTER = 10`, `StageId.STAGES_PER_CHAPTER = 10`, `StageId.MAX_GLOBAL_INDEX = 100`; `StageDefinition.bossOnly` = 모든 챕터의 10스테이지.
- Consumes: 없음 (첫 태스크).

- [ ] **Step 1: 실패하는 테스트 작성**

`modules/stage/src/test/kotlin/com/hanjjak/stage/domain/StageTest.kt` 전체를 아래로 교체:

```kotlin
package com.hanjjak.stage.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class StageTest {
    @Test
    fun `global index and stable key follow stage contract`() {
        val stage = StageId(4, 10)
        assertEquals(40, stage.globalIndex)
        assertEquals("stage.04-10", stage.key)
    }

    @Test
    fun `chapters five through ten are addressable`() {
        assertEquals(50, StageId(5, 10).globalIndex)
        assertEquals("stage.05-10", StageId(5, 10).key)
        assertEquals(100, StageId(10, 10).globalIndex)
        assertEquals("stage.10-10", StageId(10, 10).key)
    }

    @Test
    fun `chapter beyond the maximum is rejected`() {
        assertFailsWith<IllegalArgumentException> { StageId(11, 1) }
        assertFailsWith<IllegalArgumentException> { StageId(0, 1) }
        assertFailsWith<IllegalArgumentException> { StageId(5, 11) }
    }

    @Test
    fun `every chapter final stage is boss only`() {
        assertTrue(definition(StageId(1, 10)).bossOnly)
        assertTrue(definition(StageId(7, 10)).bossOnly)
        assertFalse(definition(StageId(7, 9)).bossOnly)
    }

    private fun definition(id: StageId) = StageDefinition(
        id = id,
        enemyLevel = 1,
        enemyDefense = 0,
        normalHp = 1,
        normalAttack = 0,
        bossHp = 1,
        bossAttack = 0,
        bossType = BossType.STANDARD,
    )
}
```

- [ ] **Step 2: 실패 확인**

Run: `./gradlew :modules:stage:test --tests "com.hanjjak.stage.domain.StageTest"`
Expected: FAIL — `chapters five through ten are addressable` 에서 `IllegalArgumentException` (require(chapter in 1..4)), `every chapter final stage is boss only` 에서 `StageId(7,10)` 생성 실패.

- [ ] **Step 3: `Stage.kt` 수정**

`modules/stage/src/main/kotlin/com/hanjjak/stage/domain/Stage.kt` 의 `StageId` 와 `StageDefinition.bossOnly` 를 교체:

```kotlin
package com.hanjjak.stage.domain

data class StageId(val chapter: Int, val number: Int) {
    init { require(chapter in 1..MAX_CHAPTER && number in 1..STAGES_PER_CHAPTER) }
    val key: String = "stage.%02d-%02d".format(chapter, number)
    val globalIndex: Int = STAGES_PER_CHAPTER * (chapter - 1) + number

    companion object {
        const val MAX_CHAPTER: Int = 10
        const val STAGES_PER_CHAPTER: Int = 10
        const val MAX_GLOBAL_INDEX: Int = MAX_CHAPTER * STAGES_PER_CHAPTER
    }
}
```

같은 파일의 `StageDefinition` 안에서 `bossOnly` 한 줄만 교체:

```kotlin
    val bossOnly: Boolean = id.number == StageId.STAGES_PER_CHAPTER
```

- [ ] **Step 4: `JsonStageCatalog.kt` 스테이지 수 가드 완화**

`modules/stage/src/main/kotlin/com/hanjjak/stage/infrastructure/JsonStageCatalog.kt:28` 의 `.also { ... }` 를 교체:

```kotlin
    }.also { rows ->
        require(rows.isNotEmpty() && rows.size % StageId.STAGES_PER_CHAPTER == 0 && rows.size <= StageId.MAX_GLOBAL_INDEX) {
            "expected whole chapters up to ${StageId.MAX_GLOBAL_INDEX} stages, got ${rows.size}"
        }
    }
```

- [ ] **Step 5: 테스트 통과 확인**

Run: `./gradlew :modules:stage:test`
Expected: PASS (StageTest 4개 + StageProgressionTest + VersionedStageCatalogTest 모두 통과)

- [ ] **Step 6: 커밋**

```bash
git add modules/stage/src/main/kotlin/com/hanjjak/stage/domain/Stage.kt modules/stage/src/main/kotlin/com/hanjjak/stage/infrastructure/JsonStageCatalog.kt modules/stage/src/test/kotlin/com/hanjjak/stage/domain/StageTest.kt
git commit -m "feat(stage): allow chapters 5-10 in the stage domain"
```

---

### Task 2: 진행 도메인 상한 해제

**Files:**
- Modify: `modules/progression/src/main/kotlin/com/hanjjak/progression/domain/Progression.kt:80-98`
- Test: `modules/progression/src/test/kotlin/com/hanjjak/progression/domain/ProgressionRulesTest.kt`

**Interfaces:**
- Consumes: Task 1의 `StageId.MAX_GLOBAL_INDEX` 는 다른 모듈이므로 직접 참조하지 않는다 — 이 파일은 자체 상수 `MAX_STAGE_INDEX = 100` 을 쓴다.
- Produces: `ProgressionRules.experienceForNormal(globalIndex: Int): Long` 이 `1..100` 을 받는다; `grantForEnemy("stage.10-10", boss)` 가 동작한다.

- [ ] **Step 1: 실패하는 테스트 추가**

`modules/progression/src/test/kotlin/com/hanjjak/progression/domain/ProgressionRulesTest.kt` 클래스 안에 아래 테스트를 추가:

```kotlin
    @Test
    fun `chapters five through ten grant experience and rice`() {
        val grant = ProgressionRules.grantForEnemy("stage.10-10", boss = true)
        assertEquals(ProgressionRules.experienceForNormal(100) * 5, grant.experience)
        assertEquals(1_000, grant.rice)
        assertTrue(ProgressionRules.experienceForNormal(100) > ProgressionRules.experienceForNormal(40))
    }

    @Test
    fun `stage index beyond one hundred is rejected`() {
        assertFailsWith<IllegalArgumentException> { ProgressionRules.experienceForNormal(101) }
        assertFailsWith<IllegalArgumentException> { ProgressionRules.grantForEnemy("stage.11-01", boss = false) }
    }
```

파일 상단 import 에 아래가 없으면 추가:

```kotlin
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue
```

- [ ] **Step 2: 실패 확인**

Run: `./gradlew :modules:progression:test --tests "com.hanjjak.progression.domain.ProgressionRulesTest"`
Expected: FAIL — `INVALID_STAGE_INDEX` / `STAGE_NOT_FOUND` (globalIndex 1..40, chapter 1..4 가드)

- [ ] **Step 3: 가드 확장**

`Progression.kt` 의 `object ProgressionRules` 안에서 아래 세 곳을 수정.

상수 추가 (`const val MAX_LEVEL: Int = 500` 바로 아래):

```kotlin
    const val MAX_STAGE_INDEX: Int = 100
    private const val STAGES_PER_CHAPTER: Int = 10
```

`experienceForNormal` 의 require 교체:

```kotlin
    fun experienceForNormal(globalIndex: Int): Long {
        require(globalIndex in 1..MAX_STAGE_INDEX) { "INVALID_STAGE_INDEX" }
        val referenceLevel = 2 * globalIndex - 1
        val levelMinutes = 6.0 + (11.0 / 30.0) * (2 * referenceLevel - 1)
        return (1_000.0 * referenceLevel / (50.0 * levelMinutes)).roundToLong().coerceAtLeast(1)
    }
```

`globalIndex` 의 require 교체:

```kotlin
    private fun globalIndex(stageId: String): Int {
        val match = Regex("^stage\\.(\\d{2})-(\\d{2})$").matchEntire(stageId) ?: throw IllegalArgumentException("STAGE_NOT_FOUND")
        val chapter = match.groupValues[1].toInt()
        val number = match.groupValues[2].toInt()
        require(chapter in 1..(MAX_STAGE_INDEX / STAGES_PER_CHAPTER) && number in 1..STAGES_PER_CHAPTER) { "STAGE_NOT_FOUND" }
        return STAGES_PER_CHAPTER * (chapter - 1) + number
    }
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `./gradlew :modules:progression:test`
Expected: PASS

- [ ] **Step 5: 커밋**

```bash
git add modules/progression/src/main/kotlin/com/hanjjak/progression/domain/Progression.kt modules/progression/src/test/kotlin/com/hanjjak/progression/domain/ProgressionRulesTest.kt
git commit -m "feat(progression): grant rewards for chapters 5-10"
```

---

### Task 3: 콘텐츠 JSON Schema 확장

**Files:**
- Modify: `packages/game-content/schema/stages.schema.json:9,18`
- Modify: `packages/game-content/schema/material-drops.schema.json:13-17`
- Modify: `packages/game-content/schema/progression.schema.json:13,16`

**Interfaces:**
- Produces: 스테이지 배열 40~100행 허용, `chapter` 1..10, `chapterGenerationWeights`/`chapters` 키 1..10(1-4 필수), `levelRange.max` 200 허용.
- Consumes: 없음. (Task 5의 validator가 버전별 정확한 행 수를 강제한다.)

- [ ] **Step 1: `stages.schema.json` 수정**

`packages/game-content/schema/stages.schema.json` 9번 줄의 `stages` 를 교체:

```json
    "stages": { "type": "array", "minItems": 40, "maxItems": 100, "items": { "$ref": "#/$defs/stage" } }
```

18번 줄의 `chapter` 를 교체:

```json
        "chapter": { "type": "integer", "minimum": 1, "maximum": 10 },
```

- [ ] **Step 2: `material-drops.schema.json` 수정**

`packages/game-content/schema/material-drops.schema.json` 의 `chapterGenerationWeights` 와 `chapters` 블록(13-17번 줄)을 교체:

```json
    "chapterGenerationWeights": {
      "type": "object", "required": ["1", "2", "3", "4"], "additionalProperties": false,
      "properties": {
        "1": { "$ref": "#/$defs/weights" }, "2": { "$ref": "#/$defs/weights" }, "3": { "$ref": "#/$defs/weights" },
        "4": { "$ref": "#/$defs/weights" }, "5": { "$ref": "#/$defs/weights" }, "6": { "$ref": "#/$defs/weights" },
        "7": { "$ref": "#/$defs/weights" }, "8": { "$ref": "#/$defs/weights" }, "9": { "$ref": "#/$defs/weights" },
        "10": { "$ref": "#/$defs/weights" }
      }
    },
    "chapters": {
      "type": "object", "required": ["1", "2", "3", "4"], "additionalProperties": false,
      "properties": {
        "1": { "$ref": "#/$defs/chapter" }, "2": { "$ref": "#/$defs/chapter" }, "3": { "$ref": "#/$defs/chapter" },
        "4": { "$ref": "#/$defs/chapter" }, "5": { "$ref": "#/$defs/chapter" }, "6": { "$ref": "#/$defs/chapter" },
        "7": { "$ref": "#/$defs/chapter" }, "8": { "$ref": "#/$defs/chapter" }, "9": { "$ref": "#/$defs/chapter" },
        "10": { "$ref": "#/$defs/chapter" }
      }
    }
```

- [ ] **Step 3: `progression.schema.json` 수정**

`packages/game-content/schema/progression.schema.json` 13번 줄의 `levelRange` 두 번째 분기를 교체:

```json
        { "type": "object", "additionalProperties": false, "required": ["min", "max"], "properties": { "min": { "const": 1 }, "max": { "enum": [79, 200] } } }
```

16번 줄의 `stages` 를 교체:

```json
    "stages": { "type": "array", "minItems": 40, "maxItems": 100, "items": { "$ref": "#/$defs/stage" } }
```

- [ ] **Step 4: 기존 콘텐츠가 여전히 스키마를 통과하는지 확인**

Run: `corepack pnpm@10.33.2 --filter @hanjjak/content-validator validate`
Expected: exit 0 (스키마만 느슨해졌으므로 현행 40스테이지 콘텐츠는 그대로 통과)

- [ ] **Step 5: 커밋**

```bash
git add packages/game-content/schema/stages.schema.json packages/game-content/schema/material-drops.schema.json packages/game-content/schema/progression.schema.json
git commit -m "feat(content): widen schemas to 100 stages and level 200"
```

---

### Task 4: TS 생성기·밸리데이터 확장

**Files:**
- Modify: `tools/content-validator/src/buildProgressionRebalance.ts:59-72,83-96,97-110,150-153`
- Modify: `tools/content-validator/src/validate.ts` (스테이지/진행 행 수 검사, material-drops 챕터 루프)
- Test: `tools/content-validator/src/buildProgressionRebalance.test.ts`

**Interfaces:**
- Consumes: Task 3의 완화된 스키마.
- Produces: `buildProgressionRebalanceContent()` 가 100행 + `levelRange {min:1,max:200}`; `buildRebalanceMaterialDrops()` 가 챕터 1-10; `validateContent()` 가 v1=40행 / progression-rebalance-v1=100행을 강제.

- [ ] **Step 1: 실패하는 테스트 추가**

`tools/content-validator/src/buildProgressionRebalance.test.ts` 에 추가:

```ts
it("covers one hundred stages and the raised level cap", () => {
  const content = buildProgressionRebalanceContent();
  expect(content.stages).toHaveLength(100);
  expect(content.levelRange).toEqual({ min: 1, max: 200 });
  expect(content.stages[99]!.stageId).toBe("stage.10-10");
  expect(content.stages[99]!.bossExperience).toBe(content.stages[99]!.normalExperience * 5);
  expect(content.stages[99]!.bossRice).toBe(1_000);
});

it("keeps material generations at M1-M4 for every chapter", () => {
  const drops = buildRebalanceMaterialDrops();
  expect(Object.keys(drops.chapterGenerationWeights)).toHaveLength(10);
  expect(drops.chapterGenerationWeights["10"]).toEqual([125, 25, 5, 1]);
  expect(drops.chapters["10"]!.mode).toBe("CHANCE");
});
```

파일 상단 import 에 `buildRebalanceMaterialDrops` 가 없으면 추가한다.

- [ ] **Step 2: 실패 확인**

Run: `corepack pnpm@10.33.2 --filter @hanjjak/content-validator test`
Expected: FAIL — `expected 40 to be 100`

- [ ] **Step 3: `buildProgressionRebalance.ts` 확장**

(a) `ProgressionContent` 타입의 `levelRange` (31번 줄):

```ts
  levelRange: { min: 1; max: 200 } | null;
```

(b) `firstClearFor` (83-96번 줄) 는 **변경하지 않는다.** 챕터 1·2·3만 특수 분기이고 나머지는 마지막 `return` 으로 fall-through 하므로, 챕터 5-10은 이미 승인된 챕터 4 스케줄(M1 `6n`, M2·M3 램프, 쌀 `2610n`)을 그대로 받는다. 챕터 10스테이지도 `number === 10` 분기의 `return null` 로 이미 처리된다.

(c) `buildProgression` (97-110번 줄) 의 스테이지 개수와 levelRange:

```ts
function buildProgression(authority: Authority, contentVersion: string, rebalance: boolean): ProgressionContent {
  const stageCount = rebalance ? 100 : 40;
  return {
    authority,
    contentVersion,
    levelRange: rebalance ? { min: 1, max: 200 } : null,
    stages: Array.from({ length: stageCount }, (_, offset) => {
      const globalIndex = offset + 1;
      const referenceLevel = 2 * globalIndex - 1;
      const levelMinutes = rebalance ? 2 + (11 / 90) * (2 * referenceLevel - 1) : 6 + (11 / 30) * (2 * referenceLevel - 1);
      const normalExperience = Math.max(1, Math.round(1_000 * referenceLevel / (50 * levelMinutes)));
      return { stageId: stageId(globalIndex), normalExperience, bossExperience: normalExperience * 5, normalRice: globalIndex, bossRice: globalIndex * 10, firstClear: rebalance ? firstClearFor(globalIndex) : null };
    }),
  };
}
```

(d) `buildRebalanceMaterialDrops` (150-153번 줄) 를 교체:

```ts
export function buildRebalanceMaterialDrops(): MaterialDropsContent {
  const baseWeights: Record<string, number[]> = { "1": [1, 0, 0, 0], "2": [5, 1, 0, 0], "3": [25, 5, 1, 0], "4": [125, 25, 5, 1] };
  const chapterGenerationWeights: Record<string, number[]> = { ...baseWeights };
  for (let chapter = 5; chapter <= 10; chapter += 1) chapterGenerationWeights[String(chapter)] = [125, 25, 5, 1];
  const chapters: Record<string, MaterialDropChapter> = {
    "1": { mode: "GUARANTEED", normalQuantity: 2, bossQuantity: 10, generationWeights: chapterGenerationWeights["1"]! },
  };
  for (let chapter = 2; chapter <= 10; chapter += 1) {
    chapters[String(chapter)] = { mode: "CHANCE", normalQuantity: 10, bossQuantity: 10, generationWeights: chapterGenerationWeights[String(chapter)]! };
  }
  return { authority: "applied", contentVersion: BALANCE_VERSION, dropChanceBasisPoints: 1_500, quantityPerSuccess: 10, normalEnemyRolls: 1, bossRolls: 5, chapterGenerationWeights, chapters };
}
```

- [ ] **Step 4: `validate.ts` 의 40/4챕터 가정 해제**

`tools/content-validator/src/validate.ts` 에서 아래를 수정한다.

(a) 진행 콘텐츠 행 수 검사 — `progression.stages.length !== 40` 를 버전별 기대값으로 교체:

```ts
      const expectedStageCount = version === BALANCE_VERSION ? 100 : 40;
      if (progression.stages.length !== expectedStageCount) throw new Error(`${version} progression must contain ${expectedStageCount} stages`);
```

(b) 스테이지 ID/보상 스케줄 루프의 `40` 을 `expectedStageCount` 로 교체:

```ts
      for (let index = 0; index < expectedStageCount; index += 1) {
```

(c) `validateMaterialDropsContent` 의 챕터 루프를 콘텐츠 기준으로 교체:

```ts
function validateMaterialDropsContent(content: MaterialDropsContent): void {
  const expected = [[1, 0, 0, 0], [5, 1, 0, 0], [25, 5, 1, 0], [125, 25, 5, 1]];
  const chapters = Object.keys(content.chapterGenerationWeights).map(Number).sort((left, right) => left - right);
  if (chapters[0] !== 1 || chapters.some((chapter, index) => chapter !== index + 1)) throw new Error("material chapters must be a 1..n run");
  for (const chapter of chapters) {
    const weights = content.chapterGenerationWeights[String(chapter)];
    if (!weights) throw new Error(`missing material weights in chapter ${chapter}`);
    const row = expected[Math.min(chapter, 4) - 1]!;
    if (weights.some((weight, index) => weight !== row[index])) throw new Error(`unexpected material weights in chapter ${chapter}`);
    if (chapter <= 4 && weights.some((weight, index) => index >= chapter && weight !== 0)) throw new Error(`locked material generation in chapter ${chapter}`);
  }
  if (content.normalEnemyRolls * 20 + content.bossRolls !== 25) throw new Error("material cycle must have 25 independent rolls");
}
```

- [ ] **Step 5: 테스트·타입체크 통과 확인**

Run: `corepack pnpm@10.33.2 --filter @hanjjak/content-validator test`
Expected: PASS

Run: `corepack pnpm@10.33.2 --filter @hanjjak/content-validator typecheck`
Expected: exit 0

- [ ] **Step 6: 커밋**

```bash
git add tools/content-validator/src
git commit -m "feat(content-validator): generate and validate 100 stages"
```

---

### Task 5: balance-lab 생성기 확장 + 공격력 모델 교체

**Files:**
- Modify: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/ProgressionRebalance.kt:181-192,196-207,263-271,1043-1086`
- Modify: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/Main.kt:60-79`
- Test: `apps/balance-lab/src/test/kotlin/com/hanjjak/balancelab/ProgressionRebalanceTest.kt`

**Interfaces:**
- Consumes: Task 1·2의 도메인 상한.
- Produces: `generatedStageRows()` 가 100행 반환; `referenceQ(1..100)`; `bossTypeFor(chapter, number)` 가 챕터 1-10 지원; 일반 공격 = `round(NORMAL_ATTACK_HP_RATIO × 기준빌드 최대HP)`.

- [ ] **Step 1: 실패하는 테스트 추가**

`apps/balance-lab/src/test/kotlin/com/hanjjak/balancelab/ProgressionRebalanceTest.kt` 에 추가:

```kotlin
    @Test
    fun `generator covers one hundred stages with rotating growth walls`() {
        val rows = generatedStageRows()
        assertEquals(100, rows.size)
        assertEquals("stage.10-10", rows.last().id)
        assertEquals(199, rows.last().level)
        assertEquals("PENETRATION_CHECK", bossTypeFor(10, 10))
        assertEquals("PENETRATION_CHECK", bossTypeFor(5, 4))
        assertEquals("ATTACK_CHECK", bossTypeFor(5, 7))
        assertEquals("HP_CHECK", bossTypeFor(5, 10))
    }

    @Test
    fun `normal attack tracks reference max hp instead of a flat ramp`() {
        val rows = generatedStageRows()
        val early = rows.first { it.id == "stage.01-01" }
        val late = rows.first { it.id == "stage.10-09" }
        assertTrue(early.normalAttack >= 4, "early attack should not be the old 2-point ramp: ${early.normalAttack}")
        assertTrue(late.normalAttack > early.normalAttack * 10, "late attack should scale with reference HP: ${late.normalAttack}")
    }

    @Test
    fun `reference q keeps climbing through chapter ten`() {
        assertEquals(93, referenceQ(40))
        assertTrue(referenceQ(100) > referenceQ(40))
        assertTrue((1..100).map(::referenceQ).zipWithNext().all { (left, right) -> right >= left })
    }
```

파일 상단 import 에 `kotlin.test.assertTrue` 가 없으면 추가한다.

- [ ] **Step 2: 실패 확인**

Run: `./gradlew :apps:balance-lab:test --tests "com.hanjjak.balancelab.ProgressionRebalanceTest"`
Expected: FAIL — `referenceQ` 의 `require(stageIndex in 1..40)`, `generatedStageRows()` 가 40행.

- [ ] **Step 3: `referenceQ` 끝점 연장**

`ProgressionRebalance.kt:181-192` 의 `referenceQ` 를 교체:

```kotlin
internal fun referenceQ(stageIndex: Int): Int {
    require(stageIndex in 1..100)
    val endpoints = listOf(
        1 to 0, 10 to 25, 20 to 54, 30 to 64, 40 to 93,
        50 to 116, 60 to 139, 70 to 162, 80 to 185, 90 to 208, 100 to 231,
    )
    val segment = endpoints.zipWithNext().first { stageIndex in it.first.first..it.second.first }
    val left = segment.first
    val right = segment.second
    return (
        left.second +
            (stageIndex - left.first).toDouble() / (right.first - left.first) *
            (right.second - left.second)
        ).roundToInt()
}
```

- [ ] **Step 4: 성장벽 축 회전을 주기 3으로 일반화**

`ProgressionRebalance.kt:196-201` 의 `bossTypeFor` 를 교체:

```kotlin
/** Growth-wall check axes rotate with period 3, so chapter c matches chapter ((c-1) % 3) + 1. */
internal fun bossTypeFor(chapter: Int, number: Int): String = when (((chapter - 1) % 3) + 1 to number) {
    1 to 4, 2 to 7, 3 to 10 -> "ATTACK_CHECK"
    1 to 7, 2 to 10, 3 to 4 -> "HP_CHECK"
    1 to 10, 2 to 4, 3 to 7 -> "PENETRATION_CHECK"
    else -> "STANDARD"
}
```

- [ ] **Step 5: 보석 기준을 40스테이지 이후로 연장**

`ProgressionRebalance.kt:203-207` 의 `referenceGemLevels` 를 교체:

```kotlin
internal fun referenceGemLevels(stageIndex: Int): List<Int> = when {
    stageIndex == 30 -> listOf(2, 2, 2, 3, 3, 3)
    stageIndex >= 40 -> List(6) { 4 }
    else -> emptyList()
}
```

- [ ] **Step 6: 일반 공격력을 기준 빌드 최대 HP 종속으로 교체**

`ProgressionRebalance.kt:262-271` 의 `normalStats` 를 교체하고 바로 위에 비율 상수를 추가:

```kotlin
/** Normal-monster attack is a fixed share of the reference build's max HP so felt pressure stays flat. */
internal const val NORMAL_ATTACK_HP_RATIO: Double = 0.015

/** Enemy synthesis is content-shaped, not a clock target or a sampled time curve. */
private fun normalStats(stageIndex: Int): EnemyStats {
    val level = referenceLevel(stageIndex)
    val q = referenceQ(stageIndex)
    val referenceMaxHp = referenceFighter(level, q, q, q, referenceGemLevels(stageIndex), "STANDARD").maxHp
    return EnemyStats(
        hp = (600 + level * 30 + q * 50).coerceAtLeast(1),
        attack = (NORMAL_ATTACK_HP_RATIO * referenceMaxHp).roundToInt().coerceAtLeast(1),
        defense = q * 5,
    )
}
```

- [ ] **Step 7: 생성기를 100스테이지로 확장하고 보스 공격 비율을 2.0으로 고정**

`ProgressionRebalance.kt:1043-1086` 의 `generatedStageRows` 에서 범위와 보스 공격만 수정한다. 첫 줄:

```kotlin
internal fun generatedStageRows(): List<StageRow> = (1..100).map { stage ->
```

그리고 같은 함수 안의 `baseBossAttack` 한 줄을 교체:

```kotlin
    val baseBossAttack = if (type == "HP_CHECK" || type == "PENETRATION_CHECK") 0 else normal.attack * 2
```

- [ ] **Step 8: 시뮬레이션 게이트를 챕터 1-10으로 확장**

`Main.kt:60-79` 의 게이트 블록을 교체한다. 챕터 1-4의 절대 밴드는 공격력 모델 교체로 이동하므로, **단조 증가 + 보스 수용성 + 승급 시점**만 강제하고 절대 밴드는 Step 10에서 측정값으로 다시 채운다:

```kotlin
    val report = simulateProgressionRebalance(1L..accountCount.toLong())
    val arrivals = (1..10).map { report.chapter(it).p50Minutes }
    require(arrivals.zipWithNext().all { (earlier, later) -> later > earlier }) {
        "chapter arrival must increase monotonically\n${report.toCsv()}"
    }
    require(report.bosses.all { it.referenceSuccessRate >= 0.90 && it.deficientSuccessRate < it.referenceSuccessRate }) {
        "boss acceptance gate failed\n${report.toCsv()}"
    }
    require(
        report.promotion("NORMAL_TO_RARE").p50StageIndex <= 15 &&
            report.promotion("RARE_TO_EPIC").p50StageIndex <= 35,
    ) { "promotion gate failed\n${report.toCsv()}" }
```

`ProgressionRebalanceReport` 의 `arrivals` 생성 범위도 10챕터로 넓힌다 — `ProgressionRebalance.kt:492` 의 `(1..4).map { chapter ->` 를 교체:

```kotlin
    val arrivals = (1..10).map { chapter ->
```

- [ ] **Step 9: 테스트 통과 확인**

Run: `./gradlew :apps:balance-lab:test`
Expected: PASS

- [ ] **Step 10: 커밋**

```bash
git add apps/balance-lab/src
git commit -m "feat(balance-lab): generate chapters 5-10 with HP-linked normal attack"
```

---

### Task 6: 콘텐츠 생성 및 검증

**Files:**
- Modify: `packages/game-content/versions/progression-rebalance-v1/chapters/chapters.json`
- Modify: `packages/game-content/versions/progression-rebalance-v1/stages/stages.json`
- Generated: `progression-rebalance-v1/{progression,equipment,drops}/*.json`, `manifest.json`
- Generated: `docs/70-plans/mvp-release/verification/progression-rebalance-v1.csv`

**Interfaces:**
- Consumes: Task 1-5 전부. `Main.kt` 의 `updateStageValues` 는 **JSON 객체 수와 생성 행 수가 정확히 같아야** 하므로, 생성기 실행 전에 스켈레톤 60행을 먼저 넣어야 한다.
- Produces: 라이브 버전의 100스테이지 콘텐츠.

- [ ] **Step 1: 챕터 5-10 스켈레톤 생성 스크립트 작성**

`C:\Users\SSAFY\AppData\Local\Temp\claude\C--Users-SSAFY-Desktop-tpjt-S15P21B107\79c4c427-cbb5-4562-80b7-0dc622aa8510\scratchpad\seed-chapters.mjs` 를 생성:

```js
import { readFile, writeFile } from "node:fs/promises";

const root = "packages/game-content/versions/progression-rebalance-v1";
const chaptersPath = `${root}/chapters/chapters.json`;
const stagesPath = `${root}/stages/stages.json`;

const chapters = JSON.parse(await readFile(chaptersPath, "utf8"));
const stages = JSON.parse(await readFile(stagesPath, "utf8"));

const pad = (value) => String(value).padStart(2, "0");
const bossTypeFor = (chapter, number) => {
  const cycle = ((chapter - 1) % 3) + 1;
  const key = `${cycle}-${number}`;
  if (["1-4", "2-7", "3-10"].includes(key)) return "ATTACK_CHECK";
  if (["1-7", "2-10", "3-4"].includes(key)) return "HP_CHECK";
  if (["1-10", "2-4", "3-7"].includes(key)) return "PENETRATION_CHECK";
  return "STANDARD";
};

for (let chapter = 5; chapter <= 10; chapter += 1) {
  if (chapters.chapters.some((row) => row.id === `chapter.${pad(chapter)}`)) continue;
  chapters.chapters.push({
    id: `chapter.${pad(chapter)}`,
    stageIds: Array.from({ length: 10 }, (_, index) => `stage.${pad(chapter)}-${pad(index + 1)}`),
  });
  for (let number = 1; number <= 10; number += 1) {
    const type = bossTypeFor(chapter, number);
    stages.stages.push({
      id: `stage.${pad(chapter)}-${pad(number)}`,
      chapter,
      number,
      enemyLevel: 1,
      referenceQ: 0,
      referenceSkill: "N1",
      normalHp: 1,
      normalAttack: 1,
      bossType: type,
      bossHp: 1,
      bossAttack: 0,
      enemyDefense: 0,
      pattern: type === "ATTACK_CHECK" ? "10초 제한" : type === "HP_CHECK" ? "3초 강타 1" : "—",
    });
  }
}

await writeFile(chaptersPath, `${JSON.stringify(chapters, null, 2)}\n`);
await writeFile(stagesPath, `${JSON.stringify(stages, null, 2)}\n`);
console.log(`chapters=${chapters.chapters.length} stages=${stages.stages.length}`);
```

- [ ] **Step 2: 스켈레톤 주입**

Run: `node "C:/Users/SSAFY/AppData/Local/Temp/claude/C--Users-SSAFY-Desktop-tpjt-S15P21B107/79c4c427-cbb5-4562-80b7-0dc622aa8510/scratchpad/seed-chapters.mjs"`
Expected: `chapters=10 stages=100`

- [ ] **Step 3: 소규모 시뮬레이션으로 게이트 확인**

Run: `./gradlew :apps:balance-lab:run --args="progression-rebalance --accounts=200"`
Expected: 표준출력에 챕터 1-10 arrival CSV. 실패 시 `NORMAL_ATTACK_HP_RATIO`(Task 5 Step 6) 와 `referenceQ` 끝점(Task 5 Step 3)을 조정해 재실행한다. 목표: 챕터별 P50 도달 시간이 단조 증가하고 챕터 5-10의 챕터당 증가폭이 챕터 4 수준.

- [ ] **Step 4: 수치 확정 write**

Run: `./gradlew :apps:balance-lab:run --args="progression-rebalance --accounts=10000 --write"`
Expected: `progression-rebalance accounts=10000 wrote=100 stages ...` 출력, `stages.json` 의 100행 수치가 채워지고 검증 CSV가 갱신됨.

- [ ] **Step 5: TS 생성기 진입점 추가**

`buildProgressionRebalance.ts` 는 `writeProgressionPackages(root)` 를 export 하지만 CLI 진입점이 없다. `tools/content-validator/src/generate.ts` 를 생성:

```ts
import { resolve } from "node:path";
import { writeProgressionPackages } from "./buildProgressionRebalance.js";

await writeProgressionPackages(resolve(import.meta.dirname, "../../../packages/game-content"));
console.log("wrote progression-rebalance packages");
```

`tools/content-validator/package.json` 의 `scripts` 에 항목을 추가(기존 항목은 유지):

```json
  "scripts": { "build": "tsc", "typecheck": "tsc --noEmit", "test": "vitest run", "validate": "tsx src/validate.ts", "generate": "tsx src/generate.ts" },
```

- [ ] **Step 6: 진행·장비·드롭 재생성**

Run: `corepack pnpm@10.33.2 --filter @hanjjak/content-validator generate`
Expected: `wrote progression-rebalance packages`. `progression/progression.json` 이 100행 + `levelRange.max: 200`, `drops/material-drops.json` 이 챕터 1-10, 매니페스트 갱신.

- [ ] **Step 7: 전체 콘텐츠 검증**

Run: `corepack pnpm@10.33.2 --filter @hanjjak/content-validator validate`
Expected: exit 0

Run: `git status --short packages/game-content`
Expected: `progression-rebalance-v1` 하위의 `stages.json`, `chapters.json`, `progression.json`, `material-drops.json`, `manifest.json` 만 변경 (`versions/v1` 디렉터리는 무변경)

- [ ] **Step 8: 서버 전체 테스트**

Run: `./gradlew test`
Expected: PASS

- [ ] **Step 9: 커밋**

```bash
git add packages/game-content tools/content-validator docs/70-plans/mvp-release/verification
git commit -m "feat(content): add chapters 5-10 stage and progression content"
```

---

### Task 7: 문서 동기화

**Files:**
- Modify: `docs/30-domain/world/ssot.md:29,35,49-54`
- Modify: `docs/80-decisions/README.md` (결정 로그 행 추가)

**Interfaces:**
- Consumes: Task 6의 확정 수치.
- Produces: 없음 (문서만).

- [ ] **Step 1: 스테이지 SSOT 갱신**

`docs/30-domain/world/ssot.md:29` 의 "5~10장 적 수치·80레벨 이후 시간곡선·4-10 신규 보상은 후속 설계 전까지 확정하지 않는다." 를 교체:

```markdown
- 5~10장 적 수치는 2026-09-14 설계로 확정했다. `apps/balance-lab` 생성기가 기준 레벨 `2i−1`·기준 Q 램프에서 계산하며, 일반 공격은 기준 빌드 최대 HP의 고정 비율이다. 플레이어 레벨 상한은 200이다. 4-10 신규 보상은 여전히 후속 범위다.
```

35번 줄의 "MVP 범위는 ... 챕터 1–4, 총 40스테이지다." 를 교체:

```markdown
장기 설계는 챕터당 10개 스테이지로 구성한다. 구현 범위는 챕터 1–10, 총 100스테이지다(2026-09-14 확정). 기존 배포 버전의 동결 콘텐츠 `v1` 은 챕터 1–4·40스테이지로 유지한다.
```

49-54번 줄의 성장벽 표에 챕터 5-10 행을 추가한다(주기 3):

```markdown
| 5 | 방어 관통 | 공격력 | 최대 HP |
| 6 | 최대 HP | 방어 관통 | 공격력 |
| 7 | 공격력 | 최대 HP | 방어 관통 |
| 8 | 방어 관통 | 공격력 | 최대 HP |
| 9 | 최대 HP | 방어 관통 | 공격력 |
| 10 | 공격력 | 최대 HP | 방어 관통 |
```

- [ ] **Step 2: 결정 로그 추가**

`docs/80-decisions/README.md` 의 결정 표 마지막에 행을 추가:

```markdown
| 챕터 5-10 확장 | 2026-09-14 사용자 결정 | confirmed | [설계](../superpowers/specs/2026-09-14-chapter-5-10-content-balance-design.md)의 100스테이지·레벨 200·라이브 생성기 공식 연장. 일반 몬스터 공격은 기준 빌드 최대 HP 비례로 교체하고 보스:일반 = 2.0으로 통일한다. 레거시 `v1` 콘텐츠는 40스테이지로 동결 유지한다. |
```

- [ ] **Step 3: 커밋**

```bash
git add docs/30-domain/world/ssot.md docs/80-decisions/README.md
git commit -m "docs: record chapter 5-10 expansion as confirmed scope"
```

---

## 검증 요약

| 검증 | 명령 | 기대 |
|---|---|---|
| 스테이지 도메인 | `./gradlew :modules:stage:test` | PASS |
| 진행 도메인 | `./gradlew :modules:progression:test` | PASS |
| TS 생성기·밸리데이터 | `corepack pnpm@10.33.2 --filter @hanjjak/content-validator test` | PASS |
| 콘텐츠 무결성 | `corepack pnpm@10.33.2 --filter @hanjjak/content-validator validate` | exit 0 |
| 밸런스 시뮬레이션 | `./gradlew :apps:balance-lab:run --args="progression-rebalance --accounts=10000"` | 게이트 통과 |
| 서버 전체 | `./gradlew test` | PASS |
