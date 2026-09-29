package com.hanjjak.account.application

import com.hanjjak.account.domain.MaterialType
import java.util.UUID

data class MaterialPreferenceCommand(val fingerprint: String)

interface MaterialPreferenceRepository {
    fun lockAccount(accountId: UUID)
    fun find(accountId: UUID): MaterialType?
    fun select(accountId: UUID, materialType: MaterialType): Boolean
    fun incrementStateVersion(accountId: UUID): Long
    fun stateVersion(accountId: UUID): Long
    fun populationCounts(): Map<MaterialType, Long>
    fun findCommand(accountId: UUID, idempotencyKey: UUID): MaterialPreferenceCommand?
    fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String)
}
