package com.hanjjak.progression.application

import com.hanjjak.progression.domain.CharacterProgression
import java.util.UUID

interface ProgressionRepository {
    data class StoredCommand(val fingerprint: String, val resultJson: String)

    fun lockAccount(accountId: UUID)
    fun progression(accountId: UUID): CharacterProgression
    fun saveProgression(accountId: UUID, progression: CharacterProgression)
    fun command(accountId: UUID, idempotencyKey: UUID): StoredCommand?
    fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String)
}
