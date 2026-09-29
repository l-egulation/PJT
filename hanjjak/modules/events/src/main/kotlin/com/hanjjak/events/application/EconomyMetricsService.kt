package com.hanjjak.events.application

import com.fasterxml.jackson.databind.ObjectMapper
import org.springframework.jdbc.core.simple.JdbcClient
import java.sql.Date
import java.sql.Timestamp
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset

class EconomyMetricsService(
    private val jdbc: JdbcClient,
    private val mapper: ObjectMapper,
) {
    fun refreshDaily(from: LocalDate, to: LocalDate): List<EconomyDailyMetric> {
        require(!to.isBefore(from)) { "INVALID_DATE_RANGE" }
        val accumulator = EconomyMetricAccumulator()
        val start = from.atStartOfDay().toInstant(ZoneOffset.UTC)
        val end = to.plusDays(1).atStartOfDay().toInstant(ZoneOffset.UTC)
        jdbc.sql(
            """
            select event_type, payload::text as payload, created_at
            from outbox_event
            where event_type in (
              'MARKET_LISTING_CREATED',
              'MARKET_LISTING_CANCELLED',
              'MARKET_ORDER_CREATED',
              'MARKET_ORDER_CANCELLED',
              'MARKET_TRADE_COMPLETED',
              'BATTLE_ENEMY_SETTLED',
              'STAGE_FIRST_CLEAR_REWARD',
              'STAGE_FIRST_CLEAR_REWARD_CLAIMED',
              'EQUIPMENT_CRAFTED',
              'EQUIPMENT_ENHANCEMENT_ATTEMPTED',
              'EQUIPMENT_PROMOTED'
            )
              and created_at >= :start
              and created_at < :end
            order by created_at, event_id
            """.trimIndent(),
        )
            .param("start", Timestamp.from(start))
            .param("end", Timestamp.from(end))
            .query { rs, _ ->
                EventRow(
                    eventType = rs.getString("event_type"),
                    payload = rs.getString("payload"),
                    createdAt = rs.getTimestamp("created_at").toInstant(),
                )
            }
            .list()
            .forEach { row -> accumulator.record(row.eventType, mapper.readTree(row.payload), row.createdAt) }

        val metrics = accumulator.snapshot()
        jdbc.sql("delete from economy_metric_daily where metric_date between :from and :to")
            .param("from", Date.valueOf(from))
            .param("to", Date.valueOf(to))
            .update()
        metrics.forEach(::upsert)
        return metrics
    }

    fun listDaily(from: LocalDate, to: LocalDate): List<EconomyDailyMetric> {
        require(!to.isBefore(from)) { "INVALID_DATE_RANGE" }
        return jdbc.sql(
            """
            select metric_date, item_family, generation, item_id, listed_quantity, cancelled_quantity, trade_count, traded_quantity, trade_amount,
                   fee_amount, settlement_amount, dropped_quantity, consumed_quantity, rice_generated, rice_consumed
            from economy_metric_daily
            where metric_date between :from and :to
            order by metric_date, item_id
            """.trimIndent(),
        )
            .param("from", Date.valueOf(from))
            .param("to", Date.valueOf(to))
            .query { rs, _ ->
                EconomyDailyMetric(
                    metricDate = rs.getDate("metric_date").toLocalDate(),
                    itemFamily = rs.getString("item_family"),
                    generation = rs.getObject("generation", Integer::class.java)?.toInt(),
                    itemId = rs.getString("item_id"),
                    listedQuantity = rs.getLong("listed_quantity"),
                    cancelledQuantity = rs.getLong("cancelled_quantity"),
                    tradeCount = rs.getLong("trade_count"),
                    tradedQuantity = rs.getLong("traded_quantity"),
                    tradeAmount = rs.getLong("trade_amount"),
                    feeAmount = rs.getLong("fee_amount"),
                    settlementAmount = rs.getLong("settlement_amount"),
                    droppedQuantity = rs.getLong("dropped_quantity"),
                    consumedQuantity = rs.getLong("consumed_quantity"),
                    riceGenerated = rs.getLong("rice_generated"),
                    riceConsumed = rs.getLong("rice_consumed"),
                )
            }
            .list()
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
              listed_quantity = excluded.listed_quantity,
              cancelled_quantity = excluded.cancelled_quantity,
              trade_count = excluded.trade_count,
              traded_quantity = excluded.traded_quantity,
              trade_amount = excluded.trade_amount,
              fee_amount = excluded.fee_amount,
              settlement_amount = excluded.settlement_amount,
              dropped_quantity = excluded.dropped_quantity,
              consumed_quantity = excluded.consumed_quantity,
              rice_generated = excluded.rice_generated,
              rice_consumed = excluded.rice_consumed,
              updated_at = excluded.updated_at
            """.trimIndent(),
        )
            .param("metricDate", Date.valueOf(metric.metricDate))
            .param("itemFamily", metric.itemFamily)
            .param("generation", metric.generation)
            .param("itemId", metric.itemId)
            .param("listedQuantity", metric.listedQuantity)
            .param("cancelledQuantity", metric.cancelledQuantity)
            .param("tradeCount", metric.tradeCount)
            .param("tradedQuantity", metric.tradedQuantity)
            .param("tradeAmount", metric.tradeAmount)
            .param("feeAmount", metric.feeAmount)
            .param("settlementAmount", metric.settlementAmount)
            .param("droppedQuantity", metric.droppedQuantity)
            .param("consumedQuantity", metric.consumedQuantity)
            .param("riceGenerated", metric.riceGenerated)
            .param("riceConsumed", metric.riceConsumed)
            .param("updatedAt", Timestamp.from(Instant.now()))
            .update()
    }

    private data class EventRow(val eventType: String, val payload: String, val createdAt: Instant)
}
