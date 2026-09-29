package com.hanjjak.market.api

import com.hanjjak.market.application.AdvanceMarketReadCursorRequest
import com.hanjjak.market.application.MarketDeliveryService
import com.hanjjak.market.application.OrderBookService
import com.hanjjak.market.domain.*
import jakarta.servlet.http.HttpSession
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/market")
class MarketController(
    private val orders: OrderBookService,
    private val deliveries: MarketDeliveryService,
    private val clock: Clock,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping("/instruments")
    fun instruments(session: HttpSession) = account(session).let { success(it, orders.instruments(it)) }

    @GetMapping("/order-books/{instrumentId}")
    fun orderBook(@PathVariable instrumentId: UUID, @RequestParam(defaultValue = "5") levels: Int, session: HttpSession) =
        account(session).let { success(it, orders.orderBook(it, instrumentId, levels)) }

    @GetMapping("/instruments/{instrumentId}/price-levels")
    fun priceLevels(
        @PathVariable instrumentId: UUID,
        @RequestParam side: MarketOrderSide,
        @RequestParam(required = false) afterUnitPrice: Long?,
        @RequestParam(defaultValue = "10") limit: Int,
        session: HttpSession,
    ) = account(session).let { success(it, orders.priceLevels(it, instrumentId, side, afterUnitPrice, limit)) }

    @GetMapping("/instruments/{instrumentId}/candles")
    fun candles(
        @PathVariable instrumentId: UUID,
        @RequestParam(defaultValue = "5m") interval: String,
        @RequestParam(defaultValue = "12h") period: String,
        session: HttpSession,
    ) = account(session).let { success(it, orders.candles(it, instrumentId, MarketCandleInterval.parse(interval), MarketCandlePeriod.parse(period))) }

    @GetMapping("/instruments/{instrumentId}/order-quote")
    fun quote(
        @PathVariable instrumentId: UUID,
        @RequestParam side: MarketOrderSide,
        @RequestParam timeInForce: MarketTimeInForce,
        @RequestParam quantity: Long,
        @RequestParam limitUnitPrice: Long,
        session: HttpSession,
    ) = account(session).let { success(it, orders.quote(it, instrumentId, side, timeInForce, quantity, limitUnitPrice)) }

    @PostMapping("/orders")
    fun create(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestBody request: CreateMarketOrderRequest,
        session: HttpSession,
    ) = account(session).let { success(it, orders.create(it, idempotencyKey, request)) }

    @GetMapping("/orders/me")
    fun myOrders(
        @RequestParam(defaultValue = "ACTIVE") scope: MarketOrderScope,
        @RequestParam(required = false) side: MarketOrderSide?,
        @RequestParam(required = false) instrumentId: UUID?,
        @RequestParam(required = false) cursor: String?,
        session: HttpSession,
    ) = account(session).let { success(it, orders.orders(it, scope, side, instrumentId, cursor)) }

    @PatchMapping("/orders/{orderId}")
    fun update(
        @PathVariable orderId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestBody request: UpdateMarketOrderRequest,
        session: HttpSession,
    ) = account(session).let { success(it, orders.update(it, orderId, idempotencyKey, request)) }

    @PostMapping("/orders/{orderId}/cancel")
    fun cancel(
        @PathVariable orderId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        session: HttpSession,
    ) = account(session).let { success(it, orders.cancel(it, orderId, idempotencyKey)) }

    @GetMapping("/deliveries/me")
    fun deliveries(
        @RequestParam(required = false) claimable: Boolean?,
        @RequestParam(required = false) cursor: String?,
        session: HttpSession,
    ) = account(session).let { success(it, deliveries.list(it, claimable, cursor)) }

    @PostMapping("/deliveries/{deliveryId}/claim")
    fun claimDelivery(
        @PathVariable deliveryId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        session: HttpSession,
    ) = account(session).let { success(it, deliveries.claim(it, deliveryId, idempotencyKey)) }

    @PostMapping("/deliveries/claim-all")
    fun claimAllDeliveries(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        session: HttpSession,
    ) = account(session).let { success(it, deliveries.claimAll(it, idempotencyKey)) }

    @GetMapping("/summary/me")
    fun summary(session: HttpSession) = account(session).let { success(it, deliveries.summary(it)) }

    @PostMapping("/unread-cursors/{stream}")
    fun markRead(
        @PathVariable stream: MarketUnreadStream,
        @RequestHeader("Idempotency-Key") ignoredIdempotencyKey: UUID,
        @RequestBody request: AdvanceMarketReadCursorRequest,
        session: HttpSession,
    ) = account(session).let { success(it, deliveries.markRead(it, stream, request.readThroughSequence)) }

    @GetMapping("/trades/me")
    fun myTrades(
        @RequestParam(required = false) instrumentId: List<UUID>?,
        @RequestParam(required = false) cursor: String?,
        session: HttpSession,
    ) = account(session).let { success(it, orders.trades(it, instrumentIds(instrumentId), cursor)) }

    @GetMapping("/trades")
    fun publicTrades(
        @RequestParam(required = false) instrumentId: List<UUID>?,
        @RequestParam(required = false) cursor: String?,
        session: HttpSession,
    ) = account(session).let { success(it, orders.trades(null, instrumentIds(instrumentId), cursor)) }

    private fun instrumentIds(ids: List<UUID>?): List<UUID> = ids ?: throw IllegalArgumentException("MARKET_INSTRUMENT_REQUIRED")
    private fun account(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
    private fun <T> success(accountId: UUID, data: T): ResponseEntity<Envelope<T>> = ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(Envelope(UUID.randomUUID(), clock.instant(), orders.stateVersion(accountId), data))
}
