# Equipment Stat Growth Guarantees Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking; checked steps record the shipped implementation and verification evidence.

**Goal:** Make every equipment enhancement increase its primary integer stat by at least one while making attack/HP gains at the same enhancement step strictly larger in each higher grade.

**Architecture:** `EquipmentRules` becomes the single source for integer equipment contributions. It preserves existing grade-start values, derives per-step deltas from the current exponential curve, applies the approved minimum and cross-grade guarantees, and exposes the result to equipment previews, character stats, combat snapshots, and balance simulations. Stored equipment state and costs remain unchanged; only derived stats change.

**Tech Stack:** Kotlin 2.3/JVM 21, Spring Boot 3.5, Gradle, Kotlin Test/JUnit 5, React 19, TypeScript 7, Vitest, Markdown SSOT documentation

## Global Constraints

- Use `docs/superpowers/specs/2026-09-11-equipment-stat-growth-guarantees-design.md` as the approved behavior contract.
- Keep `Q = gradeIndex × 39 + enhancementLevel`, all costs, materials, grade names, level limits, and stored `(slot, grade, enhancementLevel)` unchanged.
- For every slot and all 29 enhancement transitions per grade, the primary integer contribution must increase by at least 1.
- For `WEAPON`, `GLOVES`, `ARMOR`, and `HELMET`, the same `level→level+1` delta must satisfy `RARE > NORMAL`, `EPIC > RARE`, and `LEGENDARY > EPIC`.
- Preserve each grade's current `+1` start value and require every promoted `+1` value to exceed the prior grade's `+30` value.
- Keep cape and shoes on the current `round(3.6 × Q)` and `round(2.4 × Q)` curves; do not add cross-grade delta scaling to penetration.
- `EquipmentRules` must be the only source of derived integer equipment contributions; clients must not hide or repair `+0` locally.
- Do not add a DB migration. Existing accounts adopt the new derived values from their stored grade and level.
- Do not rewrite stored idempotent command result JSON; historical replay remains historical, while new reads and commands use the new rule.
- Do not adjust enemy content as part of this change. Report balance regressions separately with evidence.

---

### Task 1: Canonical Integer Equipment Contribution Curve

**Files:**
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/Equipment.kt:97-164`
- Modify: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/domain/EquipmentRulesTest.kt:87-108`

**Interfaces:**
- Consumes: existing `EquipmentSlotState`, `EquipmentSlot`, `EquipmentGrade`, `EquipmentRules.q(state)`.
- Produces: `EquipmentRules.statSummary(slot: EquipmentSlot, state: EquipmentSlotState?): EquipmentStatSummary` as the canonical runtime stat API and `EquipmentRules.referenceStatSummary(slot: EquipmentSlot, q: Int): EquipmentStatSummary` for abstract balance builds whose reference Q may be unattainable or exceed the current equipment cap; the old public `statSummary(slot, q)` API is removed after callers migrate.
- Invariant: a `null` state retains the existing Q=0 baseline contribution used for unowned slots.

- [x] **Step 1: Add failing exhaustive domain tests**

Add these helpers and tests inside `EquipmentRulesTest`:

```kotlin
private val scaledSlots = listOf(
    EquipmentSlot.WEAPON,
    EquipmentSlot.GLOVES,
    EquipmentSlot.ARMOR,
    EquipmentSlot.HELMET,
)

private fun primary(summary: EquipmentStatSummary): Int =
    summary.attack.takeIf { it != 0 }
        ?: summary.maxHp.takeIf { it != 0 }
        ?: summary.penetration

@Test
fun `every equipment enhancement increases its primary integer stat`() {
    EquipmentSlot.entries.forEach { slot ->
        EquipmentGrade.entries.forEach { grade ->
            (1 until EquipmentRules.MAX_ENHANCEMENT_LEVEL).forEach { level ->
                val current = EquipmentRules.statSummary(slot, state(grade, level, slot))
                val next = EquipmentRules.statSummary(slot, state(grade, level + 1, slot))
                assertTrue(primary(next) - primary(current) >= 1, "$slot $grade +$level")
            }
        }
    }
}

@Test
fun `same enhancement step gains more attack or hp in every higher grade`() {
    scaledSlots.forEach { slot ->
        (1 until EquipmentRules.MAX_ENHANCEMENT_LEVEL).forEach { level ->
            val deltas = EquipmentGrade.entries.map { grade ->
                primary(EquipmentRules.statSummary(slot, state(grade, level + 1, slot))) -
                    primary(EquipmentRules.statSummary(slot, state(grade, level, slot)))
            }
            assertTrue(deltas.zipWithNext().all { (lower, higher) -> higher > lower }, "$slot +$level: $deltas")
        }
    }
}

@Test
fun `promotion one remains above previous grade thirty`() {
    scaledSlots.forEach { slot ->
        EquipmentGrade.entries.zipWithNext().forEach { (grade, nextGrade) ->
            val before = primary(EquipmentRules.statSummary(slot, state(grade, 30, slot)))
            val after = primary(EquipmentRules.statSummary(slot, state(nextGrade, 1, slot)))
            assertTrue(after > before, "$slot $grade -> $nextGrade")
        }
    }
}

@Test
fun `normal glove zero-gain transitions now gain exactly one`() {
    listOf(1, 3, 6, 10, 15).forEach { level ->
        val current = primary(EquipmentRules.statSummary(EquipmentSlot.GLOVES, state(EquipmentGrade.NORMAL, level, EquipmentSlot.GLOVES)))
        val next = primary(EquipmentRules.statSummary(EquipmentSlot.GLOVES, state(EquipmentGrade.NORMAL, level + 1, EquipmentSlot.GLOVES)))
        assertEquals(1, next - current, "GLOVES NORMAL +$level")
    }
}
```

