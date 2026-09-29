package com.hanjjak.eventconsumers

import org.junit.jupiter.api.AfterAll
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.jdbc.datasource.DriverManagerDataSource
import org.testcontainers.containers.PostgreSQLContainer
import java.math.BigDecimal
import java.sql.DriverManager
import java.time.Instant
import java.util.UUID

class JdbcMarketPriceSnapshotWriterTest {
    private val jdbc = JdbcClient.create(DriverManagerDataSource(database.jdbcUrl, database.username, database.password))
    private val writer = JdbcMarketPriceSnapshotWriter(jdbc)

    @Test
    fun `records and accumulates market price snapshots`() {
        val itemId = "test-${UUID.randomUUID()}"
        val first = trade(itemId, quantity = 3, unitPrice = 50, totalPrice = 150, occurredAt = "2026-09-12T00:00:00Z")
        val latest = trade(itemId, quantity = 2, unitPrice = 60, totalPrice = 120, occurredAt = "2026-09-12T00:00:01Z")

        assertEquals(ConsumerBatchResult(2, 0), writer.recordAll(listOf(first, latest)))
        assertEquals(ConsumerBatchResult(0, 2), writer.recordAll(listOf(first, latest)))

        val snapshot = jdbc.sql("select last_unit_price,average_unit_price,trade_count,traded_quantity,trade_amount,fee_amount,settlement_amount,last_event_id from market_price_snapshot where item_id=:itemId")
            .param("itemId", itemId)
            .query { row, _ ->
                Snapshot(
                    row.getLong("last_unit_price"),
                    row.getBigDecimal("average_unit_price"),
                    row.getLong("trade_count"),
                    row.getLong("traded_quantity"),
                    row.getLong("trade_amount"),
                    row.getLong("fee_amount"),
                    row.getLong("settlement_amount"),
                    row.getObject("last_event_id", UUID::class.java),
                )
            }
            .single()
        assertEquals(Snapshot(60, BigDecimal("54.00"), 2, 5, 270, 27, 243, latest.eventId), snapshot)
    }

    private fun trade(itemId: String, quantity: Long, unitPrice: Long, totalPrice: Long, occurredAt: String) =
        MarketTradeCompletedEnvelope(
            eventId = UUID.randomUUID(),
            itemId = itemId,
            quantity = quantity,
            unitPrice = unitPrice,
            totalPrice = totalPrice,
            feeAmount = totalPrice / 10,
            settlementAmount = totalPrice - totalPrice / 10,
            occurredAt = Instant.parse(occurredAt),
        )

    private data class Snapshot(
        val lastUnitPrice: Long,
        val averageUnitPrice: BigDecimal,
        val tradeCount: Long,
        val tradedQuantity: Long,
        val tradeAmount: Long,
        val feeAmount: Long,
        val settlementAmount: Long,
        val lastEventId: UUID,
    )

    companion object {
        private val externalDatabase = System.getenv("TEST_DATABASE_URL")?.takeIf(String::isNotBlank)
        private val database = externalDatabase?.let {
            Database(it, requireNotNull(System.getenv("TEST_DATABASE_USER")), requireNotNull(System.getenv("TEST_DATABASE_PASSWORD")))
        } ?: PostgreSQLContainer<Nothing>("postgres:17.11-alpine").apply { start() }.let { container ->
            Database(container.jdbcUrl, container.username, container.password, container)
        }

        init {
            DriverManager.getConnection(database.jdbcUrl, database.username, database.password).use { connection ->
                connection.createStatement().use { statement ->
                    statement.execute("create table processed_kafka_event(event_id uuid not null, consumer_name varchar(120) not null, primary key(event_id,consumer_name))")
                    statement.execute("create table market_price_snapshot(item_id varchar(120) primary key,last_unit_price bigint not null,average_unit_price numeric(19,2) not null,trade_count bigint not null,traded_quantity bigint not null,trade_amount bigint not null,fee_amount bigint not null,settlement_amount bigint not null,last_trade_at timestamptz not null,last_event_id uuid,updated_at timestamptz not null)")
                    statement.execute("create table market_anomaly_alert(alert_id uuid primary key,item_id varchar(120) not null,trade_id uuid not null unique,alert_type varchar(80) not null,unit_price bigint not null,reference_price numeric(19,2) not null,deviation_ratio numeric not null,occurred_at timestamptz not null)")
                }
            }
        }

        @JvmStatic
        @AfterAll
        fun stopDatabase() {
            database.container?.stop()
        }

        private data class Database(
            val jdbcUrl: String,
            val username: String,
            val password: String,
            val container: PostgreSQLContainer<Nothing>? = null,
        )
    }
}
