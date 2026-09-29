package com.hanjjak.marketbenchmark

import java.nio.file.Files
import java.nio.file.Path
import java.sql.Connection
import java.sql.DriverManager
import java.time.Duration
import java.time.Instant
import java.util.Collections
import java.util.UUID
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicLong
import kotlin.math.max

fun runOutboxLoadBenchmark(args: Array<String>): OutboxLoadSummary {
    val config = OutboxLoadConfig.from(args, System.getenv()).normalized()
    config.validateSafety()
    DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        connection.autoCommit = false
        createOutboxTable(connection)
        if (config.resetOutbox) resetOutbox(connection)
        seedOutbox(connection, config)
        connection.commit()
    }
    val scenarios = config.workerCounts.flatMap { workers ->
        config.batchSizes.map { batchSize ->
            resetPending(config)
            drainOutbox(config, workers, batchSize)
        }
    }
    val aggregateMillis = measureAggregate(config)
    val summary = OutboxLoadSummary(
        runId = config.runId,
        startedAt = Instant.now().toString(),
        eventCount = config.eventCount,
        batchSizes = config.batchSizes,
        workerCounts = config.workerCounts,
        aggregateMillis = aggregateMillis,
        scenarios = scenarios,
        kafkaSignal = kafkaSignal(scenarios, aggregateMillis),
    )
    if (config.outputPath.isNotBlank()) {
        val output = Path.of(config.outputPath)
        Files.createDirectories(output.parent ?: Path.of("."))
        Files.writeString(output, summary.toJson())
    }
    println(summary.toJson())
    return summary
}

fun cleanOutboxLoadBenchmark(args: Array<String>): Long {
    val config = OutboxLoadConfig.from(args, System.getenv())
    require(config.runId.isNotBlank()) { "--run-id is required for outbox-clean" }
    if (!config.allowNonLocalDb) {
        require(config.dbUrl.contains("localhost") || config.dbUrl.contains("127.0.0.1")) { "outbox-clean requires localhost DB or --allow-non-local-db true" }
    }
    val deleted = DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        connection.prepareStatement("delete from outbox_event where payload->>'runId'=?").use { statement ->
            statement.setString(1, config.runId)
            statement.executeUpdate().toLong()
        }
    }
    println("deleted=$deleted runId=${config.runId}")
    return deleted
}

data class OutboxLoadConfig(
    val dbUrl: String,
    val dbUser: String,
    val dbPassword: String,
    val runId: String,
    val eventCount: Int,
    val batchSizes: List<Int>,
    val workerCounts: List<Int>,
    val resetOutbox: Boolean,
    val allowNonLocalDb: Boolean,
    val outputPath: String,
) {
    fun normalized(): OutboxLoadConfig = if (runId.isBlank()) copy(runId = "outbox-load-${timestamp()}") else this

    fun validateSafety() {
        require(eventCount > 0) { "event-count must be positive" }
        require(batchSizes.all { it > 0 }) { "batch sizes must be positive" }
        require(workerCounts.all { it > 0 }) { "worker counts must be positive" }
        if (resetOutbox && !allowNonLocalDb) {
            require(dbUrl.contains("localhost") || dbUrl.contains("127.0.0.1")) { "reset-outbox requires localhost DB or --allow-non-local-db true" }
        }
    }

    companion object {
        fun from(args: Array<String>, env: Map<String, String>): OutboxLoadConfig {
            val values = keyValues(args)
            fun value(key: String, default: String): String = values[key] ?: env[key.uppercase().replace('-', '_')] ?: default
            return OutboxLoadConfig(
                dbUrl = value("db-url", env["DB_URL"] ?: "jdbc:postgresql://localhost:5432/hanjjak"),
                dbUser = value("db-user", env["DB_USER"] ?: "hanjjak"),
                dbPassword = value("db-password", env["DB_PASSWORD"] ?: "local-only"),
                runId = value("run-id", ""),
                eventCount = value("event-count", "100000").toInt(),
                batchSizes = value("batch-sizes", "100,1000,5000").split(',').map { it.trim().toInt() },
                workerCounts = value("worker-counts", "1,2,4").split(',').map { it.trim().toInt() },
                resetOutbox = value("reset-outbox", "true").toBooleanStrict(),
                allowNonLocalDb = value("allow-non-local-db", "false").toBooleanStrict(),
                outputPath = value("output-path", "build/reports/outbox-load/outbox-load-${timestamp()}.json"),
            )
        }
    }
}

data class OutboxDrainScenario(
    val workers: Int,
    val batchSize: Int,
    val processed: Long,
    val wallMillis: Long,
    val throughputPerSecond: Double,
    val p95BatchMillis: Long,
    val duplicateProcessed: Boolean,
)

