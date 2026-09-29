package com.hanjjak.raid.application

import com.fasterxml.jackson.annotation.JsonInclude
import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.raid.domain.*
import com.hanjjak.sim.RaidCombatEventType
import com.hanjjak.sim.RaidCombatInput
import com.hanjjak.sim.RaidCombatResult
import com.hanjjak.sim.RaidSimulator
import com.hanjjak.sim.SkillProfile
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Duration
import java.time.Instant
import java.util.UUID

data class RaidConfirmationResult(
    val state: RaidStateView,
    val confirmedResult: RaidConfirmedResult,
) {
    val session get() = state.session
    val slots get() = state.slots
    val currentAttempt get() = state.currentAttempt
}

data class RaidRankingEntryView(val rank: Int, val nickname: String, val sealContribution: Long)

data class RaidRankingApiView(
    val sessionId: UUID,
    val settled: Boolean,
    val currentRank: Int?,
    val currentSealContribution: Long,
    val topEntries: List<RaidRankingEntryView>,
    val nextCursor: String?,
    val totalEligibleAccounts: Long,
)

data class RaidClaimView(
    val claimId: UUID,
    val sessionId: UUID,
    val kind: RaidClaimKind,
    val status: RaidClaimStatus,
    val reward: RaidRewardBundle,
    val createdAt: Instant,
    val claimedAt: Instant?,
)

data class RaidClaimsView(
    val items: List<RaidClaimView>,
    val claimableCount: Long,
    val claimedCount: Long,
    val nextCursor: String? = null,
)

