package com.hanjjak.admin.application

import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import java.time.Instant
import java.util.UUID

@Service
class AdminAnomalyService(private val jdbc: JdbcClient) {
    data class Alert(val alertId: UUID, val itemId: String, val tradeId: UUID, val alertType: String, val unitPrice: Long, val referencePrice: Double, val deviationRatio: Double, val occurredAt: Instant, val createdAt: Instant)

    fun list(limit: Int): List<Alert> = jdbc.sql("select alert_id,item_id,trade_id,alert_type,unit_price,reference_price,deviation_ratio,occurred_at,created_at from market_anomaly_alert order by created_at desc limit :limit")
        .param("limit", limit.coerceIn(1, 100)).query { row, _ -> Alert(row.getObject("alert_id", UUID::class.java), row.getString("item_id"), row.getObject("trade_id", UUID::class.java), row.getString("alert_type"), row.getLong("unit_price"), row.getBigDecimal("reference_price").toDouble(), row.getBigDecimal("deviation_ratio").toDouble(), row.getTimestamp("occurred_at").toInstant(), row.getTimestamp("created_at").toInstant()) }.list()
}
