package com.hanjjak.raid.application

import com.hanjjak.raid.domain.RaidAttemptStatus
import org.springframework.scheduling.annotation.Scheduled
import java.time.Instant

/** Reads one bounded due page without locks, then processes each key account-first. */
open class RaidAttemptTerminalizer(
    private val repository: RaidRepository,
    private val completion: RaidAttemptCompletionService,
    private val clock: RaidClock,
    private val pageSize: Int = 100,
) {
    init { require(pageSize in 1..1_000) { "INVALID_LIMIT" } }

    @Scheduled(fixedDelay = 1_000)
    open fun terminalizeDue(): Int = terminalizeDue(clock.now())

    fun terminalizeDue(asOf: Instant): Int {
        val due = repository.dueAttempts(asOf, null, pageSize)
            .sortedWith(compareBy({ it.accountId }, { it.attemptId.value }))
        var terminalized = 0
        for (attempt in due) {
            if (attempt.resumePending) {
                completion.resumeAfterTerminalization(attempt)
                continue
            }
            completion.terminalizeOne(attempt.accountId, attempt.attemptId.value, asOf)?.let {
                completion.resumeAfterTerminalization(it)
                if (it.status == RaidAttemptStatus.RESULT_HELD) terminalized++
            }
        }
        return terminalized
    }
}
