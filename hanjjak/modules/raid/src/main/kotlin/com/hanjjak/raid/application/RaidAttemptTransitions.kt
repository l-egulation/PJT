package com.hanjjak.raid.application

import com.hanjjak.raid.domain.RaidAttemptStatus

enum class RaidAttemptAction { RETRY, DISCARD, TERMINALIZE, CONFIRM }

object RaidAttemptTransitions {
    private val allowed = mapOf(
        RaidAttemptStatus.RUNNING to setOf(RaidAttemptAction.RETRY, RaidAttemptAction.DISCARD, RaidAttemptAction.TERMINALIZE, RaidAttemptAction.CONFIRM),
        RaidAttemptStatus.RESULT_HELD to setOf(RaidAttemptAction.RETRY, RaidAttemptAction.DISCARD, RaidAttemptAction.CONFIRM),
        RaidAttemptStatus.DISCARDED to emptySet(),
        RaidAttemptStatus.CONFIRMED to emptySet(),
    )

    fun requireAllowed(status: RaidAttemptStatus, action: RaidAttemptAction) {
        require(action in allowed.getValue(status)) { "RAID_INVALID_TRANSITION" }
    }
}
