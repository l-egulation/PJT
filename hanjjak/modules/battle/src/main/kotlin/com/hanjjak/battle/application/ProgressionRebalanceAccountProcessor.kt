package com.hanjjak.battle.application

import com.fasterxml.jackson.core.type.TypeReference
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.domain.FirstClearItemStatus
import com.hanjjak.battle.domain.FirstClearRewardResult
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.inventory.domain.ItemReward
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Propagation
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.time.Clock
import java.time.OffsetDateTime
import java.util.UUID

/** Per-account transaction boundary for the progression-rebalance backfill. */
@Service
open class ProgressionRebalanceAccountProcessor(
    private val jdbc: JdbcClient,
    private val firstClearRewards: FirstClearRewardService,
    private val catalog: com.hanjjak.progression.application.ProgressionRewardCatalog,
    private val mapper: ObjectMapper,
    private val events: DomainEventPublisher,
    private val clock: Clock = Clock.systemUTC(),
) {
    @Transactional(propagation = Propagation.REQUIRES_NEW)
    open fun applyOne(accountId: UUID): AccountBackfillResult {
        jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId).query(UUID::class.java).single()
        val state = jdbc.sql("select balance_version,retroactive_completed_at from account_balance_state where account_id=:account")
            .param("account", accountId)
            .query { row, _ -> row.getString("balance_version") to row.getObject("retroactive_completed_at") }
            .optional().orElseThrow { IllegalArgumentException("BALANCE_VERSION_MISSING") }
        if (state.first == TARGET_VERSION && state.second != null) {
            return AccountBackfillResult(accountId, 0, 0, 0, emptyMap(), 0, 0, false)
        }
        val activeLegacySession = jdbc.sql(
            "select exists(select 1 from battle_session where account_id=:account and status='ACTIVE' and content_version=:legacy)",
        ).params(mapOf("account" to accountId, "legacy" to LEGACY_VERSION)).query(Boolean::class.java).single()
        require(!activeLegacySession) { LEGACY_SESSION_ACTIVE }
        require(state.first == LEGACY_VERSION || state.first == TARGET_VERSION) { "BALANCE_VERSION_NOT_ELIGIBLE" }

        val clearedStages = jdbc.sql(
            "select stage_id from stage_progress where account_id=:account and first_cleared_at is not null order by stage_id",
        ).param("account", accountId).query(String::class.java).list()
        var alreadyApplied = 0
        var riceTotal = 0L
        val itemQuantities = linkedMapOf<String, Long>()
        var immediateCandidates = 0
        var pendingCandidates = 0
        clearedStages.forEach { stageId ->
            if (catalog.firstClearOrNull(TARGET_VERSION, stageId) == null) return@forEach
            val applied = firstClearRewards.apply(accountId, stageId, TARGET_VERSION, sourceId(accountId, stageId))
            val row = applied?.let { null } ?: firstClearRewardsRow(accountId, stageId)
                ?: throw IllegalStateException("FIRST_CLEAR_REWARD_INSERT_FAILED")
            val rice: Long
            val items: List<ItemReward>
            val status: String
            if (applied != null) {
                rice = applied.riceGranted
                items = applied.items
                status = applied.itemStatus.name
                publishRewardEvent(accountId, applied)
            } else {
                alreadyApplied++
                rice = row.riceGranted
                items = row.items
                status = row.itemStatus
            }
            riceTotal = Math.addExact(riceTotal, rice)
            items.forEach { item ->
                itemQuantities[item.itemId] = Math.addExact(itemQuantities[item.itemId] ?: 0L, item.quantity)
            }
            if (status == FirstClearItemStatus.PENDING.name) pendingCandidates++ else immediateCandidates++
        }

        val switched = jdbc.sql(
            "update account_balance_state set balance_version=:version, applied_at=now(), retroactive_completed_at=now(), " +
                "provisioned_during_rollout=false " +
                "where account_id=:account and balance_version in (:legacy,:version) and retroactive_completed_at is null",
        ).params(mapOf("version" to TARGET_VERSION, "account" to accountId, "legacy" to LEGACY_VERSION)).update() == 1
        if (switched) {
            jdbc.sql("update account set state_version=state_version+1 where id=:account")
                .param("account", accountId).update()
        }
        return AccountBackfillResult(accountId, clearedStages.size, alreadyApplied, riceTotal, itemQuantities, immediateCandidates, pendingCandidates, switched)
    }

    private fun firstClearRewardsRow(accountId: UUID, stageId: String): FirstClearRewardRepository.RewardRow? =
        jdbc.sql(
            "select reward_id,account_id,stage_id,reward_version,source_id,rice_granted,unlocked_skill_id,item_status,items_json::text,required_slots,claimed_at " +
                "from stage_first_clear_reward where account_id=:account and stage_id=:stage and reward_version=:version",
        ).params(mapOf("account" to accountId, "stage" to stageId, "version" to TARGET_VERSION))
            .query { row, _ ->
                FirstClearRewardRepository.RewardRow(
                    row.getObject("reward_id", UUID::class.java), row.getObject("account_id", UUID::class.java),
                    row.getString("stage_id"), row.getString("reward_version"), row.getObject("source_id", UUID::class.java),
                    row.getLong("rice_granted"), row.getString("unlocked_skill_id"), row.getString("item_status"),
                    mapper.readValue(row.getString("items_json"), object : TypeReference<List<ItemReward>>() {}),
                    row.getInt("required_slots"), row.getObject("claimed_at", OffsetDateTime::class.java)?.toInstant(),
                )
            }.optional().orElse(null)

    private fun publishRewardEvent(accountId: UUID, reward: FirstClearRewardResult) {
        val chapter = reward.stageId.removePrefix("stage.").substringBefore("-").toIntOrNull()
        val items = reward.items.map { item -> mapOf("itemId" to item.itemId, "quantity" to item.quantity) }
        events.publish(
            eventType = "STAGE_FIRST_CLEAR_REWARD",
            aggregateId = accountId,
            occurredAt = clock.instant(),
            accountId = accountId,
            chapter = chapter,
            stageId = reward.stageId,
            commandId = reward.sourceId,
            payload = mapOf(
                "stageId" to reward.stageId,
                "rewardId" to reward.rewardId,
                "rewardVersion" to reward.rewardVersion,
                "riceGranted" to reward.riceGranted,
                "grantedItems" to if (reward.itemStatus == FirstClearItemStatus.CLAIMED) items else emptyList<Map<String, Any>>(),
                "pendingItems" to if (reward.itemStatus == FirstClearItemStatus.PENDING) items else emptyList<Map<String, Any>>(),
                "unlockedSkillId" to reward.unlockedSkillId,
                "itemStatus" to reward.itemStatus.name,
                "requiredSlots" to reward.requiredSlots,
            ),
        )
    }

    private fun sourceId(accountId: UUID, stageId: String): UUID = UUID.nameUUIDFromBytes(
        "progression-rebalance-v1\u0000$accountId\u0000$stageId".toByteArray(StandardCharsets.UTF_8),
    )

    companion object {
        const val TARGET_VERSION = "progression-rebalance-v1"
        const val LEGACY_VERSION = "enemy-v1-applied"
        const val LEGACY_SESSION_ACTIVE = "LEGACY_BATTLE_SESSION_ACTIVE"
    }
}

data class AccountBackfillResult(
    val accountId: UUID,
    val targetStages: Int,
    val alreadyApplied: Int,
    val riceTotal: Long,
    val quantitiesByItem: Map<String, Long>,
    val immediateCandidates: Int,
    val pendingCandidates: Int,
    val switched: Boolean,
)