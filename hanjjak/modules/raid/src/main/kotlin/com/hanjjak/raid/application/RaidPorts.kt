package com.hanjjak.raid.application

import com.hanjjak.raid.domain.RaidAccountState
import com.hanjjak.raid.domain.RaidAttempt
import com.hanjjak.raid.domain.RaidAttemptId
import com.hanjjak.raid.domain.RaidAttemptResult
import com.hanjjak.raid.domain.RaidClaim
import com.hanjjak.raid.domain.RaidClaimId
import com.hanjjak.raid.domain.RaidCommandRecord
import com.hanjjak.raid.domain.RaidCommandStatus
import com.hanjjak.raid.domain.RaidConfirmedResult
import com.hanjjak.raid.domain.RaidContribution
import com.hanjjak.raid.domain.RaidFinalRank
import com.hanjjak.raid.domain.RaidRewardBundle
import com.hanjjak.raid.domain.RaidRewardSlot
import com.hanjjak.raid.domain.RaidSession
import com.hanjjak.raid.domain.RaidSessionId
import com.hanjjak.raid.domain.RaidSettlementAccount
import com.hanjjak.raid.domain.RaidSettlementPhase
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import java.time.Instant
import java.util.UUID

fun interface RaidClock {
    fun now(): Instant
}

data class RaidBattleHandoff(
    val battleSessionId: UUID,
    val gameSessionId: UUID,
    val stageId: String,
)

data class RaidCombatSnapshot(
    val player: FighterStats,
    val skills: SkillProfile,
    val contentVersion: String,
    val level: Int = 1,
    val defense: Int = 0,
    val skillIds: List<String> = skills.activeOrder,
    val activeSkillLoadout: List<String> = skills.activeOrder,
    val mainGemPreset: String = "MAIN",
    val cosmeticEffectIds: List<String> = emptyList(),
)
fun interface RaidCombatSnapshotProvider {
    fun snapshot(accountId: UUID): RaidCombatSnapshot
}

data class RaidContentSnapshot(
    val contentVersion: String,
    val rewardVersion: String,
    val unlockStageId: String,
    val sealTarget: Long = 50_000,
    val bossId: String = "daily-seal-boss",
    val bossDisplayName: String = "공동 봉인 보스",
    val bossInitialAttack: Int,
    val bossInitialDefense: Int,
    val maxTicks: Int = 3_000,
    val tickDurationMilliseconds: Int = 100,
    val escalationIntervalTicks: Int = 50,
    val escalationBasisPoints: Int = 500,
    val gradeDamageThresholds: Map<String, Long> = emptyMap(),
    val sealContributions: Map<String, Int> = emptyMap(),
    /** Only applied content is eligible for explicit raid activation. */
    val authority: String = "working",
)

fun interface RaidContentPort {
    fun current(): RaidContentSnapshot
    fun forVersion(contentVersion: String, rewardVersion: String): RaidContentSnapshot {
        val snapshot = current()
        require(snapshot.contentVersion == contentVersion && snapshot.rewardVersion == rewardVersion) { "RAID_CONTENT_VERSION_UNAVAILABLE" }
        return snapshot
    }
}

/** Read-only composition-root queries required to assemble complete raid API views. */
interface RaidQueryPort {
    fun stateVersion(accountId: UUID): Long
    fun sealedContribution(sessionId: RaidSessionId): Long
    fun ranking(accountId: UUID, session: RaidSession, cursor: String?, limit: Int): RaidRankingApiView
    fun claims(accountId: UUID, cursor: String?): RaidClaimsView

    object Empty : RaidQueryPort {
        override fun stateVersion(accountId: UUID): Long = 0L
        override fun sealedContribution(sessionId: RaidSessionId): Long = 0L
        override fun ranking(accountId: UUID, session: RaidSession, cursor: String?, limit: Int): RaidRankingApiView =
            RaidRankingApiView(session.sessionId.value, session.status != com.hanjjak.raid.domain.RaidSessionStatus.OPEN, null, 0L, emptyList(), null, 0L)
        override fun claims(accountId: UUID, cursor: String?): RaidClaimsView = RaidClaimsView(emptyList(), 0L, 0L, null)
    }

}
interface RaidRewardPort {
    /** Caller holds the account row and all raid aggregate locks. */
    fun preflightLocked(accountId: UUID, bundle: RaidRewardBundle)
    /** Caller holds the same locks and has completed [preflightLocked]. */
    fun creditLocked(accountId: UUID, sourceId: UUID, bundle: RaidRewardBundle)
}


