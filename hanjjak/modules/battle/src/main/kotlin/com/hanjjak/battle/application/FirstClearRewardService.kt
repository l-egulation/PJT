package com.hanjjak.battle.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.domain.FirstClearItemStatus
import com.hanjjak.battle.domain.FirstClearRewardResult
import com.hanjjak.battle.domain.PendingFirstClearReward
import com.hanjjak.inventory.application.InventoryReservationService
import com.hanjjak.inventory.application.StackGrantPlan
import com.hanjjak.inventory.domain.ItemReward
import com.hanjjak.progression.application.ProgressionRewardCatalog
import com.hanjjak.skills.application.SkillService
import com.hanjjak.wallet.application.WalletService
import com.hanjjak.events.application.DomainEventPublisher
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Instant
import java.util.UUID

open class FirstClearRewardService(
    private val repository: FirstClearRewardRepository,
    private val catalog: ProgressionRewardCatalog,
    private val inventory: InventoryReservationService,
    private val wallet: WalletService,
    private val skills: SkillService,
    private val mapper: ObjectMapper,
    private val events: DomainEventPublisher = com.hanjjak.events.application.NoopDomainEventPublisher,
) {
    @Transactional
    open fun apply(accountId: UUID, stageId: String, rewardVersion: String, sourceId: UUID): FirstClearRewardResult? {
        val definition = catalog.firstClearOrNull(rewardVersion, stageId) ?: return null
        repository.lockAccount(accountId)
        if (repository.findByVersion(accountId, stageId, rewardVersion) != null) return null
        val items = definition.items.map { ItemReward(it.itemId, it.quantity) }
        val plan = inventory.planStackGrant(accountId, items)
        if (definition.rice > 0) wallet.credit(accountId, definition.rice, "STAGE_FIRST_CLEAR", sourceId, bumpStateVersion = false)
        definition.directSkillId?.let { skills.unlockFromFirstClear(accountId, it, bumpStateVersion = false) }
        val rewardId = UUID.randomUUID()
        val status = if (plan.canGrant) FirstClearItemStatus.CLAIMED else FirstClearItemStatus.PENDING
        if (plan.canGrant) inventory.applyStackGrant(accountId, plan, bumpStateVersion = false)
        val row = FirstClearRewardRepository.RewardRow(
            rewardId = rewardId,
            accountId = accountId,
            stageId = stageId,
            rewardVersion = rewardVersion,
            sourceId = sourceId,
            riceGranted = definition.rice,
            unlockedSkillId = definition.directSkillId,
            itemStatus = status.name,
            items = items,
            requiredSlots = plan.requiredSlots,
            claimedAt = if (status == FirstClearItemStatus.CLAIMED) Instant.now() else null,
        )
        check(repository.insert(row)) { "FIRST_CLEAR_REWARD_INSERT_FAILED" }
        return row.toResult()
    }

    @Transactional(readOnly = true)
    open fun pending(accountId: UUID): List<PendingFirstClearReward> = repository.pending(accountId).map { it.toPending() }

    @Transactional
    open fun claim(accountId: UUID, rewardId: UUID, idempotencyKey: UUID): PendingFirstClearReward {
        repository.lockAccount(accountId)
        val fingerprint = sha256("first-clear-claim\u0000$rewardId")
        repository.claimCommand(accountId, idempotencyKey)?.let { command ->
            require(command.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return mapper.readValue(command.resultJson, PendingFirstClearReward::class.java)
        }
        val row = repository.lockReward(accountId, rewardId) ?: throw IllegalArgumentException("FIRST_CLEAR_REWARD_NOT_FOUND")
        require(row.accountId == accountId) { "FIRST_CLEAR_REWARD_NOT_FOUND" }
        require(row.itemStatus == FirstClearItemStatus.PENDING.name) { "FIRST_CLEAR_REWARD_ALREADY_CLAIMED" }
        val plan = inventory.planStackGrant(accountId, row.items)
        inventory.applyStackGrant(accountId, plan, bumpStateVersion = true)
        val claimedAt = Instant.now()
        repository.markClaimed(row.rewardId, claimedAt)
        val result = row.copy(itemStatus = FirstClearItemStatus.CLAIMED.name, requiredSlots = plan.requiredSlots, claimedAt = claimedAt)
            .toPending((plan.availableSlots - plan.requiredSlots).coerceAtLeast(0))
        repository.saveClaimCommand(accountId, idempotencyKey, rewardId, fingerprint, mapper.writeValueAsString(result))
        publishClaimEvent(accountId, row, claimedAt)
        return result
    }
    private fun publishClaimEvent(accountId: UUID, row: FirstClearRewardRepository.RewardRow, occurredAt: Instant) {
        val chapter = row.stageId.removePrefix("stage.").substringBefore("-").toIntOrNull()
        events.publish(
            eventType = "STAGE_FIRST_CLEAR_REWARD_CLAIMED",
            aggregateId = accountId,
            occurredAt = occurredAt,
            accountId = accountId,
            chapter = chapter,
            stageId = row.stageId,
            commandId = row.sourceId,
            payload = mapOf(
                "stageId" to row.stageId,
                "rewardId" to row.rewardId,
                "rewardVersion" to row.rewardVersion,
                "grantedItems" to row.items.map { item -> mapOf("itemId" to item.itemId, "quantity" to item.quantity) },
                "itemStatus" to FirstClearItemStatus.CLAIMED.name,
            ),
        )
    }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }

    private fun FirstClearRewardRepository.RewardRow.toResult() = FirstClearRewardResult(
        rewardId, accountId, stageId, rewardVersion, sourceId, riceGranted, unlockedSkillId,
        FirstClearItemStatus.valueOf(itemStatus), items, requiredSlots,
    )

    private fun FirstClearRewardRepository.RewardRow.toPending(availableSlots: Int? = null) = PendingFirstClearReward(
        rewardId, accountId, stageId, rewardVersion, sourceId, riceGranted, unlockedSkillId, items, requiredSlots, claimedAt,
        availableSlots,
    )
}
