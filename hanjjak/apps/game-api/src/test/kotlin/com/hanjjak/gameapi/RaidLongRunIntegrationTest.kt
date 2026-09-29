package com.hanjjak.gameapi

import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.RaidCombatInput
import com.hanjjak.sim.RaidSimulator
import com.hanjjak.sim.SkillProfile
import org.junit.jupiter.api.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

/** Database-free deterministic evidence; this test must execute without Spring or Docker. */
class RaidLongRunIntegrationTest {
    @Test
    fun `one thousand seed combat replay has a stable terminal result`() {
        val input = RaidCombatInput(
            contentVersion = "raid-mvp-v1-working",
            seed = 1L,
            player = FighterStats(100, 1_000_000, 20),
            skills = SkillProfile(activeOrder = emptyList()),
            bossInitialAttack = 14,
            bossInitialDefense = 1,
        )
        val first = (1L..1_000L).map { RaidSimulator.simulate(input.copy(seed = it)) }
        val replay = (1L..1_000L).map { RaidSimulator.simulate(input.copy(seed = it)) }

        assertEquals(first, replay)
        assertEquals(1_000, first.size)
        assertTrue(first.all { it.elapsedTicks in 1..3_000 })
    }
}
