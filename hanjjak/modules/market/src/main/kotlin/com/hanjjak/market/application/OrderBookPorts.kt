package com.hanjjak.market.application

import com.hanjjak.market.domain.*
import java.time.Instant
import java.util.UUID

interface OrderBookRepository {
    data class StoredCommand(val commandId: UUID, val fingerprint: String, val resultJson: String)
    data class OrderCursor(val createdAt: Instant, val orderId: UUID)
    data class TradeCursor(val filledAt: Instant, val tradeId: UUID)
    data class MutationWindow(val mutationCount: Int, val activeOrderCount: Int)
    data class TradeInsert(
        val tradeId: UUID,
        val legacyListingId: UUID?,
        val makerOrderId: UUID,
        val takerOrderId: UUID,
        val instrumentId: UUID,
        val buyerAccountId: UUID,
        val sellerAccountId: UUID,
        val itemId: String,
        val displayName: String,
        val quantity: Long,
        val unitPrice: Long,
        val totalPrice: Long,
        val fee: Long,
        val settlementAmount: Long,
        val filledAt: Instant,
    )

    fun stateVersion(accountId: UUID): Long
    fun command(accountId: UUID, idempotencyKey: UUID): StoredCommand?
    fun saveCommand(commandId: UUID, accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String)

    fun synchronizeInstruments(instruments: List<MarketInstrument>, now: Instant)
    fun instruments(accountId: UUID): List<MarketInstrumentSummary>
    fun instrument(instrumentId: UUID): MarketInstrument?
    fun lockInstrument(instrumentId: UUID): MarketInstrument?
    fun incrementRevision(instrumentId: UUID): Long

    fun lockAccountControl(accountId: UUID, now: Instant): MutationWindow
    fun appendMutation(mutationId: UUID, accountId: UUID, instrumentId: UUID, orderId: UUID, side: MarketOrderSide, type: String, commandId: UUID, createdAt: Instant)

    fun order(orderId: UUID): MarketOrder?
    fun lockOrder(orderId: UUID): MarketOrder?
    fun createOrder(order: MarketOrder)
    fun updateOrder(order: MarketOrder)
    fun matchCandidates(instrumentId: UUID, incomingSide: MarketOrderSide, limitUnitPrice: Long, now: Instant): List<MarketOrder>
    fun crossingOwnOrder(instrumentId: UUID, accountId: UUID, incomingSide: MarketOrderSide, limitUnitPrice: Long, now: Instant): Boolean
    fun depth(instrumentId: UUID, accountId: UUID, side: MarketOrderSide, levels: Int, now: Instant): List<MarketDepthLevel>
    fun priceLevels(instrumentId: UUID, accountId: UUID, side: MarketOrderSide, afterUnitPrice: Long?, limit: Int, now: Instant): List<MarketPriceLevel>
    fun recentTrades(instrumentId: UUID, limit: Int): List<MarketRecentTrade>
    fun priceLevelCount(instrumentId: UUID, side: MarketOrderSide, now: Instant): Long
    fun candles(instrumentId: UUID, interval: MarketCandleInterval, from: Instant, to: Instant): List<MarketCandle>
    fun listOrders(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?, cursor: OrderCursor?, limit: Int): List<MarketOrderSummary>
    fun countOrders(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?): Long
    fun expiredOrders(now: Instant, limit: Int): List<MarketOrder>
    fun listAdminOrders(orderId: UUID?, instrumentId: UUID?, cursor: OrderCursor?, limit: Int): List<AdminMarketOrderSummary>
    fun ensureSystemMarketAccount(now: Instant): UUID
    fun isSystemMarketAccount(accountId: UUID): Boolean

    fun createTrade(trade: TradeInsert)
    fun listTrades(accountId: UUID?, instrumentIds: List<UUID>, cursor: TradeCursor?, limit: Int): List<MarketTradeView>
    fun countTrades(accountId: UUID?, instrumentIds: List<UUID>): Long

    fun createDelivery(delivery: MarketDelivery)
    fun listDeliveries(accountId: UUID, claimable: Boolean?, cursor: OrderCursor?, limit: Int): List<MarketDelivery>
    fun countDeliveries(accountId: UUID, claimable: Boolean?): Long
    fun lockDelivery(accountId: UUID, deliveryId: UUID): MarketDelivery?
    fun lockClaimableDeliveries(accountId: UUID): List<MarketDelivery>
    fun markDeliveriesClaimed(deliveryIds: List<UUID>, claimedAt: Instant)

    fun appendUnread(accountId: UUID, stream: MarketUnreadStream, sourceId: UUID, createdAt: Instant): Long
    fun unreadStates(accountId: UUID): List<MarketUnreadState>
    fun issueReadThrough(accountId: UUID, stream: MarketUnreadStream, sequence: Long)
    fun advanceReadCursor(accountId: UUID, stream: MarketUnreadStream, sequence: Long): Long

    fun migrateLegacyMarket(now: Instant): List<Pair<UUID, Long>>
}
