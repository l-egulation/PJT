package com.hanjjak.eventconsumers

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.EconomyMetricAccumulator
import com.hanjjak.events.application.EconomyDailyMetric
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.kafka.annotation.KafkaListener
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.sql.Date
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID

@Service
@ConditionalOnProperty(prefix = "events.kafka.consumer.economy-metrics", name = ["enabled"], havingValue = "true", matchIfMissing = true)
class EconomyMetricsKafkaConsumer(
    private val mapper: ObjectMapper,
    private val writer: EconomyMetricsKafkaWriter,
    private val metrics: KafkaOpsMetrics,
) {
    @KafkaListener(
        topics = ["\${events.kafka.topic:domain-events.v1}"],
        groupId = "\${events.kafka.consumer.economy-metrics.group-id:economy-metrics}",
    )
    fun consume(messages: List<String>) {
        val startedAt = Instant.now()
        metrics.consumerBatch(CONSUMER_NAME, messages.size)
        try {
            val events = messages.map { message ->
                val root = mapper.readTree(message)
                MetricEvent(
                    eventId = UUID.fromString(root.path("eventId").asText()),
                    eventType = root.path("eventType").asText().also { require(it.isNotBlank()) { "EVENT_TYPE_REQUIRED" } },
                    envelope = root,
                    occurredAt = Instant.parse(root.path("occurredAt").asText()),
                )
            }
            val result = writer.record(events)
            metrics.consumerEvents(CONSUMER_NAME, result.processed)
            metrics.consumerDuplicates(CONSUMER_NAME, result.duplicates)
        } catch (error: Exception) {
            metrics.consumerFailed(CONSUMER_NAME)
            throw error
        } finally {
            metrics.consumerLatency(CONSUMER_NAME, java.time.Duration.between(startedAt, Instant.now()))
        }
    }

    companion object {
        const val CONSUMER_NAME = "economy-metrics"
    }
}

data class ConsumerBatchResult(val processed: Int, val duplicates: Int)

data class MetricEvent(
    val eventId: UUID,
    val eventType: String,
    val envelope: JsonNode,
    val occurredAt: Instant,
)

interface EconomyMetricsKafkaWriter {
    fun record(events: List<MetricEvent>): ConsumerBatchResult
}

@ConditionalOnProperty(prefix = "events.kafka.consumer.economy-metrics", name = ["enabled"], havingValue = "true", matchIfMissing = true)
@Service
class JdbcEconomyMetricsKafkaWriter(private val jdbc: JdbcClient) : EconomyMetricsKafkaWriter {
    @Transactional
    override fun record(events: List<MetricEvent>): ConsumerBatchResult {
        val uniqueEvents = events.distinctBy { it.eventId }
        if (uniqueEvents.isEmpty()) return ConsumerBatchResult(0, events.size)
        val parameters = mutableMapOf<String, Any>()
        val values = uniqueEvents.mapIndexed { index, event ->
            parameters["event$index"] = event.eventId
            "(cast(:event$index as uuid),'economy-metrics')"
        }.joinToString(",")
        val insertedIds = jdbc.sql(
            "insert into processed_kafka_event(event_id,consumer_name) values $values on conflict do nothing returning event_id",
        ).params(parameters).query(UUID::class.java).list().toSet()
        if (insertedIds.isEmpty()) return ConsumerBatchResult(0, events.size)

        val accumulator = EconomyMetricAccumulator()
        uniqueEvents.filter { it.eventId in insertedIds }
            .forEach { accumulator.record(it.eventType, it.envelope, it.occurredAt) }
        accumulator.snapshot().forEach(::upsert)
        return ConsumerBatchResult(insertedIds.size, events.size - uniqueEvents.size + uniqueEvents.size - insertedIds.size)
    }

    private fun upsert(metric: EconomyDailyMetric) {
        jdbc.sql(
            """
            insert into economy_metric_daily(
              metric_date, item_family, generation, item_id, listed_quantity, cancelled_quantity, trade_count, traded_quantity, trade_amount,
              fee_amount, settlement_amount, dropped_quantity, consumed_quantity, rice_generated, rice_consumed, updated_at
            ) values (
              :metricDate, :itemFamily, :generation, :itemId, :listedQuantity, :cancelledQuantity, :tradeCount, :tradedQuantity, :tradeAmount,
              :feeAmount, :settlementAmount, :droppedQuantity, :consumedQuantity, :riceGenerated, :riceConsumed, :updatedAt
            )
            on conflict (metric_date, item_id) do update set
              item_family = excluded.item_family,
              generation = excluded.generation,
              listed_quantity = economy_metric_daily.listed_quantity + excluded.listed_quantity,
              cancelled_quantity = economy_metric_daily.cancelled_quantity + excluded.cancelled_quantity,
              trade_count = economy_metric_daily.trade_count + excluded.trade_count,
              traded_quantity = economy_metric_daily.traded_quantity + excluded.traded_quantity,
              trade_amount = economy_metric_daily.trade_amount + excluded.trade_amount,
              fee_amount = economy_metric_daily.fee_amount + excluded.fee_amount,
              settlement_amount = economy_metric_daily.settlement_amount + excluded.settlement_amount,
              dropped_quantity = economy_metric_daily.dropped_quantity + excluded.dropped_quantity,
              consumed_quantity = economy_metric_daily.consumed_quantity + excluded.consumed_quantity,
              rice_generated = economy_metric_daily.rice_generated + excluded.rice_generated,
              rice_consumed = economy_metric_daily.rice_consumed + excluded.rice_consumed,
              updated_at = excluded.updated_at
            """.trimIndent(),
        ).params(
            mapOf(
                "metricDate" to Date.valueOf(metric.metricDate),
                "itemFamily" to metric.itemFamily,
                "generation" to metric.generation,
                "itemId" to metric.itemId,
                "listedQuantity" to metric.listedQuantity,
                "cancelledQuantity" to metric.cancelledQuantity,
                "tradeCount" to metric.tradeCount,
                "tradedQuantity" to metric.tradedQuantity,
                "tradeAmount" to metric.tradeAmount,
                "feeAmount" to metric.feeAmount,
                "settlementAmount" to metric.settlementAmount,
                "droppedQuantity" to metric.droppedQuantity,
                "consumedQuantity" to metric.consumedQuantity,
                "riceGenerated" to metric.riceGenerated,
                "riceConsumed" to metric.riceConsumed,
                "updatedAt" to Timestamp.from(Instant.now()),
            ),
        ).update()
    }
}
