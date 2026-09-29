package com.hanjjak.raid.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.raid.domain.*
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import kotlin.test.*
import java.time.Instant
import java.util.UUID

class RaidAttemptServiceTest {
    private val account = UUID.randomUUID()
    private val now = Instant.parse("2026-09-14T08:00:00Z")
    private val content = RaidContentSnapshot("raid-v1", "reward-v1", "stage.01-02", bossInitialAttack = 1, bossInitialDefense = 1, maxTicks = 10, gradeDamageThresholds = mapOf("D" to 1), sealContributions = mapOf("D" to 100, "PARTICIPATION" to 0))
    private val mapper = ObjectMapper().findAndRegisterModules()

    @Test
    fun `start snapshots and replays immutable server result`() {
        val repository = MemoryRepository(now)
        val handoff = Handoff()
        val service = service(repository, handoff)
        val key = UUID.randomUUID()
        val started = service.startAttempt(account, key, RaidAttemptMode.REWARD)
        assertEquals(RaidAttemptStatus.RUNNING, started.status)
        assertTrue(started.inputSnapshot.contentVersion == "raid-v1")
        assertTrue(started.renderingTimeline.events.isNotEmpty())
        assertEquals(started, service.startAttempt(account, key, RaidAttemptMode.REWARD))
        assertEquals(1, repository.attempts.size)
        assertEquals(1, handoff.pauses)
        assertFailsWith<IllegalArgumentException> { service.startAttempt(account, key, RaidAttemptMode.PRACTICE) }
    }

    @Test
    fun `retry discards current attempt and consumes a second slot attempt`() {
        val repository = MemoryRepository(now)
        val service = service(repository, Handoff())
        val first = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val second = service.retryAttempt(account, UUID.randomUUID(), first.attemptId)
        assertNotEquals(first.attemptId, second.attemptId)
        assertEquals(RaidAttemptStatus.DISCARDED, repository.attempts.getValue(RaidAttemptId(first.attemptId)).status)
        assertEquals(2, repository.slots.single { it.ordinal == 1 }.attemptsStarted)
        val third = service.retryAttempt(account, UUID.randomUUID(), second.attemptId)
        assertEquals(3, repository.slots.single { it.ordinal == 1 }.attemptsStarted)
        assertFailsWith<IllegalArgumentException> { service.retryAttempt(account, UUID.randomUUID(), third.attemptId) }
    }

    @Test
    fun `terminalizer holds elapsed result then final discard resumes main battle`() {
        val repository = MemoryRepository(now)
        val handoff = Handoff(original = RaidBattleHandoff(UUID.randomUUID(), UUID.randomUUID(), "stage.01-01"))
        val service = service(repository, handoff)
        val attempt = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val terminalizer = RaidAttemptTerminalizer(repository, completion(repository, handoff), RaidClock { now.plusSeconds(2) })
        assertEquals(1, terminalizer.terminalizeDue(now.plusSeconds(2)))
        assertEquals(RaidAttemptStatus.RESULT_HELD, repository.attempts.values.single().status)
        service.discardAttempt(account, UUID.randomUUID(), attempt.attemptId)
        assertEquals(RaidSlotStatus.DISCARDED, repository.slots.single { it.ordinal == 1 }.status)
        assertEquals(1, handoff.resumes)
    }
    @Test
    fun `running prefix only counts player impacts and has no ended timestamp`() {
        val repository = MemoryRepository(now)
        val service = service(repository, Handoff())
        service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val current = service(repository, Handoff(), RaidClock { now.plusMillis(500) }).currentAttempt(account)
        assertNotNull(current)
        val playerDamage = current.renderingTimeline.events.filter { it.type == "PLAYER_IMPACT" }.sumOf { it.damage?.toLong() ?: 0L }
        assertEquals(playerDamage, current.currentResult.damage)
        assertNull(current.currentResult.endedAt)
    }

    @Test
    fun `timeline maps only closed raid event types with actor and target`() {
        val repository = MemoryRepository(now)
        val service = service(repository, Handoff())
        val started = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val allowed = setOf("PLAYER_ACTION", "PLAYER_IMPACT", "BOSS_HIT", "ESCALATION", "PLAYER_DEATH", "TIME_LIMIT_END")
        assertTrue(started.renderingTimeline.events.all { it.type in allowed })
        started.renderingTimeline.events.filter { it.type == "PLAYER_IMPACT" }.forEach { assertEquals("PLAYER", it.actor); assertEquals("BOSS", it.target) }
        started.renderingTimeline.events.filter { it.type == "BOSS_HIT" }.forEach { assertEquals("BOSS", it.actor); assertEquals("PLAYER", it.target) }
    }

