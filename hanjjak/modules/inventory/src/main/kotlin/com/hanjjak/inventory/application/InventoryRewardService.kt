package com.hanjjak.inventory.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.domain.*
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.util.UUID

open class InventoryRewardService(
    private val repository: InventoryRepository,
    private val catalog: ItemCatalog,
    private val mapper: ObjectMapper,
) {
    data class Execution(val result: RewardResult, val replayed: Boolean)

    @Transactional
    open fun grant(accountId: UUID, idempotencyKey: UUID, source: String, rewards: List<ItemReward>): RewardResult =
        grantOnce(accountId, idempotencyKey, source, rewards).result

    @Transactional
    open fun grantOnce(accountId: UUID, idempotencyKey: UUID, source: String, rewards: List<ItemReward>): Execution {
        require(source.isNotBlank()) { "INVALID_REWARD_SOURCE" }
        val normalized = rewards.groupingBy { it.itemId }.fold(0L) { total, reward -> Math.addExact(total, reward.quantity) }
            .toSortedMap().map { (itemId, quantity) -> ItemReward(itemId, quantity) }
        normalized.forEach { catalog.require(it.itemId) }
        val fingerprint = sha256("$source\u0000${normalized.joinToString("|") { "${it.itemId}:${it.quantity}" }}")
        repository.lockAccount(accountId)
        repository.rewardCommand(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return Execution(mapper.readValue(existing.resultJson, RewardResult::class.java), true)
        }

        val stacks = repository.stacks(accountId).filter { it.quantity > 0 }.associateBy { it.itemId }.toMutableMap()
        val instances = repository.instances(accountId)
        var usedSlots = repository.usedSlotCount(accountId)
        var sequence = (stacks.values.maxOfOrNull { it.acquiredSequence } ?: 0L).coerceAtLeast(instances.maxOfOrNull { it.acquiredSequence } ?: 0L)
        val lines = normalized.map { reward ->
            val definition = catalog.require(reward.itemId)
            if (usedSlots >= INVENTORY_MAX_SLOTS) {
                return@map RewardLine(reward.itemId, reward.quantity, 0, 0, reward.quantity)
            }
            if (definition.stackable) {
                val existing = stacks[reward.itemId]
                val current = existing?.quantity ?: 0L
                val reserved = repository.hasCapacityReservation(accountId, reward.itemId)
                val before = InventorySlots.stackSlots(current, definition, reserved)
                val limit = InventorySlots.limit(definition)
                val capacity = if (limit == Long.MAX_VALUE) Long.MAX_VALUE - current
                    else (INVENTORY_MAX_SLOTS - usedSlots + before) * limit - current
                val grantable = minOf(reward.quantity, capacity)
                val total = current + grantable
                val updated = existing?.copy(quantity = total)
                    ?: InventoryStack(accountId, reward.itemId, total, 0, ++sequence)
                repository.saveStack(updated)
                stacks[reward.itemId] = updated
                usedSlots += Math.toIntExact(InventorySlots.stackSlots(total, definition, reserved) - before)
                RewardLine(reward.itemId, reward.quantity, grantable, 0, reward.quantity - grantable)
            } else {
                val grantable = minOf(reward.quantity, (INVENTORY_MAX_SLOTS - usedSlots).toLong())
                repeat(grantable.toInt()) {
                    sequence++
                    repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, reward.itemId, false, sequence))
                }
                usedSlots += grantable.toInt()
                RewardLine(reward.itemId, reward.quantity, grantable, 0, reward.quantity - grantable)
            }
        }
        if (lines.any { it.grantedQuantity > 0 }) repository.incrementStateVersion(accountId)
        val result = RewardResult(lines, usedSlots, INVENTORY_MAX_SLOTS, usedSlots == INVENTORY_MAX_SLOTS)
        repository.saveRewardCommand(accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return Execution(result, false)
    }

    /** Validates one stack reward while the caller owns the account lock. No state/version mutation. */
    open fun preflightGrantLocked(accountId: UUID, itemId: String, quantity: Long) {
        require(quantity >= 0) { "INVALID_QUANTITY" }
        if (quantity == 0L) return
        val definition = catalog.require(itemId)
        require(definition.stackable) { "ITEM_NOT_STACKABLE" }
        val existing = repository.lockStack(accountId, itemId)
        val current = existing?.quantity ?: 0L
        Math.addExact(current, quantity)
        val reserved = repository.hasCapacityReservation(accountId, itemId)
        val before = InventorySlots.stackSlots(current, definition, reserved)
        val after = InventorySlots.stackSlots(Math.addExact(current, quantity), definition, reserved)
        require(repository.usedSlotCount(accountId).toLong() - before + after <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
    }

    /** Applies one previously preflighted stack reward. Caller owns account and asset locks. */
    open fun grantLocked(accountId: UUID, itemId: String, quantity: Long) {
        require(quantity >= 0) { "INVALID_QUANTITY" }
        if (quantity == 0L) return
        val definition = catalog.require(itemId)
        require(definition.stackable) { "ITEM_NOT_STACKABLE" }
        val existing = repository.lockStack(accountId, itemId)
        val current = existing?.quantity ?: 0L
        val total = Math.addExact(current, quantity)
        val stack = existing?.copy(quantity = total) ?: InventoryStack(accountId, itemId, total, 0, repository.nextAcquiredSequence(accountId))
        repository.saveStack(stack)
    }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
}
