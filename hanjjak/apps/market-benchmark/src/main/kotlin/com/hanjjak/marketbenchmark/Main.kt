package com.hanjjak.marketbenchmark

import java.net.URI
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.net.http.HttpTimeoutException
import java.nio.file.Files
import java.nio.file.Path
import java.sql.Connection
import java.sql.DriverManager
import java.sql.ResultSet
import java.io.IOException
import java.time.Duration
import java.time.Instant
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import java.util.Collections
import java.util.Locale
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.math.max

fun main(args: Array<String>) {
    if (args.asList().windowed(2, 1).any { it == listOf("--mode", "outbox-clean") }) {
        cleanOutboxLoadBenchmark(args)
        return
    }
    if (args.asList().windowed(2, 1).any { it == listOf("--mode", "outbox-load") }) {
        runOutboxLoadBenchmark(args)
        return
    }
    if (args.asList().windowed(2, 1).any { it == listOf("--mode", "prepare-accounts") }) {
        prepareAccountFixture(Config.from(args, System.getenv()).normalizedSingle())
        return
    }
    val config = Config.from(args, System.getenv())
    if (config.seriesConcurrency.isNotEmpty()) {
        runSeries(config)
        return
    }
    val summary = runSingle(config.normalizedSingle())
    println(summary.toJson())
    require(summary.invariants.values.all { it }) { "market invariants failed: ${summary.invariants.filterValues { !it }.keys}" }
}

fun runSeries(config: Config) {
    require(config.outputDir.isNotBlank()) { "--output-dir is required for series mode" }
    val prefix = config.runId.ifBlank { "market-series-${timestamp()}" }
    val root = Path.of(config.outputDir)
    val raw = root.resolve("raw")
    Files.createDirectories(raw)
    val summaries = mutableListOf<BenchmarkSummary>()
    config.seriesConcurrency.forEach { concurrency ->
        repeat(config.repetitions) { repetitionIndex ->
            val runId = "$prefix-c${concurrency.toString().padStart(3, '0')}-r${(repetitionIndex + 1).toString().padStart(2, '0')}"
            val output = raw.resolve("c${concurrency.toString().padStart(3, '0')}-r${(repetitionIndex + 1).toString().padStart(2, '0')}.json")
            val single = config.copy(runId = runId, concurrency = concurrency, outputPath = output.toString(), resetItemData = true)
            println("series run concurrency=$concurrency repetition=${repetitionIndex + 1}/${config.repetitions}")
            val summary = runSingle(single.normalizedSingle())
            require(summary.invariants.values.all { it }) { "market invariants failed for $runId: ${summary.invariants.filterValues { !it }.keys}" }
            summaries += summary
        }
    }
    Files.writeString(root.resolve("summary.csv"), summaryCsv(summaries))
    Files.writeString(root.resolve("chart-data.json"), chartDataJson(summaries))
    Files.writeString(root.resolve("summary.md"), summaryMarkdown(config, summaries))
    println("series result written ${root.toAbsolutePath()}")
    println(summaryMarkdown(config, summaries))
}