data class OutboxLoadSummary(
    val runId: String,
    val startedAt: String,
    val eventCount: Int,
    val batchSizes: List<Int>,
    val workerCounts: List<Int>,
    val aggregateMillis: Long,
    val scenarios: List<OutboxDrainScenario>,
    val kafkaSignal: String,
) {
    fun toJson(): String = buildString {
        appendLine("{")
        field("runId", runId); comma()
        field("startedAt", startedAt); comma()
        field("eventCount", eventCount); comma()
        append("  \"batchSizes\": "); append(batchSizes.joinToString(prefix = "[", postfix = "]")); comma()
        append("  \"workerCounts\": "); append(workerCounts.joinToString(prefix = "[", postfix = "]")); comma()
        field("aggregateMillis", aggregateMillis); comma()
        field("kafkaSignal", kafkaSignal); comma()
        appendLine("  \"scenarios\": [")
        scenarios.forEachIndexed { index, scenario ->
            appendLine("    {")
            appendLine("      \"workers\": ${scenario.workers},")
            appendLine("      \"batchSize\": ${scenario.batchSize},")
            appendLine("      \"processed\": ${scenario.processed},")
            appendLine("      \"wallMillis\": ${scenario.wallMillis},")
            appendLine("      \"throughputPerSecond\": ${fmt(scenario.throughputPerSecond)},")
            appendLine("      \"p95BatchMillis\": ${scenario.p95BatchMillis},")
            appendLine("      \"duplicateProcessed\": ${scenario.duplicateProcessed}")
            append("    }")
            appendLine(if (index == scenarios.lastIndex) "" else ",")
        }
        appendLine("  ]")
        appendLine("}")
    }
}

private fun createOutboxTable(connection: Connection) {
    connection.createStatement().use { statement ->
        statement.executeUpdate(
            """
            create table if not exists outbox_event (
              event_id uuid primary key,
              event_type varchar(120) not null,
              aggregate_id uuid not null,
              payload jsonb not null,
              status varchar(16) not null default 'PENDING' check (status in ('PENDING','SENT','FAILED')),
              attempt_count integer not null default 0 check (attempt_count >= 0),
              next_retry_at timestamptz,
              created_at timestamptz not null default now()
            )
            """.trimIndent(),
        )
        statement.executeUpdate("create index if not exists outbox_event_pending_idx on outbox_event(status, next_retry_at, created_at) where status = 'PENDING'")
    }
}

private fun resetOutbox(connection: Connection) {
    connection.createStatement().use { statement -> statement.executeUpdate("truncate table outbox_event") }
}

private fun seedOutbox(connection: Connection, config: OutboxLoadConfig) {
    val sql = "insert into outbox_event(event_id,event_type,aggregate_id,payload,status,created_at) values (?,?,?,cast(? as jsonb), 'PENDING', now() - (? * interval '1 millisecond'))"
    connection.prepareStatement(sql).use { statement ->
        repeat(config.eventCount) { index ->
            val eventType = EVENT_TYPES[index % EVENT_TYPES.size]
            val itemId = ITEM_IDS[index % ITEM_IDS.size]
            val quantity = (index % 20) + 1
            statement.setObject(1, UUID.nameUUIDFromBytes("${config.runId}:$index".toByteArray()))
            statement.setString(2, eventType)
            statement.setObject(3, UUID.nameUUIDFromBytes("account:${index % 1000}".toByteArray()))
            statement.setString(4, payload(config.runId, eventType, itemId, quantity))
            statement.setInt(5, config.eventCount - index)
            statement.addBatch()
            if ((index + 1) % 1_000 == 0) statement.executeBatch()
        }
        statement.executeBatch()
    }
}

private fun resetPending(config: OutboxLoadConfig) {
    DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        connection.prepareStatement("update outbox_event set status='PENDING', attempt_count=0 where payload->>'runId'=?").use { statement ->
            statement.setString(1, config.runId)
            statement.executeUpdate()
        }
    }
}

private fun drainOutbox(config: OutboxLoadConfig, workers: Int, batchSize: Int): OutboxDrainScenario {
    val processed = AtomicLong(0)
    val batchMillis = Collections.synchronizedList(mutableListOf<Long>())
    val pool = Executors.newFixedThreadPool(workers)
    val started = System.nanoTime()
    repeat(workers) {
        pool.execute {
            DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
                connection.autoCommit = false
                while (processed.get() < config.eventCount) {
                    val batchStarted = System.nanoTime()
                    val ids = selectPending(connection, config.runId, batchSize)
                    if (ids.isEmpty()) {
                        connection.commit()
                        if (processed.get() >= config.eventCount) break
                        Thread.sleep(2)
                        continue
                    }
                    markSent(connection, ids)
                    connection.commit()
                    batchMillis += TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - batchStarted)
                    processed.addAndGet(ids.size.toLong())
                }
            }
        }
    }
    pool.shutdown()
    require(pool.awaitTermination(10, TimeUnit.MINUTES)) { "outbox drain timed out" }
    val wallMillis = max(1, TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - started))
    val sentRows = sentRows(config)
    return OutboxDrainScenario(
        workers = workers,
        batchSize = batchSize,
        processed = sentRows,
        wallMillis = wallMillis,
        throughputPerSecond = sentRows * 1000.0 / wallMillis,
        p95BatchMillis = percentile(batchMillis.sorted(), 0.95),
        duplicateProcessed = processed.get() != sentRows,
    )
}

