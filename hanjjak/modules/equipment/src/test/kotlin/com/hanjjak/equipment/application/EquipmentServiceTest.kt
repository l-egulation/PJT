package com.hanjjak.equipment.application

import com.fasterxml.jackson.module.kotlin.jacksonObjectMapper
import com.hanjjak.events.application.DomainEventPublishRequest
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.equipment.domain.EquipmentSlotState
import com.hanjjak.equipment.domain.EquipmentStatSummary
import com.hanjjak.equipment.domain.MaterialCost
import com.hanjjak.wallet.application.WalletDebitEntry
import com.hanjjak.wallet.application.WalletRepository
import com.hanjjak.wallet.application.WalletService
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNotNull

class EquipmentServiceTest {
    private val accountId = UUID(0, 1)
    private val key = UUID(0, 2)
    private val repository = FakeEquipmentRepository(accountId)
    private val walletRepository = FakeWalletRepository(accountId, repository)
    private val events = RecordingDomainEventPublisher()
    private val service = EquipmentService(repository, jacksonObjectMapper(), WalletService(walletRepository), events)

    @Test
    fun `unlock charges once and same idempotency key replays result`() {
        repository.rice = 10_000
        repository.seedMaterials(10_000)

        val first = service.unlock(accountId, key, EquipmentSlot.WEAPON)
        val replay = service.unlock(accountId, key, EquipmentSlot.WEAPON)

        assertEquals(first, replay)
        assertEquals(1, repository.consumeCalls)
        assertEquals(1, repository.version)
        assertEquals(9_900, repository.rice)
        assertEquals(1, walletRepository.ledger.size)
        assertEquals(-100, walletRepository.ledger.single().amount)
        assertEquals(9_990, repository.materials["POTATO_M1"])
        assertEquals(6, first.state.slots.size)
        assertEquals(1, events.requests.size)
        val event = events.requests.single()
        assertEquals("EQUIPMENT_CRAFTED", event.eventType)
        assertEquals(accountId, event.accountId)
        assertEquals("equipment:weapon:normal", event.itemId)
        assertEquals("WEAPON", event.payload["equipmentSlot"])
        assertEquals("NORMAL", event.payload["gradeAfter"])
        assertEquals(1, event.payload["levelAfter"])
        assertEquals(100L, event.payload["riceCost"])
    }

    @Test
    fun `slot lifecycle events share a stable aggregate identity`() {
        repository.rice = 10_000
        repository.seedMaterials(10_000)

        service.unlock(accountId, key, EquipmentSlot.WEAPON)
        service.enhance(accountId, UUID(0, 3), EquipmentSlot.WEAPON)

        assertEquals(2, events.requests.size)
        assertEquals(events.requests[0].aggregateId, events.requests[1].aggregateId)
        assertEquals(accountId, events.requests[0].accountId)
        assertEquals("WEAPON", events.requests[0].payload["equipmentSlot"])
        assertEquals("WEAPON", events.requests[1].payload["equipmentSlot"])
    }

    @Test
    fun `same key with another slot is rejected`() {
        repository.rice = 10_000
        repository.seedMaterials(10_000)
        service.unlock(accountId, key, EquipmentSlot.WEAPON)

        val error = assertFailsWith<IllegalArgumentException> {
            service.unlock(accountId, key, EquipmentSlot.GLOVES)
        }

        assertEquals("IDEMPOTENCY_KEY_REUSED", error.message)
        assertEquals(1, repository.consumeCalls)
    }

    @Test
    fun `unlocking an owned slot is rejected before charging`() {
        repository.rice = 10_000
        repository.seedMaterials(10_000)
        repository.states[EquipmentSlot.WEAPON] = slot(EquipmentGrade.NORMAL, 1)

        val error = assertFailsWith<IllegalArgumentException> {
            service.unlock(accountId, key, EquipmentSlot.WEAPON)
        }

        assertEquals("EQUIPMENT_ALREADY_UNLOCKED", error.message)
        assertEquals(0, repository.consumeCalls)
        assertEquals(0, repository.version)
    }

    @Test
    fun `enhancement consumes the current grade cost and advances exactly once`() {
        repository.rice = 10_000
        repository.seedMaterials(10_000)
        repository.states[EquipmentSlot.WEAPON] = slot(EquipmentGrade.RARE, 1)

        val result = service.enhance(accountId, key, EquipmentSlot.WEAPON)

        assertEquals(EquipmentGrade.RARE, result.slot.current.grade)
        assertEquals(2, result.slot.current.enhancementLevel)
        assertEquals(1, repository.consumeCalls)
        assertEquals(1, repository.version)
        assertEquals(1, walletRepository.ledger.size)
        assertEquals(-930, walletRepository.ledger.single().amount)
        val event = events.requests.single()
        assertEquals("EQUIPMENT_ENHANCEMENT_ATTEMPTED", event.eventType)
        assertEquals("WEAPON", event.payload["equipmentSlot"])
        assertEquals("RARE", event.payload["gradeBefore"])
        assertEquals("RARE", event.payload["gradeAfter"])
        assertEquals(1, event.payload["levelBefore"])
        assertEquals(2, event.payload["levelAfter"])
        assertEquals(930L, event.payload["riceCost"])
    }