fun runSingle(config: Config): BenchmarkSummary {
    val runId = config.runId.ifBlank { "market-baseline-${timestamp()}" }
    val startedAt = Instant.now()
    println("market benchmark start runId=$runId baseUrl=${config.apiBaseUrl} item=${config.itemId} concurrency=${config.concurrency} totalRequests=${config.totalRequests}")

    if (config.resetItemData) {
        DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
            connection.autoCommit = false
            resetItemData(connection, config.itemId)
            connection.commit()
        }
    }

    val sellers = if (config.preparedAccounts) {
        loginPreparedAccounts(config, PreparedAccountRole.SELLER, config.sellers)
    } else {
        (1..config.sellers).map { index ->
            ApiSession(config.apiBaseUrl).also { session ->
                session.accountId = session.signupOrLogin("$runId-seller-$index@example.com", config.password, "판매자$index", config.itemId.toMaterialType())
            }
        }
    }
    val buyers = if (config.preparedAccounts) {
        loginPreparedAccounts(config, PreparedAccountRole.BUYER, config.buyers)
    } else {
        (1..config.buyers).map { index ->
            ApiSession(config.apiBaseUrl).also { session ->
                session.accountId = session.signupOrLogin("$runId-buyer-$index@example.com", config.password, "구매자$index", config.itemId.toMaterialType())
            }
        }
    }

    DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        connection.autoCommit = false
        seedData(connection, config, sellers.map { it.accountId }, buyers.map { it.accountId })
        connection.commit()
    }
    val instrumentId = DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        connection.prepareStatement("select instrument_id from market_instrument where item_id=? and status='ACTIVE'").use { statement ->
            statement.setString(1, config.itemId)
            statement.executeQuery().use { rows -> require(rows.next()) { "market instrument missing for ${config.itemId}" }; rows.getObject(1, UUID::class.java) }
        }
    }

    val listingIds = sellers.flatMapIndexed { sellerIndex, seller ->
        (1..config.listingsPerSeller).map { listingIndex ->
            val price = config.unitPrice + (sellerIndex + listingIndex - 1) * 10L
            val result = seller.postJson("/api/v1/market/orders", "{\"instrumentId\":\"$instrumentId\",\"side\":\"SELL\",\"timeInForce\":\"GTC\",\"quantity\":${config.listingQuantity},\"limitUnitPrice\":$price}")
            require(result.statusCode in 200..299) { "sell order create failed status=${result.statusCode} body=${result.body}" }
            extractString(result.body, "orderId") ?: error("orderId missing: ${result.body}")
        }
    }

    val before = DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        DbSnapshot.capture(connection, config.itemId, sellers.map { it.accountId }, buyers.map { it.accountId }, listingIds)
    }

    val sampler = LockSampler(config)
    sampler.start()
    val workerPool = Executors.newVirtualThreadPerTaskExecutor()
    val startGate = CountDownLatch(1)
    val doneGate = CountDownLatch(config.totalRequests)
    val results = Collections.synchronizedList(mutableListOf<RequestResult>())
    repeat(config.totalRequests) { requestIndex ->
        workerPool.execute {
            startGate.await()
            val buyer = buyers[requestIndex % buyers.size]
            val requestStart = System.nanoTime()
            val result = try {
                synchronized(buyer) {
                    val response = buyer.postJson("/api/v1/market/orders", "{\"instrumentId\":\"$instrumentId\",\"side\":\"BUY\",\"timeInForce\":\"IOC\",\"quantity\":${config.purchaseQuantity},\"limitUnitPrice\":999999}")
                    val elapsedMillis = TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - requestStart)
                    val purchasedQuantity = if (response.statusCode in 200..299) extractLong(response.body, "filledQuantity") ?: 0L else 0L
                    val errorCode = if (response.statusCode in 200..299) null else extractString(response.body, "code") ?: "HTTP_${response.statusCode}"
                    RequestResult(response.statusCode, elapsedMillis, purchasedQuantity, errorCode)
                }
            } catch (error: Exception) {
                failedRequestResult(error, TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - requestStart), requestIndex, buyer.accountId)
            }
            results += result
            doneGate.countDown()
        }
    }
    val wallStart = System.nanoTime()
    startGate.countDown()
    val completed = doneGate.await(config.timeoutSeconds, TimeUnit.SECONDS)
    val wallMillis = TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - wallStart)
    workerPool.shutdownNow()
    sampler.stop()
    require(completed) { "benchmark timed out completed=${results.size}/${config.totalRequests}" }

    val after = DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        DbSnapshot.capture(connection, config.itemId, sellers.map { it.accountId }, buyers.map { it.accountId }, listingIds)
    }

    val sorted = results.map { it.latencyMillis }.sorted()
    val successCount = results.count { it.statusCode in 200..299 }
    val totalPurchased = results.sumOf { it.purchasedQuantity }
    val requestedQuantity = config.totalRequests.toLong() * config.purchaseQuantity
    val fillRate = if (requestedQuantity == 0L) 0.0 else totalPurchased.toDouble() / requestedQuantity.toDouble()
    val actualTradeTotal = after.tradeTotalPrice - before.tradeTotalPrice
    val invariants = mapOf(
        "tradeQuantityMatchesResponses" to (after.tradeQuantity - before.tradeQuantity == totalPurchased),
        "buyerInventoryMatchesResponses" to (after.buyerQuantity - before.buyerQuantity == totalPurchased),
        "listingRemainingMatchesResponses" to (before.listingRemaining - after.listingRemaining == totalPurchased),
        "buyerWalletDebitMatchesTrades" to (before.buyerWalletBalance - after.buyerWalletBalance == actualTradeTotal),
        "sellerInventoryUnaffectedByPurchases" to (before.sellerQuantity == after.sellerQuantity),
        "negativeInventoryAbsent" to after.negativeInventoryAbsent,
        "negativeWalletAbsent" to after.negativeWalletAbsent,
    )

    val summary = BenchmarkSummary(
        runId = runId,
        startedAt = startedAt.toString(),
        finishedAt = Instant.now().toString(),
        config = config,
        listingIds = listingIds,
        wallMillis = wallMillis,
        sentRequests = config.totalRequests,
        completedRequests = results.size,
        successCount = successCount,
        failureCount = results.size - successCount,
        purchasedQuantity = totalPurchased,
        requestedQuantity = requestedQuantity,
        fillRate = fillRate,
        throughputPerSecond = if (wallMillis == 0L) 0.0 else results.size * 1000.0 / wallMillis,
        averageLatencyMillis = if (results.isEmpty()) 0.0 else sorted.average(),
        p50LatencyMillis = percentile(sorted, 0.50),
        p95LatencyMillis = percentile(sorted, 0.95),
        p99LatencyMillis = percentile(sorted, 0.99),
        statusCounts = results.groupingBy { it.statusCode.toString() }.eachCount().toSortedMap(),
        errorCounts = results.mapNotNull { it.errorCode }.groupingBy { it }.eachCount().toSortedMap(),
        failureDetails = results.mapNotNull { it.failure },
        dbWaitSamples = sampler.snapshot(),
        before = before,
        after = after,
        invariants = invariants,
    )

    val json = summary.toJson()
    if (config.outputPath.isNotBlank()) {
        Path.of(config.outputPath).parent?.let { Files.createDirectories(it) }
        Files.writeString(Path.of(config.outputPath), json)
        println("benchmark result written ${config.outputPath}")
    }
    println("market benchmark done runId=$runId success=$successCount failure=${summary.failureCount} p95=${summary.p95LatencyMillis}ms")
    return summary
}

