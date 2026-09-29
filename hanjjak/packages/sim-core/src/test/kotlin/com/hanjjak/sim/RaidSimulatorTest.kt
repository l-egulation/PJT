package com.hanjjak.sim

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNotEquals
import kotlin.test.assertTrue

class RaidSimulatorTest {
    @Test
    fun `escalation applies before actions at tick fifty`() {
        val result = RaidSimulator.simulate(
            fixture(
                maxTicks = 60,
                skills = SkillProfile(dotTotalBasisPoints = 25_000, activeOrder = listOf("active_dot")),
            ),
        )

        val escalation = result.events.single { it.logicalTick == 50 && it.type == RaidCombatEventType.ESCALATION }
        val sameTickActions = result.events.filter { it.logicalTick == 50 && it.type != RaidCombatEventType.ESCALATION }

        assertTrue(sameTickActions.isNotEmpty())
        assertTrue(sameTickActions.all { it.sequence > escalation.sequence })
        assertEquals(1, escalation.escalationStage)
        assertEquals(105, escalation.bossDefense)
    }

    @Test
    fun `raid ends when boss attack kills the player`() {
        val result = RaidSimulator.simulate(
            fixture(player = FighterStats(100, 50, 20), bossInitialAttack = 100, maxTicks = 3_000),
        )

        assertTrue(result.playerDied)
        assertTrue(result.elapsedTicks < 3_000)
        assertEquals(RaidCombatEventType.PLAYER_DIED, result.events.last().type)
        assertEquals(0, result.remainingPlayerHp)
    }

    @Test
    fun `raid hard stops at three thousand ticks`() {
        val result = RaidSimulator.simulate(fixture(maxTicks = 3_000))

        assertEquals(3_000, result.elapsedTicks)
        assertFalse(result.playerDied)
        assertEquals(RaidCombatEventType.TIME_LIMIT_REACHED, result.events.last().type)
    }

    @Test
    fun `same input and seed replay exactly`() {
        val input = fixture(seed = 42, skills = SkillProfile(criticalChanceBasisPoints = 5_000))

        assertEquals(RaidSimulator.simulate(input), RaidSimulator.simulate(input))
    }

    @Test
    fun `different fixed seeds can change critical damage`() {
        val first = RaidSimulator.simulate(fixture(seed = 1, maxTicks = 40, skills = SkillProfile(criticalChanceBasisPoints = 5_000)))
        val second = RaidSimulator.simulate(fixture(seed = 2, maxTicks = 40, skills = SkillProfile(criticalChanceBasisPoints = 5_000)))

        assertNotEquals(first.totalDamage, second.totalDamage)
    }

    @Test
    fun `boss stats use initial value times compound stage with half up rounding`() {
        val result = RaidSimulator.simulate(fixture(bossInitialAttack = 101, bossInitialDefense = 101, maxTicks = 110))

        val stageOne = result.events.single { it.type == RaidCombatEventType.ESCALATION && it.escalationStage == 1 }
        val stageTwo = result.events.single { it.type == RaidCombatEventType.ESCALATION && it.escalationStage == 2 }

        assertEquals(106, stageOne.bossAttack)
        assertEquals(106, stageOne.bossDefense)
        assertEquals(111, stageTwo.bossAttack)
        assertEquals(111, stageTwo.bossDefense)
    }

    private fun fixture(
        seed: Long = 1,
        player: FighterStats = FighterStats(100, 1_000_000_000, 20),
        skills: SkillProfile = SkillProfile(activeOrder = emptyList()),
        bossInitialAttack: Int = 0,
        bossInitialDefense: Int = 100,
        maxTicks: Int = 100,
    ) = RaidCombatInput(
        contentVersion = "raid-test",
        seed = seed,
        player = player,
        skills = skills,
        bossInitialAttack = bossInitialAttack,
        bossInitialDefense = bossInitialDefense,
        maxTicks = maxTicks,
    )
}
