package com.hanjjak.battle.domain

import com.hanjjak.inventory.domain.ItemReward
import java.time.Instant
import java.util.UUID

enum class FirstClearItemStatus { CLAIMED, PENDING }

data class FirstClearRewardResult(
    val rewardId: UUID,
    val accountId: UUID,
    val stageId: String,
    val rewardVersion: String,
    val sourceId: UUID,
    val riceGranted: Long,
    val unlockedSkillId: String?,
    val itemStatus: FirstClearItemStatus,
    val items: List<ItemReward>,
    val requiredSlots: Int,
)

data class PendingFirstClearReward(
    val rewardId: UUID,
    val accountId: UUID,
    val stageId: String,
    val rewardVersion: String,
    val sourceId: UUID,
    val riceGranted: Long,
    val unlockedSkillId: String?,
    val items: List<ItemReward>,
    val requiredSlots: Int,
    val claimedAt: Instant? = null,
    val availableSlots: Int? = null,
)
