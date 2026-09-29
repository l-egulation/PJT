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

class AuthenticationServiceTest {
    private val repository = MemoryAccountRepository()
    private val versions = BalanceVersionService.Companion.InMemory("progression-rebalance-v1")
    private val service = AuthenticationService(
        repository,
        BCryptPasswordEncoder(),
        Clock.fixed(Instant.parse("2026-09-02T00:00:00Z"), ZoneOffset.UTC),
        versions,
    )

    @Test
    fun `signup creates one account and one character`() {
        val result = service.signup(" PLAYER@EXAMPLE.COM ", "password123", " 한짝 ")
        assertEquals("player@example.com", result.account.email)
        assertEquals(result.account.id, result.character.accountId)
        assertEquals("한짝", result.character.nickname)
        assertEquals(1, result.character.level)
    }

    @Test
    fun `signup assigns configured balance version immediately after account save`() {
        val result = service.signup("versioned@example.com", "password123", "한짝")
        assertEquals("progression-rebalance-v1", service.balanceVersion(result.account.id))
    }

    @Test
    fun `signup rejects invalid email before persistence`() {
        val error = assertFailsWith<IllegalArgumentException> { service.signup("not-an-email", "password123", "한짝") }
        assertEquals("INVALID_EMAIL", error.message)
    }

    @Test
    fun `login uses the same normalized email as signup`() {
        service.signup("PLAYER@EXAMPLE.COM", "password123", "한짝")
        val result = service.login(" player@example.com ", "password123")
        assertEquals("player@example.com", result.account.email)
    }

    @Test
    fun `signup rejects blank nicknames`() {
        val error = assertFailsWith<IllegalArgumentException> { service.signup("player@example.com", "password123", "  ") }
        assertEquals("NICKNAME_REQUIRED", error.message)
    }

    @Test
    fun `signup rejects nicknames over twenty characters`() {
        val error = assertFailsWith<IllegalArgumentException> { service.signup("player@example.com", "password123", "123456789012345678901") }
        assertEquals("NICKNAME_TOO_LONG", error.message)
    }

    @Test
    fun `update nickname changes display name and bumps state version`() {
        val signup = service.signup("player@example.com", "password123", "한짝")
        val updated = service.updateNickname(signup.account.id, " 새한짝 ")
        assertEquals("새한짝", updated.character.nickname)
        assertEquals(2, updated.account.stateVersion)
    }

    @Test
    fun `login rejects an incorrect password without revealing account state`() {
        service.signup("player@example.com", "password123", "한짝")
        val error = assertFailsWith<IllegalArgumentException> { service.login("player@example.com", "wrong-password") }
    }

    @Test
    fun `password change verifies current password confirmation and replaces credential`() {
        val signup = service.signup("change@example.com", "password123", "한짝")

        val wrongCurrent = assertFailsWith<IllegalArgumentException> {
            service.changePassword(signup.account.id, "wrong-password", "new-password456", "new-password456")
        }
        assertEquals("CURRENT_PASSWORD_INVALID", wrongCurrent.message)
        assertEquals(1, repository.findById(signup.account.id)?.stateVersion)

        val mismatch = assertFailsWith<IllegalArgumentException> {
            service.changePassword(signup.account.id, "password123", "new-password456", "different-password")
        }
        assertEquals("PASSWORD_CONFIRMATION_MISMATCH", mismatch.message)
        assertEquals(1, repository.findById(signup.account.id)?.stateVersion)

        val updated = service.changePassword(signup.account.id, "password123", "new-password456", "new-password456")
        assertEquals(2, updated.account.stateVersion)
        assertFailsWith<IllegalArgumentException> { service.login("change@example.com", "password123") }
        assertEquals(signup.account.id, service.login("change@example.com", "new-password456").account.id)
    }

    @Test
    fun `delete anonymizes account and character`() {
        val signup = service.signup("delete@example.com", "password123", "한짝")
        service.deleteAccount(signup.account.id)
        val deleted = repository.findById(signup.account.id)
        assertEquals("deleted-${signup.account.id}@deleted.local", deleted?.email)
        assertEquals("탈퇴한 사용자", repository.findCharacter(signup.account.id)?.nickname)
    }

    private class MemoryAccountRepository : AccountRepository {
        private val accounts = mutableMapOf<UUID, Account>()
        private val characters = mutableMapOf<UUID, Character>()
        override fun findByEmail(email: String) = accounts.values.firstOrNull { it.email == email }
        override fun findById(id: UUID) = accounts[id]
        override fun findCharacter(accountId: UUID) = characters.values.firstOrNull { it.accountId == accountId }
        override fun save(account: Account) = account.also { accounts[it.id] = it }
        override fun saveCharacter(character: Character) = character.also { characters[it.id] = it }
        override fun updateCharacterNickname(accountId: UUID, nickname: String): Character? {
            val current = characters.values.firstOrNull { it.accountId == accountId } ?: return null
            val updated = current.copy(nickname = nickname)
            characters[updated.id] = updated
            return updated
        }
        override fun incrementStateVersion(id: UUID): Account? {
            val current = accounts[id] ?: return null
            val updated = current.copy(stateVersion = current.stateVersion + 1)
            accounts[id] = updated
            return updated
        }
        override fun updatePasswordAndIncrementStateVersion(id: UUID, passwordHash: String): Account? {
            val current = accounts[id] ?: return null
            val updated = current.copy(passwordHash = passwordHash, stateVersion = current.stateVersion + 1)
            accounts[id] = updated
            return updated
        }
        override fun softDelete(id: UUID, anonymizedEmail: String, anonymizedNickname: String): Boolean {
            val account = accounts[id] ?: return false
            accounts[id] = account.copy(email = anonymizedEmail, stateVersion = account.stateVersion + 1)
            val character = characters.values.firstOrNull { it.accountId == id } ?: return false
            characters[character.id] = character.copy(nickname = anonymizedNickname)
            return true
        }
    }
}
