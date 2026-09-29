package com.hanjjak.account.application

import com.hanjjak.account.domain.MaterialAlreadySelectedException
import com.hanjjak.account.domain.MaterialType
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class MaterialPreferenceServiceTest {
    private val repository = MemoryRepository()
    private val service = MaterialPreferenceService(repository)
    private val accountId = UUID.randomUUID()

    @Test
    fun `query returns every server-calculated option before selection`() {
        val state = service.get(accountId).data
        assertEquals(false, state.selected)
        assertEquals(listOf(MaterialType.POTATO, MaterialType.SWEET_POTATO, MaterialType.CORN), state.options.map { it.materialType })
        assertEquals(listOf(80, 10, 10), state.options.first().dropRates.let { listOf(it.potato, it.sweetPotato, it.corn) })
        assertEquals(1, service.get(accountId).stateVersion)
    }

    @Test
    fun `same selection is idempotent and a different selection is rejected`() {
        val key = UUID.randomUUID()
        val first = service.select(accountId, key, MaterialType.POTATO)
        val retry = service.select(accountId, key, MaterialType.POTATO)
        assertEquals(MaterialType.POTATO, first.data.primaryMaterialType)
        assertEquals(2, first.stateVersion)
        assertEquals(first, retry)
        val error = assertFailsWith<MaterialAlreadySelectedException> { service.select(accountId, UUID.randomUUID(), MaterialType.CORN) }
        assertEquals(MaterialType.POTATO, error.current)
        assertEquals(2, service.get(accountId).stateVersion)
    }

    @Test
    fun `concurrent different selections leave exactly one value`() {
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        val outcomes = listOf(MaterialType.POTATO, MaterialType.CORN).map { material ->
            pool.submit<Boolean> {
                start.await()
                runCatching { service.select(accountId, UUID.randomUUID(), material) }.isSuccess
            }
        }
        start.countDown()
        assertEquals(1, outcomes.count { it.get() })
        assertTrue(service.get(accountId).data.primaryMaterialType in setOf(MaterialType.POTATO, MaterialType.CORN))
        pool.shutdownNow()
    }

    private class MemoryRepository : MaterialPreferenceRepository {
        private val values = ConcurrentHashMap<UUID, MaterialType>()
        private val commands = ConcurrentHashMap<Pair<UUID, UUID>, MaterialPreferenceCommand>()
        private var version = 1L
        override fun lockAccount(accountId: UUID) = Unit
        override fun find(accountId: UUID): MaterialType? = values[accountId]
        override fun select(accountId: UUID, materialType: MaterialType): Boolean = values.putIfAbsent(accountId, materialType) == null
        override fun incrementStateVersion(accountId: UUID): Long = ++version
        override fun stateVersion(accountId: UUID): Long = version
        override fun populationCounts(): Map<MaterialType, Long> = values.values.groupingBy { it }.eachCount().mapValues { it.value.toLong() }
        override fun findCommand(accountId: UUID, idempotencyKey: UUID): MaterialPreferenceCommand? = commands[accountId to idempotencyKey]
        override fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String) { commands[accountId to idempotencyKey] = MaterialPreferenceCommand(fingerprint) }
    }
}
