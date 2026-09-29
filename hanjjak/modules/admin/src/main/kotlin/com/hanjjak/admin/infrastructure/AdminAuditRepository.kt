package com.hanjjak.admin.infrastructure

import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Repository
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID

@Repository
class AdminAuditRepository(private val jdbc: JdbcClient, private val transactionManager: org.springframework.transaction.PlatformTransactionManager) {
    data class AuditEvent(
        val auditId: UUID,
        val occurredAt: Instant,
        val operatorId: UUID?,
        val username: String,
        val action: String,
        val targetType: String,
        val targetId: String?,
        val outcome: String,
        val requestId: UUID,
        val remoteAddress: String,
        val mutation: Boolean,
        val reason: String?,
        val beforeSummary: String?,
        val afterSummary: String?,
        val idempotencyKey: UUID?,
    )

    data class Record(
        val occurredAt: Instant,
        val operatorId: UUID?,
        val username: String,
        val action: String,
        val targetType: String,
        val targetId: String?,
        val outcome: String,
        val requestId: UUID,
        val remoteAddress: String,
        val mutation: Boolean = false,
        val reason: String? = null,
        val beforeSummary: String? = null,
        val afterSummary: String? = null,
        val idempotencyKey: UUID? = null,
    )

    fun record(
        occurredAt: Instant,
        operatorId: UUID?,
        username: String,
        action: String,
        targetType: String,
        targetId: String?,
        outcome: String,
        requestId: UUID,
        remoteAddress: String,
    ) = record(Record(occurredAt, operatorId, username, action, targetType, targetId, outcome, requestId, remoteAddress))

    fun record(record: Record) {
        jdbc.sql(
            """
            insert into admin_audit_event(
              audit_id, occurred_at, operator_id, username, action, target_type, target_id, outcome,
              request_id, remote_address, mutation, reason, before_summary, after_summary, idempotency_key
            ) values (
              :auditId, :occurredAt, :operatorId, :username, :action, :targetType, :targetId, :outcome,
              :requestId, :remoteAddress, :mutation, :reason, :beforeSummary, :afterSummary, :idempotencyKey
            )
            """.trimIndent(),
        )
            .param("auditId", UUID.randomUUID())
            .param("occurredAt", Timestamp.from(record.occurredAt))
            .param("operatorId", record.operatorId)
            .param("username", record.username.take(255))
            .param("action", record.action.take(120))
            .param("targetType", record.targetType.take(80))
            .param("targetId", record.targetId?.take(160))
            .param("outcome", record.outcome)
            .param("requestId", record.requestId)
            .param("remoteAddress", record.remoteAddress.take(64))
            .param("mutation", record.mutation)
            .param("reason", record.reason?.take(500))
            .param("beforeSummary", record.beforeSummary)
            .param("afterSummary", record.afterSummary)
            .param("idempotencyKey", record.idempotencyKey)
            .update()
    }

    fun list(limit: Int, operatorId: UUID? = null, action: String? = null, mutationsOnly: Boolean = false): List<AuditEvent> {
        val predicates = buildList {
            if (operatorId != null) add("operator_id = :operatorId")
            if (!action.isNullOrBlank()) add("action = :action")
            if (mutationsOnly) add("mutation = true")
        }
        var query = jdbc.sql(
            """
            select audit_id, occurred_at, operator_id, username, action, target_type, target_id, outcome,
                   request_id, remote_address, mutation, reason, before_summary, after_summary, idempotency_key
            from admin_audit_event
            ${if (predicates.isEmpty()) "" else "where ${predicates.joinToString(" and ")}"}
            order by occurred_at desc, audit_id desc
            limit :limit
            """.trimIndent(),
        ).param("limit", limit)
        if (operatorId != null) query = query.param("operatorId", operatorId)
        if (!action.isNullOrBlank()) query = query.param("action", action.trim())
        return query.query { result, _ ->
            AuditEvent(
                result.getObject("audit_id", UUID::class.java),
                result.getTimestamp("occurred_at").toInstant(),
                result.getObject("operator_id", UUID::class.java),
                result.getString("username"),
                result.getString("action"),
                result.getString("target_type"),
                result.getString("target_id"),
                result.getString("outcome"),
                result.getObject("request_id", UUID::class.java),
                result.getString("remote_address"),
                result.getBoolean("mutation"),
                result.getString("reason"),
                result.getString("before_summary"),
                result.getString("after_summary"),
                result.getObject("idempotency_key", UUID::class.java),
            )
        }.list()
    }

    fun recordFailure(record: Record) {
        org.springframework.transaction.support.TransactionTemplate(transactionManager).apply {
            propagationBehavior = org.springframework.transaction.TransactionDefinition.PROPAGATION_REQUIRES_NEW
        }.executeWithoutResult { record(record) }
    }
}