Add `import kotlin.test.assertTrue`. Existing Q and cost tests remain untouched.

- [x] **Step 2: Run the exhaustive tests and verify the current curve fails**

Run:

```bash
./gradlew.bat :modules:equipment:test --tests com.hanjjak.equipment.domain.EquipmentRulesTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: FAIL at compile because the state-based overload is absent, or after a temporary overload signature is present, FAIL on normal glove `+1→+2` and the same-step grade guarantee.

If the repository's known Windows test-class output issue produces `ClassNotFoundException`, first inspect `modules/equipment/build/test-results/test/TEST-Gradle#20Test#20Executor#201.xml`; do not treat that infrastructure failure as a behavioral failure. Retry from the repository's verified ASCII drive mapping with the same flags, as documented in G-task evidence.

- [x] **Step 3: Implement the canonical curve in `EquipmentRules`**

Replace the Q-only scaled-stat implementation with state-aware helpers. Preserve the existing Q=0 baseline for `state == null`. Keep abstract balance Q handling in the same rule object so simulation code does not duplicate formulas:

```kotlin
fun statSummary(slot: EquipmentSlot, state: EquipmentSlotState?): EquipmentStatSummary =
    statSummary(slot, state?.grade, state?.enhancementLevel ?: 0)

fun referenceStatSummary(slot: EquipmentSlot, q: Int): EquipmentStatSummary {
    require(q >= 0) { "INVALID_EQUIPMENT_Q" }
    val exactState = gradeAndLevel(q)
    return if (exactState == null) rawStatSummary(slot, q) else statSummary(slot, exactState.first, exactState.second)
}

private fun statSummary(slot: EquipmentSlot, grade: EquipmentGrade?, level: Int): EquipmentStatSummary {
    val q = grade?.let { it.index * 39 + level } ?: 0
    return when (slot) {
        EquipmentSlot.WEAPON -> EquipmentStatSummary(attack = scaledContribution(36, grade, level), maxHp = 0, penetration = 0)
        EquipmentSlot.GLOVES -> EquipmentStatSummary(attack = scaledContribution(24, grade, level), maxHp = 0, penetration = 0)
        EquipmentSlot.ARMOR -> EquipmentStatSummary(attack = 0, maxHp = scaledContribution(360, grade, level), penetration = 0)
        EquipmentSlot.HELMET -> EquipmentStatSummary(attack = 0, maxHp = scaledContribution(240, grade, level), penetration = 0)
        EquipmentSlot.CAPE -> EquipmentStatSummary(attack = 0, maxHp = 0, penetration = (3.6 * q).roundToInt())
        EquipmentSlot.SHOES -> EquipmentStatSummary(attack = 0, maxHp = 0, penetration = (2.4 * q).roundToInt())
    }
}

private fun scaledContribution(base: Int, grade: EquipmentGrade?, level: Int): Int {
    if (grade == null) return scaled(base, 0)
    val startQ = grade.index * 39 + 1
    return scaled(base, startQ) + (1 until level).sumOf { step -> guaranteedDelta(base, grade, step) }
}

private fun guaranteedDelta(base: Int, grade: EquipmentGrade, level: Int): Int {
    val currentQ = grade.index * 39 + level
    val rawDelta = scaled(base, currentQ + 1) - scaled(base, currentQ)
    val lowerGradeDelta = grade.takeIf { it.index > 0 }
        ?.let { EquipmentGrade.entries[it.index - 1] }
        ?.let { guaranteedDelta(base, it, level) }
    return maxOf(1, rawDelta, lowerGradeDelta?.plus(1) ?: 1)
}

private fun gradeAndLevel(q: Int): Pair<EquipmentGrade, Int>? = when (q) {
    in 1..30 -> EquipmentGrade.NORMAL to q
    in 40..69 -> EquipmentGrade.RARE to q - 39
    in 79..108 -> EquipmentGrade.EPIC to q - 78
    in 118..147 -> EquipmentGrade.LEGENDARY to q - 117
    else -> null
}

private fun rawStatSummary(slot: EquipmentSlot, q: Int): EquipmentStatSummary = when (slot) {
    EquipmentSlot.WEAPON -> EquipmentStatSummary(attack = scaled(36, q), maxHp = 0, penetration = 0)
    EquipmentSlot.GLOVES -> EquipmentStatSummary(attack = scaled(24, q), maxHp = 0, penetration = 0)
    EquipmentSlot.ARMOR -> EquipmentStatSummary(attack = 0, maxHp = scaled(360, q), penetration = 0)
    EquipmentSlot.HELMET -> EquipmentStatSummary(attack = 0, maxHp = scaled(240, q), penetration = 0)
    EquipmentSlot.CAPE -> EquipmentStatSummary(attack = 0, maxHp = 0, penetration = (3.6 * q).roundToInt())
    EquipmentSlot.SHOES -> EquipmentStatSummary(attack = 0, maxHp = 0, penetration = (2.4 * q).roundToInt())
}
```

