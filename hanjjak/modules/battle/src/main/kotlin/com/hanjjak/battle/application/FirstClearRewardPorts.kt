package com.hanjjak.battle.application

import com.hanjjak.battle.domain.PendingFirstClearReward
import com.hanjjak.battle.domain.FirstClearRewardResult
import com.hanjjak.inventory.domain.ItemReward
import java.time.Instant
import java.util.UUID

interface FirstClearRewardRepository {
    data class RewardRow(
        val rewardId: UUID,
        val accountId: UUID,
        val stageId: String,
        val rewardVersion: String,
        val sourceId: UUID,
        val riceGranted: Long,
        val unlockedSkillId: String?,
        val itemStatus: String,
        val items: List<ItemReward>,
        val requiredSlots: Int,
        val claimedAt: Instant?,
    )
    data class ClaimCommand(val fingerprint: String, val resultJson: String)

    fun lockAccount(accountId: UUID)
    fun findByVersion(accountId: UUID, stageId: String, rewardVersion: String): RewardRow?
    fun insert(row: RewardRow): Boolean
    fun lockReward(accountId: UUID, rewardId: UUID): RewardRow?
    fun pending(accountId: UUID): List<RewardRow>
    fun markClaimed(rewardId: UUID, claimedAt: Instant)
    fun claimCommand(accountId: UUID, idempotencyKey: UUID): ClaimCommand?
    fun saveClaimCommand(accountId: UUID, idempotencyKey: UUID, rewardId: UUID, fingerprint: String, resultJson: String)
}
