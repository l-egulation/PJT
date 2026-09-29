package com.hanjjak.gameapi.ranking

import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

class CombatPowerFormulaTest {
    @Test
    fun `initial fighter stats produce the baseline score`() {
        assertEquals(3_000L, CombatPowerFormula.calculate(FighterStats(40, 400, 20)))
    }

    @Test
    fun `weights standard offense twice and maximum hp once`() {
        assertEquals(5_000L, CombatPowerFormula.calculate(FighterStats(80, 400, 20)))
        assertEquals(4_000L, CombatPowerFormula.calculate(FighterStats(40, 800, 20)))
        assertEquals(3_003L, CombatPowerFormula.calculate(FighterStats(40, 401, 20)))
    }

    @Test
    fun `every equipped active effect increases standard combat power`() {
        val fighter = FighterStats(40, 400, 20)
        val baseline = CombatPowerFormula.calculate(fighter)
        val profiles = listOf(
            SkillProfile(heavyBasisPoints = 20_000, activeOrder = listOf("active_heavy")),
            SkillProfile(dotTotalBasisPoints = 25_000, activeOrder = listOf("active_dot")),
            SkillProfile(hasteBasisPoints = 2_000, activeOrder = listOf("active_haste")),
            SkillProfile(basicAmplificationBasisPoints = 2_000, activeOrder = listOf("active_basic_amp")),
        )

        profiles.forEach { profile ->
            assert(CombatPowerFormula.calculate(fighter, profile) > baseline)
        }
    }

    @Test
    fun `permanent offensive effects increase standard combat power`() {
        val fighter = FighterStats(40, 400, 20)
        val baseline = CombatPowerFormula.calculate(fighter)

        assert(CombatPowerFormula.calculate(fighter, SkillProfile(allDamageBasisPoints = 1_000)) > baseline)
        assert(CombatPowerFormula.calculate(fighter, SkillProfile(criticalChanceBasisPoints = 1_000)) > baseline)
        assert(CombatPowerFormula.calculate(fighter, SkillProfile(permanentHasteBasisPoints = 1_000)) > baseline)
        assert(CombatPowerFormula.calculate(fighter, SkillProfile(permanentBasicAmplificationBasisPoints = 1_000)) > baseline)
    }

    @Test
    fun `unmounted active effects do not change combat power`() {
        val fighter = FighterStats(40, 400, 20)
        val unmounted = SkillProfile(heavyBasisPoints = 92_500, activeOrder = emptyList())

        assertEquals(CombatPowerFormula.calculate(fighter), CombatPowerFormula.calculate(fighter, unmounted))
    }

    @Test
    fun `buff casts replace their action instead of adding a phantom basic hit`() {
        val fighter = FighterStats(40, 400, 20)
        val hasteOnly = SkillProfile(hasteBasisPoints = 2_000, activeOrder = listOf("active_haste"))
        val amplificationOnly = SkillProfile(basicAmplificationBasisPoints = 2_000, activeOrder = listOf("active_basic_amp"))

        assertEquals(3_120L, CombatPowerFormula.calculate(fighter, hasteOnly))
        assertEquals(3_112L, CombatPowerFormula.calculate(fighter, amplificationOnly))
    }

    @Test
    fun `buff duration extends haste and basic amplification windows`() {
        val fighter = FighterStats(40, 400, 20)
        val haste = SkillProfile(hasteBasisPoints = 2_000, activeOrder = listOf("active_haste"))
        val amplification = SkillProfile(basicAmplificationBasisPoints = 2_000, activeOrder = listOf("active_basic_amp"))

        assert(CombatPowerFormula.calculate(fighter, haste.copy(buffDurationBonusTicks = 20)) > CombatPowerFormula.calculate(fighter, haste))
        assert(CombatPowerFormula.calculate(fighter, amplification.copy(buffDurationBonusTicks = 20)) > CombatPowerFormula.calculate(fighter, amplification))
    }
}
