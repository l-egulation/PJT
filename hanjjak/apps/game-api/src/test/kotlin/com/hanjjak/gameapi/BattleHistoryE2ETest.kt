package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.application.BattleHistoryService
import com.hanjjak.sim.FighterStats
import jakarta.servlet.http.Cookie
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.http.MediaType
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.MvcResult
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get as getRequest
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.status
import java.time.Instant
import java.util.UUID

@GameApiIntegrationTest
class BattleHistoryE2ETest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var battleStats: MutableBattleStatsProvider
    @Autowired lateinit var history: BattleHistoryService

    @Test
    fun `authenticated history is durable isolated and idempotent`() {
        val player = Client()
        val email = "history-player-${UUID.randomUUID()}@test.local"
        val accountId = player.signup(email, "history-player")
        player.selectMaterial()
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        val gameSessionId = player.openGameSession()
        val started = player.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-01"), gameSessionId)
        val battleSessionId = started.at("/data/battleSessionId").asText()
        val token = started.at("/data/battleToken").asText()
        jdbc.sql("update battle_session set completable_at=now()-interval '1 second' where id=:id")
            .param("id", UUID.fromString(battleSessionId)).update()
        val completeKey = UUID.randomUUID()

        player.post(
            "/api/v1/battles/sessions/$battleSessionId/complete",
            mapOf("predictedHash" to null, "renderingCheckpoint" to null),
            gameSessionId,
            token,
            completeKey,
        )
        player.post(
            "/api/v1/battles/sessions/$battleSessionId/complete",
            mapOf("predictedHash" to null, "renderingCheckpoint" to null),
            gameSessionId,
            token,
            completeKey,
        )

        val restored = player.get("/api/v1/battle-history")
        assertEquals(listOf("STAGE_CLEARED", "STAGE_ENTERED"), restored.at("/data").map { it.at("/type").asText() })
        val clearSnapshot = restored.at("/data/0/combatSnapshot")
        assertEquals(20, clearSnapshot.at("/normalCount").asInt())
        assertTrue(clearSnapshot.at("/elapsedTicks").asInt() > 0)
        assertTrue(clearSnapshot.at("/remainingHp").asInt() in 1..clearSnapshot.at("/maxHp").asInt())
        assertEquals(restored.at("/data/1/occurredAt").asText(), clearSnapshot.at("/stageEnteredAt").asText())
        assertEquals(0, clearSnapshot.at("/lastEnemyRemainingHp").asInt())
        assertTrue(clearSnapshot.at("/riceGained").asLong() > 0)
        assertTrue(clearSnapshot.at("/experienceGained").asLong() > 0)
        assertTrue(clearSnapshot.at("/rewards").isArray)
        clearSnapshot.at("/rewards").forEach { reward ->
            assertTrue(reward.at("/displayName").asText().isNotBlank())
            assertTrue(reward.at("/quantity").asLong() > 0)
        }
        assertEquals(2L, historyCount(accountId))
        assertEquals("no-store", player.lastResponse!!.getHeader("Cache-Control"))
        player.logout()
        player.login(email)
        assertEquals(
            listOf("STAGE_CLEARED", "STAGE_ENTERED"),
            player.get("/api/v1/battle-history").at("/data").map { it.at("/type").asText() },
        )

        val otherPlayer = Client()
        otherPlayer.signup("history-other-${UUID.randomUUID()}@test.local", "history-other")
        otherPlayer.selectMaterial()
        assertTrue(otherPlayer.get("/api/v1/battle-history").at("/data").isEmpty)
    }

    @Test
    fun `history keeps latest fifty and returns one stage failure snapshot`() {
        val player = Client()
        val accountId = player.signup("history-limit-${UUID.randomUUID()}@test.local", "history-limit")
        player.selectMaterial()
        repeat(51) { index ->
            val sourceId = UUID.randomUUID()
            val occurredAt = Instant.parse("2026-09-09T00:00:00Z").plusSeconds(index.toLong())
            history.recordStageEntered(accountId, sourceId, "stage.01-01", occurredAt)
            if (index == 50) {
                history.recordStageResult(
                    accountId,
                    sourceId,
                    "stage.01-01",
                    "enemy-v1-applied",
                    com.hanjjak.sim.CycleInput(
                        "enemy-v1-applied",
                        index.toLong(),
                        FighterStats(111, 222, 333),
                        com.hanjjak.sim.EnemyStats(10, 10, 10),
                        com.hanjjak.sim.EnemyStats(10, 10, 10),
                    ),
                    com.hanjjak.sim.CycleResult(false, "PLAYER_DEFEATED", 0, 7, 90),
                    com.hanjjak.inventory.domain.RewardResult(
                        listOf(com.hanjjak.inventory.domain.RewardLine("POTATO_M1", 3, 3, 0)),
                        1,
                        200,
                        false,
                    ),
                    com.hanjjak.progression.domain.ProgressionRewardResult(7, 11, 1, 1, 0, 7, 993, 11),
                    occurredAt.plusMillis(1),
                )
            }
        }

        val recent = player.get("/api/v1/battle-history").at("/data")
        assertEquals(50, recent.size())
        assertEquals("STAGE_FAILED", recent[0].at("/type").asText())
        assertEquals("PLAYER_DEFEATED", recent[0].at("/resultCode").asText())
        assertEquals(111, recent[0].at("/combatSnapshot/attack").asInt())
        assertEquals(222, recent[0].at("/combatSnapshot/maxHp").asInt())
        assertEquals(333, recent[0].at("/combatSnapshot/penetration").asInt())
        assertEquals(0, recent[0].at("/combatSnapshot/remainingHp").asInt())
        assertEquals(7, recent[0].at("/combatSnapshot/defeatedNormals").asInt())
        assertEquals("2026-09-09T00:00:50Z", recent[0].at("/combatSnapshot/stageEnteredAt").asText())
        assertTrue(recent[0].at("/combatSnapshot/lastEnemyRemainingHp").isInt)
        assertEquals(90, recent[0].at("/combatSnapshot/elapsedTicks").asInt())
        assertEquals(11, recent[0].at("/combatSnapshot/riceGained").asLong())
        assertEquals("감자 한 조각", recent[0].at("/combatSnapshot/rewards/0/displayName").asText())
        assertEquals(3, recent[0].at("/combatSnapshot/rewards/0/quantity").asLong())
        assertEquals(50L, historyCount(accountId))

        val failure = player.get("/api/v1/battle-history/stages/stage.01-01/latest-failure")
            .at("/data/latestFailure")
        assertEquals("PLAYER_DEFEATED", failure.at("/resultCode").asText())
        assertEquals("enemy-v1-applied", failure.at("/contentVersion").asText())

        val empty = player.get("/api/v1/battle-history/stages/stage.01-02/latest-failure")
        assertTrue(empty.at("/data/latestFailure").isNull)
    }

    private fun historyCount(accountId: UUID): Long = jdbc.sql("select count(*) from battle_history_event where account_id=:account")
        .param("account", accountId).query(Long::class.java).single()

    private inner class Client {
        private val cookies = mutableListOf<Cookie>()
        var lastResponse: org.springframework.mock.web.MockHttpServletResponse? = null

        fun signup(email: String, nickname: String): UUID = UUID.fromString(
            post(
                "/api/v1/auth/signup",
                mapOf("email" to email, "password" to "password123", "nickname" to nickname),
            ).at("/data/accountId").asText(),
        )

        fun login(email: String) {
            post("/api/v1/auth/login", mapOf("email" to email, "password" to "password123"))
        }

        fun logout() {
            post("/api/v1/auth/logout", emptyMap<String, String>())
        }

        fun selectMaterial() {
            post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        }

        fun openGameSession(): String = post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()

        fun post(
            path: String,
            body: Any,
            gameSessionId: String? = null,
            token: String? = null,
            key: UUID = UUID.randomUUID(),
        ): JsonNode {
            val request = post(path)
                .header("Idempotency-Key", key.toString())
                .header("Origin", "http://localhost:5173")
                .contentType(MediaType.APPLICATION_JSON)
                .accept(MediaType.APPLICATION_JSON)
                .content(mapper.writeValueAsString(body))
            if (gameSessionId != null) request.header("X-Game-Session-Id", gameSessionId)
            if (token != null) request.header("X-Battle-Token", token)
            if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
            val result = mvc.perform(request).andExpect(status().isOk).andReturn()
            capture(result)
            return mapper.readTree(result.response.contentAsString)
        }

        fun get(path: String): JsonNode {
            val request = getRequest(path).accept(MediaType.APPLICATION_JSON)
            if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
            val result = mvc.perform(request).andExpect(status().isOk).andReturn()
            capture(result)
            return mapper.readTree(result.response.contentAsString)
        }

        private fun capture(result: MvcResult) {
            lastResponse = result.response
            result.response.cookies.forEach { cookie ->
                cookies.removeIf { it.name == cookie.name }
                cookies += cookie
            }
        }
    }

}
