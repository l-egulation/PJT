package com.hanjjak.battle.api
import com.fasterxml.jackson.annotation.JsonInclude
import com.fasterxml.jackson.databind.ObjectMapper

import com.hanjjak.account.application.MaterialPreferenceService
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.battle.application.BattleStatsProvider
import com.hanjjak.battle.application.BattleSkillProvider
import com.hanjjak.battle.application.FirstClearRewardPresenter
import com.hanjjak.battle.application.FirstClearRewardView
import com.hanjjak.battle.application.StageBattleService
import com.hanjjak.battle.application.StageCompletionService
import com.hanjjak.inventory.application.BattleRewardService
import com.hanjjak.inventory.domain.RewardLine
import com.hanjjak.inventory.domain.RewardResult
import com.hanjjak.progression.application.ProgressionRewardService
import com.hanjjak.progression.application.ProgressionRewardCatalog
import com.hanjjak.progression.domain.ProgressionRewardResult
import com.hanjjak.sim.CycleResult
import com.hanjjak.events.application.DomainEventPublishRequest
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.*
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/battles")
class BattleController(
    private val mapper: ObjectMapper,
    private val battles: StageBattleService,
    private val rewards: BattleRewardService,
    private val materialPreferences: MaterialPreferenceService,
    private val progression: ProgressionRewardService,
    private val progressionCatalog: ProgressionRewardCatalog,
    private val inventoryQueries: com.hanjjak.inventory.application.InventoryQueryService,
    private val stats: BattleStatsProvider,
    private val skills: BattleSkillProvider,
    private val gameSessions: com.hanjjak.battle.application.GameSessionService,
    private val events: DomainEventPublisher,
    private val jdbc: JdbcClient,
    private val clock: Clock,
    private val balanceVersions: com.hanjjak.account.application.BalanceVersionService,
    private val stageCompletion: StageCompletionService,
    private val firstClearRewardPresenter: FirstClearRewardPresenter,
) {
    data class StartRequest(val stageId: String)
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class BattleCycleResponse(val battle: CycleResult, val reward: RewardResult, val progression: ProgressionRewardResult, val firstClearReward: FirstClearRewardView? = null)
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class StageRunResponse(val stageId: String, val battle: CycleResult, val reward: RewardResult, val progression: ProgressionRewardResult, val firstClearReward: FirstClearRewardView? = null)
    data class ChapterProgressionSummary(
        val experienceGained: Long,
        val riceGained: Long,
        val levelBefore: Int,
        val levelAfter: Int,
        val experienceAfter: Long,
        val experienceToNextLevel: Long,
        val riceBalance: Long,
    )
    data class ChapterAutoRunResponse(
        val chapter: Int,
        val runs: List<StageRunResponse>,
        val stoppedReason: String,
        val stoppedStageId: String?,
        val rewards: List<RewardLine>,
        val progression: ChapterProgressionSummary?,
        val finalInventory: InventorySnapshot?,
    )
    data class InventorySnapshot(val usedSlots: Int, val maxSlots: Int, val isFull: Boolean)
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)
    private data class StoredBattleCommand(val fingerprint: String, val resultJson: String)

    @PostMapping("/cycles")
    @Transactional
    fun run(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestHeader("X-Game-Session-Id") gameSessionId: UUID,
        @RequestBody request: StartRequest,
        session: HttpSession,
    ): Envelope<BattleCycleResponse> {
        val accountId = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        lockAccount(accountId)
        gameSessions.requireActive(accountId, gameSessionId)
        materialPreferences.requireSelected(accountId)
        val fingerprint = sha256("cycle\u0000${request.stageId}")
        battleCommand(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), mapper.readValue(existing.resultJson, BattleCycleResponse::class.java))
        }
        requireStageAccess(accountId, request.stageId)
        updateCurrentStage(accountId, request.stageId)
        val seed = idempotencyKey.mostSignificantBits xor idempotencyKey.leastSignificantBits
        val balanceVersion = balanceVersions.current(accountId)
        val result = battles.run(request.stageId, stats.forAccount(accountId), seed, balanceVersion, skills.forAccount(accountId))
        val settlementKey = stageSettlementKey(idempotencyKey, request.stageId)
        val settled = settleImmediateBattle(accountId, settlementKey, request.stageId, seed, result, balanceVersion)
        val reward = settled.first
        val progressionReward = settled.second
        val firstClearReward = if (result.success) {
            stageCompletion.complete(accountId, request.stageId, balanceVersion, settlementKey).firstClearReward
                ?.let(firstClearRewardPresenter::present)
        } else {
            null
        }
        val response = BattleCycleResponse(result, reward, progressionReward, firstClearReward)
        val commandId = saveBattleCommand(accountId, idempotencyKey, fingerprint, response)
        publishBattleEvents(accountId, commandId, idempotencyKey, listOf(StageRunResponse(request.stageId, result, reward, progressionReward, firstClearReward)), balanceVersion)
        return Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), response)
    }

    @PostMapping("/chapters/{chapter}/auto-run")
    @Transactional
    fun runChapter(
        @PathVariable chapter: Int,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestHeader("X-Game-Session-Id") gameSessionId: UUID,
        session: HttpSession,
    ): Envelope<ChapterAutoRunResponse> {
        require(chapter in 1..10) { "INVALID_CHAPTER" }
        val accountId = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        lockAccount(accountId)
        gameSessions.requireActive(accountId, gameSessionId)
        val fingerprint = sha256("chapter\u0000$chapter")
        battleCommand(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), mapper.readValue(existing.resultJson, ChapterAutoRunResponse::class.java))
        }
        materialPreferences.requireSelected(accountId)
        val balanceVersion = balanceVersions.current(accountId)
        val chapterStages = (1..10).map { number -> "stage.%02d-%02d".format(chapter, number) }
        val firstTarget = chapterStages.firstOrNull { canEnter(accountId, it) && clearCount(accountId, it) == 0L }
            ?: chapterStages.firstOrNull { canEnter(accountId, it) }
        val runs = mutableListOf<StageRunResponse>()
        if (firstTarget == null) {
            val response = ChapterAutoRunResponse(chapter, runs, "NO_STAGE", null, emptyList(), null, null)
            val commandId = saveBattleCommand(accountId, idempotencyKey, fingerprint, response)
            return Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), response)
        }
        for (stageId in chapterStages.dropWhile { it != firstTarget }) {
            if (!canEnter(accountId, stageId)) {
                val lastInventory = runs.lastOrNull()?.reward?.let { InventorySnapshot(it.usedSlots, it.maxSlots, it.isFull) }
                val response = ChapterAutoRunResponse(chapter, runs, "STAGE_LOCKED", stageId, aggregate(runs), aggregateProgression(runs), lastInventory)
                val commandId = saveBattleCommand(accountId, idempotencyKey, fingerprint, response)
                publishBattleEvents(accountId, commandId, idempotencyKey, runs, balanceVersion)
                return Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), response)
            }
            updateCurrentStage(accountId, stageId)
            val stageKey = UUID.nameUUIDFromBytes("$idempotencyKey:$stageId".toByteArray(StandardCharsets.UTF_8))
            val seed = stageKey.mostSignificantBits xor stageKey.leastSignificantBits
            val result = battles.run(stageId, stats.forAccount(accountId), seed, balanceVersion, skills.forAccount(accountId))
            val settled = settleImmediateBattle(accountId, stageKey, stageId, seed, result, balanceVersion)
            val reward = settled.first
            val progressionReward = settled.second
            val firstClearReward = if (result.success) {
                stageCompletion.complete(accountId, stageId, balanceVersion, stageKey).firstClearReward
                    ?.let(firstClearRewardPresenter::present)
            } else {
                null
            }
            runs += StageRunResponse(stageId, result, reward, progressionReward, firstClearReward)
            if (!result.success) {
                val response = ChapterAutoRunResponse(chapter, runs, "BATTLE_FAILED", stageId, aggregate(runs), aggregateProgression(runs), InventorySnapshot(reward.usedSlots, reward.maxSlots, reward.isFull))
                val commandId = saveBattleCommand(accountId, idempotencyKey, fingerprint, response)
                publishBattleEvents(accountId, commandId, idempotencyKey, runs, balanceVersion)
                return Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), response)
            }
        }
        val finalInventory = runs.lastOrNull()?.reward?.let { InventorySnapshot(it.usedSlots, it.maxSlots, it.isFull) }
        val response = ChapterAutoRunResponse(chapter, runs, "CHAPTER_COMPLETE", "stage.%02d-10".format(chapter), aggregate(runs), aggregateProgression(runs), finalInventory)
        val commandId = saveBattleCommand(accountId, idempotencyKey, fingerprint, response)
        publishBattleEvents(accountId, commandId, idempotencyKey, runs, balanceVersion)
        return Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), response)
    }

    // Serialize growth mutations before reading the paired fighter and skill inputs.
    private fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId).query(UUID::class.java).single()
    }

    private fun requireStageAccess(accountId: UUID, stageId: String) {
        require(canEnter(accountId, stageId)) { "STAGE_LOCKED" }
    }

    private fun canEnter(accountId: UUID, stageId: String): Boolean {
        if (stageId == "stage.01-01") return true
        val parts = stageId.removePrefix("stage.").split("-")
        val index = (parts[0].toInt() - 1) * 10 + parts[1].toInt()
        val previousIndex = index - 1
        val previous = "stage.%02d-%02d".format((previousIndex - 1) / 10 + 1, (previousIndex - 1) % 10 + 1)
        return clearCount(accountId, previous) > 0
    }

    private fun clearCount(accountId: UUID, stageId: String): Long = jdbc.sql("select count(*) from stage_progress where account_id=:account and stage_id=:stage and first_cleared_at is not null")
        .param("account", accountId)
        .param("stage", stageId)
        .query(Long::class.java)
        .single()

    private fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId)
        .query(Long::class.java)
        .single()

    private fun battleCommand(accountId: UUID, idempotencyKey: UUID): StoredBattleCommand? = jdbc.sql("select fingerprint,result_json::text from battle_command_record where account_id=:account and idempotency_key=:key")
        .param("account", accountId)
        .param("key", idempotencyKey)
        .query { row, _ -> StoredBattleCommand(row.getString("fingerprint"), row.getString("result_json")) }
        .optional()
        .orElse(null)

    private fun saveBattleCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, result: Any): UUID {
        val commandId = UUID.randomUUID()
        jdbc.sql("insert into battle_command_record(command_id,account_id,idempotency_key,fingerprint,result_json) values (:command,:account,:key,:fingerprint,cast(:result as jsonb))")
            .params(mapOf("command" to commandId, "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint, "result" to mapper.writeValueAsString(result)))
            .update()
        return commandId
    }

    private fun settleImmediateBattle(
        accountId: UUID,
        commandId: UUID,
        stageId: String,
        seed: Long,
        result: CycleResult,
        balanceVersion: String,
    ): Pair<RewardResult, ProgressionRewardResult> {
        val rewardLines = mutableListOf<RewardLine>()
        val progressionLines = mutableListOf<ProgressionRewardResult>()
        val enemyIndexes = (1..result.defeatedNormals).toList() + if (result.success) listOf(21) else emptyList()
        val eventRequests = mutableListOf<DomainEventPublishRequest>()
        for (enemyIndex in enemyIndexes) {
            val boss = enemyIndex == 21
            val settlementId = UUID.nameUUIDFromBytes("$commandId:$enemyIndex".toByteArray(StandardCharsets.UTF_8))
            val reward = rewards.grantEnemy(accountId, settlementId, stageId, seed, enemyIndex, boss, balanceVersion).result
            val progressionResult = progression.grantEnemy(accountId, settlementId, stageId, enemyIndex, boss, progressionCatalog.grant(balanceVersion, stageId, boss)).result
            rewardLines += reward.rewards
            progressionLines += progressionResult
            val chapter = stageId.removePrefix("stage.").substringBefore("-").toInt()
            reward.rewards.filter { it.grantedQuantity > 0 }.forEach { line ->
                eventRequests += DomainEventPublishRequest(
                    eventType = "ITEM_DROPPED",
                    aggregateId = accountId,
                    occurredAt = clock.instant(),
                    accountId = accountId,
                    chapter = chapter,
                    stageId = stageId,
                    itemId = line.itemId,
                    quantity = line.grantedQuantity,
                    commandId = settlementId,
                    correlationId = commandId,
                    payload = mapOf(
                        "itemId" to line.itemId,
                        "quantity" to line.grantedQuantity,
                        "sourceType" to "BATTLE_ENEMY",
                        "sourceId" to settlementId,
                        "stageId" to stageId,
                        "chapter" to chapter,
                    ),
                )
            }
        }
        events.publishAll(eventRequests)
        val status = inventoryQueries.status(accountId)
        val reward = RewardResult(
            rewardLines.groupBy { it.itemId }.map { (itemId, lines) ->
                RewardLine(itemId, lines.sumOf { it.requestedQuantity }, lines.sumOf { it.grantedQuantity }, lines.sumOf { it.discardedQuantity }, lines.sumOf { it.skippedQuantity })
            }.sortedBy { it.itemId },
            status.usedSlots,
            status.maxSlots,
            status.isFull,
        )
        val current = progression.current(accountId)
        val progressionResult = if (progressionLines.isEmpty()) current else current.copy(
            experienceGained = progressionLines.sumOf { it.experienceGained },
            riceGained = progressionLines.sumOf { it.riceGained },
            levelBefore = progressionLines.first().levelBefore,
            experienceBefore = progressionLines.first().experienceBefore,
        )
        return reward to progressionResult
    }

    private fun publishBattleEvents(accountId: UUID, commandId: UUID, settlementRootKey: UUID, runs: List<StageRunResponse>, balanceVersion: String) {
        val now = clock.instant()
        runs.forEach { run ->
            val chapter = run.stageId.removePrefix("stage.").substringBefore("-").toIntOrNull()
            events.publish(
                eventType = "BATTLE_CYCLE_COMPLETED",
                aggregateId = accountId,
                occurredAt = now,
                accountId = accountId,
                chapter = chapter,
                stageId = run.stageId,
                quantity = run.reward.rewards.sumOf { it.grantedQuantity },
                commandId = commandId,
                payload = mapOf(
                    "stageId" to run.stageId,
                    "chapter" to chapter,
                    "success" to run.battle.success,
                    "failureCode" to run.battle.failureCode,
                    "elapsedTicks" to run.battle.elapsedTicks,
                    "defeatedNormals" to run.battle.defeatedNormals,
                    "remainingHp" to run.battle.remainingHp,
                    "experienceGained" to run.progression.experienceGained,
                    "riceGained" to run.progression.riceGained,
                ),
            )
            val enemyIndexes = (1..run.battle.defeatedNormals).toList() + if (run.battle.success) listOf(21) else emptyList()
            enemyIndexes.forEach { enemyIndex ->
                val boss = enemyIndex == 21
                val settlementId = UUID.nameUUIDFromBytes("${stageSettlementKey(settlementRootKey, run.stageId)}:$enemyIndex".toByteArray(StandardCharsets.UTF_8))
                val grant = progressionCatalog.grant(balanceVersion, run.stageId, boss)
                events.publish(
                    eventType = "BATTLE_ENEMY_SETTLED",
                    aggregateId = accountId,
                    occurredAt = now,
                    accountId = accountId,
                    chapter = chapter,
                    stageId = run.stageId,
                    commandId = settlementId,
                    correlationId = commandId,
                    payload = mapOf(
                        "settlementId" to settlementId,
                        "battleSessionId" to commandId,
                        "stageId" to run.stageId,
                        "chapter" to chapter,
                        "enemyIndex" to enemyIndex,
                        "boss" to boss,
                        "experienceGained" to grant.experience,
                        "riceGained" to grant.rice,
                    ),
                )
            }
        }
    }

    private fun stageSettlementKey(rootKey: UUID, stageId: String): UUID =
        UUID.nameUUIDFromBytes("$rootKey:$stageId".toByteArray(StandardCharsets.UTF_8))

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }

    private fun aggregate(runs: List<StageRunResponse>): List<RewardLine> = runs
        .flatMap { it.reward.rewards }
        .groupBy { it.itemId }
        .map { (itemId, lines) ->
            RewardLine(
                itemId,
                lines.sumOf { it.requestedQuantity },
                lines.sumOf { it.grantedQuantity },
                lines.sumOf { it.discardedQuantity },
                lines.sumOf { it.skippedQuantity },
            )
        }
        .sortedBy { it.itemId }

    private fun aggregateProgression(runs: List<StageRunResponse>): ChapterProgressionSummary? {
        if (runs.isEmpty()) return null
        val first = runs.first().progression
        val last = runs.last().progression
        return ChapterProgressionSummary(
            experienceGained = runs.sumOf { it.progression.experienceGained },
            riceGained = runs.sumOf { it.progression.riceGained },
            levelBefore = first.levelBefore,
            levelAfter = last.levelAfter,
            experienceAfter = last.experienceAfter,
            experienceToNextLevel = last.experienceToNextLevel,
            riceBalance = last.riceBalance,
        )
    }

    private fun updateCurrentStage(accountId: UUID, stageId: String) {
        val changed = jdbc.sql(
            """insert into account_runtime_state(account_id,current_stage_id) values (:account,:stage)
               on conflict(account_id) do update set current_stage_id=excluded.current_stage_id
               where account_runtime_state.current_stage_id is distinct from excluded.current_stage_id""",
        ).params(mapOf("account" to accountId, "stage" to stageId)).update()
        if (changed == 1) {
            jdbc.sql("update account set state_version=state_version+1 where id=:account")
                .param("account", accountId)
                .update()
        }
    }

}
