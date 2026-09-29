package com.hanjjak.battle.application

import com.hanjjak.sim.CombatSimulator
import com.hanjjak.sim.FighterStats
import com.hanjjak.stage.application.InMemoryStageCatalog
import com.hanjjak.stage.domain.BossType
import com.hanjjak.stage.domain.StageDefinition
import com.hanjjak.stage.domain.StageId
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class StageBattleServiceTest {
    @Test
    fun `stage 1-1 runs a complete 20 normal plus boss cycle`() {
        val service = StageBattleService(InMemoryStageCatalog.minimumVerticalSlice())
        val result = service.run("stage.01-01", FighterStats(500, 1000, 100), 42, "enemy-v1-applied")
        assertTrue(result.success)
        assertEquals(20, result.defeatedNormals)
        assertEquals(1000, result.remainingHp)
    }

    @Test
    fun `issued battle input reproduces the authoritative result`() {
        val service = StageBattleService(InMemoryStageCatalog.minimumVerticalSlice())
        val input = service.input("stage.01-01", FighterStats(500, 1000, 100), 42, "enemy-v1-applied")

        assertEquals(service.run("stage.01-01", input.player, input.seed, input.contentVersion, input.skills), CombatSimulator.simulate(input))
    }

    @Test
    fun `every chapter stage 10 starts without normal encounters`() {
        val definitions = (1..4).flatMap { chapter ->
            (1..10).map { number ->
                StageDefinition(StageId(chapter, number), 1, 0, 1, 0, 1, 0, BossType.STANDARD)
            }
        }
        val service = StageBattleService(InMemoryStageCatalog(definitions))

        definitions.forEach { stage ->
            val input = service.input(stage.id.key, FighterStats(1, 1, 0), 42, "enemy-v1-applied")
            assertEquals(if (stage.id.number == 10) 0 else 20, input.normalCount, stage.id.key)
        }
    }

    @Test
    fun `stage 10 starts directly with its single final boss`() {
        val finalStage = StageDefinition(StageId(3, 10), 59, 674, 6210, 93, 43495, 85, BossType.ATTACK_CHECK)
        val service = StageBattleService(InMemoryStageCatalog(listOf(finalStage)))
        val input = service.input("stage.03-10", FighterStats(1000, 1000, 100), 42, "v1")
        val trace = CombatSimulator.simulateWithEvents(input)

        assertEquals(0, input.normalCount)
        assertEquals("BOSS_SPAWNED", trace.renderingTimeline.first().type)
        assertEquals(1, trace.renderingTimeline.first().enemyIndex)
        assertTrue(trace.renderingTimeline.first().boss)
    }
}