data class RaidClaimResult(val claim: RaidClaimView, val state: RaidClaimsView)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidAttemptResultView(
    val damage: Long,
    val grade: RaidGrade,
    val sealContribution: Int,
    val endedAt: Instant?,
    val playerDied: Boolean,
    val timeLimitReached: Boolean,
    val reward: RaidRewardBundle? = null,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidCombatEventView(
    val sequence: Int,
    val logicalTick: Int,
    val type: String,
    val actor: String? = null,
    val target: String? = null,
    val damage: Int? = null,
    val critical: Boolean? = null,
    val hpBefore: Int? = null,
    val hpAfter: Int? = null,
    val escalationStage: Int? = null,
    val bossAttack: Int? = null,
    val bossDefense: Int? = null,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidRenderingTimeline(
    val tickDurationMilliseconds: Int,
    val durationMilliseconds: Long,
    val events: List<RaidCombatEventView>,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidBossSnapshotView(
    val bossId: String,
    val displayName: String,
    val contentVersion: String,
    val initialAttack: Int,
    val initialDefense: Int,
    val maxTicks: Int,
    val tickDurationMilliseconds: Int,
    val escalationIntervalTicks: Int,
    val escalationBasisPoints: Int,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidPlayerSnapshotView(
    val level: Int,
    val attack: Int,
    val maxHp: Int,
    val defense: Int,
    val defensePenetration: Int,
    val skillIds: List<String>,
    val skillLoadout: List<String>,
    val mainGemPreset: String,
    val cosmeticEffectIds: List<String>,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidCombatInputSnapshotView(
    val contentVersion: String,
    val rewardVersion: String,
    val seed: Long,
    val player: RaidPlayerSnapshotView,
    val boss: RaidBossSnapshotView,
    val capturedAt: Instant,
)

internal data class RaidStoredCombatInputSnapshot(
    val contentVersion: String,
    val rewardVersion: String,
    val seed: Long,
    val player: RaidPlayerSnapshotView,
    val boss: RaidBossSnapshotView,
    val skillProfile: SkillProfile,
    val gradeDamageThresholds: Map<String, Long>,
    val sealContributions: Map<String, Int>,
    val pausedBattle: RaidBattleHandoff?,
    val capturedAt: Instant,
) {
    fun publicView() = RaidCombatInputSnapshotView(contentVersion, rewardVersion, seed, player, boss, capturedAt)
}

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidAttemptView(
    val attemptId: UUID,
    val sessionId: UUID,
    val slotOrdinal: Int,
    val mode: RaidAttemptMode,
    val status: RaidAttemptStatus,
    val inputSnapshot: RaidCombatInputSnapshotView,
    val tickDurationMilliseconds: Int,
    val durationMilliseconds: Long,
    val startedAt: Instant,
    val completableAt: Instant,
    val endedAt: Instant?,
    val currentResult: RaidAttemptResultView,
    val terminalResult: RaidAttemptResultView?,
    val renderingTimeline: RaidRenderingTimeline,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidSlotView(
    val ordinal: Int,
    val status: RaidSlotStatus,
    val attemptsStarted: Int,
    val attemptsRemaining: Int,
    val currentAttemptId: UUID?,
    val resultHeld: Boolean,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidSessionView(
    val sessionId: UUID,
    val contentVersion: String,
    val rewardVersion: String,
    val settlesAt: Instant,
    val status: RaidSessionStatus,
    val sealTarget: Long,
    val sealedContribution: Long,
    val sealProgressBasisPoints: Long,
    val sealSuccessScheduled: Boolean,
    val serverNow: Instant,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class RaidStateView(
    val featureAvailable: Boolean,
    val unlockStageId: String,
    val unlocked: Boolean,
    val session: RaidSessionView,
    val slots: List<RaidSlotView>,
    val currentAttempt: RaidAttemptView?,
    val ranking: RaidRankingApiView,
    val claims: RaidClaimsView,
)

/** Transactional owner of account-scoped raid attempt and reward state. */
open class RaidService(
    private val repository: RaidRepository,
    private val clock: RaidClock,
    private val snapshotProvider: RaidCombatSnapshotProvider,
    private val contentPort: RaidContentPort,
    private val progress: RaidProgressPort,
    private val mainBattle: RaidMainBattlePort,
    private val completion: RaidAttemptCompletionService,
    private val mapper: ObjectMapper,
    private val rewards: RaidRewardPort,
    private val events: com.hanjjak.events.application.DomainEventPublisher,
    private val queries: RaidQueryPort = RaidQueryPort.Empty,
) {
    @Transactional
    open fun startAttempt(accountId: UUID, commandId: UUID, mode: RaidAttemptMode): RaidAttemptView {
        validateCommandId(commandId)
        recoverPendingResume(accountId)
        repository.lockAccount(accountId)
        val fingerprint = fingerprint("start", mode.name)
        replay(accountId, commandId, fingerprint, RaidAttemptView::class.java)?.let { return it }
        val requestTime = clock.now()
        val discovered = repository.openSession(requestTime) ?: throw IllegalArgumentException("RAID_SESSION_NOT_OPEN")
        val session = repository.lockSession(discovered.sessionId)
        requireOpenSession(session, requestTime)
        val content = contentPort.forVersion(session.contentVersion, session.rewardVersion)
        require(progress.hasFirstCleared(accountId, content.unlockStageId)) { "RAID_CONTENT_LOCKED" }
        val state = ensureState(session, accountId)
        val existing = repository.currentAttempt(accountId)
        require(existing == null) {
            if (existing?.status == RaidAttemptStatus.RESULT_HELD) "RAID_ATTEMPT_RESULT_HELD" else "RAID_ATTEMPT_RUNNING"
        }
        val slot: RaidRewardSlot?
        val attemptOrdinal: Int
        if (mode == RaidAttemptMode.REWARD) {
            slot = repository.lockSlot(session.sessionId, accountId, state.currentSlotOrdinal)
            require(slot.status == RaidSlotStatus.AVAILABLE) { "RAID_NO_AVAILABLE_SLOT" }
            require(slot.attemptsStarted < MAX_ATTEMPTS_PER_SLOT) { "RAID_ATTEMPT_LIMIT_REACHED" }
            attemptOrdinal = slot.attemptsStarted + 1
        } else {
            require(state.slotsTerminal == REWARD_SLOT_COUNT) { "RAID_PRACTICE_LOCKED" }
            slot = null
            attemptOrdinal = state.rewardAttemptsStarted + 1
        }
        val handoff = mainBattle.pauseLockedWithHandoff(accountId, commandId)
        val startedAt = clock.now()
        val seed = commandId.mostSignificantBits xor commandId.leastSignificantBits xor startedAt.toEpochMilli()
        val attempt = createAttempt(session, accountId, mode, slot, attemptOrdinal, startedAt, seed, content, snapshotProvider.snapshot(accountId), handoff)
        repository.saveAttempt(attempt)
        if (slot != null) {
            repository.saveSlot(slot.copy(status = RaidSlotStatus.ACTIVE, attemptsStarted = attemptOrdinal, currentAttemptId = attempt.attemptId))
            repository.saveAccountState(state.copy(rewardAttemptsStarted = state.rewardAttemptsStarted + 1, stateVersion = state.stateVersion + 1))
        }
        return view(attempt).also { saveCommand(accountId, commandId, fingerprint, it) }
    }

    @Transactional
    open fun retryAttempt(accountId: UUID, commandId: UUID, attemptId: UUID): RaidAttemptView {
        validateCommandId(commandId)
        recoverPendingResume(accountId)
        repository.lockAccount(accountId)
        val fingerprint = fingerprint("retry", attemptId.toString())
        replay(accountId, commandId, fingerprint, RaidAttemptView::class.java)?.let { return it }
        val current = repository.currentAttempt(accountId)
        require(current?.attemptId?.value == attemptId) { "RAID_ATTEMPT_NOT_CURRENT" }
        RaidAttemptTransitions.requireAllowed(current.status, RaidAttemptAction.RETRY)
        val session = repository.lockSession(current.sessionId)
        requireOpenSession(session, clock.now())
        val state = repository.lockAccountState(session.sessionId, accountId)
        val content = contentPort.forVersion(session.contentVersion, session.rewardVersion)
        val slot = if (current.mode == RaidAttemptMode.REWARD) {
            repository.lockSlot(current.sessionId, accountId, state.currentSlotOrdinal).also {
                require(it.currentAttemptId?.value == attemptId) { "RAID_ATTEMPT_NOT_CURRENT" }
                require(it.attemptsStarted < MAX_ATTEMPTS_PER_SLOT) { "RAID_ATTEMPT_LIMIT_REACHED" }
            }
        } else {
            null
        }
        val handoff = mainBattle.pauseLockedWithHandoff(accountId, commandId) ?: handoffFrom(current)
        val startedAt = clock.now()
        val random = UUID.randomUUID()
        val seed = random.mostSignificantBits xor random.leastSignificantBits xor startedAt.toEpochMilli()
        repository.saveAttempt(current.copy(status = RaidAttemptStatus.DISCARDED, endedAt = current.endedAt ?: startedAt, resumePending = false))
        val nextOrdinal = slot?.attemptsStarted?.plus(1) ?: state.rewardAttemptsStarted + 1
        val next = createAttempt(session, accountId, current.mode, slot, nextOrdinal, startedAt, seed, content, snapshotProvider.snapshot(accountId), handoff)
        repository.saveAttempt(next)
        if (slot != null) {
            repository.saveSlot(slot.copy(status = RaidSlotStatus.ACTIVE, attemptsStarted = nextOrdinal, currentAttemptId = next.attemptId))
            repository.saveAccountState(state.copy(rewardAttemptsStarted = state.rewardAttemptsStarted + 1, stateVersion = state.stateVersion + 1))
        }
        return view(next).also { saveCommand(accountId, commandId, fingerprint, it) }
    }

    @Transactional
    open fun discardAttempt(accountId: UUID, commandId: UUID, attemptId: UUID): RaidStateView {
        validateCommandId(commandId)
        recoverPendingResume(accountId)
        repository.lockAccount(accountId)
        val fingerprint = fingerprint("discard", attemptId.toString())
        replay(accountId, commandId, fingerprint, RaidStateView::class.java)?.let { return it }
        val current = repository.currentAttempt(accountId)
        require(current?.attemptId?.value == attemptId) { "RAID_ATTEMPT_NOT_CURRENT" }
        RaidAttemptTransitions.requireAllowed(current.status, RaidAttemptAction.DISCARD)
        val session = repository.lockSession(current.sessionId)
        requireOpenSession(session, clock.now())
        val now = clock.now()
        val handoff = handoffFrom(current)
        if (current.mode == RaidAttemptMode.PRACTICE) {
            repository.saveAttempt(current.copy(status = RaidAttemptStatus.DISCARDED, endedAt = current.endedAt ?: now, resumePending = false))
            if (current.status == RaidAttemptStatus.RUNNING && handoff != null) mainBattle.resumeLocked(accountId, commandId, handoff)
        } else {
            val state = repository.lockAccountState(session.sessionId, accountId)
            val slot = repository.lockSlot(session.sessionId, accountId, state.currentSlotOrdinal)
            require(slot.currentAttemptId?.value == attemptId) { "RAID_ATTEMPT_NOT_CURRENT" }
            repository.saveAttempt(current.copy(status = RaidAttemptStatus.DISCARDED, endedAt = current.endedAt ?: now, resumePending = false))
            repository.saveSlot(slot.copy(status = RaidSlotStatus.DISCARDED, currentAttemptId = null, terminalAt = now))
            repository.saveAccountState(
                state.copy(
                    currentSlotOrdinal = (state.currentSlotOrdinal + 1).coerceAtMost(REWARD_SLOT_COUNT),
                    slotsTerminal = state.slotsTerminal + 1,
                    stateVersion = state.stateVersion + 1,
                ),
            )
            if (current.status == RaidAttemptStatus.RUNNING && handoff != null) mainBattle.resumeLocked(accountId, commandId, handoff)
        }
        return stateView(session, accountId, clock.now()).also { saveCommand(accountId, commandId, fingerprint, it) }
    }

    @Transactional
    open fun currentAttempt(accountId: UUID): RaidAttemptView? {
        val observed = repository.currentAttempt(accountId) ?: return null
        val asOf = clock.now()
        if (observed.status == RaidAttemptStatus.RUNNING && !asOf.isBefore(observed.completableAt)) {
            completion.terminalizeOne(accountId, observed.attemptId.value, asOf)?.let(completion::resumeAfterTerminalization)
        } else if (observed.resumePending) {
            completion.resumeAfterTerminalization(observed)
        }
        repository.lockAccount(accountId)
        val current = repository.currentAttempt(accountId) ?: return null
        val session = repository.lockSession(current.sessionId)
        contentPort.forVersion(session.contentVersion, session.rewardVersion)
        return view(current)
    }

    @Transactional
    open fun state(accountId: UUID): RaidStateView {
        recoverPendingResume(accountId)
        repository.lockAccount(accountId)
        val session = repository.openSession(clock.now()) ?: throw IllegalArgumentException("RAID_SESSION_STALE")
        val locked = repository.lockSession(session.sessionId)
        val content = contentPort.forVersion(locked.contentVersion, locked.rewardVersion)
        ensureState(locked, accountId)
        return stateView(locked, accountId, clock.now(), content)
    }
    @Transactional(readOnly = true)
    open fun claims(accountId: UUID, cursor: String? = null): RaidClaimsView {
        repository.lockAccount(accountId)
        return queries.claims(accountId, cursor)
    }


    private fun recoverPendingResume(accountId: UUID) {
        repository.currentAttempt(accountId)?.takeIf { it.resumePending }?.let(completion::resumeAfterTerminalization)
    }

    @Transactional(readOnly = true)
    open fun ranking(accountId: UUID, cursor: String?, limit: Int, sessionId: UUID? = null): RaidRankingApiView {
        require(limit in 1..100) { "INVALID_LIMIT" }
        repository.lockAccount(accountId)
        val session = if (sessionId == null) {
            repository.openSession(clock.now()) ?: throw IllegalArgumentException("RAID_SESSION_STALE")
        } else {
            repository.session(RaidSessionId(sessionId))
                ?.also {
                    require(it.status == RaidSessionStatus.SETTLED_SUCCESS || it.status == RaidSessionStatus.SETTLED_FAILURE) {
                        "RAID_SESSION_STALE"
                    }
                }
                ?: throw IllegalArgumentException("RAID_SESSION_STALE")
        }
        require(progress.hasFirstCleared(accountId, contentPort.forVersion(session.contentVersion, session.rewardVersion).unlockStageId)) { "RAID_CONTENT_LOCKED" }
        return queries.ranking(accountId, session, cursor, limit)
    }
    private fun ensureState(session: RaidSession, accountId: UUID): RaidAccountState {
        repository.accountState(session.sessionId, accountId)?.let { return repository.lockAccountState(session.sessionId, accountId) }
        val state = RaidAccountState(session.sessionId, accountId, 1, 0, 0, 1, false)
        repository.saveAccountState(state)
        for (ordinal in 1..REWARD_SLOT_COUNT) repository.saveSlot(RaidRewardSlot(session.sessionId, accountId, ordinal, RaidSlotStatus.AVAILABLE, 0))
        return state
    }




    private fun createAttempt(
        session: RaidSession,
        accountId: UUID,
        mode: RaidAttemptMode,
        slot: RaidRewardSlot?,
        ordinal: Int,
        startedAt: Instant,
        seed: Long,
        content: RaidContentSnapshot,
        snapshot: RaidCombatSnapshot,
        handoff: RaidBattleHandoff?,
    ): RaidAttempt {
        val trace = RaidSimulator.simulate(
            RaidCombatInput(content.contentVersion, seed, snapshot.player, snapshot.skills, content.bossInitialAttack, content.bossInitialDefense, content.maxTicks),
        )
        val completableAt = startedAt.plusMillis(trace.elapsedTicks * content.tickDurationMilliseconds.toLong())
        val grade = grade(trace.totalDamage, content.gradeDamageThresholds)
        val result = mapOf(
            "damage" to trace.totalDamage,
            "grade" to grade.name,
            "sealContribution" to (content.sealContributions[grade.name] ?: 0),
            "endedAt" to completableAt.toString(),
            "playerDied" to trace.playerDied,
            "timeLimitReached" to (!trace.playerDied && trace.elapsedTicks >= content.maxTicks),
        )
        val player = RaidPlayerSnapshotView(
            snapshot.level,
            snapshot.player.attack,
            snapshot.player.maxHp,
            snapshot.defense,
            snapshot.player.penetration,
            snapshot.skillIds,
            snapshot.activeSkillLoadout,
            snapshot.mainGemPreset,
            snapshot.cosmeticEffectIds,
        )
        val boss = RaidBossSnapshotView(
            content.bossId,
            content.bossDisplayName,
            content.contentVersion,
            content.bossInitialAttack,
            content.bossInitialDefense,
            content.maxTicks,
            content.tickDurationMilliseconds,
            content.escalationIntervalTicks,
            content.escalationBasisPoints,
        )
        val stored = RaidStoredCombatInputSnapshot(
            content.contentVersion,
            content.rewardVersion,
            seed,
            player,
            boss,
            snapshot.skills,
            content.gradeDamageThresholds,
            content.sealContributions,
            handoff,
            startedAt,
        )
        return RaidAttempt(
            RaidAttemptId(UUID.randomUUID()),
            session.sessionId,
            accountId,
            mode,
            slot?.slotId,
            ordinal,
            RaidAttemptStatus.RUNNING,
            mapper.writeValueAsString(stored),
            mapper.writeValueAsString(result),
            mapper.writeValueAsString(renderingTimeline(trace)),
            startedAt,
            completableAt,
            seed = seed,
        )
    }

    private fun renderingTimeline(trace: RaidCombatResult): List<RaidCombatEventView> = trace.events.mapNotNull { event ->
        val type = when (event.type) {
            RaidCombatEventType.BATTLE_STARTED -> return@mapNotNull null
            RaidCombatEventType.PLAYER_SKILL_CAST -> "PLAYER_ACTION"
            RaidCombatEventType.PLAYER_HIT, RaidCombatEventType.DOT_HIT -> "PLAYER_IMPACT"
            RaidCombatEventType.BOSS_HIT -> "BOSS_HIT"
            RaidCombatEventType.ESCALATION -> "ESCALATION"
            RaidCombatEventType.PLAYER_DIED -> "PLAYER_DEATH"
            RaidCombatEventType.TIME_LIMIT_REACHED -> "TIME_LIMIT_END"
        }
        RaidCombatEventView(
            sequence = event.sequence,
            logicalTick = event.logicalTick,
            type = type,
            actor = when (type) {
                "PLAYER_ACTION", "PLAYER_IMPACT" -> "PLAYER"
                "BOSS_HIT" -> "BOSS"
                else -> null
            },
            target = when (type) {
                "PLAYER_IMPACT" -> "BOSS"
                "BOSS_HIT", "PLAYER_DEATH" -> "PLAYER"
                else -> null
            },
            damage = event.damage,
            critical = event.critical,
            hpBefore = event.playerHpBefore,
            hpAfter = event.playerHpAfter,
            escalationStage = event.escalationStage,
            bossAttack = event.bossAttack,
            bossDefense = event.bossDefense,
        )
    }

    @Transactional
    open fun confirmAttempt(accountId: UUID, commandId: UUID, attemptId: UUID): RaidConfirmationResult {
        validateCommandId(commandId)
        recoverPendingResume(accountId)
        repository.lockAccount(accountId)
        val fingerprint = fingerprint("confirm", attemptId.toString())
        replay(accountId, commandId, fingerprint, RaidConfirmationResult::class.java)?.let { return it }
        val current = repository.currentAttempt(accountId)
        require(current?.attemptId?.value == attemptId) { "RAID_ATTEMPT_NOT_CURRENT" }
        val session = repository.lockSession(current.sessionId)
        requireOpenSession(session, clock.now())
        val state = repository.lockAccountState(session.sessionId, accountId)
        require(current.mode == RaidAttemptMode.REWARD) { "RAID_PRACTICE_CANNOT_CONFIRM" }
        val slot = repository.lockSlot(session.sessionId, accountId, state.currentSlotOrdinal)
        require(slot.currentAttemptId?.value == attemptId) { "RAID_ATTEMPT_NOT_CURRENT" }
        val attempt = repository.lockAttempt(current.attemptId, session.sessionId, accountId)
        RaidAttemptTransitions.requireAllowed(attempt.status, RaidAttemptAction.CONFIRM)
        val authoritative = if (attempt.status == RaidAttemptStatus.RESULT_HELD) terminalResult(attempt) else {
            val input = mapper.readValue(attempt.inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
            val elapsedTick = minOf(
                Duration.between(attempt.startedAt, clock.now()).toMillis().coerceAtLeast(0L) / input.boss.tickDurationMilliseconds,
                terminalElapsedTicks(attempt).toLong(),
            )
            val type = mapper.typeFactory.constructCollectionType(List::class.java, RaidCombatEventView::class.java)
            val events = mapper.readValue<List<RaidCombatEventView>>(attempt.timeline, type).filter { it.logicalTick <= elapsedTick }
            resultViewForPrefix(events, input)
        }
        require(authoritative.damage > 0) { "RAID_ZERO_DAMAGE_CANNOT_CONFIRM" }
        val bundle = RaidRules.personalReward(authoritative.grade)
        val previous = repository.lockContribution(session.sessionId, accountId)
        Math.addExact(previous.sealContribution, authoritative.sealContribution.toLong())
        Math.addExact(previous.damage, authoritative.damage)
        Math.addExact(previous.confirmedAttempts.toLong(), 1L)
        rewards.preflightLocked(accountId, bundle)
        val now = clock.now()
        val resultJson = mapper.writeValueAsString(mapOf(
            "damage" to authoritative.damage,
            "grade" to authoritative.grade.name,
            "sealContribution" to authoritative.sealContribution,
            "endedAt" to (authoritative.endedAt ?: now).toString(),
            "playerDied" to authoritative.playerDied,
            "timeLimitReached" to authoritative.timeLimitReached,
            "reward" to bundle,
        ))
        val confirmed = RaidConfirmedResult(UUID.randomUUID(), attempt.attemptId, session.sessionId, accountId, authoritative.damage,
            authoritative.grade, authoritative.sealContribution.toLong(), session.rewardVersion, now, bundle, resultJson)
        rewards.creditLocked(accountId, attempt.attemptId.value, bundle)
        repository.saveAttempt(attempt.copy(status = RaidAttemptStatus.CONFIRMED, endedAt = attempt.endedAt ?: now, resumePending = false))
        repository.saveSlot(slot.copy(status = RaidSlotStatus.CONFIRMED, currentAttemptId = null, terminalAt = now))
        repository.appendConfirmedResult(confirmed)
        repository.upsertContribution(RaidContribution(session.sessionId, accountId, confirmed.sealContribution, confirmed.damage, 1, confirmed.damage))
        repository.saveAccountState(state.copy(currentSlotOrdinal = minOf(REWARD_SLOT_COUNT, state.currentSlotOrdinal + 1),
            slotsTerminal = state.slotsTerminal + 1, stateVersion = state.stateVersion + 1))
        repository.incrementAccountStateVersion(accountId)
        if (attempt.status == RaidAttemptStatus.RUNNING) {
            handoffFrom(attempt)?.let { mainBattle.resumeLocked(accountId, commandId, it) }
        }
        events.publish("RAID_ATTEMPT_CONFIRMED", attempt.attemptId.value, now, accountId = accountId, commandId = commandId,
            payload = mapOf("sessionId" to session.sessionId.value, "damage" to confirmed.damage, "grade" to confirmed.grade.name, "reward" to bundle))
        val result = RaidConfirmationResult(stateView(session, accountId, now), confirmed)
        saveCommand(accountId, commandId, fingerprint, result)
        return result
    }

    private fun resultViewForPrefix(events: List<RaidCombatEventView>, input: RaidStoredCombatInputSnapshot): RaidAttemptResultView {
        val damage = events.asSequence().filter { it.type == "PLAYER_IMPACT" }.fold(0L) { total, event -> Math.addExact(total, event.damage?.toLong() ?: 0L) }
        val grade = RaidRules.grade(damage, input.gradeDamageThresholds)
        return RaidAttemptResultView(damage, grade, RaidRules.contribution(grade, input.sealContributions).toInt(), null,
            events.any { it.type == "PLAYER_DEATH" }, events.any { it.type == "TIME_LIMIT_END" })
    }

    @Transactional
    open fun claimReward(accountId: UUID, commandId: UUID, claimId: UUID): RaidClaimResult {
        validateCommandId(commandId)
        repository.lockAccount(accountId)
        val fingerprint = fingerprint("claim", claimId.toString())
        replay(accountId, commandId, fingerprint, RaidClaimResult::class.java)?.let { return it }
        val observed = repository.claim(RaidClaimId(claimId), accountId) ?: throw IllegalArgumentException("RAID_CLAIM_NOT_FOUND")
        require(observed.status == RaidClaimStatus.CLAIMABLE) { "RAID_CLAIM_ALREADY_CLAIMED" }
        rewards.preflightLocked(accountId, observed.bundle)
        val claim = repository.lockClaim(RaidClaimId(claimId), accountId)
        require(claim.status == RaidClaimStatus.CLAIMABLE) { "RAID_CLAIM_ALREADY_CLAIMED" }
        require(claim == observed) { "RAID_CLAIM_STATE_CONFLICT" }
        val now = clock.now()
        rewards.creditLocked(accountId, claim.sourceId, claim.bundle)
        require(repository.markClaimed(claim.claimId, accountId, now)) { "RAID_CLAIM_ALREADY_CLAIMED" }
        repository.incrementAccountStateVersion(accountId)
        events.publish("RAID_REWARD_CLAIMED", claim.claimId.value, now, accountId = accountId, commandId = commandId,
            payload = mapOf("sessionId" to claim.sessionId.value, "kind" to claim.kind.name, "reward" to claim.bundle))
        val claimed = claim.copy(status = RaidClaimStatus.CLAIMED, claimedAt = now)
        val result = RaidClaimResult(claimView(claimed), claimsView(accountId, null))
        saveCommand(accountId, commandId, fingerprint, result)
        return result
    }

    private fun stateView(session: RaidSession, accountId: UUID, now: Instant, resolvedContent: RaidContentSnapshot? = null): RaidStateView {
        val slots = repository.slots(session.sessionId, accountId)
        val content = resolvedContent ?: contentPort.forVersion(session.contentVersion, session.rewardVersion)
        val sealed = repository.contributions(session.sessionId).fold(0L) { total, contribution ->
            Math.addExact(total, contribution.sealContribution)
        }
        val progress = if (sealed >= content.sealTarget) 10_000L else (sealed * 10_000L / content.sealTarget)
        return RaidStateView(
            featureAvailable = content.authority == "applied",
            unlockStageId = content.unlockStageId,
            unlocked = progressPortUnlocked(accountId, content.unlockStageId),
            session = RaidSessionView(session.sessionId.value, session.contentVersion, session.rewardVersion, session.settlesAt, session.status,
                content.sealTarget, sealed, progress, sealed >= content.sealTarget, now),
            slots = slots.map {
                RaidSlotView(it.ordinal, it.status, it.attemptsStarted,
                    (MAX_ATTEMPTS_PER_SLOT - it.attemptsStarted).coerceAtLeast(0), it.currentAttemptId?.value,
                    it.status == RaidSlotStatus.RESULT_HELD)
            },
            currentAttempt = repository.currentAttempt(accountId)?.let(::view),
            ranking = queries.ranking(accountId, session, null, 100),
            claims = claimsView(accountId, null),
        )
    }

    private fun accountProgressUnlocked(accountId: UUID, stageId: String): Boolean = progress.hasFirstCleared(accountId, stageId)
    private fun progressPortUnlocked(accountId: UUID, stageId: String): Boolean = accountProgressUnlocked(accountId, stageId)

    private fun claimView(claim: RaidClaim) = RaidClaimView(claim.claimId.value, claim.sessionId.value, claim.kind, claim.status, claim.bundle, claim.claimableAt, claim.claimedAt)

    private fun claimsView(accountId: UUID, cursor: String?): RaidClaimsView {
        val all = repository.claims(accountId).sortedWith(compareBy<RaidClaim> { it.claimableAt }.thenBy { it.claimId.value })
        val start = cursor?.let { raw ->
            val id = raw.substringAfterLast(':', missingDelimiterValue = "")
            val parsed = runCatching { UUID.fromString(id) }.getOrElse { throw IllegalArgumentException("INVALID_CLAIM_CURSOR") }
            val index = all.indexOfFirst { it.claimId.value == parsed }
            require(index >= 0) { "INVALID_CLAIM_CURSOR" }
            index + 1
        } ?: 0
        val page = all.drop(start).take(CLAIMS_PAGE_SIZE)
        val next = page.lastOrNull()?.takeIf { start + page.size < all.size }?.let { "${it.claimableAt.toEpochMilli()}:${it.claimId.value}" }
        return RaidClaimsView(page.map(::claimView), all.count { it.status == RaidClaimStatus.CLAIMABLE }.toLong(),
            all.count { it.status == RaidClaimStatus.CLAIMED }.toLong(), next)
    }


    private fun terminalResult(attempt: RaidAttempt): RaidAttemptResultView = resultView(mapper.readTree(attempt.result), attempt.endedAt)

    private fun terminalElapsedTicks(attempt: RaidAttempt): Long {
        val input = mapper.readValue(attempt.inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        return Duration.between(attempt.startedAt, attempt.completableAt).toMillis() / input.boss.tickDurationMilliseconds
    }

    private fun view(attempt: RaidAttempt): RaidAttemptView {
        val input = mapper.readValue(attempt.inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        val resultNode = mapper.readTree(attempt.result)
        val finalResult = resultView(resultNode, attempt.endedAt)
        val timelineType = mapper.typeFactory.constructCollectionType(List::class.java, RaidCombatEventView::class.java)
        val allEvents = mapper.readValue<List<RaidCombatEventView>>(attempt.timeline, timelineType)
        val elapsed = Duration.between(attempt.startedAt, minOf(clock.now(), attempt.completableAt)).toMillis().coerceAtLeast(0)
        val elapsedTicks = (elapsed / input.boss.tickDurationMilliseconds).toInt()
        val events = if (attempt.status == RaidAttemptStatus.RUNNING) allEvents.filter { it.logicalTick <= elapsedTicks } else allEvents
        val currentResult = if (attempt.status == RaidAttemptStatus.RUNNING) resultViewForPrefix(events, input) else finalResult
        val ordinal = attempt.slotId?.let { slotId -> repository.slots(attempt.sessionId, attempt.accountId).firstOrNull { it.slotId == slotId }?.ordinal } ?: 0
        val duration = Duration.between(attempt.startedAt, attempt.completableAt).toMillis()
        return RaidAttemptView(
            attempt.attemptId.value,
            attempt.sessionId.value,
            ordinal,
            attempt.mode,
            attempt.status,
            input.publicView(),
            input.boss.tickDurationMilliseconds,
            duration,
            attempt.startedAt,
            attempt.completableAt,
            attempt.endedAt,
            currentResult,
            if (attempt.status == RaidAttemptStatus.RUNNING) null else finalResult,
            RaidRenderingTimeline(input.boss.tickDurationMilliseconds, duration, events),
        )
    }


    private fun resultView(node: JsonNode, endedAt: Instant?) = RaidAttemptResultView(
        node["damage"].asLong(),
        RaidGrade.valueOf(node["grade"].asText()),
        node["sealContribution"].asInt(),
        endedAt ?: node["endedAt"]?.asText()?.let(Instant::parse),
        node["playerDied"]?.asBoolean() ?: false,
        node["timeLimitReached"]?.asBoolean() ?: false,
    )

    private fun handoffFrom(attempt: RaidAttempt): RaidBattleHandoff? = runCatching {
        mapper.readValue(attempt.inputSnapshot, RaidStoredCombatInputSnapshot::class.java).pausedBattle
    }.getOrNull()

    private fun requireOpenSession(session: RaidSession, now: Instant) {
        require(session.status == RaidSessionStatus.OPEN && now.isBefore(session.settlesAt)) { "RAID_SESSION_CLOSED" }
    }

    private fun validateCommandId(commandId: UUID) {
        require(commandId.version() == 4 && commandId.variant() == 2) { "INVALID_COMMAND_ID" }
    }

    private fun <T> replay(accountId: UUID, key: UUID, fingerprint: String, type: Class<T>): T? {
        val existing = repository.claimCommand(accountId, key) ?: return null
        require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
        require(existing.resultJson != null) { "RAID_COMMAND_IN_PROGRESS" }
        return mapper.readValue(existing.resultJson, type)
    }

    private fun saveCommand(accountId: UUID, key: UUID, fingerprint: String, result: Any) {
        require(repository.recordCommand(accountId, key, fingerprint, mapper.writeValueAsString(result), RaidCommandStatus.SUCCEEDED)) {
            "RAID_COMMAND_REPLAY_RACE"
        }
    }
    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
    private fun fingerprint(vararg values: String): String = sha256(values.joinToString("\u0000"))
    private fun grade(damage: Long, thresholds: Map<String, Long>): RaidGrade = RaidRules.grade(damage, thresholds)

    private companion object {
        const val REWARD_SLOT_COUNT = 3
        const val MAX_ATTEMPTS_PER_SLOT = 3
        const val CLAIMS_PAGE_SIZE = 100
    }
}
