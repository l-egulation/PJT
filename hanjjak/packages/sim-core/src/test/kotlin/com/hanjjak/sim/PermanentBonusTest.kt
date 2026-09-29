package com.hanjjak.sim

import kotlin.test.Test
import kotlin.test.assertEquals

class PermanentBonusTest {
    private fun input(skills: SkillProfile) = CycleInput("test", 1, FighterStats(10, 100, 20), EnemyStats(1, 0, 20), EnemyStats(40, 0, 20), skills, normalCount = 0)

    @Test fun `permanent haste works without learning an active buff`() {
        assertEquals(6, CombatSimulator.simulate(input(SkillProfile(permanentHasteBasisPoints = 10000, activeOrder = emptyList()))).elapsedTicks)
        assertEquals(12, CombatSimulator.simulate(input(SkillProfile(activeOrder = emptyList()))).elapsedTicks)
    }

    @Test fun `permanent basic damage does not require an active buff`() {
        assertEquals(4, CombatSimulator.simulate(input(SkillProfile(permanentBasicAmplificationBasisPoints = 10000, activeOrder = emptyList()))).elapsedTicks)
    }

    @Test fun `twelve percent gem haste accumulates between attacks without an active buff`() {
        // Thirty ten-damage attacks need 29 intervals: 11.6s normally, about 10.36s with the gem.
        val baseline = input(SkillProfile(activeOrder = emptyList())).copy(boss = EnemyStats(300, 0, 20))
        val withGem = baseline.copy(skills = baseline.skills.copy(permanentHasteBasisPoints = 1200))
        assertEquals(116, CombatSimulator.simulate(baseline).elapsedTicks)
        assertEquals(104, CombatSimulator.simulate(withGem).elapsedTicks)
    }
}