    @Test
    fun `enhancement rejects locked and maximum slots before charging`() {
        assertEquals("EQUIPMENT_NOT_UNLOCKED", assertFailsWith<IllegalArgumentException> {
            service.enhance(accountId, key, EquipmentSlot.WEAPON)
        }.message)
        repository.states[EquipmentSlot.WEAPON] = slot(EquipmentGrade.NORMAL, 30)
        assertEquals("EQUIPMENT_MAX_ENHANCEMENT", assertFailsWith<IllegalArgumentException> {
            service.enhance(accountId, UUID(0, 3), EquipmentSlot.WEAPON)
        }.message)
        assertEquals(0, repository.consumeCalls)
    }

    @Test
    fun `promotion requires maximum level then chapter and returns next grade one`() {
        repository.rice = 10_000
        repository.seedMaterials(10_000)
        repository.states[EquipmentSlot.WEAPON] = slot(EquipmentGrade.NORMAL, 29)
        assertEquals("EQUIPMENT_MAX_ENHANCEMENT_REQUIRED", assertFailsWith<IllegalArgumentException> {
            service.promote(accountId, key, EquipmentSlot.WEAPON)
        }.message)

        repository.states[EquipmentSlot.WEAPON] = slot(EquipmentGrade.NORMAL, 30)
        assertEquals("EQUIPMENT_CHAPTER_NOT_CLEARED", assertFailsWith<IllegalArgumentException> {
            service.promote(accountId, UUID(0, 3), EquipmentSlot.WEAPON)
        }.message)

        repository.firstCleared += "stage.01-10"
        val result = service.promote(accountId, UUID(0, 4), EquipmentSlot.WEAPON)

        assertEquals(9_000, repository.rice)
        assertEquals(9_990, repository.materials["POTATO_M2"])
        assertEquals(1, repository.consumeCalls)
        assertEquals(1, repository.version)
        assertEquals(1, walletRepository.ledger.size)
        assertEquals(-1_000, walletRepository.ledger.single().amount)
        val event = events.requests.single()
        assertEquals("EQUIPMENT_PROMOTED", event.eventType)
        assertEquals("WEAPON", event.payload["equipmentSlot"])
        assertEquals("NORMAL", event.payload["gradeBefore"])
        assertEquals("RARE", event.payload["gradeAfter"])
        assertEquals(30, event.payload["levelBefore"])
        assertEquals(1, event.payload["levelAfter"])
        assertEquals(1_000L, event.payload["riceCost"])
    }

    @Test
    fun `legendary thirty rejects further growth`() {
        repository.states[EquipmentSlot.WEAPON] = slot(EquipmentGrade.LEGENDARY, 30)

        assertEquals("EQUIPMENT_MAX_ENHANCEMENT", assertFailsWith<IllegalArgumentException> {
            service.enhance(accountId, key, EquipmentSlot.WEAPON)
        }.message)
        assertEquals("EQUIPMENT_MAX_GRADE", assertFailsWith<IllegalArgumentException> {
            service.promote(accountId, UUID(0, 3), EquipmentSlot.WEAPON)
        }.message)
        assertEquals(0, repository.consumeCalls)
    }

    @Test
    fun `state provides server-authored locked enhancement promotion and terminal actions`() {
        repository.rice = 2_000
        repository.seedMaterials(1_000)
        repository.states[EquipmentSlot.GLOVES] = slot(EquipmentGrade.NORMAL, 1, EquipmentSlot.GLOVES)
        repository.states[EquipmentSlot.ARMOR] = slot(EquipmentGrade.NORMAL, 30, EquipmentSlot.ARMOR)
        repository.states[EquipmentSlot.HELMET] = slot(EquipmentGrade.LEGENDARY, 30, EquipmentSlot.HELMET)

        val state = service.state(accountId)
        val locked = state.slots.first { it.slot == EquipmentSlot.WEAPON }
        val enhancement = state.slots.first { it.slot == EquipmentSlot.GLOVES }
        val promotion = state.slots.first { it.slot == EquipmentSlot.ARMOR }
        val terminal = state.slots.first { it.slot == EquipmentSlot.HELMET }

        assertEquals(0, locked.current.q)
        assertNotNull(locked.unlock)
        assertEquals(EquipmentGrade.NORMAL, locked.unlock.result.grade)
        assertNotNull(enhancement.enhance)
        assertEquals(2, enhancement.enhance.result.enhancementLevel)
        assertEquals(EquipmentStatSummary(1, 0, 0), enhancement.enhance.statIncrease)
        assertEquals("EQUIPMENT_CHAPTER_NOT_CLEARED", promotion.promote?.disabledReason)
        assertEquals(true, terminal.growthComplete)
        assertEquals(null, terminal.enhance)
        assertEquals(null, terminal.promote)
    }