    @Test
    fun `terminalizer returns null for stale mismatched and not due keys`() {
        val repository = MemoryRepository(now)
        val service = service(repository, Handoff())
        val attempt = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val completion = completion(repository, Handoff())
        assertNull(completion.terminalizeOne(account, UUID.randomUUID(), now.plusSeconds(100)))
        assertNull(completion.terminalizeOne(account, attempt.attemptId, now))
        assertEquals(RaidAttemptStatus.RUNNING, repository.attempts.values.single().status)
        assertEquals(RaidAttemptStatus.RESULT_HELD, completion.terminalizeOne(account, attempt.attemptId, now.plusSeconds(2))?.status)
    }

    @Test
    fun `terminalizer processes one bounded page per invocation`() {
        val repository = MemoryRepository(now)
        val service = service(repository, Handoff())
        val first = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        service.discardAttempt(account, UUID.randomUUID(), first.attemptId)
        val second = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        repository.attempts[RaidAttemptId(second.attemptId)] = repository.attempts.getValue(RaidAttemptId(second.attemptId)).copy(completableAt = now.minusSeconds(1))
        val terminalizer = RaidAttemptTerminalizer(repository, completion(repository, Handoff()), RaidClock { now.plusSeconds(2) }, pageSize = 1)
        assertEquals(1, terminalizer.terminalizeDue(now.plusSeconds(2)))
        assertEquals(1, repository.dueCalls)
        assertEquals(1, repository.attempts.values.count { it.status == RaidAttemptStatus.RESULT_HELD })
    }

    @Test
    fun `terminalizer retries a persisted pending resume after a crash gap`() {
        val repository = MemoryRepository(now)
        val handoff = Handoff(original = RaidBattleHandoff(UUID.randomUUID(), UUID.randomUUID(), "stage.01-01"))
        val service = service(repository, handoff)
        val attempt = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val completion = completion(repository, handoff)
        val held = completion.terminalizeOne(account, attempt.attemptId, now.plusSeconds(2))
        assertNotNull(held)
        assertTrue(repository.attempts.getValue(RaidAttemptId(attempt.attemptId)).resumePending)

        val terminalizer = RaidAttemptTerminalizer(repository, completion, RaidClock { now.plusSeconds(3) })
        assertEquals(0, terminalizer.terminalizeDue(now.plusSeconds(3)))
        assertEquals(1, handoff.resumes)
        assertFalse(repository.attempts.getValue(RaidAttemptId(attempt.attemptId)).resumePending)
    }