Retain `private fun scaled(base: Int, q: Int)` and `fun q(state)` unchanged. `referenceStatSummary` uses the guaranteed runtime curve for attainable Q values and the prior raw formula only for balance-only gap/future Q values such as 70 or 240. Do not add tables or persisted stat fields.

- [x] **Step 4: Run the domain tests and verify all invariants pass**

Run the command from Step 2.

Expected: PASS. The new exhaustive tests cover `6 slots × 4 grades × 29 transitions`, all same-step cross-grade comparisons for four scaled slots, and all promotion boundaries.

- [x] **Step 5: Commit the canonical curve**

```bash
git add modules/equipment/src/main/kotlin/com/hanjjak/equipment/domain/Equipment.kt modules/equipment/src/test/kotlin/com/hanjjak/equipment/domain/EquipmentRulesTest.kt
git commit -m "fix(equipment): guarantee integer stat growth"
```

---

### Task 2: Equipment API Preview Consistency

**Files:**
- Modify: `modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentService.kt:85-125`
- Modify: `modules/equipment/src/test/kotlin/com/hanjjak/equipment/application/EquipmentServiceTest.kt:99-205`

**Interfaces:**
- Consumes: `EquipmentRules.statSummary(slot, state)` from Task 1.
- Produces: equipment `current.stats`, `enhance.result.stats`, and `enhance.statIncrease` from the canonical integer curve.
- Preserves: command cost, material consumption, level transition, events, and id organizer/replay behavior.

- [x] **Step 1: Change the service test to require positive glove preview**

Replace the obsolete zero expectation in `state provides server-authored locked enhancement promotion and terminal actions`:

```kotlin
assertEquals(EquipmentStatSummary(1, 0, 0), enhancement.enhance.statIncrease)
assertEquals(
    enhancement.enhance.result.stats.attack - enhancement.current.stats.attack,
    enhancement.enhance.statIncrease.attack,
)
```

Add a focused command test:

```kotlin
@Test
fun `glove enhancement preview equals the committed stat increase`() {
    repository.rice = 10_000
    repository.seedMaterials(10_000)
    repository.states[EquipmentSlot.GLOVES] = slot(EquipmentGrade.NORMAL, 1, EquipmentSlot.GLOVES)

    val before = service.state(accountId).slots.first { it.slot == EquipmentSlot.GLOVES }
    val result = service.enhance(accountId, key, EquipmentSlot.GLOVES)

    assertEquals(1, before.enhance?.statIncrease?.attack)
    assertEquals(
        before.enhance?.statIncrease?.attack,
        result.slot.current.stats.attack - before.current.stats.attack,
    )
    assertEquals(2, result.slot.current.enhancementLevel)
}
```

