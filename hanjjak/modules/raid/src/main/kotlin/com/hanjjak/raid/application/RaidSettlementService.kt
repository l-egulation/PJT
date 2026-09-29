package com.hanjjak.raid.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.raid.domain.RaidAccountState
import com.hanjjak.raid.domain.RaidAttempt
import com.hanjjak.raid.domain.RaidAttemptMode
import com.hanjjak.raid.domain.RaidAttemptResult
import com.hanjjak.raid.domain.RaidAttemptStatus
import com.hanjjak.raid.domain.RaidClaim
import com.hanjjak.raid.domain.RaidClaimId
import com.hanjjak.raid.domain.RaidClaimKind
import com.hanjjak.raid.domain.RaidClaimStatus
import com.hanjjak.raid.domain.RaidConfirmedResult
import com.hanjjak.raid.domain.RaidContribution
import com.hanjjak.raid.domain.RaidFinalRank
import com.hanjjak.raid.domain.RaidGrade
import com.hanjjak.raid.domain.RaidRankCursor
import com.hanjjak.raid.domain.RaidRankingRow
import com.hanjjak.raid.domain.RaidRankingView
import com.hanjjak.raid.domain.RaidRewardSlot
import com.hanjjak.raid.domain.RaidRules
import com.hanjjak.raid.domain.RaidSession
import com.hanjjak.raid.domain.RaidSessionId
import com.hanjjak.raid.domain.RaidSessionStatus
import com.hanjjak.raid.domain.RaidSettlementAccountStatus
import com.hanjjak.raid.domain.RaidSettlementPhase
import com.hanjjak.raid.domain.RaidSlotStatus
import org.springframework.transaction.annotation.Propagation
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.time.Instant
import java.time.ZoneId
import java.time.temporal.ChronoUnit
import java.util.UUID

/** A short transaction that fences due sessions before opening the current KST interval. */
open class RaidSessionRotationService(
    private val repository: RaidRepository,
    private val contentPort: RaidContentPort,
    private val pageSize: Int = 100,
) {
    init { require(pageSize in 1..1_000) { "INVALID_LIMIT" } }

    @Transactional
    open fun rotateSession(now: Instant): RaidSession {
        repository.dueOpenSessions(now, pageSize).forEach { open ->
            repository.saveSession(
                open.copy(
                    status = RaidSessionStatus.SETTLING,
                    cutoffAt = open.settlesAt,
                    settlementPhase = RaidSettlementPhase.FREEZE_WORKSET,
                ),
            )
        }
        val boundary = RaidSettlementService.settlementAt(now)
        repository.findSessionBySettlesAt(boundary)?.let { return it }
        val content = contentPort.current()
        val session = RaidSession(
            RaidSessionId(UUID.nameUUIDFromBytes("raid-session:$boundary".toByteArray(StandardCharsets.UTF_8))),
            content.contentVersion,
            content.rewardVersion,
            boundary,
            RaidSessionStatus.OPEN,
        )
        repository.saveSession(session)
        return repository.findSessionBySettlesAt(boundary) ?: error("RAID_SESSION_ROTATION_FAILED")
    }
}

/** Phase transitions that own only the settlement session row. */
open class RaidSettlementPhaseService(private val repository: RaidRepository) {
    @Transactional
    open fun freezeWorkset(sessionId: RaidSessionId) {
        val session = repository.lockSession(sessionId)
        if (session.status != RaidSessionStatus.SETTLING || session.settlementPhase > RaidSettlementPhase.FREEZE_WORKSET) return
        val cutoff = requireNotNull(session.cutoffAt)
        repository.freezeSettlementWorkset(sessionId, cutoff)
        val count = repository.settlementAccountCount(sessionId)
        repository.saveSession(
            session.copy(
                settlementPhase = RaidSettlementPhase.FINALIZE_ACCOUNTS,
                settlementAccountCount = count,
                finalizedAccountCount = repository.finalizedSettlementAccountCount(sessionId),
            ),
        )
    }

