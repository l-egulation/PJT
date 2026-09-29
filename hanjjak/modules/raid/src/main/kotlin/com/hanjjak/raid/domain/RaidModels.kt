package com.hanjjak.raid.domain

import java.time.Instant
import java.util.UUID

@JvmInline
value class RaidSessionId(val value: UUID)

@JvmInline
value class RaidAttemptId(val value: UUID)

@JvmInline
value class RaidClaimId(val value: UUID)

enum class RaidSessionStatus { OPEN, SETTLING, SETTLED_SUCCESS, SETTLED_FAILURE }
enum class RaidSettlementPhase { ROTATE_SESSION, FREEZE_WORKSET, FINALIZE_ACCOUNTS, MATERIALIZE_RANKS, SETTLED }
enum class RaidSlotStatus { AVAILABLE, ACTIVE, RESULT_HELD, CONFIRMED, DISCARDED, EXPIRED }
enum class RaidAttemptStatus { RUNNING, RESULT_HELD, DISCARDED, CONFIRMED }
enum class RaidAttemptMode { REWARD, PRACTICE }
enum class RaidClaimKind { AUTO_PERSONAL, DAILY_RANK }
enum class RaidClaimStatus { CLAIMABLE, CLAIMED }
enum class RaidGrade { PARTICIPATION, D, C, B, A, S, SS, SSS }

data class RaidSession(
    val sessionId: RaidSessionId,
    val contentVersion: String,
    val rewardVersion: String,
    val settlesAt: Instant,
    val status: RaidSessionStatus,
    val cutoffAt: Instant? = null,
    val batchCursor: UUID? = null,
    val settlementAccountCount: Long = 0,
    val finalizedAccountCount: Long = 0,
    val settlementPhase: RaidSettlementPhase = RaidSettlementPhase.ROTATE_SESSION,
    val completedAt: Instant? = null,
) {
    init {
        require(contentVersion.isNotBlank())
        require(rewardVersion.isNotBlank())
        require(status == RaidSessionStatus.OPEN && cutoffAt == null || status != RaidSessionStatus.OPEN && cutoffAt != null)
        require(cutoffAt == null || cutoffAt <= settlesAt)
        require(settlementAccountCount >= 0)
        require(finalizedAccountCount in 0..settlementAccountCount)
        require(status != RaidSessionStatus.OPEN || settlementPhase == RaidSettlementPhase.ROTATE_SESSION)
        require(completedAt == null || status in setOf(RaidSessionStatus.SETTLED_SUCCESS, RaidSessionStatus.SETTLED_FAILURE))
    }
}

data class RaidAccountState(
    val sessionId: RaidSessionId,
    val accountId: UUID,
    val currentSlotOrdinal: Int,
    val slotsTerminal: Int,
    val rewardAttemptsStarted: Int,
    val stateVersion: Long,
    val autoFinalized: Boolean,
) {
    init {
        require(currentSlotOrdinal in 1..3)
        require(slotsTerminal in 0..3)
        require(slotsTerminal <= currentSlotOrdinal)
        require(rewardAttemptsStarted >= 0)
        require(stateVersion >= 1)
    }
}

data class RaidAttempt(
    val attemptId: RaidAttemptId,
    val sessionId: RaidSessionId,
    val accountId: UUID,
    val mode: RaidAttemptMode,
    val slotId: UUID?,
    val attemptOrdinal: Int,
    val status: RaidAttemptStatus,
    val inputSnapshot: String,
    val result: String,
    val timeline: String,
    val startedAt: Instant,
    val completableAt: Instant,
    val endedAt: Instant? = null,
    val seed: Long = 0L,
    val resumePending: Boolean = false,
) {
    init {
        require(if (mode == RaidAttemptMode.REWARD) attemptOrdinal in 1..3 else attemptOrdinal >= 1)
        require(mode == RaidAttemptMode.PRACTICE || slotId != null)
        require(mode == RaidAttemptMode.REWARD || slotId == null)
        require(inputSnapshot.isNotBlank())
        require(result.isNotBlank())
        require(timeline.isNotBlank())
    }
}

data class RaidConfirmedResult(
    val resultId: UUID,
    val attemptId: RaidAttemptId,
    val sessionId: RaidSessionId,
    val accountId: UUID,
    val damage: Long,
    val grade: RaidGrade,
    val sealContribution: Long,
    val rewardVersion: String,
    val confirmedAt: Instant,
    val rewardBundle: RaidRewardBundle,
    val resultJson: String,
) {
    init {
        require(damage >= 0)
        require(sealContribution >= 0)
        require(rewardVersion.isNotBlank())
        require(resultJson.isNotBlank())
    }
}