- [x] **Step 2: Run the service test and verify it fails**

Run:

```bash
./gradlew.bat :modules:equipment:test --tests com.hanjjak.equipment.application.EquipmentServiceTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: FAIL until `EquipmentService.growth` passes the full state to the Task 1 API.

- [x] **Step 3: Migrate `EquipmentService.growth` to the state API**

Replace only the stat calculation inside `growth`:

```kotlin
private fun growth(slot: EquipmentSlot, state: EquipmentSlotState?): EquipmentGrowthSummary {
    val q = state?.let(EquipmentRules::q) ?: 0
    return EquipmentGrowthSummary(
        state?.grade,
        state?.grade?.label,
        state?.enhancementLevel ?: 0,
        q,
        EquipmentRules.statSummary(slot, state),
    )
}
```

Keep `statDifference` unchanged. It now subtracts two canonical summaries.

- [x] **Step 4: Run equipment module tests**

Run:

```bash
./gradlew.bat :modules:equipment:test -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: PASS with the glove preview and committed result both reporting `+1`.

- [x] **Step 5: Commit API preview consistency**

```bash
git add modules/equipment/src/main/kotlin/com/hanjjak/equipment/application/EquipmentService.kt modules/equipment/src/test/kotlin/com/hanjjak/equipment/application/EquipmentServiceTest.kt
git commit -m "fix(equipment): align previews with guaranteed gains"
```

---

### Task 3: Character and Combat Stat Consistency

**Files:**
- Modify: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculator.kt:20-58`
- Modify: `apps/game-api/src/test/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculatorTest.kt:10-30`

**Interfaces:**
- Consumes: `EquipmentRules.statSummary(slot, state)` from Task 1.
- Produces: integer `StatSource.value` contributions for equipment; `CharacterCalculation.fighter` continues to feed character UI, combat sessions, and combat power.
- Preserves: base character stats, level growth, gems, cosmetics, percentage order, and final half-up rounding.

- [x] **Step 1: Add failing character calculation tests**

Add a helper inside `CharacterStatsCalculatorTest`:

```kotlin
private fun attackWith(glovesLevel: Int): Int = CharacterStatsCalculator.calculate(
    1,
    mapOf(EquipmentSlot.GLOVES to EquipmentSlotState(account, EquipmentSlot.GLOVES, EquipmentGrade.NORMAL, glovesLevel)),
    emptyList(),
    SkillProfile(),
    emptyList(),
).fighter.attack
```

Add these tests:

```kotlin
@Test
fun `normal glove one to two increases actual attack by previewed one`() {
    assertEquals(1, attackWith(2) - attackWith(1))
}

@Test
fun `character equipment source equals canonical integer contribution`() {
    val state = EquipmentSlotState(account, EquipmentSlot.GLOVES, EquipmentGrade.RARE, 4)
    val result = CharacterStatsCalculator.calculate(
        1,
        mapOf(EquipmentSlot.GLOVES to state),
        emptyList(),
        SkillProfile(),
        emptyList(),
    )
    val source = result.stats.first { it.statId == "attack" }.sources.first { it.sourceId == "equipment.GLOVES" }

    assertEquals(EquipmentRules.statSummary(EquipmentSlot.GLOVES, state).attack.toDouble(), source.value)
}
```

Update `base uses level and rounds permanent equipment contribution once` expected values only from actual canonical outputs; do not preserve a stale expected number.

- [x] **Step 2: Run calculator tests and verify the current duplicate formula fails**

Run:

```bash
./gradlew.bat :apps:game-api:test --tests com.hanjjak.gameapi.character.CharacterStatsCalculatorTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: FAIL because `CharacterStatsCalculator` still derives floating contributions independently and normal gloves `+1→+2` may leave final attack unchanged.

- [x] **Step 3: Replace duplicate equipment formulas with canonical summaries**

Replace `equipmentSource(slot, factor, exponential)` with:

```kotlin
fun equipmentSource(slot: EquipmentSlot): StatSource {
    val item = equipment[slot]
    val summary = EquipmentRules.statSummary(slot, item)
    val amount = when (slot) {
        EquipmentSlot.WEAPON, EquipmentSlot.GLOVES -> summary.attack
        EquipmentSlot.ARMOR, EquipmentSlot.HELMET -> summary.maxHp
        EquipmentSlot.CAPE, EquipmentSlot.SHOES -> summary.penetration
    }
    return source(
        "equipment.${slot.name}",
        "${slot.label} ${item?.let { "${it.grade.label} +${it.enhancementLevel}" } ?: "기본값"}",
        "EQUIPMENT",
        amount,
    )
}
```

