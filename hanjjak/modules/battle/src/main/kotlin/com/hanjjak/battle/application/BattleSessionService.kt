package com.hanjjak.battle.application

import com.fasterxml.jackson.annotation.JsonInclude
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.inventory.domain.RewardResult
import com.hanjjak.progression.domain.ProgressionRewardResult
import com.hanjjak.sim.CycleInput
import com.hanjjak.sim.CycleResult
import com.hanjjak.sim.CombatRenderingEvent
import com.hanjjak.stage.domain.StageId
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.scheduling.annotation.Scheduled
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Service
class BattleSessionService(
    private val mapper: ObjectMapper,
    private val battles: StageBattleService,
    private val settlements: BattleEnemySettlementService,
    private val events: DomainEventPublisher,
    private val stageCompletion: StageCompletionService,
    private val firstClearRewardPresenter: FirstClearRewardPresenter,
    private val stats: BattleStatsProvider,
    private val skills: BattleSkillProvider,
    private val gameSessions: GameSessionService,
    private val battleModes: BattleModeService,
    private val history: BattleHistoryService,
    private val jdbc: JdbcClient,
    private val balanceVersions: com.hanjjak.account.application.BalanceVersionService,
    private val clock: Clock,
) {
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class Started(
        val battleSessionId: UUID,
        val battleToken: UUID,
        val status: String,
        val stageId: String,
        val idleMode: String,
        val repeatStageId: String?,
        val logicalTickBasis: Int,
        val tickDurationMilliseconds: Int,
        val durationMilliseconds: Long,
        val completableAt: Instant,
        val input: CycleInput,
        val renderingTimeline: List<CombatRenderingEvent>,
    )

    data class State(val battleSessionId: UUID, val status: String, val lastHeartbeatAt: Instant)
    data class SettlementResult(val battleSessionId: UUID, val throughEnemyIndex: Int, val settlements: List<BattleEnemySettlementService.Settlement>)
    data class RenderingCheckpoint(val logicalTick: Int, val defeatedNormals: Int, val playerHp: Int)
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class Completion(
        val battleSessionId: UUID,
        val status: String,
        val stageId: String,
        val idleMode: String,
        val repeatStageId: String?,
        val nextStageId: String,
        val battle: CycleResult,
        val reward: RewardResult,
        val progression: ProgressionRewardResult,
        val settlements: List<BattleEnemySettlementService.Settlement>,
        val predictionMatched: Boolean?,
        val firstClearReward: FirstClearRewardView? = null,
    )

    private data class StoredSession(
        val id: UUID,
        val accountId: UUID,
        val gameSessionId: UUID,
        val tokenHash: String,
        val status: String,
        val stageId: String,
        val seed: Long,
        val inputJson: String,
        val lastHeartbeatAt: Instant,
        val completableAt: Instant,
        val resultJson: String?,
    )
    data class RaidBattleHandoffData(val battleSessionId: UUID, val gameSessionId: UUID, val stageId: String)

    private data class StoredCommand(val fingerprint: String, val resultJson: String)

    @Transactional
    fun start(accountId: UUID, gameSessionId: UUID, idempotencyKey: UUID, stageId: String): Started =
        startLocked(accountId, gameSessionId, idempotencyKey, stageId, lockAccount = true)

    /**
     * Resumes only the exact battle cycle captured when raid entry paused it.
     * The marker is consumed once; no unrelated aborted battle is eligible.
     */
    @Transactional
    fun resumeAfterRaidLocked(
        accountId: UUID,
        commandId: UUID,
        battleSessionId: UUID,
        gameSessionId: UUID,
    ): Started? {
        val previous = jdbc.sql(
            "select id,game_session_id,stage_id from battle_session where id=:id and account_id=:account and game_session_id=:gameSession and status='ABORTED' and raid_handoff_consumed_at is null",
        ).params(mapOf("id" to battleSessionId, "account" to accountId, "gameSession" to gameSessionId))
            .query { row, _ -> Triple(row.getObject("id", UUID::class.java), row.getObject("game_session_id", UUID::class.java), row.getString("stage_id")) }
            .optional().orElse(null) ?: return null
        if (!gameSessions.isActiveForUpdate(accountId, previous.second)) {
            jdbc.sql("update battle_session set raid_handoff_consumed_at=:now where id=:id and raid_handoff_consumed_at is null")
                .params(mapOf("now" to utc(clock.instant()), "id" to previous.first)).update()
            return null
        }
        val consumed = jdbc.sql("update battle_session set raid_handoff_consumed_at=:now where id=:id and raid_handoff_consumed_at is null")
            .params(mapOf("now" to utc(clock.instant()), "id" to previous.first)).update()
        if (consumed != 1) return null
        return startLocked(accountId, previous.second, commandId, previous.third, lockAccount = false)
    }

    private fun startLocked(accountId: UUID, gameSessionId: UUID, idempotencyKey: UUID, stageId: String, lockAccount: Boolean): Started {
        if (lockAccount) lockAccount(accountId)
        gameSessions.requireActive(accountId, gameSessionId)
        requireStageAccess(accountId, stageId)
        val fingerprint = sha256("start\u0000$gameSessionId\u0000$stageId")
        command(accountId, idempotencyKey, fingerprint)?.let {
            return mapper.readValue(it.resultJson, Started::class.java)
        }

        closeActive(accountId, "ABORTED")
        val battleSessionId = UUID.randomUUID()
        val battleToken = UUID.randomUUID()
        val seed = idempotencyKey.mostSignificantBits xor idempotencyKey.leastSignificantBits
        val balanceVersion = balanceVersions.current(accountId)
        val input = battles.input(stageId, stats.forAccount(accountId), seed, balanceVersion, skills.forAccount(accountId))
        val trace = com.hanjjak.sim.CombatSimulator.simulateWithEvents(input)
        val now = clock.instant()
        val durationMilliseconds = trace.result.elapsedTicks * TICK_DURATION_MILLISECONDS.toLong()
        val completableAt = now.plusMillis(durationMilliseconds)
        val battleMode = battleModes.snapshot(accountId)
        val started = Started(
            battleSessionId = battleSessionId,
            battleToken = battleToken,
            status = "ACTIVE",
            stageId = stageId,
            idleMode = battleMode.idleMode,
            repeatStageId = battleMode.repeatStageId,
            logicalTickBasis = 0,
            tickDurationMilliseconds = TICK_DURATION_MILLISECONDS,
            durationMilliseconds = durationMilliseconds,
            completableAt = completableAt,
            input = input,
            renderingTimeline = trace.renderingTimeline,
        )

        jdbc.sql(
            """insert into battle_session(
                 id,account_id,game_session_id,token_hash,status,stage_id,content_version,seed,logical_tick_basis,input_json,
                 started_at,last_heartbeat_at,completable_at
               ) values (
                 :id,:account,:gameSession,:tokenHash,'ACTIVE',:stage,:contentVersion,:seed,0,cast(:input as jsonb),:now,:now,:completableAt
               )""",
        ).params(
            mapOf(
                "id" to battleSessionId,
                "account" to accountId,
                "gameSession" to gameSessionId,
                "tokenHash" to sha256(battleToken.toString()),
                "stage" to stageId,
                "contentVersion" to input.contentVersion,
                "seed" to seed,
                "input" to mapper.writeValueAsString(input),
                "now" to utc(now),
                "completableAt" to utc(completableAt),
            ),
        ).update()
        settlements.prepare(accountId, battleSessionId, stageId, seed, now, TICK_DURATION_MILLISECONDS, trace.renderingTimeline, input.contentVersion)
        history.recordStageEntered(accountId, battleSessionId, stageId, now)
        updateCurrentStage(accountId, stageId)
        saveCommand(accountId, idempotencyKey, fingerprint, started)
        return started
    }

    /** Closes the active cycle and returns its exact identity for one-shot resume. */
    @Transactional
    fun abortForRaidWithHandoffLocked(accountId: UUID): RaidBattleHandoffData? {
        val previous = jdbc.sql("select id,game_session_id,stage_id from battle_session where account_id=:account and status='ACTIVE' order by started_at desc,id desc limit 1")
            .param("account", accountId).query { row, _ -> RaidBattleHandoffData(row.getObject("id", UUID::class.java), row.getObject("game_session_id", UUID::class.java), row.getString("stage_id")) }.optional().orElse(null)
        closeActive(accountId, "ABORTED")
        return previous
    }
    @Transactional
    fun heartbeat(
        accountId: UUID,
        gameSessionId: UUID,
        battleSessionId: UUID,
        battleToken: UUID,
        idempotencyKey: UUID,
    ): State {
        lockAccount(accountId)
        gameSessions.requireActive(accountId, gameSessionId)
        val fingerprint = sha256("heartbeat\u0000$gameSessionId\u0000$battleSessionId")
        command(accountId, idempotencyKey, fingerprint)?.let { return mapper.readValue(it.resultJson, State::class.java) }
        val session = requireOpen(accountId, gameSessionId, battleSessionId, battleToken)
        if (session.lastHeartbeatAt.isBefore(clock.instant().minus(SESSION_TIMEOUT))) {
            expireSession(session.id)
            throw IllegalArgumentException("BATTLE_SESSION_EXPIRED")
        }
        val now = clock.instant()
        jdbc.sql("update battle_session set last_heartbeat_at=:now where id=:id and status='ACTIVE'")
            .params(mapOf("now" to utc(now), "id" to session.id)).update()
        val state = State(session.id, "ACTIVE", now)
        saveCommand(accountId, idempotencyKey, fingerprint, state)
        return state
    }

    @Transactional
    fun settle(
        accountId: UUID,
        gameSessionId: UUID,
        battleSessionId: UUID,
        battleToken: UUID,
        idempotencyKey: UUID,
        throughEnemyIndex: Int,
        renderingCheckpoint: RenderingCheckpoint,
    ): SettlementResult {
        lockAccount(accountId)
        gameSessions.requireActive(accountId, gameSessionId)
        val fingerprint = sha256("settle\u0000$gameSessionId\u0000$battleSessionId\u0000$throughEnemyIndex\u0000${mapper.writeValueAsString(renderingCheckpoint)}")
        command(accountId, idempotencyKey, fingerprint)?.let {
            return mapper.readValue(it.resultJson, SettlementResult::class.java)
        }
        val session = requireOpen(accountId, gameSessionId, battleSessionId, battleToken)
        if (session.lastHeartbeatAt.isBefore(clock.instant().minus(SESSION_TIMEOUT))) {
            expireSession(session.id)
            throw IllegalArgumentException("BATTLE_SESSION_EXPIRED")
        }
        require(renderingCheckpoint.defeatedNormals == throughEnemyIndex) { "INVALID_RENDERING_CHECKPOINT" }
        val summary = settlements.settleThrough(accountId, battleSessionId, session.stageId, throughEnemyIndex, renderingCheckpoint.logicalTick)
        val result = SettlementResult(battleSessionId, throughEnemyIndex, summary.settlements)
        saveCommand(accountId, idempotencyKey, fingerprint, result)
        return result
    }

    @Transactional
    fun complete(
        accountId: UUID,
        gameSessionId: UUID,
        battleSessionId: UUID,
        battleToken: UUID,
        idempotencyKey: UUID,
        predictedHash: String?,
        renderingCheckpoint: RenderingCheckpoint?,
    ): Completion {
        lockAccount(accountId)
        val fingerprint = sha256("complete\u0000$gameSessionId\u0000$battleSessionId\u0000${predictedHash.orEmpty()}\u0000${mapper.writeValueAsString(renderingCheckpoint)}")
        command(accountId, idempotencyKey, fingerprint)?.let {
            return mapper.readValue(it.resultJson, Completion::class.java)
        }
        gameSessions.requireActive(accountId, gameSessionId)
        val session = requireSession(accountId, gameSessionId, battleSessionId, battleToken)
        if (session.status != "ACTIVE") throw IllegalArgumentException("BATTLE_SESSION_CLOSED")
        if (session.lastHeartbeatAt.isBefore(clock.instant().minus(SESSION_TIMEOUT))) {
            expireSession(session.id)
            throw IllegalArgumentException("BATTLE_SESSION_EXPIRED")
        }
        require(!clock.instant().isBefore(session.completableAt)) { "BATTLE_SESSION_NOT_READY" }

        val input = mapper.readValue(session.inputJson, CycleInput::class.java)
        val trace = com.hanjjak.sim.CombatSimulator.simulateWithEvents(input)
        val battle = trace.result
        val finalEnemyIndex = input.normalCount + 1
        if (battle.defeatedNormals > 0) settlements.settleThrough(accountId, battleSessionId, session.stageId, battle.defeatedNormals, enforceEligibility = false)
        if (battle.success) settlements.settleThrough(accountId, battleSessionId, session.stageId, finalEnemyIndex, enforceEligibility = false)
        val settlementSummary = settlements.summary(accountId, battleSessionId)
        val firstClearReward = if (battle.success) {
            stageCompletion.complete(accountId, session.stageId, input.contentVersion, battleSessionId).firstClearReward
                ?.let(firstClearRewardPresenter::present)
        } else {
            null
        }
        val automaticNextStageId = nextStage(accountId, session.stageId, battle.success, input.contentVersion)
        val appliedMode = battleModes.snapshot(accountId)
        val nextStageId = if (battle.success) appliedMode.repeatStageId ?: automaticNextStageId else session.stageId
        updateCurrentStage(accountId, nextStageId)
        val predictionMatched = predictedHash?.let { constantTimeEquals(it, resultHash(battle)) }
        val completion = Completion(
            battleSessionId = session.id,
            status = "COMPLETED",
            stageId = session.stageId,
            idleMode = appliedMode.idleMode,
            repeatStageId = appliedMode.repeatStageId,
            nextStageId = nextStageId,
            battle = battle,
            reward = settlementSummary.reward,
            progression = settlementSummary.progression,
            settlements = settlementSummary.settlements,
            predictionMatched = predictionMatched,
            firstClearReward = firstClearReward,
        )

        jdbc.sql(
            """update battle_session set status='COMPLETED',closed_at=:now,predicted_hash=:predicted,
               rendering_checkpoint_json=cast(:checkpoint as jsonb),result_json=cast(:result as jsonb) where id=:id and status='ACTIVE'""",
        ).params(
            mapOf(
                "now" to utc(clock.instant()),
                "predicted" to predictedHash,
                "checkpoint" to mapper.writeValueAsString(renderingCheckpoint),
                "result" to mapper.writeValueAsString(completion),
                "id" to session.id,
            ),
        ).update()
        history.recordStageResult(
            accountId,
            battleSessionId,
            session.stageId,
            input.contentVersion,
            input,
            battle,
            settlementSummary.reward,
            settlementSummary.progression,
            renderingTimeline = trace.renderingTimeline,
        )
        if (!battle.success) {
            history.recordStageReturned(
                accountId,
                battleSessionId,
                session.stageId,
                "${requireNotNull(battle.failureCode)}_RESTARTED",
            )
        }
        publishBattleEvents(accountId, battleSessionId, completion)
        saveCommand(accountId, idempotencyKey, fingerprint, completion)
        return completion
    }

    @Transactional
    fun abort(
        accountId: UUID,
        gameSessionId: UUID,
        battleSessionId: UUID,
        battleToken: UUID,
        idempotencyKey: UUID,
        renderingCheckpoint: RenderingCheckpoint? = null,
    ): State {
        lockAccount(accountId)
        gameSessions.requireActive(accountId, gameSessionId)
        val fingerprint = sha256("abort\u0000$gameSessionId\u0000$battleSessionId\u0000${mapper.writeValueAsString(renderingCheckpoint)}")
        command(accountId, idempotencyKey, fingerprint)?.let {
            return mapper.readValue(it.resultJson, State::class.java)
        }
        val session = requireSession(accountId, gameSessionId, battleSessionId, battleToken)
        if (session.status != "ACTIVE") throw IllegalArgumentException("BATTLE_SESSION_CLOSED")
        renderingCheckpoint?.let { checkpoint ->
            if (checkpoint.defeatedNormals > 0) {
                settlements.settleThrough(accountId, battleSessionId, session.stageId, checkpoint.defeatedNormals, checkpoint.logicalTick)
            }
        }
        val now = clock.instant()
        jdbc.sql("update battle_session set status='ABORTED',closed_at=:now where id=:id and status='ACTIVE'")
            .params(mapOf("now" to utc(now), "id" to session.id))
            .update()
        val state = State(session.id, "ABORTED", session.lastHeartbeatAt)
        saveCommand(accountId, idempotencyKey, fingerprint, state)
        return state
    }

    @Scheduled(fixedDelay = 30_000)
    @Transactional
    fun expireSilentSessions(): Int = jdbc.sql(
        "update battle_session set status='EXPIRED',closed_at=:now where status='ACTIVE' and last_heartbeat_at<:deadline",
    ).params(mapOf("now" to utc(clock.instant()), "deadline" to utc(clock.instant().minus(SESSION_TIMEOUT)))).update()

    fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId)
        .query(Long::class.java)
        .single()

    private fun requireOpen(accountId: UUID, gameSessionId: UUID, battleSessionId: UUID, battleToken: UUID): StoredSession {
        val session = requireSession(accountId, gameSessionId, battleSessionId, battleToken)
        if (session.status != "ACTIVE") throw IllegalArgumentException("BATTLE_SESSION_CLOSED")
        return session
    }

    private fun requireSession(accountId: UUID, gameSessionId: UUID, battleSessionId: UUID, battleToken: UUID): StoredSession {
        val session = jdbc.sql(
            """select id,account_id,game_session_id,token_hash,status,stage_id,seed,input_json::text,
               last_heartbeat_at,completable_at,result_json::text
               from battle_session where id=:id and account_id=:account and game_session_id=:gameSession""",
        ).params(mapOf("id" to battleSessionId, "account" to accountId, "gameSession" to gameSessionId)).query { row, _ ->
            StoredSession(
                row.getObject("id", UUID::class.java),
                row.getObject("account_id", UUID::class.java),
                row.getObject("game_session_id", UUID::class.java),
                row.getString("token_hash"),
                row.getString("status"),
                row.getString("stage_id"),
                row.getLong("seed"),
                row.getString("input_json"),
                row.getObject("last_heartbeat_at", OffsetDateTime::class.java).toInstant(),
                row.getObject("completable_at", OffsetDateTime::class.java).toInstant(),
                row.getString("result_json"),
            )
        }.optional().orElseThrow { IllegalArgumentException("BATTLE_SESSION_NOT_FOUND") }
        require(constantTimeEquals(session.tokenHash, sha256(battleToken.toString()))) { "BATTLE_SESSION_TOKEN_INVALID" }
        return session
    }

    private fun command(accountId: UUID, idempotencyKey: UUID, fingerprint: String): StoredCommand? = jdbc.sql(
        "select fingerprint,result_json::text from battle_session_command where account_id=:account and idempotency_key=:key",
    ).params(mapOf("account" to accountId, "key" to idempotencyKey)).query { row, _ ->
        require(row.getString("fingerprint") == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
        StoredCommand(row.getString("fingerprint"), row.getString("result_json"))
    }.optional().orElse(null)

    private fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, result: Any) {
        jdbc.sql(
            """insert into battle_session_command(account_id,idempotency_key,fingerprint,result_json,created_at)
               values (:account,:key,:fingerprint,cast(:result as jsonb),:now)""",
        ).params(
            mapOf(
                "account" to accountId,
                "key" to idempotencyKey,
                "fingerprint" to fingerprint,
                "result" to mapper.writeValueAsString(result),
                "now" to utc(clock.instant()),
            ),
        ).update()
    }

    private fun requireStageAccess(accountId: UUID, stageId: String) {
        require(canEnter(accountId, stageId)) { "STAGE_LOCKED" }
    }

    private fun canEnter(accountId: UUID, stageId: String): Boolean {
        if (stageId == "stage.01-01") return true
        val parts = stageId.removePrefix("stage.").split("-")
        require(parts.size == 2) { "INVALID_STAGE_ID" }
        val chapter = parts[0].toIntOrNull() ?: throw IllegalArgumentException("INVALID_STAGE_ID")
        val stage = parts[1].toIntOrNull() ?: throw IllegalArgumentException("INVALID_STAGE_ID")
        require(chapter in 1..StageId.MAX_CHAPTER && stage in 1..StageId.STAGES_PER_CHAPTER) { "INVALID_STAGE_ID" }
        val globalIndex = (chapter - 1) * 10 + stage
        val previousIndex = globalIndex - 1
        val previous = "stage.%02d-%02d".format((previousIndex - 1) / 10 + 1, (previousIndex - 1) % 10 + 1)
        return clearCount(accountId, previous) > 0
    }

    private fun nextStage(accountId: UUID, stageId: String, success: Boolean, contentVersion: String): String {
        if (!success) return stageId
        val parts = stageId.removePrefix("stage.").split("-")
        val globalIndex = (parts[0].toInt() - 1) * 10 + parts[1].toInt()
        if (globalIndex >= battles.lastPlayableIndex(contentVersion)) return stageId
        val next = "stage.%02d-%02d".format(globalIndex / 10 + 1, globalIndex % 10 + 1)
        return if (canEnter(accountId, next)) next else stageId
    }


    private fun updateCurrentStage(accountId: UUID, stageId: String) {
        val changed = jdbc.sql(
            """insert into account_runtime_state(account_id,current_stage_id) values (:account,:stage)
               on conflict(account_id) do update set current_stage_id=excluded.current_stage_id
               where account_runtime_state.current_stage_id is distinct from excluded.current_stage_id""",
        ).params(mapOf("account" to accountId, "stage" to stageId)).update()
        if (changed == 1) jdbc.sql("update account set state_version=state_version+1 where id=:account").param("account", accountId).update()
    }

    private fun closeActive(accountId: UUID, status: String) {
        jdbc.sql("update battle_session set status=:status,closed_at=:now where account_id=:account and status='ACTIVE'")
            .params(mapOf("status" to status, "now" to utc(clock.instant()), "account" to accountId))
            .update()
    }

    private fun expireSession(battleSessionId: UUID) {
        jdbc.sql("update battle_session set status='EXPIRED',closed_at=:now where id=:id and status='ACTIVE'")
            .params(mapOf("now" to utc(clock.instant()), "id" to battleSessionId))
            .update()
    }

    private fun clearCount(accountId: UUID, stageId: String): Long = jdbc.sql(
        "select count(*) from stage_progress where account_id=:account and stage_id=:stage and first_cleared_at is not null",
    ).params(mapOf("account" to accountId, "stage" to stageId)).query(Long::class.java).single()

    private fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId)
            .query(UUID::class.java)
            .optional()
            .orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }
    }

    private fun resultHash(result: CycleResult): String = sha256(mapper.writeValueAsString(result))
    private fun constantTimeEquals(left: String, right: String): Boolean = MessageDigest.isEqual(
        left.toByteArray(StandardCharsets.UTF_8),
        right.toByteArray(StandardCharsets.UTF_8),
    )
    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
    private fun utc(instant: Instant): OffsetDateTime = OffsetDateTime.ofInstant(instant, ZoneOffset.UTC)
    private fun publishBattleEvents(accountId: UUID, commandId: UUID, completion: Completion) {
        val chapter = completion.stageId.removePrefix("stage.").substringBefore("-").toIntOrNull()
        events.publish(
            eventType = "BATTLE_CYCLE_COMPLETED",
            aggregateId = accountId,
            occurredAt = clock.instant(),
            accountId = accountId,
            chapter = chapter,
            stageId = completion.stageId,
            quantity = completion.reward.rewards.sumOf { it.grantedQuantity },
            commandId = commandId,
            payload = mapOf(
                "stageId" to completion.stageId,
                "chapter" to chapter,
                "success" to completion.battle.success,
                "failureCode" to completion.battle.failureCode,
                "elapsedTicks" to completion.battle.elapsedTicks,
                "defeatedNormals" to completion.battle.defeatedNormals,
                "remainingHp" to completion.battle.remainingHp,
                "experienceGained" to completion.progression.experienceGained,
                "riceGained" to completion.progression.riceGained,
            ),
        )
    }


    private companion object {
        const val TICK_DURATION_MILLISECONDS = 100
        val SESSION_TIMEOUT: Duration = Duration.ofSeconds(90)
    }
}
