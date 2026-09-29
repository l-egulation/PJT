package com.hanjjak.raid.infrastructure
import com.fasterxml.jackson.databind.ObjectMapper

import com.hanjjak.raid.application.RaidRepository
import com.hanjjak.raid.domain.RaidAccountState
import com.hanjjak.raid.domain.RaidAttempt
import com.hanjjak.raid.domain.RaidAttemptId
import com.hanjjak.raid.domain.RaidAttemptMode
import com.hanjjak.raid.domain.RaidAttemptStatus
import com.hanjjak.raid.domain.RaidContribution
import com.hanjjak.raid.domain.RaidClaim
import com.hanjjak.raid.domain.RaidFinalRank
import com.hanjjak.raid.domain.RaidClaimId
import com.hanjjak.raid.domain.RaidClaimKind
import com.hanjjak.raid.domain.RaidClaimStatus
import com.hanjjak.raid.domain.RaidRewardBundle
import com.hanjjak.raid.domain.RaidRewardSlot
import com.hanjjak.raid.domain.RaidSession
import com.hanjjak.raid.domain.RaidSessionId
import com.hanjjak.raid.domain.RaidSessionStatus
import com.hanjjak.raid.domain.RaidSlotStatus
import org.flywaydb.core.Flyway
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.jdbc.datasource.DriverManagerDataSource
import org.springframework.jdbc.datasource.SingleConnectionDataSource
import org.testcontainers.containers.PostgreSQLContainer
import java.nio.file.Path
import java.sql.Connection
import java.sql.DriverManager
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.TimeoutException
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNotNull
import kotlin.test.assertNull
import kotlin.test.assertTrue

class JdbcRaidRepositoryTest {
    @Test
    fun `writes and reads raid state while preserving immutable session fields and caller terminal time`() {
        val fixture = fixture()
        val sessionId = RaidSessionId(UUID.randomUUID())
        val settlesAt = Instant.parse("2026-09-13T08:30:00Z")
        val session = RaidSession(sessionId, "raid-v1", "rewards-v1", settlesAt, RaidSessionStatus.OPEN)
        fixture.repository.saveSession(session)
        assertFailsWith<IllegalArgumentException> {
            fixture.repository.saveSession(
                session.copy(
                    contentVersion = "raid-v2",
                    rewardVersion = "rewards-v2",
                    settlesAt = settlesAt.plusSeconds(60),
                    status = RaidSessionStatus.SETTLING,
                    cutoffAt = settlesAt.plusSeconds(60),
                ),
            )
        }
        fixture.repository.saveSession(session.copy(status = RaidSessionStatus.SETTLING, cutoffAt = settlesAt))
        val savedSession = fixture.repository.lockSession(sessionId)
        assertEquals(RaidSessionStatus.SETTLING, savedSession.status)
        assertEquals("raid-v1", savedSession.contentVersion)
        assertEquals("rewards-v1", savedSession.rewardVersion)
        assertEquals(settlesAt, savedSession.settlesAt)

        val state = RaidAccountState(sessionId, fixture.accountId, 1, 0, 0, 1, false)
        fixture.repository.saveAccountState(state)
        val terminalAt = Instant.parse("2026-09-13T08:29:59Z")
        val slot = RaidRewardSlot(sessionId, fixture.accountId, 1, RaidSlotStatus.ACTIVE, 1)
        fixture.repository.saveSlot(slot)
        val attempt = RaidAttempt(
            RaidAttemptId(UUID.randomUUID()), sessionId, fixture.accountId, RaidAttemptMode.REWARD,
            slot.slotId, 1, RaidAttemptStatus.RUNNING, "{\"seed\":7}",
            "{\"damage\":10,\"grade\":\"D\",\"sealContribution\":100,\"endedAt\":\"2026-09-13T08:29:00Z\"}",
            "[{\"tick\":1}]", Instant.parse("2026-09-13T08:20:00Z"), Instant.parse("2026-09-13T08:25:00Z"), seed = 7,
        )
        fixture.repository.saveAttempt(attempt)
        fixture.repository.saveSlot(slot.copy(status = RaidSlotStatus.CONFIRMED, currentAttemptId = attempt.attemptId, terminalAt = terminalAt))

        val savedAttempt = fixture.repository.lockAttempt(attempt.attemptId, sessionId, fixture.accountId)
        assertEquals(attempt.attemptId, savedAttempt.attemptId)
        assertEquals(attempt.inputSnapshot.replace(" ", ""), savedAttempt.inputSnapshot.replace(" ", ""))
        assertEquals(attempt.result.replace(" ", "").let { ObjectMapper().readTree(it) }, ObjectMapper().readTree(savedAttempt.result))
        assertEquals(attempt.seed, savedAttempt.seed)
        assertEquals(terminalAt, fixture.repository.lockSlot(sessionId, fixture.accountId, 1).terminalAt)
        assertNotNull(fixture.repository.attemptResult(attempt.attemptId))
    }

