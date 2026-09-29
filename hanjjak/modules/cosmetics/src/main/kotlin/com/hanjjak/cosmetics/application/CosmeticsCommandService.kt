package com.hanjjak.cosmetics.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.domain.VersionedResult
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.util.UUID

@Service
class CosmeticsCommandService(private val repository: CosmeticsCommandRepository, private val mapper: ObjectMapper) {
    fun <T : Any> execute(accountId: UUID, key: UUID, payload: String, type: Class<T>, action: () -> T): T = executeVersioned(accountId, key, payload, type, action = action).data

    @Transactional
    fun <T : Any> executeVersioned(accountId: UUID, key: UUID, payload: String, type: Class<T>, audit: ((T) -> CosmeticAudit)? = null, action: () -> T): VersionedResult<T> {
        val fingerprint = MessageDigest.getInstance("SHA-256").digest(payload.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
        repository.lockAccount(accountId)
        val existing = repository.find(accountId, key)
        if (existing != null) {
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            val stored = mapper.readTree(existing.resultJson)
            return if (stored.path("_cosmeticsCommandVersioned").asBoolean(false)) VersionedResult(mapper.treeToValue(stored.get("data"), type), stored.get("stateVersion").asLong())
            else VersionedResult(mapper.treeToValue(stored, type), 0L)
        }
        val result = action()
        val stateVersion = repository.incrementStateVersion(accountId)
        repository.save(accountId, key, fingerprint, mapper.writeValueAsString(mapOf("_cosmeticsCommandVersioned" to true, "stateVersion" to stateVersion, "data" to result)), audit?.invoke(result))
        return VersionedResult(result, stateVersion)
    }
}
