package com.hanjjak.inventory.application

import com.hanjjak.inventory.domain.INVENTORY_MAX_SLOTS
import com.hanjjak.inventory.domain.InventoryInstance
import com.hanjjak.inventory.domain.InventorySlots
import com.hanjjak.inventory.domain.ItemCategory
import org.springframework.transaction.annotation.Transactional
import java.util.UUID

open class InventoryInstanceService(
    private val repository: InventoryRepository,
    private val catalog: ItemCatalog,
) {
    data class GemGroup(val itemId: String, val stackKey: String, val locked: Boolean = false)

    @Transactional
    open fun consumeStackForInstances(accountId: UUID, itemId: String, quantity: Long, instanceCount: Int, possibleGroups: List<GemGroup> = emptyList()) {
        require(quantity > 0 && instanceCount >= 0) { "INVALID_QUANTITY" }
        repository.lockAccount(accountId)
        val stack = repository.lockStack(accountId, itemId) ?: throw IllegalArgumentException("INSUFFICIENT_AVAILABLE_QUANTITY")
        require(stack.availableQuantity >= quantity) { "INSUFFICIENT_AVAILABLE_QUANTITY" }
        val reservedSlot = repository.hasCapacityReservation(accountId, itemId)
        val definition = catalog.require(itemId)
        val releasedSlots = InventorySlots.stackSlots(stack.quantity, definition, reservedSlot) -
            InventorySlots.stackSlots(stack.quantity - quantity, definition, reservedSlot)
        val needed = worstCaseAdditionalSlots(repository.instances(accountId), instanceCount, possibleGroups)
        require(repository.usedSlotCount(accountId).toLong() - releasedSlots + needed <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
        repository.saveStack(stack.copy(quantity = stack.quantity - quantity))
        repository.incrementStateVersion(accountId)
    }

    @Transactional
    open fun grant(accountId: UUID, instanceId: UUID, itemId: String, stackKey: String? = null, locked: Boolean = false) {
        val definition = catalog.require(itemId)
        require(!definition.stackable) { "ITEM_NOT_INSTANCE" }
        require(stackKey == null || definition.category == ItemCategory.GEM) { "INVALID_STACK_KEY" }
        repository.lockAccount(accountId)
        val owned = repository.instances(accountId)
        val slot = if (stackKey == null) null else InventorySlots.instanceGroups(owned)
            .firstOrNull { it.size < InventorySlots.GEM_LIMIT && it.first().itemId == itemId && it.first().stackKey == stackKey && it.first().locked == locked && it.first().inventorySlotId != null }
            ?.first()?.inventorySlotId ?: UUID.randomUUID()
        val instance = InventoryInstance(instanceId, accountId, itemId, false, repository.nextAcquiredSequence(accountId), stackKey, slot, locked)
        val delta = InventorySlots.instanceSlots(owned + instance) - InventorySlots.instanceSlots(owned)
        require(repository.usedSlotCount(accountId) + delta <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
        repository.addInstance(instance)
        repository.incrementStateVersion(accountId)
    }

    @Transactional
    open fun checkExchange(accountId: UUID, consumedIds: List<UUID>, possibleGroups: List<GemGroup>, grantedCount: Int = 1) {
        require(grantedCount > 0) { "INVALID_QUANTITY" }
        repository.lockAccount(accountId)
        val owned = repository.instances(accountId)
        val remaining = owned.filterNot { it.instanceId in consumedIds }
        val released = InventorySlots.instanceSlots(owned) - InventorySlots.instanceSlots(remaining)
        require(repository.usedSlotCount(accountId) - released + worstCaseAdditionalSlots(remaining, grantedCount, possibleGroups) <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
    }

    // Maximize slot growth over all possible outcomes before any draw or consumption.
    private fun worstCaseAdditionalSlots(owned: List<InventoryInstance>, count: Int, groups: List<GemGroup>): Long {
        if (groups.isEmpty()) return count.toLong()
        var best = LongArray(count + 1) { Long.MIN_VALUE }
        best[0] = 0
        for (group in groups.distinct()) {
            val free = InventorySlots.instanceGroups(owned).filter { it.first().itemId == group.itemId && it.first().stackKey == group.stackKey && it.first().locked == group.locked && it.first().inventorySlotId != null }
                .sumOf { InventorySlots.GEM_LIMIT - it.size }
            val next = LongArray(count + 1) { Long.MIN_VALUE }
            for (used in 0..count) if (best[used] != Long.MIN_VALUE) {
                for (added in 0..count - used) {
                    val delta = InventorySlots.count((added - free).coerceAtLeast(0), InventorySlots.GEM_LIMIT)
                    next[used + added] = maxOf(next[used + added], best[used] + delta)
                }
            }
            best = next
        }
        return best[count]
    }

    @Transactional
    open fun consume(accountId: UUID, instanceIds: List<UUID>) {
        require(instanceIds.isNotEmpty() && instanceIds.distinct().size == instanceIds.size) { "INVALID_QUANTITY" }
        repository.lockAccount(accountId)
        val owned = repository.instances(accountId).associateBy { it.instanceId }
        require(instanceIds.all { owned[it]?.let { instance -> !instance.reservedForSale && !instance.locked } == true }) { "INSTANCE_NOT_AVAILABLE" }
        repository.removeInstances(accountId, instanceIds)
        repository.incrementStateVersion(accountId)
    }
}
