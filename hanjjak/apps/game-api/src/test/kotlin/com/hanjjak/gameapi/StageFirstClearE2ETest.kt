package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.application.BattleStatsProvider
import com.hanjjak.skills.application.SkillProfileProvider
import com.hanjjak.sim.FighterStats
import jakarta.servlet.http.Cookie
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Primary
import org.springframework.http.MediaType
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.context.DynamicPropertyRegistry
import org.springframework.test.context.DynamicPropertySource
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.MvcResult
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get as getRequest
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post as postRequest
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.status
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap

@SpringBootTest(
    webEnvironment = SpringBootTest.WebEnvironment.MOCK,
    properties = ["hanjjak.api-security.legacy-battle-reward-api-enabled=true"],
)
@AutoConfigureMockMvc
class StageFirstClearE2ETest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var battleStats: MutableBattleStatsProvider
    @Autowired lateinit var skillProfiles: SkillProfileProvider

    @Test
    fun `stage completion returns first clear once across session and cycle paths`() {
        val player = Client()
        val accountId = player.signup("first-clear-${UUID.randomUUID()}@test.local", "first-clear")
        player.selectMaterial()
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        player.openGameSession()

        val first = player.post("/api/v1/battles/cycles", mapOf("stageId" to "stage.01-01"))
        assertTrue(first.at("/data/battle/success").asBoolean())
        assertEquals("CLAIMED", first.at("/data/firstClearReward/itemStatus").asText())
        assertEquals("stage.01-01", first.at("/data/firstClearReward/stageId").asText())
        assertTrue(first.at("/data/firstClearReward/riceGranted").asLong() > 0)
        assertEquals(3, first.at("/data/firstClearReward/grantedItems").size())
        assertEquals("감자 한 조각", first.at("/data/firstClearReward/grantedItems/0/displayName").asText())
        assertEquals(0, first.at("/data/firstClearReward/pendingItems").size())
        assertTrue(first.at("/data/firstClearReward/items").isMissingNode)

        val repeated = player.post("/api/v1/battles/cycles", mapOf("stageId" to "stage.01-01"))
        assertTrue(repeated.at("/data/battle/success").asBoolean())
        assertTrue(repeated.at("/data/firstClearReward").isNull)

        val started = player.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-02"))
        val sessionId = started.at("/data/battleSessionId").asText()
        jdbc.sql("update battle_session set completable_at=now()-interval '1 second' where id=:id")
            .param("id", UUID.fromString(sessionId)).update()
        val completeKey = UUID.randomUUID()
        val completed = player.post(
            "/api/v1/battles/sessions/$sessionId/complete",
            mapOf("predictedHash" to null, "renderingCheckpoint" to null),
            completeKey,
            started.at("/data/battleToken").asText(),
        )
        assertEquals("CLAIMED", completed.at("/data/firstClearReward/itemStatus").asText())
        assertEquals(3, completed.at("/data/firstClearReward/grantedItems").size())
        assertEquals(0, completed.at("/data/firstClearReward/pendingItems").size())
        val replay = player.post(
            "/api/v1/battles/sessions/$sessionId/complete",
            mapOf("predictedHash" to null, "renderingCheckpoint" to null),
            completeKey,
            started.at("/data/battleToken").asText(),
        )
        assertEquals(completed.at("/data"), replay.at("/data"))

        val beforeTickets = ticketBalance(accountId)
        val chapter = player.post("/api/v1/battles/chapters/1/auto-run", emptyMap<String, String>())
        assertEquals("CHAPTER_COMPLETE", chapter.at("/data/stoppedReason").asText())
        assertEquals(10L, jdbc.sql("select count(*) from stage_first_clear_reward where account_id=:account")
            .param("account", accountId).query(Long::class.java).single())
        val unlocked = player.get("/api/v1/stages").at("/data").first { it.at("/stageId").asText() == "stage.01-06" }
        assertTrue(unlocked.at("/unlocked").asBoolean())
        assertEquals(beforeTickets + 10, ticketBalance(accountId))
        assertEquals(
            2L,
            jdbc.sql("select count(*) from skill_state where account_id=:account and skill_id in ('passive_critical','passive_all_damage')")
                .param("account", accountId).query(Long::class.java).single(),
        )
        assertEquals(
            0L,
            jdbc.sql("select count(*) from skill_loadout where account_id=:account and skill_id like 'passive_%'")
                .param("account", accountId).query(Long::class.java).single(),
        )
        val profile = skillProfiles.profile(accountId)
        assertEquals(500, profile.criticalChanceBasisPoints)
        assertEquals(500, profile.allDamageBasisPoints)

        val secondChapter = player.post("/api/v1/battles/chapters/1/auto-run", emptyMap<String, String>())
        assertEquals("CHAPTER_COMPLETE", secondChapter.at("/data/stoppedReason").asText())
        assertEquals(beforeTickets + 10, ticketBalance(accountId))

        val pending = Client()
        val pendingAccountId = pending.signup("pending-${UUID.randomUUID()}@test.local", "pending")
        pending.selectMaterial()
        battleStats.set(pendingAccountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        pending.openGameSession()
        jdbc.sql("insert into inventory_instance select gen_random_uuid(),:account,'gem:1:flat_attack',false,n from generate_series(1,200) n")
            .param("account", pendingAccountId).update()
        val pendingClear = pending.post("/api/v1/battles/cycles", mapOf("stageId" to "stage.01-01"))
        assertEquals("PENDING", pendingClear.at("/data/firstClearReward/itemStatus").asText())
        assertEquals(0, pendingClear.at("/data/firstClearReward/grantedItems").size())
        assertTrue(pendingClear.at("/data/firstClearReward/pendingItems").size() > 0)
        assertEquals("감자 한 조각", pendingClear.at("/data/firstClearReward/pendingItems/0/displayName").asText())
        assertTrue(pendingClear.at("/data/firstClearReward/items").isMissingNode)
        val nextStage = pending.get("/api/v1/stages").at("/data").first { it.at("/stageId").asText() == "stage.01-02" }
        assertTrue(nextStage.at("/unlocked").asBoolean())
    }

    @Test
    fun `legacy account clears a stage without an undefined first-clear reward`() {
        val player = Client()
        val accountId = player.signup("legacy-clear-${UUID.randomUUID()}@test.local", "legacy-clear")
        player.selectMaterial()
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        jdbc.sql("update account_balance_state set balance_version='enemy-v1-applied' where account_id=:account")
            .param("account", accountId).update()
        player.openGameSession()

        val result = player.post("/api/v1/battles/cycles", mapOf("stageId" to "stage.01-01"))

        assertTrue(result.at("/data/battle/success").asBoolean())
        assertTrue(result.at("/data/firstClearReward").isNull)
        assertEquals(1L, jdbc.sql("select highest_clear_count from stage_progress where account_id=:account and stage_id='stage.01-01'")
            .param("account", accountId).query(Long::class.java).single())
    }

    private fun ticketBalance(accountId: UUID): Long = jdbc.sql("select cosmetic_ticket_balance from character where account_id=:account")
        .param("account", accountId).query(Long::class.java).single()

    private inner class Client {
        private val cookies = mutableListOf<Cookie>()
        private lateinit var accountId: UUID
        private lateinit var gameSessionId: String

        fun signup(email: String, nickname: String): UUID {
            val result = post("/api/v1/auth/signup", mapOf("email" to email, "password" to "password123", "nickname" to nickname), authenticated = false)
            accountId = UUID.fromString(result.at("/data/accountId").asText())
            return accountId
        }

        fun selectMaterial() {
            post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        }

        fun openGameSession() {
            gameSessionId = post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()
        }

        fun post(path: String, body: Any, key: UUID = UUID.randomUUID(), token: String? = null, authenticated: Boolean = true): JsonNode {
            val request = postRequest(path)
                .header("Origin", "http://localhost:5173")
                .header("Idempotency-Key", key)
                .contentType(MediaType.APPLICATION_JSON)
                .accept(MediaType.APPLICATION_JSON)
                .content(mapper.writeValueAsString(body))
            if (path.startsWith("/api/v1/battles/")) request.header("X-Game-Session-Id", gameSessionId)
            token?.let { request.header("X-Battle-Token", it) }
            if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
            if (authenticated) request.sessionAttr("accountId", accountId)
            val result = mvc.perform(request).andExpect(status().isOk).andReturn()
            capture(result)
            return mapper.readTree(result.response.contentAsString)
        }

        fun get(path: String): JsonNode {
            val request = getRequest(path).sessionAttr("accountId", accountId).accept(MediaType.APPLICATION_JSON)
            if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
            val result = mvc.perform(request).andExpect(status().isOk).andReturn()
            capture(result)
            return mapper.readTree(result.response.contentAsString)
        }

        private fun capture(result: MvcResult) {
            result.response.cookies.forEach { cookie ->
                cookies.removeIf { it.name == cookie.name }
                cookies += cookie
            }
        }
    }

    @TestConfiguration
    class BattleStatsTestConfiguration {
        @Bean
        @Primary
        fun mutableBattleStatsProvider(): MutableBattleStatsProvider = MutableBattleStatsProvider()
    }

    companion object {
        private val databaseConnection = PostgresTestDatabase.schema("stage_first_clear")

        @DynamicPropertySource
        @JvmStatic
        fun database(registry: DynamicPropertyRegistry) {
            databaseConnection.register(registry)
            registry.add("spring.session.store-type") { "none" }
            registry.add("hanjjak.balance.active-version") { "progression-rebalance-v1" }
        }
    }
}