data class Config(
    val apiBaseUrl: String,
    val dbUrl: String,
    val dbUser: String,
    val dbPassword: String,
    val runId: String,
    val itemId: String,
    val sellers: Int,
    val buyers: Int,
    val listingsPerSeller: Int,
    val listingQuantity: Long,
    val unitPrice: Long,
    val buyerRice: Long,
    val purchaseQuantity: Long,
    val concurrency: Int,
    val totalRequests: Int,
    val timeoutSeconds: Long,
    val password: String,
    val outputPath: String,
    val outputDir: String,
    val seriesConcurrency: List<Int>,
    val repetitions: Int,
    val resetItemData: Boolean,
    val preparedAccounts: Boolean,
    val preparedAccountCount: Int,
    val preparedAccountPrefix: String,
    val preparedLoginConcurrency: Int,
) {
    fun normalizedSingle(): Config = (if (runId.isBlank()) copy(runId = "market-baseline-${timestamp()}") else this).also { config ->
        require(config.concurrency <= config.buyers) { "--buyers must be at least --concurrency so one logical user does not issue overlapping purchases" }
        require(config.unitPrice in 10..999_990) { "--unit-price must be between 10 and 999990 rice" }
        require(config.purchaseQuantity in 1..999) { "--purchase-quantity must be between 1 and 999" }
        if (config.preparedAccounts) config.validatePreparedAccounts()
    }

    companion object {
        fun from(args: Array<String>, env: Map<String, String>): Config {
            val values = mutableMapOf<String, String>()
            var index = 0
            while (index < args.size) {
                val key = args[index]
                require(key.startsWith("--")) { "expected --key at ${args[index]}" }
                require(index + 1 < args.size) { "missing value for $key" }
                values[key.removePrefix("--")] = args[index + 1]
                index += 2
            }
            fun value(key: String, default: String): String = values[key] ?: env[key.uppercase().replace('-', '_')] ?: default
            val series = value("series-concurrency", "").split(',').mapNotNull { it.trim().takeIf(String::isNotBlank)?.toInt() }
            return Config(
                apiBaseUrl = value("api-base-url", "http://127.0.0.1:8080"),
                dbUrl = value("db-url", env["DB_URL"] ?: "jdbc:postgresql://localhost:5432/hanjjak"),
                dbUser = value("db-user", env["DB_USER"] ?: "hanjjak"),
                dbPassword = value("db-password", env["DB_PASSWORD"] ?: "local-only"),
                runId = value("run-id", ""),
                itemId = value("item-id", "POTATO_M1"),
                sellers = value("sellers", "1").toInt(),
                buyers = value("buyers", "20").toInt(),
                listingsPerSeller = value("listings-per-seller", "1").toInt(),
                listingQuantity = value("listing-quantity", "400").toLong(),
                unitPrice = value("unit-price", "10").toLong(),
                buyerRice = value("buyer-rice", "100000").toLong(),
                purchaseQuantity = value("purchase-quantity", "1").toLong(),
                concurrency = value("concurrency", "20").toInt(),
                totalRequests = value("total-requests", "200").toInt(),
                timeoutSeconds = value("timeout-seconds", "120").toLong(),
                password = value("password", "password123"),
                outputPath = value("output-path", ""),
                outputDir = value("output-dir", ""),
                seriesConcurrency = series,
                repetitions = value("repetitions", "5").toInt(),
                resetItemData = value("reset-item-data", series.isNotEmpty().toString()).toBooleanStrictOrNull() ?: false,
                preparedAccounts = value("prepared-accounts", "false").toBooleanStrictOrNull() ?: false,
                preparedAccountCount = value("prepared-account-count", "2000").toInt(),
                preparedAccountPrefix = value("prepared-account-prefix", "market-load").lowercase(),
                preparedLoginConcurrency = value("prepared-login-concurrency", "64").toInt(),
            )
        }
    }
}

class ApiSession(private val apiBaseUrl: String) {
    private val cookies = mutableMapOf<String, String>()
    lateinit var accountId: UUID
    internal fun executor() = SHARED_HTTP_CLIENT.executor().orElseThrow()

    fun signupOrLogin(email: String, password: String, nickname: String, materialType: String): UUID {
        val signup = postJson("/api/v1/auth/signup", "{\"email\":\"$email\",\"password\":\"$password\",\"nickname\":\"$nickname\"}")
        val body = if (signup.statusCode in 200..299) signup.body else {
            val login = postJson("/api/v1/auth/login", "{\"email\":\"$email\",\"password\":\"$password\"}")
            require(login.statusCode in 200..299) { "login failed status=${login.statusCode} body=${login.body}" }
            login.body
        }
        val accountId = UUID.fromString(extractString(body, "accountId") ?: error("accountId missing: $body"))
        val preference = postJson("/api/v1/material-preference", "{\"materialType\":\"$materialType\"}")
        require(preference.statusCode in 200..299 || extractString(preference.body, "code") == "MATERIAL_ALREADY_SELECTED") {
            "material preference failed status=${preference.statusCode} body=${preference.body}"
        }
        return accountId
    }

