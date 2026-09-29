package com.hanjjak.inventory.domain

import java.util.UUID

const val INVENTORY_MAX_SLOTS = 200

enum class ItemCategory { MATERIAL, SKILL_BOOK, GEM, GEM_BOX, COSMETIC_BOX }
enum class ItemSort { NAME_ASC, NAME_DESC, QUANTITY_ASC, QUANTITY_DESC, ACQUIRED_DESC }
enum class MaterialType { POTATO, SWEET_POTATO, CORN }

data class ItemDefinition(
    val itemId: String,
    val displayName: String,
    val icon: String,
    val category: ItemCategory,
    val description: String,
    val acquisitionSources: List<String>,
    val usages: List<String>,
    val tradeable: Boolean,
    val stackable: Boolean,
)

data class InventoryStack(
    val accountId: UUID,
    val itemId: String,
    val quantity: Long,
    val reservedQuantity: Long,
    val acquiredSequence: Long,
) {
    init {
        require(quantity >= 0) { "INVALID_QUANTITY" }
        require(reservedQuantity in 0..quantity) { "INVALID_RESERVED_QUANTITY" }
    }
    val availableQuantity: Long get() = quantity - reservedQuantity
}

data class InventoryInstance(
    val instanceId: UUID,
    val accountId: UUID,
    val itemId: String,
    val reservedForSale: Boolean,
    val acquiredSequence: Long,
    val stackKey: String? = null,
    val inventorySlotId: UUID? = null,
    val locked: Boolean = false,
)

data class InventoryItem(
    val itemId: String,
    val name: String,
    val icon: String,
    val category: ItemCategory,
    val description: String,
    val acquisitionSources: List<String>,
    val usages: List<String>,
    val tradeable: Boolean,
    val totalQuantity: Long,
    val reservedQuantity: Long,
    val availableQuantity: Long,
    val acquiredSequence: Long,
    val instanceId: UUID? = null,
    val slotId: String? = null,
    val members: List<InventoryMember> = emptyList(),
    val locked: Boolean = false,
)

data class InventoryMember(val instanceId: UUID, val reservedForSale: Boolean)

data class InventoryStatus(val usedSlots: Int, val maxSlots: Int = INVENTORY_MAX_SLOTS) {
    init { require(usedSlots in 0..maxSlots) { "INVENTORY_CAPACITY_EXCEEDED" } }
    val isFull: Boolean get() = usedSlots == maxSlots
}

data class InventoryPage(
    val items: List<InventoryItem>,
    val usedSlots: Int,
    val maxSlots: Int,
    val isFull: Boolean,
    val nextCursor: String?,
)

data class ItemReward(val itemId: String, val quantity: Long) {
    init { require(quantity > 0) { "INVALID_QUANTITY" } }
}

data class RewardLine(
    val itemId: String,
    val requestedQuantity: Long,
    val grantedQuantity: Long,
    val discardedQuantity: Long,
    val skippedQuantity: Long = 0,
)

data class RewardResult(
    val rewards: List<RewardLine>,
    val usedSlots: Int,
    val maxSlots: Int,
    val isFull: Boolean,
)