    @Test
    fun `retry carries original handoff when live pause has no active battle`() {
        val repository = MemoryRepository(now)
        val handoff = Handoff(original = RaidBattleHandoff(UUID.randomUUID(), UUID.randomUUID(), "stage.01-01"))
        val service = service(repository, handoff)
        val first = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        handoff.nextPause = null
        val retried = service.retryAttempt(account, UUID.randomUUID(), first.attemptId)
        val firstStored = mapper.readValue(repository.attempts.getValue(RaidAttemptId(first.attemptId)).inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        val retryStored = mapper.readValue(repository.attempts.getValue(RaidAttemptId(retried.attemptId)).inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        assertEquals(firstStored.pausedBattle, retryStored.pausedBattle)
    }

    @Test
    fun `held retry captures newly resumed battle handoff`() {
        val repository = MemoryRepository(now)
        val original = RaidBattleHandoff(UUID.randomUUID(), UUID.randomUUID(), "stage.01-01")
        val resumed = RaidBattleHandoff(UUID.randomUUID(), UUID.randomUUID(), "stage.01-01")
        val handoff = Handoff(original = original, resumed = resumed)
        val service = service(repository, handoff)
        val first = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val completion = completion(repository, handoff)
        val held = completion.terminalizeOne(account, first.attemptId, now.plusSeconds(2))
        assertNotNull(held)
        completion.resumeAfterTerminalization(held)
        val retried = service.retryAttempt(account, UUID.randomUUID(), first.attemptId)
        val stored = mapper.readValue(repository.attempts.getValue(RaidAttemptId(retried.attemptId)).inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        assertEquals(resumed, stored.pausedBattle)
    }

    @Test
    fun `running view only exposes elapsed timeline prefix`() {
        val repository = MemoryRepository(now)
        val service = service(repository, Handoff())
        val attempt = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val current = service(repository, Handoff(), RaidClock { now.plusMillis(500) }).currentAttempt(account)
        assertNotNull(current)
        assertTrue(current.renderingTimeline.events.all { it.logicalTick <= 5 })
        assertNull(current.terminalResult)
    }

    @Test
    fun `live discard resumes the paused main battle before final slot`() {
        val repository = MemoryRepository(now)
        val handoff = Handoff(original = RaidBattleHandoff(UUID.randomUUID(), UUID.randomUUID(), "stage.01-01"))
        val service = service(repository, handoff)
        val attempt = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        service.discardAttempt(account, UUID.randomUUID(), attempt.attemptId)
        assertEquals(1, handoff.resumes)
    }

    @Test
    fun `command UUID requires RFC 4122 variant`() {
        val repository = MemoryRepository(now)
        val service = service(repository, Handoff())
        val nonIetf = UUID(0x0000000000004000L, 0x0000000000000000L)
        assertFailsWith<IllegalArgumentException> { service.startAttempt(account, nonIetf, RaidAttemptMode.REWARD) }
    }

    @Test
    fun `input snapshot retains full skill parameters and grading tables`() {
        val repository = MemoryRepository(now)
        val profile = SkillProfile(heavyBasisPoints = 123, dotTotalBasisPoints = 456, activeOrder = listOf("active_dot"))
        val service = service(
            repository,
            Handoff(),
            snapshot = RaidCombatSnapshot(FighterStats(10, 100, 0), profile, "character-v1", level = 7, defense = 9, skillIds = listOf("active_dot", "passive_critical"), cosmeticEffectIds = listOf("set-a")),
            raidContent = content.copy(gradeDamageThresholds = mapOf("D" to 1, "S" to 999), sealContributions = mapOf("D" to 100, "S" to 500)),
        )
        val started = service.startAttempt(account, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val stored = mapper.readValue(repository.attempts.getValue(RaidAttemptId(started.attemptId)).inputSnapshot, RaidStoredCombatInputSnapshot::class.java)
        assertEquals(123, stored.skillProfile.heavyBasisPoints)
        assertEquals(999L, stored.gradeDamageThresholds["S"])
        assertEquals(500, stored.sealContributions["S"])
        assertEquals(listOf("active_dot", "passive_critical"), started.inputSnapshot.player.skillIds)
    }
    @Test
    fun `state keeps raid unavailable until explicit runtime activation`() {
        val state = service(MemoryRepository(now), Handoff(), progressCleared = true).state(account)
        assertFalse(state.featureAvailable)
        assertEquals("stage.01-02", state.unlockStageId)
        assertTrue(state.unlocked)
    }

    @Test
    fun `state preserves unlock metadata while account is locked`() {
        val state = service(MemoryRepository(now), Handoff(), progressCleared = false).state(account)
        assertFalse(state.featureAvailable)
        assertEquals("stage.01-02", state.unlockStageId)
        assertFalse(state.unlocked)
    }

    @Test
    fun `ranking accepts explicit settled session while default remains open`() {
        val repository = MemoryRepository(now)
        val selected = mutableListOf<RaidSessionId>()
        val queries = object : RaidQueryPort {
            override fun stateVersion(accountId: UUID) = 0L
            override fun sealedContribution(sessionId: RaidSessionId) = 0L
            override fun ranking(accountId: UUID, session: RaidSession, cursor: String?, limit: Int): RaidRankingApiView {
                selected += session.sessionId
                return RaidRankingApiView(session.sessionId.value, session.status != RaidSessionStatus.OPEN, null, 0L, emptyList(), null, 0L)
            }
            override fun claims(accountId: UUID, cursor: String?) = RaidClaimsView(emptyList(), 0L, 0L, null)
        }
        val service = service(repository, Handoff(), queries = queries)
        service.ranking(account, null, 100)
        service.ranking(account, null, 100, repository.historicalSession.sessionId.value)
        assertEquals(listOf(repository.session.sessionId, repository.historicalSession.sessionId), selected)
    }
    @Test
    fun `ranking rejects explicit settling historical session before query adapter`() {
        val repository = MemoryRepository(now)
        val settling = repository.historicalSession.copy(
            status = RaidSessionStatus.SETTLING,
            settlementPhase = RaidSettlementPhase.MATERIALIZE_RANKS,
            completedAt = null,
        )
        repository.saveSession(settling)
        var queried = false
        val queries = object : RaidQueryPort {
            override fun stateVersion(accountId: UUID) = 0L
            override fun sealedContribution(sessionId: RaidSessionId) = 0L
            override fun ranking(accountId: UUID, session: RaidSession, cursor: String?, limit: Int): RaidRankingApiView {
                queried = true
                return RaidRankingApiView(session.sessionId.value, true, 1, 100L, emptyList(), null, 1L)
            }
            override fun claims(accountId: UUID, cursor: String?) = RaidClaimsView(emptyList(), 0L, 0L, null)
        }
        val service = service(repository, Handoff(), queries = queries)

        val error = assertFailsWith<IllegalArgumentException> {
            service.ranking(account, null, 100, settling.sessionId.value)
        }

        assertEquals("RAID_SESSION_STALE", error.message)
        assertFalse(queried)
    }

    private fun service(
        repository: MemoryRepository,
        handoff: Handoff,
        raidClock: RaidClock = RaidClock { now },
        snapshot: RaidCombatSnapshot = RaidCombatSnapshot(FighterStats(10, 100, 0), SkillProfile(), "character-v1"),
        raidContent: RaidContentSnapshot = content,
        progressCleared: Boolean = true,
        queries: RaidQueryPort = RaidQueryPort.Empty,
    ): RaidService {
        val completion = completion(repository, handoff)
        return RaidService(repository, raidClock, RaidCombatSnapshotProvider { snapshot }, RaidContentPort { raidContent }, RaidProgressPort { _, _ -> progressCleared }, handoff, completion, mapper, NoopTestRewardPort, com.hanjjak.events.application.NoopDomainEventPublisher, queries)
    }
    private fun completion(repository: MemoryRepository, handoff: Handoff) = RaidAttemptCompletionService(repository, handoff, mapper)
    private object NoopTestRewardPort : RaidRewardPort {
        override fun preflightLocked(accountId: UUID, bundle: RaidRewardBundle) = Unit
        override fun creditLocked(accountId: UUID, sourceId: UUID, bundle: RaidRewardBundle) = Unit
    }


    private class Handoff(
        private val original: RaidBattleHandoff? = null,
        private val resumed: RaidBattleHandoff? = null,
    ) : RaidMainBattlePort {
        var resumes = 0
        var nextPause: RaidBattleHandoff? = original
        var pauses = 0
        override fun pauseLockedWithHandoff(accountId: UUID, commandId: UUID): RaidBattleHandoff? { pauses++; return nextPause }
        override fun resumeLocked(accountId: UUID, commandId: UUID, handoff: RaidBattleHandoff): RaidBattleHandoff? { resumes++; nextPause = resumed; return resumed }
    }


    private class MemoryRepository(private val now: Instant) : RaidRepository {
        val sessions = linkedMapOf<UUID, RaidSession>()
        val states = linkedMapOf<Pair<UUID, UUID>, RaidAccountState>()
        val slots = mutableListOf<RaidRewardSlot>()
        var dueCalls = 0
        val attempts = linkedMapOf<RaidAttemptId, RaidAttempt>()
        val commands = linkedMapOf<Pair<UUID, UUID>, RaidCommandRecord>()
        val session = RaidSession(RaidSessionId(UUID.randomUUID()), "raid-v1", "reward-v1", now.plusSeconds(3600), RaidSessionStatus.OPEN)
        val historicalSession = RaidSession(RaidSessionId(UUID.randomUUID()), "raid-v1", "reward-v1", now.minusSeconds(3600), RaidSessionStatus.SETTLED_SUCCESS, cutoffAt = now.minusSeconds(3600), settlementPhase = RaidSettlementPhase.SETTLED, completedAt = now.minusSeconds(3600))

        init { sessions[session.sessionId.value] = session; sessions[historicalSession.sessionId.value] = historicalSession }
        override fun openSession(now: Instant) = session
        override fun dueOpenSessions(asOf: Instant, limit: Int) = listOf(session)
        override fun lockSession(sessionId: RaidSessionId) = sessions.getValue(sessionId.value)
        override fun lockAccount(accountId: UUID) = Unit
        override fun accountState(sessionId: RaidSessionId, accountId: UUID) = states[sessionId.value to accountId]
        override fun lockAccountState(sessionId: RaidSessionId, accountId: UUID) = states.getValue(sessionId.value to accountId)
        override fun lockSlot(sessionId: RaidSessionId, accountId: UUID, ordinal: Int) = slots.first { it.sessionId == sessionId && it.accountId == accountId && it.ordinal == ordinal }
        override fun lockAttempt(attemptId: RaidAttemptId, sessionId: RaidSessionId, accountId: UUID) = attempts.getValue(attemptId)
        override fun slots(sessionId: RaidSessionId, accountId: UUID) = slots.filter { it.sessionId == sessionId && it.accountId == accountId }.sortedBy { it.ordinal }
        override fun lockClaim(claimId: RaidClaimId, accountId: UUID): RaidClaim = error("unused")
        override fun attemptResult(attemptId: RaidAttemptId) = null
        override fun currentAttempt(accountId: UUID) = attempts.values.filter { it.accountId == accountId && it.status in setOf(RaidAttemptStatus.RUNNING, RaidAttemptStatus.RESULT_HELD) }.maxByOrNull { it.startedAt }
        override fun dueAttempts(asOf: Instant, after: UUID?, limit: Int): List<RaidAttempt> { dueCalls++; return attempts.values.filter { (it.status == RaidAttemptStatus.RUNNING && !it.completableAt.isAfter(asOf)) || it.resumePending }.sortedBy { it.attemptId.value }.dropWhile { after != null && it.attemptId.value <= after }.take(limit) }
        override fun clearResumePending(attemptId: RaidAttemptId, sessionId: RaidSessionId, accountId: UUID): Boolean {
            val attempt = attempts[attemptId] ?: return false
            if (attempt.sessionId != sessionId || attempt.accountId != accountId || !attempt.resumePending) return false
            attempts[attemptId] = attempt.copy(resumePending = false)
            return true
        }
        override fun settlementAccounts(sessionId: RaidSessionId, after: UUID?, limit: Int) = emptyList<UUID>()
        override fun freezeSettlementWorkset(sessionId: RaidSessionId, cutoffAt: Instant) = 0
        override fun settlementAccountCount(sessionId: RaidSessionId) = 0L
        override fun finalizedSettlementAccountCount(sessionId: RaidSessionId) = 0L
        override fun settlementAttempts(sessionId: RaidSessionId, accountId: UUID) = attempts.values.filter { it.sessionId == sessionId && it.accountId == accountId }
        override fun contributions(sessionId: RaidSessionId) = emptyList<RaidContribution>()
        override fun finalRanks(sessionId: RaidSessionId) = emptyList<RaidFinalRank>()
        override fun rankCount(sessionId: RaidSessionId) = 0L
        override fun dailyRankClaimCount(sessionId: RaidSessionId) = 0L
        override fun findSessionBySettlesAt(settlesAt: Instant) = sessions.values.firstOrNull { it.settlesAt == settlesAt }
        override fun session(sessionId: RaidSessionId) = sessions[sessionId.value]
        override fun settlingSessions(asOf: Instant) = sessions.values.filter { it.status == RaidSessionStatus.SETTLING && !it.settlesAt.isAfter(asOf) }
        override fun lockSettlementAccount(sessionId: RaidSessionId, accountId: UUID): RaidSettlementAccount = error("unused")
        override fun finalizeSettlementAccount(sessionId: RaidSessionId, accountId: UUID, finalizedAt: Instant) = false
        override fun saveSession(session: RaidSession) { sessions[session.sessionId.value] = session }
        override fun saveAccountState(state: RaidAccountState) { states[state.sessionId.value to state.accountId] = state }
        override fun saveSlot(slot: RaidRewardSlot) { slots.removeIf { it.slotId == slot.slotId }; slots += slot }
        override fun claim(claimId: RaidClaimId, accountId: UUID): RaidClaim? = null
        override fun incrementAccountStateVersion(accountId: UUID): Long = 0
        override fun saveAttempt(attempt: RaidAttempt) { attempts[attempt.attemptId] = attempt }
        override fun appendConfirmedResult(result: RaidConfirmedResult) = Unit
        override fun lockContribution(sessionId: RaidSessionId, accountId: UUID): RaidContribution = error("unused")
        override fun upsertContribution(contribution: RaidContribution) = Unit
        override fun saveFinalRank(rank: RaidFinalRank) = Unit
        override fun insertClaim(claim: RaidClaim) = Unit
        override fun markClaimed(claimId: RaidClaimId, accountId: UUID, claimedAt: Instant) = false
        override fun claims(accountId: UUID) = emptyList<RaidClaim>()
        override fun claimCommand(accountId: UUID, idempotencyKey: UUID) = commands[accountId to idempotencyKey]
        override fun recordCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String?, status: RaidCommandStatus, expiresAt: Instant?): Boolean {
            val key = accountId to idempotencyKey
            if (commands.containsKey(key)) return false
            commands[key] = RaidCommandRecord(UUID.randomUUID(), accountId, idempotencyKey, fingerprint, resultJson, status, now, expiresAt)
            return true
        }
    }
}
