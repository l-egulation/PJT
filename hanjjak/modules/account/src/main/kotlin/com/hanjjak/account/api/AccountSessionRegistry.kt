package com.hanjjak.account.api
import com.hanjjak.account.application.AuthenticationSessionLifecycle

import org.springframework.context.event.EventListener
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.session.events.SessionDestroyedEvent
import org.springframework.stereotype.Component
import java.time.Clock
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Component
class AccountSessionRegistry(
    private val jdbc: JdbcClient,
    private val clock: Clock,
) : AuthenticationSessionLifecycle {
    fun activate(accountId: UUID, sessionId: String) {
        jdbc.sql(
            """
            insert into account_active_session(account_id, session_id, activated_at)
            values (:accountId, :sessionId, :activatedAt)
            on conflict (account_id) do update
            set session_id = excluded.session_id, activated_at = excluded.activated_at
            """.trimIndent(),
        )
            .param("accountId", accountId)
            .param("sessionId", sessionId)
            .param("activatedAt", OffsetDateTime.ofInstant(clock.instant(), ZoneOffset.UTC))
            .update()
    }

    fun isActive(accountId: UUID, sessionId: String): Boolean = jdbc.sql(
        "select count(*) from account_active_session where account_id = :accountId and session_id = :sessionId",
    )
        .param("accountId", accountId)
        .param("sessionId", sessionId)
        .query(Int::class.java)
        .single() == 1

    fun deactivate(accountId: UUID, sessionId: String) {
        jdbc.sql("delete from account_active_session where account_id = :accountId and session_id = :sessionId")
            .param("accountId", accountId)
            .param("sessionId", sessionId)
            .update()
    }

    override fun closeForPasswordReset(accountId: UUID) {
        val sessionId = jdbc.sql("select session_id from account_active_session where account_id = :accountId")
            .param("accountId", accountId)
            .query(String::class.java)
            .optional()
            .orElse(null)
        jdbc.sql("delete from account_active_session where account_id = :accountId")
            .param("accountId", accountId)
            .update()
        if (sessionId != null) {
            jdbc.sql("delete from spring_session where session_id = :sessionId")
                .param("sessionId", sessionId)
                .update()
        }
    }

    @EventListener
    fun sessionDestroyed(event: SessionDestroyedEvent) {
        jdbc.sql("delete from account_active_session where session_id = :sessionId")
            .param("sessionId", event.sessionId)
            .update()
    }
}
