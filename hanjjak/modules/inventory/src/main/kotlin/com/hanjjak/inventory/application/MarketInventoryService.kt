package com.hanjjak.inventory.application

import com.hanjjak.inventory.domain.ItemDefinition
import com.hanjjak.inventory.domain.INVENTORY_MAX_SLOTS
import com.hanjjak.inventory.domain.InventoryStack
import com.hanjjak.inventory.domain.InventorySlots
import org.springframework.transaction.annotation.Transactional
import java.util.UUID

open class MarketInventoryService(private val repository: InventoryRepository, private val catalog: ItemCatalog) {
    data class ReservedTransfer(val sellerAccountId: UUID, val buyerAccountId: UUID, val itemId: String, val quantity: Long)
    @Transactional
    open fun reserveForSale(accountId: UUID, itemId: String, quantity: Long) {
        require(quantity > 0) { "INVALID_QUANTITY" }
        requireTradeableStack(itemId)
        repository.lockAccount(accountId)
        val stack = repository.lockStack(accountId, itemId) ?: throw IllegalArgumentException("INSUFFICIENT_AVAILABLE_QUANTITY")
        require(stack.availableQuantity >= quantity) { "INSUFFICIENT_AVAILABLE_QUANTITY" }
        // The listing owns escrow stock; it no longer occupies inventory.
        repository.saveStack(stack.copy(quantity = stack.quantity - quantity))
        repository.incrementStateVersion(accountId)
    }

    @Transactional
    open fun releaseReservation(accountId: UUID, itemId: String, quantity: Long) {
        require(quantity > 0) { "INVALID_QUANTITY" }
        requireTradeableStack(itemId)
        InventoryReservationService(repository, catalog).grantStackOrReject(accountId, itemId, quantity)
    }
    @Transactional
    open fun consumeExternalReservation(accountId: UUID, itemId: String, quantity: Long) {
        require(quantity > 0) { "INVALID_QUANTITY" }
        requireTradeableStack(itemId)
        repository.lockAccount(accountId)
        repository.incrementStateVersion(accountId)
    }


    @Transactional
    open fun transferReserved(sellerAccountId: UUID, buyerAccountId: UUID, itemId: String, quantity: Long, bumpStateVersion: Boolean = true) {
        require(quantity > 0) { "INVALID_QUANTITY" }
        require(sellerAccountId != buyerAccountId) { "SELF_PURCHASE_NOT_ALLOWED" }
        val definition = requireTradeableStack(itemId)

        listOf(sellerAccountId, buyerAccountId).sorted().forEach(repository::lockAccount)
        val locked = lockSellerAndBuyer(sellerAccountId, buyerAccountId, itemId)
        val buyerStack = locked.buyer
        val reserved = repository.hasCapacityReservation(buyerAccountId, itemId)
        val delta = InventorySlots.stackSlots(Math.addExact(buyerStack?.quantity ?: 0L, quantity), definition, reserved) -
            InventorySlots.stackSlots(buyerStack?.quantity ?: 0L, definition, reserved)
        require(repository.usedSlotCount(buyerAccountId) + delta <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }

        if (buyerStack == null) {
            repository.saveStack(InventoryStack(buyerAccountId, itemId, quantity, 0, repository.nextAcquiredSequence(buyerAccountId)))
        } else {
            repository.saveStack(buyerStack.copy(quantity = Math.addExact(buyerStack.quantity, quantity)))
        }
        if (bumpStateVersion) {
            repository.incrementStateVersion(sellerAccountId)
            repository.incrementStateVersion(buyerAccountId)
        }
    }