    fun login(email: String, password: String): UUID {
        val response = postJson("/api/v1/auth/login", "{\"email\":\"$email\",\"password\":\"$password\"}")
        require(response.statusCode in 200..299) { "login failed status=${response.statusCode} body=${response.body}" }
        return UUID.fromString(extractString(response.body, "accountId") ?: error("accountId missing: ${response.body}"))
    }
    internal fun request(path: String, body: String): HttpRequest = HttpRequest.newBuilder(URI.create(apiBaseUrl.trimEnd('/') + path))
        .timeout(Duration.ofSeconds(30))
        .header("Accept", "application/json")
        .header("Origin", apiBaseUrl.trimEnd('/'))
        .header("Content-Type", "application/json")
        .header("Idempotency-Key", UUID.randomUUID().toString())
        .apply { cookies.takeIf { it.isNotEmpty() }?.let { header("Cookie", it.entries.joinToString("; ") { (name, value) -> "$name=$value" }) } }
        .POST(HttpRequest.BodyPublishers.ofString(body))
        .build()

    fun postJson(path: String, body: String): HttpResult {
        val response = SHARED_HTTP_CLIENT.send(request(path, body), HttpResponse.BodyHandlers.ofString())
        response.headers().allValues("Set-Cookie").forEach { header ->
            val cookie = header.substringBefore(';').split('=', limit = 2)
            if (cookie.size == 2) cookies[cookie[0]] = cookie[1]
        }
        return HttpResult(response.statusCode(), response.body())
    }
}

private val SHARED_HTTP_EXECUTOR = Executors.newFixedThreadPool(256) { task ->
    Thread(task, "market-benchmark-http").apply { isDaemon = true }
}
private val SHARED_HTTP_CLIENT = HttpClient.newBuilder()
    .connectTimeout(Duration.ofSeconds(5))
    .executor(SHARED_HTTP_EXECUTOR)
    .build()

data class HttpResult(val statusCode: Int, val body: String)
data class RequestFailure(
    val requestIndex: Int,
    val buyerAccountId: UUID?,
    val latencyMillis: Long,
    val failedAt: Instant,
    val causeChain: String,
)
data class RequestResult(val statusCode: Int, val latencyMillis: Long, val purchasedQuantity: Long, val errorCode: String?, val failure: RequestFailure? = null)
internal fun failedRequestResult(
    error: Exception,
    latencyMillis: Long,
    requestIndex: Int = -1,
    buyerAccountId: UUID? = null,
    failedAt: Instant = Instant.now(),
): RequestResult {
    val causeChain = throwableChain(error)
    val errorCode = if (error is HttpTimeoutException) "HTTP_TIMEOUT" else causeChain
    return RequestResult(599.takeUnless { error is HttpTimeoutException } ?: 598, latencyMillis, 0L, errorCode, RequestFailure(requestIndex, buyerAccountId, latencyMillis, failedAt, causeChain))
}

internal fun throwableChain(error: Throwable): String {
    val seen = Collections.newSetFromMap(java.util.IdentityHashMap<Throwable, Boolean>())
    return generateSequence(error) { it.cause }
        .takeWhile(seen::add)
        .joinToString(" <- ") { cause ->
            val type = cause::class.qualifiedName ?: cause::class.simpleName ?: "Throwable"
            cause.message?.takeIf(String::isNotBlank)?.let { "$type:$it" } ?: type
        }
}

data class DbSnapshot(
    val listingRemaining: Long,
    val sellerQuantity: Long,
    val sellerReserved: Long,
    val buyerQuantity: Long,
    val buyerWalletBalance: Long,
    val tradeQuantity: Long,
    val tradeTotalPrice: Long,
    val tradeFee: Long,
    val settlementMailRice: Long,
    val negativeInventoryAbsent: Boolean,
    val negativeWalletAbsent: Boolean,
) {
    companion object {
        fun capture(connection: Connection, itemId: String, sellerIds: List<UUID>, buyerIds: List<UUID>, listingIds: List<String>): DbSnapshot = DbSnapshot(
            listingRemaining = sumLong(connection, "select coalesce(sum(remaining_quantity),0) from market_order where order_id in (${uuidList(listingIds)})"),
            sellerQuantity = inventorySum(connection, sellerIds, itemId, "quantity"),
            sellerReserved = inventorySum(connection, sellerIds, itemId, "reserved_quantity"),
            buyerQuantity = inventorySum(connection, buyerIds, itemId, "quantity"),
            buyerWalletBalance = sumLong(connection, "select coalesce(sum(balance),0) from wallet_balance where account_id in (${uuidList(buyerIds.map { it.toString() })})"),
            tradeQuantity = sumLong(connection, "select coalesce(sum(quantity),0) from market_trade where maker_order_id in (${uuidList(listingIds)}) or taker_order_id in (${uuidList(listingIds)})"),
            tradeTotalPrice = sumLong(connection, "select coalesce(sum(total_price),0) from market_trade where maker_order_id in (${uuidList(listingIds)}) or taker_order_id in (${uuidList(listingIds)})"),
            tradeFee = sumLong(connection, "select coalesce(sum(fee),0) from market_trade where maker_order_id in (${uuidList(listingIds)}) or taker_order_id in (${uuidList(listingIds)})"),
            settlementMailRice = sumLong(connection, "select coalesce(sum(m.rice_amount),0) from mail_message m join market_trade t on t.trade_id=m.source_trade_id where t.maker_order_id in (${uuidList(listingIds)}) or t.taker_order_id in (${uuidList(listingIds)})"),
            negativeInventoryAbsent = sumLong(connection, "select count(*) from inventory_stack where quantity < 0 or reserved_quantity < 0 or reserved_quantity > quantity") == 0L,
            negativeWalletAbsent = sumLong(connection, "select count(*) from wallet_balance where balance < 0") == 0L,
        )
    }
}

