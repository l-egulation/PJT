package com.hanjjak.events.application

import java.time.Instant
import java.util.UUID

data class DomainEventPublishRequest(
    val eventType: String,
    val aggregateId: UUID,
    val occurredAt: Instant,
    val accountId: UUID? = null,
    val characterId: UUID? = null,
    val chapter: Int? = null,
    val stageId: String? = null,
    val itemId: String? = null,
    val quantity: Long? = null,
    val unitPrice: Long? = null,
    val totalPrice: Long? = null,
    val commandId: UUID? = null,
    val correlationId: UUID? = null,
    val payload: Map<String, Any?> = emptyMap(),
)

interface DomainEventPublisher {
    fun publish(
        eventType: String,
        aggregateId: UUID,
        occurredAt: Instant,
        accountId: UUID? = null,
        characterId: UUID? = null,
        chapter: Int? = null,
        stageId: String? = null,
        itemId: String? = null,
        quantity: Long? = null,
        unitPrice: Long? = null,
        totalPrice: Long? = null,
        commandId: UUID? = null,
        correlationId: UUID? = null,
        payload: Map<String, Any?> = emptyMap(),
    ): UUID = publish(DomainEventPublishRequest(eventType, aggregateId, occurredAt, accountId, characterId, chapter, stageId, itemId, quantity, unitPrice, totalPrice, commandId, correlationId, payload))

    fun publish(request: DomainEventPublishRequest): UUID

    fun publishAll(requests: List<DomainEventPublishRequest>): List<UUID> =
        requests.map(::publish)
}

object NoopDomainEventPublisher : DomainEventPublisher {
    override fun publish(request: DomainEventPublishRequest): UUID = UUID.randomUUID()

    override fun publishAll(requests: List<DomainEventPublishRequest>): List<UUID> =
        requests.map { UUID.randomUUID() }
}
