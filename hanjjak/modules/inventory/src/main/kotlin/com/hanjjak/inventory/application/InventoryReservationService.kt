package com.hanjjak.inventory.application

import com.hanjjak.inventory.domain.INVENTORY_MAX_SLOTS
import com.hanjjak.inventory.domain.InventoryStack
import com.hanjjak.inventory.domain.InventorySlots
import org.springframework.transaction.annotation.Transactional
import java.util.UUID

data class StackGrantPlan(
    val accountId: UUID,
    val items: List<com.hanjjak.inventory.domain.ItemReward>,
    val resultingStacks: List<InventoryStack>,
    val requiredSlots: Int,
    val availableSlots: Int,
    val canGrant: Boolean,
)

data class StackGrantPreview(
    val requiredSlots: Int,
    val availableSlots: Int,
    val canGrant: Boolean,
)

open class InventoryReservationService(private val repository: InventoryRepository, private val catalog: ItemCatalog) {
    @Transactional
    open fun reserveForSale(accountId: UUID, itemId: String, quantity: Long) {
        require(quantity > 0) { "INVALID_QUANTITY" }
        require(catalog.require(itemId).tradeable) { "ITEM_NOT_TRADEABLE" }
        repository.lockAccount(accountId)
        val stack = repository.stacks(accountId).firstOrNull { it.itemId == itemId } ?: throw IllegalArgumentException("INSUFFICIENT_AVAILABLE_QUANTITY")
        require(stack.availableQuantity >= quantity) { "INSUFFICIENT_AVAILABLE_QUANTITY" }
        repository.saveStack(stack.copy(reservedQuantity = Math.addExact(stack.reservedQuantity, quantity)))
        repository.incrementStateVersion(accountId)
    }

    @Transactional
    open fun releaseSaleReservation(accountId: UUID, itemId: String, quantity: Long) {
        require(quantity > 0) { "INVALID_QUANTITY" }
        repository.lockAccount(accountId)
        val stack = repository.stacks(accountId).firstOrNull { it.itemId == itemId } ?: throw IllegalArgumentException("RESERVATION_NOT_FOUND")
        require(stack.reservedQuantity >= quantity) { "RESERVATION_NOT_FOUND" }
        repository.saveStack(stack.copy(reservedQuantity = stack.reservedQuantity - quantity))
        repository.incrementStateVersion(accountId)
    }

    /**
     * 중첩 아이템을 전량 지급하거나 전체를 거절한다.
     * 새 스택 슬롯이 필요한데 빈 칸이 없으면 아무 상태도 바꾸지 않고 INVENTORY_CAPACITY_EXCEEDED로 실패한다.
     * 공간 부족 시 지급을 건너뛰는 전투 보상 경로(InventoryRewardService)와 달리 부분 지급하지 않는다.
     */
    @Transactional
    open fun grantStackOrReject(accountId: UUID, itemId: String, quantity: Long) {
        val plan = planStackGrant(accountId, listOf(com.hanjjak.inventory.domain.ItemReward(itemId, quantity)))
        require(plan.canGrant) { "INVENTORY_CAPACITY_EXCEEDED" }
        applyStackGrant(accountId, plan)
    }

    @Transactional
    open fun planStackGrant(accountId: UUID, items: List<com.hanjjak.inventory.domain.ItemReward>): StackGrantPlan {
        val normalized = normalize(items)
        repository.lockAccount(accountId)
        val locked = repository.lockStacks(normalized.map { InventoryRepository.StackKey(accountId, it.itemId) })
        val usedSlots = repository.usedSlotCount(accountId)
        val capacityReservedItemIds = repository.capacityReservedItemIds(accountId, normalized.map { it.itemId })
        var nextSequence: Long? = null
        val resulting = normalized.map { reward ->
            val existing = locked[InventoryRepository.StackKey(accountId, reward.itemId)]
            if (existing == null) {
                if (nextSequence == null) nextSequence = repository.nextAcquiredSequence(accountId)
                InventoryStack(accountId, reward.itemId, reward.quantity, 0, checkNotNull(nextSequence).also { nextSequence = it + 1 })
            } else existing.copy(quantity = Math.addExact(existing.quantity, reward.quantity))
        }
        val beforeSlots = normalized.sumOf { reward ->
            val existing = locked[InventoryRepository.StackKey(accountId, reward.itemId)]
            InventorySlots.stackSlots(existing?.quantity ?: 0, catalog.require(reward.itemId), reward.itemId in capacityReservedItemIds)
        }
        val afterSlots = resulting.sumOf { stack ->
            InventorySlots.stackSlots(stack.quantity, catalog.require(stack.itemId), stack.itemId in capacityReservedItemIds)
        }
        val requiredSlots = Math.toIntExact((afterSlots - beforeSlots).coerceAtLeast(0))
        val availableSlots = (INVENTORY_MAX_SLOTS - usedSlots).coerceAtLeast(0)
        return StackGrantPlan(accountId, normalized, resulting, requiredSlots, availableSlots, requiredSlots <= availableSlots)
    }

