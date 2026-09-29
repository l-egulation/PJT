package com.hanjjak.inventory.domain

object InventorySlots {
    const val GEM_LIMIT = 99L
    const val MATERIAL_LIMIT = 999L

    fun limit(definition: ItemDefinition): Long = if (definition.category == ItemCategory.MATERIAL) MATERIAL_LIMIT else Long.MAX_VALUE

    fun count(quantity: Long, limit: Long): Long {
        require(quantity >= 0 && limit > 0) { "INVALID_QUANTITY" }
        return if (quantity == 0L) 0 else (quantity - 1) / limit + 1
    }

    fun stackSlots(quantity: Long, definition: ItemDefinition, reservedSlot: Boolean = false): Long =
        maxOf(count(quantity, limit(definition)), if (reservedSlot) 1L else 0L)

    fun instanceGroups(instances: List<InventoryInstance>): List<List<InventoryInstance>> =
        instances.groupBy { it.inventorySlotId?.toString() ?: "${it.itemId}:${it.stackKey ?: it.instanceId}:${it.locked}" }
            .values.flatMap { group -> group.sortedWith(compareBy<InventoryInstance> { it.acquiredSequence }.thenBy { it.instanceId }).chunked(GEM_LIMIT.toInt()) }

    fun instanceSlots(instances: List<InventoryInstance>): Long = instanceGroups(instances).size.toLong()

    fun gemKey(level: Int, option: String, value: Int): String = "$level:$option:$value"
}
