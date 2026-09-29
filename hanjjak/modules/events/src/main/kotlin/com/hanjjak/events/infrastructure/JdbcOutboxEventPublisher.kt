package com.hanjjak.events.infrastructure

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublishRequest
import com.hanjjak.events.application.DomainEventPublisher
import org.springframework.jdbc.core.simple.JdbcClient
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID

class JdbcOutboxEventPublisher(
    private val jdbc: JdbcClient,
    private val mapper: ObjectMapper,
) : DomainEventPublisher {
    override fun publish(request: DomainEventPublishRequest): UUID =
        publishAll(listOf(request)).single()

    override fun publishAll(requests: List<DomainEventPublishRequest>): List<UUID> {
        if (requests.isEmpty()) return emptyList()
        val eventIds = requests.map { UUID.randomUUID() }
        val params = mutableMapOf<String, Any>()
        val values = requests.mapIndexed { index, request ->
            params["event$index"] = eventIds[index]
            params["type$index"] = request.eventType
            params["aggregate$index"] = request.aggregateId
            params["payload$index"] = mapper.writeValueAsString(envelope(eventIds[index], request))
            params["created$index"] = Timestamp.from(request.occurredAt)
            "(cast(:event$index as uuid), cast(:type$index as varchar), cast(:aggregate$index as uuid), cast(:payload$index as jsonb), 'PENDING', cast(:created$index as timestamptz))"
        }.joinToString(",")
        jdbc.sql("insert into outbox_event(event_id,event_type,aggregate_id,payload,status,created_at) values $values")
            .params(params)
            .update()
        return eventIds
    }

    private fun envelope(eventId: UUID, request: DomainEventPublishRequest): Map<String, Any?> = linkedMapOf<String, Any?>(
        "eventId" to eventId,
        "eventType" to request.eventType,
        "schemaVersion" to 1,
        "occurredAt" to request.occurredAt,
        "producer" to "game-api",
        "accountId" to request.accountId,
        "characterId" to request.characterId,
        "chapter" to request.chapter,
        "stageId" to request.stageId,
        "itemId" to request.itemId,
        "quantity" to request.quantity,
        "unitPrice" to request.unitPrice,
        "totalPrice" to request.totalPrice,
        "commandId" to request.commandId,
        "correlationId" to request.correlationId,
        "payload" to request.payload,
    ).filterValues { it != null }
}