Update the three main-stat calls:

```kotlin
mainStat("attack", "공격력", 40, 2, listOf(equipmentSource(EquipmentSlot.WEAPON), equipmentSource(EquipmentSlot.GLOVES)), GemOption.FLAT_ATTACK, "attackPercent")
mainStat("maxHp", "최대 HP", 400, 20, listOf(equipmentSource(EquipmentSlot.ARMOR), equipmentSource(EquipmentSlot.HELMET)), GemOption.FLAT_HP, "maxHpPercent")
mainStat("penetration", "방어 관통", 20, 0, listOf(equipmentSource(EquipmentSlot.CAPE), equipmentSource(EquipmentSlot.SHOES)), GemOption.FLAT_PENETRATION, "defensePenetrationPercent")
```

Remove unused `kotlin.math.pow`. Retain `rounded` for total calculations.

- [x] **Step 4: Run calculator and equipment tests**

Run:

```bash
./gradlew.bat :modules:equipment:test :apps:game-api:test --tests com.hanjjak.equipment.domain.EquipmentRulesTest --tests com.hanjjak.equipment.application.EquipmentServiceTest --tests com.hanjjak.gameapi.character.CharacterStatsCalculatorTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: PASS. The normal glove enhancement increases the real fighter attack by exactly the API previewed value when no percentage modifiers are present.

- [x] **Step 5: Commit shared runtime calculation**

```bash
git add apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculator.kt apps/game-api/src/test/kotlin/com/hanjjak/gameapi/character/CharacterStatsCalculatorTest.kt
git commit -m "fix(combat): use canonical equipment contributions"
```

---

### Task 4: Balance Lab Uses Production Equipment Rules

**Files:**
- Modify: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/Main.kt:44-59`
- Modify: `apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/DungeonBalance.kt:92-100`
- Modify: `apps/balance-lab/src/test/kotlin/com/hanjjak/balancelab/EconomySimulationTest.kt`
- Generate: `build/reports/balance/enemy-v1-working.csv`
- Generate: `build/reports/pre-kafka-j11-validation.json`
- Generate: `build/reports/balance/gem-dungeon-candidate.csv`

**Interfaces:**
- Consumes: `EquipmentRules.referenceStatSummary(slot, q)` and existing abstract stage/dungeon `referenceQ` values, including gap and future Q values that do not map to a current runtime grade/level.
- Produces: balance-lab `FighterStats` built through the same `EquipmentRules` boundary as production; attainable Q values receive the guaranteed curve and abstract Q values retain the prior raw curve.

- [x] **Step 1: Add failing reference-Q tests**

Add to `EconomySimulationTest`:

```kotlin
@Test
fun `reference stats use guaranteed values for attainable equipment q`() {
    val glove = EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, 2)
    val runtime = EquipmentRules.statSummary(
        EquipmentSlot.GLOVES,
        EquipmentSlotState(UUID(0, 1), EquipmentSlot.GLOVES, EquipmentGrade.NORMAL, 2),
    )

    assertEquals(runtime, glove)
    assertEquals(26, glove.attack)
}

@Test
fun `reference stats retain raw curve for abstract gap and future q`() {
    assertEquals(132, EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, 70).attack)
    assertEquals(8_374, EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, 240).attack)
    assertFailsWith<IllegalArgumentException> {
        EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, -1)
    }
}
```

Add imports for `EquipmentGrade`, `EquipmentRules`, `EquipmentSlot`, `EquipmentSlotState`, `UUID`, `assertEquals`, and `assertFailsWith`. These abstract-Q values fix the prior positive-number `roundToInt` behavior while attainable runtime Q values use the guaranteed curve.

- [x] **Step 2: Run balance-lab tests and verify the production boundary is not used yet**

Run:

