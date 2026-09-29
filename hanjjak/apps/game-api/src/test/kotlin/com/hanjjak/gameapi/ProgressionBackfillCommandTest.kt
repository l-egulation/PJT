package com.hanjjak.gameapi

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class ProgressionBackfillCommandTest {
    @Test
    fun `dry run parses without apply and default batch`() {
        assertEquals(ProgressionBackfillOptions(true, 100), ProgressionBackfillCommand.parse(listOf("--dry-run")))
    }

    @Test
    fun `apply parses explicit batch size`() {
        assertEquals(ProgressionBackfillOptions(false, 25, false), ProgressionBackfillCommand.parse(listOf("--apply", "--batch-size=25")))
    }

    @Test
    fun `mode and batch are validated`() {
        assertFailsWith<IllegalArgumentException> { ProgressionBackfillCommand.parse(emptyList()) }
        assertFailsWith<IllegalArgumentException> { ProgressionBackfillCommand.parse(listOf("--dry-run", "--apply")) }
        assertFailsWith<IllegalArgumentException> { ProgressionBackfillCommand.parse(listOf("--apply", "--batch-size=0")) }
        assertFailsWith<IllegalArgumentException> { ProgressionBackfillCommand.parse(listOf("--apply", "--batch-size=abc")) }
    }
    @Test
    fun `backfill command disables scheduling and servlet startup`() {
        val properties = ProgressionBackfillCommand.applicationProperties()
        assertEquals("false", properties["spring.task.scheduling.enabled"])
        assertEquals("none", properties["spring.main.web-application-type"])
    }
    @Test
    fun `until complete is apply only`() {
        assertEquals(ProgressionBackfillOptions(false, 100, true), ProgressionBackfillCommand.parse(listOf("--apply", "--until-complete")))
        assertFailsWith<IllegalArgumentException> { ProgressionBackfillCommand.parse(listOf("--dry-run", "--until-complete")) }
    }

    @Test
    fun `command failure remains machine readable`() {
        assertEquals(
            mapOf("errorCode" to "BALANCE_VERSION_NOT_APPLIED"),
            ProgressionBackfillCommand.failurePayload(IllegalArgumentException("BALANCE_VERSION_NOT_APPLIED")),
        )
    }
}
