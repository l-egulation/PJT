package com.hanjjak.market.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.NoopDomainEventPublisher
import com.hanjjak.inventory.application.*
import com.hanjjak.inventory.domain.*
import com.hanjjak.inventory.infrastructure.StaticItemCatalog
import com.hanjjak.mail.application.*
import com.hanjjak.mail.domain.MailMessage
import com.hanjjak.mail.application.MailService
import com.hanjjak.market.domain.*
import com.hanjjak.wallet.application.*
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals

class OrderBookServiceTest {
    private val now = Instant.parse("2026-09-11T00:00:00Z")
    private val buyer = UUID(0, 1)
    private val seller = UUID(0, 2)

    @Test
    fun `maker price and strict price time priority drive partial fill`() {
        val fixture = fixture()
        fixture.inventory.stacks[seller to "POTATO_M1"] = InventoryStack(seller, "POTATO_M1", 5, 0, 1)
        val sell = fixture.service.create(seller, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 5, 90, MarketTimeInForce.GTC)).result
        val buy = fixture.service.create(buyer, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.BUY, 3, 100, MarketTimeInForce.IOC)).result
        assertEquals(90, buy.fills.single().unitPrice)
        assertEquals(3, buy.filledQuantity)
        assertEquals(2, fixture.repository.orders.getValue(sell.orderId).remainingQuantity)
        assertEquals(730, fixture.wallet.balances[buyer])
        assertEquals(3, fixture.inventory.stacks.getValue(buyer to "POTATO_M1").quantity)
    }

    @Test
    fun `resting buy reserves rice and creates delivery`() {
        val fixture = fixture()
        fixture.inventory.stacks[seller to "POTATO_M1"] = InventoryStack(seller, "POTATO_M1", 4, 0, 1)
        val buy = fixture.service.create(buyer, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.BUY, 4, 100, MarketTimeInForce.GTC)).result
        assertEquals(600, fixture.wallet.balances[buyer])
        fixture.service.create(seller, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 2, 80, MarketTimeInForce.IOC))
        assertEquals(1, fixture.repository.deliveries.size)
        assertEquals(200, fixture.repository.orders.getValue(buy.orderId).reservedRice)
        assertEquals(600, fixture.wallet.balances[buyer])
    }

    @Test
    fun `buying skips own listings but fills another sellers listing`() {
        val fixture = fixture()
        fixture.inventory.stacks[buyer to "POTATO_M1"] = InventoryStack(buyer, "POTATO_M1", 2, 0, 1)
        fixture.inventory.stacks[seller to "POTATO_M1"] = InventoryStack(seller, "POTATO_M1", 1, 0, 1)
        val ownSell = fixture.service.create(buyer, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 2, 100, MarketTimeInForce.GTC)).result
        val otherSell = fixture.service.create(seller, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 1, 90, MarketTimeInForce.GTC)).result

        val quote = fixture.service.quote(buyer, fixture.instrument.instrumentId, MarketOrderSide.BUY, MarketTimeInForce.IOC, 2, 100)
        assertEquals(1, quote.expectedFilledQuantity)
        assertEquals(90, quote.totalPrice)

        val buy = fixture.service.create(buyer, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.BUY, 2, 100, MarketTimeInForce.IOC)).result
        assertEquals(1, buy.filledQuantity)
        assertEquals(2, fixture.repository.orders.getValue(ownSell.orderId).remainingQuantity)
        assertEquals(0, fixture.repository.orders.getValue(otherSell.orderId).remainingQuantity)
    }

    @Test
    fun `buy quote ignores own listing when it is the only available listing`() {
        val fixture = fixture()
        fixture.inventory.stacks[buyer to "POTATO_M1"] = InventoryStack(buyer, "POTATO_M1", 1, 0, 1)
        fixture.service.create(buyer, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 1, 100, MarketTimeInForce.GTC))

        val quote = fixture.service.quote(buyer, fixture.instrument.instrumentId, MarketOrderSide.BUY, MarketTimeInForce.IOC, 1, 100)
        assertEquals(0, quote.expectedFilledQuantity)
        assertEquals(0, quote.totalPrice)
    }

    @Test
    fun `sell IOC fills resting buy from escrow without a second debit`() {
        val fixture = fixture()
        fixture.inventory.stacks[seller to "POTATO_M1"] = InventoryStack(seller, "POTATO_M1", 3, 0, 1)
        fixture.service.create(buyer, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.BUY, 3, 100, MarketTimeInForce.GTC))
        assertEquals(700, fixture.wallet.balances[buyer])

        fixture.service.create(seller, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 2, 90, MarketTimeInForce.IOC))

        assertEquals(700, fixture.wallet.balances[buyer])
        assertEquals(100, fixture.repository.orders.values.single { it.accountId == buyer }.reservedRice)
        assertEquals(1, fixture.repository.deliveries.size)
    }

    @Test
    fun `sell quote sums fee per maker fill`() {
        val fixture = fixture()
        val otherBuyer = UUID(0, 3)
        fixture.wallet.balances[otherBuyer] = 1_000
        fixture.service.create(buyer, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.BUY, 1, 15, MarketTimeInForce.GTC))
        fixture.service.create(otherBuyer, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.BUY, 1, 15, MarketTimeInForce.GTC))

        val quote = fixture.service.quote(seller, fixture.instrument.instrumentId, MarketOrderSide.SELL, MarketTimeInForce.IOC, 2, 10)

        assertEquals(2, quote.expectedFee)
        assertEquals(28, quote.expectedSettlementAmount)
    }

    @Test
    fun `price levels preserve global cumulative values across exclusive pages`() {
        val fixture = fixture()
        val otherSeller = UUID(0, 3)
        fixture.inventory.stacks[seller to "POTATO_M1"] = InventoryStack(seller, "POTATO_M1", 3, 0, 1)
        fixture.inventory.stacks[otherSeller to "POTATO_M1"] = InventoryStack(otherSeller, "POTATO_M1", 3, 0, 1)
        fixture.service.create(seller, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 1, 100, MarketTimeInForce.GTC))
        fixture.service.create(otherSeller, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 1, 110, MarketTimeInForce.GTC))
        fixture.service.create(otherSeller, UUID.randomUUID(), CreateMarketOrderRequest(fixture.instrument.instrumentId, MarketOrderSide.SELL, 1, 120, MarketTimeInForce.GTC))

        val first = fixture.service.priceLevels(seller, fixture.instrument.instrumentId, MarketOrderSide.SELL, null, 1)
        assertEquals(listOf(100L), first.items.map { it.unitPrice })
        assertEquals(1L, first.items.single().cumulativeQuantity)
        assertEquals(0L, first.items.single().cumulativeOtherQuantity)
        assertEquals(100L, first.nextUnitPrice)

        val second = fixture.service.priceLevels(seller, fixture.instrument.instrumentId, MarketOrderSide.SELL, first.nextUnitPrice, 2)
        assertEquals(listOf(110L, 120L), second.items.map { it.unitPrice })
        assertEquals(listOf(2L, 3L), second.items.map { it.cumulativeQuantity })
        assertEquals(listOf(1L, 2L), second.items.map { it.cumulativeOtherQuantity })
        assertEquals(null, second.nextUnitPrice)
    }

    @Test
    fun `candle periods expose the requested time window`() {
        val fixture = fixture()
        val expected = listOf(
            "12h" to now.minusSeconds(12 * 60 * 60),
            "1d" to now.minusSeconds(24 * 60 * 60),
            "2d" to now.minusSeconds(48 * 60 * 60),
        )

        expected.forEach { (bucket, from) ->
            val result = fixture.service.candles(buyer, fixture.instrument.instrumentId, MarketCandleInterval.FIVE_MINUTES, MarketCandlePeriod.parse(bucket))

            assertEquals("5m:$bucket", result.interval)
            assertEquals(from, result.from)
            assertEquals(now, result.to)
        }
    }


    private fun fixture(): Fixture {
        val catalog = StaticItemCatalog()
        val inventory = MemoryInventory()
        val wallet = MemoryWallet(mutableMapOf(buyer to 1_000L, seller to 0L))
        val repository = MemoryOrderBook(now)
        val clock = Clock.fixed(now, ZoneOffset.UTC)
        val mapper = ObjectMapper().findAndRegisterModules()
        val inventoryService = com.hanjjak.inventory.application.MarketInventoryService(inventory, catalog)
        val walletService = WalletService(wallet)
        val mail = MailService(MemoryMail(), walletService, mapper, clock)
        val catalogSync = MarketInstrumentCatalog(repository, catalog, clock)
        val service = OrderBookService(repository, inventoryService, walletService, mail, catalog, mapper, clock, NoopDomainEventPublisher, catalogSync)
        catalogSync.synchronize()
        val instrument = repository.instruments.values.first { it.itemId == "POTATO_M1" }
        return Fixture(service, repository, inventory, wallet, instrument)
    }

    private data class Fixture(val service: OrderBookService, val repository: MemoryOrderBook, val inventory: MemoryInventory, val wallet: MemoryWallet, val instrument: MarketInstrument)

    private class MemoryInventory : InventoryRepository {
        val stacks = mutableMapOf<Pair<UUID, String>, InventoryStack>()
        override fun lockAccount(accountId: UUID) = Unit
        override fun stateVersion(accountId: UUID) = 1L
        override fun incrementStateVersion(accountId: UUID) = 2L
        override fun stacks(accountId: UUID) = stacks.values.filter { it.accountId == accountId }
        override fun instances(accountId: UUID) = emptyList<InventoryInstance>()
        override fun saveStack(stack: InventoryStack) { stacks[stack.accountId to stack.itemId] = stack }
        override fun addInstance(instance: InventoryInstance) = Unit
        override fun removeInstances(accountId: UUID, instanceIds: List<UUID>) = Unit
        override fun setInstancesReserved(accountId: UUID, instanceIds: List<UUID>, reserved: Boolean) = Unit
        override fun transferInstances(fromAccountId: UUID, toAccountId: UUID, instanceIds: List<UUID>, reserved: Boolean) = Unit
        override fun hasCapacityReservation(accountId: UUID, itemId: String) = false
        override fun lockStack(accountId: UUID, itemId: String) = stacks[accountId to itemId]
        override fun usedSlotCount(accountId: UUID) = stacks(accountId).count { it.quantity > 0 }
        override fun nextAcquiredSequence(accountId: UUID) = 1L
        override fun rewardCommand(accountId: UUID, idempotencyKey: UUID) = null
        override fun saveRewardCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) = Unit
    }

    private class MemoryWallet(val balances: MutableMap<UUID, Long>) : WalletRepository {
        override fun lockBalance(accountId: UUID) = balances[accountId] ?: 0
        override fun applyDelta(accountId: UUID, delta: Long, sourceType: String, sourceId: UUID, bumpStateVersion: Boolean): Long { val next = Math.addExact(lockBalance(accountId), delta); require(next >= 0) { "INSUFFICIENT_RICE" }; balances[accountId] = next; return next }
        override fun applyBatchedDebit(accountId: UUID, entries: List<WalletDebitEntry>, bumpStateVersion: Boolean) = applyDelta(accountId, -entries.sumOf { it.amount }, "BATCH", UUID.randomUUID(), bumpStateVersion)
    }

    private class MemoryMail : MailRepository {
        override fun stateVersion(accountId: UUID) = 1L
        override fun command(accountId: UUID, idempotencyKey: UUID) = null
        override fun saveCommand(commandId: UUID, accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) = Unit
        override fun createSettlementMail(accountId: UUID, tradeId: UUID, riceAmount: Long) = UUID.randomUUID()
        override fun list(accountId: UUID, claimable: Boolean?, cursor: Instant?, limit: Int) = emptyList<MailMessage>()
        override fun lockClaimable(accountId: UUID, mailId: UUID) = null
        override fun lockClaimableAll(accountId: UUID) = emptyList<MailMessage>()
        override fun markClaimed(mailIds: List<UUID>, claimedAt: Instant) = Unit
    }

    private class MemoryOrderBook(private val now: Instant) : OrderBookRepository {
        val instruments = mutableMapOf<UUID, MarketInstrument>()
        val orders = mutableMapOf<UUID, MarketOrder>()
        val deliveries = mutableMapOf<UUID, MarketDelivery>()
        val trades = mutableListOf<OrderBookRepository.TradeInsert>()
        val unread = mutableMapOf<Pair<UUID, MarketUnreadStream>, Long>()
        val mutations = mutableMapOf<UUID, Int>()
        override fun stateVersion(accountId: UUID) = 1L
        override fun command(accountId: UUID, idempotencyKey: UUID) = null
        override fun saveCommand(commandId: UUID, accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) = Unit
        override fun synchronizeInstruments(instruments: List<MarketInstrument>, now: Instant) { instruments.forEach { this.instruments[it.instrumentId] = it } }
        override fun instruments(accountId: UUID) = instruments.values.map { MarketInstrumentSummary(it.instrumentId, it.canonicalKey, it.itemId, it.displayName, it.category, it.attributes, it.status, null, null, null, it.marketRevision) }
        override fun instrument(instrumentId: UUID) = instruments[instrumentId]
        override fun lockInstrument(instrumentId: UUID) = instruments[instrumentId]
        override fun incrementRevision(instrumentId: UUID): Long { val current = instruments.getValue(instrumentId); val next = current.marketRevision + 1; instruments[instrumentId] = current.copy(marketRevision = next); return next }
        override fun lockAccountControl(accountId: UUID, now: Instant) = OrderBookRepository.MutationWindow(mutations[accountId] ?: 0, orders.values.count { it.accountId == accountId && it.status in setOf(MarketOrderStatus.ACTIVE, MarketOrderStatus.PARTIALLY_FILLED) })
        override fun appendMutation(mutationId: UUID, accountId: UUID, instrumentId: UUID, orderId: UUID, side: MarketOrderSide, type: String, commandId: UUID, createdAt: Instant) { mutations[accountId] = (mutations[accountId] ?: 0) + 1 }
        override fun order(orderId: UUID) = orders[orderId]
        override fun lockOrder(orderId: UUID) = orders[orderId]
        override fun createOrder(order: MarketOrder) { orders[order.orderId] = order }
        override fun updateOrder(order: MarketOrder) { orders[order.orderId] = order }
        override fun matchCandidates(instrumentId: UUID, incomingSide: MarketOrderSide, limitUnitPrice: Long, now: Instant) = orders.values.filter { it.instrumentId == instrumentId && it.side != incomingSide && it.status in setOf(MarketOrderStatus.ACTIVE, MarketOrderStatus.PARTIALLY_FILLED) && if (incomingSide == MarketOrderSide.BUY) it.limitUnitPrice <= limitUnitPrice else it.limitUnitPrice >= limitUnitPrice }.sortedWith(if (incomingSide == MarketOrderSide.BUY) compareBy<MarketOrder> { it.limitUnitPrice }.thenBy { it.priorityAt }.thenBy { it.orderId } else compareByDescending<MarketOrder> { it.limitUnitPrice }.thenBy { it.priorityAt }.thenBy { it.orderId })
        override fun crossingOwnOrder(instrumentId: UUID, accountId: UUID, incomingSide: MarketOrderSide, limitUnitPrice: Long, now: Instant) = matchCandidates(instrumentId, incomingSide, limitUnitPrice, now).any { it.accountId == accountId }
        override fun depth(instrumentId: UUID, accountId: UUID, side: MarketOrderSide, levels: Int, now: Instant) = emptyList<MarketDepthLevel>()
        override fun priceLevels(instrumentId: UUID, accountId: UUID, side: MarketOrderSide, afterUnitPrice: Long?, limit: Int, now: Instant): List<MarketPriceLevel> {
            val prices = orders.values.asSequence().filter { it.instrumentId == instrumentId && it.side == side && it.status in setOf(MarketOrderStatus.ACTIVE, MarketOrderStatus.PARTIALLY_FILLED) }.groupBy { it.limitUnitPrice }
            val ordered = if (side == MarketOrderSide.BUY) prices.entries.sortedByDescending { it.key } else prices.entries.sortedBy { it.key }
            var cumulative = 0L
            var cumulativeOther = 0L
            val levels = ordered.map { (price, levelOrders) ->
                val total = levelOrders.sumOf { it.remainingQuantity }
                val mine = levelOrders.filter { it.accountId == accountId }.sumOf { it.remainingQuantity }
                cumulative = Math.addExact(cumulative, total)
                cumulativeOther = Math.addExact(cumulativeOther, total - mine)
                MarketPriceLevel(price, total, cumulative, mine, cumulativeOther)
            }
            return levels.filter { afterUnitPrice == null || if (side == MarketOrderSide.BUY) it.unitPrice < afterUnitPrice else it.unitPrice > afterUnitPrice }.take(limit)
        }
        override fun priceLevelCount(instrumentId: UUID, side: MarketOrderSide, now: Instant) = orders.values.count { it.instrumentId == instrumentId && it.side == side && it.status in setOf(MarketOrderStatus.ACTIVE, MarketOrderStatus.PARTIALLY_FILLED) }.toLong()
        override fun recentTrades(instrumentId: UUID, limit: Int) = trades.filter { it.instrumentId == instrumentId }.sortedWith(compareByDescending<OrderBookRepository.TradeInsert> { it.filledAt }.thenByDescending { it.tradeId }).take(limit).map { MarketRecentTrade(it.tradeId, it.quantity, it.unitPrice, it.totalPrice, it.filledAt) }
        override fun candles(instrumentId: UUID, interval: MarketCandleInterval, from: Instant, to: Instant) = emptyList<MarketCandle>()
        override fun listOrders(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?, cursor: OrderBookRepository.OrderCursor?, limit: Int) = emptyList<MarketOrderSummary>()
        override fun countOrders(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?) = 0L
        override fun expiredOrders(now: Instant, limit: Int) = emptyList<MarketOrder>()
        override fun listAdminOrders(orderId: UUID?, instrumentId: UUID?, cursor: OrderBookRepository.OrderCursor?, limit: Int) = emptyList<AdminMarketOrderSummary>()
        override fun ensureSystemMarketAccount(now: Instant) = UUID(0, 99)
        override fun isSystemMarketAccount(accountId: UUID) = accountId == UUID(0, 99)
        override fun createTrade(trade: OrderBookRepository.TradeInsert) { trades += trade }
        override fun listTrades(accountId: UUID?, instrumentIds: List<UUID>, cursor: OrderBookRepository.TradeCursor?, limit: Int) = emptyList<MarketTradeView>()
        override fun countTrades(accountId: UUID?, instrumentIds: List<UUID>) = 0L
        override fun createDelivery(delivery: MarketDelivery) { deliveries[delivery.deliveryId] = delivery }
        override fun listDeliveries(accountId: UUID, claimable: Boolean?, cursor: OrderBookRepository.OrderCursor?, limit: Int) = deliveries.values.toList()
        override fun countDeliveries(accountId: UUID, claimable: Boolean?) = deliveries.size.toLong()
        override fun lockDelivery(accountId: UUID, deliveryId: UUID) = deliveries[deliveryId]
        override fun lockClaimableDeliveries(accountId: UUID) = deliveries.values.filter { it.claimedAt == null }
        override fun markDeliveriesClaimed(deliveryIds: List<UUID>, claimedAt: Instant) = Unit
        override fun appendUnread(accountId: UUID, stream: MarketUnreadStream, sourceId: UUID, createdAt: Instant): Long { val key = accountId to stream; val next = (unread[key] ?: 0) + 1; unread[key] = next; return next }
        override fun unreadStates(accountId: UUID) = MarketUnreadStream.entries.map { MarketUnreadState(it, unread[accountId to it] ?: 0, 0, unread[accountId to it] ?: 0) }
        override fun issueReadThrough(accountId: UUID, stream: MarketUnreadStream, sequence: Long) = Unit
        override fun advanceReadCursor(accountId: UUID, stream: MarketUnreadStream, sequence: Long) = sequence
        override fun migrateLegacyMarket(now: Instant) = emptyList<Pair<UUID, Long>>()
    }
}
