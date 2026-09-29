package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.raid.application.*
import com.hanjjak.raid.domain.*
import com.hanjjak.raid.infrastructure.JdbcRaidRepository
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import org.flywaydb.core.Flyway
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.jdbc.datasource.DriverManagerDataSource
import org.springframework.jdbc.datasource.DataSourceTransactionManager
import org.springframework.transaction.support.TransactionTemplate
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import kotlin.test.*

/** Real PostgreSQL lifecycle coverage; no mocked repository or clock is used. */
class RaidAttemptIntegrationTest {
    private val database = PostgresTestDatabase.schema("raid_attempt")
    private val jdbc: JdbcClient
    private val repository: JdbcRaidRepository
    private val mapper = ObjectMapper().findAndRegisterModules()
    private val dataSource = DriverManagerDataSource(database.jdbcUrl, database.username, database.password)
    private val now = Instant.parse("2026-09-14T08:00:00Z")
    private val snapshot = RaidCombatSnapshot(FighterStats(10, 100, 0), SkillProfile(), "character-v1")
    private val content = RaidContentSnapshot(
        "raid-v1", "reward-v1", "stage.01-02", bossInitialAttack = 1, bossInitialDefense = 1,
        maxTicks = 1, gradeDamageThresholds = mapOf("D" to 1),
        sealContributions = mapOf("PARTICIPATION" to 0, "D" to 100),
    )

    init {
        Flyway.configure().dataSource(database.jdbcUrl, database.username, database.password)
            .schemas(database.jdbcUrl.substringAfter("currentSchema="))
            .defaultSchema(database.jdbcUrl.substringAfter("currentSchema="))
            .locations("classpath:db/migration").load().migrate()
        jdbc = JdbcClient.create(dataSource)
        repository = JdbcRaidRepository(jdbc, mapper)
    }

    @Test
    fun `postgres lifecycle preserves snapshots and idempotent replay`() {
        val fixture = fixture()
        val service = service(fixture.accountId)
        val key = UUID.randomUUID()
        val first = service.startAttempt(fixture.accountId, key, RaidAttemptMode.REWARD)
        assertEquals(first, service.startAttempt(fixture.accountId, key, RaidAttemptMode.REWARD))
        assertFailsWith<IllegalArgumentException> { service.startAttempt(fixture.accountId, key, RaidAttemptMode.PRACTICE) }
        val persisted = repository.lockAttempt(RaidAttemptId(first.attemptId), RaidSessionId(first.sessionId), fixture.accountId)
        val stored = mapper.readTree(persisted.inputSnapshot)
        assertEquals(first.inputSnapshot.contentVersion, stored.path("contentVersion").asText())
        assertEquals(first.inputSnapshot.rewardVersion, stored.path("rewardVersion").asText())
        assertEquals(first.inputSnapshot.player.skillIds, stored.path("player").path("skillIds").map { it.asText() })
        assertEquals(first.inputSnapshot.player.skillLoadout, stored.path("player").path("skillLoadout").map { it.asText() })
        val retry = service.retryAttempt(fixture.accountId, UUID.randomUUID(), first.attemptId)
        assertEquals(RaidAttemptStatus.DISCARDED, repository.lockAttempt(RaidAttemptId(first.attemptId), RaidSessionId(first.sessionId), fixture.accountId).status)
        assertNotEquals(first.inputSnapshot, retry.inputSnapshot)
        assertEquals(2, repository.lockSlot(RaidSessionId(first.sessionId), fixture.accountId, 1).attemptsStarted)
        assertFailsWith<IllegalArgumentException> { service.startAttempt(fixture.accountId, key, RaidAttemptMode.PRACTICE) }
    }

