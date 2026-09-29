package com.hanjjak.gameapi

import com.hanjjak.account.application.AuthenticationCommandService
import com.hanjjak.battle.application.GameSessionService
import com.hanjjak.account.application.AuthenticationService
import com.hanjjak.account.application.MaterialPreferenceService
import com.hanjjak.account.domain.MaterialAlreadySelectedException
import com.hanjjak.account.domain.MaterialType
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Assertions.assertNotEquals
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.http.MediaType
import org.springframework.test.web.servlet.MockMvc
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.get
import org.springframework.test.web.servlet.post
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import com.hanjjak.sim.FighterStats
import java.security.MessageDigest

@GameApiIntegrationTest
class AccountFlowIntegrationTest {
    @Autowired private lateinit var authentication: AuthenticationService
    @Autowired private lateinit var authenticationCommands: AuthenticationCommandService
    @Autowired private lateinit var materials: MaterialPreferenceService
    @Autowired private lateinit var mockMvc: MockMvc
    @Autowired private lateinit var jdbc: JdbcClient
    @Autowired private lateinit var resetMail: RecordingPasswordResetMailSender
    @Autowired private lateinit var mapper: com.fasterxml.jackson.databind.ObjectMapper
    @Autowired private lateinit var battleStats: MutableBattleStatsProvider
    @Autowired private lateinit var offlineRewardSettings: com.hanjjak.battle.application.OfflineRewardSettings
    @Autowired private lateinit var gameSessionService: GameSessionService

    @Test
    fun `authentication validation always uses the common error envelope`() {
        mockMvc.post("/api/v1/auth/signup") {
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"player@example.com","password":"short","nickname":"한짝"}"""
        }.andExpect {
            status { isUnprocessableEntity() }
            jsonPath("$.code") { value("PASSWORD_TOO_SHORT") }
            jsonPath("$.messageKey") { value("error.password.too.short") }
            jsonPath("$.retryable") { value(false) }
            jsonPath("$.details[0].field") { value("password") }
        }

        mockMvc.post("/api/v1/auth/login") {
            header("Origin", "http://localhost:5173")
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"player@example.com","password":"password123"}"""
        }.andExpect {
            status { isBadRequest() }
            jsonPath("$.code") { value("IDEMPOTENCY_KEY_REQUIRED") }
        }

