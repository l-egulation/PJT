package com.hanjjak.eventconsumers

import com.fasterxml.jackson.databind.ObjectMapper
import org.springframework.beans.factory.annotation.Value
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.kafka.core.KafkaTemplate
import org.springframework.scheduling.annotation.Scheduled
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.util.UUID
import java.util.concurrent.TimeUnit

@Service
@ConditionalOnProperty(prefix = "events.kafka.publisher", name = ["enabled"], havingValue = "true", matchIfMissing = true)
class OutboxKafkaPublisher(
    private val jdbc: JdbcClient,
    private val kafka: KafkaTemplate<String, String>,
    private val mapper: ObjectMapper,
    private val metrics: KafkaOpsMetrics,
    @param:Value("\${events.kafka.topic:domain-events.v1}") private val topic: String,
    @param:Value("\${events.kafka.publisher.batch-size:1000}") private val batchSize: Int,
)
{
    @Scheduled(fixedDelayString = "\${events.kafka.publisher.fixed-delay-ms:1000}")
    fun publishScheduled() {
        publishBatch()
    }

    fun publishBatch(): Int {
        val events = claimBatch()
        metrics.outboxClaimed(events.size)
        events.forEach { event ->
            runCatching {
                kafka.send(topic, event.key(mapper), event.payload).get(10, TimeUnit.SECONDS)
                markSent(event.eventId)
                metrics.outboxSent()
            }.onFailure {
                markRetry(event.eventId)
                if (event.attemptCount >= 5) metrics.outboxFailed() else metrics.outboxRetry()
            }
        }
        return events.size
    }

    @Transactional
    fun claimBatch(): List<PendingOutboxEvent> = jdbc.sql(
        """
        with picked as (
          select event_id
          from outbox_event
          where status='PENDING' and (next_retry_at is null or next_retry_at <= now())
          order by created_at, event_id
          limit :limit
          for update skip locked
        )
        update outbox_event event
        set attempt_count=attempt_count+1,
            next_retry_at=now() + interval '5 seconds'
        from picked
        where event.event_id=picked.event_id
        returning event.event_id,event.event_type,event.aggregate_id,event.payload::text,event.attempt_count
        """.trimIndent(),
    )
        .param("limit", batchSize)
        .query { row, _ ->
            PendingOutboxEvent(
                eventId = row.getObject("event_id", UUID::class.java),
                eventType = row.getString("event_type"),
                aggregateId = row.getObject("aggregate_id", UUID::class.java),
                payload = row.getString("payload"),
                attemptCount = row.getInt("attempt_count"),
            )
        }
        .list()

    private fun markSent(eventId: UUID) {
        jdbc.sql("update outbox_event set status='SENT', next_retry_at=null where event_id=:event")
            .param("event", eventId)
            .update()
    }

    private fun markRetry(eventId: UUID) {
        jdbc.sql("update outbox_event set status=case when attempt_count >= 5 then 'FAILED' else 'PENDING' end where event_id=:event")
            .param("event", eventId)
            .update()
    }
}

data class PendingOutboxEvent(
    val eventId: UUID,
    val eventType: String,
    val aggregateId: UUID,
    val payload: String,
    val attemptCount: Int,
) {
    fun key(mapper: ObjectMapper): String {
        val root = mapper.readTree(payload)
        return root.path("accountId").takeIf { it.isTextual }?.asText()
            ?: root.path("aggregateId").takeIf { it.isTextual }?.asText()
            ?: aggregateId.toString()
    }
}
