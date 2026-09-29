package com.hanjjak.inventory.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.fasterxml.jackson.module.kotlin.registerKotlinModule
import com.hanjjak.inventory.domain.*
import com.hanjjak.inventory.infrastructure.StaticItemCatalog
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class InventoryServicesTest {
    private val accountId = UUID.randomUUID()
    private val repository = MemoryRepository()
    private val catalog = StaticItemCatalog()
    private val rewards = InventoryRewardService(repository, catalog, ObjectMapper().registerKotlinModule())

    @Test
    fun `stack rewards share one slot and retries return the stored result`() {
        val key = UUID.randomUUID()
        val first = rewards.grant(accountId, key, "battle:one", listOf(ItemReward("POTATO_M1", 10), ItemReward("POTATO_M1", 20)))
        val retry = rewards.grant(accountId, key, "battle:one", listOf(ItemReward("POTATO_M1", 30)))
        assertEquals(first, retry)
        assertEquals(30, repository.stacks(accountId).single().quantity)
        assertEquals(1, first.usedSlots)
        assertEquals(2, repository.stateVersion(accountId))
    }

    @Test
    fun `a reused idempotency key with another payload is rejected`() {
        val key = UUID.randomUUID()
        rewards.grant(accountId, key, "battle:one", listOf(ItemReward("POTATO_M1", 10)))
        val error = assertFailsWith<IllegalArgumentException> {
            rewards.grant(accountId, key, "battle:one", listOf(ItemReward("CORN_M1", 10)))
        }
        assertEquals("IDEMPOTENCY_KEY_REUSED", error.message)
    }

    @Test
    fun `full inventory skips all item drops including existing stacks`() {
        repeat(199) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, (it + 1).toLong())) }
        repository.saveStack(InventoryStack(accountId, "POTATO_M1", 5, 0, 200))
        val result = rewards.grant(accountId, UUID.randomUUID(), "battle:full", listOf(ItemReward("POTATO_M1", 10), ItemReward("CORN_M1", 7)))
        assertEquals(5, repository.stacks(accountId).first { it.itemId == "POTATO_M1" }.quantity)
        assertEquals(7, result.rewards.first { it.itemId == "CORN_M1" }.skippedQuantity)
        assertEquals(10, result.rewards.first { it.itemId == "POTATO_M1" }.skippedQuantity)
        assertTrue(result.rewards.all { it.discardedQuantity == 0L && it.grantedQuantity == 0L })
        assertTrue(result.isFull)
    }

    @Test
    fun `reserved quantity is excluded from available consumption`() {
        repository.saveStack(InventoryStack(accountId, "POTATO_M1", 10, 0, 1))
        val service = InventoryReservationService(repository, catalog)
        service.reserveForSale(accountId, "POTATO_M1", 7)
        assertFailsWith<IllegalArgumentException> { service.consumeAvailable(accountId, "POTATO_M1", 4) }
        service.consumeAvailable(accountId, "POTATO_M1", 3)
        assertEquals(7, repository.stacks(accountId).single().quantity)
        assertEquals(0, repository.stacks(accountId).single().availableQuantity)
    }

    @Test
    fun `query filters sorts and paginates with opaque cursor`() {
        repository.saveStack(InventoryStack(accountId, "POTATO_M1", 4, 1, 1))
        repository.saveStack(InventoryStack(accountId, "CORN_M1", 9, 0, 2))
        val query = InventoryQueryService(repository, catalog)
        val first = query.list(accountId, ItemCategory.MATERIAL, ItemSort.QUANTITY_DESC, null, 1)
        val second = query.list(accountId, ItemCategory.MATERIAL, ItemSort.QUANTITY_DESC, first.nextCursor, 1)
        assertEquals("CORN_M1", first.items.single().itemId)
        assertEquals("POTATO_M1", second.items.single().itemId)
        assertEquals(3, second.items.single().availableQuantity)
        assertEquals(2, second.usedSlots)
    }

    @Test
    fun `all or nothing grant rejects new and zero stacks at capacity`() {
        repeat(200) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, it.toLong())) }
        val service = InventoryReservationService(repository, catalog)
        for (zeroStack in listOf(false, true)) {
            if (zeroStack) repository.saveStack(InventoryStack(accountId, "POTATO_M1", 0, 0, 201))
            val error = assertFailsWith<IllegalArgumentException> { service.grantStackOrReject(accountId, "POTATO_M1", 2) }
            assertEquals("INVENTORY_CAPACITY_EXCEEDED", error.message)
            assertEquals(0L, repository.lockStack(accountId, "POTATO_M1")?.quantity ?: 0L)
            assertEquals(1L, repository.stateVersion(accountId))
        }
    }
    @Test
    fun `stack grant plan is all or nothing and performs no writes until applied`() {
        repeat(INVENTORY_MAX_SLOTS - 1) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, it.toLong())) }
        val service = InventoryReservationService(repository, catalog)
        val before = repository.stacks(accountId)
        val plan = service.planStackGrant(accountId, listOf(ItemReward("POTATO_M1", 1), ItemReward("CORN_M1", 1)))
        assertEquals(false, plan.canGrant)
        assertEquals(before, repository.stacks(accountId))
        assertEquals(2, plan.requiredSlots)
        assertEquals(1, plan.availableSlots)
    }

    @Test
    fun `stack grant plan normalizes rewards and applies one complete write set`() {
        val service = InventoryReservationService(repository, catalog)
        val plan = service.planStackGrant(accountId, listOf(ItemReward("CORN_M1", 2), ItemReward("POTATO_M1", 10), ItemReward("CORN_M1", 3)))
        assertEquals(listOf(ItemReward("CORN_M1", 5), ItemReward("POTATO_M1", 10)), plan.items)
        assertTrue(plan.canGrant)
        service.applyStackGrant(accountId, plan)
        assertEquals(5L, repository.lockStack(accountId, "CORN_M1")!!.quantity)
        assertEquals(10L, repository.lockStack(accountId, "POTATO_M1")!!.quantity)
    }

    @Test
    fun `all or nothing grant adds to existing stack at capacity`() {
        repeat(199) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, it.toLong())) }
        repository.saveStack(InventoryStack(accountId, "POTATO_M1", 3, 1, 200))
        InventoryReservationService(repository, catalog).grantStackOrReject(accountId, "POTATO_M1", 201)
        assertEquals(204L, repository.lockStack(accountId, "POTATO_M1")!!.quantity)
        assertEquals(1L, repository.lockStack(accountId, "POTATO_M1")!!.reservedQuantity)
        assertEquals(200, repository.usedSlotCount(accountId))
        assertEquals(2L, repository.stateVersion(accountId))
    }

    @Test
    fun `catalog accepts cosmetic definitions and rejects duplicate ids`() {
        val box = catalog.require("GEM_BOX").copy(itemId = "selector-test", category = ItemCategory.COSMETIC_BOX)
        assertEquals(box, StaticItemCatalog(listOf(box)).require(box.itemId))
        assertFailsWith<IllegalArgumentException> { StaticItemCatalog(listOf(box, box)) }
        assertFailsWith<IllegalArgumentException> { StaticItemCatalog(listOf(catalog.require("GEM_BOX"))) }
    }

    @Test
    fun `skillbook display names follow the content contract`() {
        assertEquals("노말 한짝의 일격 비법서", catalog.require("skillbook:active_heavy:normal").displayName)
        assertEquals("영웅 화력 최대로! 비법서", catalog.require("skillbook:active_basic_amp:epic").displayName)
    }

    @Test
    fun `material descriptions omit display names and end as noun phrases`() {
        val families = listOf("POTATO", "SWEET_POTATO", "CORN")

        for (generation in 1..5) {
            families.forEach { family ->
                assertEquals(
                    "장비 제작과 강화에 사용하는 ${generation}세대 재료",
                    catalog.require("${family}_M$generation").description,
                )
            }
        }
    }

    @Test
    fun `market escrows purchases and reclaims tradeable skillbooks`() {
        val seller = UUID.randomUUID()
        val buyer = UUID.randomUUID()
        val itemId = "skillbook:active_heavy:normal"
        val service = MarketInventoryService(repository, catalog)
        repository.saveStack(InventoryStack(seller, itemId, 7, 0, 1))

        service.reserveForSale(seller, itemId, 5)
        assertEquals(2L, repository.lockStack(seller, itemId)!!.quantity)
        service.transferReserveds(listOf(MarketInventoryService.ReservedTransfer(seller, buyer, itemId, 3)))
        assertEquals(3L, repository.lockStack(buyer, itemId)!!.quantity)
        service.releaseReservation(seller, itemId, 2)

        assertEquals(4L, repository.lockStack(seller, itemId)!!.quantity)
        assertEquals(1, repository.usedSlotCount(seller))
        assertEquals(1, repository.usedSlotCount(buyer))
    }

    @Test
    fun `market skillbook credit respects shared inventory capacity without mutation`() {
        val seller = UUID.randomUUID()
        val buyer = UUID.randomUUID()
        val itemId = "skillbook:active_heavy:normal"
        repeat(INVENTORY_MAX_SLOTS) {
            repository.addInstance(InventoryInstance(UUID.randomUUID(), buyer, "gem:1:flat_attack", false, it.toLong()))
        }
        val service = MarketInventoryService(repository, catalog)

        assertEquals(
            "INVENTORY_CAPACITY_EXCEEDED",
            assertFailsWith<IllegalArgumentException> { service.transferReserved(seller, buyer, itemId, 1) }.message,
        )
        assertEquals(null, repository.lockStack(buyer, itemId))
        assertEquals(INVENTORY_MAX_SLOTS, repository.usedSlotCount(buyer))
    }

    @Test
    fun `market rejects non-tradeable stack assets for every escrow operation`() {
        val seller = UUID.randomUUID()
        val buyer = UUID.randomUUID()
        val service = MarketInventoryService(repository, catalog)
        repository.saveStack(InventoryStack(seller, "GEM_BOX", 3, 0, 1))

        val operations = listOf<() -> Unit>(
            { service.reserveForSale(seller, "GEM_BOX", 1) },
            { service.transferReserved(seller, buyer, "GEM_BOX", 1) },
            { service.transferReserveds(listOf(MarketInventoryService.ReservedTransfer(seller, buyer, "GEM_BOX", 1))) },
            { service.releaseReservation(seller, "GEM_BOX", 1) },
        )

        operations.forEach { operation ->
            assertEquals("ITEM_NOT_TRADEABLE", assertFailsWith<IllegalArgumentException>(block = operation).message)
        }
        assertEquals(3L, repository.lockStack(seller, "GEM_BOX")!!.quantity)
        assertEquals(null, repository.lockStack(buyer, "GEM_BOX"))
    }

    @Test
    fun `market rejects zero quantity buyer stack at full capacity for single and batch transfers`() {
        val seller = UUID.randomUUID()
        repeat(200) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, it.toLong())) }
        repository.saveStack(InventoryStack(seller, "POTATO_M1", 3, 3, 1))
        // Keep both owners' rows in a dedicated repository to exercise the zero-row path.
        val rows = mapOf(
            InventoryRepository.StackKey(seller, "POTATO_M1") to InventoryStack(seller, "POTATO_M1", 3, 3, 1),
            InventoryRepository.StackKey(accountId, "POTATO_M1") to InventoryStack(accountId, "POTATO_M1", 0, 0, 1),
        )
        val fixture = object : InventoryRepository by repository {
            override fun lockStack(accountId: UUID, itemId: String) = rows[InventoryRepository.StackKey(accountId, itemId)]
            override fun lockStacks(keys: List<InventoryRepository.StackKey>) = rows.filterKeys { it in keys }
        }
        val service = MarketInventoryService(fixture, catalog)
        assertEquals("INVENTORY_CAPACITY_EXCEEDED", assertFailsWith<IllegalArgumentException> { service.transferReserved(seller, accountId, "POTATO_M1", 1) }.message)
        assertEquals("INVENTORY_CAPACITY_EXCEEDED", assertFailsWith<IllegalArgumentException> { service.transferReserveds(listOf(MarketInventoryService.ReservedTransfer(seller, accountId, "POTATO_M1", 1))) }.message)
    }

    @Test
    fun `material boundary grants consume slots and reject overflow without mutation`() {
        val service = InventoryReservationService(repository, catalog)
        service.grantStackOrReject(accountId, "POTATO_M1", 999)
        assertEquals(1, repository.usedSlotCount(accountId))
        service.grantStackOrReject(accountId, "POTATO_M1", 1)
        assertEquals(2, repository.usedSlotCount(accountId))
        service.consumeAvailable(accountId, "POTATO_M1", 1)
        assertEquals(1, repository.usedSlotCount(accountId))
        repeat(199) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, it.toLong())) }
        assertFailsWith<IllegalArgumentException> { service.grantStackOrReject(accountId, "POTATO_M1", 1) }
        assertEquals(999, repository.lockStack(accountId, "POTATO_M1")!!.quantity)
    }

    @Test
    fun `stack grant preview is read only and carries virtual capacity across bundles`() {
        repeat(199) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, it.toLong())) }
        val readOnlyRepository = object : InventoryRepository by repository {
            override fun lockAccount(accountId: UUID): Nothing = error("preview must not lock account")
            override fun lockStack(accountId: UUID, itemId: String): Nothing = error("preview must not lock stacks")
            override fun lockStacks(keys: List<InventoryRepository.StackKey>): Nothing = error("preview must not lock stacks")
        }
        val service = InventoryReservationService(readOnlyRepository, catalog)

        val previews = service.previewStackGrants(
            accountId,
            listOf(
                listOf(ItemReward("POTATO_M1", 999)),
                listOf(ItemReward("POTATO_M1", 1)),
                listOf(ItemReward("CORN_M1", 1)),
            ),
        )

        assertEquals(listOf(true, false, false), previews.map { it.canGrant })
        assertEquals(0, repository.stacks(accountId).size)
    }

    @Test
    fun `material partial reward fills final slot and replay preserves skipped quantity`() {
        repeat(198) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, it.toLong())) }
        repository.saveStack(InventoryStack(accountId, "POTATO_M1", 998, 0, 200))
        val key = UUID.randomUUID()
        val result = rewards.grant(accountId, key, "boundary", listOf(ItemReward("POTATO_M1", 1001)))
        assertEquals(1000, result.rewards.single().grantedQuantity)
        assertEquals(1, result.rewards.single().skippedQuantity)
        assertEquals(200, result.usedSlots)
        assertEquals(result, rewards.grant(accountId, key, "boundary", listOf(ItemReward("POTATO_M1", 1001))))
    }

    @Test
    fun `gem groups preserve ids and distinguish values at the stack boundary`() {
        val service = InventoryInstanceService(repository, catalog)
        repeat(99) { service.grant(accountId, UUID.randomUUID(), "gem:1:flat_attack", "1:FLAT_ATTACK:3") }
        assertEquals(1, repository.usedSlotCount(accountId))
        service.grant(accountId, UUID.randomUUID(), "gem:1:flat_attack", "1:FLAT_ATTACK:3")
        assertEquals(2, repository.usedSlotCount(accountId))
        service.grant(accountId, UUID.randomUUID(), "gem:1:flat_attack", "1:FLAT_ATTACK:4")
        assertEquals(3, repository.usedSlotCount(accountId))
        assertEquals(101, repository.instances(accountId).map { it.instanceId }.distinct().size)
        service.consume(accountId, listOf(repository.instances(accountId).first().instanceId))
        assertEquals(3, repository.usedSlotCount(accountId))
    }

    @Test
    fun `query splits material slots and preserves aggregate reservation totals`() {
        repository.saveStack(InventoryStack(accountId, "POTATO_M1", 2000, 1000, 1))
        val query = InventoryQueryService(repository, catalog)
        val page = query.list(accountId, ItemCategory.MATERIAL, ItemSort.NAME_ASC, null, 100)
        assertEquals(listOf(999L, 999L, 2L), page.items.map { it.totalQuantity })
        assertEquals(listOf(999L, 1L, 0L), page.items.map { it.reservedQuantity })
        assertEquals(3, page.items.map { it.slotId }.distinct().size)
        assertEquals(3, page.usedSlots)
        val detail = query.detail(accountId, "POTATO_M1")
        assertEquals(2000L, detail.totalQuantity)
        assertEquals(1000L, detail.availableQuantity)
        assertEquals(null, detail.slotId)
        assertTrue(detail.members.isEmpty())
    }

    @Test
    fun `query groups gems by actual value preserves members and paginates slots`() {
        repeat(100) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", it % 2 == 0, it.toLong(), "1:FLAT_ATTACK:3")) }
        repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, 100, "1:FLAT_ATTACK:4"))
        repeat(2) { repository.addInstance(InventoryInstance(UUID.randomUUID(), accountId, "gem:1:flat_attack", false, 101L + it)) }
        val query = InventoryQueryService(repository, catalog)
        val rows = mutableListOf<InventoryItem>()
        var cursor: String? = null
        do {
            val page = query.list(accountId, ItemCategory.GEM, ItemSort.QUANTITY_DESC, cursor, 2)
            rows += page.items
            cursor = page.nextCursor
        } while (cursor != null)
        assertEquals(listOf(99L, 1L, 1L, 1L, 1L), rows.map { it.totalQuantity })
        assertEquals(5, rows.map { it.slotId }.distinct().size)
        assertEquals(103, rows.flatMap { it.members }.map { it.instanceId }.distinct().size)
        assertEquals(50L, rows.sumOf { it.reservedQuantity })
        assertEquals(50, rows.flatMap { it.members }.count { it.reservedForSale })
        assertEquals(103L, query.detail(accountId, "gem:1:flat_attack").totalQuantity)
        assertEquals(5, query.status(accountId).usedSlots)
    }
    @Test
    fun `batch account version update deduplicates accounts`() {
        val first = UUID.randomUUID()
        val second = UUID.randomUUID()
        val versions = mutableMapOf(first to 1L, second to 1L)
        val repo = object : InventoryRepository by MemoryRepository() {
            override fun incrementStateVersion(accountId: UUID): Long = versions.compute(accountId) { _, value -> value!! + 1L }!!
            override fun incrementStateVersions(accountIds: Collection<UUID>) {
                accountIds.distinct().sorted().forEach(::incrementStateVersion)
            }
        }

        repo.incrementStateVersions(listOf(first, first, second))

        assertEquals(2L, versions[first])
        assertEquals(2L, versions[second])
    }


    private class MemoryRepository : InventoryRepository {
        private val stacks = mutableMapOf<InventoryRepository.StackKey, InventoryStack>()
        private val instances = mutableListOf<InventoryInstance>()
        private val commands = mutableMapOf<UUID, InventoryRepository.RewardCommand>()
        private var version = 1L
        override fun lockAccount(accountId: UUID) = Unit
        override fun stateVersion(accountId: UUID) = version
        override fun incrementStateVersion(accountId: UUID): Long = ++version
        override fun stacks(accountId: UUID) = stacks.values.filter { it.accountId == accountId }
        override fun instances(accountId: UUID) = instances.filter { it.accountId == accountId }
        override fun saveStack(stack: InventoryStack) { stacks[InventoryRepository.StackKey(stack.accountId, stack.itemId)] = stack }
        override fun addInstance(instance: InventoryInstance) { instances += instance }
        override fun removeInstances(accountId: UUID, instanceIds: List<UUID>) { instances.removeAll { it.accountId == accountId && it.instanceId in instanceIds } }
        override fun setInstancesReserved(accountId: UUID, instanceIds: List<UUID>, reserved: Boolean) {
            instanceIds.forEach { id ->
                val index = instances.indexOfFirst { it.accountId == accountId && it.instanceId == id }
                require(index >= 0) { "INSTANCE_NOT_AVAILABLE" }
                instances[index] = instances[index].copy(reservedForSale = reserved)
            }
        }
        override fun transferInstances(fromAccountId: UUID, toAccountId: UUID, instanceIds: List<UUID>, reserved: Boolean) {
            instanceIds.forEach { id ->
                val index = instances.indexOfFirst { it.accountId == fromAccountId && it.instanceId == id }
                require(index >= 0) { "INSTANCE_NOT_AVAILABLE" }
                instances[index] = instances[index].copy(accountId = toAccountId, reservedForSale = reserved)
            }
        }
        override fun hasCapacityReservation(accountId: UUID, itemId: String) = false
        override fun lockStack(accountId: UUID, itemId: String) = stacks[InventoryRepository.StackKey(accountId, itemId)]
        override fun usedSlotCount(accountId: UUID) = Math.toIntExact(stacks(accountId).sumOf { InventorySlots.stackSlots(it.quantity, StaticItemCatalog().require(it.itemId)) } + InventorySlots.instanceSlots(instances(accountId)))
        override fun nextAcquiredSequence(accountId: UUID): Long {
            val stackMax = stacks.values.filter { it.accountId == accountId }.maxOfOrNull { it.acquiredSequence } ?: 0L
            val instanceMax = instances.filter { it.accountId == accountId }.maxOfOrNull { it.acquiredSequence } ?: 0L
            return maxOf(stackMax, instanceMax) + 1
        }
        override fun rewardCommand(accountId: UUID, idempotencyKey: UUID) = commands[idempotencyKey]
        override fun saveRewardCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) {
            commands[idempotencyKey] = InventoryRepository.RewardCommand(fingerprint, resultJson)
        }
    }
}