    @Transactional
    open fun advanceToRanks(sessionId: RaidSessionId): Boolean {
        val session = repository.lockSession(sessionId)
        if (session.status != RaidSessionStatus.SETTLING) return false
        if (session.settlementPhase > RaidSettlementPhase.FINALIZE_ACCOUNTS) return true
        if (session.settlementPhase != RaidSettlementPhase.FINALIZE_ACCOUNTS) return false
        val expected = repository.settlementAccountCount(sessionId)
        val finalized = repository.finalizedSettlementAccountCount(sessionId)
        if (expected != finalized) return false
        repository.saveSession(
            session.copy(
                settlementPhase = RaidSettlementPhase.MATERIALIZE_RANKS,
                settlementAccountCount = expected,
                finalizedAccountCount = finalized,
            ),
        )
        return true
    }
}

/** Finalizes one frozen account using the global account-first lock order. */
open class RaidSettlementAccountService(
    private val repository: RaidRepository,
    private val rewards: RaidRewardPort,
    private val mainBattle: RaidMainBattlePort,
    private val events: DomainEventPublisher,
    private val mapper: ObjectMapper,
) {
    @Transactional(propagation = Propagation.REQUIRES_NEW)
    open fun finalizeAccount(sessionId: RaidSessionId, accountId: UUID, now: Instant) {
        val session = repository.session(sessionId) ?: throw IllegalArgumentException("RAID_SESSION_NOT_FOUND")
        require(session.status == RaidSessionStatus.SETTLING) { "RAID_SESSION_NOT_SETTLING" }
        require(session.settlementPhase == RaidSettlementPhase.FINALIZE_ACCOUNTS) { "RAID_SETTLEMENT_PHASE_CONFLICT" }
        val cutoff = requireNotNull(session.cutoffAt)

        repository.lockAccount(accountId)
        val work = repository.lockSettlementAccount(sessionId, accountId)
        if (work.status == RaidSettlementAccountStatus.FINALIZED) return
        require(work.cutoffAt == cutoff) { "RAID_SETTLEMENT_CUTOFF_CONFLICT" }

        val state = repository.accountState(sessionId, accountId)?.let { repository.lockAccountState(sessionId, accountId) }
        if (state != null) finalizeRaidState(session, state, cutoff, now)
        require(repository.finalizeSettlementAccount(sessionId, accountId, now)) { "RAID_SETTLEMENT_ACCOUNT_CONFLICT" }
    }

    private fun finalizeRaidState(session: RaidSession, state: RaidAccountState, cutoff: Instant, now: Instant) {
        val slots = repository.slots(session.sessionId, state.accountId)
            .sortedBy(RaidRewardSlot::ordinal)
            .map { repository.lockSlot(session.sessionId, state.accountId, it.ordinal) }
        require(slots.size == 3) { "RAID_SETTLEMENT_SLOT_COUNT_CONFLICT" }
        val attempts = repository.settlementAttempts(session.sessionId, state.accountId)
        val activeAttempts = attempts
            .filter { it.status in setOf(RaidAttemptStatus.RUNNING, RaidAttemptStatus.RESULT_HELD) }
            .sortedWith(compareBy<RaidAttempt> { it.startedAt }.thenBy { it.attemptId.value })
        val eligibleRewardAttempts = activeAttempts.filter { it.mode == RaidAttemptMode.REWARD && !it.startedAt.isAfter(cutoff) }
        require(eligibleRewardAttempts.size <= 1) { "RAID_SETTLEMENT_MULTIPLE_REWARD_ATTEMPTS" }
        val rewardAttempt = eligibleRewardAttempts.singleOrNull()

        var autoConfirmed = false
        if (rewardAttempt != null) {
            val slot = slots.singleOrNull { it.slotId == rewardAttempt.slotId }
                ?: throw IllegalArgumentException("RAID_SETTLEMENT_SLOT_NOT_FOUND")
            require(slot.currentAttemptId == rewardAttempt.attemptId) { "RAID_SETTLEMENT_ATTEMPT_NOT_CURRENT" }
            val result = persistedResult(rewardAttempt, cutoff)
            if (result.damage > 0) {
                autoConfirm(session, rewardAttempt, result, slot, now)
                autoConfirmed = true
            } else {
                discardAttempt(rewardAttempt, cutoff)
            }
        }
        activeAttempts.filter { it != rewardAttempt }.forEach { discardAttempt(it, cutoff) }

        slots.forEach { slot ->
            val refreshed = repository.lockSlot(session.sessionId, state.accountId, slot.ordinal)
            if (refreshed.status in setOf(RaidSlotStatus.AVAILABLE, RaidSlotStatus.ACTIVE, RaidSlotStatus.RESULT_HELD)) {
                repository.saveSlot(refreshed.copy(status = RaidSlotStatus.EXPIRED, currentAttemptId = null, terminalAt = cutoff))
            }
        }
        repository.saveAccountState(
            state.copy(
                currentSlotOrdinal = 3,
                slotsTerminal = 3,
                autoFinalized = true,
                stateVersion = state.stateVersion + 1,
            ),
        )
        repository.incrementAccountStateVersion(state.accountId)
        events.publish(
            "RAID_ACCOUNT_AUTO_FINALIZED",
            session.sessionId.value,
            now,
            accountId = state.accountId,
            payload = mapOf("autoConfirmed" to autoConfirmed, "cutoffAt" to cutoff),
        )
    }

    private fun discardAttempt(attempt: RaidAttempt, cutoff: Instant) {
        resumeIfNeeded(attempt)
        repository.saveAttempt(
            attempt.copy(
                status = RaidAttemptStatus.DISCARDED,
                endedAt = attempt.endedAt ?: minOf(cutoff, attempt.completableAt),
                resumePending = false,
            ),
        )
    }

    private fun autoConfirm(
        session: RaidSession,
        attempt: RaidAttempt,
        result: RaidAttemptResult,
        slot: RaidRewardSlot,
        now: Instant,
    ) {
        val bundle = RaidRules.personalReward(result.grade)
        val confirmed = RaidConfirmedResult(
            UUID.nameUUIDFromBytes("raid-auto-result:${attempt.attemptId.value}".toByteArray(StandardCharsets.UTF_8)),
            attempt.attemptId,
            session.sessionId,
            attempt.accountId,
            result.damage,
            result.grade,
            result.sealContribution.toLong(),
            session.rewardVersion,
            now,
            bundle,
            mapper.writeValueAsString(
                mapOf(
                    "damage" to result.damage,
                    "grade" to result.grade.name,
                    "sealContribution" to result.sealContribution,
                    "endedAt" to result.endedAt,
                    "reward" to bundle,
                    "automatic" to true,
                ),
            ),
        )
        val contribution = repository.lockContribution(session.sessionId, attempt.accountId)
        Math.addExact(contribution.sealContribution, confirmed.sealContribution)
        Math.addExact(contribution.damage, confirmed.damage)
        Math.addExact(contribution.confirmedAttempts.toLong(), 1L)
        val immediate = canCreditImmediately(attempt.accountId, bundle)

        repository.appendConfirmedResult(confirmed)
        if (immediate) {
            rewards.creditLocked(attempt.accountId, attempt.attemptId.value, bundle)
        } else {
            repository.insertClaim(
                RaidClaim(
                    RaidClaimId(UUID.nameUUIDFromBytes("raid-auto-claim:${attempt.attemptId.value}".toByteArray(StandardCharsets.UTF_8))),
                    session.sessionId,
                    attempt.accountId,
                    RaidClaimKind.AUTO_PERSONAL,
                    confirmed.resultId,
                    RaidClaimStatus.CLAIMABLE,
                    session.rewardVersion,
                    bundle,
                    now,
                ),
            )
        }
        resumeIfNeeded(attempt)
        repository.saveAttempt(attempt.copy(status = RaidAttemptStatus.CONFIRMED, endedAt = attempt.endedAt ?: result.endedAt, resumePending = false))
        repository.saveSlot(slot.copy(status = RaidSlotStatus.CONFIRMED, currentAttemptId = null, terminalAt = now))
        repository.upsertContribution(RaidContribution(session.sessionId, attempt.accountId, confirmed.sealContribution, confirmed.damage, 1, confirmed.damage))
        events.publish(
            "RAID_ATTEMPT_CONFIRMED",
            attempt.attemptId.value,
            now,
            accountId = attempt.accountId,
            payload = mapOf("sessionId" to session.sessionId.value, "automatic" to true, "claimCreated" to !immediate),
        )
    }

    private fun canCreditImmediately(accountId: UUID, bundle: com.hanjjak.raid.domain.RaidRewardBundle): Boolean = try {
        rewards.preflightLocked(accountId, bundle)
        true
    } catch (_: ArithmeticException) {
        false
    } catch (error: IllegalArgumentException) {
        if (error.message == "INVENTORY_CAPACITY_EXCEEDED") false else throw error
    }

    private fun resumeIfNeeded(attempt: RaidAttempt) {
        if (attempt.status != RaidAttemptStatus.RUNNING && !attempt.resumePending) return
        val stored = mapper.readValue(attempt.inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        stored.pausedBattle?.let { mainBattle.resumeLocked(attempt.accountId, attempt.attemptId.value, it) }
    }

    private fun persistedResult(attempt: RaidAttempt, cutoff: Instant): RaidAttemptResult {
        val stored = mapper.readValue(attempt.inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        val node = mapper.readTree(attempt.result)
        val prefixAtCutoff = attempt.status == RaidAttemptStatus.RUNNING || attempt.endedAt?.isAfter(cutoff) == true
        val damage = if (prefixAtCutoff) {
            val elapsedTick = ChronoUnit.MILLIS.between(attempt.startedAt, minOf(cutoff, attempt.completableAt))
                .coerceAtLeast(0) / stored.boss.tickDurationMilliseconds
            mapper.readTree(attempt.timeline)
                .filter { it.path("type").asText() == "PLAYER_IMPACT" && it.path("logicalTick").asLong() <= elapsedTick }
                .fold(0L) { total, event -> Math.addExact(total, event.path("damage").asLong()) }
        } else {
            node.path("damage").asLong()
        }
        val grade = RaidRules.grade(damage, stored.gradeDamageThresholds)
        return RaidAttemptResult(
            damage,
            grade,
            RaidRules.contribution(grade, stored.sealContributions).toInt(),
            if (prefixAtCutoff) cutoff else attempt.endedAt ?: attempt.completableAt,
        )
    }
}

/** Materializes immutable ranks and one daily claim per eligible account. */
open class RaidSettlementRankService(private val repository: RaidRepository) {
    @Transactional
    open fun materializeRanks(sessionId: RaidSessionId): List<RaidFinalRank> {
        val session = repository.lockSession(sessionId)
        if (session.status in setOf(RaidSessionStatus.SETTLED_SUCCESS, RaidSessionStatus.SETTLED_FAILURE)) {
            return repository.finalRanks(sessionId)
        }
        require(session.status == RaidSessionStatus.SETTLING) { "RAID_SESSION_NOT_SETTLING" }
        require(session.settlementPhase == RaidSettlementPhase.MATERIALIZE_RANKS) { "RAID_SETTLEMENT_PHASE_CONFLICT" }
        require(repository.finalizedSettlementAccountCount(sessionId) == repository.settlementAccountCount(sessionId)) {
            "RAID_SETTLEMENT_ACCOUNTS_PENDING"
        }
        val eligible = repository.contributions(sessionId).filter { it.sealContribution > 0 }
        val success = eligible.sumOf { it.sealContribution } >= RaidSettlementService.SEAL_TARGET
        val ranks = RaidSettlementService.computeRanks(sessionId, eligible)
        ranks.forEach { rank ->
            repository.saveFinalRank(rank)
            repository.insertClaim(
                RaidClaim(
                    RaidClaimId(UUID.nameUUIDFromBytes("raid-rank-claim:${sessionId.value}:${rank.accountId}".toByteArray(StandardCharsets.UTF_8))),
                    sessionId,
                    rank.accountId,
                    RaidClaimKind.DAILY_RANK,
                    sessionId.value,
                    RaidClaimStatus.CLAIMABLE,
                    session.rewardVersion,
                    RaidRules.rankReward(success, rank.competitiveRank),
                    session.settlesAt,
                ),
            )
        }
        require(repository.rankCount(sessionId) == eligible.size.toLong()) { "RAID_RANK_COMPLETENESS_FAILED" }
        require(repository.dailyRankClaimCount(sessionId) == eligible.size.toLong()) { "RAID_RANK_CLAIM_COMPLETENESS_FAILED" }
        return repository.finalRanks(sessionId)
    }
}

/** Final short transaction; never repeats account work or combat. */
open class RaidSettlementCompletionService(private val repository: RaidRepository) {
    @Transactional
    open fun complete(sessionId: RaidSessionId, now: Instant) {
        val session = repository.lockSession(sessionId)
        if (session.status != RaidSessionStatus.SETTLING) return
        require(session.settlementPhase == RaidSettlementPhase.MATERIALIZE_RANKS) { "RAID_SETTLEMENT_PHASE_CONFLICT" }
        require(repository.finalizedSettlementAccountCount(sessionId) == repository.settlementAccountCount(sessionId)) {
            "RAID_SETTLEMENT_ACCOUNTS_PENDING"
        }
        val eligible = repository.contributions(sessionId).count { it.sealContribution > 0 }.toLong()
        require(repository.rankCount(sessionId) == eligible) { "RAID_RANK_COMPLETENESS_FAILED" }
        require(repository.dailyRankClaimCount(sessionId) == eligible) { "RAID_RANK_CLAIM_COMPLETENESS_FAILED" }
        val total = repository.contributions(sessionId).sumOf { it.sealContribution }
        repository.saveSession(
            session.copy(
                status = if (total >= RaidSettlementService.SEAL_TARGET) RaidSessionStatus.SETTLED_SUCCESS else RaidSessionStatus.SETTLED_FAILURE,
                settlementPhase = RaidSettlementPhase.SETTLED,
                completedAt = now,
                finalizedAccountCount = repository.finalizedSettlementAccountCount(sessionId),
            ),
        )
    }
}

/** Non-transactional coordinator; every collaborator call crosses a Spring proxy boundary. */
open class RaidSettlementService(
    private val repository: RaidRepository,
    private val clock: RaidClock,
    private val rotation: RaidSessionRotationService,
    private val phases: RaidSettlementPhaseService,
    private val accounts: RaidSettlementAccountService,
    private val ranks: RaidSettlementRankService,
    private val completion: RaidSettlementCompletionService,
    private val pageSize: Int = 100,
) {
    init { require(pageSize in 1..1_000) { "INVALID_LIMIT" } }

    fun rotateSession(now: Instant = clock.now()): RaidSession = rotation.rotateSession(now)

    fun settleDueSessions(now: Instant = clock.now()): List<RaidSessionId> {
        rotation.rotateSession(now)
        val settling = repository.settlingSessions(now)
        settling.forEach { settleOne(it.sessionId, now) }
        return settling.map(RaidSession::sessionId)
    }

    fun settleOne(sessionId: RaidSessionId, now: Instant = clock.now()) {
        var session = repository.session(sessionId) ?: return
        if (session.status != RaidSessionStatus.SETTLING) return
        if (session.settlementPhase <= RaidSettlementPhase.FREEZE_WORKSET) phases.freezeWorkset(sessionId)
        session = repository.session(sessionId) ?: return
        if (session.settlementPhase == RaidSettlementPhase.FINALIZE_ACCOUNTS) {
            val expectedAccounts = repository.settlementAccountCount(sessionId)
            var pageReads = 0L
            while (true) {
                require(pageReads <= expectedAccounts) { "RAID_SETTLEMENT_ACCOUNT_DRAIN_LIMIT" }
                val finalizedBefore = repository.finalizedSettlementAccountCount(sessionId)
                val page = repository.settlementAccounts(sessionId, null, pageSize).sorted()
                pageReads++
                if (page.isEmpty()) break
                page.forEach { accountId -> accounts.finalizeAccount(sessionId, accountId, now) }
                require(repository.finalizedSettlementAccountCount(sessionId) > finalizedBefore) {
                    "RAID_SETTLEMENT_ACCOUNT_DRAIN_STALLED"
                }
            }
            if (!phases.advanceToRanks(sessionId)) return
        }
        session = repository.session(sessionId) ?: return
        if (session.settlementPhase == RaidSettlementPhase.MATERIALIZE_RANKS) {
            ranks.materializeRanks(sessionId)
            completion.complete(sessionId, now)
        }
    }

    fun ranking(accountId: UUID, sessionId: UUID, cursor: RaidRankCursor?, limit: Int): RaidRankingView =
        publicRanks(repository.finalRanks(RaidSessionId(sessionId)), accountId, cursor, limit)

    companion object {
        const val SEAL_TARGET: Long = 50_000
        private val ZONE: ZoneId = ZoneId.of("Asia/Seoul")

        fun settlementAt(now: Instant): Instant {
            val local = now.atZone(ZONE)
            val today = local.toLocalDate().atTime(17, 30).atZone(ZONE)
            return if (now.isBefore(today.toInstant())) today.toInstant() else today.plusDays(1).toInstant()
        }

        fun nextSettlementAt(settlement: Instant): Instant =
            settlement.atZone(ZONE).toLocalDate().plusDays(1).atTime(17, 30).atZone(ZONE).toInstant()

        fun computeRanks(sessionId: RaidSessionId, contributions: List<RaidContribution>): List<RaidFinalRank> {
            var rank = 0
            var previous: Triple<Long, Long, Long>? = null
            return contributions
                .filter { it.sealContribution > 0 }
                .sortedWith(compareByDescending<RaidContribution> { it.sealContribution }.thenByDescending { it.damage }.thenByDescending { it.highestDamage }.thenBy { it.accountId })
                .mapIndexed { index, contribution ->
                    val key = Triple(contribution.sealContribution, contribution.damage, contribution.highestDamage)
                    if (key != previous) rank = index + 1
                    previous = key
                    RaidFinalRank(sessionId, contribution.accountId, rank, contribution.sealContribution, contribution.damage, contribution.highestDamage)
                }
        }

        fun publicRanks(
            ranks: List<RaidFinalRank>,
            callerAccountId: UUID? = null,
            cursor: RaidRankCursor? = null,
            limit: Int = 100,
        ): RaidRankingView {
            require(limit in 1..1_000) { "INVALID_LIMIT" }
            val ordered = ranks.sortedWith(compareBy<RaidFinalRank> { it.competitiveRank }.thenBy { it.accountId })
            val visible = ordered.filter { it.competitiveRank <= 100 }
            val start = cursor?.let { expected ->
                val index = visible.indexOfFirst { RaidRankCursor(it.sealContribution, it.totalDamage, it.highestDamage, it.accountId) == expected }
                require(index >= 0) { "INVALID_RANK_CURSOR" }
                index + 1
            } ?: 0
            val remaining = visible.drop(start)
            val boundaryRank = remaining.take(limit).lastOrNull()?.competitiveRank
            val page = if (boundaryRank == null) emptyList() else remaining.withIndex()
                .takeWhile { (index, row) -> index < limit || row.competitiveRank == boundaryRank }
                .map { it.value }
            val next = page.lastOrNull()?.takeIf { start + page.size < visible.size }?.let {
                RaidRankCursor(it.sealContribution, it.totalDamage, it.highestDamage, it.accountId)
            }
            fun row(rank: RaidFinalRank) = RaidRankingRow(rank.accountId, rank.competitiveRank, rank.sealContribution)
            return RaidRankingView(page.map(::row), ranks.firstOrNull { it.accountId == callerAccountId }?.let(::row), next)
        }
    }
}