interface RaidMainBattlePort {
    /** The caller has already acquired the account row lock. */
    fun pauseLockedWithHandoff(accountId: UUID, commandId: UUID): RaidBattleHandoff?
    /** Resumes only the exact cycle captured by [handoff]. */
    fun resumeLocked(accountId: UUID, commandId: UUID, handoff: RaidBattleHandoff): RaidBattleHandoff?
}

fun interface RaidProgressPort {
    fun hasFirstCleared(accountId: UUID, stageId: String): Boolean
}

interface RaidRepository {
    fun openSession(now: Instant): RaidSession?
    fun dueOpenSessions(asOf: Instant, limit: Int = 100): List<RaidSession>
    fun lockSession(sessionId: RaidSessionId): RaidSession
    fun lockAccount(accountId: UUID)
    fun lockAccountState(sessionId: RaidSessionId, accountId: UUID): RaidAccountState
    /** Non-locking lookup used only to initialize a new account/session aggregate. */
    fun accountState(sessionId: RaidSessionId, accountId: UUID): RaidAccountState? = null
    fun lockSlot(sessionId: RaidSessionId, accountId: UUID, ordinal: Int): RaidRewardSlot
    fun lockAttempt(attemptId: RaidAttemptId, sessionId: RaidSessionId, accountId: UUID): RaidAttempt
    fun lockClaim(claimId: RaidClaimId, accountId: UUID): RaidClaim
    fun slots(sessionId: RaidSessionId, accountId: UUID): List<RaidRewardSlot>
    fun attemptResult(attemptId: RaidAttemptId): RaidAttemptResult?
    fun currentAttempt(accountId: UUID): RaidAttempt?
    fun dueAttempts(asOf: Instant, after: UUID? = null, limit: Int = 100): List<RaidAttempt>
    fun clearResumePending(attemptId: RaidAttemptId, sessionId: RaidSessionId, accountId: UUID): Boolean
    fun settlementAccounts(sessionId: RaidSessionId, after: UUID? = null, limit: Int = 100): List<UUID>
    fun freezeSettlementWorkset(sessionId: RaidSessionId, cutoffAt: Instant): Int
    fun settlementAccountCount(sessionId: RaidSessionId): Long
    fun finalizedSettlementAccountCount(sessionId: RaidSessionId): Long
    fun lockSettlementAccount(sessionId: RaidSessionId, accountId: UUID): RaidSettlementAccount
    fun finalizeSettlementAccount(sessionId: RaidSessionId, accountId: UUID, finalizedAt: Instant): Boolean
    fun settlementAttempts(sessionId: RaidSessionId, accountId: UUID): List<RaidAttempt>
    fun contributions(sessionId: RaidSessionId): List<RaidContribution>
    fun finalRanks(sessionId: RaidSessionId): List<RaidFinalRank>
    fun rankCount(sessionId: RaidSessionId): Long
    fun dailyRankClaimCount(sessionId: RaidSessionId): Long
    fun findSessionBySettlesAt(settlesAt: Instant): RaidSession?
    fun session(sessionId: RaidSessionId): RaidSession?
    fun settlingSessions(asOf: Instant): List<RaidSession>
    fun saveSession(session: RaidSession)
    fun saveAccountState(state: RaidAccountState)
    fun saveSlot(slot: RaidRewardSlot)
    fun saveAttempt(attempt: RaidAttempt)
    fun appendConfirmedResult(result: RaidConfirmedResult)
    fun lockContribution(sessionId: RaidSessionId, accountId: UUID): RaidContribution
    fun upsertContribution(contribution: RaidContribution)
    fun saveFinalRank(rank: RaidFinalRank)
    fun insertClaim(claim: RaidClaim)
    fun markClaimed(claimId: RaidClaimId, accountId: UUID, claimedAt: Instant): Boolean
    fun claims(accountId: UUID): List<RaidClaim>
    fun claim(claimId: RaidClaimId, accountId: UUID): RaidClaim?
    fun incrementAccountStateVersion(accountId: UUID): Long
    fun claimCommand(accountId: UUID, idempotencyKey: UUID): RaidCommandRecord?
    fun recordCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String?, status: RaidCommandStatus, expiresAt: Instant? = null): Boolean
}