    @Transactional
    open fun transferReserveds(transfers: List<ReservedTransfer>, bumpStateVersion: Boolean = true) {
        if (transfers.isEmpty()) return
        transfers.forEach {
            require(it.quantity > 0) { "INVALID_QUANTITY" }
            require(it.sellerAccountId != it.buyerAccountId) { "SELF_PURCHASE_NOT_ALLOWED" }
            requireTradeableStack(it.itemId)
        }

        val buyerQuantities = transfers
            .groupingBy { InventoryRepository.StackKey(it.buyerAccountId, it.itemId) }
            .fold(0L) { total, transfer -> Math.addExact(total, transfer.quantity) }
        val keys = buyerQuantities.keys
            .distinct()
            .sortedWith(compareBy<InventoryRepository.StackKey> { it.accountId }.thenBy { it.itemId })
        transfers.map { it.buyerAccountId }.distinct().sorted().forEach(repository::lockAccount)
        val locked = repository.lockStacks(keys)
        val updated = locked.toMutableMap()

        val nextSequences = mutableMapOf<UUID, Long>()
        buyerQuantities.forEach { (key, quantity) ->
            val stack = updated[key]
            if (stack == null) {
                val sequence = nextSequences.compute(key.accountId) { accountId, next ->
                    if (next == null) repository.nextAcquiredSequence(accountId) else next + 1
                }!!
                updated[key] = InventoryStack(key.accountId, key.itemId, quantity, 0, sequence)
            } else {
                updated[key] = stack.copy(quantity = Math.addExact(stack.quantity, quantity))
            }
        }

        keys.groupBy { it.accountId }.forEach { (accountId, accountKeys) ->
            val delta = accountKeys.sumOf { key ->
                val definition = catalog.require(key.itemId)
                val reserved = repository.hasCapacityReservation(accountId, key.itemId)
                InventorySlots.stackSlots(updated[key]?.quantity ?: 0L, definition, reserved) -
                    InventorySlots.stackSlots(locked[key]?.quantity ?: 0L, definition, reserved)
            }
            require(repository.usedSlotCount(accountId) + delta <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
        }
        repository.saveStacks(updated.values.sortedWith(compareBy<InventoryStack> { it.accountId }.thenBy { it.itemId }))
        if (bumpStateVersion) {
            transfers.flatMap { listOf(it.sellerAccountId, it.buyerAccountId) }
                .distinct()
                .sorted()
                .forEach { repository.incrementStateVersion(it) }
        }
    }

    @Transactional
    open fun reserveInstancesForSale(accountId: UUID, itemId: String, instanceIds: List<UUID>) {
        require(instanceIds.isNotEmpty() && instanceIds.distinct().size == instanceIds.size) { "INVALID_QUANTITY" }
        val definition = catalog.require(itemId)
        require(definition.tradeable && !definition.stackable) { "ITEM_NOT_TRADEABLE" }
        repository.lockAccount(accountId)
        val owned = repository.instances(accountId).associateBy { it.instanceId }
        val selected = instanceIds.map { id -> owned[id] ?: throw IllegalArgumentException("INSTANCE_NOT_AVAILABLE") }
        require(selected.all { it.itemId == itemId && !it.reservedForSale && !it.locked }) { "INSTANCE_NOT_AVAILABLE" }
        repository.setInstancesReserved(accountId, instanceIds, true)
        repository.incrementStateVersion(accountId)
    }

    @Transactional
    open fun deliverInstances(accountId: UUID, instanceIds: List<UUID>) {
        require(instanceIds.isNotEmpty()) { "INVALID_QUANTITY" }
        repository.lockAccount(accountId)
        val owned = repository.instances(accountId).filter { it.instanceId in instanceIds }
        require(owned.size == instanceIds.distinct().size && owned.all { it.reservedForSale }) { "INSTANCE_NOT_AVAILABLE" }
        repository.transferInstances(accountId, accountId, instanceIds, false)
        repository.incrementStateVersion(accountId)
    }

    @Transactional
    open fun transferReservedInstances(sellerAccountId: UUID, buyerAccountId: UUID, instanceIds: List<UUID>) {
        require(instanceIds.isNotEmpty() && sellerAccountId != buyerAccountId) { "INVALID_QUANTITY" }
        listOf(sellerAccountId, buyerAccountId).sorted().forEach(repository::lockAccount)
        val owned = repository.instances(sellerAccountId).filter { it.instanceId in instanceIds }
        require(owned.size == instanceIds.distinct().size && owned.all { it.reservedForSale }) { "INSTANCE_NOT_AVAILABLE" }
        val buyerInstances = repository.instances(buyerAccountId)
        val incoming = owned.map { it.copy(accountId = buyerAccountId, reservedForSale = false, acquiredSequence = repository.nextAcquiredSequence(buyerAccountId)) }
        require(repository.usedSlotCount(buyerAccountId) + InventorySlots.instanceSlots(buyerInstances + incoming) - InventorySlots.instanceSlots(buyerInstances) <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
        repository.transferInstances(sellerAccountId, buyerAccountId, instanceIds, false)
        repository.incrementStateVersion(sellerAccountId)
        repository.incrementStateVersion(buyerAccountId)
    }

    open fun availableQuantity(accountId: UUID, itemId: String): Long {
        val definition = catalog.require(itemId)
        return if (definition.stackable) repository.stacks(accountId).firstOrNull { it.itemId == itemId }?.availableQuantity ?: 0
        else repository.instances(accountId).count { it.itemId == itemId && !it.reservedForSale && !it.locked }.toLong()
    }

    open fun status(accountId: UUID) = com.hanjjak.inventory.domain.InventoryStatus(repository.usedSlotCount(accountId))

    /** 계정 상태 버전을 한 번만 올린다. 여러 체결을 묶어 처리할 때 호출자가 쓴다. (실험용) */
    open fun bumpStateVersions(accountIds: Collection<UUID>) {
        repository.incrementStateVersions(accountIds)
    }

    private fun requireTradeableStack(itemId: String): ItemDefinition = catalog.require(itemId).also { definition ->
        require(definition.tradeable && definition.stackable) { "ITEM_NOT_TRADEABLE" }
    }

    private fun lockSellerAndBuyer(sellerAccountId: UUID, buyerAccountId: UUID, itemId: String): LockedStacks {
        val first = listOf(sellerAccountId, buyerAccountId).sorted().first()
        val second = if (first == sellerAccountId) buyerAccountId else sellerAccountId
        val firstStack = repository.lockStack(first, itemId)
        val secondStack = repository.lockStack(second, itemId)
        return if (sellerAccountId == first) LockedStacks(firstStack, secondStack) else LockedStacks(secondStack, firstStack)
    }

    private data class LockedStacks(val seller: InventoryStack?, val buyer: InventoryStack?)
}