```bash
./gradlew.bat :apps:balance-lab:test -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: the new boundary tests pass after Task 1, but existing `Main.kt` and `DungeonBalance.kt` still contain direct `1.05.pow(...)` equipment formulas. Use the source migration in Step 3 as the behavior change; do not add another Q-to-state mapper.

- [x] **Step 3: Implement production-rule reference stats**

In `Main.kt`, import `EquipmentRules` and `EquipmentSlot`. Replace the duplicate attack/HP formula in `referenceStats`:

```kotlin
private fun referenceStats(level: Int, attackQ: Int, hpQ: Int, penetrationQ: Int): FighterStats = FighterStats(
    attack = 40 + 2 * (level - 1) +
        EquipmentRules.referenceStatSummary(EquipmentSlot.WEAPON, attackQ).attack +
        EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, attackQ).attack,
    maxHp = 400 + 20 * (level - 1) +
        EquipmentRules.referenceStatSummary(EquipmentSlot.ARMOR, hpQ).maxHp +
        EquipmentRules.referenceStatSummary(EquipmentSlot.HELMET, hpQ).maxHp,
    penetration = 20 +
        EquipmentRules.referenceStatSummary(EquipmentSlot.CAPE, penetrationQ).penetration +
        EquipmentRules.referenceStatSummary(EquipmentSlot.SHOES, penetrationQ).penetration,
)
```

Remove unused `kotlin.math.pow` from `Main.kt`.

In `DungeonBalance.kt`, import `EquipmentRules` and `EquipmentSlot`, then replace the duplicate equipment multiplier inside `snapshot`. Keep gem flat and percentage ordering unchanged:

```kotlin
val baseAttack = 40 + 2 * (build.level - 1) +
    EquipmentRules.referenceStatSummary(EquipmentSlot.WEAPON, build.q).attack +
    EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, build.q).attack +
    gems.flatAttack
val baseHp = 400 + 20 * (build.level - 1) +
    EquipmentRules.referenceStatSummary(EquipmentSlot.ARMOR, build.q).maxHp +
    EquipmentRules.referenceStatSummary(EquipmentSlot.HELMET, build.q).maxHp
val penetration = 20 +
    EquipmentRules.referenceStatSummary(EquipmentSlot.CAPE, build.q).penetration +
    EquipmentRules.referenceStatSummary(EquipmentSlot.SHOES, build.q).penetration
```

Use `baseHp + gems.flatHp` for `FighterStats.maxHp` and retain the existing attack-percent rounding. Remove unused `kotlin.math.pow`.

- [x] **Step 4: Run balance-lab tests and simulations**

Run:

```bash
./gradlew.bat :apps:balance-lab:test :apps:balance-lab:run :apps:balance-lab:dungeonBalance -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: PASS with three generated reports. Record whether any `enemy-v1-working.csv` success or deficient-success fields differ from `docs/70-plans/mvp-release/verification/enemy-v1-applied.csv`. Do not overwrite the applied CSV in this task.

- [x] **Step 5: Commit balance-lab production parity**

Generated `build/reports` files are evidence and may be ignored; commit only tracked sources unless repository status shows the reports are already tracked:

```bash
git add apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/Main.kt apps/balance-lab/src/main/kotlin/com/hanjjak/balancelab/DungeonBalance.kt apps/balance-lab/src/test/kotlin/com/hanjjak/balancelab/EconomySimulationTest.kt
git commit -m "test(balance): use production equipment stat curve"
```

---

### Task 5: Web Display Contract

**Files:**
- Modify: `apps/web/src/features/equipment/EquipmentScreen.test.tsx:12-47`
- Verify unchanged: `apps/web/src/features/equipment/EquipmentScreen.tsx:41-50,137-153`

**Interfaces:**
- Consumes: positive server-authored `EquipmentActionSummary.statIncrease`.
- Produces: visible `강화 시 +1` for the corrected normal glove transition without client-side fallback.
- Non-goal: no production UI code change unless the test proves the current component alters the server value.

- [x] **Step 1: Update the fixture and add the no-zero assertion**

Change the normal glove fixture from attack 25 to 26 at +2 and from `attack: 0` to `attack: 1`:

```tsx
{ slot: "GLOVES", slotName: "장갑", unlocked: true, current: growth("NORMAL", "노말", 1, 1, 25, 0, 0), unlock: null, enhance: action(30, [material("POTATO_M1", "감자 M1", 125, 125)], growth("NORMAL", "노말", 2, 2, 26, 0, 0), { attack: 1, maxHp: 0, penetration: 0 }, true, null), promote: null, growthComplete: false },
```

Add to the render test:

```tsx
expect(html).toContain("강화 시 +1")
expect(html).not.toContain("강화 시 +0")
```

- [x] **Step 2: Run the focused web test**

Run from repository root:

```bash
corepack pnpm --filter @hanjjak/web exec vitest run src/features/equipment/EquipmentScreen.test.tsx
```

