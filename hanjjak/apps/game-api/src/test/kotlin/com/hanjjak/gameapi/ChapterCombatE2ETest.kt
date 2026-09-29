package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.sim.FighterStats
import jakarta.servlet.http.Cookie
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.http.MediaType
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.MvcResult
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.status
import java.util.UUID

@GameApiIntegrationTest
class ChapterCombatE2ETest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var battleStats: MutableBattleStatsProvider

    @Test
    fun `full inventory skips drops while combat experience and rice continue`() {
        val account = AccountSession()
        account.signup("full-${UUID.randomUUID()}@test.local", "full-inventory")
        account.selectMaterial("POTATO")
        account.openGameSession()
        jdbc.sql("insert into inventory_instance select gen_random_uuid(),:account,'gem:1:flat_attack',false,n from generate_series(1,200) n")
            .param("account", account.accountId).update()
        battleStats.set(account.accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        var skipped = 0L
        repeat(5) {
            val result = account.postJson("/api/v1/battles/cycles", mapOf("stageId" to "stage.01-01"))
            assertTrue(result.at("/data/battle/success").asBoolean())
            assertTrue(result.at("/data/progression/experienceGained").asLong() > 0)
            assertTrue(result.at("/data/progression/riceGained").asLong() > 0)
            result.at("/data/reward/rewards").forEach { line ->
                assertEquals(0, line.path("grantedQuantity").asLong())
                assertEquals(0, line.path("discardedQuantity").asLong())
                skipped += line.path("skippedQuantity").asLong()
            }
        }
        assertTrue(skipped > 0)
        assertEquals(200, account.getJson("/api/v1/inventory/status").at("/data/usedSlots").asInt())
    }

    @Test
    fun `chapter one start to four ten covers failure retry unlock denial and reconnect restore`() {
        val account = AccountSession()
        val email = "chapter-${UUID.randomUUID()}@test.local"
        account.signup(email, "chapter-runner")
        account.selectMaterial("POTATO")
        account.openGameSession()

        val initialStages = account.getJson("/api/v1/stages").at("/data")
        assertTrue(stage(initialStages, "stage.01-01").path("unlocked").asBoolean())
        assertFalse(stage(initialStages, "stage.01-02").path("unlocked").asBoolean())
        assertFalse(stage(initialStages, "stage.04-10").path("unlocked").asBoolean())
        assertFalse(stage(initialStages, "stage.01-01").path("bossOnly").asBoolean())
        assertTrue(stage(initialStages, "stage.01-10").path("bossOnly").asBoolean())
        assertFalse(stage(initialStages, "stage.01-10").path("repeatEligible").asBoolean())

        battleStats.set(account.accountId, FighterStats(1, 1, 0))
        val failed = account.postJson("/api/v1/battles/cycles", mapOf("stageId" to "stage.01-01"))
        assertFalse(failed.at("/data/battle/success").asBoolean())
        assertEquals(0, clearCount(account.accountId, "stage.01-01"))

        battleStats.set(account.accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        val retry = account.postJson("/api/v1/battles/cycles", mapOf("stageId" to "stage.01-01"))
        assertTrue(retry.at("/data/battle/success").asBoolean())
        assertEquals(1, clearCount(account.accountId, "stage.01-01"))

        account.postJsonExpectClientError("/api/v1/battles/cycles", mapOf("stageId" to "stage.02-01"))

        val chapterOne = account.postJson("/api/v1/battles/chapters/1/auto-run", emptyMap<String, String>())
        assertEquals("CHAPTER_COMPLETE", chapterOne.at("/data/stoppedReason").asText())
        assertEquals("stage.01-10", chapterOne.at("/data/stoppedStageId").asText())
        assertEquals(9, chapterOne.at("/data/runs").size())
        assertEquals(1, clearCount(account.accountId, "stage.01-10"))
        assertTrue(stage(account.getJson("/api/v1/stages").at("/data"), "stage.02-01").path("unlocked").asBoolean())

        val reconnected = AccountSession()
        reconnected.login(email)
        reconnected.selectMaterial("POTATO")
        reconnected.openGameSession()
        val restoredStages = reconnected.getJson("/api/v1/stages").at("/data")
        assertTrue(stage(restoredStages, "stage.02-01").path("unlocked").asBoolean())
        assertEquals(1, stage(restoredStages, "stage.01-10").path("clearCount").asLong())

        listOf(2, 3, 4).forEach { chapter ->
            val response = reconnected.postJson("/api/v1/battles/chapters/$chapter/auto-run", emptyMap<String, String>())
            assertEquals("CHAPTER_COMPLETE", response.at("/data/stoppedReason").asText())
            assertEquals("stage.%02d-10".format(chapter), response.at("/data/stoppedStageId").asText())
            assertTrue(response.at("/data/runs").size() >= 10)
        }
        assertEquals(1, clearCount(account.accountId, "stage.04-10"))
        assertTrue(stage(reconnected.getJson("/api/v1/stages").at("/data"), "stage.04-10").path("unlocked").asBoolean())
        assertTrue(eventCount("BATTLE_CYCLE_COMPLETED") >= 41)
    }

    private fun AccountSession.signup(email: String, nickname: String) {
        val result = postJson(
            "/api/v1/auth/signup",
            mapOf("email" to email, "password" to "password123", "nickname" to nickname),
            authenticated = false,
        )
        accountId = UUID.fromString(result.at("/data/accountId").asText())
    }

    private fun AccountSession.login(email: String) {
        val result = postJson(
            "/api/v1/auth/login",
            mapOf("email" to email, "password" to "password123"),
            authenticated = false,
        )
        accountId = UUID.fromString(result.at("/data/accountId").asText())
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        // The caller selects the immutable material before opening a replacement game session.
    }

    private fun AccountSession.selectMaterial(material: String) {
        postJson("/api/v1/material-preference", mapOf("materialType" to material))
    }

    private fun AccountSession.postJson(path: String, body: Any, authenticated: Boolean = true): JsonNode {
        val request = post(path)
            .header("Idempotency-Key", UUID.randomUUID().toString())
            .header("Origin", "http://localhost:5173")
            .contentType(MediaType.APPLICATION_JSON)
            .accept(MediaType.APPLICATION_JSON)
            .content(mapper.writeValueAsString(body))
        if (path.startsWith("/api/v1/battles/")) request.header("X-Game-Session-Id", requireNotNull(gameSessionId))
        if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
        if (authenticated) request.sessionAttr("accountId", accountId)
        val result = mvc.perform(request).andExpect(status().isOk).andReturn()
        captureCookies(result)
        return mapper.readTree(result.response.contentAsString)
    }

    private fun AccountSession.postJsonExpectClientError(path: String, body: Any) {
        val request = post(path)
            .sessionAttr("accountId", accountId)
            .header("Idempotency-Key", UUID.randomUUID().toString())
            .header("Origin", "http://localhost:5173")
            .contentType(MediaType.APPLICATION_JSON)
            .accept(MediaType.APPLICATION_JSON)
            .content(mapper.writeValueAsString(body))
        request.header("X-Game-Session-Id", requireNotNull(gameSessionId))
        if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
        mvc.perform(request).andExpect(status().is4xxClientError)
    }

    private fun AccountSession.openGameSession() {
        gameSessionId = postJson("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()
    }

    private fun AccountSession.getJson(path: String): JsonNode {
        val request = get(path)
            .sessionAttr("accountId", accountId)
            .accept(MediaType.APPLICATION_JSON)
        if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
        val result = mvc.perform(request).andExpect(status().isOk).andReturn()
        captureCookies(result)
        return mapper.readTree(result.response.contentAsString)
    }

    private fun AccountSession.captureCookies(result: MvcResult) {
        result.response.cookies.forEach { cookie ->
            cookies.removeIf { it.name == cookie.name }
            cookies += cookie
        }
    }

    private fun stage(stages: JsonNode, stageId: String): JsonNode = stages.elements().asSequence().first { it.path("stageId").asText() == stageId }

    private fun clearCount(accountId: UUID, stageId: String): Long = jdbc.sql("select count(*) from stage_progress where account_id=:account and stage_id=:stage and first_cleared_at is not null")
        .param("account", accountId)
        .param("stage", stageId)
        .query(Long::class.java)
        .single()

    private fun eventCount(eventType: String): Long = jdbc.sql("select count(*) from outbox_event where event_type=:type")
        .param("type", eventType)
        .query(Long::class.java)
        .single()

    private inner class AccountSession {
        lateinit var accountId: UUID
        var gameSessionId: String? = null
        val cookies = mutableListOf<Cookie>()
    }


}
