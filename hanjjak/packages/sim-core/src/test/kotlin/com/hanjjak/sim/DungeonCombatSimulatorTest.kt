package com.hanjjak.sim

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class DungeonCombatSimulatorTest {
    @Test
    fun `survival applies seven strikes and succeeds at one hp`() {
        val result = DungeonCombatSimulator.simulate(
            DungeonCombatInput("test", 1, DungeonMode.SURVIVE, FighterStats(1, 701, 0), SkillProfile(), DungeonBossStats(1, 0, 0, 150, 100)),
        )
        assertTrue(result.success)
        assertEquals(1, result.remainingPlayerHp)
        assertEquals(7, result.events.count { it.type == DungeonEventType.SURVIVAL_STRIKE })
    }

    @Test
    fun `survival fails when the final strike consumes all hp`() {
        val result = DungeonCombatSimulator.simulate(
            DungeonCombatInput("test", 1, DungeonMode.SURVIVE, FighterStats(1, 700, 0), SkillProfile(), DungeonBossStats(1, 0, 0, 150, 100)),
        )
        assertFalse(result.success)
        assertEquals("PLAYER_DIED", result.failureCode)
    }

    @Test
    fun `offensive encounter is deterministic and emits ordered events`() {
        val input = DungeonCombatInput("test", 42, DungeonMode.DEFEAT_BOSS, FighterStats(100, 1_000, 100), SkillProfile(criticalChanceBasisPoints = 5_000), DungeonBossStats(1_000, 0, 100, 150))
        val first = DungeonCombatSimulator.simulate(input)
        val second = DungeonCombatSimulator.simulate(input)
        assertEquals(first, second)
        assertTrue(first.events.zipWithNext().all { (a, b) -> a.sequence < b.sequence && a.tick <= b.tick })
    }

    @Test
    fun `permanent basic amplification increases dungeon basic attack damage`() {
        fun firstHit(skills: SkillProfile) = DungeonCombatSimulator.simulate(
            DungeonCombatInput("test", 1, DungeonMode.DEFEAT_BOSS, FighterStats(100, 1_000, 0), skills, DungeonBossStats(10_000, 0, 0, 10)),
        ).events.first { it.type == DungeonEventType.PLAYER_HIT }.amount

        assertEquals(100, firstHit(SkillProfile(activeOrder = emptyList())))
        assertEquals(200, firstHit(SkillProfile(permanentBasicAmplificationBasisPoints = 10_000, activeOrder = emptyList())))
    }

    @Test
    fun `permanent haste increases dungeon basic attack frequency`() {
        fun hitCount(skills: SkillProfile) = DungeonCombatSimulator.simulate(
            DungeonCombatInput("test", 1, DungeonMode.DEFEAT_BOSS, FighterStats(1, 1_000, 0), skills, DungeonBossStats(10_000, 0, 0, 15)),
        ).events.count { it.type == DungeonEventType.PLAYER_HIT }

        assertEquals(4, hitCount(SkillProfile(activeOrder = emptyList())))
        assertEquals(8, hitCount(SkillProfile(permanentHasteBasisPoints = 10_000, activeOrder = emptyList())))
    }

    @Test
    fun `small permanent haste accumulates across dungeon actions`() {
        fun hitCount(skills: SkillProfile) = DungeonCombatSimulator.simulate(
            DungeonCombatInput("test", 1, DungeonMode.DEFEAT_BOSS, FighterStats(1, 1_000, 0), skills, DungeonBossStats(100_000, 0, 0, 150)),
        ).events.count { it.type == DungeonEventType.PLAYER_HIT }

        val baseline = hitCount(SkillProfile(activeOrder = emptyList()))
        val withHaste = hitCount(SkillProfile(permanentHasteBasisPoints = 1_200, activeOrder = emptyList()))
        assertTrue(withHaste > baseline)
    }

    @Test
    fun `dot cast lock is one third of the normal action lock`() {
        val result = DungeonCombatSimulator.simulate(
            DungeonCombatInput(
                "dot-field-v1",
                1,
                DungeonMode.DEFEAT_BOSS,
                FighterStats(1, 1_000, 0),
                SkillProfile(dotTotalBasisPoints = 10_000, activeOrder = listOf("active_dot")),
                DungeonBossStats(100, 0, 0, 12),
            ),
        )

        assertEquals(0, result.events.first { it.type == DungeonEventType.SKILL_CAST }.tick)
        assertEquals(2, result.events.first { it.type == DungeonEventType.PLAYER_HIT }.tick)
        assertEquals(listOf(10), result.events.filter { it.type == DungeonEventType.DOT_HIT }.map { it.tick })
    }
}
