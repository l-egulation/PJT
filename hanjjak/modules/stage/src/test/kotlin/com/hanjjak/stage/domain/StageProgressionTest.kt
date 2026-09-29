package com.hanjjak.stage.domain

import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals

class StageProgressionTest {
    @Test
    fun `first clear timestamp is immutable while clear count advances`() {
        val initial = StageProgression.initial(UUID.randomUUID(), "enemy-v1-applied")
        val first = StageProgression.clear(initial, Instant.parse("2026-09-02T00:00:00Z"))
        val repeated = StageProgression.clear(first, Instant.parse("2026-09-02T01:00:00Z"))
        assertEquals(first.firstClearedAt, repeated.firstClearedAt)
        assertEquals(2, repeated.clearCount)
    }
}
