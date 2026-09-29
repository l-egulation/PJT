package com.hanjjak.battle.application

import com.hanjjak.battle.domain.PendingFirstClearReward
import com.hanjjak.inventory.application.InventoryQueryService
import com.hanjjak.inventory.application.InventoryRepository
import com.hanjjak.inventory.domain.InventoryInstance
import com.hanjjak.inventory.domain.InventoryStack
import com.hanjjak.inventory.domain.ItemReward
import com.hanjjak.inventory.infrastructure.StaticItemCatalog
import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals

class FirstClearRewardPresenterTest {
    @Test
    fun `pending page reuses one inventory snapshot snapshot`() {
        val accountId = UUID.randomUUID()
        val inventory = CountingInventoryRepository()
        val presenter = FirstClearRewardPresenter(InventoryQueryService(inventory, StaticItemCatalog()), StaticItemCatalog())
        val rewards = listOf(
            pending(accountId, "stage.01-01"),
            pending(accountId, "stage.01-02"),
        )

        val views = presenter.presentPending(rewards)

        assertEquals(listOf(1, 1), views.map { it.availableSlots })
        assertEquals(0, inventory.stacksReads)
        assertEquals(0, inventory.instancesReads)
        assertEquals(1, inventory.slotCountReads)

        presenter.presentPending(emptyList())
        assertEquals(1, inventory.slotCountReads)
    }

    private fun pending(accountId: UUID, stageId: String) = PendingFirstClearReward(
        rewardId = UUID.randomUUID(),
        accountId = accountId,
        stageId = stageId,
        rewardVersion = "progression-rebalance-v1",
        sourceId = UUID.randomUUID(),
        riceGranted = 0,
        unlockedSkillId = null,
        items = listOf(ItemReward("POTATO_M1", 1)),
        requiredSlots = 2,
        claimedAt = null,
    )

    private class CountingInventoryRepository : InventoryRepository {
        var stacksReads = 0
        var instancesReads = 0
        var slotCountReads = 0

        override fun lockAccount(accountId: UUID) = Unit
        override fun stateVersion(accountId: UUID) = 1L
        override fun incrementStateVersion(accountId: UUID) = 2L
        override fun stacks(accountId: UUID): List<InventoryStack> {
            stacksReads++
            return emptyList()
        }
        override fun instances(accountId: UUID): List<InventoryInstance> {
            instancesReads++
            return emptyList()
        }
        override fun saveStack(stack: InventoryStack) = Unit
        override fun addInstance(instance: InventoryInstance) = Unit
        override fun removeInstances(accountId: UUID, instanceIds: List<UUID>) = Unit
        override fun setInstancesReserved(accountId: UUID, instanceIds: List<UUID>, reserved: Boolean) = Unit
        override fun transferInstances(fromAccountId: UUID, toAccountId: UUID, instanceIds: List<UUID>, reserved: Boolean) = Unit
        override fun hasCapacityReservation(accountId: UUID, itemId: String) = false
        override fun lockStack(accountId: UUID, itemId: String): InventoryStack? = null
        override fun usedSlotCount(accountId: UUID): Int {
            slotCountReads++
            return 199
        }
        override fun nextAcquiredSequence(accountId: UUID) = 1L
        override fun rewardCommand(accountId: UUID, idempotencyKey: UUID): InventoryRepository.RewardCommand? = null
        override fun saveRewardCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) = Unit
    }
}
