package com.hanjjak.market.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.application.MarketInventoryService
import com.hanjjak.market.domain.*
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.time.Instant
import java.util.Base64
import java.util.UUID

open class MarketDeliveryService(
    private val repository: OrderBookRepository,
    private val inventory: MarketInventoryService,
    private val catalog: ItemCatalog,
    private val mapper: ObjectMapper,
    private val clock: Clock,
    private val mail: com.hanjjak.mail.application.MailService,
) {
    open fun list(accountId: UUID, claimable: Boolean?, cursor: String?): MarketPage<MarketDelivery> {
        val rows = repository.listDeliveries(accountId, claimable, decodeCursor(cursor), 31)
        val visible = rows.take(30)
        val readThrough = repository.unreadStates(accountId).firstOrNull { it.stream == MarketUnreadStream.DELIVERIES }?.latestSequence ?: 0
        repository.issueReadThrough(accountId, MarketUnreadStream.DELIVERIES, readThrough)
        return MarketPage(visible, if (rows.size > 30) visible.lastOrNull()?.let { encodeCursor(it.createdAt, it.deliveryId) } else null, repository.countDeliveries(accountId, claimable), readThrough)
    }

    @Transactional
    open fun claim(accountId: UUID, deliveryId: UUID, idempotencyKey: UUID): MarketCommand<MarketDeliveryClaimed> {
        val fingerprint = fingerprint("market-delivery-claim", deliveryId)
        repository.command(accountId, idempotencyKey)?.let { stored ->
            require(stored.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return MarketCommand(stored.commandId, idempotencyKey, result = mapper.readValue(stored.resultJson, MarketDeliveryClaimed::class.java))
        }
        val delivery = repository.lockDelivery(accountId, deliveryId) ?: throw IllegalArgumentException("MARKET_DELIVERY_NOT_FOUND")
        require(delivery.claimedAt == null) { "MARKET_DELIVERY_ALREADY_CLAIMED" }
        grant(delivery)
        val claimedAt = clock.instant()
        repository.markDeliveriesClaimed(listOf(deliveryId), claimedAt)
        val result = MarketDeliveryClaimed(deliveryId, delivery.itemId, delivery.quantity, claimedAt)
        val commandId = UUID.randomUUID()
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return MarketCommand(commandId, idempotencyKey, result = result)
    }

    @Transactional
    open fun claimAll(accountId: UUID, idempotencyKey: UUID): MarketCommand<MarketDeliveryClaimAllResult> {
        val fingerprint = fingerprint("market-delivery-claim-all")
        repository.command(accountId, idempotencyKey)?.let { stored ->
            require(stored.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return MarketCommand(stored.commandId, idempotencyKey, result = mapper.readValue(stored.resultJson, MarketDeliveryClaimAllResult::class.java))
        }
        val claimedAt = clock.instant()
        val claimed = mutableListOf<MarketDeliveryClaimed>()
        val remaining = mutableListOf<MarketDelivery>()
        repository.lockClaimableDeliveries(accountId).forEach { delivery ->
            try {
                grant(delivery)
                claimed += MarketDeliveryClaimed(delivery.deliveryId, delivery.itemId, delivery.quantity, claimedAt)
            } catch (error: IllegalArgumentException) {
                if (error.message != "INVENTORY_CAPACITY_EXCEEDED") throw error
                remaining += delivery
            }
        }
        repository.markDeliveriesClaimed(claimed.map { it.deliveryId }, claimedAt)
        val status = inventory.status(accountId)
        val result = MarketDeliveryClaimAllResult(claimed, remaining, status.usedSlots, status.maxSlots)
        val commandId = UUID.randomUUID()
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return MarketCommand(commandId, idempotencyKey, result = result)
    }

    open fun summary(accountId: UUID): MarketSummary = MarketSummary(
        activeOrders = repository.countOrders(accountId, MarketOrderScope.ACTIVE, null, null),
        closedOrders = repository.countOrders(accountId, MarketOrderScope.CLOSED, null, null),
        recoveryReviewOrders = repository.countOrders(accountId, MarketOrderScope.RECOVERY_REVIEW, null, null),
        claimableDeliveries = repository.countDeliveries(accountId, true),
        claimableSettlements = mail.count(accountId, true),
        unread = repository.unreadStates(accountId),
    )

    @Transactional
    open fun markRead(accountId: UUID, stream: MarketUnreadStream, readThroughSequence: Long): MarketReadCursorResult {
        require(readThroughSequence >= 0) { "INVALID_UNREAD_SEQUENCE" }
        return MarketReadCursorResult(stream, repository.advanceReadCursor(accountId, stream, readThroughSequence))
    }

    private fun grant(delivery: MarketDelivery) {
        if (catalog.require(delivery.itemId).stackable) {
            inventory.releaseReservation(delivery.accountId, delivery.itemId, delivery.quantity)
        } else if (delivery.sourceAccountId == null || delivery.sourceAccountId == delivery.accountId) {
            inventory.deliverInstances(delivery.accountId, delivery.instanceIds)
        } else {
            inventory.transferReservedInstances(delivery.sourceAccountId, delivery.accountId, delivery.instanceIds)
        }
    }

    private fun fingerprint(vararg values: Any?): String = MessageDigest.getInstance("SHA-256").digest(values.joinToString("\u0000").toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
    private fun encodeCursor(at: Instant, id: UUID) = Base64.getUrlEncoder().withoutPadding().encodeToString("$at|$id".toByteArray())
    private fun decodeCursor(value: String?): OrderBookRepository.OrderCursor? {
        if (value == null) return null
        return runCatching { String(Base64.getUrlDecoder().decode(value)).split("|").let { OrderBookRepository.OrderCursor(Instant.parse(it[0]), UUID.fromString(it[1])) } }.getOrElse { throw IllegalArgumentException("INVALID_CURSOR") }
    }
}

data class AdvanceMarketReadCursorRequest(val readThroughSequence: Long)