    @Test
    fun `current and due queries return only eligible attempts in stable order`() {
        val fixture = fixture()
        val sessionId = RaidSessionId(UUID.randomUUID())
        val now = Instant.parse("2026-09-13T08:30:00Z")
        fixture.repository.saveSession(RaidSession(sessionId, "raid-v1", "rewards-v1", now.plusSeconds(60), RaidSessionStatus.OPEN))
        fixture.repository.saveAccountState(RaidAccountState(sessionId, fixture.accountId, 1, 0, 0, 1, false))
        val slot = RaidRewardSlot(sessionId, fixture.accountId, 1, RaidSlotStatus.ACTIVE, 1)
        fixture.repository.saveSlot(slot)
        val secondSlot = RaidRewardSlot(sessionId, fixture.accountId, 2, RaidSlotStatus.ACTIVE, 1)
        fixture.repository.saveSlot(secondSlot)
        val due = attempt(sessionId, fixture.accountId, slot.slotId, RaidAttemptStatus.RUNNING, now.minusSeconds(1), null)
        val current = attempt(sessionId, fixture.accountId, secondSlot.slotId, RaidAttemptStatus.RESULT_HELD, now, now.plusSeconds(1))
        fixture.repository.saveAttempt(due)
        fixture.repository.saveAttempt(current)
        assertEquals(current.attemptId, fixture.repository.currentAttempt(fixture.accountId)?.attemptId)
        assertEquals(listOf(due.attemptId), fixture.repository.dueAttempts(now).map(RaidAttempt::attemptId))
    }

    @Test
    fun `contribution lock blocks a concurrent writer until the owning transaction commits`() {
        val fixture = fixture()
        val sessionId = RaidSessionId(UUID.randomUUID())
        fixture.repository.saveSession(RaidSession(sessionId, "raid-v1", "rewards-v1", Instant.parse("2026-09-13T08:30:00Z"), RaidSessionStatus.OPEN))
        fixture.repository.upsertContribution(RaidContribution(sessionId, fixture.accountId, 100, 10, 1, 10))
        val connectionA = fixture.connection()
        val connectionB = fixture.connection()
        connectionA.autoCommit = false
        connectionB.autoCommit = false
        val repoA = JdbcRaidRepository(JdbcClient.create(SingleConnectionDataSource(connectionA, false)))
        val jdbcB = JdbcClient.create(SingleConnectionDataSource(connectionB, false))
        repoA.lockContribution(sessionId, fixture.accountId)
        val executor = Executors.newSingleThreadExecutor()
        try {
            val updateStarted = CountDownLatch(1)
            val update = executor.submit {
                updateStarted.countDown()
                jdbcB.sql("update raid_contribution set total_damage=total_damage+1 where session_id=:session and account_id=:account")
                    .params(mapOf("session" to sessionId.value, "account" to fixture.accountId)).update()
            }
            assertTrue(updateStarted.await(1, TimeUnit.SECONDS))
            assertFailsWith<TimeoutException> { update.get(100, TimeUnit.MILLISECONDS) }
            connectionA.commit()
            update.get(2, TimeUnit.SECONDS)
            connectionB.commit()
        } finally {
            executor.shutdownNow()
            connectionA.close()
            connectionB.close()
        }
        assertEquals(11, fixture.repository.lockContribution(sessionId, fixture.accountId).damage)
    }

