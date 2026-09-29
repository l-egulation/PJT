package com.hanjjak.stage.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class StageTest {
    @Test
    fun `global index and stable key follow stage contract`() {
        val stage = StageId(4, 10)
        assertEquals(40, stage.globalIndex)
        assertEquals("stage.04-10", stage.key)
    }

    @Test
    fun `chapters five through ten are addressable`() {
        assertEquals(50, StageId(5, 10).globalIndex)
        assertEquals("stage.05-10", StageId(5, 10).key)
        assertEquals(100, StageId(10, 10).globalIndex)
        assertEquals("stage.10-10", StageId(10, 10).key)
    }

    @Test
    fun `chapter beyond the maximum is rejected`() {
        assertFailsWith<IllegalArgumentException> { StageId(11, 1) }
        assertFailsWith<IllegalArgumentException> { StageId(0, 1) }
        assertFailsWith<IllegalArgumentException> { StageId(5, 11) }
    }

    @Test
    fun `every chapter final stage is boss only`() {
        assertTrue(definition(StageId(1, 10)).bossOnly)
        assertTrue(definition(StageId(7, 10)).bossOnly)
        assertFalse(definition(StageId(7, 9)).bossOnly)
    }

    private fun definition(id: StageId) = StageDefinition(
        id = id,
        enemyLevel = 1,
        enemyDefense = 0,
        normalHp = 1,
        normalAttack = 0,
        bossHp = 1,
        bossAttack = 0,
        bossType = BossType.STANDARD,
    )
}
