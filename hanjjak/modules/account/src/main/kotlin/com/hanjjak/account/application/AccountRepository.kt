package com.hanjjak.account.application

import com.hanjjak.account.domain.Account
import com.hanjjak.account.domain.Character
import java.util.UUID

interface AccountRepository {
    fun findByEmail(email: String): Account?
    fun findById(id: UUID): Account?
    fun findCharacter(accountId: UUID): Character?
    fun save(account: Account): Account
    fun saveCharacter(character: Character): Character
    fun updateCharacterNickname(accountId: UUID, nickname: String): Character?
    fun incrementStateVersion(id: UUID): Account?
    fun updatePasswordAndIncrementStateVersion(id: UUID, passwordHash: String): Account?
    fun softDelete(id: UUID, anonymizedEmail: String, anonymizedNickname: String): Boolean
}

interface AuthenticationCommandRepository {
    fun lock(idempotencyKey: UUID)
    fun find(idempotencyKey: UUID): AuthenticationCommand?
    fun save(command: AuthenticationCommand)
}

data class AuthenticationCommand(
    val idempotencyKey: UUID,
    val fingerprint: String,
    val accountId: UUID,
    val characterId: UUID,
)
