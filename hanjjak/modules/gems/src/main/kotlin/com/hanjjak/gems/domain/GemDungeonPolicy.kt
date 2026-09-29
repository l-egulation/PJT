package com.hanjjak.gems.domain

import java.time.Instant

/**
 * Operational knobs for the gem dungeon loop.
 *
 * The defaults are the applied v2 service values. Older applied and beta content resources remain
 * immutable and can be selected through `hanjjak.gem-dungeon.*` for replay or rollback.
 */
data class GemDungeonPolicy(
    val rotationPeriodSeconds: Long = 3_600,
    val ticketRegenSeconds: Long = 3_600,
    val ticketMaxStock: Int = 3,
    val ticketInitialGrant: Int = 3,
    val contentResource: String = "classpath:gem-dungeons/gem-dungeons-v2.json",
    val contentAuthority: String = "applied",
) {
    init {
        require(rotationPeriodSeconds > 0) { "INVALID_GEM_DUNGEON_ROTATION_PERIOD" }
        require(ticketRegenSeconds > 0) { "INVALID_GEM_TICKET_REGEN_INTERVAL" }
        require(ticketMaxStock >= 1) { "INVALID_GEM_TICKET_MAX_STOCK" }
        require(ticketInitialGrant in 0..ticketMaxStock) { "INVALID_GEM_TICKET_INITIAL_GRANT" }
    }

    /**
     * KST-aligned rotation slot index. The applied 3,600 second period opens a new boss every KST
     * hour; the former 86,400 second period remains supported for v1 replay and rollback.
     */
    fun slotIndex(now: Instant): Long = Math.floorDiv(now.epochSecond + KST_OFFSET_SECONDS, rotationPeriodSeconds)

    fun slotStartedAt(now: Instant): Instant =
        Instant.ofEpochSecond(slotIndex(now) * rotationPeriodSeconds - KST_OFFSET_SECONDS)

    fun secondsUntilNextSlot(now: Instant): Long =
        rotationPeriodSeconds - (now.epochSecond - slotStartedAt(now).epochSecond)

    fun ticketSlotIndex(now: Instant): Long =
        Math.floorDiv(now.epochSecond + KST_OFFSET_SECONDS, ticketRegenSeconds)

    fun ticketSlotStartedAt(now: Instant): Instant =
        Instant.ofEpochSecond(ticketSlotIndex(now) * ticketRegenSeconds - KST_OFFSET_SECONDS)

    fun secondsUntilNextTicketSlot(now: Instant): Long =
        ticketRegenSeconds - (now.epochSecond - ticketSlotStartedAt(now).epochSecond)

    companion object {
        const val KST_OFFSET_SECONDS = 32_400L
    }
}
