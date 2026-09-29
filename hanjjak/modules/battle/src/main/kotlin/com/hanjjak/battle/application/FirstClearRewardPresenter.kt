package com.hanjjak.battle.application

import com.hanjjak.battle.domain.FirstClearItemStatus
import com.hanjjak.battle.domain.FirstClearRewardResult
import com.hanjjak.battle.domain.PendingFirstClearReward
import com.hanjjak.inventory.application.InventoryQueryService
import com.hanjjak.inventory.application.ItemCatalog
import org.springframework.stereotype.Component
import java.util.UUID

data class FirstClearRewardItemView(
    val itemId: String,
    val displayName: String,
    val quantity: Long,
)

data class FirstClearRewardView(
    val rewardId: UUID,
    val stageId: String,
    val rewardVersion: String,
    val firstClear: Boolean,
    val riceGranted: Long,
    val grantedItems: List<FirstClearRewardItemView>,
    val pendingItems: List<FirstClearRewardItemView>,
    val unlockedSkillId: String?,
    val itemStatus: String,
    val requiredSlots: Int,
    val availableSlots: Int,
    val missingSlots: Int,
)

@Component
class FirstClearRewardPresenter(
    private val inventory: InventoryQueryService,
    private val items: ItemCatalog,
) {
    fun present(reward: FirstClearRewardResult): FirstClearRewardView = present(
        rewardId = reward.rewardId,
        stageId = reward.stageId,
        rewardVersion = reward.rewardVersion,
        riceGranted = reward.riceGranted,
        unlockedSkillId = reward.unlockedSkillId,
        status = reward.itemStatus,
        rewards = reward.items,
        requiredSlots = reward.requiredSlots,
        availableSlots = availableSlots(reward.accountId),
    )

    fun present(reward: PendingFirstClearReward): FirstClearRewardView = present(
        rewardId = reward.rewardId,
        stageId = reward.stageId,
        rewardVersion = reward.rewardVersion,
        riceGranted = reward.riceGranted,
        unlockedSkillId = reward.unlockedSkillId,
        status = if (reward.claimedAt == null) FirstClearItemStatus.PENDING else FirstClearItemStatus.CLAIMED,
        rewards = reward.items,
        requiredSlots = reward.requiredSlots,
        availableSlots = reward.availableSlots ?: availableSlots(reward.accountId),
    )

    fun presentPending(rewards: List<PendingFirstClearReward>): List<FirstClearRewardView> {
        if (rewards.isEmpty()) return emptyList()
        val accountId = rewards.first().accountId
        require(rewards.all { it.accountId == accountId }) { "FIRST_CLEAR_REWARD_ACCOUNT_MISMATCH" }
        val availableSlots = availableSlots(accountId)
        return rewards.map { reward ->
            present(
                rewardId = reward.rewardId,
                stageId = reward.stageId,
                rewardVersion = reward.rewardVersion,
                riceGranted = reward.riceGranted,
                unlockedSkillId = reward.unlockedSkillId,
                status = if (reward.claimedAt == null) FirstClearItemStatus.PENDING else FirstClearItemStatus.CLAIMED,
                rewards = reward.items,
                requiredSlots = reward.requiredSlots,
                availableSlots = availableSlots,
            )
        }
    }

    private fun availableSlots(accountId: UUID): Int = inventory.status(accountId).let { (it.maxSlots - it.usedSlots).coerceAtLeast(0) }

    private fun present(
        rewardId: UUID,
        stageId: String,
        rewardVersion: String,
        riceGranted: Long,
        unlockedSkillId: String?,
        status: FirstClearItemStatus,
        rewards: List<com.hanjjak.inventory.domain.ItemReward>,
        requiredSlots: Int,
        availableSlots: Int,
    ): FirstClearRewardView {
        val lines = rewards.map { reward ->
            FirstClearRewardItemView(reward.itemId, items.require(reward.itemId).displayName, reward.quantity)
        }
        return FirstClearRewardView(
            rewardId = rewardId,
            stageId = stageId,
            rewardVersion = rewardVersion,
            firstClear = true,
            riceGranted = riceGranted,
            grantedItems = if (status == FirstClearItemStatus.CLAIMED) lines else emptyList(),
            pendingItems = if (status == FirstClearItemStatus.PENDING) lines else emptyList(),
            unlockedSkillId = unlockedSkillId,
            itemStatus = status.name,
            requiredSlots = requiredSlots,
            availableSlots = availableSlots,
            missingSlots = (requiredSlots - availableSlots).coerceAtLeast(0),
        )
    }
}