    @Transactional(readOnly = true)
    open fun previewStackGrants(accountId: UUID, bundles: List<List<com.hanjjak.inventory.domain.ItemReward>>): List<StackGrantPreview> =
        previewStackGrants(mapOf(accountId to bundles))[accountId].orEmpty()

    @Transactional(readOnly = true)
    open fun previewStackGrants(
        bundlesByAccount: Map<UUID, List<List<com.hanjjak.inventory.domain.ItemReward>>>,
    ): Map<UUID, List<StackGrantPreview>> {
        val normalizedByAccount = bundlesByAccount.mapValues { (_, bundles) -> bundles.map(::normalize) }
        val nonEmpty = normalizedByAccount.filterValues { it.isNotEmpty() }
        if (nonEmpty.isEmpty()) return normalizedByAccount.mapValues { emptyList() }
        val snapshots = repository.stackGrantSnapshots(
            nonEmpty.mapValues { (_, bundles) -> bundles.flatten().map { it.itemId } },
        )
        return normalizedByAccount.mapValues { (accountId, bundles) ->
            if (bundles.isEmpty()) return@mapValues emptyList()
            val snapshot = checkNotNull(snapshots[accountId]) { "INVENTORY_SNAPSHOT_MISSING" }
            previewStackGrants(accountId, bundles, snapshot)
        }
    }

    private fun previewStackGrants(
        accountId: UUID,
        bundles: List<List<com.hanjjak.inventory.domain.ItemReward>>,
        snapshot: InventoryRepository.StackGrantSnapshot,
    ): List<StackGrantPreview> {
        val virtualStacks = snapshot.stacks.associateBy { it.itemId }.toMutableMap()
        var usedSlots = snapshot.usedSlots
        return bundles.map { bundle ->
            val beforeSlots = bundle.sumOf { reward ->
                InventorySlots.stackSlots(
                    virtualStacks[reward.itemId]?.quantity ?: 0,
                    catalog.require(reward.itemId),
                    reward.itemId in snapshot.capacityReservedItemIds,
                )
            }
            val resulting = bundle.map { reward ->
                Math.addExact(virtualStacks[reward.itemId]?.quantity ?: 0L, reward.quantity)
            }
            val afterSlots = bundle.indices.sumOf { index ->
                val reward = bundle[index]
                InventorySlots.stackSlots(
                    resulting[index],
                    catalog.require(reward.itemId),
                    reward.itemId in snapshot.capacityReservedItemIds,
                )
            }
            val requiredSlots = Math.toIntExact((afterSlots - beforeSlots).coerceAtLeast(0))
            val availableSlots = (INVENTORY_MAX_SLOTS - usedSlots).coerceAtLeast(0)
            val canGrant = requiredSlots <= availableSlots
            if (canGrant) {
                bundle.forEachIndexed { index, reward ->
                    val existing = virtualStacks[reward.itemId]
                    virtualStacks[reward.itemId] = InventoryStack(
                        accountId,
                        reward.itemId,
                        resulting[index],
                        existing?.reservedQuantity ?: 0,
                        existing?.acquiredSequence ?: 0,
                    )
                }
                usedSlots = Math.addExact(usedSlots, requiredSlots)
            }
            StackGrantPreview(requiredSlots, availableSlots, canGrant)
        }
    }

    private fun normalize(items: List<com.hanjjak.inventory.domain.ItemReward>): List<com.hanjjak.inventory.domain.ItemReward> {
        require(items.all { it.quantity > 0 }) { "INVALID_QUANTITY" }
        val normalized = items.groupingBy { it.itemId }
            .fold(0L) { total, item -> Math.addExact(total, item.quantity) }
            .toSortedMap()
            .map { (itemId, quantity) -> com.hanjjak.inventory.domain.ItemReward(itemId, quantity) }
        normalized.forEach { reward -> require(catalog.require(reward.itemId).stackable) { "ITEM_NOT_STACKABLE" } }
        return normalized
    }

    @Transactional
    open fun applyStackGrant(accountId: UUID, plan: StackGrantPlan, bumpStateVersion: Boolean = true) {
        require(plan.accountId == accountId) { "STACK_GRANT_ACCOUNT_MISMATCH" }
        require(plan.canGrant) { "INVENTORY_CAPACITY_EXCEEDED" }
        repository.lockAccount(accountId)
        repository.saveStacks(plan.resultingStacks)
        if (bumpStateVersion) repository.incrementStateVersion(accountId)
    }

    @Transactional
    open fun consumeAvailable(accountId: UUID, itemId: String, quantity: Long) {
        require(quantity > 0) { "INVALID_QUANTITY" }
        repository.lockAccount(accountId)
        val stack = repository.stacks(accountId).firstOrNull { it.itemId == itemId } ?: throw IllegalArgumentException("INSUFFICIENT_AVAILABLE_QUANTITY")
        require(stack.availableQuantity >= quantity) { "INSUFFICIENT_AVAILABLE_QUANTITY" }
        repository.saveStack(stack.copy(quantity = stack.quantity - quantity))
        repository.incrementStateVersion(accountId)
    }
}
