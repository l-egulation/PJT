package com.hanjjak.market.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublishRequest
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.inventory.infrastructure.StaticItemCatalog
import com.hanjjak.market.domain.*
import com.hanjjak.wallet.application.WalletDebitEntry
import com.hanjjak.wallet.application.WalletRepository
import com.hanjjak.wallet.application.WalletService
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class AdminMarketServiceTest {
    private val now = Instant.parse("2026-09-11T00:00:00Z")
    private val system = UUID.fromString("00000000-0000-4000-8000-000000000001")
    private val seller = UUID.fromString("00000000-0000-4000-8000-000000000010")
    private val instrument = MarketInstrument(UUID.fromString("00000000-0000-4000-8000-000000000100"), "material:potato_m1", "POTATO_M1", "감자 한 조각", "MATERIAL", emptyMap(), MarketInstrumentStatus.ACTIVE, 1)
    private val mapper = ObjectMapper().findAndRegisterModules()

    @Test
    fun `system purchase fills exact player sell order and credits net settlement`() {
        val fixture = fixture()
        val order = fixture.market.seedSell(seller, 50, 100)

        val result = fixture.service.purchaseOrder(order.orderId, UUID.randomUUID(), 20, "매도 유동성 회수")

        assertEquals(20, result.quantity)
        assertEquals(30, result.remainingOrderQuantity)
        assertEquals(1_800, fixture.wallet.balances[seller])
        assertEquals(system, fixture.market.trades.single().buyerAccountId)
    }

    @Test
    fun `forced cancellation removes escrow without returning player stock`() {
        val fixture = fixture()
        val order = fixture.market.seedSell(seller, 30, 100)

        val result = fixture.service.cancelOrder(order.orderId, UUID.randomUUID(), "비정상 주문 제거")

        assertEquals(30, result.cancelledQuantity)
        assertEquals(MarketOrderStatus.CANCELLED, fixture.market.orders.getValue(order.orderId).status)
        assertEquals(0, fixture.wallet.balances[seller])
    }

    @Test
    fun `system cannot purchase its own liquidity order`() {
        val fixture = fixture()
        val order = fixture.market.seedSell(system, 10, 100)

        val error = assertFailsWith<IllegalArgumentException> {
            fixture.service.purchaseOrder(order.orderId, UUID.randomUUID(), 1, "자기 매수 거절")
        }

        assertEquals("SYSTEM_ORDER_NOT_PURCHASABLE", error.message)
    }

    private fun fixture(): Fixture {
        val market = MemoryOrderBookRepository(now, system, instrument)
        val wallet = MemoryWalletRepository(mutableMapOf(system to 0, seller to 0))
        val events = MemoryEvents()
        return Fixture(AdminMarketService(market, WalletService(wallet), StaticItemCatalog(), mapper, Clock.fixed(now, ZoneOffset.UTC), events), market, wallet)
    }

    private data class Fixture(val service: AdminMarketService, val market: MemoryOrderBookRepository, val wallet: MemoryWalletRepository)

    private class MemoryEvents : DomainEventPublisher {
        override fun publish(request: DomainEventPublishRequest) = UUID.randomUUID()
        override fun publishAll(requests: List<DomainEventPublishRequest>) = requests.map { UUID.randomUUID() }
    }

    private class MemoryWalletRepository(val balances: MutableMap<UUID, Long>) : WalletRepository {
        override fun lockBalance(accountId: UUID) = balances.getOrDefault(accountId, 0)
        override fun applyDelta(accountId: UUID, delta: Long, sourceType: String, sourceId: UUID, bumpStateVersion: Boolean): Long = Math.addExact(balances.getOrDefault(accountId, 0), delta).also { balances[accountId] = it }
        override fun applyBatchedDebit(accountId: UUID, entries: List<WalletDebitEntry>, bumpStateVersion: Boolean): Long = applyDelta(accountId, -entries.sumOf { it.amount }, "BATCH", entries.first().sourceId, bumpStateVersion)
    }

    private class MemoryOrderBookRepository(
        private val now: Instant,
        private val system: UUID,
        private val instrumentValue: MarketInstrument,
    ) : OrderBookRepository {
        val orders = linkedMapOf<UUID, MarketOrder>()
        val trades = mutableListOf<OrderBookRepository.TradeInsert>()
        private val commands = mutableMapOf<Pair<UUID, UUID>, OrderBookRepository.StoredCommand>()
        private var revision = instrumentValue.marketRevision

        fun seedSell(accountId: UUID, quantity: Long, price: Long): MarketOrder = MarketOrder(
            UUID.randomUUID(), accountId, instrumentValue.instrumentId, MarketOrderSide.SELL, MarketTimeInForce.GTC,
            quantity, 0, quantity, price, 0, MarketOrderStatus.ACTIVE, now, now, now, now.plusSeconds(3600), instrumentValue.displayName,
        ).also { orders[it.orderId] = it }

        override fun stateVersion(accountId: UUID) = 1L
        override fun command(accountId: UUID, idempotencyKey: UUID) = commands[accountId to idempotencyKey]
        override fun saveCommand(commandId: UUID, accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) { commands[accountId to idempotencyKey] = OrderBookRepository.StoredCommand(commandId, fingerprint, resultJson) }
        override fun synchronizeInstruments(instruments: List<MarketInstrument>, now: Instant) = Unit
        override fun instruments(accountId: UUID) = listOf(MarketInstrumentSummary(instrumentValue.instrumentId, instrumentValue.canonicalKey, instrumentValue.itemId, instrumentValue.displayName, instrumentValue.category, instrumentValue.attributes, instrumentValue.status, null, null, null, revision))
        override fun instrument(instrumentId: UUID) = instrumentValue.takeIf { it.instrumentId == instrumentId }?.copy(marketRevision = revision)
        override fun lockInstrument(instrumentId: UUID) = instrument(instrumentId)
        override fun incrementRevision(instrumentId: UUID) = ++revision
        override fun lockAccountControl(accountId: UUID, now: Instant) = OrderBookRepository.MutationWindow(0, 0)
        override fun appendMutation(mutationId: UUID, accountId: UUID, instrumentId: UUID, orderId: UUID, side: MarketOrderSide, type: String, commandId: UUID, createdAt: Instant) = Unit
        override fun order(orderId: UUID) = orders[orderId]
        override fun lockOrder(orderId: UUID) = orders[orderId]
        override fun createOrder(order: MarketOrder) { orders[order.orderId] = order }
        override fun updateOrder(order: MarketOrder) { orders[order.orderId] = order }
        override fun matchCandidates(instrumentId: UUID, incomingSide: MarketOrderSide, limitUnitPrice: Long, now: Instant) = emptyList<MarketOrder>()
        override fun crossingOwnOrder(instrumentId: UUID, accountId: UUID, incomingSide: MarketOrderSide, limitUnitPrice: Long, now: Instant) = false
        override fun depth(instrumentId: UUID, accountId: UUID, side: MarketOrderSide, levels: Int, now: Instant) = emptyList<MarketDepthLevel>()
        override fun priceLevels(instrumentId: UUID, accountId: UUID, side: MarketOrderSide, afterUnitPrice: Long?, limit: Int, now: Instant) = emptyList<MarketPriceLevel>()
        override fun priceLevelCount(instrumentId: UUID, side: MarketOrderSide, now: Instant) = 0L
        override fun recentTrades(instrumentId: UUID, limit: Int) = emptyList<MarketRecentTrade>()
        override fun candles(instrumentId: UUID, interval: MarketCandleInterval, from: Instant, to: Instant) = emptyList<MarketCandle>()
        override fun listOrders(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?, cursor: OrderBookRepository.OrderCursor?, limit: Int) = emptyList<MarketOrderSummary>()
        override fun countOrders(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?) = 0L
        override fun expiredOrders(now: Instant, limit: Int) = emptyList<MarketOrder>()
        override fun listAdminOrders(orderId: UUID?, instrumentId: UUID?, cursor: OrderBookRepository.OrderCursor?, limit: Int) = emptyList<AdminMarketOrderSummary>()
        override fun ensureSystemMarketAccount(now: Instant) = system
        override fun isSystemMarketAccount(accountId: UUID) = accountId == system
        override fun createTrade(trade: OrderBookRepository.TradeInsert) { trades += trade }
        override fun listTrades(accountId: UUID?, instrumentIds: List<UUID>, cursor: OrderBookRepository.TradeCursor?, limit: Int) = emptyList<MarketTradeView>()
        override fun countTrades(accountId: UUID?, instrumentIds: List<UUID>) = 0L
        override fun createDelivery(delivery: MarketDelivery) = Unit
        override fun listDeliveries(accountId: UUID, claimable: Boolean?, cursor: OrderBookRepository.OrderCursor?, limit: Int) = emptyList<MarketDelivery>()
        override fun countDeliveries(accountId: UUID, claimable: Boolean?) = 0L
        override fun lockDelivery(accountId: UUID, deliveryId: UUID): MarketDelivery? = null
        override fun lockClaimableDeliveries(accountId: UUID) = emptyList<MarketDelivery>()
        override fun markDeliveriesClaimed(deliveryIds: List<UUID>, claimedAt: Instant) = Unit
        override fun appendUnread(accountId: UUID, stream: MarketUnreadStream, sourceId: UUID, createdAt: Instant) = 0L
        override fun unreadStates(accountId: UUID) = emptyList<MarketUnreadState>()
        override fun issueReadThrough(accountId: UUID, stream: MarketUnreadStream, sequence: Long) = Unit
        override fun advanceReadCursor(accountId: UUID, stream: MarketUnreadStream, sequence: Long) = sequence
        override fun migrateLegacyMarket(now: Instant) = emptyList<Pair<UUID, Long>>()
    }
}