class LockSampler(private val config: Config) {
    private val running = AtomicBoolean(false)
    private val waits = Collections.synchronizedMap(mutableMapOf<String, Long>())
    @Volatile private var samples = 0L
    @Volatile private var maxWaitingSessions = 0L
    private val pool = Executors.newSingleThreadExecutor()

    fun start() {
        running.set(true)
        pool.execute {
            DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
                while (running.get()) {
                    val rows = connection.createStatement().use { statement ->
                        statement.executeQuery(
                            """
                            select coalesce(wait_event_type,'NONE') as wait_type, coalesce(wait_event,'NONE') as wait_event, count(*) as count
                            from pg_stat_activity
                            where datname = current_database() and wait_event_type is not null
                            group by wait_event_type, wait_event
                            """.trimIndent(),
                        ).use { rs -> readWaitRows(rs) }
                    }
                    samples++
                    val waiting = rows.sumOf { it.count }
                    maxWaitingSessions = max(maxWaitingSessions, waiting)
                    rows.forEach { row -> waits.merge("${row.waitType}:${row.waitEvent}", row.count, Long::plus) }
                    Thread.sleep(20)
                }
            }
        }
    }

    fun stop(): DbWaitSamples {
        running.set(false)
        pool.shutdown()
        pool.awaitTermination(2, TimeUnit.SECONDS)
        return snapshot()
    }

    fun snapshot(): DbWaitSamples = DbWaitSamples(samples, maxWaitingSessions, waits.toSortedMap())

    private fun readWaitRows(rs: ResultSet): List<WaitRow> {
        val rows = mutableListOf<WaitRow>()
        while (rs.next()) rows += WaitRow(rs.getString("wait_type"), rs.getString("wait_event"), rs.getLong("count"))
        return rows
    }
}

data class WaitRow(val waitType: String, val waitEvent: String, val count: Long)
data class DbWaitSamples(val samples: Long, val maxWaitingSessions: Long, val waitEventCounts: Map<String, Long>)

data class BenchmarkSummary(
    val runId: String,
    val startedAt: String,
    val finishedAt: String,
    val config: Config,
    val listingIds: List<String>,
    val wallMillis: Long,
    val sentRequests: Int,
    val completedRequests: Int,
    val successCount: Int,
    val failureCount: Int,
    val purchasedQuantity: Long,
    val requestedQuantity: Long,
    val fillRate: Double,
    val throughputPerSecond: Double,
    val averageLatencyMillis: Double,
    val p50LatencyMillis: Long,
    val p95LatencyMillis: Long,
    val p99LatencyMillis: Long,
    val statusCounts: Map<String, Int>,
    val errorCounts: Map<String, Int>,
    val failureDetails: List<RequestFailure>,
    val dbWaitSamples: DbWaitSamples,
    val before: DbSnapshot,
    val after: DbSnapshot,
    val invariants: Map<String, Boolean>,
) {
    fun toJson(): String = buildString {
        append("{\n")
        field("runId", runId); comma()
        field("startedAt", startedAt); comma()
        field("finishedAt", finishedAt); comma()
        append("  \"config\": "); configJson(config); comma()
        append("  \"listingIds\": "); stringArray(listingIds); comma()
        field("wallMillis", wallMillis); comma()
        field("sentRequests", sentRequests); comma()
        field("completedRequests", completedRequests); comma()
        field("successCount", successCount); comma()
        field("failureCount", failureCount); comma()
        field("purchasedQuantity", purchasedQuantity); comma()
        field("requestedQuantity", requestedQuantity); comma()
        field("fillRate", fillRate); comma()
        field("throughputPerSecond", throughputPerSecond); comma()
        field("averageLatencyMillis", averageLatencyMillis); comma()
        field("p50LatencyMillis", p50LatencyMillis); comma()
        field("p95LatencyMillis", p95LatencyMillis); comma()
        field("p99LatencyMillis", p99LatencyMillis); comma()
        append("  \"statusCounts\": "); intMap(statusCounts); comma()
        append("  \"errorCounts\": "); intMap(errorCounts); comma()
        append("  \"failureDetails\": "); failureArray(failureDetails); comma()
        append("  \"dbWaitSamples\": "); dbWaitJson(dbWaitSamples); comma()
        append("  \"before\": "); snapshotJson(before); comma()
        append("  \"after\": "); snapshotJson(after); comma()
        append("  \"invariants\": "); booleanMap(invariants); append('\n')
        append("}\n")
    }
}

