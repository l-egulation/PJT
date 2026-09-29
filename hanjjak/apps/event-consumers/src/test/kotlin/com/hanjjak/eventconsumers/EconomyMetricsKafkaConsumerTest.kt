package com.hanjjak.eventconsumers

import com.fasterxml.jackson.module.kotlin.jacksonObjectMapper
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertThrows
import java.time.Instant
import java.util.UUID

class EconomyMetricsKafkaConsumerTest {
    @Test
    fun `consumer forwards valid events and removes duplicate ids in writer`() {
        val writer = CapturingEconomyMetricsWriter()
        val consumer = EconomyMetricsKafkaConsumer(jacksonObjectMapper(), writer, KafkaOpsMetrics(io.micrometer.core.instrument.simple.SimpleMeterRegistry()))
        val eventId = UUID.fromString("00000000-0000-4000-8000-000000001001")
        val message = """
            {"eventId":"$eventId","eventType":"MARKET_TRADE_COMPLETED","occurredAt":"2026-09-11T00:00:00Z","itemId":"POTATO_M1","quantity":2,"totalPrice":100,"payload":{"fee":10,"settlementAmount":90}}
        """.trimIndent()

        consumer.consume(listOf(message, message))

        assertEquals(listOf(eventId), writer.events.map { it.eventId })
    }

    @Test
    fun `consumer rejects malformed event`() {
        val consumer = EconomyMetricsKafkaConsumer(jacksonObjectMapper(), CapturingEconomyMetricsWriter(), KafkaOpsMetrics(io.micrometer.core.instrument.simple.SimpleMeterRegistry()))
        assertThrows(Exception::class.java) { consumer.consume(listOf("{malformed")) }
    }
}

private class CapturingEconomyMetricsWriter : EconomyMetricsKafkaWriter {
    val events = mutableListOf<MetricEvent>()
    override fun record(events: List<MetricEvent>): ConsumerBatchResult {
        val unique = events.distinctBy { it.eventId }
        this.events += unique
        return ConsumerBatchResult(unique.size, events.size - unique.size)
    }
}
