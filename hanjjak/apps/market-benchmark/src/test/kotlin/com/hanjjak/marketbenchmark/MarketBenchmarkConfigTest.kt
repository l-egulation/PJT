package com.hanjjak.marketbenchmark

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class MarketBenchmarkConfigTest {
    @Test
    fun `defaults satisfy current marketplace contract`() {
        val config = Config.from(emptyArray(), emptyMap()).normalizedSingle()

        assertEquals(10, config.unitPrice)
        assertEquals(1, config.purchaseQuantity)
    }

    @Test
    fun `accepts one-rice unit price increments`() {
        val config = Config.from(arrayOf("--unit-price", "11"), emptyMap()).normalizedSingle()

        assertEquals(11, config.unitPrice)
    }

    @Test
    fun `parses reusable two-thousand account fixture`() {
        val config = Config.from(
            arrayOf(
                "--prepared-accounts", "true",
                "--prepared-account-count", "2000",
                "--prepared-account-prefix", "capacity-main",
                "--prepared-login-concurrency", "64",
                "--sellers", "100",
                "--buyers", "1000",
            ),
            emptyMap(),
        ).normalizedSingle()

        assertEquals(true, config.preparedAccounts)
        assertEquals(2_000, config.preparedAccountCount)
        assertEquals("capacity-main", config.preparedAccountPrefix)
        assertEquals("capacity-main-buyer-1000@benchmark.local", preparedAccountEmail(config.preparedAccountPrefix, PreparedAccountRole.BUYER, 1_000))
    }

    @Test
    fun `benchmark requests carry the API origin required by the mutation guard`() {
        val session = ApiSession("http://127.0.0.1:18092")
        val request = session.request("/api/v1/market/orders", "{\"quantity\":1}")

        assertEquals("http://127.0.0.1:18092", request.headers().firstValue("Origin").orElseThrow())
    }
    @Test
    fun `benchmark sessions share a bounded HTTP executor`() {
        val first = ApiSession("http://127.0.0.1:18092")
        val second = ApiSession("http://127.0.0.1:18092")

        assertEquals(first.executor(), second.executor())
    }


    @Test
    fun `rejects obsolete prices and oversized purchase quantities`() {
        assertFailsWith<IllegalArgumentException> {
            Config.from(arrayOf("--unit-price", "7"), emptyMap()).normalizedSingle()
        }
        assertFailsWith<IllegalArgumentException> {
            Config.from(arrayOf("--purchase-quantity", "1000"), emptyMap()).normalizedSingle()
        }
    }
    @Test
    fun `rejects overlapping requests from reused logical buyers`() {
        assertFailsWith<IllegalArgumentException> {
            Config.from(arrayOf("--buyers", "100", "--concurrency", "1000", "--total-requests", "1000"), emptyMap()).normalizedSingle()
        }
    }


    @Test
    fun `records timeout and complete connection failure diagnostics`() {
        val timeout = failedRequestResult(java.net.http.HttpTimeoutException("timed out"), 30_000)
        assertEquals("HTTP_TIMEOUT", timeout.errorCode)
        assertEquals("java.net.http.HttpTimeoutException:timed out", timeout.failure?.causeChain)

        val refused = java.net.ConnectException().apply { initCause(java.nio.channels.ClosedChannelException()) }
        val failedAt = java.time.Instant.parse("2026-09-10T00:00:00Z")
        val connection = failedRequestResult(refused, 5, requestIndex = 17, buyerAccountId = java.util.UUID.fromString("00000000-0000-0000-0000-000000000017"), failedAt = failedAt)

        assertEquals("java.net.ConnectException <- java.nio.channels.ClosedChannelException", connection.errorCode)
        assertEquals(17, connection.failure?.requestIndex)
        assertEquals(failedAt, connection.failure?.failedAt)
        assertEquals("java.net.ConnectException <- java.nio.channels.ClosedChannelException", connection.failure?.causeChain)
    }
}
