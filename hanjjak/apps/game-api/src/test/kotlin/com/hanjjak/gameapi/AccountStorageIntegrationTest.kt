package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
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
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.status
import java.util.UUID

@GameApiIntegrationTest
class AccountStorageIntegrationTest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var battleStats: MutableBattleStatsProvider

    @Test
    fun `signup login save reconnect and forced close recovery restore account state`() {
        val email = "storage-${UUID.randomUUID()}@test.local"
        val firstClient = Client()
        val signup = firstClient.postJson(
            "/api/v1/auth/signup",
            mapOf("email" to email, "password" to "password123", "nickname" to "before-save"),
        )
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        assertEquals(accountId.toString(), firstClient.getJson("/api/v1/auth/session").at("/data/account/accountId").asText())

        firstClient.postJson("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        val firstGameSessionId = firstClient.openGameSession()
        assertTrue(eventCount("MATERIAL_PREFERENCE_SELECTED") >= 1)
        firstClient.postJson("/api/v1/auth/profile/nickname", mapOf("nickname" to "after-save"))
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        val battle = firstClient.postJson("/api/v1/battles/cycles", mapOf("stageId" to "stage.01-01"))
        assertTrue(battle.at("/data/battle/success").asBoolean())
        val savedRice = wallet(accountId)
        assertTrue(savedRice > 0)
        assertEquals(1, clearCount(accountId, "stage.01-01"))
        assertTrue(stage(firstClient.getJson("/api/v1/stages").at("/data"), "stage.01-02").path("unlocked").asBoolean())

        val reconnectClient = Client()
        val login = reconnectClient.postJson(
            "/api/v1/auth/login",
            mapOf("email" to email, "password" to "password123"),
        )
        assertEquals(accountId.toString(), login.at("/data/accountId").asText())
        assertEquals("REPLACED", gameSessionStatus(firstGameSessionId))
        val reconnectGameSessionId = reconnectClient.openGameSession()
        assertEquals("after-save", login.at("/data/nickname").asText())
        assertEquals(savedRice, login.at("/data/rice").asLong())
        assertEquals("POTATO", reconnectClient.getJson("/api/v1/material-preference").at("/data/primaryMaterialType").asText())
        val restoredStages = reconnectClient.getJson("/api/v1/stages").at("/data")
        assertEquals(1, stage(restoredStages, "stage.01-01").path("clearCount").asLong())
        assertTrue(stage(restoredStages, "stage.01-02").path("unlocked").asBoolean())

        val forcedCloseClient = Client()
        val recovered = forcedCloseClient.postJson(
            "/api/v1/auth/login",
            mapOf("email" to email, "password" to "password123"),
        )
        assertEquals(accountId.toString(), recovered.at("/data/accountId").asText())
        assertEquals("REPLACED", gameSessionStatus(reconnectGameSessionId))
        val recoveredGameSessionId = forcedCloseClient.openGameSession()
        assertEquals("ACTIVE", gameSessionStatus(recoveredGameSessionId))
        assertEquals("after-save", recovered.at("/data/nickname").asText())
        assertEquals(savedRice, recovered.at("/data/rice").asLong())
        assertTrue(forcedCloseClient.getJson("/api/v1/auth/session").at("/data/authenticated").asBoolean())
    }

    @Test
    fun `password change invalidates sessions aborts battle and replaces login credential`() {
        val email = "password-change-${UUID.randomUUID()}@test.local"
        val client = Client()
        val signup = client.postJson(
            "/api/v1/auth/signup",
            mapOf("email" to email, "password" to "password123", "nickname" to "password-change"),
        )
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.postJson("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        val gameSessionId = client.openGameSession()
        val battleSessionId = client.postJson("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-01"))
            .at("/data/battleSessionId").asText()

        val changed = client.postJson(
            "/api/v1/auth/profile/password",
            mapOf(
                "currentPassword" to "password123",
                "newPassword" to "new-password456",
                "newPasswordConfirmation" to "new-password456",
            ),
        )

        val changedVersion = changed.path("stateVersion").asLong()
        assertTrue(changedVersion > signup.path("stateVersion").asLong())
        assertEquals(false, changed.at("/data/authenticated").asBoolean())
        assertEquals("CLOSED", gameSessionStatus(gameSessionId))
        assertEquals("ABORTED", battleSessionStatus(battleSessionId))
        assertEquals(false, client.getJson("/api/v1/auth/session").at("/data/authenticated").asBoolean())

        Client().postJsonExpectUnauthorized(
            "/api/v1/auth/login",
            mapOf("email" to email, "password" to "password123"),
            "LOGIN_FAILED",
        )
        val login = Client().postJson(
            "/api/v1/auth/login",
            mapOf("email" to email, "password" to "new-password456"),
        )
        assertEquals(accountId.toString(), login.at("/data/accountId").asText())
        assertEquals(changedVersion, login.path("stateVersion").asLong())
    }

    private fun Client.postJson(path: String, body: Any): JsonNode {
        val request = post(path)
            .header("Idempotency-Key", UUID.randomUUID().toString())
            .header("Origin", "http://localhost:5173")
            .contentType(MediaType.APPLICATION_JSON)
            .accept(MediaType.APPLICATION_JSON)
            .content(mapper.writeValueAsString(body))
        if (path.startsWith("/api/v1/battles/")) request.header("X-Game-Session-Id", requireNotNull(gameSessionId))
        if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
        val result = mvc.perform(request).andExpect(status().isOk).andReturn()
        captureCookies(result)
        return mapper.readTree(result.response.contentAsString)
    }

    private fun Client.postJsonExpectUnauthorized(path: String, body: Any, code: String) {
        val request = post(path)
            .header("Idempotency-Key", UUID.randomUUID().toString())
            .header("Origin", "http://localhost:5173")
            .contentType(MediaType.APPLICATION_JSON)
            .accept(MediaType.APPLICATION_JSON)
            .content(mapper.writeValueAsString(body))
        if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
        val result = mvc.perform(request).andExpect(status().isUnauthorized).andReturn()
        assertEquals(code, mapper.readTree(result.response.contentAsString).path("code").asText())
    }

    private fun Client.openGameSession(): String {
        val id = postJson("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()
        gameSessionId = id
        return id
    }

    private fun Client.getJson(path: String): JsonNode {
        val request = get(path).accept(MediaType.APPLICATION_JSON)
        if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
        val result = mvc.perform(request).andExpect(status().isOk).andReturn()
        captureCookies(result)
        return mapper.readTree(result.response.contentAsString)
    }


    private fun Client.captureCookies(result: MvcResult) {
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

    private fun wallet(accountId: UUID): Long = jdbc.sql("select balance from wallet_balance where account_id=:account")
        .param("account", accountId)
        .query(Long::class.java)
        .single()

    private fun eventCount(eventType: String): Long = jdbc.sql("select count(*) from outbox_event where event_type=:type")
        .param("type", eventType)
        .query(Long::class.java)
        .single()

    private fun gameSessionStatus(gameSessionId: String): String = jdbc.sql("select status from game_session where id=:id")
        .param("id", UUID.fromString(gameSessionId))
        .query(String::class.java)
        .single()

    private fun battleSessionStatus(battleSessionId: String): String = jdbc.sql("select status from battle_session where id=:id")
        .param("id", UUID.fromString(battleSessionId))
        .query(String::class.java)
        .single()

    private class Client {
        var gameSessionId: String? = null
        val cookies = mutableListOf<Cookie>()
    }


}
