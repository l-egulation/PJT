package com.hanjjak.market.domain

import com.fasterxml.jackson.annotation.JsonInclude
import java.nio.ByteBuffer
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Instant
import java.util.UUID

const val MIN_MARKET_UNIT_PRICE = 10L
const val MARKET_ORDER_DURATION_HOURS = 8L
const val MAX_ORDER_BOOK_UNIT_PRICE = 999_999L
const val MAX_ACTIVE_MARKET_ORDERS = 30
const val MAX_MARKET_MUTATIONS_PER_MINUTE = 30

enum class MarketInstrumentStatus { ACTIVE, CANCELLING, INACTIVE }
enum class MarketOrderSide { BUY, SELL }
enum class MarketTimeInForce { GTC, IOC }
enum class MarketOrderStatus { ACTIVE, PARTIALLY_FILLED, PARTIALLY_FILLED_CLOSED, FILLED, CANCELLED, EXPIRED, RECOVERY_REVIEW }
enum class MarketDeliverySource { BUY_FILL, SELL_CANCEL_RETURN, SELL_EXPIRE_RETURN }
enum class MarketUnreadStream { FILLS, DELIVERIES, SETTLEMENTS, EXPIRATIONS }
enum class MarketOrderScope { ACTIVE, CLOSED, RECOVERY_REVIEW }
enum class MarketCandleInterval(val bucket: String, val bucketSeconds: Long) {
    FIVE_MINUTES("5m", 5 * 60),
    FIFTEEN_MINUTES("15m", 15 * 60),
    THIRTY_MINUTES("30m", 30 * 60);

    companion object {
        fun parse(value: String): MarketCandleInterval = entries.firstOrNull { it.bucket == value }
            ?: throw IllegalArgumentException("INVALID_CANDLE_INTERVAL")
    }
}

enum class MarketCandlePeriod(val bucket: String, val duration: java.time.Duration) {
    TWELVE_HOURS("12h", java.time.Duration.ofHours(12)),
    DAY("1d", java.time.Duration.ofDays(1)),
    TWO_DAYS("2d", java.time.Duration.ofDays(2));

    companion object {
        fun parse(value: String): MarketCandlePeriod = entries.firstOrNull { it.bucket == value }
            ?: throw IllegalArgumentException("INVALID_CANDLE_PERIOD")
    }
}

data class MarketInstrument(
    val instrumentId: UUID,
    val canonicalKey: String,
    val itemId: String,
    val displayName: String,
    val category: String,
    val attributes: Map<String, String>,
    val status: MarketInstrumentStatus,
    val marketRevision: Long,
)

data class MarketInstrumentSummary(
    val instrumentId: UUID,
    val canonicalKey: String,
    val itemId: String,
    val displayName: String,
    val category: String,
    val attributes: Map<String, String>,
    val status: MarketInstrumentStatus,
    val bestBidUnitPrice: Long?,
    val bestAskUnitPrice: Long?,
    val lastTradeUnitPrice: Long?,
    val marketRevision: Long,
)

data class MarketOrder(
    val orderId: UUID,
    val accountId: UUID,
    val instrumentId: UUID,
    val side: MarketOrderSide,
    val timeInForce: MarketTimeInForce,
    val initialQuantity: Long,
    val filledQuantity: Long,
    val remainingQuantity: Long,
    val limitUnitPrice: Long,
    val reservedRice: Long,
    val status: MarketOrderStatus,
    val priorityAt: Instant,
    val createdAt: Instant,
    val updatedAt: Instant,
    val expiresAt: Instant?,
    val displayNameSnapshot: String,
    val instanceIds: List<UUID> = emptyList(),
    val legacyListingId: UUID? = null,
)

data class MarketDepthLevel(val unitPrice: Long, val totalQuantity: Long, val cumulativeQuantity: Long, val myQuantity: Long)
data class MarketPriceLevel(val unitPrice: Long, val totalQuantity: Long, val cumulativeQuantity: Long, val myQuantity: Long, val cumulativeOtherQuantity: Long)
@JsonInclude(JsonInclude.Include.ALWAYS)
data class MarketPriceLevels(val instrumentId: UUID, val side: MarketOrderSide, val marketRevision: Long, val items: List<MarketPriceLevel>, val nextUnitPrice: Long?, val totalLevels: Long)
data class MarketRecentTrade(val tradeId: UUID, val quantity: Long, val unitPrice: Long, val totalPrice: Long, val filledAt: Instant)
data class MarketCandle(val openedAt: Instant, val open: Long, val high: Long, val low: Long, val close: Long, val quantity: Long, val totalPrice: Long)
data class MarketCandles(val instrumentId: UUID, val interval: String, val from: Instant, val to: Instant, val marketRevision: Long, val items: List<MarketCandle>)

