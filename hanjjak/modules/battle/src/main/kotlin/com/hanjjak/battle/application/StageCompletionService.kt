package com.hanjjak.battle.application

import com.hanjjak.battle.domain.FirstClearRewardResult
import com.hanjjak.events.application.DomainEventPublisher
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.time.Clock
import java.util.UUID

/** Owns the single state transition made when a stage is cleared. */
@Service
open class StageCompletionService(
    private val jdbc: JdbcClient,
    private val firstClearRewards: FirstClearRewardService,
    private val events: DomainEventPublisher,
    private val clock: Clock,
) {
    data class StageCompletionResult(
        val firstClear: Boolean,
        val firstClearReward: FirstClearRewardResult?,
    )

    @Transactional
    open fun complete(accountId: UUID, stageId: String, contentVersion: String, sourceId: UUID): StageCompletionResult {
        lockAccount(accountId)
        val alreadyCleared = jdbc.sql(
            "select first_cleared_at is not null from stage_progress where account_id=:account and stage_id=:stage",
        ).params(mapOf("account" to accountId, "stage" to stageId))
            .query { row, _ -> row.getBoolean(1) }
            .optional()
            .orElse(false)
        jdbc.sql(
            """insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version)
               values (:account,:stage,true,now(),1,:contentVersion)
               on conflict(account_id,stage_id) do update set
                 unlocked=true,
                 first_cleared_at=coalesce(stage_progress.first_cleared_at,excluded.first_cleared_at),
                 highest_clear_count=stage_progress.highest_clear_count+1,
                 content_version=excluded.content_version""",
        ).params(mapOf("account" to accountId, "stage" to stageId, "contentVersion" to contentVersion)).update()

        val firstClear = !alreadyCleared
        val reward = if (firstClear) firstClearRewards.apply(accountId, stageId, contentVersion, sourceId) else null
        if (stageId == "stage.01-05") {
            jdbc.sql(
                """update character set cosmetics_unlocked=true,
                   cosmetic_ticket_balance=case when cosmetics_unlocked then cosmetic_ticket_balance else cosmetic_ticket_balance+10 end
                   where account_id=:account""",
            ).param("account", accountId).update()
        }
        jdbc.sql("update account set state_version=state_version+1 where id=:account")
            .param("account", accountId).update()
        reward?.let { publishRewardEvent(accountId, sourceId, it) }
        return StageCompletionResult(firstClear, reward)
    }

    private fun publishRewardEvent(accountId: UUID, sourceId: UUID, reward: FirstClearRewardResult) {
        val chapter = reward.stageId.removePrefix("stage.").substringBefore("-").toIntOrNull()
        val items = reward.items.map { item -> mapOf("itemId" to item.itemId, "quantity" to item.quantity) }
        events.publish(
            eventType = "STAGE_FIRST_CLEAR_REWARD",
            aggregateId = accountId,
            occurredAt = clock.instant(),
            accountId = accountId,
            chapter = chapter,
            stageId = reward.stageId,
            commandId = sourceId,
            payload = mapOf(
                "stageId" to reward.stageId,
                "rewardId" to reward.rewardId,
                "rewardVersion" to reward.rewardVersion,
                "riceGranted" to reward.riceGranted,
                "grantedItems" to if (reward.itemStatus.name == "CLAIMED") items else emptyList<Map<String, Any>>(),
                "pendingItems" to if (reward.itemStatus.name == "PENDING") items else emptyList<Map<String, Any>>(),
                "unlockedSkillId" to reward.unlockedSkillId,
                "itemStatus" to reward.itemStatus.name,
                "requiredSlots" to reward.requiredSlots,
            ),
        )
    }

    private fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId)
            .query(UUID::class.java)
            .optional()
            .orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }
    }
}
