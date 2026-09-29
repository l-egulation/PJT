package com.hanjjak.battle.application

import com.hanjjak.account.application.GameSessionLifecycle
import com.hanjjak.account.application.OfflineRewardLifecycle
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.scheduling.annotation.Scheduled
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Service
class GameSessionService(
    private val jdbc: JdbcClient,
    private val clock: Clock,
    private val offlineRewards: OfflineRewardLifecycle,
) : GameSessionLifecycle {
    data class Session(val id: UUID, val accountId: UUID, val status: String, val lastHeartbeatAt: Instant)

    @Transactional
    fun open(accountId: UUID, idempotencyKey: UUID): Session = command(accountId, idempotencyKey, "open") {
        resumeFresh(accountId)?.let { return@command it }
        settleActiveOfflineJob(accountId)
        closeActive(accountId, "REPLACED")
        closeActiveBattles(accountId)
        val session = Session(UUID.randomUUID(), accountId, "ACTIVE", clock.instant())
        jdbc.sql("insert into game_session(id,account_id,status,created_at,last_heartbeat_at) values (:id,:account,'ACTIVE',:now,:now)")
            .params(mapOf("id" to session.id, "account" to accountId, "now" to now())).update()
        offlineRewards.startForSession(accountId, session.id)
        session
    }

    @Transactional
    fun heartbeat(accountId: UUID, sessionId: UUID, idempotencyKey: UUID): Session =
        command(accountId, idempotencyKey, "heartbeat:$sessionId") {
            val changed = jdbc.sql("""update game_session set last_heartbeat_at=:now
                where id=:id and account_id=:account and status='ACTIVE' and last_heartbeat_at>=:deadline""")
                .params(mapOf("now" to now(), "deadline" to deadline(), "id" to sessionId, "account" to accountId)).update()
            require(changed == 1) { "GAME_SESSION_NOT_ACTIVE" }
            val heartbeatAt = clock.instant()
            offlineRewards.touchForSession(accountId, sessionId, heartbeatAt)
            Session(sessionId, accountId, "ACTIVE", heartbeatAt)
        }

    @Transactional
    fun close(accountId: UUID, sessionId: UUID, idempotencyKey: UUID): Session =
        command(accountId, idempotencyKey, "close:$sessionId") {
            val changed = jdbc.sql("update game_session set status='CLOSED',closed_at=:now where id=:id and account_id=:account and status='ACTIVE'")
                .params(mapOf("now" to now(), "id" to sessionId, "account" to accountId)).update()
            require(changed == 1) { "GAME_SESSION_NOT_ACTIVE" }
            offlineRewards.cancelForSession(accountId, sessionId)
            Session(sessionId, accountId, "CLOSED", clock.instant())
        }

    fun requireActive(accountId: UUID, sessionId: UUID) {
        val heartbeatAt = clock.instant()
        val active = jdbc.sql("""update game_session set last_heartbeat_at=:now
            where id=:id and account_id=:account and status='ACTIVE' and last_heartbeat_at>=:deadline""")
            .params(mapOf("now" to utc(heartbeatAt), "id" to sessionId, "account" to accountId, "deadline" to deadline())).update()
        require(active == 1) { "GAME_SESSION_NOT_ACTIVE" }
        offlineRewards.touchForSession(accountId, sessionId, heartbeatAt)
    }

    /** Caller already holds the account lock; this row lock fences expiry while a raid resumes. */
    fun isActiveForUpdate(accountId: UUID, sessionId: UUID): Boolean = jdbc.sql(
        """select status='ACTIVE' and last_heartbeat_at>=:deadline
           from game_session where id=:id and account_id=:account for update""",
    ).params(mapOf("id" to sessionId, "account" to accountId, "deadline" to deadline()))
        .query(Boolean::class.java).optional().orElse(false)

    fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId).query(Long::class.java).single()

    @Transactional
    override fun replaceForLogin(accountId: UUID) {
        lockAccount(accountId)
        settleActiveOfflineJob(accountId)
        closeActive(accountId, "REPLACED")
        closeActiveBattles(accountId)
    }

    @Transactional
    override fun closeForLogout(accountId: UUID) {
        lockAccount(accountId)
        closeActive(accountId, "CLOSED")
        offlineRewards.markDisconnectedForAccount(accountId, clock.instant())
        closeActiveBattles(accountId)
    }

    @Transactional
    override fun closeForPasswordReset(accountId: UUID) {
        lockAccount(accountId)
        offlineRewards.cancelForAccount(accountId)
        closeActive(accountId, "CLOSED")
        closeActiveBattles(accountId)
    }

    private fun closeActiveBattles(accountId: UUID) {
        if (!jdbc.sql("select to_regclass('battle_session') is not null").query(Boolean::class.java).single()) return
        jdbc.sql("update battle_session set status='ABORTED',closed_at=:now where account_id=:account and status='ACTIVE'")
            .params(mapOf("now" to now(), "account" to accountId)).update()
    }

    @Scheduled(fixedDelay = 30_000)
    @Transactional
    fun expireSilentSessions(): Int {
        val current = clock.instant()
        return jdbc.sql("update game_session set status='EXPIRED',closed_at=:now where status='ACTIVE' and last_heartbeat_at<:deadline")
            .params(mapOf("now" to utc(current), "deadline" to offlineRewardDeadline())).update()
    }

    private fun command(accountId: UUID, key: UUID, fingerprint: String, action: () -> Session): Session {
        lockAccount(accountId)
        val stored = jdbc.sql("""select fingerprint,game_session_id,result_status,result_heartbeat_at
            from game_session_command where account_id=:account and idempotency_key=:key""")
            .params(mapOf("account" to accountId, "key" to key)).query { row, _ ->
                require(row.getString("fingerprint") == sha256(fingerprint)) { "IDEMPOTENCY_KEY_REUSED" }
                Session(row.getObject("game_session_id", UUID::class.java), accountId, row.getString("result_status"), row.getObject("result_heartbeat_at", OffsetDateTime::class.java).toInstant())
            }.optional().orElse(null)
        if (stored != null) return stored
        val result = action()
        jdbc.sql("""insert into game_session_command(account_id,idempotency_key,fingerprint,game_session_id,result_status,result_heartbeat_at,created_at)
            values (:account,:key,:fingerprint,:session,:status,:heartbeat,:now)""")
            .params(mapOf("account" to accountId, "key" to key, "fingerprint" to sha256(fingerprint), "session" to result.id, "status" to result.status, "heartbeat" to utc(result.lastHeartbeatAt), "now" to now())).update()
        return result
    }

    private fun resumeFresh(accountId: UUID): Session? {
        val sessionId = jdbc.sql("""select id from game_session where account_id=:account and status='ACTIVE'
            and last_heartbeat_at>=:deadline order by created_at desc limit 1 for update""")
            .params(mapOf("account" to accountId, "deadline" to offlineRewardDeadline()))
            .query(UUID::class.java)
            .optional()
            .orElse(null)
            ?: return null
        val heartbeatAt = clock.instant()
        jdbc.sql("update game_session set last_heartbeat_at=:now where id=:id")
            .params(mapOf("id" to sessionId, "now" to utc(heartbeatAt))).update()
        offlineRewards.touchForSession(accountId, sessionId, heartbeatAt)
        return Session(sessionId, accountId, "ACTIVE", heartbeatAt)
    }

    private fun settleActiveOfflineJob(accountId: UUID) {
        val session = jdbc.sql("""select session.status,session.last_heartbeat_at
            from offline_job job join game_session session on session.id=job.game_session_id
            where job.account_id=:account and job.status='ACTIVE'
            order by job.created_at desc limit 1 for update of job""")
            .param("account", accountId)
            .query { row, _ -> row.getString("status") to row.getObject("last_heartbeat_at", OffsetDateTime::class.java).toInstant() }
            .optional()
            .orElse(null)
            ?: return
        val disconnected = session.first in setOf("CLOSED", "EXPIRED") ||
            session.first == "ACTIVE" && session.second.isBefore(clock.instant().minusSeconds(OfflineRewardPolicy.BUCKET_SECONDS))
        if (disconnected) offlineRewards.freezeForAccount(accountId, "SESSION_REPLACED")
        else offlineRewards.cancelForAccount(accountId)
    }

    private fun closeActive(accountId: UUID, status: String) {
        jdbc.sql("update game_session set status=:status,closed_at=:now where account_id=:account and status='ACTIVE'")
            .params(mapOf("status" to status, "now" to now(), "account" to accountId)).update()
    }

    private fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).single()
    }

    private fun now(): OffsetDateTime = utc(clock.instant())
    private fun deadline(): OffsetDateTime = offlineRewardDeadline()
    private fun offlineRewardDeadline(): OffsetDateTime = utc(clock.instant().minusSeconds(OfflineRewardPolicy.BUCKET_SECONDS))
    private fun utc(value: Instant): OffsetDateTime = OffsetDateTime.ofInstant(value, ZoneOffset.UTC)
    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256").digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
}