data class SeriesRow(
    val concurrency: Int,
    val repetitions: Int,
    val throughputMedian: Double,
    val throughputMin: Double,
    val throughputMax: Double,
    val p50Median: Long,
    val p95Median: Long,
    val p99Median: Long,
    val averageLatencyMedian: Double,
    val maxWaitSessionMedian: Long,
    val tupleLockMedian: Long,
    val transactionLockMedian: Long,
    val successMedian: Long,
    val failureMedian: Long,
    val purchasedQuantityMedian: Long,
    val requestedQuantityMedian: Long,
    val fillRateMedian: Double,
)

fun buildSeriesRows(summaries: List<BenchmarkSummary>): List<SeriesRow> = summaries.groupBy { it.config.concurrency }.toSortedMap().map { (concurrency, rows) ->
    SeriesRow(
        concurrency = concurrency,
        repetitions = rows.size,
        throughputMedian = medianDouble(rows.map { it.throughputPerSecond }),
        throughputMin = rows.minOf { it.throughputPerSecond },
        throughputMax = rows.maxOf { it.throughputPerSecond },
        p50Median = medianLong(rows.map { it.p50LatencyMillis }),
        p95Median = medianLong(rows.map { it.p95LatencyMillis }),
        p99Median = medianLong(rows.map { it.p99LatencyMillis }),
        averageLatencyMedian = medianDouble(rows.map { it.averageLatencyMillis }),
        maxWaitSessionMedian = medianLong(rows.map { it.dbWaitSamples.maxWaitingSessions }),
        tupleLockMedian = medianLong(rows.map { it.dbWaitSamples.waitEventCounts["Lock:tuple"] ?: 0L }),
        transactionLockMedian = medianLong(rows.map { it.dbWaitSamples.waitEventCounts["Lock:transactionid"] ?: 0L }),
        successMedian = medianLong(rows.map { it.successCount.toLong() }),
        failureMedian = medianLong(rows.map { it.failureCount.toLong() }),
        purchasedQuantityMedian = medianLong(rows.map { it.purchasedQuantity }),
        requestedQuantityMedian = medianLong(rows.map { it.requestedQuantity }),
        fillRateMedian = medianDouble(rows.map { it.fillRate }),
    )
}

fun summaryCsv(summaries: List<BenchmarkSummary>): String = buildString {
    appendLine("concurrency,repetitions,success_median,failure_median,purchased_quantity_median,requested_quantity_median,fill_rate_median,throughput_median,throughput_min,throughput_max,average_latency_median,p50_median,p95_median,p99_median,max_wait_session_median,lock_tuple_median,lock_transactionid_median")
    buildSeriesRows(summaries).forEach { row ->
        appendLine(listOf(row.concurrency, row.repetitions, row.successMedian, row.failureMedian, row.purchasedQuantityMedian, row.requestedQuantityMedian, fmt(row.fillRateMedian), fmt(row.throughputMedian), fmt(row.throughputMin), fmt(row.throughputMax), fmt(row.averageLatencyMedian), row.p50Median, row.p95Median, row.p99Median, row.maxWaitSessionMedian, row.tupleLockMedian, row.transactionLockMedian).joinToString(","))
    }
}

fun chartDataJson(summaries: List<BenchmarkSummary>): String = buildString {
    append("{\n  \"series\": [\n")
    buildSeriesRows(summaries).forEachIndexed { index, row ->
        append("    {\"concurrency\":${row.concurrency},\"throughputMedian\":${fmt(row.throughputMedian)},\"p50Median\":${row.p50Median},\"p95Median\":${row.p95Median},\"p99Median\":${row.p99Median},\"purchasedQuantityMedian\":${row.purchasedQuantityMedian},\"requestedQuantityMedian\":${row.requestedQuantityMedian},\"fillRateMedian\":${fmt(row.fillRateMedian)},\"lockTupleMedian\":${row.tupleLockMedian},\"lockTransactionidMedian\":${row.transactionLockMedian},\"maxWaitSessionMedian\":${row.maxWaitSessionMedian}}")
        if (index != buildSeriesRows(summaries).lastIndex) append(',')
        append('\n')
    }
    append("  ]\n}\n")
}

