package com.hanjjak.skills.application

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class SkillRollSourceTest {
    @Test
    fun `validated roll rejects values outside basis point range`() {
        val error = assertFailsWith<IllegalArgumentException> {
            validatedRoll(SkillRollSource { 10_000 })
        }
        assertEquals("SKILL_ROLL_OUT_OF_RANGE", error.message)
    }

    @Test
    fun `validated roll accepts range boundaries`() {
        assertEquals(0, validatedRoll(SkillRollSource { 0 }))
        assertEquals(9_999, validatedRoll(SkillRollSource { 9_999 }))
    }
}