private fun selectPending(connection: Connection, runId: String, batchSize: Int): List<UUID> = connection.prepareStatement(
    """
    select event_id
    from outbox_event
    where status='PENDING' and payload->>'runId'=? and (next_retry_at is null or next_retry_at <= now())
    order by created_at
    limit ?
    for update skip locked
    """.trimIndent(),
).use { statement ->
    statement.setString(1, runId)
    statement.setInt(2, batchSize)
    statement.executeQuery().use { rs ->
        buildList {
            while (rs.next()) add(rs.getObject(1, UUID::class.java))
        }
    }
}

private fun markSent(connection: Connection, ids: List<UUID>) {
    connection.prepareStatement("update outbox_event set status='SENT', attempt_count=attempt_count+1 where event_id = any (?)").use { statement ->
        statement.setArray(1, connection.createArrayOf("uuid", ids.toTypedArray()))
        statement.executeUpdate()
    }
}

private fun sentRows(config: OutboxLoadConfig): Long = DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
    connection.prepareStatement("select count(*) from outbox_event where payload->>'runId'=? and status='SENT'").use { statement ->
        statement.setString(1, config.runId)
        statement.executeQuery().use { rs -> rs.next(); rs.getLong(1) }
    }
}

private fun measureAggregate(config: OutboxLoadConfig): Long = DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
    val started = System.nanoTime()
    connection.prepareStatement(
        """
        select coalesce(payload->>'itemId', 'RICE') as item_id,
               count(*) as event_count,
               coalesce(sum((payload->>'quantity')::bigint), 0) as quantity
        from outbox_event
        where payload->>'runId'=?
        group by item_id
        order by item_id
        """.trimIndent(),
    ).use { statement ->
        statement.setString(1, config.runId)
        statement.executeQuery().use { rs -> while (rs.next()) rs.getString(1) }
    }
    TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - started)
}

private fun kafkaSignal(scenarios: List<OutboxDrainScenario>, aggregateMillis: Long): String {
    val best = scenarios.maxOfOrNull { it.throughputPerSecond } ?: 0.0
    return when {
        scenarios.any { it.duplicateProcessed } -> "consumer-idempotency-required"
        aggregateMillis > 5_000 -> "db-aggregate-pressure"
        best < 5_000.0 -> "kafka-useful-if-event-tps-exceeds-db-drain"
        else -> "db-polling-ok-for-single-consumer-baseline"
    }
}

private fun keyValues(args: Array<String>): Map<String, String> {
    val values = mutableMapOf<String, String>()
    var index = 0
    while (index < args.size) {
        val key = args[index]
        require(key.startsWith("--")) { "expected --key at ${args[index]}" }
        require(index + 1 < args.size) { "missing value for $key" }
        values[key.removePrefix("--")] = args[index + 1]
        index += 2
    }
    return values
}

private fun payload(runId: String, eventType: String, itemId: String, quantity: Int): String = when (eventType) {
    "BATTLE_CYCLE_COMPLETED" -> "{\"runId\":\"$runId\",\"riceGained\":$quantity}"
    "MARKET_TRADE_COMPLETED" -> "{\"runId\":\"$runId\",\"itemId\":\"$itemId\",\"quantity\":$quantity,\"totalPrice\":${quantity * 100},\"feeAmount\":${quantity * 10},\"settlementAmount\":${quantity * 90}}"
    "EQUIPMENT_CRAFTED" -> "{\"runId\":\"$runId\",\"itemId\":\"equipment:weapon:normal\",\"quantity\":1,\"consumedItems\":[{\"itemId\":\"$itemId\",\"quantity\":$quantity}],\"riceCost\":120}"
    else -> "{\"runId\":\"$runId\",\"itemId\":\"$itemId\",\"quantity\":$quantity}"
}

private val EVENT_TYPES = listOf("BATTLE_CYCLE_COMPLETED", "ITEM_DROPPED", "EQUIPMENT_CRAFTED", "MARKET_LISTING_CREATED", "MARKET_TRADE_COMPLETED")
private val ITEM_IDS = listOf("POTATO_M1", "SWEET_POTATO_M1", "CORN_M1", "POTATO_M2", "SWEET_POTATO_M2", "CORN_M2")
