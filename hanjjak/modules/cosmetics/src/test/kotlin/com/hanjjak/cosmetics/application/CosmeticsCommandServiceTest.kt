package com.hanjjak.cosmetics.application

import com.fasterxml.jackson.databind.ObjectMapper
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class CosmeticsCommandServiceTest {
    private val account = UUID.randomUUID()
    private val key = UUID.randomUUID()
    private class MemoryCommands : CosmeticsCommandRepository {
        var version = 7L
        var audit: CosmeticAudit? = null
        val records = mutableMapOf<Pair<UUID, UUID>, CosmeticsCommandRepository.Record>()
        override fun lockAccount(accountId: UUID) = Unit
        override fun find(accountId: UUID, key: UUID) = records[accountId to key]
        override fun incrementStateVersion(accountId: UUID): Long = ++version
        override fun save(accountId: UUID, key: UUID, fingerprint: String, resultJson: String) {
            records[accountId to key] = CosmeticsCommandRepository.Record(fingerprint, resultJson)
        }
        override fun save(accountId: UUID, key: UUID, fingerprint: String, resultJson: String, audit: CosmeticAudit?) {
            this.audit = audit
            save(accountId, key, fingerprint, resultJson)
        }
    }

    @Test
    fun `replay preserves original version and never repeats mutation`() {
        val repository = MemoryCommands()
        val service = CosmeticsCommandService(repository, ObjectMapper())
        var executions = 0
        val first = service.executeVersioned(account, key, "equip:HEAD:x", String::class.java) { executions++; "equipped" }
        repository.version = 12
        val replay = service.executeVersioned(account, key, "equip:HEAD:x", String::class.java) { executions++; "wrong" }
        assertEquals(8L, first.stateVersion)
        assertEquals(first, replay)
        assertEquals(1, executions)
        assertEquals(12L, repository.version)
        assertFailsWith<IllegalArgumentException> {
            service.executeVersioned(account, key, "equip:HEAD:y", String::class.java) { "wrong" }
        }
        assertEquals(12L, repository.version)
    }

    @Test
    fun `failed mutation cannot advance version or save a command`() {
        val repository = MemoryCommands()
        val service = CosmeticsCommandService(repository, ObjectMapper())
        assertFailsWith<IllegalArgumentException> {
            service.executeVersioned(account, key, "register:x", String::class.java) { throw IllegalArgumentException("insufficient") }
        }
        assertEquals(7L, repository.version)
        assertEquals(0, repository.records.size)
    }

    @Test
    fun `old command record remains replayable with unknown original version`() {
        val repository = MemoryCommands()
        val service = CosmeticsCommandService(repository, ObjectMapper())
        service.execute(account, key, "old", String::class.java) { "original" }
        val record = repository.records.getValue(account to key)
        repository.records[account to key] = record.copy(resultJson = "\"original\"")
        val replay = service.executeVersioned(account, key, "old", String::class.java) { error("must not execute") }
        assertEquals("original", replay.data)
        assertEquals(0L, replay.stateVersion)
        assertEquals(8L, repository.version)
    }

    @Test
    fun `successful command passes typed audit to repository`() {
        val repository = MemoryCommands()
        val service = CosmeticsCommandService(repository, ObjectMapper())
        val audit = CosmeticAudit(CosmeticOperation.DRAW, "cosmetics-v2-placeholder", mapOf("count" to 1), DrawReproduction("HMAC-SHA256-V1", "test-key", "token"))
        service.executeVersioned(account, key, "draw:banner:1", String::class.java, audit = { audit }) { "drawn" }
        assertEquals(audit, repository.audit)
    }
}
