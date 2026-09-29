package com.hanjjak.cosmetics.application

import com.hanjjak.cosmetics.domain.*
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class CosmeticsServiceTest {
    private val account = UUID.randomUUID()
    private val definitions = CosmeticSlot.entries.mapIndexed { index, slot ->
        CosmeticDefinition("cosmetic-${String.format("%03d", index + 61)}", null, null, CosmeticGrade.LEGENDARY, slot, "cosmetic-set-11")
    }
    private val thresholds = CosmeticGrade.entries.associateWith { grade ->
        if (grade == CosmeticGrade.LEGENDARY) listOf(1, 2, 3, 5, 10) else listOf(1, 100, 200, 300, 400)
    }
    private val content = CosmeticsContent(
        "catalog-v2", 5_000, CosmeticGrade.entries.associateWith { 250_000 }, thresholds, definitions,
        listOf(SetDefinition("cosmetic-set-11", null, CosmeticGrade.LEGENDARY, "cosmetic-banner-01", "cosmetic-selector-box-01", definitions.associate { it.slot to it.id },
            mapOf(1 to listOf(StatEffect("attackPercent", 25, EffectUnit.BASIS_POINTS))))),
    )
    private class MemoryRepository(private val account: UUID) : CosmeticsRepository {
        var state = CosmeticState(account, "cosmetic-061", 1, 5, 0)
        var equipment: String? = "cosmetic-061"
        var writes = 0
        override fun stateVersion(accountId: UUID) = 42L
        override fun lockAccount(accountId: UUID) = Unit
        override fun findStates(accountId: UUID) = listOf(state)
        override fun findStateForUpdate(accountId: UUID, cosmeticId: String) = state.takeIf { it.cosmeticId == cosmeticId }
        override fun saveState(state: CosmeticState) { this.state = state; writes++ }
        override fun findEquipment(accountId: UUID) = listOf(EquipmentSlot(account, CosmeticSlot.HEAD, equipment))
        override fun saveEquipment(accountId: UUID, slot: CosmeticSlot, cosmeticId: String?) { equipment = cosmeticId; writes++ }
    }
    private class Wallet(private val unlocked: Boolean) : CosmeticsWallet {
        override fun balanceForUpdate(accountId: UUID) = CosmeticsWallet.Balance(0, 0, unlocked)
        override fun milestone(accountId: UUID, bannerId: String, boxItemId: String) = CosmeticsWallet.Milestone(0, 0, 0)
        override fun debit(accountId: UUID, rice: Long, tickets: Long, sourceId: UUID) = error("not used")
        override fun addDraws(accountId: UUID, bannerId: String, count: Int) = error("not used")
        override fun claimBoxes(accountId: UUID, bannerId: String, boxItemId: String, count: Long): Long = error("not used")
        override fun openBox(accountId: UUID, boxItemId: String) = error("not used")
        override fun preflightCreditTicketsLocked(accountId: UUID, amount: Long) = error("not used")
        override fun creditTicketsLocked(accountId: UUID, amount: Long, sourceId: UUID) = error("not used")
    }

    @Test
    fun `locked cosmetic reads and commands fail before asset changes`() {
        val repository = MemoryRepository(account)
        val service = CosmeticsService(repository, content, Wallet(false))
        val operations = listOf<() -> Any>(
            { service.catalog(account) }, { service.versionedCollection(account) },
            { service.register(account, "cosmetic-061", RegistrationMode.UNTIL_NEXT_STAR) },
            { service.equip(account, CosmeticSlot.HEAD, null) }, { service.receive(account, "cosmetic-061") },
        )
        operations.forEach { action -> assertEquals("COSMETICS_LOCKED", assertFailsWith<IllegalArgumentException> { action() }.message) }
        assertEquals(0, repository.writes)
        assertEquals(1, repository.state.registeredQuantity)
        assertEquals("cosmetic-061", repository.equipment)
        assertEquals(emptyList(), service.snapshotForCombat(account).totalEffects)
    }

    @Test
    fun `catalog includes nullable metadata and generic effects with matching collection version`() {
        val service = CosmeticsService(MemoryRepository(account), content, Wallet(true))
        val catalog = service.catalog(account)
        val collection = service.versionedCollection(account)
        assertEquals(42L, catalog.stateVersion)
        assertEquals(42L, collection.stateVersion)
        assertEquals("catalog-v2", catalog.data.contentVersion)
        assertEquals(catalog.data.contentVersion, collection.data.contentVersion)
        assertEquals(6, catalog.data.cosmetics.size)
        assertEquals(null, catalog.data.cosmetics.first().displayName)
        assertEquals(null, catalog.data.cosmetics.first().imageUrl)
        assertEquals(listOf(StatEffect("attackPercent", 25, EffectUnit.BASIS_POINTS)), catalog.data.sets.single().effects[1])
        assertEquals("NOT_OWNED", collection.data.states.first { it.cosmeticId == "cosmetic-066" }.upgradeDisabledReason)
    }

    @Test
    fun `registration uses the owned cosmetic grade thresholds`() {
        val repository = MemoryRepository(account).apply { state = state.copy(registeredQuantity = 2) }
        val service = CosmeticsService(repository, content, Wallet(true))
        service.register(account, "cosmetic-061", RegistrationMode.UNTIL_NEXT_STAR)
        assertEquals(3, repository.state.registeredQuantity)
        assertEquals(4, repository.state.unregisteredQuantity)
    }
}
