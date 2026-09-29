package com.hanjjak.admin.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.EconomyMetricAccumulator
import com.hanjjak.events.application.EconomyDailyMetric
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.sql.Date
import java.time.LocalDate
import java.time.ZoneOffset

@Service
class AdminEconomyReplayService(private val jdbc: JdbcClient, private val mapper: ObjectMapper) {
    data class ReplayResult(val from: LocalDate, val to: LocalDate, val eventCount: Long, val metricCount: Int)

    @Transactional
    fun replay(from: LocalDate, to: LocalDate): ReplayResult {
        require(!to.isBefore(from)) { "ADMIN_INVALID_DATE_RANGE" }
        require(to.toEpochDay() - from.toEpochDay() <= 90) { "ADMIN_REPLAY_RANGE_TOO_LARGE" }
        val start = from.atStartOfDay().toInstant(ZoneOffset.UTC)
        val end = to.plusDays(1).atStartOfDay().toInstant(ZoneOffset.UTC)
        val accumulator = EconomyMetricAccumulator()
        val events = jdbc.sql("select event_type,payload::text as payload,created_at from outbox_event where event_type in ('MARKET_LISTING_CREATED','MARKET_LISTING_CANCELLED','MARKET_ORDER_CREATED','MARKET_ORDER_CANCELLED','MARKET_TRADE_COMPLETED','ITEM_DROPPED','BATTLE_ENEMY_SETTLED','STAGE_FIRST_CLEAR_REWARD','STAGE_FIRST_CLEAR_REWARD_CLAIMED','EQUIPMENT_CRAFTED','EQUIPMENT_ENHANCEMENT_ATTEMPTED','EQUIPMENT_PROMOTED') and created_at >= :start and created_at < :end order by created_at,event_id")
            .params(mapOf("start" to java.sql.Timestamp.from(start), "end" to java.sql.Timestamp.from(end)))
            .query { row, _ -> Triple(row.getString("event_type"), row.getString("payload"), row.getTimestamp("created_at").toInstant()) }.list()
        events.forEach { (type, payload, occurredAt) -> accumulator.record(type, mapper.readTree(payload), occurredAt) }
        jdbc.sql("delete from economy_metric_daily where metric_date between :from and :to").params(mapOf("from" to Date.valueOf(from), "to" to Date.valueOf(to))).update()
        val metrics = accumulator.snapshot()
        metrics.forEach { metric -> upsert(metric) }
        return ReplayResult(from, to, events.size.toLong(), metrics.size)
    }

    private fun upsert(metric: EconomyDailyMetric) {
        jdbc.sql("insert into economy_metric_daily(metric_date,item_family,generation,item_id,listed_quantity,cancelled_quantity,trade_count,traded_quantity,trade_amount,fee_amount,settlement_amount,dropped_quantity,consumed_quantity,rice_generated,rice_consumed,updated_at) values (:date,:family,:generation,:item,:listed,:cancelled,:trades,:quantity,:amount,:fee,:settlement,:dropped,:consumed,:generated,:riceConsumed,now()) on conflict (metric_date,item_id) do update set item_family=excluded.item_family,generation=excluded.generation,listed_quantity=excluded.listed_quantity,cancelled_quantity=excluded.cancelled_quantity,trade_count=excluded.trade_count,traded_quantity=excluded.traded_quantity,trade_amount=excluded.trade_amount,fee_amount=excluded.fee_amount,settlement_amount=excluded.settlement_amount,dropped_quantity=excluded.dropped_quantity,consumed_quantity=excluded.consumed_quantity,rice_generated=excluded.rice_generated,rice_consumed=excluded.rice_consumed,updated_at=now())")
            .params(mapOf("date" to Date.valueOf(metric.metricDate), "family" to metric.itemFamily, "generation" to metric.generation, "item" to metric.itemId, "listed" to metric.listedQuantity, "cancelled" to metric.cancelledQuantity, "trades" to metric.tradeCount, "quantity" to metric.tradedQuantity, "amount" to metric.tradeAmount, "fee" to metric.feeAmount, "settlement" to metric.settlementAmount, "dropped" to metric.droppedQuantity, "consumed" to metric.consumedQuantity, "generated" to metric.riceGenerated, "riceConsumed" to metric.riceConsumed)).update()
    }
}