fun summaryMarkdown(config: Config, summaries: List<BenchmarkSummary>): String = buildString {
    appendLine("# 거래소 단순 DB 동시성 시리즈")
    appendLine()
    appendLine("- 상태: 검토 필요")
    appendLine("- 기능: 거래소")
    appendLine("- 날짜: 2026-09-03")
    appendLine("- 실행자: SSAFY")
    appendLine()
    appendLine("## 조건")
    appendLine()
    appendLine("- API: `${config.apiBaseUrl}`")
    appendLine("- DB: `${config.dbUrl}`")
    appendLine("- item: `${config.itemId}`")
    appendLine("- 판매자: ${config.sellers}명")
    appendLine("- 구매자: ${config.buyers}명")
    appendLine("- 매물: 판매자당 ${config.listingsPerSeller}개, 매물당 ${config.listingQuantity}개")
    appendLine("- 요청 수: 각 실행 ${config.totalRequests}건")
    appendLine("- 반복: 각 동시성 ${config.repetitions}회")
    appendLine("- 격리: 각 실행 전 대상 item 거래소 데이터 reset")
    appendLine()
    appendLine("## 요약")
    appendLine()
    appendLine("| 동시성 | 처리량 중앙값(req/s) | p50(ms) | p95(ms) | p99(ms) | 구매 수량 중앙값 | 요청 수량 중앙값 | 충족률 중앙값 | Lock:tuple 중앙값 | Lock:transactionid 중앙값 | 최대 wait session 중앙값 | 실패 중앙값 |")
    appendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
    buildSeriesRows(summaries).forEach { row ->
        appendLine("| ${row.concurrency} | ${fmt(row.throughputMedian)} | ${row.p50Median} | ${row.p95Median} | ${row.p99Median} | ${row.purchasedQuantityMedian} | ${row.requestedQuantityMedian} | ${fmt(row.fillRateMedian)} | ${row.tupleLockMedian} | ${row.transactionLockMedian} | ${row.maxWaitSessionMedian} | ${row.failureMedian} |")
    }
    appendLine()
    appendLine("## 해석")
    appendLine()
    appendLine("- 모든 실행에서 정합성 불변 조건을 통과해야 이 파일이 생성된다.")
    appendLine("- p95/p99와 `Lock:tuple`, `Lock:transactionid`가 함께 증가하는 구간을 첫 DB 병목 개선 후보로 본다.")
    appendLine("- 이 실험은 Kafka 효과가 아니라 단순 DB 거래 커널의 기준선과 잠금 경쟁 위치를 찾는 목적이다.")
    appendLine()
    appendLine("## 증거")
    appendLine()
    appendLine("- 원본 JSON: `raw/*.json`")
    appendLine("- CSV: `summary.csv`")
    appendLine("- 그래프 데이터: `chart-data.json`")
}

fun seedData(connection: Connection, config: Config, sellerIds: List<UUID>, buyerIds: List<UUID>) {
    sellerIds.forEachIndexed { index, accountId ->
        upsertInventory(connection, accountId, config.itemId, config.listingsPerSeller.toLong() * config.listingQuantity, 0, index + 1L)
        upsertWallet(connection, accountId, 0)
    }
    buyerIds.forEachIndexed { index, accountId ->
        upsertWallet(connection, accountId, config.buyerRice)
        upsertInventory(connection, accountId, config.itemId, 0, 0, index + 1000L)
    }
}

fun resetItemData(connection: Connection, itemId: String) {
    val safeItem = itemId.replace("'", "''")
    val tradeSubquery = "select trade_id from market_trade where item_id='$safeItem'"
    connection.createStatement().use { statement ->
        statement.executeUpdate("delete from wallet_ledger where source_id in ($tradeSubquery)")
        statement.executeUpdate("delete from market_unread_event where source_id in (select trade_id from market_trade where item_id='$safeItem')")
        statement.executeUpdate("delete from market_delivery where instrument_id in (select instrument_id from market_instrument where item_id='$safeItem')")
        statement.executeUpdate("delete from mail_message where source_trade_id in ($tradeSubquery)")
        statement.executeUpdate("delete from market_trade where item_id='$safeItem'")
        statement.executeUpdate("delete from market_order_mutation_history where instrument_id in (select instrument_id from market_instrument where item_id='$safeItem')")
        statement.executeUpdate("delete from market_order where instrument_id in (select instrument_id from market_instrument where item_id='$safeItem')")
    }
}

fun upsertInventory(connection: Connection, accountId: UUID, itemId: String, quantity: Long, reserved: Long, sequence: Long) {
    connection.prepareStatement(
        """
        insert into inventory_stack(account_id,item_id,quantity,reserved_quantity,acquired_sequence)
        values (?::uuid,?,?,?,?)
        on conflict(account_id,item_id) do update set quantity=excluded.quantity,reserved_quantity=excluded.reserved_quantity,acquired_sequence=excluded.acquired_sequence
        """.trimIndent(),
    ).use { statement ->
        statement.setObject(1, accountId)
        statement.setString(2, itemId)
        statement.setLong(3, quantity)
        statement.setLong(4, reserved)
        statement.setLong(5, sequence)
        statement.executeUpdate()
    }
}

fun upsertWallet(connection: Connection, accountId: UUID, balance: Long) {
    connection.prepareStatement(
        """
        insert into wallet_balance(account_id,balance,updated_at)
        values (?::uuid,?,now())
        on conflict(account_id) do update set balance=excluded.balance,updated_at=now()
        """.trimIndent(),
    ).use { statement ->
        statement.setObject(1, accountId)
        statement.setLong(2, balance)
        statement.executeUpdate()
    }
}

