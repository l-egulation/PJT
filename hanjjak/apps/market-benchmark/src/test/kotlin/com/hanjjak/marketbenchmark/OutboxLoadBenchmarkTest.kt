package com.hanjjak.marketbenchmark

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class OutboxLoadBenchmarkTest {
    @Test
    fun `parses outbox load config`() {
        val config = OutboxLoadConfig.from(
            arrayOf(
                "--mode", "outbox-load",
                "--db-url", "jdbc:postgresql://localhost:5432/hanjjak_load",
                "--db-user", "tester",
                "--db-password", "secret",
                "--event-count", "2000",
                "--batch-sizes", "100,500",
                "--worker-counts", "1,2",
                "--output-path", "build/reports/outbox-load/test.json",
            ),
            emptyMap(),
        )

        assertEquals("jdbc:postgresql://localhost:5432/hanjjak_load", config.dbUrl)
        assertEquals(2_000, config.eventCount)
        assertEquals(listOf(100, 500), config.batchSizes)
        assertEquals(listOf(1, 2), config.workerCounts)
        config.validateSafety()
    }

    @Test
    fun `reset outbox refuses non local db by default`() {
        val config = OutboxLoadConfig.from(
            arrayOf(
                "--mode", "outbox-load",
                "--db-url", "jdbc:postgresql://prod.example.com:5432/hanjjak",
            ),
            emptyMap(),
        )

        val error = assertFailsWith<IllegalArgumentException> { config.validateSafety() }
        assertTrue(error.message!!.contains("reset-outbox requires localhost DB"))
    }
}
