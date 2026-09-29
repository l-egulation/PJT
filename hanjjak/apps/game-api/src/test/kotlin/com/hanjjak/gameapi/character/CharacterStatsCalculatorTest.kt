package com.hanjjak.gameapi.character

import com.hanjjak.equipment.domain.*
import com.hanjjak.gems.domain.*
import com.hanjjak.sim.SkillProfile
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.*
import java.util.UUID

class CharacterStatsCalculatorTest {
    private val account = UUID(0, 1)
    private fun gem(id: Long, option: GemOption, value: Int) = GemInstance(UUID(0, id), account, 7, option, value, false)
    private fun attackWith(glovesLevel: Int): Int = CharacterStatsCalculator.calculate(
        1,
        mapOf(EquipmentSlot.GLOVES to EquipmentSlotState(account, EquipmentSlot.GLOVES, EquipmentGrade.NORMAL, glovesLevel)),
        emptyList(),
        SkillProfile(),
        emptyList(),
    ).fighter.attack

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

    @Test fun `base uses level and rounds permanent equipment contribution once`() {
        val equipment = EquipmentSlot.entries.associateWith { slot -> EquipmentSlotState(account, slot, EquipmentGrade.NORMAL, 3) }
        val result = CharacterStatsCalculator.calculate(2, equipment, emptyList(), SkillProfile(), emptyList())
        assertEquals(108, result.stats.first { it.statId == "attack" }.base)
        assertEquals(38, result.fighter.penetration)
        assertEquals(result.fighter.attack, result.stats.first { it.statId == "attack" }.total)
    }

    @Test fun `promoted equipment uses q jump and stable slot source identity`() {
        val result = CharacterStatsCalculator.calculate(1, mapOf(
            EquipmentSlot.WEAPON to EquipmentSlotState(account, EquipmentSlot.WEAPON, EquipmentGrade.RARE, 1),
        ), emptyList(), SkillProfile(), emptyList())
        val source = result.stats.first { it.statId == "attack" }.sources.first { it.sourceId == "equipment.WEAPON" }
        assertEquals("무기 희귀 +1", source.label)
        assertTrue(source.value > 95.0)
        assertEquals(160, result.stats.first { it.statId == "attack" }.base)
    }

    @Test fun `gem max effects retain excluded source and percentages run after flats`() {
        val result = CharacterStatsCalculator.calculate(1, emptyMap(), listOf(
            gem(2, GemOption.FLAT_ATTACK, 10), gem(3, GemOption.ATTACK_PERCENT, 800), gem(4, GemOption.ATTACK_PERCENT, 1500)
        ), SkillProfile(), listOf(CharacterSetBonus("set.test", "시험 세트", listOf(CharacterSetEffect("attackPercent", 1000, "BASIS_POINTS")))))
        val row = result.stats.first { it.statId == "attack" }
        assertEquals(100, row.base)
        assertEquals(139, row.total)
        assertEquals(39, row.additional)
        assertFalse(row.sources.first { it.sourceId == UUID(0, 3).toString() }.applied)
        assertEquals(800.0, row.sources.first { it.sourceId == UUID(0, 3).toString() }.value)
    }

    @Test fun `temporary active buffs do not appear as standing bonuses`() {
        val result = CharacterStatsCalculator.calculate(1, emptyMap(), listOf(gem(2, GemOption.HASTE, 1200)),
            SkillProfile(hasteBasisPoints = 3000, basicAmplificationBasisPoints = 5000, criticalChanceBasisPoints = 7000),
            listOf(CharacterSetBonus("set.test", "시험 세트", listOf(CharacterSetEffect("criticalChancePoint", 1000, "BASIS_POINTS")))))
        assertEquals(1200, result.stats.first { it.statId == "attackSpeed" }.total)
        assertEquals(0, result.stats.first { it.statId == "basicAttackDamage" }.total)
        assertEquals(7500, result.skills.criticalChanceBasisPoints)
        assertEquals(3000, result.skills.hasteBasisPoints)
        assertEquals(1200, result.skills.permanentHasteBasisPoints)
    }
}
