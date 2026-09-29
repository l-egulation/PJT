package com.hanjjak.raid.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.raid.domain.RaidAttempt
import com.hanjjak.raid.domain.RaidAttemptId
import com.hanjjak.raid.domain.RaidAttemptStatus
import com.hanjjak.raid.domain.RaidSlotStatus
import org.springframework.transaction.annotation.Propagation
import org.springframework.transaction.annotation.Transactional
import java.time.Instant
import java.util.UUID

/** Owns one due-attempt transaction and the separate optional main-battle resume transaction. */
open class RaidAttemptCompletionService(
    private val repository: RaidRepository,
    private val mainBattle: RaidMainBattlePort,
    private val mapper: ObjectMapper,
) {
    @Transactional(propagation = Propagation.REQUIRES_NEW)
    open fun terminalizeOne(accountId: UUID, attemptId: UUID, asOf: Instant): RaidAttempt? {
        repository.lockAccount(accountId)
        val candidate = repository.currentAttempt(accountId) ?: return null
        if (candidate.attemptId.value != attemptId) return null
        val locked = repository.lockAttempt(RaidAttemptId(attemptId), candidate.sessionId, accountId)
        if (locked.status != RaidAttemptStatus.RUNNING || asOf.isBefore(locked.completableAt)) return null
        RaidAttemptTransitions.requireAllowed(locked.status, RaidAttemptAction.TERMINALIZE)
        val stored = mapper.readValue(locked.inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        val ended = locked.copy(status = RaidAttemptStatus.RESULT_HELD, endedAt = locked.completableAt, resumePending = stored.pausedBattle != null)
        repository.saveAttempt(ended)
        locked.slotId?.let { slotId ->
            repository.slots(locked.sessionId, accountId)
                .firstOrNull { it.slotId == slotId && it.status == RaidSlotStatus.ACTIVE }
                ?.let { repository.saveSlot(it.copy(status = RaidSlotStatus.RESULT_HELD)) }
        }
        return ended
    }

    @Transactional(propagation = Propagation.REQUIRES_NEW)
    open fun resumeAfterTerminalization(attempt: RaidAttempt) {
        repository.lockAccount(attempt.accountId)
        val locked = repository.lockAttempt(attempt.attemptId, attempt.sessionId, attempt.accountId)
        if (!locked.resumePending) return
        val stored = mapper.readValue(locked.inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        val handoff = stored.pausedBattle
        if (handoff != null) mainBattle.resumeLocked(locked.accountId, locked.attemptId.value, handoff)
        repository.clearResumePending(locked.attemptId, locked.sessionId, locked.accountId)
    }
}
