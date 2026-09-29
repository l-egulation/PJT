package com.hanjjak.eventconsumers

import io.micrometer.core.instrument.simple.SimpleMeterRegistry
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test
import java.time.Duration

class KafkaOpsMetricsTest {
    @Test
    fun `records outbox and consumer counters with processing latency`() {
        val registry = SimpleMeterRegistry()
        val metrics = KafkaOpsMetrics(registry)

        metrics.outboxClaimed(3)
        metrics.outboxSent()
        metrics.outboxRetry()
        metrics.consumerBatch("economy-metrics", 3)
        metrics.consumerEvents("economy-metrics", 2)
        metrics.consumerDuplicates("economy-metrics", 1)
        metrics.consumerLatency("economy-metrics", Duration.ofMillis(25))

        assertEquals(3.0, registry.counter("outbox.events.claimed").count())
        assertEquals(1.0, registry.counter("outbox.events.sent").count())
        assertEquals(1.0, registry.counter("outbox.events.retry").count())
        assertEquals(2.0, registry.counter("kafka.consumer.events", "consumer", "economy-metrics").count())
        assertEquals(1.0, registry.counter("kafka.consumer.duplicates", "consumer", "economy-metrics").count())
        assertEquals(1L, registry.timer("kafka.consumer.processing", "consumer", "economy-metrics").count())
    }
}
