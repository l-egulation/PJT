package com.hanjjak.account.application

import com.hanjjak.account.domain.Account
import com.hanjjak.account.domain.Character
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNotEquals
import kotlin.test.assertTrue

class AuthenticationCommandServiceTest {
    private val accounts = MemoryAccountRepository()
    private val commands = MemoryCommandRepository()
    private val encoder = BCryptPasswordEncoder(4)
    private val authentication = AuthenticationService(accounts, encoder, Clock.fixed(Instant.parse("2026-09-07T00:00:00Z"), ZoneOffset.UTC))
    private val service = AuthenticationCommandService(authentication, commands, accounts, encoder)

    @Test
    fun `signup retry returns one account and stores a slow salted fingerprint`() {
        val key = UUID.randomUUID()
        val first = service.signup(key, " PLAYER@EXAMPLE.COM ", "password123", " 한짝 ")
        val retry = service.signup(key, "player@example.com", "password123", "한짝")

        assertEquals(first.account.id, retry.account.id)
        assertEquals(1, accounts.accountCount)
        assertEquals(1, commands.saveCount)
        assertTrue(commands.savedFingerprint.startsWith("$2"))
        assertNotEquals("password123", commands.savedFingerprint)
    }

    @Test
    fun `same key with a different payload is rejected without another mutation`() {
        val key = UUID.randomUUID()
        service.signup(key, "player@example.com", "password123", "한짝")

        val error = assertFailsWith<IllegalArgumentException> {
            service.signup(key, "player@example.com", "different-password", "한짝")
        }

        assertEquals("IDEMPOTENCY_KEY_REUSED", error.message)
        assertEquals(1, accounts.accountCount)
        assertEquals(1, commands.saveCount)
    }

    @Test
    fun `nickname retry changes the character and state version once`() {
        val signup = service.signup(UUID.randomUUID(), "player@example.com", "password123", "한짝")
        val key = UUID.randomUUID()

        val first = service.updateNickname(key, signup.account.id, " 새한짝 ")
        val retry = service.updateNickname(key, signup.account.id, "새한짝")

        assertEquals("새한짝", retry.character.nickname)
        assertEquals(first.account.stateVersion, retry.account.stateVersion)
        assertEquals(2, retry.account.stateVersion)
    }

    @Test
    fun `password change retry rotates the credential and state version once`() {
        val signup = service.signup(UUID.randomUUID(), "password@example.com", "password123", "한짝")
        val key = UUID.randomUUID()

        val first = service.changePassword(key, signup.account.id, "password123", "new-password456", "new-password456")
        val retry = service.changePassword(key, signup.account.id, "password123", "new-password456", "new-password456")

        assertEquals(first.account.stateVersion, retry.account.stateVersion)
        assertEquals(2, retry.account.stateVersion)
        assertFailsWith<IllegalArgumentException> { authentication.login("password@example.com", "password123") }
        assertEquals(signup.account.id, authentication.login("password@example.com", "new-password456").account.id)
    }

    private class MemoryCommandRepository : AuthenticationCommandRepository {
        private val values = mutableMapOf<UUID, AuthenticationCommand>()
        var saveCount = 0
            private set
        var savedFingerprint = ""
            private set

        override fun lock(idempotencyKey: UUID) = Unit
        override fun find(idempotencyKey: UUID): AuthenticationCommand? = values[idempotencyKey]
        override fun save(command: AuthenticationCommand) {
            values[command.idempotencyKey] = command
            saveCount += 1
            savedFingerprint = command.fingerprint
        }
    }

    private class MemoryAccountRepository : AccountRepository {
        private val accounts = mutableMapOf<UUID, Account>()
        private val characters = mutableMapOf<UUID, Character>()
        val accountCount get() = accounts.size

        override fun findByEmail(email: String) = accounts.values.firstOrNull { it.email == email }
        override fun findById(id: UUID) = accounts[id]
        override fun findCharacter(accountId: UUID) = characters.values.firstOrNull { it.accountId == accountId }
        override fun save(account: Account) = account.also { accounts[it.id] = it }
        override fun saveCharacter(character: Character) = character.also { characters[it.id] = it }
        override fun updateCharacterNickname(accountId: UUID, nickname: String): Character? {
            val current = findCharacter(accountId) ?: return null
            return current.copy(nickname = nickname).also { characters[it.id] = it }
        }
        override fun incrementStateVersion(id: UUID): Account? {
            val current = accounts[id] ?: return null
            return current.copy(stateVersion = current.stateVersion + 1).also { accounts[id] = it }
        }
        override fun updatePasswordAndIncrementStateVersion(id: UUID, passwordHash: String): Account? {
            val current = accounts[id] ?: return null
            return current.copy(passwordHash = passwordHash, stateVersion = current.stateVersion + 1).also { accounts[id] = it }
        }
        override fun softDelete(id: UUID, anonymizedEmail: String, anonymizedNickname: String): Boolean = accounts.containsKey(id)
    }
}
