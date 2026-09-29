package com.hanjjak.cosmetics.application

import java.util.UUID

interface CosmeticsCommandRepository {
    data class Record(val fingerprint: String, val resultJson: String)
    fun find(accountId: UUID, key: UUID): Record?
    fun lockAccount(accountId: UUID)
    fun incrementStateVersion(accountId: UUID): Long
    fun save(accountId: UUID, key: UUID, fingerprint: String, resultJson: String)
    fun save(accountId: UUID, key: UUID, fingerprint: String, resultJson: String, audit: CosmeticAudit?) {
        save(accountId, key, fingerprint, resultJson)
    }
}
