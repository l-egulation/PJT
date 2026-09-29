package com.hanjjak.battle.domain

import com.fasterxml.jackson.annotation.JsonIgnore
import java.util.UUID

/** The counters emitted by a progression-rebalance backfill run. */
data class ProgressionBackfillReport(
    val targetAccounts: Int,
    val targetStages: Int,
    val alreadyApplied: Int,
    val riceTotal: Long,
    val quantitiesByItem: Map<String, Long>,
    val immediateCandidates: Int,
    val pendingCandidates: Int,
    val failedAccountIds: List<UUID>,
    val deferredAccountIds: List<UUID>,
    val switchedAccountCount: Int,
) {
    @get:JsonIgnore val targetAccountCount: Int get() = targetAccounts
    @get:JsonIgnore val targetStageCount: Int get() = targetStages
    @get:JsonIgnore val totalRice: Long get() = riceTotal
    @get:JsonIgnore val itemQuantities: Map<String, Long> get() = quantitiesByItem

    companion object {
        fun empty(targetAccounts: Int = 0, targetStages: Int = 40) = ProgressionBackfillReport(
            targetAccounts, targetStages, 0, 0, emptyMap(), 0, 0, emptyList(), emptyList(), 0,
        )
    }
}
