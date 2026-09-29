package com.hanjjak.inventory.application

import com.hanjjak.inventory.domain.*
import java.nio.charset.StandardCharsets
import java.util.Base64
import java.util.UUID

class InventoryQueryService(private val repository: InventoryRepository, private val catalog: ItemCatalog) {
    fun list(accountId: UUID, category: ItemCategory?, sort: ItemSort, cursor: String?, limit: Int): InventoryPage {
        require(limit in 1..100) { "INVALID_LIMIT" }
        val state = snapshot(accountId)
        val sorted = state.items.asSequence()
            .filter { category == null || it.category == category }
            .sortedWith(comparator(sort))
            .toList()
        val offset = decodeCursor(cursor)
        require(offset in 0..sorted.size) { "INVALID_CURSOR" }
        val page = sorted.drop(offset).take(limit)
        val nextOffset = offset + page.size
        return InventoryPage(page, state.status.usedSlots, state.status.maxSlots, state.status.isFull, if (nextOffset < sorted.size) encodeCursor(nextOffset) else null)
    }

    fun detail(accountId: UUID, itemId: String): InventoryItem {
        require(ITEM_ID.matches(itemId)) { "INVALID_ITEM_ID" }
        val definition = catalog.require(itemId)
        val owned = snapshot(accountId).items.filter { it.itemId == itemId }
        return item(definition, owned.sumOf { it.totalQuantity }, owned.sumOf { it.reservedQuantity }, owned.maxOfOrNull { it.acquiredSequence } ?: 0)
    }

    fun status(accountId: UUID): InventoryStatus = InventoryStatus(repository.usedSlotCount(accountId))

    private data class Snapshot(val items: List<InventoryItem>, val status: InventoryStatus)

    private fun snapshot(accountId: UUID): Snapshot {
        val stacks = repository.stacks(accountId).filter { it.quantity > 0 }.associateBy { it.itemId }
        val instances = repository.instances(accountId)
        val items = stacks.values.flatMap { stack ->
            val definition = catalog.require(stack.itemId)
            val limit = InventorySlots.limit(definition)
            List(Math.toIntExact(InventorySlots.count(stack.quantity, limit))) { index ->
                val start = index.toLong() * limit
                val quantity = minOf(limit, stack.quantity - start)
                // Reservations belong to the aggregate stack; distribute them in slot order.
                val reserved = (stack.reservedQuantity - start).coerceIn(0, quantity)
                item(definition, quantity, reserved, stack.acquiredSequence)
                    .copy(slotId = "stack:${stack.itemId}:$index")
            }
        } + InventorySlots.instanceGroups(instances).map { members ->
                        val first = members.first()
                        item(catalog.require(first.itemId), members.size.toLong(), members.count { it.reservedForSale }.toLong(), members.maxOf { it.acquiredSequence })
                            .copy(instanceId = members.singleOrNull()?.instanceId,
                                slotId = "instance:${first.inventorySlotId ?: first.instanceId}",
                                locked = first.locked,
                                members = members.map { InventoryMember(it.instanceId, it.reservedForSale) })
        }
        return Snapshot(items, InventoryStatus(repository.usedSlotCount(accountId)))
    }

    private fun item(definition: ItemDefinition, total: Long, reserved: Long, sequence: Long) = InventoryItem(
        definition.itemId,
        definition.displayName,
        definition.icon,
        definition.category,
        definition.description,
        definition.acquisitionSources,
        definition.usages,
        definition.tradeable,
        total,
        reserved,
        total - reserved,
        sequence,
    )

    private fun comparator(sort: ItemSort): Comparator<InventoryItem> {
        val itemIdTieBreak = compareBy<InventoryItem> { it.itemId }
        val primary = when (sort) {
            ItemSort.NAME_ASC -> compareBy(String.CASE_INSENSITIVE_ORDER) { it.name }
            ItemSort.NAME_DESC -> compareByDescending<InventoryItem> { it.name.lowercase() }
            ItemSort.QUANTITY_ASC -> compareBy { it.totalQuantity }
            ItemSort.QUANTITY_DESC -> compareByDescending { it.totalQuantity }
            ItemSort.ACQUIRED_DESC -> compareByDescending { it.acquiredSequence }
        }
        return primary.then(itemIdTieBreak).thenBy { it.slotId }
    }

    private fun encodeCursor(offset: Int): String = Base64.getUrlEncoder().withoutPadding().encodeToString("inventory-v2:$offset".toByteArray(StandardCharsets.UTF_8))

    private fun decodeCursor(cursor: String?): Int {
        if (cursor == null) return 0
        val decoded = runCatching { String(Base64.getUrlDecoder().decode(cursor), StandardCharsets.UTF_8) }.getOrElse { throw IllegalArgumentException("INVALID_CURSOR") }
        require(decoded.startsWith("inventory-v2:")) { "INVALID_CURSOR" }
        return decoded.substringAfter(':').toIntOrNull() ?: throw IllegalArgumentException("INVALID_CURSOR")
    }

    companion object {
        private val ITEM_ID = Regex("[A-Za-z0-9][A-Za-z0-9:_-]{0,119}")
    }
}