data class MarketOrderBook(
    val instrumentId: UUID,
    val marketRevision: Long,
    val bids: List<MarketDepthLevel>,
    val asks: List<MarketDepthLevel>,
    val bestBidUnitPrice: Long?,
    val bestAskUnitPrice: Long?,
    val spread: Long?,
    val recentTrades: List<MarketRecentTrade>,
)

data class MarketOrderQuote(
    val instrumentId: UUID,
    val side: MarketOrderSide,
    val timeInForce: MarketTimeInForce,
    val requestedQuantity: Long,
    val limitUnitPrice: Long,
    val expectedFilledQuantity: Long,
    val expectedRemainingQuantity: Long,
    val lowestFilledUnitPrice: Long?,
    val highestFilledUnitPrice: Long?,
    val weightedAverageUnitPrice: Double?,
    val totalPrice: Long,
    val expectedFee: Long,
    val expectedSettlementAmount: Long,
    val availableRice: Long,
    val availableQuantity: Long,
    val marketRevision: Long,
)

data class CreateMarketOrderRequest(
    val instrumentId: UUID,
    val side: MarketOrderSide,
    val quantity: Long,
    val limitUnitPrice: Long,
    val timeInForce: MarketTimeInForce,
    val instanceIds: List<UUID> = emptyList(),
)

data class UpdateMarketOrderRequest(
    val quantity: Long? = null,
    val limitUnitPrice: Long? = null,
)

data class MarketOrderFill(
    val tradeId: UUID,
    val makerOrderId: UUID,
    val takerOrderId: UUID,
    val quantity: Long,
    val unitPrice: Long,
    val totalPrice: Long,
    val fee: Long,
    val settlementAmount: Long,
)

data class MarketOrderResult(
    val orderId: UUID,
    val instrumentId: UUID,
    val side: MarketOrderSide,
    val timeInForce: MarketTimeInForce,
    val initialQuantity: Long,
    val filledQuantity: Long,
    val remainingQuantity: Long,
    val limitUnitPrice: Long,
    val reservedRice: Long,
    val status: MarketOrderStatus,
    val priorityAt: Instant,
    val expiresAt: Instant?,
    val totalPrice: Long,
    val lowestFilledUnitPrice: Long?,
    val highestFilledUnitPrice: Long?,
    val weightedAverageUnitPrice: Double?,
    val marketRevision: Long,
    val fills: List<MarketOrderFill>,
)

data class MarketOrderSummary(
    val orderId: UUID,
    val instrumentId: UUID,
    val itemId: String,
    val displayName: String,
    val side: MarketOrderSide,
    val timeInForce: MarketTimeInForce,
    val initialQuantity: Long,
    val filledQuantity: Long,
    val remainingQuantity: Long,
    val limitUnitPrice: Long,
    val reservedRice: Long,
    val status: MarketOrderStatus,
    val priorityAt: Instant,
    val createdAt: Instant,
    val updatedAt: Instant,
    val expiresAt: Instant?,
)

data class MarketOrderCancelled(
    val orderId: UUID,
    val instrumentId: UUID,
    val side: MarketOrderSide,
    val status: MarketOrderStatus,
    val refundedRice: Long,
    val deliveryId: UUID?,
    val releasedQuantity: Long,
    val marketRevision: Long,
)

data class MarketDelivery(
    val deliveryId: UUID,
    val accountId: UUID,
    val instrumentId: UUID,
    val orderId: UUID,
    val tradeId: UUID?,
    val source: MarketDeliverySource,
    val itemId: String,
    val instanceIds: List<UUID>,
    val quantity: Long,
    val displayName: String,
    val createdAt: Instant,
    val claimedAt: Instant?,
    val sourceAccountId: UUID? = null,
)

