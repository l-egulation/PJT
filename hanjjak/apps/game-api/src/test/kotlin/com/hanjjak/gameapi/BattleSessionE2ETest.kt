package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.sim.FighterStats
import jakarta.servlet.http.Cookie
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNotEquals
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.http.MediaType
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.MvcResult
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get as getRequest
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch as patchRequest
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.status
import java.util.UUID

@GameApiIntegrationTest
class BattleSessionE2ETest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var battleStats: MutableBattleStatsProvider
    @Autowired lateinit var battleSessions: com.hanjjak.battle.application.BattleSessionService

    @Test
    fun `battle session completes once then aborts and expires without extra rewards`() {
        val client = Client()
        val signup = client.post("/api/v1/auth/signup", mapOf("email" to "battle-session-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "session-runner"))
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        val gameSessionId = client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()

        val started = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-01"), gameSessionId)
        val battleSessionId = started.at("/data/battleSessionId").asText()
        val token = started.at("/data/battleToken").asText()
        assertTrue(started.at("/data/durationMilliseconds").asLong() > 0)
        jdbc.sql("update battle_session set completable_at=now()-interval '1 second' where id=:id")
            .param("id", UUID.fromString(battleSessionId))
            .update()

        val completionKey = UUID.randomUUID()
        val completion = client.post("/api/v1/battles/sessions/$battleSessionId/complete", mapOf("predictedHash" to null, "renderingCheckpoint" to null), gameSessionId, token, completionKey)
        val replay = client.post("/api/v1/battles/sessions/$battleSessionId/complete", mapOf("predictedHash" to null, "renderingCheckpoint" to null), gameSessionId, token, completionKey)
        assertEquals(completion.at("/data"), replay.at("/data"))
        assertEquals("COMPLETED", completion.at("/data/status").asText())
        assertEquals(1, clearCount(accountId, "stage.01-01"))

        client.postExpectConflict("/api/v1/battles/sessions/$battleSessionId/complete", mapOf("predictedHash" to null, "renderingCheckpoint" to null), gameSessionId, token)

        val second = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-02"), gameSessionId)
        client.post("/api/v1/battles/sessions/${second.at("/data/battleSessionId").asText()}/abort", emptyMap<String, String>(), gameSessionId, second.at("/data/battleToken").asText())
        assertEquals(0, clearCount(accountId, "stage.01-02"))

        val third = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-02"), gameSessionId)
        val thirdId = third.at("/data/battleSessionId").asText()
        jdbc.sql("update battle_session set last_heartbeat_at=now()-interval '91 seconds' where id=:id")
            .param("id", UUID.fromString(thirdId))
            .update()
        client.postExpectConflict("/api/v1/battles/sessions/$thirdId/heartbeat", emptyMap<String, String>(), gameSessionId, third.at("/data/battleToken").asText())
    }

    @Test
    fun `normal monster settlement persists before cycle completion and replays once`() {
        val client = Client()
        val signup = client.post("/api/v1/auth/signup", mapOf("email" to "monster-settlement-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "settlement-runner"))
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        val gameSessionId = client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()
        val started = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-01"), gameSessionId)
        val battleSessionId = started.at("/data/battleSessionId").asText()
        val token = started.at("/data/battleToken").asText()
        val firstDefeat = started.at("/data/renderingTimeline").first { it.at("/type").asText() == "ENEMY_DEFEATED" }
        val enemyIndex = firstDefeat.at("/enemyIndex").asInt()
        val logicalTick = firstDefeat.at("/logicalTick").asInt()
        jdbc.sql("update battle_enemy_settlement set eligible_at=now()-interval '1 second' where battle_session_id=:battle and enemy_index=:enemy")
            .params(mapOf("battle" to UUID.fromString(battleSessionId), "enemy" to enemyIndex))
            .update()

        val key = UUID.randomUUID()
        val request = mapOf(
            "throughEnemyIndex" to enemyIndex,
            "renderingCheckpoint" to mapOf("logicalTick" to logicalTick, "defeatedNormals" to enemyIndex, "playerHp" to started.at("/data/input/player/maxHp").asInt()),
        )
        val settled = client.post("/api/v1/battles/sessions/$battleSessionId/settlements", request, gameSessionId, token, key)
        val replay = client.post("/api/v1/battles/sessions/$battleSessionId/settlements", request, gameSessionId, token, key)

        assertEquals(settled.at("/data"), replay.at("/data"))
        assertEquals(1, settled.at("/data/settlements").size())
        assertTrue(settled.at("/data/settlements/0/progression/experienceGained").asLong() > 0)
        assertTrue(settled.at("/data/settlements/0/progression/riceGained").asLong() > 0)
        assertEquals(1, jdbc.sql("select count(*) from battle_enemy_settlement where battle_session_id=:battle and settled_at is not null").param("battle", UUID.fromString(battleSessionId)).query(Int::class.java).single())
        assertEquals(1, jdbc.sql("select count(*) from progression_reward_command where account_id=:account").param("account", accountId).query(Int::class.java).single())
        assertEquals(0, clearCount(accountId, "stage.01-01"))

        client.post("/api/v1/battles/sessions/$battleSessionId/abort", mapOf("renderingCheckpoint" to request.getValue("renderingCheckpoint")), gameSessionId, token)
        assertEquals(1, jdbc.sql("select count(*) from battle_enemy_settlement where battle_session_id=:battle and settled_at is not null").param("battle", UUID.fromString(battleSessionId)).query(Int::class.java).single())
    }

    /*
     * 자동 진행은 챕터 경계에서 멈추지 않는다. 4-10 을 깨면 5-1 로 넘어간다. 예전에는 40 번째
     * 스테이지가 종점으로 박혀 있어 4-10 에서 제자리 반복으로 남았다. 진행 상한은 이제
     * 그림이 붙은 마지막 스테이지에서 나오므로, 아트 없는 챕터는 자동으로 닫힌 채다.
     */
    @Test
    fun `clearing the chapter four finale advances into chapter five`() {
        val client = Client()
        val signup = client.post("/api/v1/auth/signup", mapOf("email" to "chapter-cross-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "cross-runner"))
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        seedClearsThrough(accountId, "stage.04-09")
        val gameSessionId = client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()

        val started = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.04-10"), gameSessionId)
        jdbc.sql("update battle_session set completable_at=now()-interval '1 second' where id=:id")
            .param("id", UUID.fromString(started.at("/data/battleSessionId").asText()))
            .update()
        val completion = client.post(
            "/api/v1/battles/sessions/${started.at("/data/battleSessionId").asText()}/complete",
            mapOf("predictedHash" to null, "renderingCheckpoint" to null),
            gameSessionId,
            started.at("/data/battleToken").asText(),
        )

        assertTrue(completion.at("/data/battle/success").asBoolean())
        assertEquals("stage.05-01", completion.at("/data/nextStageId").asText())
    }

    /** 그림이 없는 챕터는 진행이 닿지 않는다. 지금은 7-10 이 마지막이라 그대로 머문다. */
    @Test
    fun `progress stops at the last stage that has art`() {
        val client = Client()
        val signup = client.post("/api/v1/auth/signup", mapOf("email" to "chapter-tail-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "tail-runner"))
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        seedClearsThrough(accountId, "stage.10-09")
        val gameSessionId = client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()

        val started = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.10-10"), gameSessionId)
        jdbc.sql("update battle_session set completable_at=now()-interval '1 second' where id=:id")
            .param("id", UUID.fromString(started.at("/data/battleSessionId").asText()))
            .update()
        val completion = client.post(
            "/api/v1/battles/sessions/${started.at("/data/battleSessionId").asText()}/complete",
            mapOf("predictedHash" to null, "renderingCheckpoint" to null),
            gameSessionId,
            started.at("/data/battleToken").asText(),
        )

        assertTrue(completion.at("/data/battle/success").asBoolean())
        assertEquals("stage.10-10", completion.at("/data/nextStageId").asText())
    }

    /** `upTo` 까지를 클리어한 것으로 둔다. 해금 판정이 직전 스테이지 클리어만 보기 때문이다. */
    private fun seedClearsThrough(accountId: UUID, upTo: String) {
        val chapter = upTo.removePrefix("stage.").substringBefore("-").toInt()
        val number = upTo.substringAfter("-").toInt()
        val last = (chapter - 1) * 10 + number
        (1..last).forEach { index ->
            jdbc.sql(
                """insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version)
                   values (:account,:stage,true,now(),1,'progression-rebalance-v1')
                   on conflict (account_id,stage_id) do update set unlocked=true, first_cleared_at=now(), highest_clear_count=1""",
            ).params(
                mapOf(
                    "account" to accountId,
                    "stage" to "stage.%02d-%02d".format((index - 1) / 10 + 1, (index - 1) % 10 + 1),
                ),
            ).update()
        }
    }

    @Test
    fun `repeat target is validated persisted and applied at the cycle boundary`() {
        val client = Client()
        val signup = client.post("/api/v1/auth/signup", mapOf("email" to "repeat-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "repeat-runner"))
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        battleStats.set(accountId, FighterStats(1_000_000, 1_000_000, 1_000_000))
        val gameSessionId = client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()

        val started = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-01"), gameSessionId)
        val update = client.patch("/api/v1/battle/repeat-stage", mapOf("stageId" to "stage.01-01"), gameSessionId)
        assertEquals("REPEAT_STAGE", update.at("/data/idleMode").asText())
        assertEquals("stage.01-01", update.at("/data/repeatStageId").asText())
        assertTrue(update.at("/data/appliesAfterCurrentCycle").asBoolean())

        assertEquals("AUTO_PROGRESS", started.at("/data/idleMode").asText())
        assertTrue(started.at("/data/repeatStageId").isNull)
        jdbc.sql("update battle_session set completable_at=now()-interval '1 second' where id=:id")
            .param("id", UUID.fromString(started.at("/data/battleSessionId").asText()))
            .update()
        val completion = client.post(
            "/api/v1/battles/sessions/${started.at("/data/battleSessionId").asText()}/complete",
            mapOf("predictedHash" to null, "renderingCheckpoint" to null),
            gameSessionId,
            started.at("/data/battleToken").asText(),
        )
        assertEquals("stage.01-01", completion.at("/data/nextStageId").asText())
        assertEquals("REPEAT_STAGE", completion.at("/data/idleMode").asText())
        assertEquals("stage.01-01", completion.at("/data/repeatStageId").asText())

        val restored = client.get("/api/v1/runtime-state")
        assertEquals("stage.01-01", restored.at("/data/currentStageId").asText())
        assertEquals("REPEAT_STAGE", restored.at("/data/idleMode").asText())
        assertEquals("stage.01-01", restored.at("/data/repeatStageId").asText())

        val automatic = client.patch("/api/v1/battle/repeat-stage", mapOf("stageId" to null), gameSessionId)
        assertEquals("AUTO_PROGRESS", automatic.at("/data/idleMode").asText())
        assertTrue(automatic.at("/data/repeatStageId").isMissingNode || automatic.at("/data/repeatStageId").isNull)
        assertFalse(automatic.at("/data/appliesAfterCurrentCycle").asBoolean())
    }

    @Test
    fun `failed battle keeps its stage when a pending repeat target exists`() {
        val client = Client()
        val signup = client.post("/api/v1/auth/signup", mapOf("email" to "failed-retry-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "failed-retry"))
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        val gameSessionId = client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()
        val started = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-01"), gameSessionId)

        jdbc.sql(
            """insert into account_runtime_state(account_id,current_stage_id,repeat_stage_id)
               values (:account,'stage.01-01','stage.01-02')
               on conflict(account_id) do update set repeat_stage_id=excluded.repeat_stage_id""",
        ).param("account", accountId).update()
        jdbc.sql("update battle_session set completable_at=now()-interval '1 second' where id=:id")
            .param("id", UUID.fromString(started.at("/data/battleSessionId").asText()))
            .update()

        val completion = client.post(
            "/api/v1/battles/sessions/${started.at("/data/battleSessionId").asText()}/complete",
            mapOf("predictedHash" to null, "renderingCheckpoint" to null),
            gameSessionId,
            started.at("/data/battleToken").asText(),
        )

        assertFalse(completion.at("/data/battle/success").asBoolean())
        assertEquals("stage.01-01", completion.at("/data/nextStageId").asText())
        assertEquals("stage.01-01", client.get("/api/v1/runtime-state").at("/data/currentStageId").asText())
    }

    @Test
    fun `repeat target rejects locked and breakthrough stages`() {
        val client = Client()
        client.post("/api/v1/auth/signup", mapOf("email" to "repeat-errors-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "repeat-errors"))
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        val gameSessionId = client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText()

        client.patchExpectUnprocessable("/api/v1/battle/repeat-stage", mapOf("stageId" to "stage.01-02"), gameSessionId, "STAGE_LOCKED")
        client.patchExpectUnprocessable("/api/v1/battle/repeat-stage", mapOf("stageId" to "stage.01-10"), gameSessionId, "STAGE_NOT_REPEATABLE")
    }

    @Test
    fun `raid handoff resumes only the exact paused battle once`() {
        val client = Client()
        val signup = client.post("/api/v1/auth/signup", mapOf("email" to "raid-handoff-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "raid-handoff"))
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        val gameSessionId = UUID.fromString(client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText())
        val started = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-01"), gameSessionId.toString())
        val battleSessionId = UUID.fromString(started.at("/data/battleSessionId").asText())
        val handoff = requireNotNull(battleSessions.abortForRaidWithHandoffLocked(accountId))
        assertEquals(battleSessionId, handoff.battleSessionId)
        assertNull(battleSessions.resumeAfterRaidLocked(accountId, UUID.randomUUID(), UUID.randomUUID(), gameSessionId))

        val resumed = requireNotNull(battleSessions.resumeAfterRaidLocked(accountId, UUID.randomUUID(), battleSessionId, gameSessionId))
        assertNotEquals(battleSessionId, resumed.battleSessionId)
        assertNull(battleSessions.resumeAfterRaidLocked(accountId, UUID.randomUUID(), battleSessionId, gameSessionId))
        assertEquals(1, jdbc.sql("select count(*) from battle_session where account_id=:account and status='ACTIVE'").param("account", accountId).query(Int::class.java).single())
    }

    @Test
    fun `expired game session consumes exact handoff without restarting battle`() {
        val client = Client()
        val signup = client.post("/api/v1/auth/signup", mapOf("email" to "raid-expired-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "raid-expired"))
        val accountId = UUID.fromString(signup.at("/data/accountId").asText())
        client.post("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        val gameSessionId = UUID.fromString(client.post("/api/v1/game-sessions", emptyMap<String, String>()).at("/data/gameSessionId").asText())
        val started = client.post("/api/v1/battles/sessions", mapOf("stageId" to "stage.01-01"), gameSessionId.toString())
        val battleSessionId = UUID.fromString(started.at("/data/battleSessionId").asText())
        battleSessions.abortForRaidWithHandoffLocked(accountId)
        jdbc.sql("update game_session set status='EXPIRED',closed_at=now() where id=:id").param("id", gameSessionId).update()

        assertNull(battleSessions.resumeAfterRaidLocked(accountId, UUID.randomUUID(), battleSessionId, gameSessionId))
        assertNull(battleSessions.resumeAfterRaidLocked(accountId, UUID.randomUUID(), battleSessionId, gameSessionId))
        assertEquals(0, jdbc.sql("select count(*) from battle_session where account_id=:account and status='ACTIVE'").param("account", accountId).query(Int::class.java).single())
    }

    private fun clearCount(accountId: UUID, stageId: String): Int = jdbc.sql(
        "select count(*) from stage_progress where account_id=:account and stage_id=:stage and first_cleared_at is not null",
    ).params(mapOf("account" to accountId, "stage" to stageId)).query(Int::class.java).single()

    private inner class Client {
        private val cookies = mutableListOf<Cookie>()

        fun post(path: String, body: Any, gameSessionId: String? = null, token: String? = null, key: UUID = UUID.randomUUID()): JsonNode {
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

        fun patch(path: String, body: Any, gameSessionId: String, key: UUID = UUID.randomUUID()): JsonNode {
            val request = patchRequest(path)
                .header("Idempotency-Key", key.toString())
                .header("Origin", "http://localhost:5173")
                .header("X-Game-Session-Id", gameSessionId)
                .contentType(MediaType.APPLICATION_JSON)
                .accept(MediaType.APPLICATION_JSON)
                .content(mapper.writeValueAsString(body))
            if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
            val result = mvc.perform(request).andExpect(status().isOk).andReturn()
            capture(result)
            return mapper.readTree(result.response.contentAsString)
        }

        fun patchExpectUnprocessable(path: String, body: Any, gameSessionId: String, code: String) {
            val request = patchRequest(path)
                .header("Idempotency-Key", UUID.randomUUID().toString())
                .header("Origin", "http://localhost:5173")
                .header("X-Game-Session-Id", gameSessionId)
                .contentType(MediaType.APPLICATION_JSON)
                .accept(MediaType.APPLICATION_JSON)
                .content(mapper.writeValueAsString(body))
            if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
            val result = mvc.perform(request).andExpect(status().isUnprocessableEntity).andReturn()
            assertEquals(code, mapper.readTree(result.response.contentAsString).path("code").asText())
        }

        fun postExpectConflict(path: String, body: Any, gameSessionId: String, token: String) {
            val request = post(path)
                .header("Idempotency-Key", UUID.randomUUID().toString())
                .header("Origin", "http://localhost:5173")
                .header("X-Game-Session-Id", gameSessionId)
                .header("X-Battle-Token", token)
                .contentType(MediaType.APPLICATION_JSON)
                .content(mapper.writeValueAsString(body))
            if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
            mvc.perform(request).andExpect(status().isConflict)
        }

        private fun capture(result: MvcResult) {
            result.response.cookies.forEach { cookie ->
                cookies.removeIf { it.name == cookie.name }
                cookies += cookie
            }
        }
    }

}
