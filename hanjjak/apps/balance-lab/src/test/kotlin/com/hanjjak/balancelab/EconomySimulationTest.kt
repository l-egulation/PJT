package com.hanjjak.balancelab

import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentRules
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.equipment.domain.EquipmentSlotState
import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class EconomySimulationTest {
    @Test
    fun `seven day P50 economy simulation produces permanent equipment trade and rice indicators`() {
        val report = simulateP50Economy(testStages(), Instant.parse("2026-09-07T00:00:00Z"))

        assertFalse(report.criteria.orderSheetsIncluded)
        assertTrue(report.fiveDayLevelBandCheck.passes)
        assertEquals(1_440, report.sevenDay.effectiveCombatMinutes)
        assertTrue(report.sevenDay.endingLevel >= 55)
        assertTrue(report.sevenDay.endingStage.startsWith("stage.03-"))
        assertTrue(report.sevenDay.riceGenerated > 0)
        assertTrue(report.sevenDay.riceConsumedByEquipment > 0)
        assertTrue(report.sevenDay.ecosystemRiceRemaining > 0)
        assertEquals(6, report.sevenDay.unlockedSlotCount)
        assertTrue(report.sevenDay.enhancementCount > 0)
        assertTrue(report.sevenDay.tradedQuantity > 0)
        assertTrue(report.metrics.any { it.droppedQuantity > 0 && it.consumedQuantity > 0 })
        assertTrue(report.metrics.any { it.tradeCount > 0 && it.feeAmount == it.tradeAmount / 10 })
    }

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

    private fun testStages(): List<StageRow> = (1..4).flatMap { chapter ->
        (1..10).map { stage ->
            val globalIndex = (chapter - 1) * 10 + stage
            StageRow(
                id = "stage.%02d-%02d".format(chapter, stage),
                level = globalIndex * 2 - 1,
                q = globalIndex,
                skill = "없음",
                normalHp = 1,
                normalAttack = 1,
                bossType = "NONE",
                bossHp = 1,
                bossAttack = 1,
                defense = 0,
                pattern = "",
            )
        }
    }
}
