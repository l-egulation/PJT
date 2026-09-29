package com.hanjjak.gameapi

import org.junit.jupiter.api.Disabled
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.post
import org.springframework.http.MediaType
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import kotlin.test.assertTrue

/** PostgreSQL load probes stay disabled solely because this environment has no Docker daemon. */
@Disabled("Docker-backed PostgreSQL fixture unavailable; compile-only database load scenario")
@GameApiIntegrationTest
class RaidLongRunDatabaseLoadTest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var jdbc: JdbcClient

    @Test
    fun `burst confirms before settlement cutoff remain idempotent`() {
        val accountId = UUID.randomUUID()
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(16)
        try {
            val calls = (1..64).map {
                pool.submit {
                    start.await()
                    mvc.post("/api/v1/raid/attempts/${UUID.randomUUID()}/confirm") {
                        sessionAttr("accountId", accountId)
                        header("Idempotency-Key", UUID.randomUUID())
                        contentType = MediaType.APPLICATION_JSON
                        accept = MediaType.APPLICATION_JSON
                        content = "{}"
                    }.andReturn()
                }
            }
            start.countDown()
            calls.forEach { it.get(30, TimeUnit.SECONDS) }
        } finally {
            pool.shutdownNow()
        }
        assertTrue(jdbc.sql("select 1").query(Int::class.java).single() == 1)
    }
}
