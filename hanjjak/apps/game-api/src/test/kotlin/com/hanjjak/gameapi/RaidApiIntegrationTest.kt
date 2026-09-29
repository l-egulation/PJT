package com.hanjjak.gameapi

import org.junit.jupiter.api.Disabled
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.get
import org.springframework.test.web.servlet.post
import org.springframework.http.MediaType
import java.time.Instant
import java.sql.Timestamp
import java.util.UUID

@GameApiIntegrationTest
class RaidApiIntegrationTest {
    @Autowired
    lateinit var mvc: MockMvc

    @Test
    fun `anonymous raid state is rejected with authentication error envelope`() {
        mvc.get("/api/v1/raid") {
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isUnauthorized() }
            jsonPath("$.code") { value("AUTHENTICATION_REQUIRED") }
            jsonPath("$.requestId") { exists() }
            jsonPath("$.serverTime") { exists() }
            jsonPath("$.retryable") { value(false) }
            /* 봉투는 이제 코드마다 details 한 줄을 함께 싣는다. */
            jsonPath("$.details[0].code") { value("AUTHENTICATION_REQUIRED") }
        }
    }

    @Test
    fun `raid mutation requires canonical idempotency uuid`() {
        mvc.post("/api/v1/raid/attempts") {
            accept = MediaType.APPLICATION_JSON
            contentType = MediaType.APPLICATION_JSON
            header("Origin", "http://localhost:5173")
            content = "{\"mode\":\"REWARD\"}"
        }.andExpect {
            /* 열쇠가 아예 없으면 인증을 보기 전에 잘못된 요청으로 돌려보낸다.
               익명 거절 자체는 위의 상태 조회 시험이 맡는다. */
            status { isBadRequest() }
            jsonPath("$.code") { value("IDEMPOTENCY_KEY_REQUIRED") }
        }

        mvc.post("/api/v1/raid/attempts") {
            accept = MediaType.APPLICATION_JSON
            contentType = MediaType.APPLICATION_JSON
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", "not-a-uuid")
            content = "{\"mode\":\"REWARD\"}"
        }.andExpect {
            status { isBadRequest() }
            jsonPath("$.code") { value("INVALID_IDEMPOTENCY_KEY") }
        }
    }

    @Test
    fun `raid routes expose nullable current attempt and command envelope shape`() {
        val key = UUID.randomUUID()
        mvc.get("/api/v1/raid/attempts/current") {
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isUnauthorized() }
        }
        mvc.post("/api/v1/raid/attempts") {
            accept = MediaType.APPLICATION_JSON
            contentType = MediaType.APPLICATION_JSON
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", key)
            content = "{\"mode\":\"REWARD\"}"
        }.andExpect {
            status { isUnauthorized() }
        }
    }

    @Disabled("Compile-only coverage; execution requires the Docker-backed PostgreSQL fixture")
    @Test
    fun `authenticated locked state exposes stale error envelope and nullable details`() {
        mvc.get("/api/v1/raid") {
            sessionAttr("accountId", UUID.randomUUID())
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isConflict() }
            jsonPath("$.code") { value("RAID_CONTENT_LOCKED") }
            jsonPath("$.requestId") { exists() }
            jsonPath("$.serverTime") { exists() }
            jsonPath("$.retryable") { value(false) }
            jsonPath("$.details") { exists() }
        }
    }

    @Disabled("Compile-only coverage; execution requires the Docker-backed PostgreSQL fixture")
    @Test
    fun `unlocked state serializes required and nullable raid shapes with state version`() {
        val accountId = UUID.randomUUID()
        mvc.get("/api/v1/raid") {
            sessionAttr("accountId", accountId)
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isOk() }
            jsonPath("$.requestId") { exists() }
            jsonPath("$.serverTime") { exists() }
            jsonPath("$.stateVersion") { exists() }
            jsonPath("$.data.session.sessionId") { exists() }
            jsonPath("$.data.session.sealedContribution") { exists() }
            jsonPath("$.data.slots") { isArray() }
            jsonPath("$.data.currentAttempt") { exists() }
            jsonPath("$.data.ranking.topEntries") { isArray() }
            jsonPath("$.data.ranking.nextCursor") { exists() }
            jsonPath("$.data.claims.items") { isArray() }
            jsonPath("$.data.claims.nextCursor") { exists() }
        }
    }

    @Disabled("Compile-only coverage; execution requires the Docker-backed PostgreSQL fixture")
    @Test
    fun `ranking ties and cursor pagination preserve rank and nullable cursor`() {
        val accountId = UUID.randomUUID()
        mvc.get("/api/v1/raid/ranking?limit=100") {
            sessionAttr("accountId", accountId)
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.sessionId") { exists() }
            jsonPath("$.data.settled") { exists() }
            jsonPath("$.data.currentRank") { exists() }
            jsonPath("$.data.currentSealContribution") { exists() }
            jsonPath("$.data.topEntries") { isArray() }
            jsonPath("$.data.topEntries[0].rank") { exists() }
            jsonPath("$.data.nextCursor") { exists() }
            jsonPath("$.data.totalEligibleAccounts") { exists() }
        }
        mvc.get("/api/v1/raid/ranking?cursor=malformed&limit=100") {
            sessionAttr("accountId", accountId)
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isConflict() }
            jsonPath("$.code") { value("RAID_SESSION_STALE") }
        }
    }

    @Disabled("Compile-only coverage; execution requires the Docker-backed PostgreSQL fixture")
    @Test
    fun `claims cursor page exposes counts and nullable next cursor`() {
        mvc.get("/api/v1/raid/claims") {
            sessionAttr("accountId", UUID.randomUUID())
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.items") { isArray() }
            jsonPath("$.data.claimableCount") { exists() }
            jsonPath("$.data.claimedCount") { exists() }
            jsonPath("$.data.nextCursor") { exists() }
        }
    }

    @Disabled("Compile-only coverage; execution requires the Docker-backed PostgreSQL fixture")
    @Test
    fun `attempt commands cover start retry confirm discard and practice envelopes`() {
        val accountId = UUID.randomUUID()
        val attemptId = UUID.randomUUID()
        fun command(path: String, body: String = "{}") = mvc.post(path) {
            sessionAttr("accountId", accountId)
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            header("Origin", "http://localhost:5173")
            accept = MediaType.APPLICATION_JSON
            content = body
        }

        command("/api/v1/raid/attempts", "{\"mode\":\"REWARD\"}").andExpect {
            status { isOk() }
            jsonPath("$.data.commandId") { exists() }
            jsonPath("$.data.idempotencyKey") { exists() }
            jsonPath("$.data.status") { value("SUCCEEDED") }
            jsonPath("$.data.result.attemptId") { exists() }
        }
        command("/api/v1/raid/attempts/$attemptId/retry").andExpect { status { isOk() } }
        command("/api/v1/raid/attempts/$attemptId/confirm").andExpect { status { isOk() } }
        command("/api/v1/raid/attempts/$attemptId/discard").andExpect { status { isOk() } }
        command("/api/v1/raid/attempts", "{\"mode\":\"PRACTICE\"}").andExpect { status { isOk() } }
    }

    @Disabled("Compile-only coverage; execution requires the Docker-backed PostgreSQL fixture")
    @Test
    fun `attempt timeline serialization preserves event fields and nullable result`() {
        mvc.get("/api/v1/raid/attempts/current") {
            sessionAttr("accountId", UUID.randomUUID())
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.attemptId") { exists() }
            jsonPath("$.data.inputSnapshot.contentVersion") { exists() }
            jsonPath("$.data.renderingTimeline.tickDurationMilliseconds") { exists() }
            jsonPath("$.data.renderingTimeline.durationMilliseconds") { exists() }
            jsonPath("$.data.renderingTimeline.events") { isArray() }
            jsonPath("$.data.currentResult.damage") { exists() }
            jsonPath("$.data.terminalResult") { exists() }
        }
    }

    @Disabled("Compile-only coverage; execution requires the Docker-backed PostgreSQL fixture")
    @Test
    fun `settled and stale sessions expose settled state without restarting attempts`() {
        val accountId = UUID.randomUUID()
        mvc.get("/api/v1/raid") {
            sessionAttr("accountId", accountId)
            accept = MediaType.APPLICATION_JSON
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.session.status") { exists() }
            jsonPath("$.data.session.sealSuccessScheduled") { exists() }
            jsonPath("$.data.currentAttempt") { exists() }
        }
        mvc.post("/api/v1/raid/attempts") {
            sessionAttr("accountId", accountId)
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            header("Origin", "http://localhost:5173")
            accept = MediaType.APPLICATION_JSON
            content = "{\"mode\":\"REWARD\"}"
        }.andExpect {
            status { isConflict() }
            jsonPath("$.code") { value("RAID_SESSION_STALE") }
        }
    }
}