data class RaidSettlementAccount(
    val sessionId: RaidSessionId,
    val accountId: UUID,
    val status: RaidSettlementAccountStatus,
    val createdAt: Instant,
    val cutoffAt: Instant,
    val finalizedAt: Instant? = null,
)

enum class RaidSettlementAccountStatus { PENDING, FINALIZED }

enum class RaidCommandStatus { SUCCEEDED, FAILED, IN_PROGRESS }

data class RaidCommandRecord(
    val commandId: UUID,
    val accountId: UUID,
    val idempotencyKey: UUID,
    val fingerprint: String,
    val resultJson: String?,
    val status: RaidCommandStatus,
    val createdAt: Instant,
    val expiresAt: Instant?,
)

data class RaidClaim(
    val claimId: RaidClaimId,
    val sessionId: RaidSessionId,
    val accountId: UUID,
    val kind: RaidClaimKind,
    val sourceId: UUID,
    val status: RaidClaimStatus,
    val rewardVersion: String,
    val bundle: RaidRewardBundle,
    val claimableAt: Instant,
    val claimedAt: Instant? = null,
)

data class RaidContribution(
    val sessionId: RaidSessionId,
    val accountId: UUID,
    val sealContribution: Long,
    val damage: Long,
    val confirmedAttempts: Int,
    val highestDamage: Long,
)

data class RaidFinalRank(
    val sessionId: RaidSessionId,
    val accountId: UUID,
    val competitiveRank: Int,
    val sealContribution: Long,
    val totalDamage: Long,
    val highestDamage: Long,
)

data class RaidRewardSlot(
    val sessionId: RaidSessionId,
    val accountId: UUID,
    val ordinal: Int,
    val status: RaidSlotStatus,
    val attemptsStarted: Int,
    val currentAttemptId: RaidAttemptId? = null,
    val terminalAt: Instant? = null,
    val slotId: UUID = UUID.nameUUIDFromBytes("raid-slot:$sessionId:$accountId:$ordinal".toByteArray()),
) {
    init {
        require(ordinal in 1..3)
        require(attemptsStarted in 0..3)
        require((status in setOf(RaidSlotStatus.CONFIRMED, RaidSlotStatus.DISCARDED, RaidSlotStatus.EXPIRED)) == (terminalAt != null))
    }
}

data class RaidRewardBundle(
    val cosmeticTickets: Long,
    val gemBoxes: Long,
    val rice: Long,
) {
    init {
        require(cosmeticTickets >= 0)
        require(gemBoxes >= 0)
        require(rice >= 0)
    }
}

data class RaidAttemptResult(
    val damage: Long,
    val grade: RaidGrade,
    val sealContribution: Int,
    val endedAt: Instant,
) {
    init {
        require(damage >= 0)
        require(sealContribution >= 0)
        require(if (grade == RaidGrade.PARTICIPATION) sealContribution == 0 else damage > 0 && sealContribution > 0)
    }
}

/** Ordering cursor for the immutable competitive leaderboard. */
data class RaidRankCursor(
    val sealContribution: Long,
    val totalDamage: Long,
    val highestDamage: Long,
    val accountId: UUID,
) {
    fun encode(): String = listOf(sealContribution, totalDamage, highestDamage, accountId).joinToString(":")

    companion object {
        fun from(row: RaidRankingRow, ranks: List<RaidFinalRank>): RaidRankCursor {
            val rank = ranks.firstOrNull { it.accountId == row.accountId } ?: throw IllegalArgumentException("RANK_NOT_FOUND")
            return RaidRankCursor(rank.sealContribution, rank.totalDamage, rank.highestDamage, rank.accountId)
        }

        fun decode(value: String?): RaidRankCursor? {
            if (value.isNullOrBlank()) return null
            val parts = value.split(":")
            require(parts.size == 4) { "INVALID_RANK_CURSOR" }
            return runCatching { RaidRankCursor(parts[0].toLong(), parts[1].toLong(), parts[2].toLong(), UUID.fromString(parts[3])) }
                .getOrElse { throw IllegalArgumentException("INVALID_RANK_CURSOR", it) }
        }
    }
}

data class RaidRankingRow(
    val accountId: UUID,
    val competitiveRank: Int,
    val sealContribution: Long,
)

data class RaidRankingView(
    val items: List<RaidRankingRow>,
    val caller: RaidRankingRow?,
    val nextCursor: RaidRankCursor?,
)
