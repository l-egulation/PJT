package com.hanjjak.battle.application

import com.fasterxml.jackson.annotation.JsonInclude
import com.fasterxml.jackson.core.type.TypeReference
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublishRequest
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.inventory.application.BattleRewardService
import com.hanjjak.inventory.application.InventoryQueryService
import com.hanjjak.inventory.domain.ItemReward
import com.hanjjak.inventory.domain.RewardLine
import com.hanjjak.inventory.domain.RewardResult
import com.hanjjak.progression.application.ProgressionRewardService
import com.hanjjak.progression.domain.ProgressionGrant
import com.hanjjak.progression.domain.ProgressionRewardResult
import com.hanjjak.sim.CombatRenderingEvent
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.sql.Timestamp
import java.time.Clock
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Service
class BattleEnemySettlementService(
    private val mapper: ObjectMapper,
    private val rewards: BattleRewardService,
    private val inventory: InventoryQueryService,
    private val progression: ProgressionRewardService,
    private val progressionCatalog: com.hanjjak.progression.application.ProgressionRewardCatalog,
    private val events: DomainEventPublisher,
    private val jdbc: JdbcClient,
    private val clock: Clock,
) {
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class Settlement(
        val settlementId: UUID,
        val battleSessionId: UUID,
        val enemyIndex: Int,
        val boss: Boolean,
        val settledAt: Instant,
        val reward: RewardResult,
        val progression: ProgressionRewardResult,
    )

    data class Summary(
        val settlements: List<Settlement>,
        val reward: RewardResult,
        val progression: ProgressionRewardResult,
    )

    private data class StoredSettlement(
        val settlementId: UUID,
        val battleSessionId: UUID,
        val enemyIndex: Int,
        val boss: Boolean,
        val eligibleTick: Int,
        val eligibleAt: Instant,
        val rewardContentVersion: String,
        val requestedRewards: List<ItemReward>,
        val experience: Long,
        val rice: Long,
        val settledAt: Instant?,
        val rewardResult: RewardResult?,
        val progressionResult: ProgressionRewardResult?,
    )

    fun prepare(
        accountId: UUID,
        battleSessionId: UUID,
        stageId: String,
        seed: Long,
        startedAt: Instant,
        tickDurationMilliseconds: Int,
        timeline: List<CombatRenderingEvent>,
        balanceVersion: String = "enemy-v1-applied",
    ) {
        val defeated = timeline.filter { it.type == "ENEMY_DEFEATED" }
        if (defeated.isEmpty()) return
        val parameters = mutableMapOf<String, Any>()
        val values = defeated.mapIndexed { rowIndex, event ->
            val enemyIndex = requireNotNull(event.enemyIndex) { "DEFEATED_ENEMY_INDEX_REQUIRED" }
            val boss = event.boss
            val prepared = rewards.prepare(accountId, stageId, seed, enemyIndex, boss, balanceVersion)
            val grant = progressionCatalog.grant(balanceVersion, stageId, boss)
            val settlementId = UUID.nameUUIDFromBytes("$battleSessionId:$enemyIndex".toByteArray(Charsets.UTF_8))
            parameters["settlement$rowIndex"] = settlementId
            parameters["battle$rowIndex"] = battleSessionId
            parameters["enemy$rowIndex"] = enemyIndex
            parameters["boss$rowIndex"] = boss
            parameters["tick$rowIndex"] = event.logicalTick
            parameters["eligible$rowIndex"] = Timestamp.from(startedAt.plusMillis(event.logicalTick * tickDurationMilliseconds.toLong()))
            parameters["version$rowIndex"] = prepared.contentVersion
            parameters["rewards$rowIndex"] = mapper.writeValueAsString(prepared.rewards)
            parameters["experience$rowIndex"] = grant.experience
            parameters["rice$rowIndex"] = grant.rice
            "(cast(:settlement$rowIndex as uuid),cast(:battle$rowIndex as uuid),:enemy$rowIndex,:boss$rowIndex,:tick$rowIndex,cast(:eligible$rowIndex as timestamptz),cast(:version$rowIndex as varchar),cast(:rewards$rowIndex as jsonb),:experience$rowIndex,:rice$rowIndex)"
        }.joinToString(",")
        jdbc.sql(
            """insert into battle_enemy_settlement(
                settlement_id,battle_session_id,enemy_index,boss,eligible_tick,eligible_at,reward_content_version,
                requested_reward_json,experience,rice
            ) values $values""",
        ).params(parameters).update()
    }

    @Transactional
    fun settleThrough(
        accountId: UUID,
        battleSessionId: UUID,
        stageId: String,
        throughEnemyIndex: Int,
        confirmedLogicalTick: Int? = null,
        enforceEligibility: Boolean = true,
    ): Summary {
        require(throughEnemyIndex in 0..21) { "INVALID_ENEMY_INDEX" }
        if (throughEnemyIndex == 0) return summary(accountId, battleSessionId)
        val requested = row(battleSessionId, throughEnemyIndex)
            ?: throw IllegalArgumentException("BATTLE_ENEMY_NOT_DEFEATED")
        require(confirmedLogicalTick == null || requested.eligibleTick <= confirmedLogicalTick) { "INVALID_RENDERING_CHECKPOINT" }
        require(!enforceEligibility || !clock.instant().isBefore(requested.eligibleAt)) { "BATTLE_SETTLEMENT_NOT_READY" }

        val pending = rows(battleSessionId, throughEnemyIndex, lock = true)
            .filter { it.settledAt == null }
        val now = clock.instant()
        require(!enforceEligibility || pending.none { now.isBefore(it.eligibleAt) }) { "BATTLE_SETTLEMENT_NOT_READY" }
        require(confirmedLogicalTick == null || pending.none { it.eligibleTick > confirmedLogicalTick }) { "INVALID_RENDERING_CHECKPOINT" }
        val eventRequests = mutableListOf<DomainEventPublishRequest>()
        for (entry in pending) {
            val reward = rewards.grantPrepared(
                accountId,
                entry.settlementId,
                "battle:$battleSessionId:${entry.enemyIndex}:${entry.boss}:${entry.rewardContentVersion}",
                entry.requestedRewards,
            ).result
            val progressionResult = progression.grantEnemy(
                accountId,
                entry.settlementId,
                stageId,
                entry.enemyIndex,
                entry.boss,
                ProgressionGrant(entry.experience, entry.rice),
            ).result
            jdbc.sql(
                """update battle_enemy_settlement
                   set settled_at=:settled,reward_result_json=cast(:reward as jsonb),progression_result_json=cast(:progression as jsonb)
                   where settlement_id=:settlement and settled_at is null""",
            ).params(
                mapOf(
                    "settled" to utc(now),
                    "reward" to mapper.writeValueAsString(reward),
                    "progression" to mapper.writeValueAsString(progressionResult),
                    "settlement" to entry.settlementId,
                ),
            ).update()
            val chapter = stageId.removePrefix("stage.").substringBefore("-").toInt()
            eventRequests += DomainEventPublishRequest(
                eventType = "BATTLE_ENEMY_SETTLED",
                aggregateId = accountId,
                occurredAt = now,
                accountId = accountId,
                chapter = chapter,
                stageId = stageId,
                quantity = reward.rewards.sumOf { it.grantedQuantity },
                commandId = entry.settlementId,
                correlationId = battleSessionId,
                payload = mapOf(
                    "settlementId" to entry.settlementId,
                    "battleSessionId" to battleSessionId,
                    "stageId" to stageId,
                    "chapter" to chapter,
                    "enemyIndex" to entry.enemyIndex,
                    "boss" to entry.boss,
                    "experienceGained" to progressionResult.experienceGained,
                    "riceGained" to progressionResult.riceGained,
                ),
            )
            reward.rewards.filter { it.grantedQuantity > 0 }.forEach { line ->
                eventRequests += DomainEventPublishRequest(
                    eventType = "ITEM_DROPPED",
                    aggregateId = accountId,
                    occurredAt = now,
                    accountId = accountId,
                    chapter = chapter,
                    stageId = stageId,
                    itemId = line.itemId,
                    quantity = line.grantedQuantity,
                    commandId = entry.settlementId,
                    correlationId = battleSessionId,
                    payload = mapOf(
                        "itemId" to line.itemId,
                        "quantity" to line.grantedQuantity,
                        "sourceType" to "BATTLE_ENEMY",
                        "sourceId" to entry.settlementId,
                        "stageId" to stageId,
                        "chapter" to chapter,
                    ),
                )
            }
        }
        events.publishAll(eventRequests)
        return summary(accountId, battleSessionId)
    }

    fun summary(accountId: UUID, battleSessionId: UUID): Summary {
        val settlements = rows(battleSessionId, 21, lock = false)
            .filter { it.settledAt != null }
            .map { entry ->
                Settlement(
                    entry.settlementId,
                    entry.battleSessionId,
                    entry.enemyIndex,
                    entry.boss,
                    requireNotNull(entry.settledAt),
                    requireNotNull(entry.rewardResult),
                    requireNotNull(entry.progressionResult),
                )
            }
        val reward = aggregateRewards(accountId, settlements)
        val progressionResult = aggregateProgression(accountId, settlements)
        return Summary(settlements, reward, progressionResult)
    }

    private fun aggregateRewards(accountId: UUID, settlements: List<Settlement>): RewardResult {
        val status = inventory.status(accountId)
        val lines = settlements.flatMap { it.reward.rewards }
            .groupBy { it.itemId }
            .map { (itemId, rewards) ->
                RewardLine(
                    itemId,
                    rewards.sumOf { it.requestedQuantity },
                    rewards.sumOf { it.grantedQuantity },
                    rewards.sumOf { it.discardedQuantity },
                    rewards.sumOf { it.skippedQuantity },
                )
            }
            .sortedBy { it.itemId }
        return RewardResult(lines, status.usedSlots, status.maxSlots, status.isFull)
    }

    private fun aggregateProgression(accountId: UUID, settlements: List<Settlement>): ProgressionRewardResult {
        if (settlements.isEmpty()) return progression.current(accountId)
        val first = settlements.first().progression
        val last = settlements.last().progression
        return ProgressionRewardResult(
            experienceGained = settlements.sumOf { it.progression.experienceGained },
            riceGained = settlements.sumOf { it.progression.riceGained },
            levelBefore = first.levelBefore,
            levelAfter = last.levelAfter,
            experienceBefore = first.experienceBefore,
            experienceAfter = last.experienceAfter,
            experienceToNextLevel = last.experienceToNextLevel,
            riceBalance = last.riceBalance,
        )
    }

    private fun row(battleSessionId: UUID, enemyIndex: Int): StoredSettlement? = jdbc.sql(
        """select settlement_id,battle_session_id,enemy_index,boss,eligible_tick,eligible_at,reward_content_version,
           requested_reward_json::text,experience,rice,settled_at,reward_result_json::text,progression_result_json::text
           from battle_enemy_settlement where battle_session_id=:battle and enemy_index=:enemy""",
    ).params(mapOf("battle" to battleSessionId, "enemy" to enemyIndex)).query(::mapRow).optional().orElse(null)

    private fun rows(battleSessionId: UUID, throughEnemyIndex: Int, lock: Boolean): List<StoredSettlement> = jdbc.sql(
        """select settlement_id,battle_session_id,enemy_index,boss,eligible_tick,eligible_at,reward_content_version,
           requested_reward_json::text,experience,rice,settled_at,reward_result_json::text,progression_result_json::text
           from battle_enemy_settlement where battle_session_id=:battle and enemy_index<=:enemy
           order by enemy_index${if (lock) " for update" else ""}""",
    ).params(mapOf("battle" to battleSessionId, "enemy" to throughEnemyIndex)).query(::mapRow).list()

    private fun mapRow(row: java.sql.ResultSet, ignored: Int): StoredSettlement = StoredSettlement(
        settlementId = row.getObject("settlement_id", UUID::class.java),
        battleSessionId = row.getObject("battle_session_id", UUID::class.java),
        enemyIndex = row.getInt("enemy_index"),
        boss = row.getBoolean("boss"),
        eligibleTick = row.getInt("eligible_tick"),
        eligibleAt = row.getObject("eligible_at", OffsetDateTime::class.java).toInstant(),
        rewardContentVersion = row.getString("reward_content_version"),
        requestedRewards = mapper.readValue(row.getString("requested_reward_json"), object : TypeReference<List<ItemReward>>() {}),
        experience = row.getLong("experience"),
        rice = row.getLong("rice"),
        settledAt = row.getObject("settled_at", OffsetDateTime::class.java)?.toInstant(),
        rewardResult = row.getString("reward_result_json")?.let { mapper.readValue(it, RewardResult::class.java) },
        progressionResult = row.getString("progression_result_json")?.let { mapper.readValue(it, ProgressionRewardResult::class.java) },
    )

    private fun utc(instant: Instant): OffsetDateTime = OffsetDateTime.ofInstant(instant, ZoneOffset.UTC)
}