fun StringBuilder.field(name: String, value: String) { append("  \"").append(name).append("\": \"").append(escape(value)).append('"') }
fun StringBuilder.field(name: String, value: Long) { append("  \"").append(name).append("\": ").append(value) }
fun StringBuilder.field(name: String, value: Int) { append("  \"").append(name).append("\": ").append(value) }
fun StringBuilder.field(name: String, value: Double) { append("  \"").append(name).append("\": ").append(fmt(value)) }
fun StringBuilder.comma() { append(",\n") }
fun StringBuilder.configJson(config: Config) {
    append("{\"apiBaseUrl\":\"").append(escape(config.apiBaseUrl)).append("\",\"dbUrl\":\"").append(escape(config.dbUrl)).append("\",\"itemId\":\"").append(config.itemId)
        .append("\",\"sellers\":").append(config.sellers).append(",\"buyers\":").append(config.buyers).append(",\"listingsPerSeller\":").append(config.listingsPerSeller)
        .append(",\"listingQuantity\":").append(config.listingQuantity).append(",\"unitPrice\":").append(config.unitPrice).append(",\"buyerRice\":").append(config.buyerRice)
        .append(",\"purchaseQuantity\":").append(config.purchaseQuantity).append(",\"concurrency\":").append(config.concurrency).append(",\"totalRequests\":").append(config.totalRequests)
        .append(",\"resetItemData\":").append(config.resetItemData).append(",\"preparedAccounts\":").append(config.preparedAccounts)
        .append(",\"preparedAccountCount\":").append(config.preparedAccountCount).append(",\"preparedLoginConcurrency\":").append(config.preparedLoginConcurrency).append('}')
}

fun StringBuilder.stringArray(values: List<String>) { append(values.joinToString(prefix = "[", postfix = "]") { "\"${escape(it)}\"" }) }
fun StringBuilder.intMap(values: Map<String, Int>) { append(values.entries.joinToString(prefix = "{", postfix = "}") { "\"${escape(it.key)}\":${it.value}" }) }
fun StringBuilder.longMap(values: Map<String, Long>) { append(values.entries.joinToString(prefix = "{", postfix = "}") { "\"${escape(it.key)}\":${it.value}" }) }
fun StringBuilder.failureArray(values: List<RequestFailure>) {
    append(values.joinToString(prefix = "[", postfix = "]") { failure ->
        "{\"requestIndex\":${failure.requestIndex},\"buyerAccountId\":${failure.buyerAccountId?.let { "\"$it\"" } ?: "null"},\"latencyMillis\":${failure.latencyMillis},\"failedAt\":\"${escape(failure.failedAt.toString())}\",\"causeChain\":\"${escape(failure.causeChain)}\"}"
    })
}
fun StringBuilder.booleanMap(values: Map<String, Boolean>) { append(values.entries.joinToString(prefix = "{", postfix = "}") { "\"${escape(it.key)}\":${it.value}" }) }
fun StringBuilder.dbWaitJson(value: DbWaitSamples) { append("{\"samples\":${value.samples},\"maxWaitingSessions\":${value.maxWaitingSessions},\"waitEventCounts\":"); longMap(value.waitEventCounts); append('}') }
fun StringBuilder.snapshotJson(value: DbSnapshot) {
    append("{\"listingRemaining\":${value.listingRemaining},\"sellerQuantity\":${value.sellerQuantity},\"sellerReserved\":${value.sellerReserved},\"buyerQuantity\":${value.buyerQuantity},\"buyerWalletBalance\":${value.buyerWalletBalance},\"tradeQuantity\":${value.tradeQuantity},\"tradeTotalPrice\":${value.tradeTotalPrice},\"tradeFee\":${value.tradeFee},\"settlementMailRice\":${value.settlementMailRice},\"negativeInventoryAbsent\":${value.negativeInventoryAbsent},\"negativeWalletAbsent\":${value.negativeWalletAbsent}}")
}

fun percentile(sorted: List<Long>, percentile: Double): Long {
    if (sorted.isEmpty()) return 0
    val index = ((sorted.size - 1) * percentile).toInt()
    return sorted[index]
}

fun medianLong(values: List<Long>): Long = values.sorted().let { sorted -> sorted[sorted.size / 2] }
fun medianDouble(values: List<Double>): Double = values.sorted().let { sorted -> sorted[sorted.size / 2] }
fun fmt(value: Double): String = "%.3f".format(Locale.US, value)
fun timestamp(): String = DateTimeFormatter.ofPattern("yyyyMMdd-HHmmss").withZone(ZoneOffset.UTC).format(Instant.now())
fun inventorySum(connection: Connection, accountIds: List<UUID>, itemId: String, column: String): Long =
    sumLong(connection, "select coalesce(sum($column),0) from inventory_stack where account_id in (${uuidList(accountIds.map { it.toString() })}) and item_id='${itemId.replace("'", "''")}'")
fun sumLong(connection: Connection, sql: String): Long = connection.createStatement().use { statement -> statement.executeQuery(sql).use { rs -> rs.next(); rs.getLong(1) } }
fun uuidList(values: List<String>): String = values.joinToString(",") { "'$it'::uuid" }
fun extractString(json: String, key: String): String? = Regex("\\\"$key\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"").find(json)?.groupValues?.get(1)
fun extractLong(json: String, key: String): Long? = Regex("\\\"$key\\\"\\s*:\\s*(\\d+)").find(json)?.groupValues?.get(1)?.toLong()
fun escape(value: String): String = value.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n")
fun String.toMaterialType(): String = when {
    startsWith("SWEET_POTATO") -> "SWEET_POTATO"
    startsWith("POTATO") -> "POTATO"
    startsWith("CORN") -> "CORN"
    else -> error("unsupported benchmark material: $this")
}
