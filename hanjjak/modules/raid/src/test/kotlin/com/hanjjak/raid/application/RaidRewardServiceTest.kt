package com.hanjjak.raid.application

import com.hanjjak.raid.domain.RaidGrade
import com.hanjjak.raid.domain.RaidRewardBundle
import com.hanjjak.raid.domain.RaidRules
import kotlin.test.Test
import kotlin.test.assertEquals

class RaidRewardServiceTest {
    @Test
    fun `personal rewards exhaust every canonical grade`() {
        val expected = mapOf(
            RaidGrade.PARTICIPATION to RaidRewardBundle(5, 3, 5_000),
            RaidGrade.D to RaidRewardBundle(6, 3, 5_750),
            RaidGrade.C to RaidRewardBundle(7, 4, 6_500),
            RaidGrade.B to RaidRewardBundle(7, 4, 7_250),
            RaidGrade.A to RaidRewardBundle(8, 5, 8_000),
            RaidGrade.S to RaidRewardBundle(9, 5, 8_750),
            RaidGrade.SS to RaidRewardBundle(10, 6, 9_500),
            RaidGrade.SSS to RaidRewardBundle(10, 6, 10_000),
        )
        expected.forEach { (grade, reward) -> assertEquals(reward, RaidRules.personalReward(grade)) }
    }

    @Test
    fun `rank rewards exhaust success and failure bands including rest failure`() {
        val expectedSuccess = listOf(
            RaidRewardBundle(11, 7, 11_000),
            RaidRewardBundle(10, 6, 10_000),
            RaidRewardBundle(9, 5, 9_000),
            RaidRewardBundle(8, 5, 8_000),
        )
        val expectedFailure = listOf(
            RaidRewardBundle(8, 5, 7_700),
            RaidRewardBundle(7, 4, 7_000),
            RaidRewardBundle(6, 4, 6_300),
            RaidRewardBundle(6, 4, 5_600),
        )
        listOf(1, 2, 11, 101).forEachIndexed { index, rank ->
            assertEquals(expectedSuccess[index], RaidRules.rankReward(true, rank))
            assertEquals(expectedFailure[index], RaidRules.rankReward(false, rank))
        }
    }

    @Test
    fun `threshold and contribution are authoritative and zero damage is participation`() {
        val thresholds = mapOf("D" to 10L, "C" to 20L, "B" to 30L, "A" to 40L, "S" to 50L, "SS" to 60L, "SSS" to 70L)
        val contribution = mapOf("PARTICIPATION" to 0, "D" to 100, "C" to 167, "B" to 233, "A" to 300, "S" to 367, "SS" to 433, "SSS" to 500)
        assertEquals(RaidGrade.PARTICIPATION, RaidRules.grade(0, thresholds))
        assertEquals(RaidGrade.PARTICIPATION, RaidRules.grade(9, thresholds))
        assertEquals(RaidGrade.D, RaidRules.grade(10, thresholds))
        assertEquals(RaidGrade.SSS, RaidRules.grade(70, thresholds))
        assertEquals(233L, RaidRules.contribution(RaidGrade.B, contribution))
    }

    @Test
    fun `runtime validator rejects any drift from approved tables`() {
        val thresholds = linkedMapOf("D" to 6_955L, "C" to 12_916L, "B" to 19_871L, "A" to 29_807L, "S" to 43_716L, "SS" to 63_587L, "SSS" to 89_420L)
        val contributions = linkedMapOf("PARTICIPATION" to 0, "D" to 100, "C" to 167, "B" to 233, "A" to 300, "S" to 367, "SS" to 433, "SSS" to 500)
        RaidRules.validateTables(thresholds, contributions)
        kotlin.test.assertFailsWith<IllegalArgumentException> { RaidRules.validateTables(thresholds + ("D" to 6_956L), contributions) }
        kotlin.test.assertFailsWith<IllegalArgumentException> { RaidRules.validateTables(thresholds, contributions + ("B" to 234)) }
    }
}