Expected: PASS. This confirms the current UI renders the server value directly. Do not modify `EquipmentScreen.tsx` if this passes.

- [x] **Step 3: Run web typecheck**

```bash
corepack pnpm --filter @hanjjak/web typecheck
```

Expected: PASS.

- [x] **Step 4: Commit the web contract test**

```bash
git add apps/web/src/features/equipment/EquipmentScreen.test.tsx
git commit -m "test(web): reject zero equipment gain labels"
```

---

### Task 6: SSOT, Decision, and Delivery Evidence Synchronization

**Files:**
- Modify: `docs/30-domain/items/equipment/ssot.md:85-102`
- Modify: `docs/30-domain/combat/ssot.md:20-33`
- Modify: `docs/80-decisions/README.md:35-40`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-08-enhancement-slots-stat-table.md:23-47`
- Modify: `docs/wiki/06-delivery/tasks/G-equipment-crafting/_index.md:5-13`
- Modify: `docs/wiki/06-delivery/tasks/_index.md:68-74`

**Interfaces:**
- Consumes: verified code/test/report paths and exact command results from Tasks 1–5.
- Produces: current policy in the responsible SSOTs and implementation state/evidence in G-08 only; indexes duplicate status, not rules.
- Preserves: feature routes and indexes remain routing-only; no rule text is copied into `_index.md`.

- [x] **Step 1: Update the equipment SSOT**

Expand `## 유효 Q와 능력치` with the approved rules and link the design:

```markdown
공격력·최대 HP 장비는 등급 1강의 기존 `M(Q)=1.05^(Q/2)` 시작값을 유지한다. 등급 안의 각 강화 증가량은 기존 곡선의 정수 차이를 기준으로 하되 최소 1을 보장하고, 같은 `level→level+1`에서는 희귀가 노말보다, 영웅이 희귀보다, 전설이 영웅보다 최소 1 더 크게 오른다. 등급 1강 값에서 이 보장 증가량을 누적해 현재 능력치 기여값을 계산한다. 승급 후 1강 기여값은 직전 등급 30강보다 커야 한다.

망토와 신발은 각각 `round(3.6Q)`, `round(2.4Q)`를 유지하며 모든 강화에서 최소 1 증가해야 한다. 서버의 `EquipmentRules` 정수 기여값을 장비 조회·캐릭터 능력치·전투 입력이 공통 사용하고 클라이언트는 증가량을 재계산하거나 0을 숨기지 않는다.
```

Add `[능력치 성장 보장 설계](../../../superpowers/specs/2026-09-11-equipment-stat-growth-guarantees-design.md)` to related documents.

- [x] **Step 2: Update the combat SSOT formula boundary**

Replace only the equipment term definition around the current formula. Define `E_slot(grade, level)` as the integer contribution owned by the equipment SSOT, then express:

```text
P_ATK = 40 + 2(L-1) + E_weapon + E_gloves
P_HP = 400 + 20(L-1) + E_armor + E_helmet
P_PEN = 20 + E_cape + E_shoes
```

State that gems and percentage effects retain their current application order after these integer base contributions. Do not duplicate the guaranteed-delta algorithm in combat SSOT; link equipment SSOT.

- [x] **Step 3: Record the confirmed user decision**

Add a decision row adjacent to `영구 장비 성장·비용`:

```markdown
| 장비 능력치 단계별 성장 보장 | 2026-09-11 사용자 명세 승인 | confirmed | [장비 SSOT](../30-domain/items/equipment/ssot.md)의 모든 강화 주 능력치 최소 +1, 공격력·최대 HP 동일 단계의 `희귀 > 노말`, `영웅 > 희귀`, `전설 > 영웅`, 승급 1강의 직전 등급 30강 초과와 서버 단일 정수 계산을 사용한다. [승인 설계](../superpowers/specs/2026-09-11-equipment-stat-growth-guarantees-design.md)에 전파했다. |
```

- [x] **Step 4: Update G-08 evidence and status**

Update the completion criterion to include no-zero enhancement, same-step higher-grade growth, promotion monotonicity, and preview/runtime parity. Add dated evidence listing:

- `Equipment.kt`, `EquipmentService.kt`, `CharacterStatsCalculator.kt`
- exhaustive domain tests, service tests, character tests, focused web test
- balance-lab commands and generated report paths
- whether applied enemy acceptance changed

Set `verification_status` and the status table to `완료` only if all specified behavioral checks pass. Otherwise retain `부분 검증` and name the exact unverified command or balance regression. Mirror only that resulting G-08 status in both indexes.