    @Test
    fun `postgres max attempts and final discard unlock practice after mixed terminals`() {
        val fixture = fixture()
        val service = service(fixture.accountId)
        var current = service.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        current = service.retryAttempt(fixture.accountId, UUID.randomUUID(), current.attemptId)
        current = service.retryAttempt(fixture.accountId, UUID.randomUUID(), current.attemptId)
        assertFailsWith<IllegalArgumentException> { service.retryAttempt(fixture.accountId, UUID.randomUUID(), current.attemptId) }
        service.discardAttempt(fixture.accountId, UUID.randomUUID(), current.attemptId)
        repeat(2) {
            val attempt = service.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
            service.discardAttempt(fixture.accountId, UUID.randomUUID(), attempt.attemptId)
        }
        val state = service.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.PRACTICE)
        assertEquals(RaidAttemptMode.PRACTICE, state.mode)
        assertEquals(null, repository.lockSlot(RaidSessionId(state.sessionId), fixture.accountId, 3).currentAttemptId)
    }

    @Test
    fun `postgres terminalizer holds disconnected result and resumes after final discard`() {
        val fixture = fixture()
        val handoff = RecordingHandoff()
        val service = service(fixture.accountId, handoff)
        val first = service.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val terminalizer = RaidAttemptTerminalizer(repository, completion(handoff), RaidClock { now.plusSeconds(1) })
        assertEquals(1, terminalizer.terminalizeDue(now.plusSeconds(1)))
        assertEquals(RaidAttemptStatus.RESULT_HELD, repository.lockAttempt(RaidAttemptId(first.attemptId), RaidSessionId(first.sessionId), fixture.accountId).status)
        service.discardAttempt(fixture.accountId, UUID.randomUUID(), first.attemptId)
        assertEquals(1, handoff.resumes)
    }

    @Test
    fun `postgres terminalizer recovers persisted pending resume after crash gap`() {
        val fixture = fixture()
        val handoff = RecordingHandoff()
        val service = service(fixture.accountId, handoff)
        val first = service.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        jdbc.sql("update raid_attempt set status='RESULT_HELD',ended_at=completable_at,resume_pending=true where id=:id")
            .param("id", first.attemptId).update()
        jdbc.sql("update raid_reward_slot set status='RESULT_HELD' where current_attempt_id=:attempt")
            .param("attempt", first.attemptId).update()

        val terminalizer = RaidAttemptTerminalizer(repository, completion(handoff), RaidClock { now.plusSeconds(2) })
        assertEquals(0, terminalizer.terminalizeDue(now.plusSeconds(2)))
        assertEquals(1, handoff.resumes)
        assertEquals(false, repository.lockAttempt(RaidAttemptId(first.attemptId), RaidSessionId(first.sessionId), fixture.accountId).resumePending)
    }

    @Test
    fun `postgres stale session rejects new attempt`() {
        val fixture = fixture()
        jdbc.sql("update raid_session set status='SETTLING',cutoff_at=settles_at,settlement_phase='FREEZE_WORKSET' where id=:id").param("id", fixture.sessionId).update()
        assertFailsWith<IllegalArgumentException> { service(fixture.accountId).startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD) }
    }
    @Test
    fun `postgres concurrent same key creates and replays one attempt through transaction boundary`() {
        val fixture = fixture()
        val handoff = RecordingHandoff()
        val key = UUID.randomUUID()
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        try {
            val results = (1..2).map {
                pool.submit<RaidAttemptView> {
                    start.await()
                    transactionTemplate().execute { service(fixture.accountId, handoff).startAttempt(fixture.accountId, key, RaidAttemptMode.REWARD) }!!
                }
            }
            start.countDown()
            val successful = results.map { it.get() }
            assertEquals(1, successful.map { it.attemptId }.distinct().size)
            assertEquals(1, jdbc.sql("select count(*) from raid_attempt where account_id=:account").param("account", fixture.accountId).query(Int::class.java).single())
        } finally { pool.shutdownNow() }
    }

    @Test
    fun `postgres pause failure rolls back attempt slot state and command`() {
        val fixture = fixture()
        val failing = object : RecordingHandoff() {
            override fun pauseLockedWithHandoff(accountId: UUID, commandId: UUID): RaidBattleHandoff? = error("PAUSE_FAILED")
        }
        assertFailsWith<IllegalStateException> {
            transactionTemplate().execute { service(fixture.accountId, failing).startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD) }
        }
        assertEquals(0, jdbc.sql("select count(*) from raid_account_state where account_id=:account").param("account", fixture.accountId).query(Int::class.java).single())
        assertEquals(0, jdbc.sql("select count(*) from raid_reward_slot where account_id=:account").param("account", fixture.accountId).query(Int::class.java).single())
        assertEquals(0, jdbc.sql("select count(*) from raid_attempt where account_id=:account").param("account", fixture.accountId).query(Int::class.java).single())
        assertEquals(0, jdbc.sql("select count(*) from raid_command_record where account_id=:account").param("account", fixture.accountId).query(Int::class.java).single())
    }

    @Test
    fun `postgres expired handoff remains terminal and current query does not restart`() {
        val fixture = fixture()
        val handoff = RecordingHandoff()
        val attempt = service(fixture.accountId, handoff).startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        /* 시계는 얼려 두고 행은 Postgres 의 now() 로 적으면, 실제 시각이 그 시계를 넘긴
           오후에는 만료로 적은 시각이 되레 미래가 된다. 두 시각 모두 시험의 시계로 적는다. */
        jdbc.sql("update raid_attempt set started_at=:started,completable_at=:completable where id=:id")
            .params(mapOf("id" to attempt.attemptId, "started" to Timestamp.from(now.minusSeconds(2)), "completable" to Timestamp.from(now.minusSeconds(1)))).update()
        val current = service(fixture.accountId, handoff).currentAttempt(fixture.accountId)
        assertEquals(RaidAttemptStatus.RESULT_HELD, current?.status)
        assertEquals(RaidAttemptStatus.RESULT_HELD, repository.currentAttempt(fixture.accountId)?.status)
        assertEquals(1, handoff.resumes)
    }

    private data class Fixture(val accountId: UUID, val sessionId: UUID)

    private fun fixture(): Fixture {
        val accountId = UUID.randomUUID()
        val sessionId = UUID.randomUUID()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:id,:email,'test',1,:now)")
            .params(mapOf("id" to accountId, "email" to "$accountId@test.local", "now" to Timestamp.from(now))).update()
        jdbc.sql("insert into raid_session(id,content_version,reward_version,settles_at,status) values (:id,'raid-v1','reward-v1',:at,'OPEN')")
            .params(mapOf("id" to sessionId, "at" to Timestamp.from(now.plusSeconds(3600)))).update()
        jdbc.sql("insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version) values (:account,'stage.01-02',true,:at,1,'v1')")
            .params(mapOf("account" to accountId, "at" to Timestamp.from(now))).update()
        return Fixture(accountId, sessionId)
    }

    private fun service(accountId: UUID, handoff: RecordingHandoff = RecordingHandoff()): RaidService {
        val completion = completion(handoff)
        return RaidService(repository, RaidClock { now }, RaidCombatSnapshotProvider { snapshot }, RaidContentPort { content }, RaidProgressPort { _, _ -> true }, handoff, completion, mapper, NoopTestRewardPort, com.hanjjak.events.application.NoopDomainEventPublisher)
    }
    private fun completion(handoff: RaidMainBattlePort) = RaidAttemptCompletionService(repository, handoff, mapper)
    private fun transactionTemplate() = TransactionTemplate(DataSourceTransactionManager(dataSource))
    private object NoopTestRewardPort : RaidRewardPort {
        override fun preflightLocked(accountId: UUID, bundle: RaidRewardBundle) = Unit
        override fun creditLocked(accountId: UUID, sourceId: UUID, bundle: RaidRewardBundle) = Unit
    }


    private open class RecordingHandoff : RaidMainBattlePort {
        val order = mutableListOf<String>()
        var resumes = 0
        private val handoff = RaidBattleHandoff(UUID.randomUUID(), UUID.randomUUID(), "stage.01-01")
        override fun pauseLockedWithHandoff(accountId: UUID, commandId: UUID): RaidBattleHandoff? { order += "pause"; return handoff }
        override fun resumeLocked(accountId: UUID, commandId: UUID, handoff: RaidBattleHandoff): RaidBattleHandoff? { order += "resume"; resumes++; return null }
    }
}