    @Test
    fun `database rejects invalid cutoff states and mismatched current attempt ownership`() {
        val fixture = fixture()
        val session = UUID.randomUUID()
        val settlesAt = Instant.parse("2026-09-13T08:30:00Z")
        assertFailsWith<Exception> {
            fixture.jdbc.sql("insert into raid_session(id,content_version,reward_version,settles_at,status,cutoff_at) values (:id,'raid-v1','rewards-v1',:settles,'SETTLING',null)")
                .params(mapOf("id" to session, "settles" to Timestamp.from(settlesAt))).update()
        }
        fixture.jdbc.sql("insert into raid_session(id,content_version,reward_version,settles_at,status) values (:id,'raid-v1','rewards-v1',:settles,'OPEN')")
            .params(mapOf("id" to session, "settles" to Timestamp.from(settlesAt))).update()
        fixture.jdbc.sql("insert into raid_account_state(session_id,account_id,current_slot_ordinal,slots_terminal,reward_attempts_started,state_version,auto_finalized) values (:session,:account,1,0,0,1,false)")
            .params(mapOf("session" to session, "account" to fixture.accountId)).update()
        val slot = UUID.randomUUID()
        fixture.jdbc.sql("insert into raid_reward_slot(id,session_id,account_id,ordinal,status,attempts_started) values (:id,:session,:account,1,'ACTIVE',0)")
            .params(mapOf("id" to slot, "session" to session, "account" to fixture.accountId)).update()
        val attempt = UUID.randomUUID()
        fixture.jdbc.sql("insert into raid_attempt(id,session_id,account_id,mode,slot_id,attempt_ordinal,status,input_snapshot,result_json,timeline_json,seed,started_at,completable_at) values (:id,:session,:account,'REWARD',:slot,1,'RUNNING','{}','{}','{}',1,:started,:completable)")
            .params(mapOf("id" to attempt, "session" to session, "account" to fixture.accountId, "slot" to slot, "started" to Timestamp.from(settlesAt), "completable" to Timestamp.from(settlesAt.plusSeconds(1)))).update()
        assertFailsWith<Exception> {
            fixture.jdbc.sql("update raid_reward_slot set current_attempt_id=:attempt where id=:slot")
                .params(mapOf("attempt" to UUID.randomUUID(), "slot" to slot)).update()
        }
        fixture.jdbc.sql("update raid_reward_slot set current_attempt_id=:attempt where id=:slot")
            .params(mapOf("attempt" to attempt, "slot" to slot)).update()
        assertEquals(attempt, fixture.jdbc.sql("select current_attempt_id from raid_reward_slot where id=:slot").param("slot", slot).query(UUID::class.java).single())
    }

    @Test
    fun `attempt transition updates only the owned attempt`() {
        val fixture = fixture()
        val sessionId = RaidSessionId(UUID.randomUUID())
        val now = Instant.parse("2026-09-13T08:30:00Z")
        fixture.repository.saveSession(RaidSession(sessionId, "raid-v1", "rewards-v1", now.plusSeconds(60), RaidSessionStatus.OPEN))
        fixture.repository.saveAccountState(RaidAccountState(sessionId, fixture.accountId, 1, 0, 0, 1, false))
        val otherAccountId = UUID.randomUUID()
        fixture.jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:id,:email,'test',1,:created)")
            .params(mapOf("id" to otherAccountId, "email" to "$otherAccountId@test.local", "created" to Timestamp.from(now))).update()
        fixture.repository.saveAccountState(RaidAccountState(sessionId, otherAccountId, 1, 0, 0, 1, false))
        val firstSlot = RaidRewardSlot(sessionId, fixture.accountId, 1, RaidSlotStatus.ACTIVE, 1)
        val otherSlot = RaidRewardSlot(sessionId, otherAccountId, 1, RaidSlotStatus.ACTIVE, 1)
        fixture.repository.saveSlot(firstSlot)
        fixture.repository.saveSlot(otherSlot)
        val first = attempt(sessionId, fixture.accountId, firstSlot.slotId, RaidAttemptStatus.RUNNING, now, null)
        val other = attempt(sessionId, otherAccountId, otherSlot.slotId, RaidAttemptStatus.RUNNING, now, null)
        fixture.repository.saveAttempt(first)
        fixture.repository.saveAttempt(other)

