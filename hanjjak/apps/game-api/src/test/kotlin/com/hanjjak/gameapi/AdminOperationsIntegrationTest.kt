package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.admin.application.AdminGitlabProfile
import com.hanjjak.market.application.OrderBookService
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.assertThrows
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.http.MediaType
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.get
import org.springframework.test.web.servlet.post
import java.util.UUID

@GameApiIntegrationTest
class AdminOperationsIntegrationTest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var gitlab: StubAdminGitlabProvider
    @Autowired lateinit var orders: OrderBookService

    @Test
    fun `whitelisted GitLab login protects dashboard and records access audit`() {
        mvc.get("/api/admin/v1/dashboard")
            .andExpect { status { isUnauthorized() }; jsonPath("$.code") { value("ADMIN_AUTHENTICATION_REQUIRED") } }

        val cookie = gitlabLogin("allowed-admin")
        assertTrue(cookie.isHttpOnly)
        assertEquals("/api/admin", cookie.path)

        mvc.get("/api/admin/v1/dashboard") {
            cookie(cookie)
            header("X-Request-Id", "11111111-1111-4111-8111-111111111111")
        }.andExpect {
            status { isOk() }
            header { string("Cache-Control", "no-store") }
            jsonPath("$.data.service.status") { value("UP") }
            jsonPath("$.data.deployment.commitSha") { value("test-sha") }
            jsonPath("$.data.counts.totalAccounts") { isNumber() }
            jsonPath("$.data.counts.pendingOutboxEvents") { value(0) }
        }

        mvc.get("/api/admin/v1/audit?limit=20") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data[0].action") { value("READ_DASHBOARD") }
            jsonPath("$.data[0].outcome") { value("SUCCEEDED") }
        }

        assertThrows<org.springframework.dao.DataAccessException> {
            jdbc.sql("update admin_audit_event set outcome='FAILED' where request_id='11111111-1111-4111-8111-111111111111'").update()
        }
    }

    @Test
    fun `non-whitelisted GitLab user is denied without a session`() {
        val begin = mvc.post("/api/admin/v1/auth/gitlab/begin") { header("Origin", ADMIN_ORIGIN) }
            .andExpect { status { isOk() } }
            .andReturn()
        val authorizationUrl = mapper.readTree(begin.response.contentAsString).at("/data/authorizationUrl").asText()
        val state = queryParameter(authorizationUrl, "state")
        gitlab.profile = AdminGitlabProfile(404, "outside-user", "Outside User", "active", false)

        mvc.get("/api/admin/v1/auth/gitlab/callback?state=$state&code=provider-code")
            .andExpect {
                status { isFound() }
                header { string("Location", "$ADMIN_ORIGIN/admin/#admin-auth-error=ADMIN_GITLAB_ACCESS_DENIED") }
                cookie { doesNotExist("HANJJAK_ADMIN_SESSION") }
            }

        val persisted = jdbc.sql("select count(*) from admin_operator where gitlab_user_id = 404")
            .query(Long::class.java)
            .single()
        assertEquals(0, persisted)
    }

    @Test
    fun `GitLab OAuth state can be consumed only once`() {
        val begin = mvc.post("/api/admin/v1/auth/gitlab/begin") { header("Origin", ADMIN_ORIGIN) }
            .andExpect { status { isOk() } }
            .andReturn()
        val state = queryParameter(mapper.readTree(begin.response.contentAsString).at("/data/authorizationUrl").asText(), "state")
        gitlab.profile = AdminGitlabProfile(314, "allowed-admin", "Allowed Admin", "active", false)

        mvc.get("/api/admin/v1/auth/gitlab/callback?state=$state&code=provider-code").andExpect { status { isFound() } }
        mvc.get("/api/admin/v1/auth/gitlab/callback?state=$state&code=provider-code")
            .andExpect {
                status { isFound() }
                header { string("Location", "$ADMIN_ORIGIN/admin/#admin-auth-error=ADMIN_GITLAB_STATE_INVALID") }
            }
    }

    @Test
    fun `whitelisted GitLab identity links an existing operator username`() {
        jdbc.sql("update admin_operator set gitlab_user_id = null where gitlab_user_id = 314").update()
        val linkedOperatorId = jdbc.sql(
            """
            insert into admin_operator(operator_id, username, display_name)
            values (:id, 'allowed-link-admin', 'Legacy Admin')
            on conflict (username) do update
              set display_name = excluded.display_name,
                  gitlab_user_id = null
            returning operator_id
            """.trimIndent(),
        )
            .param("id", UUID.randomUUID())
            .query(UUID::class.java)
            .single()
        jdbc.sql("delete from admin_operator_role where operator_id = :id").param("id", linkedOperatorId).update()
        jdbc.sql("insert into admin_operator_role(operator_id, role) values (:id, 'VIEWER')").param("id", linkedOperatorId).update()

        val cookie = gitlabLogin("allowed-link-admin", gitlabUserId = 2_718)

        mvc.get("/api/admin/v1/auth/session") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data.operator.operatorId") { value(linkedOperatorId.toString()) }
            jsonPath("$.data.operator.roles[0]") { value("ADMIN") }
        }
        val operators = jdbc.sql("select count(*) from admin_operator where username = 'allowed-link-admin'")
            .query(Long::class.java)
            .single()
        assertEquals(1, operators)
    }

    @Test
    fun `admin can search and inspect an account without mutation`() {
        val player = mvc.post("/api/v1/auth/signup") {
            header("Origin", ADMIN_ORIGIN)
            header("Idempotency-Key", java.util.UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"inspect@test.local","password":"password123","nickname":"조사대상"}"""
        }.andExpect { status { isOk() } }.andReturn()
        val accountId = mapper.readTree(player.response.contentAsString).at("/data/accountId").asText()
        val cookie = gitlabLogin("allowed-admin")

        mvc.get("/api/admin/v1/accounts?query=inspect@test.local") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data[0].accountId") { value(accountId) }
            jsonPath("$.data[0].email") { value("inspect@test.local") }
            jsonPath("$.data[0].nickname") { value("조사대상") }
            jsonPath("$.data[0].rice") { value(0) }
        }
        mvc.get("/api/admin/v1/accounts") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data[0].accountId") { value(accountId) }
        }
        mvc.get("/api/admin/v1/accounts?query=사대") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data[0].accountId") { value(accountId) }
        }
        mvc.get("/api/admin/v1/catalog") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data.items[?(@.itemId == 'POTATO_M1')].displayName") { value(org.hamcrest.Matchers.contains("감자 한 조각")) }
        }
        mvc.get("/api/admin/v1/accounts/$accountId") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data.account.accountId") { value(accountId) }
            jsonPath("$.data.commands.length()") { value(1) }
            jsonPath("$.data.walletLedger.length()") { value(0) }
            jsonPath("$.data.battles.length()") { value(0) }
        }
        mvc.get("/api/admin/v1/outbox?status=PENDING&limit=10") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data.length()") { value(0) }
        }
    }

    @Test
    fun `GitLab login begin is origin protected`() {
        mvc.post("/api/admin/v1/auth/gitlab/begin") { header("Origin", "https://outside.example.com") }
            .andExpect {
                status { isForbidden() }
                jsonPath("$.code") { value("ADMIN_ORIGIN_FORBIDDEN") }
            }
    }

    @BeforeEach
    fun synchronizeMarketCatalog() {
        orders.synchronizeCatalog()
    }

    @Test
    fun `admin registers purchases and cancels order book liquidity with audit`() {
        val seller = mvc.post("/api/v1/auth/signup") {
            header("Origin", "http://localhost:5173")
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"market-admin-${UUID.randomUUID()}@test.local","password":"password123","nickname":"시장판매자"}"""
        }.andExpect { status { isOk() } }.andReturn()
        val sellerAccountId = UUID.fromString(mapper.readTree(seller.response.contentAsString).at("/data/accountId").asText())
        val instrumentId = jdbc.sql("select instrument_id from market_instrument where item_id='POTATO_M1' and status='ACTIVE'")
            .query(UUID::class.java).single()
        val playerOrderId = UUID.randomUUID()
        jdbc.sql(
            """
            insert into market_order(
              order_id,account_id,instrument_id,side,time_in_force,initial_quantity,filled_quantity,remaining_quantity,
              limit_unit_price,reserved_rice,status,priority_at,created_at,updated_at,expires_at,display_name_snapshot
            ) values (
              :order,:seller,:instrument,'SELL','GTC',50,0,50,100,0,'ACTIVE',now(),now(),now(),now()+interval '8 hours','감자 한 조각'
            )
            """.trimIndent(),
        ).params(mapOf("order" to playerOrderId, "seller" to sellerAccountId, "instrument" to instrumentId)).update()
        val cookie = gitlabLogin("allowed-admin")

        mvc.post("/api/admin/v1/market/system-orders") {
            cookie(cookie)
            header("Origin", ADMIN_ORIGIN)
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"itemId":"POTATO_M1","quantity":10000,"unitPrice":120,"reason":"시장 유동성 공급"}"""
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.totalQuantity") { value(10000) }
            jsonPath("$.data.orderIds.length()") { value(1) }
        }
        val systemQuantities = jdbc.sql("select initial_quantity from market_order where account_id='00000000-0000-4000-8000-000000000001' and side='SELL' order by created_at")
            .query(Long::class.java).list()
        assertEquals(listOf(10_000L), systemQuantities)

        mvc.post("/api/admin/v1/market/orders/$playerOrderId/purchase") {
            cookie(cookie)
            header("Origin", ADMIN_ORIGIN)
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"quantity":20,"reason":"매도 유동성 회수"}"""
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.quantity") { value(20) }
            jsonPath("$.data.settlementAmount") { value(1800) }
            jsonPath("$.data.remainingOrderQuantity") { value(30) }
        }
        assertEquals(1_800L, jdbc.sql("select balance from wallet_balance where account_id=:account").param("account", sellerAccountId).query(Long::class.java).single())

        mvc.post("/api/admin/v1/market/orders/$playerOrderId/cancel") {
            cookie(cookie)
            header("Origin", ADMIN_ORIGIN)
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"reason":"비정상 주문 제거"}"""
        }.andExpect {
            status { isOk() }
            jsonPath("$.data.cancelledQuantity") { value(30) }
            jsonPath("$.data.status") { value("CANCELLED") }
        }
        assertEquals(3L, jdbc.sql("select count(*) from admin_audit_event where action in ('CREATE_SYSTEM_MARKET_ORDER','PURCHASE_MARKET_ORDER','CANCEL_MARKET_ORDER') and outcome='SUCCEEDED'").query(Long::class.java).single())
    }

    @Test
    fun `admin manages user state and exposes attributed mutation audit`() {
        val player = mvc.post("/api/v1/auth/signup") {
            header("Origin", ADMIN_ORIGIN)
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = """{"email":"managed-${UUID.randomUUID()}@test.local","password":"password123","nickname":"관리대상"}"""
        }.andExpect { status { isOk() } }.andReturn()
        val accountId = mapper.readTree(player.response.contentAsString).at("/data/accountId").asText()
        val cookie = gitlabLogin("allowed-admin")

        fun mutate(path: String, body: String) = mvc.post("/api/admin/v1/accounts/$accountId/management/$path") {
            cookie(cookie)
            header("Origin", ADMIN_ORIGIN)
            header("Idempotency-Key", UUID.randomUUID())
            contentType = MediaType.APPLICATION_JSON
            content = body
        }.andExpect { status { isOk() } }

        mutate("progression", """{"level":10,"experience":50000,"reason":"테스트 성장 조정"}""")
        mutate("rice", """{"mode":"ADD","amount":5000,"reason":"테스트 쌀 지급"}""")
        mutate("items", """{"itemId":"POTATO_M1","mode":"SET","quantity":77,"reason":"테스트 재료 조정"}""")
        mutate("gems", """{"level":1,"option":"FLAT_ATTACK","delta":2,"reason":"테스트 보석 지급"}""")
        mutate("stages", """{"throughStageId":"stage.01-05","reason":"테스트 진행 복구"}""")
        mutate("cosmetics", """{"cosmeticId":"cosmetic-001","registeredQuantity":1,"unregisteredQuantity":2,"reason":"테스트 치장 복구"}""")
        mutate("equipment", """{"slot":"WEAPON","grade":"RARE","enhancementLevel":12,"reason":"테스트 장비 복구"}""")

        mvc.get("/api/admin/v1/accounts/$accountId/management") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data.level") { value(10) }
            jsonPath("$.data.experience") { value(50000) }
            jsonPath("$.data.rice") { value(5000) }
            jsonPath("$.data.highestUnlockedStageId") { value("stage.01-05") }
            jsonPath("$.data.items[?(@.itemId == 'POTATO_M1')].quantity") { value(77) }
            jsonPath("$.data.items[?(@.itemId == 'gem:1:flat_attack')].quantity") { value(2) }
            jsonPath("$.data.cosmetics[?(@.cosmeticId == 'cosmetic-001')].unregisteredQuantity") { value(2) }
            jsonPath("$.data.equipment[?(@.slot == 'WEAPON')].grade") { value("RARE") }
            jsonPath("$.data.equipment[?(@.slot == 'WEAPON')].enhancementLevel") { value(12) }
        }

        mvc.get("/api/admin/v1/audit?mutationsOnly=true&action=ADJUST_USER_RICE&limit=20") { cookie(cookie) }.andExpect {
            status { isOk() }
            jsonPath("$.data[0].username") { value("allowed-admin") }
            jsonPath("$.data[0].operatorId") { isNotEmpty() }
            jsonPath("$.data[0].targetId") { value(accountId) }
            jsonPath("$.data[0].reason") { value("테스트 쌀 지급") }
            jsonPath("$.data[0].beforeSummary") { isNotEmpty() }
            jsonPath("$.data[0].afterSummary") { isNotEmpty() }
            jsonPath("$.data[0].idempotencyKey") { isNotEmpty() }
        }
    }

    private fun gitlabLogin(username: String, gitlabUserId: Long = 314): jakarta.servlet.http.Cookie {
        val begin = mvc.post("/api/admin/v1/auth/gitlab/begin") { header("Origin", ADMIN_ORIGIN) }
            .andExpect { status { isOk() } }
            .andReturn()
        val authorizationUrl = mapper.readTree(begin.response.contentAsString).at("/data/authorizationUrl").asText()
        assertTrue(authorizationUrl.startsWith("https://gitlab.example.com/oauth/authorize?"))
        assertEquals("read_user", queryParameter(authorizationUrl, "scope"))
        assertEquals("S256", queryParameter(authorizationUrl, "code_challenge_method"))
        val state = queryParameter(authorizationUrl, "state")
        gitlab.profile = AdminGitlabProfile(gitlabUserId, username, "Allowed Admin", "active", false)
        val callback = mvc.get("/api/admin/v1/auth/gitlab/callback?state=$state&code=provider-code")
            .andExpect {
                status { isFound() }
                header { string("Location", "$ADMIN_ORIGIN/admin/") }
            }
            .andReturn()
        return requireNotNull(callback.response.getCookie("HANJJAK_ADMIN_SESSION"))
    }

    private fun queryParameter(url: String, name: String): String = java.net.URI(url).rawQuery.split('&')
        .map { it.split('=', limit = 2) }
        .first { it[0] == name }[1]
        .let { java.net.URLDecoder.decode(it, Charsets.UTF_8) }

    companion object {
        private const val ADMIN_ORIGIN = "http://localhost:5173"
    }
}
