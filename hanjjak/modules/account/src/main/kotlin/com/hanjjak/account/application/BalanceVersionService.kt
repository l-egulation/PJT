package com.hanjjak.account.application

import java.time.Instant
import java.util.UUID

interface BalanceVersionRepository {
    fun current(accountId: UUID): String?
    fun assign(accountId: UUID, balanceVersion: String, appliedAt: Instant): String
}

open class BalanceVersionService(
    private val repository: BalanceVersionRepository,
    private val activeVersion: String = "enemy-v1-applied",
) {
    fun current(accountId: UUID): String = validate(repository.current(accountId) ?: throw IllegalArgumentException("BALANCE_VERSION_MISSING"))

    fun assignNewAccount(accountId: UUID): String = validate(repository.assign(accountId, activeVersion, Instant.now()))

    private fun validate(version: String): String {
        require(version in KNOWN_VERSIONS) { "BALANCE_VERSION_UNKNOWN" }
        return version
    }

    companion object {
        val KNOWN_VERSIONS = setOf("enemy-v1-applied", "progression-rebalance-v1")

        class InMemory(private val configuredVersion: String = "enemy-v1-applied") : BalanceVersionService(InMemoryRepository(), configuredVersion) {
            private class InMemoryRepository : BalanceVersionRepository {
                private val versions = mutableMapOf<UUID, String>()
                override fun current(accountId: UUID): String? = versions[accountId]
                override fun assign(accountId: UUID, balanceVersion: String, appliedAt: Instant): String = balanceVersion.also { versions[accountId] = it }
            }
        }
        fun noop(): BalanceVersionService = BalanceVersionService(object : BalanceVersionRepository {
            override fun current(accountId: UUID): String = "enemy-v1-applied"
            override fun assign(accountId: UUID, balanceVersion: String, appliedAt: Instant): String = balanceVersion
        })
    }
}