        mockMvc.post("/api/v1/auth/login") {
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", "not-a-uuid")
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"player@example.com","password":"password123"}"""
        }.andExpect {
            status { isBadRequest() }
            jsonPath("$.code") { value("INVALID_IDEMPOTENCY_KEY") }
        }
    }

    @Test
    fun `password reset stores a hash consumes once and invalidates login and game sessions`() {
        resetMail.clear()
        val email = "reset-${UUID.randomUUID()}@example.com"
        val signup = mockMvc.post("/api/v1/auth/signup") {
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"$email","password":"old-password","nickname":"한짝"}"""
        }.andExpect { status { isOk() } }.andReturn()
        val cookie = signup.response.cookies.single { it.name == "SESSION" }
        materials.select(UUID.fromString(jdbc.sql("select id from account where email=:email").param("email", email).query(String::class.java).single()), UUID.randomUUID(), MaterialType.POTATO)
        mockMvc.post("/api/v1/game-sessions") {
            cookie(cookie)
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = "{}"
        }.andExpect { status { isOk() } }
        mockMvc.post("/api/v1/auth/password-reset/request") {
            header("Origin", "http://localhost:5173")
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"$email"}"""
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.accepted") { value(true) }
        }
        val rawToken = resetMail.awaitLink().rawFragment.removePrefix("token=")
        val storedHash = jdbc.sql("select token_hash from password_reset_token where account_id=:account")
            .param("account", UUID.fromString(jdbc.sql("select id from account where email=:email").param("email", email).query(String::class.java).single()))
            .query(String::class.java)
            .single()
        assertNotEquals(rawToken, storedHash)
        assertEquals(sha256(rawToken), storedHash)

        mockMvc.post("/api/v1/auth/password-reset/confirm") {
            header("Origin", "http://localhost:5173")
            contentType = MediaType.APPLICATION_JSON
            content = """{"token":"$rawToken","password":"new-password"}"""
        }.andExpect { status { isOk() } }

        mockMvc.get("/api/v1/auth/session") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data.authenticated") { value(false) }
        }
        assertEquals(0, jdbc.sql("select count(*) from game_session where status='ACTIVE' and account_id=(select id from account where email=:email)")
            .param("email", email).query(Int::class.java).single())
        mockMvc.post("/api/v1/auth/password-reset/confirm") {
            header("Origin", "http://localhost:5173")
            contentType = MediaType.APPLICATION_JSON
            content = """{"token":"$rawToken","password":"other-password"}"""
        }.andExpect {
            status { isBadRequest() }
            jsonPath("$.code") { value("PASSWORD_RESET_TOKEN_INVALID") }
        }
        authentication.login(email, "new-password")
    }

    @Test
    fun `password reset request does not disclose whether an account exists`() {
        val knownEmail = "known-${UUID.randomUUID()}@example.com"
        authentication.signup(knownEmail, "password123", "한짝")

        listOf(knownEmail, "missing-${UUID.randomUUID()}@example.com").forEach { email ->
            mockMvc.post("/api/v1/auth/password-reset/request") {
                header("Origin", "http://localhost:5173")
                contentType = MediaType.APPLICATION_JSON
                content = """{"email":"$email"}"""
            }.andExpect {
                status { isOk() }
                jsonPath("$.data.accepted") { value(true) }
            }
        }
    }

    @Test
    fun `flyway schema persists account profile and immutable material preference`() {
        val email = "flow-${UUID.randomUUID()}@example.com"
        val signup = authentication.signup(email, "password123", " 첫한짝 ")

        val selected = materials.select(signup.account.id, UUID.randomUUID(), MaterialType.SWEET_POTATO)
        val renamed = authentication.updateNickname(signup.account.id, " 고구마대장 ")
        val restored = authentication.login(email.uppercase(), "password123")

        assertEquals(MaterialType.SWEET_POTATO, selected.data.primaryMaterialType)
        assertEquals(MaterialType.SWEET_POTATO, materials.get(signup.account.id).data.primaryMaterialType)
        assertEquals("고구마대장", renamed.character.nickname)
        assertEquals("고구마대장", restored.character.nickname)
        assertEquals(3, renamed.account.stateVersion)

        val error = runCatching {
            materials.select(signup.account.id, UUID.randomUUID(), MaterialType.CORN)
        }.exceptionOrNull()
        assertTrue(error is MaterialAlreadySelectedException)
    }

    @Test
    fun `rollout provisions old-pod accounts and new signup promotes its configured version`() {
        val rolloutAccount = UUID.randomUUID()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:account,:email,'test',1,now())")
            .params(mapOf("account" to rolloutAccount, "email" to "rollout-$rolloutAccount@example.com"))
            .update()
        assertEquals(
            "enemy-v1-applied",
            jdbc.sql("select balance_version from account_balance_state where account_id=:account")
                .param("account", rolloutAccount).query(String::class.java).single(),
        )
        assertTrue(
            jdbc.sql("select provisioned_during_rollout from account_balance_state where account_id=:account")
                .param("account", rolloutAccount).query(Boolean::class.java).single(),
        )

        val signup = authentication.signup("active-${UUID.randomUUID()}@example.com", "password123", "한짝")
        assertEquals(
            "progression-rebalance-v1",
            jdbc.sql("select balance_version from account_balance_state where account_id=:account")
                .param("account", signup.account.id).query(String::class.java).single(),
        )
        assertTrue(
            jdbc.sql("select retroactive_completed_at is not null and not provisioned_during_rollout from account_balance_state where account_id=:account")
                .param("account", signup.account.id).query(Boolean::class.java).single(),
        )
    }

    @Test
    fun `concurrent signup retries with one key create one account`() {
        val key = UUID.randomUUID()
        val email = "retry-${UUID.randomUUID()}@example.com"
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        try {
            val attempts = List(2) {
                pool.submit<UUID> {
                    start.await()
                    authenticationCommands.signup(key, email, "password123", "한짝").account.id
                }
            }
            start.countDown()
            assertEquals(1, attempts.map { it.get() }.toSet().size)
        } finally {
            pool.shutdownNow()
        }
    }

    @Test
    fun `concurrent different material selections persist exactly one value`() {
        val accountId = authentication.signup("material-${UUID.randomUUID()}@example.com", "password123", "한짝").account.id
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        try {
            val attempts = listOf(MaterialType.POTATO, MaterialType.CORN).map { material ->
                pool.submit<Boolean> {
                    start.await()
                    runCatching { materials.select(accountId, UUID.randomUUID(), material) }.isSuccess
                }
            }
            start.countDown()
            assertEquals(1, attempts.count { it.get() })
            assertTrue(materials.get(accountId).data.primaryMaterialType in setOf(MaterialType.POTATO, MaterialType.CORN))
            assertEquals(2, materials.get(accountId).stateVersion)
        } finally {
            pool.shutdownNow()
        }
    }

    @Test
    fun `silent game session freezes and claims offline reward exactly once`() {
        val email = "offline-${UUID.randomUUID()}@example.com"
        val signup = mockMvc.post("/api/v1/auth/signup") {
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"$email","password":"password123","nickname":"방치"}"""
        }.andExpect { status { isOk() } }.andReturn()
        val cookie = signup.response.cookies.single { it.name == "SESSION" }
        val accountId = UUID.fromString(jdbc.sql("select id from account where email=:email").param("email", email).query(String::class.java).single())
        materials.select(accountId, UUID.randomUUID(), MaterialType.POTATO)
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))

        val firstSession = mockMvc.post("/api/v1/game-sessions") {
            cookie(cookie)
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = "{}"
        }.andExpect { status { isOk() } }.andReturn()
        val firstSessionId = mapper.readTree(firstSession.response.contentAsString).at("/data/gameSessionId").asText()
        jdbc.sql("update offline_job set last_heartbeat_at=now()-interval '1 hour' where account_id=:account and status='ACTIVE'")
            .param("account", accountId).update()
        mockMvc.get("/api/v1/offline-rewards/pending") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data") { doesNotExist() }
        }
        val resumedSession = mockMvc.post("/api/v1/game-sessions") {
            cookie(cookie)
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = "{}"
        }.andExpect { status { isOk() } }.andReturn()
        assertEquals(firstSessionId, mapper.readTree(resumedSession.response.contentAsString).at("/data/gameSessionId").asText())
        jdbc.sql("update game_session set last_heartbeat_at=now()-interval '59 seconds' where account_id=:account and status='ACTIVE'")
            .param("account", accountId).update()
        jdbc.sql("update offline_job set last_heartbeat_at=now()-interval '59 seconds' where account_id=:account and status='ACTIVE'")
            .param("account", accountId).update()
        gameSessionService.requireActive(accountId, UUID.fromString(firstSessionId))
        assertEquals(0, jdbc.sql("select count(*) from offline_job where account_id=:account and last_heartbeat_at < now()-interval '5 seconds' and status='ACTIVE'")
            .param("account", accountId).query(Int::class.java).single())
        assertEquals(0, jdbc.sql("select count(*) from offline_job where account_id=:account and status='CLAIMABLE'")
            .param("account", accountId).query(Int::class.java).single())
        jdbc.sql("update game_session set last_heartbeat_at=now()-interval '1 hour' where account_id=:account and status='ACTIVE'")
            .param("account", accountId).update()
        jdbc.sql("update offline_job set last_heartbeat_at=now()-interval '1 hour' where account_id=:account and status='ACTIVE'")
            .param("account", accountId).update()
        val replacementSession = mockMvc.post("/api/v1/game-sessions") {
            cookie(cookie)
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = "{}"
        }.andExpect { status { isOk() } }.andReturn()
        assertNotEquals(firstSessionId, mapper.readTree(replacementSession.response.contentAsString).at("/data/gameSessionId").asText())

        mockMvc.get("/api/v1/offline-rewards/pending") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data.status") { value("CLAIMABLE") }
            jsonPath("$.data.stageId") { value("stage.01-01") }
            jsonPath("$.data.eligibleSeconds") { value(org.hamcrest.Matchers.greaterThan(0)) }
            jsonPath("$.data.offlineSeconds") { value(org.hamcrest.Matchers.greaterThanOrEqualTo(3_600)) }
            jsonPath("$.data.riceGained") { value(org.hamcrest.Matchers.greaterThan(0)) }
        }
        val key = UUID.randomUUID()
        val first = mockMvc.post("/api/v1/offline-rewards/claim") {
            cookie(cookie)
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", key)
            contentType = MediaType.APPLICATION_JSON
            content = "{}"
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.status") { value("CLAIMED") }
            jsonPath("$.data.offlineSeconds") { value(org.hamcrest.Matchers.greaterThanOrEqualTo(3_600)) }
        }.andReturn()
        val firstJobId = mapper.readTree(first.response.contentAsString).at("/data/jobId").asText()
        val replay = mockMvc.post("/api/v1/offline-rewards/claim") {
            cookie(cookie)
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", key)
            contentType = MediaType.APPLICATION_JSON
            content = "{}"
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.status") { value("CLAIMED") }
            jsonPath("$.data.offlineSeconds") { value(org.hamcrest.Matchers.greaterThanOrEqualTo(3_600)) }
        }.andReturn()
        assertEquals(firstJobId, mapper.readTree(replay.response.contentAsString).at("/data/jobId").asText())
        assertEquals(1, jdbc.sql("select count(*) from offline_job where account_id=:account and status='CLAIMED'")
            .param("account", accountId).query(Int::class.java).single())
    }

    @Test
    fun `logout reward uses whole minutes half rate and current stage`() {
        val email = "offline-test-${UUID.randomUUID()}@example.com"
        val signup = mockMvc.post("/api/v1/auth/signup") {
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"$email","password":"password123","nickname":"테스트"}"""
        }.andExpect { status { isOk() } }.andReturn()
        val cookie = signup.response.cookies.single { it.name == "SESSION" }
        val accountId = UUID.fromString(jdbc.sql("select id from account where email=:email").param("email", email).query(String::class.java).single())
        materials.select(accountId, UUID.randomUUID(), MaterialType.POTATO)
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        jdbc.sql("""insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version)
            values (:account,'stage.01-03',true,now(),1,'enemy-v1-applied'),
                   (:account,'stage.01-02',true,now(),1,'enemy-v1-applied')""").param("account", accountId).update()
        jdbc.sql("""insert into account_runtime_state(account_id,current_stage_id) values (:account,'stage.01-02')
            on conflict(account_id) do update set current_stage_id=excluded.current_stage_id""")
            .param("account", accountId).update()
        offlineRewardSettings.testAllAccounts = true
        offlineRewardSettings.testRewardMultiplier = "0.5"
        offlineRewardSettings.testGracePeriodSeconds = "0"
        offlineRewardSettings.testBucketSeconds = "60"
        offlineRewardSettings.testAccrualCapSeconds = "28800"
        try {
            mockMvc.post("/api/v1/game-sessions") {
                cookie(cookie)
                header("Origin", "http://localhost:5173")
                header("Idempotency-Key", UUID.randomUUID())
                contentType = MediaType.APPLICATION_JSON
                content = "{}"
            }.andExpect { status { isOk() } }
            val oneMinuteRate = mapper.readTree(jdbc.sql("select reward_rate_snapshot_json::text from offline_job where account_id=:account and status='ACTIVE'")
                .param("account", accountId).query(String::class.java).single())

            mockMvc.post("/api/v1/auth/logout") {
                cookie(cookie)
                header("Origin", "http://localhost:5173")
                header("Idempotency-Key", UUID.randomUUID())
                contentType = MediaType.APPLICATION_JSON
                content = "{}"
            }.andExpect { status { isOk() } }
            jdbc.sql("update offline_job set last_heartbeat_at=now()-interval '119 seconds' where account_id=:account and status='ACTIVE'")
                .param("account", accountId).update()
            gameSessionService.expireSilentSessions()
            assertEquals(1, jdbc.sql("select count(*) from offline_job where account_id=:account and status='ACTIVE'")
                .param("account", accountId).query(Int::class.java).single())

            val login = mockMvc.post("/api/v1/auth/login") {
                header("Origin", "http://localhost:5173")
                header("Idempotency-Key", UUID.randomUUID())
                contentType = MediaType.APPLICATION_JSON
                content = """{"email":"$email","password":"password123"}"""
            }.andExpect { status { isOk() } }.andReturn()
            val loginCookie = login.response.cookies.single { it.name == "SESSION" }

            mockMvc.get("/api/v1/offline-rewards/pending") { cookie(loginCookie) }.andExpect {
                status { isOk() }
                jsonPath("$.data.status") { value("CLAIMABLE") }
                jsonPath("$.data.stageId") { value("stage.01-02") }
                jsonPath("$.data.offlineSeconds") { value(org.hamcrest.Matchers.greaterThanOrEqualTo(119)) }
                jsonPath("$.data.eligibleSeconds") { value(60) }
            }
            mockMvc.post("/api/v1/offline-rewards/claim") {
                cookie(loginCookie)
                header("Origin", "http://localhost:5173")
                header("Idempotency-Key", UUID.randomUUID())
                contentType = MediaType.APPLICATION_JSON
                content = "{}"
            }.andExpect {
                status { isOk() }
                jsonPath("$.data.eligibleSeconds") { value(60) }
                jsonPath("$.data.experienceGained") { value(oneMinuteRate["experiencePerMinute"].decimalValue().multiply(java.math.BigDecimal("0.5")).setScale(0, java.math.RoundingMode.DOWN).longValueExact()) }
                jsonPath("$.data.riceGained") { value(oneMinuteRate["ricePerMinute"].decimalValue().multiply(java.math.BigDecimal("0.5")).setScale(0, java.math.RoundingMode.DOWN).longValueExact()) }
            }

            mockMvc.post("/api/v1/auth/logout") {
                cookie(loginCookie)
                header("Origin", "http://localhost:5173")
                header("Idempotency-Key", UUID.randomUUID())
                contentType = MediaType.APPLICATION_JSON
                content = "{}"
            }.andExpect { status { isOk() } }
            jdbc.sql("update offline_job set last_heartbeat_at=now()-interval '59 seconds' where account_id=:account and status='ACTIVE'")
                .param("account", accountId).update()
            val shortLogin = mockMvc.post("/api/v1/auth/login") {
                header("Origin", "http://localhost:5173")
                header("Idempotency-Key", UUID.randomUUID())
                contentType = MediaType.APPLICATION_JSON
                content = """{"email":"$email","password":"password123"}"""
            }.andExpect { status { isOk() } }.andReturn()
            val shortLoginCookie = shortLogin.response.cookies.single { it.name == "SESSION" }
            mockMvc.get("/api/v1/offline-rewards/pending") { cookie(shortLoginCookie) }.andExpect {
                status { isOk() }
                jsonPath("$.data") { doesNotExist() }
            }
        } finally {
            offlineRewardSettings.testAllAccounts = false
            offlineRewardSettings.testRewardMultiplier = ""
            offlineRewardSettings.testGracePeriodSeconds = ""
            offlineRewardSettings.testBucketSeconds = ""
            offlineRewardSettings.testAccrualCapSeconds = ""
        }
    }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray())
        .joinToString("") { "%02x".format(it) }

}
