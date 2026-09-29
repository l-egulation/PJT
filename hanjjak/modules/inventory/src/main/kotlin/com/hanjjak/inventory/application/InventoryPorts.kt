package com.hanjjak.inventory.application

import com.hanjjak.inventory.domain.*
import java.util.UUID

interface ItemCatalog {
    fun all(): List<ItemDefinition>
    fun find(itemId: String): ItemDefinition?
    fun require(itemId: String): ItemDefinition = find(itemId) ?: throw IllegalArgumentException("ITEM_NOT_FOUND")
}

/**
 * 다른 도메인이 소유한 콘텐츠에서 파생되는 인벤토리 아이템을 카탈로그에 공급한다.
 * 치장 선택 상자처럼 정의가 치장 콘텐츠에 있는 아이템을 인벤토리가 직접 참조하지 않고 받기 위한 경계다.
 */
fun interface ItemDefinitionSource {
    fun items(): List<ItemDefinition>
}

interface InventoryRepository {
    data class RewardCommand(val fingerprint: String, val resultJson: String)
    data class StackKey(val accountId: UUID, val itemId: String)
    data class StackGrantSnapshot(
        val stacks: List<InventoryStack>,
        val usedSlots: Int,
        val capacityReservedItemIds: Set<String>,
    )

    fun lockAccount(accountId: UUID)
    fun stateVersion(accountId: UUID): Long
    fun incrementStateVersion(accountId: UUID): Long
    fun incrementStateVersions(accountIds: Collection<UUID>) {
        accountIds.distinct().sorted().forEach(::incrementStateVersion)
    }
    fun stacks(accountId: UUID): List<InventoryStack>
    fun instances(accountId: UUID): List<InventoryInstance>
    fun saveStack(stack: InventoryStack)
    fun saveStacks(stacks: List<InventoryStack>) {
        stacks.forEach { saveStack(it) }
    }
    fun addInstance(instance: InventoryInstance)
    fun removeInstances(accountId: UUID, instanceIds: List<UUID>)
    fun setInstancesReserved(accountId: UUID, instanceIds: List<UUID>, reserved: Boolean)
    fun transferInstances(fromAccountId: UUID, toAccountId: UUID, instanceIds: List<UUID>, reserved: Boolean)
    fun hasCapacityReservation(accountId: UUID, itemId: String): Boolean
    fun lockStack(accountId: UUID, itemId: String): InventoryStack?
    fun lockStacks(keys: List<StackKey>): Map<StackKey, InventoryStack> {
        return keys.distinct()
            .mapNotNull { key -> lockStack(key.accountId, key.itemId) }
            .associateBy { StackKey(it.accountId, it.itemId) }
    }
    fun capacityReservedItemIds(accountId: UUID, itemIds: Collection<String>): Set<String> =
        itemIds.distinct().filterTo(linkedSetOf()) { hasCapacityReservation(accountId, it) }
    fun stackGrantSnapshot(accountId: UUID, itemIds: Collection<String>): StackGrantSnapshot = StackGrantSnapshot(
        stacks = stacks(accountId),
        usedSlots = usedSlotCount(accountId),
        capacityReservedItemIds = capacityReservedItemIds(accountId, itemIds),
    )
    fun stackGrantSnapshots(itemIdsByAccount: Map<UUID, Collection<String>>): Map<UUID, StackGrantSnapshot> =
        itemIdsByAccount.mapValues { (accountId, itemIds) -> stackGrantSnapshot(accountId, itemIds) }
    fun usedSlotCount(accountId: UUID): Int
    fun nextAcquiredSequence(accountId: UUID): Long
    fun rewardCommand(accountId: UUID, idempotencyKey: UUID): RewardCommand?
    fun saveRewardCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String)
}

interface MaterialPreferenceProvider {
    fun primaryMaterial(accountId: UUID): MaterialType
}
