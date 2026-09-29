package com.hanjjak.admin.application

import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import java.time.Clock
import java.time.Instant
import java.util.UUID

@Service
class AdminEventService(
    private val jdbc: JdbcClient,
    private val clock: Clock,
) {
    data class OutboxEventSummary(
        val eventId: UUID,
        val eventType: String,
        val aggregateId: UUID,
        val status: String,
        val attemptCount: Int,
        val nextRetryAt: Instant?,
        val createdAt: Instant,
        val ageSeconds: Long,
    )

    fun outbox(status: String?, limit: Int): List<OutboxEventSummary> {
        val normalizedStatus = status?.trim()?.uppercase()?.takeIf { it.isNotBlank() }
        require(normalizedStatus == null || normalizedStatus in setOf("PENDING", "FAILED", "SENT")) { "ADMIN_INVALID_OUTBOX_STATUS" }
        val where = if (normalizedStatus == null) "" else "where status = :status"
        val query = jdbc.sql(
            """
            select event_id, event_type, aggregate_id, status, attempt_count, next_retry_at, created_at
            from outbox_event
            $where
            order by case status when 'FAILED' then 0 when 'PENDING' then 1 else 2 end, created_at desc, event_id desc
            limit :limit
            """.trimIndent(),
        ).param("limit", limit.coerceIn(1, 100))
        if (normalizedStatus != null) query.param("status", normalizedStatus)
        val now = clock.instant()
        return query.query { result, _ ->
            val createdAt = result.getTimestamp("created_at").toInstant()
            OutboxEventSummary(
                result.getObject("event_id", UUID::class.java),
                result.getString("event_type"),
                result.getObject("aggregate_id", UUID::class.java),
                result.getString("status"),
                result.getInt("attempt_count"),
                result.getTimestamp("next_retry_at")?.toInstant(),
                createdAt,
                (now.epochSecond - createdAt.epochSecond).coerceAtLeast(0),
            )
        }.list()
    }
    fun retry(eventId: UUID) {
        val updated = jdbc.sql("update outbox_event set status='PENDING', next_retry_at=now() where event_id=:event and status='FAILED'")
            .param("event", eventId).update()
        require(updated == 1) { "ADMIN_OUTBOX_RETRY_NOT_ALLOWED" }
    }
}
