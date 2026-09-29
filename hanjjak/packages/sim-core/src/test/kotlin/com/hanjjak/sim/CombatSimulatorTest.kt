package com.hanjjak.sim

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class CombatSimulatorTest {
    @Test
    fun `same input always returns same result`() {
        val input = CycleInput("v1-working", 42, FighterStats(100, 1000, 20), EnemyStats(100, 5, 0), EnemyStats(300, 10, 0), SkillProfile(criticalChanceBasisPoints = 5000))
        assertEquals(CombatSimulator.simulate(input), CombatSimulator.simulate(input))
    }

    @Test
    fun `successful cycle restores full hp`() {
        val input = CycleInput("v1-working", 1, FighterStats(500, 1000, 100), EnemyStats(10, 1, 0), EnemyStats(20, 1, 0))
        val result = CombatSimulator.simulate(input)
        assertTrue(result.success)
        assertEquals(1000, result.remainingHp)
        assertEquals(20, result.defeatedNormals)
    }

    @Test
    fun `rendering timeline is deterministic and preserves authoritative result`() {
        val input = CycleInput("v1-working", 42, FighterStats(100, 1000, 20), EnemyStats(100, 5, 0), EnemyStats(300, 10, 0), SkillProfile(criticalChanceBasisPoints = 5000))
        val first = CombatSimulator.simulateWithEvents(input)
        val second = CombatSimulator.simulateWithEvents(input)

        assertEquals(CombatSimulator.simulate(input), first.result)
        assertEquals(first, second)
        assertTrue(first.renderingTimeline.any { it.type == "PLAYER_ATTACK_IMPACT" && it.damage != null })
        assertTrue(first.renderingTimeline.any { it.type == "PLAYER_HIT" && it.hpAfter != null })
        assertEquals(first.result.defeatedNormals, first.renderingTimeline.count { it.type == "ENEMY_DEFEATED" && !it.boss })
    }

    @Test
    fun `zero normal count completes exactly one boss encounter`() {
        val trace = CombatSimulator.simulateWithEvents(CycleInput("v1-working", 42, FighterStats(500, 1000, 100), EnemyStats(10, 1, 0), EnemyStats(20, 1, 0), normalCount = 0))

        assertTrue(trace.result.success)
        assertEquals(null, trace.result.failureCode)
        assertEquals(0, trace.result.defeatedNormals)
        assertEquals(listOf(1), trace.renderingTimeline.filter { it.type == "BOSS_SPAWNED" }.map { it.enemyIndex })
        assertEquals(listOf(1), trace.renderingTimeline.filter { it.type == "ENEMY_DEFEATED" && it.boss }.map { it.enemyIndex })
        assertTrue(trace.renderingTimeline.none { it.type == "ENEMY_DEFEATED" && !it.boss })
        assertEquals("BATTLE_CYCLE_COMPLETED", trace.renderingTimeline.last().type)
    }

    @Test
    fun `dot field survives its first target and damages the monster currently in combat`() {
        val trace = CombatSimulator.simulateWithEvents(
            CycleInput(
                "dot-field-v1",
                7,
                FighterStats(1, 10_000, 0),
                EnemyStats(3, 0, 0),
                EnemyStats(1, 0, 0),
                SkillProfile(dotTotalBasisPoints = 50_000, activeOrder = listOf("active_dot")),
                normalCount = 2,
            ),
        )

        val dotTargets = trace.renderingTimeline.filter { it.type == "DOT_TICK" }.mapNotNull { it.enemyIndex }
        assertTrue(dotTargets.contains(1))
        assertTrue(dotTargets.contains(2))
    }

    @Test
    fun `normal monsters replace immediately while boss keeps its entrance gap`() {
        val trace = CombatSimulator.simulateWithEvents(
            CycleInput(
                "instant-normal-swap-v1",
                11,
                FighterStats(10_000, 10_000, 0),
                EnemyStats(1, 0, 0),
                EnemyStats(1, 0, 0),
                normalCount = 2,
            ),
        )

        val normalSpawns = trace.renderingTimeline.filter { it.type == "ENEMY_SPAWNED" }
        val lastNormalDefeat = trace.renderingTimeline.single { it.type == "ENEMY_DEFEATED" && it.enemyIndex == 2 }
        val bossSpawn = trace.renderingTimeline.single { it.type == "BOSS_SPAWNED" }

        assertEquals(listOf(0, 0), normalSpawns.map { it.logicalTick })
        assertEquals(lastNormalDefeat.logicalTick + 5, bossSpawn.logicalTick)
        assertEquals(listOf(1, 2, 3), (normalSpawns + bossSpawn).map { it.enemyIndex })
    }

    @Test
    fun `arena uses thirty times hp and both fighters use skills`() {
        val skills = SkillProfile(heavyBasisPoints = 20_000, activeOrder = listOf("active_heavy"))
        val input = ArenaInput(
            "arena-v1", 42,
            ArenaFighter("attacker", FighterStats(100, 100, 20), skills, defense = 0),
            ArenaFighter("defender", FighterStats(100, 100, 20), skills, defense = 0),
        )
        val trace = ArenaSimulator.simulateWithEvents(input)
        assertTrue(trace.events.any { it.actorId == "attacker" && it.skillId == "active_heavy" })
        assertTrue(trace.events.any { it.actorId == "defender" && it.skillId == "active_heavy" })
        assertTrue(trace.events.any { it.hpBefore == 3_000 })
        assertEquals(ArenaSimulator.simulate(input), trace.result)
    }

    @Test
    fun `arena result is deterministic`() {
        val input = ArenaInput("arena-v1", 9, ArenaFighter("a", FighterStats(70, 100, 20)), ArenaFighter("d", FighterStats(60, 100, 20)))
        assertEquals(ArenaSimulator.simulateWithEvents(input), ArenaSimulator.simulateWithEvents(input))
    }
}