    @Test
    fun `glove enhancement preview equals the committed stat increase`() {
        repository.rice = 10_000
        repository.seedMaterials(10_000)
        repository.states[EquipmentSlot.GLOVES] = slot(EquipmentGrade.NORMAL, 1, EquipmentSlot.GLOVES)

        val before = service.state(accountId).slots.first { it.slot == EquipmentSlot.GLOVES }
        val result = service.enhance(accountId, key, EquipmentSlot.GLOVES)

        assertEquals(1, before.enhance?.statIncrease?.attack)
        assertEquals(
            before.enhance?.statIncrease?.attack,
            result.slot.current.stats.attack - before.current.stats.attack,
        )
        assertEquals(2, result.slot.current.enhancementLevel)
    }

    private fun slot(grade: EquipmentGrade, level: Int, slot: EquipmentSlot = EquipmentSlot.WEAPON) =
        EquipmentSlotState(accountId, slot, grade, level)
}

private class RecordingDomainEventPublisher : DomainEventPublisher {
    val requests = mutableListOf<DomainEventPublishRequest>()

    override fun publish(request: DomainEventPublishRequest): UUID {
        requests += request
        return UUID.randomUUID()
    }
}

private class FakeEquipmentRepository(private val accountId: UUID) : EquipmentRepository {
    var rice = 0L
    var version = 0L
    var consumeCalls = 0
    val materials = mutableMapOf<String, Long>()
    val states = mutableMapOf<EquipmentSlot, EquipmentSlotState>()
    val firstCleared = mutableSetOf<String>()
    private val commands = mutableMapOf<UUID, EquipmentRepository.StoredCommand>()

    fun seedMaterials(quantity: Long) {
        for (family in listOf("POTATO", "SWEET_POTATO", "CORN")) {
            for (generation in 1..4) materials["${family}_M$generation"] = quantity
        }
    }

    override fun lockAccount(accountId: UUID) = require(accountId == this.accountId)
    override fun balanceVersion(accountId: UUID): String = "enemy-v1-applied"
    override fun riceBalance(accountId: UUID): Long = rice
    override fun materialBalances(accountId: UUID, itemIds: Collection<String>): Map<String, Long> = itemIds.associateWith { materials[it] ?: 0 }
    override fun slotStates(accountId: UUID): List<EquipmentSlotState> = states.values.toList()
    override fun findSlotState(accountId: UUID, slot: EquipmentSlot): EquipmentSlotState? = states[slot]
    override fun firstClearedStages(accountId: UUID, stageIds: Collection<String>): Set<String> = firstCleared.intersect(stageIds.toSet())

    override fun consumeMaterials(accountId: UUID, materials: List<MaterialCost>) {
        require(materials.all { (this.materials[it.itemId] ?: 0) >= it.requiredQuantity }) { "INSUFFICIENT_MATERIALS" }
        materials.forEach { this.materials[it.itemId] = this.materials.getValue(it.itemId) - it.requiredQuantity }
        consumeCalls++
    }

    override fun createSlotState(state: EquipmentSlotState) {
        require(states.putIfAbsent(state.slot, state) == null) { "EQUIPMENT_ALREADY_UNLOCKED" }
    }

    override fun updateSlotState(state: EquipmentSlotState) {
        require(states.replace(state.slot, state) != null) { "EQUIPMENT_NOT_UNLOCKED" }
    }

    override fun incrementStateVersion(accountId: UUID): Long = ++version
    override fun command(accountId: UUID, idempotencyKey: UUID): EquipmentRepository.StoredCommand? = commands[idempotencyKey]
    override fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) {
        commands[idempotencyKey] = EquipmentRepository.StoredCommand(fingerprint, resultJson)
    }
}

private class FakeWalletRepository(private val accountId: UUID, private val equipment: FakeEquipmentRepository) : WalletRepository {
    data class Ledger(val amount: Long, val sourceType: String, val sourceId: UUID)
    val ledger = mutableListOf<Ledger>()

    override fun lockBalance(accountId: UUID): Long {
        require(accountId == this.accountId)
        return equipment.rice
    }

    override fun applyDelta(accountId: UUID, delta: Long, sourceType: String, sourceId: UUID, bumpStateVersion: Boolean): Long {
        require(accountId == this.accountId)
        require(equipment.rice + delta >= 0) { "INSUFFICIENT_RICE" }
        equipment.rice += delta
        ledger += Ledger(delta, sourceType, sourceId)
        return equipment.rice
    }

    override fun applyBatchedDebit(accountId: UUID, entries: List<WalletDebitEntry>, bumpStateVersion: Boolean): Long =
        entries.fold(equipment.rice) { _, entry -> applyDelta(accountId, -entry.amount, entry.sourceType, entry.sourceId, bumpStateVersion) }
}
