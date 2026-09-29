package com.hanjjak.eventconsumers

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.kafka.annotation.KafkaListener
import org.springframework.kafka.listener.BatchListenerFailedException
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.math.BigDecimal
import java.math.RoundingMode
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID

@Service
@ConditionalOnProperty(prefix = "events.kafka.consumer.price-snapshot", name = ["enabled"], havingValue = "true", matchIfMissing = true)
class MarketPriceSnapshotConsumer(
    private val mapper: ObjectMapper,
    private val writer: MarketPriceSnapshotWriter,
    private val metrics: KafkaOpsMetrics,
) {
    @KafkaListener(topics = ["\${events.kafka.topic:domain-events.v1}"], groupId = "\${spring.kafka.consumer.group-id:event-consumers}")
    fun consume(messages: List<String>) {
        val startedAt = Instant.now()
        metrics.consumerBatch(CONSUMER_NAME, messages.size)
        try {
            val trades = mutableListOf<MarketTradeCompletedEnvelope>()
            messages.forEachIndexed { index, message ->
                try {
                    val root = mapper.readTree(message)
                    if (root.path("eventType").asText() == "MARKET_TRADE_COMPLETED") {
                        trades += MarketTradeCompletedEnvelope.from(root)
                    }
                } catch (error: Exception) {
                    writer.recordAll(trades)
                    throw BatchListenerFailedException("Invalid domain event at batch index $index", error, index)
                }
            }
            val result = writer.recordAll(trades)
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
        const val CONSUMER_NAME = "market-price-snapshot"
    }
}

data class MarketTradeCompletedEnvelope(
    val eventId: UUID,
    val itemId: String,
    val quantity: Long,
    val unitPrice: Long,
    val totalPrice: Long,
    val feeAmount: Long,
    val settlementAmount: Long,
    val occurredAt: Instant,
) {
    companion object {
        fun from(root: JsonNode): MarketTradeCompletedEnvelope {
            val payload = root.path("payload")
            return MarketTradeCompletedEnvelope(
                eventId = UUID.fromString(root.path("eventId").asText()),
                itemId = text(root, payload, "itemId"),
                quantity = long(root, payload, "quantity"),
                unitPrice = long(root, payload, "unitPrice"),
                totalPrice = long(root, payload, "totalPrice"),
                feeAmount = long(root, payload, "feeAmount", "fee"),
                settlementAmount = long(root, payload, "settlementAmount"),
                occurredAt = Instant.parse(root.path("occurredAt").asText()),
            )
        }

        private fun text(root: JsonNode, payload: JsonNode, field: String): String = root.path(field).takeIf { it.isTextual }?.asText()
            ?: payload.path(field).asText()

        private fun long(root: JsonNode, payload: JsonNode, field: String, payloadField: String = field): Long = root.path(field).takeIf { it.isNumber }?.asLong()
            ?: payload.path(payloadField).asLong()
    }
}

interface MarketPriceSnapshotWriter {
    fun recordAll(events: List<MarketTradeCompletedEnvelope>): ConsumerBatchResult
}

@Service
class JdbcMarketPriceSnapshotWriter(private val jdbc: JdbcClient) : MarketPriceSnapshotWriter {
    @Transactional
    override fun recordAll(events: List<MarketTradeCompletedEnvelope>): ConsumerBatchResult {
        val uniqueEvents = events.distinctBy { it.eventId }
        if (uniqueEvents.isEmpty()) return ConsumerBatchResult(0, events.size)
        val parameters = mutableMapOf<String, Any>()
        val values = uniqueEvents.mapIndexed { index, event ->
            parameters["event$index"] = event.eventId
            "(cast(:event$index as uuid),'market-price-snapshot')"
        }.joinToString(",")
        val insertedIds = jdbc.sql("insert into processed_kafka_event(event_id,consumer_name) values $values on conflict do nothing returning event_id").params(parameters).query(UUID::class.java).list().toSet()
        if (insertedIds.isEmpty()) return ConsumerBatchResult(0, events.size)
        uniqueEvents.asSequence().filter { it.eventId in insertedIds }.groupBy { it.itemId }.values.forEach(::recordItemBatch)
        return ConsumerBatchResult(insertedIds.size, events.size - insertedIds.size)
    }

    private fun recordItemBatch(events: List<MarketTradeCompletedEnvelope>) {
        val latest = events.maxWith(compareBy<MarketTradeCompletedEnvelope> { it.occurredAt }.thenBy { it.eventId })
        val reference = jdbc.sql("select average_unit_price from market_price_snapshot where item_id=:itemId").param("itemId", latest.itemId).query(BigDecimal::class.java).optional().orElse(null)
        if (reference != null && reference > BigDecimal.ZERO && latest.unitPrice.toBigDecimal() >= reference.multiply(BigDecimal("2"))) {
            jdbc.sql("insert into market_anomaly_alert(alert_id,item_id,trade_id,alert_type,unit_price,reference_price,deviation_ratio,occurred_at) values (:alertId,:itemId,:tradeId,'PRICE_SPIKE',:unitPrice,:reference,:ratio,:occurredAt) on conflict (trade_id) do nothing")
                .params(mapOf("alertId" to UUID.randomUUID(), "itemId" to latest.itemId, "tradeId" to latest.eventId, "unitPrice" to latest.unitPrice, "reference" to reference, "ratio" to latest.unitPrice.toBigDecimal().divide(reference, 6, RoundingMode.HALF_UP), "occurredAt" to Timestamp.from(latest.occurredAt))).update()
        }
        val tradedQuantity = events.sumOf { it.quantity }; val tradeAmount = events.sumOf { it.totalPrice }
        jdbc.sql("insert into market_price_snapshot(item_id,last_unit_price,average_unit_price,trade_count,traded_quantity,trade_amount,fee_amount,settlement_amount,last_trade_at,last_event_id,updated_at) values (:itemId,:unitPrice,round(cast(:tradeAmount as numeric) / nullif(:tradedQuantity,0),2),:tradeCount,:tradedQuantity,:tradeAmount,:feeAmount,:settlementAmount,:lastTradeAt,:lastEventId,now()) on conflict (item_id) do update set last_unit_price=excluded.last_unit_price, last_event_id=excluded.last_event_id, trade_count=market_price_snapshot.trade_count+excluded.trade_count, traded_quantity=market_price_snapshot.traded_quantity+excluded.traded_quantity, trade_amount=market_price_snapshot.trade_amount+excluded.trade_amount, fee_amount=market_price_snapshot.fee_amount+excluded.fee_amount, settlement_amount=market_price_snapshot.settlement_amount+excluded.settlement_amount, average_unit_price=round(((market_price_snapshot.trade_amount+excluded.trade_amount)::numeric/nullif(market_price_snapshot.traded_quantity+excluded.traded_quantity,0)),2), last_trade_at=greatest(market_price_snapshot.last_trade_at,excluded.last_trade_at), updated_at=now()")
            .params(mapOf("itemId" to latest.itemId, "unitPrice" to latest.unitPrice, "tradeCount" to events.size, "tradedQuantity" to tradedQuantity, "tradeAmount" to tradeAmount, "feeAmount" to events.sumOf { it.feeAmount }, "settlementAmount" to events.sumOf { it.settlementAmount }, "lastTradeAt" to Timestamp.from(latest.occurredAt), "lastEventId" to latest.eventId)).update()
    }
}
