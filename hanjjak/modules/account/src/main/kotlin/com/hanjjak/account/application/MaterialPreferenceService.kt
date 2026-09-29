package com.hanjjak.account.application

import com.hanjjak.account.domain.MaterialPreferenceSelection
import com.hanjjak.account.domain.MaterialPreferenceSnapshot
import com.hanjjak.account.domain.MaterialPreferenceState
import com.hanjjak.account.domain.MaterialAlreadySelectedException
import com.hanjjak.account.domain.MaterialDropRates
import com.hanjjak.account.domain.MaterialOption
import com.hanjjak.account.domain.MaterialType
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.events.application.NoopDomainEventPublisher
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.util.UUID

@Service
class MaterialPreferenceService(
    private val repository: MaterialPreferenceRepository,
    private val events: DomainEventPublisher = NoopDomainEventPublisher,
) {
    fun get(accountId: UUID): MaterialPreferenceSnapshot<MaterialPreferenceState> {
        val selected = repository.find(accountId)
        return MaterialPreferenceSnapshot(
            repository.stateVersion(accountId),
            MaterialPreferenceState(selected != null, selected, MaterialType.entries.map(::option), repository.populationCounts()),
        )
    }
    fun populationCounts(): Map<MaterialType, Long> = repository.populationCounts()

    @Transactional
    fun select(accountId: UUID, idempotencyKey: UUID, materialType: MaterialType): MaterialPreferenceSnapshot<MaterialPreferenceSelection> {
        repository.lockAccount(accountId)
        val fingerprint = fingerprint(materialType)
        repository.findCommand(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            val current = requireNotNull(repository.find(accountId)) { "MATERIAL_PREFERENCE_UNAVAILABLE" }
            return MaterialPreferenceSnapshot(repository.stateVersion(accountId), resolveExisting(current, materialType))
        }

        val current = repository.find(accountId)
        if (current != null) {
            val result = MaterialPreferenceSnapshot(repository.stateVersion(accountId), resolveExisting(current, materialType))
            repository.saveCommand(accountId, idempotencyKey, fingerprint)
            return result
        }

        require(repository.select(accountId, materialType)) { "MATERIAL_PREFERENCE_UNAVAILABLE" }
        val selection = selection(materialType)
        val result = MaterialPreferenceSnapshot(repository.incrementStateVersion(accountId), selection)
        events.publish(
            eventType = "MATERIAL_PREFERENCE_SELECTED",
            aggregateId = accountId,
            occurredAt = java.time.Instant.now(),
            accountId = accountId,
            correlationId = idempotencyKey,
            payload = mapOf(
                "materialType" to selection.primaryMaterialType.name,
                "dropRates" to mapOf(
                    "potato" to selection.dropRates.potato,
                    "sweetPotato" to selection.dropRates.sweetPotato,
                    "corn" to selection.dropRates.corn,
                ),
            ),
        )
        repository.saveCommand(accountId, idempotencyKey, fingerprint)
        return result
    }

    fun requireSelected(accountId: UUID): MaterialType = repository.find(accountId)
        ?: throw IllegalArgumentException("MATERIAL_SELECTION_REQUIRED")

    private fun resolveExisting(current: MaterialType, requested: MaterialType): MaterialPreferenceSelection {
        if (current != requested) throw MaterialAlreadySelectedException(current)
        return selection(current)
    }

    private fun selection(materialType: MaterialType) = MaterialPreferenceSelection(true, materialType, rates(materialType))

    private fun option(materialType: MaterialType) = MaterialOption(materialType, materialType.displayName, rates(materialType))

    private fun rates(primary: MaterialType): MaterialDropRates = MaterialDropRates(
        potato = if (primary == MaterialType.POTATO) 80 else 10,
        sweetPotato = if (primary == MaterialType.SWEET_POTATO) 80 else 10,
        corn = if (primary == MaterialType.CORN) 80 else 10,
    )

    private fun fingerprint(materialType: MaterialType): String = MessageDigest.getInstance("SHA-256")
        .digest("material-preference:$materialType".toByteArray(StandardCharsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
}
