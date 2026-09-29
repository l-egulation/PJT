package com.hanjjak.cosmetics.application

import com.hanjjak.cosmetics.domain.*
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import org.springframework.transaction.annotation.Isolation
import java.util.UUID

@Service
class CosmeticsService(private val repository: CosmeticsRepository, private val content: CosmeticsContent, private val wallet: CosmeticsWallet) {
    @Transactional(isolation = Isolation.REPEATABLE_READ)
    fun collection(accountId: UUID): CollectionSnapshot {
        requireUnlocked(accountId)
        return snapshotForCombat(accountId)
    }

    @Transactional(isolation = Isolation.REPEATABLE_READ)
    fun versionedCollection(accountId: UUID): VersionedResult<CollectionSnapshot> =
        VersionedResult(collection(accountId), repository.stateVersion(accountId))

    @Transactional(isolation = Isolation.REPEATABLE_READ)
    fun catalog(accountId: UUID): VersionedResult<CosmeticsCatalog> {
        requireUnlocked(accountId)
        return VersionedResult(CosmeticsCatalog(content.version,
            content.cosmetics.map { CosmeticCatalogItem(it.id, it.displayName, it.grade, it.slot, it.setId, it.imageUrl) },
            content.sets.map { CosmeticCatalogSet(it.id, it.displayName, it.grade, it.members, it.effects) }), repository.stateVersion(accountId))
    }

    @Transactional(readOnly = true, isolation = Isolation.REPEATABLE_READ)
    fun snapshotForCombat(accountId: UUID): CollectionSnapshot =
        CosmeticsRules.snapshot(repository.findStates(accountId), repository.findEquipment(accountId), content)

    @Transactional
    fun register(accountId: UUID, cosmeticId: String, mode: RegistrationMode): CollectionSnapshot {
        repository.lockAccount(accountId)
        requireUnlocked(accountId)
        val definition = requireDefinition(cosmeticId)
        val state = repository.findStateForUpdate(accountId, cosmeticId) ?: throw IllegalArgumentException("COSMETIC_NOT_OWNED")
        repository.saveState(CosmeticsRules.register(state, mode, content.starThresholdsByGrade.getValue(definition.grade)))
        return collection(accountId)
    }

    @Transactional
    fun equip(accountId: UUID, slot: CosmeticSlot, cosmeticId: String?): CollectionSnapshot {
        repository.lockAccount(accountId)
        requireUnlocked(accountId)
        if (cosmeticId != null) {
            val definition = requireDefinition(cosmeticId)
            require(definition.slot == slot) { "COSMETIC_SLOT_MISMATCH" }
            val state = repository.findStateForUpdate(accountId, cosmeticId) ?: throw IllegalArgumentException("COSMETIC_NOT_OWNED")
            require(state.registeredQuantity >= 1) { "COSMETIC_NOT_OWNED" }
        }
        repository.saveEquipment(accountId, slot, cosmeticId)
        return collection(accountId)
    }

    @Transactional
    fun receive(accountId: UUID, cosmeticId: String): Pair<CollectionSnapshot, Boolean> {
        repository.lockAccount(accountId)
        requireUnlocked(accountId)
        requireDefinition(cosmeticId)
        val (state, isNew) = CosmeticsRules.receive(repository.findStateForUpdate(accountId, cosmeticId), accountId, cosmeticId)
        repository.saveState(state)
        return collection(accountId) to isNew
    }

    fun content(): CosmeticsContent = content

    @Transactional(readOnly = true, isolation = Isolation.REPEATABLE_READ)
    fun totalEffects(accountId: UUID): List<StatEffect> = snapshotForCombat(accountId).totalEffects
    fun requireUnlocked(accountId: UUID) {
        require(wallet.balanceForUpdate(accountId).unlocked) { "COSMETICS_LOCKED" }
    }
    private fun requireDefinition(cosmeticId: String): CosmeticDefinition = content.cosmetics.firstOrNull { it.id == cosmeticId }
        ?: throw IllegalArgumentException("COSMETIC_NOT_FOUND")
}