data class MarketDeliveryClaimed(val deliveryId: UUID, val itemId: String, val quantity: Long, val claimedAt: Instant)
data class MarketDeliveryClaimAllResult(val claimed: List<MarketDeliveryClaimed>, val remaining: List<MarketDelivery>, val usedSlots: Int, val maxSlots: Int)
data class MarketUnreadState(val stream: MarketUnreadStream, val latestSequence: Long, val readSequence: Long, val unreadCount: Long)
data class MarketSummary(val activeOrders: Long, val closedOrders: Long, val recoveryReviewOrders: Long, val claimableDeliveries: Long, val claimableSettlements: Long, val unread: List<MarketUnreadState>)
data class MarketReadCursorResult(val stream: MarketUnreadStream, val readSequence: Long)
data class MarketPage<T>(val items: List<T>, val nextCursor: String?, val totalItems: Long, val readThroughSequence: Long = 0)

data class MarketTradeView(
    val tradeId: UUID,
    val instrumentId: UUID,
    val itemId: String,
    val displayName: String,
    val side: MarketOrderSide?,
    val quantity: Long,
    val unitPrice: Long,
    val totalPrice: Long,
    val fee: Long?,
    val settlementAmount: Long?,
    val filledAt: Instant,
)

data class AdminMarketOrderSummary(
    val orderId: UUID,
    val accountId: UUID,
    val instrumentId: UUID,
    val itemId: String,
    val displayName: String,
    val side: MarketOrderSide,
    val initialQuantity: Long,
    val remainingQuantity: Long,
    val limitUnitPrice: Long,
    val status: MarketOrderStatus,
    val createdAt: Instant,
    val updatedAt: Instant,
    val expiresAt: Instant?,
    val systemOrder: Boolean,
)

data class AdminMarketOrderPage(val items: List<AdminMarketOrderSummary>, val nextCursor: String?)
data class AdminMarketCancelResult(val orderId: UUID, val itemId: String, val cancelledQuantity: Long, val status: MarketOrderStatus, val marketRevision: Long)
data class AdminMarketPurchaseResult(val tradeId: UUID, val orderId: UUID, val itemId: String, val quantity: Long, val unitPrice: Long, val totalPrice: Long, val fee: Long, val settlementAmount: Long, val remainingOrderQuantity: Long, val orderStatus: MarketOrderStatus, val marketRevision: Long)
data class AdminMarketSellResult(val itemId: String, val totalQuantity: Long, val unitPrice: Long, val orderIds: List<UUID>, val marketRevision: Long)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class MarketCommand<T>(val commandId: UUID, val idempotencyKey: UUID, val status: String = "SUCCEEDED", val result: T)

object MarketInstrumentIds {
    private val namespace = uuidV5(UUID(0L, 0L), "urn:hanjjak:market-instrument")

    fun canonicalKey(itemId: String, stackKey: String? = null): String {
        val normalized = itemId.trim().lowercase()
        return when {
            normalized.startsWith("potato_") || normalized.startsWith("sweet_potato_") || normalized.startsWith("corn_") -> "material:$normalized"
            normalized.startsWith("skillbook:") -> normalized
            normalized.startsWith("gem:") -> {
                val parts = normalized.split(":")
                val option = if (parts.getOrNull(2) == "haste") "attack_speed" else parts.getOrNull(2)
                val value = stackKey?.substringAfterLast(':')?.toIntOrNull()
                    ?: throw IllegalArgumentException("GEM_VALUE_REQUIRED")
                "gem:${parts.getOrNull(1) ?: throw IllegalArgumentException("INVALID_MARKET_ITEM")}:$option:$value"
            }
            else -> throw IllegalArgumentException("ITEM_NOT_TRADEABLE")
        }
    }

    fun id(canonicalKey: String): UUID = uuidV5(namespace, canonicalKey)

    private fun uuidV5(namespace: UUID, name: String): UUID {
        val digest = MessageDigest.getInstance("SHA-1")
        digest.update(ByteBuffer.allocate(16).putLong(namespace.mostSignificantBits).putLong(namespace.leastSignificantBits).array())
        val bytes = digest.digest(name.toByteArray(StandardCharsets.UTF_8)).copyOf(16)
        bytes[6] = ((bytes[6].toInt() and 0x0f) or 0x50).toByte()
        bytes[8] = ((bytes[8].toInt() and 0x3f) or 0x80).toByte()
        val buffer = ByteBuffer.wrap(bytes)
        return UUID(buffer.long, buffer.long)
    }
}