        fixture.repository.saveAttempt(first.copy(status = RaidAttemptStatus.RESULT_HELD, endedAt = now.plusSeconds(1)))

        assertEquals(RaidAttemptStatus.RESULT_HELD, fixture.repository.lockAttempt(first.attemptId, sessionId, fixture.accountId).status)
        assertEquals(RaidAttemptStatus.RUNNING, fixture.repository.lockAttempt(other.attemptId, sessionId, otherAccountId).status)
    }

    @Test
    fun `settlement workset and claim transitions are guarded and idempotent`() {
        val fixture = fixture()
        val repository: RaidRepository = fixture.repository
        val sessionId = RaidSessionId(UUID.randomUUID())
        val cutoff = Instant.parse("2026-09-13T08:30:00Z")
        repository.saveSession(RaidSession(sessionId, "raid-v1", "rewards-v1", cutoff, RaidSessionStatus.OPEN))
        assertEquals(listOf(sessionId), repository.dueOpenSessions(cutoff).map(RaidSession::sessionId))
        repository.saveSession(RaidSession(sessionId, "raid-v1", "rewards-v1", cutoff, RaidSessionStatus.SETTLING, cutoff))
        repository.saveAccountState(RaidAccountState(sessionId, fixture.accountId, 1, 0, 0, 1, false))
        assertEquals(1, repository.freezeSettlementWorkset(sessionId, cutoff))
        assertEquals(0, repository.freezeSettlementWorkset(sessionId, cutoff))
        assertEquals(listOf(fixture.accountId), repository.settlementAccounts(sessionId))
        assertEquals(cutoff, repository.lockSettlementAccount(sessionId, fixture.accountId).cutoffAt)
        assertFailsWith<Exception> {
            fixture.jdbc.sql("update raid_settlement_account set cutoff_at=:wrong where session_id=:session and account_id=:account")
                .params(mapOf("wrong" to Timestamp.from(cutoff.minusSeconds(1)), "session" to sessionId.value, "account" to fixture.accountId)).update()
        }
        assertTrue(repository.finalizeSettlementAccount(sessionId, fixture.accountId, cutoff.plusSeconds(1)))
        assertTrue(!repository.finalizeSettlementAccount(sessionId, fixture.accountId, cutoff.plusSeconds(2)))
        repository.saveFinalRank(com.hanjjak.raid.domain.RaidFinalRank(sessionId, fixture.accountId, 1, 100, 10, 10))
        val claim = RaidClaim(
            RaidClaimId(UUID.randomUUID()), sessionId, fixture.accountId, RaidClaimKind.DAILY_RANK, sessionId.value,
            RaidClaimStatus.CLAIMABLE, "rewards-v1", RaidRewardBundle(1, 2, 3), cutoff,
        )
        repository.insertClaim(claim)
        assertTrue(repository.markClaimed(claim.claimId, fixture.accountId, cutoff.plusSeconds(1)))
        assertTrue(!repository.markClaimed(claim.claimId, fixture.accountId, cutoff.plusSeconds(2)))
        assertEquals(RaidClaimStatus.CLAIMED, repository.lockClaim(claim.claimId, fixture.accountId).status)
    }

    @Test
    fun `claim sources must reference earned result or final rank for the same owner`() {
        val fixture = fixture()
        val repository = fixture.repository
        val now = Instant.parse("2026-09-13T08:30:00Z")
        val sessionId = RaidSessionId(UUID.randomUUID())
        repository.saveSession(RaidSession(sessionId, "raid-v1", "rewards-v1", now.plusSeconds(60), RaidSessionStatus.OPEN))

        val arbitraryAuto = RaidClaim(
            RaidClaimId(UUID.randomUUID()), sessionId, fixture.accountId, RaidClaimKind.AUTO_PERSONAL, UUID.randomUUID(),
            RaidClaimStatus.CLAIMABLE, "rewards-v1", RaidRewardBundle(1, 0, 0), now,
        )
        assertFailsWith<Exception> { repository.insertClaim(arbitraryAuto) }

        val missingRank = RaidClaim(
            RaidClaimId(UUID.randomUUID()), sessionId, fixture.accountId, RaidClaimKind.DAILY_RANK, sessionId.value,
            RaidClaimStatus.CLAIMABLE, "rewards-v1", RaidRewardBundle(0, 1, 0), now,
        )
        assertFailsWith<Exception> { repository.insertClaim(missingRank) }

        repository.saveFinalRank(RaidFinalRank(sessionId, fixture.accountId, 1, 100, 10, 10))
        val arbitraryDaily = missingRank.copy(claimId = RaidClaimId(UUID.randomUUID()), sourceId = UUID.randomUUID())
        assertFailsWith<Exception> { repository.insertClaim(arbitraryDaily) }
        repository.insertClaim(missingRank.copy(claimId = RaidClaimId(UUID.randomUUID())))
    }

    @Test
    fun `save slot rejects ownership changes attempt decreases and terminal reopening`() {
        val fixture = fixture()
        val repository = fixture.repository
        val now = Instant.parse("2026-09-13T08:30:00Z")
        val sessionId = RaidSessionId(UUID.randomUUID())
        repository.saveSession(RaidSession(sessionId, "raid-v1", "rewards-v1", now.plusSeconds(60), RaidSessionStatus.OPEN))
        repository.saveAccountState(RaidAccountState(sessionId, fixture.accountId, 1, 0, 0, 1, false))

        val slot = RaidRewardSlot(sessionId, fixture.accountId, 1, RaidSlotStatus.AVAILABLE, 0)
        repository.saveSlot(slot)
        repository.saveSlot(slot.copy(status = RaidSlotStatus.ACTIVE, attemptsStarted = 1))
        repository.saveSlot(slot.copy(status = RaidSlotStatus.RESULT_HELD, attemptsStarted = 1))
        repository.saveSlot(slot.copy(status = RaidSlotStatus.ACTIVE, attemptsStarted = 2))
        assertFailsWith<IllegalArgumentException> {
            repository.saveSlot(slot.copy(status = RaidSlotStatus.ACTIVE, attemptsStarted = 1))
        }.also { assertEquals("RAID_SLOT_STATE_CONFLICT", it.message) }

        repository.saveSlot(slot.copy(status = RaidSlotStatus.CONFIRMED, attemptsStarted = 2, terminalAt = now))
        assertFailsWith<IllegalArgumentException> {
            repository.saveSlot(slot.copy(status = RaidSlotStatus.ACTIVE, attemptsStarted = 2))
        }.also { assertEquals("RAID_SLOT_STATE_CONFLICT", it.message) }

        val otherAccount = UUID.randomUUID()
        fixture.jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:id,:email,'test',1,:created)")
            .params(mapOf("id" to otherAccount, "email" to "$otherAccount@test.local", "created" to Timestamp.from(now))).update()
        repository.saveAccountState(RaidAccountState(sessionId, otherAccount, 1, 0, 0, 1, false))
        assertFailsWith<IllegalArgumentException> {
            repository.saveSlot(slot.copy(accountId = otherAccount, status = RaidSlotStatus.CONFIRMED, attemptsStarted = 2, terminalAt = now))
        }.also { assertEquals("RAID_SLOT_STATE_CONFLICT", it.message) }
    }

    @Test
    fun `save account state requires next version and monotonic fields`() {
        val fixture = fixture()
        val repository = fixture.repository
        val sessionId = RaidSessionId(UUID.randomUUID())
        repository.saveSession(RaidSession(sessionId, "raid-v1", "rewards-v1", Instant.parse("2026-09-13T08:30:00Z"), RaidSessionStatus.OPEN))
        val initial = RaidAccountState(sessionId, fixture.accountId, 1, 0, 0, 1, false)
        repository.saveAccountState(initial)
        assertFailsWith<IllegalArgumentException> { repository.saveAccountState(initial) }
            .also { assertEquals("RAID_ACCOUNT_STATE_CONFLICT", it.message) }
        assertFailsWith<IllegalArgumentException> { repository.saveAccountState(initial.copy(currentSlotOrdinal = 2)) }
            .also { assertEquals("RAID_ACCOUNT_STATE_CONFLICT", it.message) }

        val advanced = initial.copy(currentSlotOrdinal = 2, slotsTerminal = 1, rewardAttemptsStarted = 1, stateVersion = 2)
        repository.saveAccountState(advanced)
        assertFailsWith<IllegalArgumentException> {
            repository.saveAccountState(advanced.copy(stateVersion = 3, currentSlotOrdinal = 1, rewardAttemptsStarted = 0))
        }.also { assertEquals("RAID_ACCOUNT_STATE_CONFLICT", it.message) }
        repository.saveAccountState(advanced.copy(stateVersion = 3, autoFinalized = true))
        assertFailsWith<IllegalArgumentException> {
            repository.saveAccountState(advanced.copy(stateVersion = 4, autoFinalized = false))
        }.also { assertEquals("RAID_ACCOUNT_STATE_CONFLICT", it.message) }
    }

    private fun attempt(
        sessionId: RaidSessionId,
        accountId: UUID,
        slotId: UUID,
        status: RaidAttemptStatus,
        startedAt: Instant,
        endedAt: Instant?,
    ) = RaidAttempt(
        RaidAttemptId(UUID.randomUUID()), sessionId, accountId, RaidAttemptMode.REWARD, slotId, 1, status,
        "{}", "{\"damage\":1,\"grade\":\"D\",\"sealContribution\":100,\"endedAt\":\"2026-09-13T08:31:00Z\"}", "[]",
        startedAt, startedAt.plusSeconds(1), endedAt,
    )

    private fun fixture(): Fixture {
        val base = RaidTestDatabase.connection
        val schema = "raid_repo_${UUID.randomUUID().toString().replace("-", "")}"
        DriverManager.getConnection(base.jdbcUrl, base.username, base.password).use { connection ->
            connection.createStatement().use { it.execute("create schema $schema") }
        }
        val url = "${base.jdbcUrl}${if ('?' in base.jdbcUrl) '&' else '?'}currentSchema=$schema"
        Flyway.configure().dataSource(url, base.username, base.password).schemas(schema).defaultSchema(schema)
            .locations("filesystem:${migrationDirectory()}").load().migrate()
        val dataSource = DriverManagerDataSource(url, base.username, base.password)
        val jdbc = JdbcClient.create(dataSource)
        val accountId = UUID.randomUUID()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:id,:email,'test',1,:created)")
            .params(mapOf("id" to accountId, "email" to "$accountId@test.local", "created" to Timestamp.from(Instant.parse("2026-01-01T00:00:00Z")))).update()
        return Fixture(RaidTestDatabase.Connection(url, base.username, base.password), accountId, jdbc)
    }

    private fun migrationDirectory(): Path = Path.of("../..", "apps/game-api/src/main/resources/db/migration").toAbsolutePath()

    private data class Fixture(val database: RaidTestDatabase.Connection, val accountId: UUID, val jdbc: JdbcClient) {
        val repository = JdbcRaidRepository(jdbc)
        fun connection(): Connection = DriverManager.getConnection(database.jdbcUrl, database.username, database.password)
    }

    private object RaidTestDatabase {
        data class Connection(val jdbcUrl: String, val username: String, val password: String)

        private val container: PostgreSQLContainer<Nothing>? by lazy {
            if (System.getenv("TEST_DATABASE_URL").isNullOrBlank()) PostgreSQLContainer<Nothing>("postgres:17.11-alpine").apply { start() } else null
        }
        val connection: Connection by lazy {
            val external = System.getenv("TEST_DATABASE_URL")?.trim().orEmpty()
            if (external.isNotEmpty()) Connection(external, requireNotNull(System.getenv("TEST_DATABASE_USER")), requireNotNull(System.getenv("TEST_DATABASE_PASSWORD")))
            else requireNotNull(container).let { Connection(it.jdbcUrl, it.username, it.password) }
        }
    }
}
