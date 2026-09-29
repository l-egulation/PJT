package com.hanjjak.admin.application

import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service

@Service
class AdminMarketQualityService(private val jdbc: JdbcClient) {
    data class Finding(val check: String, val count: Long, val detail: String)
    data class Report(val passed: Boolean, val findings: List<Finding>)

    fun inspect(): Report {
        val findings = listOf(
            Finding("ORDER_REMAINING", jdbc.sql("select count(*) from market_order where remaining_quantity < 0 or filled_quantity < 0 or filled_quantity + remaining_quantity > initial_quantity").query(Long::class.java).single(), "주문 체결·잔량이 초기 수량 범위를 벗어난 행"),
            Finding("TRADE_AMOUNT", jdbc.sql("select count(*) from market_trade where total_price <> quantity * unit_price").query(Long::class.java).single(), "거래 총액이 수량×단가와 다른 행"),
            Finding("TRADE_FEE", jdbc.sql("select count(*) from market_trade where fee <> floor(total_price * 0.1)").query(Long::class.java).single(), "거래 수수료가 10% 규칙과 다른 행"),
            Finding("TRADE_SETTLEMENT", jdbc.sql("select count(*) from market_trade where settlement_amount <> total_price - fee").query(Long::class.java).single(), "정산액이 총액-수수료와 다른 행"),
            Finding("DUPLICATE_PROCESSED_EVENT", jdbc.sql("select count(*) from (select event_id from processed_kafka_event group by event_id,consumer_name having count(*) > 1) duplicates").query(Long::class.java).single(), "Kafka 소비자별 중복 처리 기록"),
            Finding("PRICE_SNAPSHOT_NEGATIVE", jdbc.sql("select count(*) from market_price_snapshot where trade_count < 0 or traded_quantity < 0 or trade_amount < 0").query(Long::class.java).single(), "가격 스냅샷 음수 누적값"),
        )
        return Report(findings.all { it.count == 0L }, findings)
    }
}
