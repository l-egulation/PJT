package com.hanjjak.events.application

import com.fasterxml.jackson.databind.ObjectMapper
import kotlin.test.Test
import kotlin.test.assertEquals
import java.time.Instant

class EconomyMetricAccumulatorTest {
    private val mapper = ObjectMapper()

    @Test
    fun `aggregates market, drop, equipment, and rice events by day and item`() {
        val accumulator = EconomyMetricAccumulator()
        val occurredAt = Instant.parse("2026-09-07T03:04:05Z")

        accumulator.record("MARKET_ORDER_CREATED", event("""
            {"itemId":"SWEET_POTATO_M1","quantity":5,"payload":{"orderId":"00000000-0000-4000-8000-000000000001","side":"SELL"}}
        """), occurredAt)
        accumulator.record("MARKET_ORDER_CANCELLED", event("""
            {"itemId":"SWEET_POTATO_M1","quantity":1,"payload":{"orderId":"00000000-0000-4000-8000-000000000001"}}
        """), occurredAt)
        accumulator.record("MARKET_TRADE_COMPLETED", event("""
            {"itemId":"SWEET_POTATO_M1","quantity":2,"totalPrice":20,"payload":{"fee":2,"settlementAmount":18}}
        """), occurredAt)
        accumulator.record("ITEM_DROPPED", event("""
            {"itemId":"SWEET_POTATO_M1","quantity":7,"payload":{"itemId":"SWEET_POTATO_M1","quantity":7}}
        """), occurredAt)
        accumulator.record("EQUIPMENT_CRAFTED", event("""
            {"payload":{"consumedItems":[{"itemId":"SWEET_POTATO_M1","quantity":3}],"riceCost":40}}
        """), occurredAt)
        accumulator.record("EQUIPMENT_PROMOTED", event("""
            {"payload":{"consumedItems":[{"itemId":"SWEET_POTATO_M2","quantity":10}],"riceCost":1000}}
        """), occurredAt)
        accumulator.record("BATTLE_ENEMY_SETTLED", event("""
            {"payload":{"riceGained":11}}
        """), occurredAt)
        accumulator.record("BATTLE_CYCLE_COMPLETED", event("""
            {"payload":{"riceGained":999}}
        """), occurredAt)

        val metrics = accumulator.snapshot()

        val item = metrics.single { it.itemId == "SWEET_POTATO_M1" }
        assertEquals("SWEET_POTATO", item.itemFamily)
        assertEquals(1, item.generation)
        assertEquals(5, item.listedQuantity)
        assertEquals(1, item.cancelledQuantity)
        assertEquals(1, item.tradeCount)
        assertEquals(2, item.tradedQuantity)
        assertEquals(20, item.tradeAmount)
        assertEquals(2, item.feeAmount)
        assertEquals(18, item.settlementAmount)
        assertEquals(7, item.droppedQuantity)
        assertEquals(3, item.consumedQuantity)
        val promoted = metrics.single { it.itemId == "SWEET_POTATO_M2" }
        assertEquals(10, promoted.consumedQuantity)

        val rice = metrics.single { it.itemId == RICE_METRIC_ITEM_ID }
        assertEquals(RICE_METRIC_ITEM_ID, rice.itemFamily)
        assertEquals(null, rice.generation)
        assertEquals(11, rice.riceGenerated)
        assertEquals(1_040, rice.riceConsumed)
    }
    @Test
    fun `first-clear reward contributes rice and immediately granted items`() {
        val accumulator = EconomyMetricAccumulator()
        accumulator.record("STAGE_FIRST_CLEAR_REWARD", event("""
            {"payload":{"riceGranted":120,"grantedItems":[{"itemId":"POTATO_M1","quantity":3}],"pendingItems":[{"itemId":"SWEET_POTATO_M1","quantity":4}]}}
        """), Instant.parse("2026-09-07T03:04:05Z"))

        val metrics = accumulator.snapshot()
        assertEquals(120, metrics.single { it.itemId == RICE_METRIC_ITEM_ID }.riceGenerated)
        assertEquals(3, metrics.single { it.itemId == "POTATO_M1" }.droppedQuantity)
        assertEquals(null, metrics.find { it.itemId == "SWEET_POTATO_M1" })
    }
    @Test
    fun `pending claim event contributes granted items once`() {
        val accumulator = EconomyMetricAccumulator()
        accumulator.record("STAGE_FIRST_CLEAR_REWARD", event("""
            {"payload":{"riceGranted":120,"grantedItems":[],"pendingItems":[{"itemId":"POTATO_M1","quantity":4}]}}
        """), Instant.parse("2026-09-07T03:04:05Z"))
        accumulator.record("STAGE_FIRST_CLEAR_REWARD_CLAIMED", event("""
            {"payload":{"grantedItems":[{"itemId":"POTATO_M1","quantity":4}]}}
        """), Instant.parse("2026-09-07T03:05:05Z"))

        assertEquals(4, accumulator.snapshot().single { it.itemId == "POTATO_M1" }.droppedQuantity)
    }

    private fun event(json: String) = mapper.readTree(json.trimIndent())
}
