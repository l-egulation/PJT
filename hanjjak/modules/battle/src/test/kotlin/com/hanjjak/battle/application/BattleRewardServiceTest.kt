package com.hanjjak.battle.application

import com.hanjjak.sim.FighterStats
import com.hanjjak.stage.application.InMemoryStageCatalog
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class BattleRewardServiceTest {
    @Test
    fun `stage battle remains available after inventory reward integration`() {
        val service = StageBattleService(InMemoryStageCatalog.minimumVerticalSlice())
        val result = service.run("stage.01-01", FighterStats(500, 1000, 100), 42, "enemy-v1-applied")
        assertTrue(result.success)
        assertEquals(20, result.defeatedNormals)
    }
}