- [x] **Step 5: Validate documentation structure and links**

Check every relative Markdown link affected by the six files using the repository's existing link-check mechanism if present. If none exists, resolve the changed relative paths directly against their parent directories. Then run:

```bash
git diff --check
```

Expected: no whitespace errors or missing changed-document links.

- [x] **Step 6: Commit synchronized documentation**

```bash
git add docs/30-domain/items/equipment/ssot.md docs/30-domain/combat/ssot.md docs/80-decisions/README.md docs/wiki/06-delivery/tasks/G-equipment-crafting/g-08-enhancement-slots-stat-table.md docs/wiki/06-delivery/tasks/G-equipment-crafting/_index.md docs/wiki/06-delivery/tasks/_index.md
git commit -m "docs(equipment): record guaranteed stat growth"
```

---

### Task 7: End-to-End Verification and Cleanup

**Files:**
- Verify: all files changed in Tasks 1–6
- Update if evidence differs: `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-08-enhancement-slots-stat-table.md`
- Update if status changes: `docs/wiki/06-delivery/tasks/G-equipment-crafting/_index.md`
- Update if status changes: `docs/wiki/06-delivery/tasks/_index.md`

**Interfaces:**
- Consumes: completed source, tests, documentation, and generated balance reports.
- Produces: final proof that the user-visible `+0` defect is gone and higher-grade gains satisfy the approved invariant.

- [x] **Step 1: Run the focused JVM verification**

```bash
./gradlew.bat :modules:equipment:test :apps:game-api:test --tests com.hanjjak.equipment.domain.EquipmentRulesTest --tests com.hanjjak.equipment.application.EquipmentServiceTest --tests com.hanjjak.gameapi.character.CharacterStatsCalculatorTest -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: PASS with no skipped tests. If the known Windows output-root `ClassNotFoundException` occurs, run the same command through the repository's verified ASCII drive mapping and record the exact successful path in G-08 evidence.

- [x] **Step 2: Run balance regression verification**

```bash
./gradlew.bat :apps:balance-lab:test :apps:balance-lab:run :apps:balance-lab:dungeonBalance -Pkotlin.incremental=false --rerun-tasks --no-daemon --no-configuration-cache --max-workers=1
```

Expected: PASS; generated reports exist. Compare `build/reports/balance/enemy-v1-working.csv` with `docs/70-plans/mvp-release/verification/enemy-v1-applied.csv`. Any changed acceptance row is reported, not silently copied.

- [x] **Step 3: Run focused web verification**

```bash
corepack pnpm --filter @hanjjak/web exec vitest run src/features/equipment/EquipmentScreen.test.tsx
corepack pnpm --filter @hanjjak/web typecheck
```

Expected: PASS; rendered HTML contains `강화 시 +1` and no `강화 시 +0`.

- [x] **Step 4: Run a direct behavior smoke check**

Use the production Kotlin rule through the smallest available executable test or API path to observe:

```text
GLOVES NORMAL +1 attack contribution = 25
GLOVES NORMAL +2 attack contribution = 26
preview statIncrease.attack = 1
character fighter.attack increase without percent modifiers = 1
```

If local PostgreSQL/Testcontainers is available, extend or execute the existing equipment HTTP flow to GET equipment state, enhance `GLOVES`, and GET character stats. If not available, the service-plus-calculator tests above are the required behavioral substitute; document that HTTP/PostgreSQL was not exercised.

- [x] **Step 5: Reconcile evidence status and clean scaffolding**

Remove no longer needed duplicate formula imports and any temporary diagnostics. Ensure G-08 evidence names only commands actually run and retains `부분 검증` if HTTP/PostgreSQL or balance acceptance remains unverified. Keep unrelated worktree changes untouched.

- [x] **Step 6: Run final hygiene checks**

```bash
git diff --check
git status --short
```

Expected: no whitespace errors; only intended equipment, character calculation, balance-lab, web test, and documentation paths are changed.

- [x] **Step 7: Commit final evidence corrections if needed**

```bash
git add docs/wiki/06-delivery/tasks/G-equipment-crafting/g-08-enhancement-slots-stat-table.md docs/wiki/06-delivery/tasks/G-equipment-crafting/_index.md docs/wiki/06-delivery/tasks/_index.md
git commit -m "docs(equipment): record stat growth verification"
```

Skip this commit when Task 6 evidence already exactly matches the final commands and status.
