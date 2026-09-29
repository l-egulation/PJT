package com.hanjjak.battle.application

import com.fasterxml.jackson.annotation.JsonInclude
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.stage.application.StageCatalog
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Service
class BattleModeService(
    private val stages: StageCatalog,
    private val mapper: ObjectMapper,
    private val gameSessions: GameSessionService,
    private val jdbc: JdbcClient,
    private val clock: Clock,
    private val balanceVersions: com.hanjjak.account.application.BalanceVersionService,
) {
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class Snapshot(val idleMode: String, val repeatStageId: String?)
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class UpdateResult(val idleMode: String, val repeatStageId: String?, val appliesAfterCurrentCycle: Boolean, val stateVersion: Long)
    private data class StoredCommand(val fingerprint: String, val resultJson: String)

    fun snapshot(accountId: UUID): Snapshot = repeatStage(accountId)?.let { Snapshot(REPEAT_STAGE, it) }
        ?: Snapshot(AUTO_PROGRESS, null)

    fun update(accountId: UUID, gameSessionId: UUID, idempotencyKey: UUID, idleMode: String, stageId: String?): UpdateResult {
        lockAccount(accountId)
        gameSessions.requireActive(accountId, gameSessionId)
        val normalizedStageId = when (idleMode) {
            AUTO_PROGRESS -> {
                require(stageId == null) { "REPEAT_STAGE_NOT_ALLOWED" }
                null
            }
            REPEAT_STAGE -> requireNotNull(stageId) { "REPEAT_STAGE_REQUIRED" }
            else -> throw IllegalArgumentException("INVALID_IDLE_MODE")
        }
        val fingerprint = sha256("idle-mode\u0000$gameSessionId\u0000$idleMode\u0000${normalizedStageId.orEmpty()}")
        command(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return mapper.readValue(existing.resultJson, UpdateResult::class.java)
        }
        if (normalizedStageId != null) requireRepeatable(accountId, normalizedStageId)
        val changed = upsert(accountId, normalizedStageId)
        if (changed == 1) incrementStateVersion(accountId)
        val result = UpdateResult(idleMode, normalizedStageId, changed == 1 && activeBattleExists(accountId), stateVersion(accountId))
        saveCommand(accountId, idempotencyKey, fingerprint, result)
        return result
    }

    fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId)
        .query(Long::class.java)
        .single()

    private fun requireRepeatable(accountId: UUID, stageId: String) {
        val stage = stages.require(balanceVersions.current(accountId), stageId)
        require(stage.id.number in 1..9) { "STAGE_NOT_REPEATABLE" }
        require(canEnter(accountId, stage.id.globalIndex)) { "STAGE_LOCKED" }
    }

    private fun canEnter(accountId: UUID, globalIndex: Int): Boolean {
        if (globalIndex == 1) return true
        val previousIndex = globalIndex - 1
        val previous = "stage.%02d-%02d".format((previousIndex - 1) / 10 + 1, (previousIndex - 1) % 10 + 1)
        return jdbc.sql(
            "select count(*) from stage_progress where account_id=:account and stage_id=:stage and first_cleared_at is not null",
        ).params(mapOf("account" to accountId, "stage" to previous)).query(Long::class.java).single() > 0
    }

    private fun repeatStage(accountId: UUID): String? = jdbc.sql(
        "select repeat_stage_id from account_runtime_state where account_id=:account",
    ).param("account", accountId).query(String::class.java).optional().orElse(null)

    private fun upsert(accountId: UUID, repeatStageId: String?): Int = jdbc.sql(
        """insert into account_runtime_state(account_id,current_stage_id,repeat_stage_id)
           values (:account,'stage.01-01',:repeatStage)
           on conflict(account_id) do update set repeat_stage_id=excluded.repeat_stage_id
           where account_runtime_state.repeat_stage_id is distinct from excluded.repeat_stage_id""",
    ).params(mapOf("account" to accountId, "repeatStage" to repeatStageId)).update()

    private fun activeBattleExists(accountId: UUID): Boolean = jdbc.sql(
        "select count(*) from battle_session where account_id=:account and status='ACTIVE'",
    ).param("account", accountId).query(Long::class.java).single() > 0

    private fun incrementStateVersion(accountId: UUID) {
        jdbc.sql("update account set state_version=state_version+1 where id=:account")
            .param("account", accountId)
            .update()
    }

    private fun command(accountId: UUID, idempotencyKey: UUID): StoredCommand? = jdbc.sql(
        "select fingerprint,result_json::text from repeat_stage_command where account_id=:account and idempotency_key=:key",
    ).params(mapOf("account" to accountId, "key" to idempotencyKey)).query { row, _ ->
        StoredCommand(row.getString("fingerprint"), row.getString("result_json"))
    }.optional().orElse(null)

    private fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, result: UpdateResult) {
        jdbc.sql(
            """insert into repeat_stage_command(account_id,idempotency_key,fingerprint,result_json,created_at)
               values (:account,:key,:fingerprint,cast(:result as jsonb),:createdAt)""",
        ).params(
            mapOf(
                "account" to accountId,
                "key" to idempotencyKey,
                "fingerprint" to fingerprint,
                "result" to mapper.writeValueAsString(result),
                "createdAt" to OffsetDateTime.ofInstant(clock.instant(), ZoneOffset.UTC),
            ),
        ).update()
    }

    private fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId)
            .query(UUID::class.java)
            .optional()
            .orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }
    }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8))
        .joinToString("") { "%02x".format(it) }

    private companion object {
        const val AUTO_PROGRESS = "AUTO_PROGRESS"
        const val REPEAT_STAGE = "REPEAT_STAGE"
    }
}
