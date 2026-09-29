package com.hanjjak.market.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublishRequest
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.domain.ItemCategory
import com.hanjjak.market.domain.AdminMarketCancelResult
import com.hanjjak.market.domain.AdminMarketOrderPage
import com.hanjjak.market.domain.AdminMarketPurchaseResult
import com.hanjjak.market.domain.AdminMarketSellResult
import com.hanjjak.market.domain.MARKET_ORDER_DURATION_HOURS
import com.hanjjak.market.domain.MAX_ORDER_BOOK_UNIT_PRICE
import com.hanjjak.market.domain.MIN_MARKET_UNIT_PRICE
import com.hanjjak.market.domain.MarketOrder
import com.hanjjak.market.domain.MarketOrderSide
import com.hanjjak.market.domain.MarketOrderStatus
import com.hanjjak.market.domain.MarketTimeInForce
import com.hanjjak.wallet.application.WalletService
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.time.temporal.ChronoUnit
import java.util.Base64
import java.util.UUID

/** Administrative order-book commands. System liquidity uses a non-login account. */
open class AdminMarketService(
    private val repository: OrderBookRepository,
    private val wallet: WalletService,
    private val catalog: ItemCatalog,
    private val mapper: ObjectMapper,
    private val clock: Clock,
    private val events: DomainEventPublisher,
) {
    @Transactional
    open fun orders(query: String?, cursor: String?): AdminMarketOrderPage {
        val systemAccountId = repository.ensureSystemMarketAccount(clock.instant())
        val normalized = query?.trim()?.takeIf(String::isNotEmpty)
        val orderId = normalized?.let { runCatching { UUID.fromString(it) }.getOrNull() }
        val instrumentId = if (normalized != null && orderId == null) {
            requireMarketItem(normalized)
            repository.instruments(systemAccountId).firstOrNull { it.itemId == normalized }?.instrumentId
                ?: throw IllegalArgumentException("MARKET_INSTRUMENT_NOT_FOUND")
        } else null
        val rows = repository.listAdminOrders(orderId, instrumentId, cursor?.let(::decodeCursor), 101)
        val visible = rows.take(100)
        return AdminMarketOrderPage(visible, rows.getOrNull(100)?.let { encodeCursor(visible.last().createdAt, visible.last().orderId) })
    }

    @Transactional
    open fun cancelOrder(orderId: UUID, idempotencyKey: UUID, reason: String): AdminMarketCancelResult {
        val normalizedReason = requireReason(reason)
        val systemAccountId = repository.ensureSystemMarketAccount(clock.instant())
        val fingerprint = sha256("admin-market-cancel\u0000$orderId\u0000$normalizedReason")
        replay<AdminMarketCancelResult>(systemAccountId, idempotencyKey, fingerprint)?.let { return it }
        val existing = repository.order(orderId) ?: throw IllegalArgumentException("MARKET_ORDER_NOT_FOUND")
        repository.lockInstrument(existing.instrumentId) ?: throw IllegalArgumentException("MARKET_INSTRUMENT_NOT_FOUND")
        val order = repository.lockOrder(orderId) ?: throw IllegalArgumentException("MARKET_ORDER_NOT_FOUND")
        require(order.status in ACTIVE_STATUSES && order.remainingQuantity > 0) { "MARKET_ORDER_NOT_ACTIVE" }
        val now = clock.instant()
        val closed = order.copy(remainingQuantity = 0, reservedRice = 0, status = MarketOrderStatus.CANCELLED, updatedAt = now)
        repository.updateOrder(closed)
        val revision = repository.incrementRevision(order.instrumentId)
        val commandId = UUID.randomUUID()
        repository.appendMutation(UUID.randomUUID(), systemAccountId, order.instrumentId, orderId, order.side, "CANCEL", commandId, now)
        val itemId = repository.instrument(order.instrumentId)?.itemId ?: throw IllegalArgumentException("MARKET_INSTRUMENT_NOT_FOUND")
        val result = AdminMarketCancelResult(orderId, itemId, order.remainingQuantity, closed.status, revision)
        events.publish(
            eventType = "MARKET_ORDER_CANCELLED",
            aggregateId = orderId,
            occurredAt = now,
            accountId = order.accountId,
            itemId = itemId,
            quantity = order.remainingQuantity,
            unitPrice = order.limitUnitPrice,
            commandId = commandId,
            payload = mapOf(
                "orderId" to orderId,
                "instrumentId" to order.instrumentId,
                "side" to order.side.name,
                "timeInForce" to order.timeInForce.name,
                "marketRevision" to revision,
                "reason" to "ADMIN_CANCELLED",
                "adminReason" to normalizedReason,
            ),
        )
        repository.saveCommand(commandId, systemAccountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return result
    }

    @Transactional
    open fun purchaseOrder(orderId: UUID, idempotencyKey: UUID, requestedQuantity: Long?, reason: String): AdminMarketPurchaseResult {
        val normalizedReason = requireReason(reason)
        if (requestedQuantity != null) require(requestedQuantity > 0) { "INVALID_QUANTITY" }
        val systemAccountId = repository.ensureSystemMarketAccount(clock.instant())
        val fingerprint = sha256("admin-market-purchase\u0000$orderId\u0000${requestedQuantity ?: "ALL"}\u0000$normalizedReason")
        replay<AdminMarketPurchaseResult>(systemAccountId, idempotencyKey, fingerprint)?.let { return it }
        val existing = repository.order(orderId) ?: throw IllegalArgumentException("MARKET_ORDER_NOT_FOUND")
        repository.lockInstrument(existing.instrumentId) ?: throw IllegalArgumentException("MARKET_INSTRUMENT_NOT_FOUND")
        val maker = repository.lockOrder(orderId) ?: throw IllegalArgumentException("MARKET_ORDER_NOT_FOUND")
        require(maker.side == MarketOrderSide.SELL && maker.status in ACTIVE_STATUSES && maker.remainingQuantity > 0) { "MARKET_ORDER_NOT_ACTIVE" }
        require(maker.accountId != systemAccountId) { "SYSTEM_ORDER_NOT_PURCHASABLE" }
        val instrument = repository.instrument(maker.instrumentId) ?: throw IllegalArgumentException("MARKET_INSTRUMENT_NOT_FOUND")
        val quantity = requestedQuantity ?: maker.remainingQuantity
        require(quantity in 1..maker.remainingQuantity) { "INVALID_QUANTITY" }
        val totalPrice = Math.multiplyExact(quantity, maker.limitUnitPrice)
        val fee = totalPrice / 10
        val settlementAmount = totalPrice - fee
        val now = clock.instant()
        val takerOrderId = UUID.randomUUID()
        repository.createOrder(
            MarketOrder(
                takerOrderId, systemAccountId, maker.instrumentId, MarketOrderSide.BUY, MarketTimeInForce.IOC,
                quantity, quantity, 0, maker.limitUnitPrice, 0, MarketOrderStatus.FILLED,
                now, now, now, null, instrument.displayName,
            ),
        )
        val remaining = maker.remainingQuantity - quantity
        val makerStatus = if (remaining == 0L) MarketOrderStatus.FILLED else MarketOrderStatus.PARTIALLY_FILLED
        repository.updateOrder(maker.copy(filledQuantity = maker.filledQuantity + quantity, remainingQuantity = remaining, status = makerStatus, updatedAt = now))
        val tradeId = UUID.randomUUID()
        repository.createTrade(
            OrderBookRepository.TradeInsert(
                tradeId, maker.legacyListingId, maker.orderId, takerOrderId, maker.instrumentId, systemAccountId, maker.accountId,
                instrument.itemId, instrument.displayName, quantity, maker.limitUnitPrice, totalPrice, fee, settlementAmount, now,
            ),
        )
        wallet.credit(maker.accountId, settlementAmount, "ADMIN_MARKET_PURCHASE", tradeId)
        val revision = repository.incrementRevision(maker.instrumentId)
        val commandId = UUID.randomUUID()
        repository.appendMutation(UUID.randomUUID(), systemAccountId, maker.instrumentId, takerOrderId, MarketOrderSide.BUY, "CREATE", commandId, now)
        val result = AdminMarketPurchaseResult(tradeId, orderId, instrument.itemId, quantity, maker.limitUnitPrice, totalPrice, fee, settlementAmount, remaining, makerStatus, revision)
        events.publish(
            eventType = "MARKET_TRADE_COMPLETED",
            aggregateId = tradeId,
            occurredAt = now,
            accountId = systemAccountId,
            itemId = instrument.itemId,
            quantity = quantity,
            unitPrice = maker.limitUnitPrice,
            totalPrice = totalPrice,
            commandId = commandId,
            payload = mapOf(
                "tradeId" to tradeId,
                "makerOrderId" to maker.orderId,
                "takerOrderId" to takerOrderId,
                "instrumentId" to maker.instrumentId,
                "buyerAccountId" to systemAccountId,
                "sellerAccountId" to maker.accountId,
                "fee" to fee,
                "settlementAmount" to settlementAmount,
                "marketRevision" to revision,
                "source" to "ADMIN_SYSTEM_PURCHASE",
                "adminReason" to normalizedReason,
            ),
        )
        repository.saveCommand(commandId, systemAccountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return result
    }

    @Transactional
    open fun sell(itemId: String, totalQuantity: Long, unitPrice: Long, idempotencyKey: UUID, reason: String): AdminMarketSellResult {
        requireMarketItem(itemId)
        require(totalQuantity > 0) { "INVALID_QUANTITY" }
        require(unitPrice in MIN_MARKET_UNIT_PRICE..MAX_ORDER_BOOK_UNIT_PRICE) { "INVALID_UNIT_PRICE" }
        val normalizedReason = requireReason(reason)
        val now = clock.instant()
        val systemAccountId = repository.ensureSystemMarketAccount(now)
        val instrument = repository.instruments(systemAccountId).firstOrNull { it.itemId == itemId }
            ?: throw IllegalArgumentException("MARKET_INSTRUMENT_NOT_FOUND")
        repository.lockInstrument(instrument.instrumentId) ?: throw IllegalArgumentException("MARKET_INSTRUMENT_NOT_FOUND")
        val fingerprint = sha256("admin-market-sell\u0000$itemId\u0000$totalQuantity\u0000$unitPrice\u0000$normalizedReason")
        replay<AdminMarketSellResult>(systemAccountId, idempotencyKey, fingerprint)?.let { return it }
        val orderId = UUID.randomUUID()
        val order = MarketOrder(
            orderId, systemAccountId, instrument.instrumentId, MarketOrderSide.SELL, MarketTimeInForce.GTC,
            totalQuantity, 0, totalQuantity, unitPrice, 0, MarketOrderStatus.ACTIVE,
            now, now, now, now.plus(MARKET_ORDER_DURATION_HOURS, ChronoUnit.HOURS), instrument.displayName,
        )
        repository.createOrder(order)
        val revision = repository.incrementRevision(instrument.instrumentId)
        val commandId = UUID.randomUUID()
        repository.appendMutation(UUID.randomUUID(), systemAccountId, instrument.instrumentId, orderId, MarketOrderSide.SELL, "CREATE", commandId, now)
        val result = AdminMarketSellResult(itemId, totalQuantity, unitPrice, listOf(orderId), revision)
        events.publishAll(
            listOf(
                DomainEventPublishRequest(
                    eventType = "MARKET_ORDER_CREATED",
                    aggregateId = orderId,
                    occurredAt = now,
                    accountId = systemAccountId,
                    itemId = itemId,
                    quantity = totalQuantity,
                    unitPrice = unitPrice,
                    commandId = commandId,
                    payload = mapOf(
                        "orderId" to orderId,
                        "instrumentId" to instrument.instrumentId,
                        "side" to MarketOrderSide.SELL.name,
                        "timeInForce" to MarketTimeInForce.GTC.name,
                        "marketRevision" to revision,
                        "source" to "ADMIN_SYSTEM_ORDER",
                        "adminReason" to normalizedReason,
                    ),
                ),
            ),
        )
        repository.saveCommand(commandId, systemAccountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return result
    }

    private fun requireMarketItem(itemId: String) {
        val definition = catalog.require(itemId)
        require(definition.tradeable && definition.stackable && definition.category in SYSTEM_ORDER_CATEGORIES) { "ITEM_NOT_TRADEABLE" }
    }

    private fun requireReason(reason: String): String = reason.trim().also {
        require(it.length in 3..500) { "ADMIN_MARKET_REASON_REQUIRED" }
    }

    private inline fun <reified T> replay(accountId: UUID, key: UUID, fingerprint: String): T? {
        val stored = repository.command(accountId, key) ?: return null
        require(stored.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
        return mapper.readValue(stored.resultJson, T::class.java)
    }

    private fun encodeCursor(createdAt: java.time.Instant, orderId: UUID): String = Base64.getUrlEncoder().withoutPadding()
        .encodeToString("$createdAt|$orderId".toByteArray(StandardCharsets.UTF_8))

    private fun decodeCursor(cursor: String): OrderBookRepository.OrderCursor = runCatching {
        val parts = String(Base64.getUrlDecoder().decode(cursor), StandardCharsets.UTF_8).split("|")
        require(parts.size == 2)
        OrderBookRepository.OrderCursor(java.time.Instant.parse(parts[0]), UUID.fromString(parts[1]))
    }.getOrElse { throw IllegalArgumentException("INVALID_CURSOR") }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }

    companion object {
        private val ACTIVE_STATUSES = setOf(MarketOrderStatus.ACTIVE, MarketOrderStatus.PARTIALLY_FILLED)
        private val SYSTEM_ORDER_CATEGORIES = setOf(ItemCategory.MATERIAL, ItemCategory.SKILL_BOOK)
    }
}
