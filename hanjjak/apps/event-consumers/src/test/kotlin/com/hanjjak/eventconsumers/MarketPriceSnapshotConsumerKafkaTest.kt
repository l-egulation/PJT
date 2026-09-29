package com.hanjjak.eventconsumers

import org.awaitility.Awaitility.await
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertThrows
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.autoconfigure.SpringBootApplication
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Primary
import org.springframework.kafka.core.KafkaTemplate
import org.springframework.kafka.test.context.EmbeddedKafka
import java.time.Duration
import java.util.Collections


@SpringBootTest(
    classes = [KafkaConsumerTestApplication::class, KafkaConsumerTestBeans::class],
    properties = [
        "events.kafka.publisher.enabled=false",
        "events.kafka.enabled=false",
        "events.kafka.consumer.price-snapshot.enabled=true",
        "events.kafka.consumer.economy-metrics.enabled=false",
        "events.kafka.topic=domain-events.v1",
        "spring.kafka.consumer.auto-offset-reset=earliest",
        "spring.kafka.consumer.group-id=price-snapshot-test",
        "spring.kafka.listener.type=batch",
        "spring.autoconfigure.exclude=org.springframework.boot.autoconfigure.jdbc.DataSourceAutoConfiguration,org.springframework.boot.autoconfigure.jdbc.JdbcTemplateAutoConfiguration",
        "spring.main.allow-bean-definition-overriding=true",
    ],
)
@EmbeddedKafka(
    partitions = 1,
    topics = ["domain-events.v1"],
    bootstrapServersProperty = "spring.kafka.bootstrap-servers",
    brokerProperties = [
        "offsets.topic.num.partitions=1",
        "transaction.state.log.num.partitions=1",
        "transaction.state.log.replication.factor=1",
        "group.initial.rebalance.delay.ms=0",
    ],
)
class MarketPriceSnapshotConsumerKafkaTest @Autowired constructor(
    private val kafka: KafkaTemplate<String, String>,
    private val writer: CapturingMarketPriceSnapshotWriter,
) {
    @BeforeEach
    fun clearEvents() {
        writer.events.clear()
    }

    @Test
    fun `market trade events reach price snapshot consumer as a batch`() {
        kafka.send("domain-events.v1", "account-1", """
            {
              "eventId":"00000000-0000-4000-8000-000000000901",
              "eventType":"MARKET_TRADE_COMPLETED",
              "schemaVersion":1,
              "occurredAt":"2026-09-07T00:00:00Z",
              "producer":"game-api",
              "accountId":"00000000-0000-4000-8000-000000000001",
              "itemId":"POTATO_M1",
              "quantity":3,
              "unitPrice":50,
              "totalPrice":150,
              "payload":{"fee":15,"settlementAmount":135}
            }
        """.trimIndent())
        kafka.send("domain-events.v1", "account-1", """
            {
              "eventId":"00000000-0000-4000-8000-000000000902",
              "eventType":"MARKET_TRADE_COMPLETED",
              "schemaVersion":1,
              "occurredAt":"2026-09-07T00:00:01Z",
              "producer":"game-api",
              "accountId":"00000000-0000-4000-8000-000000000001",
              "itemId":"POTATO_M1",
              "quantity":2,
              "unitPrice":60,
              "totalPrice":120,
              "payload":{"fee":12,"settlementAmount":108}
            }
        """.trimIndent())

        await().atMost(Duration.ofSeconds(10)).untilAsserted {
            assertEquals(2, writer.events.size)
        }
        assertEquals(listOf(150L, 120L), writer.events.map { it.totalPrice })
    }

    @Test
    fun `valid prefix is recorded before a malformed batch record is rejected`() {
        val directWriter = CapturingMarketPriceSnapshotWriter()
        val consumer = MarketPriceSnapshotConsumer(com.fasterxml.jackson.module.kotlin.jacksonObjectMapper(), directWriter, KafkaOpsMetrics(io.micrometer.core.instrument.simple.SimpleMeterRegistry()))
        val valid = """{"eventId":"00000000-0000-4000-8000-000000000903","eventType":"MARKET_TRADE_COMPLETED","occurredAt":"2026-09-07T00:00:02Z","itemId":"POTATO_M1","quantity":1,"unitPrice":70,"totalPrice":70,"payload":{"fee":7,"settlementAmount":63}}"""

        val error = assertThrows(org.springframework.kafka.listener.BatchListenerFailedException::class.java) {
            consumer.consume(listOf(valid, "{malformed", valid))
        }

        assertEquals(1, error.index)
        assertEquals(listOf(70L), directWriter.events.map { it.totalPrice })
    }
}

@SpringBootApplication
class KafkaConsumerTestApplication

@TestConfiguration
class KafkaConsumerTestBeans {
    @Primary
    @Bean("jdbcMarketPriceSnapshotWriter") fun marketPriceSnapshotWriter(): CapturingMarketPriceSnapshotWriter = CapturingMarketPriceSnapshotWriter()
}

class CapturingMarketPriceSnapshotWriter : MarketPriceSnapshotWriter {
    val events: MutableList<MarketTradeCompletedEnvelope> = Collections.synchronizedList(mutableListOf())
    override fun recordAll(events: List<MarketTradeCompletedEnvelope>): ConsumerBatchResult {
        this.events += events
        return ConsumerBatchResult(events.size, 0)
    }
}
