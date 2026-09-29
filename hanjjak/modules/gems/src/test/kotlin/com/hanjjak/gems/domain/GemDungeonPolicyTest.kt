package com.hanjjak.gems.domain

import java.time.Duration
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNotEquals

class GemDungeonPolicyTest {
    private val kst = ZoneId.of("Asia/Seoul")
    private val applied = GemDungeonPolicy()
    private val legacy = GemDungeonPolicy(rotationPeriodSeconds = 86_400, ticketRegenSeconds = 28_800, ticketMaxStock = 3, contentResource = "classpath:gem-dungeons/gem-dungeons.json")

    @Test
    fun `applied defaults use the accelerated v2 loop`() {
        assertEquals(3_600, applied.rotationPeriodSeconds)
        assertEquals(3_600, applied.ticketRegenSeconds)
        assertEquals(3, applied.ticketMaxStock)
        assertEquals(3, applied.ticketInitialGrant)
        assertEquals("classpath:gem-dungeons/gem-dungeons-v2.json", applied.contentResource)
        assertEquals("applied", applied.contentAuthority)
    }

    @Test
    fun `daily rotation reproduces the KST epoch day cycle it replaced`() {
        var now = Instant.parse("2026-09-01T00:00:00Z")
        repeat(200) {
            assertEquals(
                GemRules.todayBoss(LocalDate.ofInstant(now, kst).toEpochDay()),
                GemRules.bossForSlot(legacy.slotIndex(now)),
            )
            now = now.plus(Duration.ofHours(5))
        }
    }

    @Test
    fun `daily slots start at KST midnight`() {
        val now = Instant.parse("2026-09-12T04:37:11Z")
        assertEquals(LocalDate.ofInstant(now, kst).atStartOfDay(kst).toInstant(), legacy.slotStartedAt(now))
    }

    @Test
    fun `hourly rotation opens a new boss every hour and repeats every three`() {
        var now = Instant.parse("2026-09-12T04:37:11Z")
        repeat(48) {
            assertNotEquals(
                GemRules.bossForSlot(applied.slotIndex(now)),
                GemRules.bossForSlot(applied.slotIndex(now.plus(Duration.ofHours(1)))),
            )
            assertEquals(
                GemRules.bossForSlot(applied.slotIndex(now)),
                GemRules.bossForSlot(applied.slotIndex(now.plus(Duration.ofHours(3)))),
            )
            now = now.plus(Duration.ofHours(1))
        }
    }

    @Test
    fun `hourly slots start on the hour and report the remaining time`() {
        val now = Instant.parse("2026-09-12T04:37:11Z")
        assertEquals(Instant.parse("2026-09-12T04:00:00Z"), applied.slotStartedAt(now))
        assertEquals(3_600 - (37 * 60 + 11).toLong(), applied.secondsUntilNextSlot(now))
        assertEquals(Instant.parse("2026-09-12T04:00:00Z"), applied.ticketSlotStartedAt(now))
        assertEquals(applied.secondsUntilNextSlot(now), applied.secondsUntilNextTicketSlot(now))
    }

    @Test
    fun `rotation is stable before the KST epoch`() {
        val beforeEpoch = Instant.parse("1969-12-31T10:00:00Z")
        assertEquals(
            GemRules.todayBoss(LocalDate.ofInstant(beforeEpoch, kst).toEpochDay()),
            GemRules.bossForSlot(legacy.slotIndex(beforeEpoch)),
        )
    }

    @Test
    fun `invalid policies are rejected at startup`() {
        assertFailsWith<IllegalArgumentException> { GemDungeonPolicy(rotationPeriodSeconds = 0) }
        assertFailsWith<IllegalArgumentException> { GemDungeonPolicy(ticketRegenSeconds = 0) }
        assertFailsWith<IllegalArgumentException> { GemDungeonPolicy(ticketMaxStock = 0) }
        assertFailsWith<IllegalArgumentException> { GemDungeonPolicy(ticketMaxStock = 3, ticketInitialGrant = 4) }
    }
}
