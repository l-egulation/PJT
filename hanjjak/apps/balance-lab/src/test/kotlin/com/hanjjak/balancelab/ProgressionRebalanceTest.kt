package com.hanjjak.balancelab

import kotlin.test.Test
import kotlin.test.assertFailsWith
import kotlin.test.assertEquals
import kotlin.test.assertNotEquals
import kotlin.test.assertTrue

class ProgressionRebalanceTest {
    @Test
    fun `reference progression meets chapter arrival and boss gates`() {
        val report = simulateProgressionRebalance(1L..300L)

        val p50 = (1..10).map { report.chapter(it).p50Minutes }
        assertTrue(p50.zipWithNext().all { (earlier, later) -> later > earlier }, report.toCsv())
        assertTrue((1..10).all { report.chapter(it).p90Minutes >= report.chapter(it).p50Minutes }, report.toCsv())
        assertTrue(report.bosses.all { it.referenceSuccessRate >= 0.90 }, report.toCsv())
        assertTrue(report.bosses.all { it.deficientSuccessRate < it.referenceSuccessRate }, report.toCsv())
        assertTrue(report.promotion("NORMAL_TO_RARE").p50StageIndex <= 15)
        assertTrue(report.promotion("RARE_TO_EPIC").p50StageIndex <= 35)
    }

    @Test
    fun `generator covers one hundred stages with rotating growth walls`() {
        val rows = generatedStageRows()

        assertEquals(100, rows.size)
        assertEquals("stage.10-10", rows.last().id)
        assertEquals(199, rows.last().level)
        assertEquals("PENETRATION_CHECK", bossTypeFor(5, 4))
        assertEquals("ATTACK_CHECK", bossTypeFor(5, 7))
        assertEquals("HP_CHECK", bossTypeFor(5, 10))
        assertEquals("PENETRATION_CHECK", bossTypeFor(10, 10))
        assertEquals("STANDARD", bossTypeFor(10, 9))
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

    @Test
    fun `q endpoints and interpolation are monotone and cycle drops are exact`() {
        assertEquals(25, referenceQ(10))
        assertEquals(54, referenceQ(20))
        assertEquals(64, referenceQ(30))
        assertEquals(93, referenceQ(40))
        assertEquals(2 * 17 - 1, referenceLevel(17))
        assertTrue((1..39).map(::referenceQ).zipWithNext().all { it.first <= it.second })

        val report = simulateProgressionRebalance(1L..1L)
        assertEquals(50L, report.chapterOneM1PerFullCycle)
        assertEquals(0L, report.stageTenRepeatItems)
    }

    @Test
    fun `boss checks rotate by chapter and late bosses use fixed gem presets`() {
        assertEquals("ATTACK_CHECK", bossTypeFor(1, 4))
        assertEquals("PENETRATION_CHECK", bossTypeFor(2, 4))
        assertEquals("HP_CHECK", bossTypeFor(3, 4))
        assertEquals("ATTACK_CHECK", bossTypeFor(3, 10))
        assertEquals(listOf(2, 2, 2, 3, 3, 3), referenceGemLevels(30))
        assertEquals(List(6) { 4 }, referenceGemLevels(40))
    }

    @Test
    fun `cohort economy reports peer matched market and separate cohorts`() {
        val report = simulateProgressionRebalance(1L..300L)
        assertEquals(300, report.cohortCount)
        assertEquals(3, report.cohorts.size)
        assertTrue(report.cohorts.first { it.kind == CohortKind.MARKET_USE }.accountCount > 0)
        assertTrue(report.cohorts.first { it.kind == CohortKind.MARKET_USE }.filledOrders > 0)
        assertTrue(report.cohorts.first { it.kind == CohortKind.NO_MARKET }.accountCount > 0)
        assertEquals(0L, report.cohorts.first { it.kind == CohortKind.NO_MARKET }.requestedOrders)
        assertTrue(report.cohorts.first { it.kind == CohortKind.MARKET_LIQUIDITY_SHORTAGE }.accountCount > 0)
        assertTrue(report.cohorts.first { it.kind == CohortKind.MARKET_LIQUIDITY_SHORTAGE }.requestedOrders > 0)
        assertTrue(report.marketVolume > 0, report.toEconomyJson())
        assertTrue(report.marketSellerFee in 0..report.marketGrossAmount / 10, report.toEconomyJson())
        assertEquals(0L, report.marketSystemSellerQuantity)
        assertTrue(report.marketUnmatchedQuantity > 0, report.toEconomyJson())
    }

    @Test
    fun `applied progression experience changes arrival time`() {
        val content = loadProgressionSimulationContent()
        val slower = content.copy(
            progression = content.progression.map { reward ->
                reward.copy(normalExperience = (reward.normalExperience / 2).coerceAtLeast(1))
            },
        )

        val reference = simulateProgressionRebalance(1L..30L, content)
        val changed = simulateProgressionRebalance(1L..30L, slower)

        assertTrue(changed.chapter(1).p50Minutes > reference.chapter(1).p50Minutes)
    }

    @Test
    fun `first clear and material inputs affect economy transitions`() {
        val content = loadProgressionSimulationContent()
        val withoutFirstClear = content.copy(
            progression = content.progression.map { reward ->
                reward.copy(firstClearRice = 0, firstClearItems = emptyMap())
            },
        )
        val withoutDrops = content.copy(
            dropTable = com.hanjjak.inventory.domain.DropTable(
                com.hanjjak.inventory.domain.MaterialDropContent(
                    contentVersion = "sensitivity",
                    dropChanceBasisPoints = 0,
                    quantityPerSuccess = 10,
                    normalEnemyRolls = 1,
                    bossRolls = 0,
                    chapterGenerationWeights = mapOf(
                        1 to listOf(1, 0, 0, 0),
                        2 to listOf(5, 1, 0, 0),
                        3 to listOf(25, 5, 1, 0),
                        4 to listOf(125, 25, 5, 1),
                    ),
                ),
            ),
        )

        val reference = simulateProgressionRebalance(1L..30L, content)
        val firstClearChanged = simulateProgressionRebalance(1L..30L, withoutFirstClear)
        val withoutDropsReport = simulateProgressionRebalance(1L..30L, withoutDrops)

        assertNotEquals(firstClearChanged.riceSupply, reference.riceSupply, "reference=${reference.toEconomyJson()} changed=${firstClearChanged.toEconomyJson()}")
        assertTrue(withoutDropsReport.cohorts.all { it.p50ChapterFourMinutes >= reference.cohorts.first { cohort -> cohort.kind == it.kind }.p50ChapterFourMinutes })
    }

    @Test
    fun `loaded impossible stage cannot advance through arithmetic rewards`() {
        val content = loadProgressionSimulationContent()
        val impossible = content.copy(
            stages = content.stages.mapIndexed { index, stage ->
                if (index == 0) stage.copy(normalHp = Int.MAX_VALUE, normalAttack = Int.MAX_VALUE) else stage
            },
        )

        val failure = assertFailsWith<IllegalStateException> {
            simulateProgressionRebalance(1L..1L, impossible)
        }

        assertTrue(failure.message.orEmpty().contains("stage.01-01"))
    }

    private fun assertInRange(actual: Double, min: Double, max: Double) {
        assertTrue(actual in min..max, "expected $actual in [$min, $max]")
    }
}
