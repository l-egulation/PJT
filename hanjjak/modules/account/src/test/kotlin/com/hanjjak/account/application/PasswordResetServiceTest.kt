package com.hanjjak.account.application

import com.hanjjak.account.domain.Account
import com.hanjjak.account.domain.Character
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder
import java.net.URI
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNotEquals
import kotlin.test.assertNull
import kotlin.test.assertTrue

class PasswordResetServiceTest {
    private val now = Instant.parse("2026-09-09T00:00:00Z")
    private val accounts = MemoryAccountRepository()
    private val tokens = MemoryTokenRepository(accounts)
    private val mail = RecordingMailSender()
    private val authenticationSessions = RecordingAuthenticationSessionLifecycle()
    private val gameSessions = RecordingGameSessionLifecycle()
    private val encoder = BCryptPasswordEncoder(4)
    private val service = PasswordResetService(
        accounts,
        tokens,
        mail,
        encoder,
        gameSessions,
        authenticationSessions,
        Clock.fixed(now, ZoneOffset.UTC),
        PasswordResetProperties(URI("https://game.example.com"), Duration.ofMinutes(30)),
    )

    @Test
    fun `request stores only a hash and sends an HTTPS link for an existing account`() {
        val account = accounts.save(Account(UUID.randomUUID(), "player@example.com", encoder.encode("old-password"), "player@example.com", 1, now))

        service.request(" PLAYER@EXAMPLE.COM ")

        val link = mail.links.single()
        val rawToken = link.rawFragment.removePrefix("token=")
        assertEquals("https", link.scheme)
        assertEquals("/reset-password", link.path)
        assertNotEquals(rawToken, tokens.saved?.tokenHash)
        assertEquals(64, tokens.saved?.tokenHash?.length)
        assertEquals(account.id, tokens.saved?.accountId)
        assertEquals(now.plus(Duration.ofMinutes(30)), tokens.saved?.expiresAt)
    }

    @Test
    fun `unknown account has the same successful service outcome without mail`() {
        service.request("missing@example.com")

        assertNull(tokens.saved)
        assertTrue(mail.links.isEmpty())
    }

    @Test
    fun `token is single use and password reset closes all account sessions`() {
        val account = accounts.save(Account(UUID.randomUUID(), "player@example.com", encoder.encode("old-password"), "player@example.com", 1, now))
        service.request(account.email)
        val rawToken = mail.links.single().rawFragment.removePrefix("token=")

        service.reset(rawToken, "new-password")

        assertTrue(encoder.matches("new-password", accounts.findById(account.id)!!.passwordHash))
        assertEquals(listOf(account.id), authenticationSessions.closed)
        assertEquals(listOf(account.id), gameSessions.closed)
        val error = assertFailsWith<IllegalArgumentException> { service.reset(rawToken, "another-password") }
        assertEquals("PASSWORD_RESET_TOKEN_INVALID", error.message)
    }

    private class MemoryTokenRepository(private val accounts: MemoryAccountRepository) : PasswordResetTokenRepository {
        var saved: PasswordResetToken? = null

        override fun replaceActive(accountId: UUID, token: PasswordResetToken) {
            saved = token
        }

        override fun consume(tokenHash: String, consumedAt: Instant): PasswordResetToken? {
            val token = saved ?: return null
            if (token.tokenHash != tokenHash || token.consumedAt != null || !token.expiresAt.isAfter(consumedAt)) return null
            val consumed = token.copy(consumedAt = consumedAt)
            saved = consumed
            return consumed
        }

        override fun updatePassword(accountId: UUID, passwordHash: String): Long {
            val account = accounts.findById(accountId) ?: error("ACCOUNT_NOT_FOUND")
            accounts.save(account.copy(passwordHash = passwordHash, stateVersion = account.stateVersion + 1))
            return account.stateVersion + 1
        }
    }

    private class RecordingMailSender : PasswordResetMailSender {
        val links = mutableListOf<URI>()
        override fun send(to: String, resetLink: URI) {
            links += resetLink
        }
    }

    private class RecordingAuthenticationSessionLifecycle : AuthenticationSessionLifecycle {
        val closed = mutableListOf<UUID>()
        override fun closeForPasswordReset(accountId: UUID) {
            closed += accountId
        }
    }

    private class RecordingGameSessionLifecycle : GameSessionLifecycle {
        val closed = mutableListOf<UUID>()
        override fun closeForPasswordReset(accountId: UUID) {
            closed += accountId
        }
        override fun replaceForLogin(accountId: UUID) = Unit
        override fun closeForLogout(accountId: UUID) = Unit
    }

    private class MemoryAccountRepository : AccountRepository {
        private val accounts = mutableMapOf<UUID, Account>()
        override fun findByEmail(email: String) = accounts.values.firstOrNull { it.email == email }
        override fun findById(id: UUID) = accounts[id]
        override fun findCharacter(accountId: UUID): Character? = null
        override fun save(account: Account) = account.also { accounts[it.id] = it }
        override fun saveCharacter(character: Character) = character
        override fun updateCharacterNickname(accountId: UUID, nickname: String): Character? = null
        override fun incrementStateVersion(id: UUID): Account? = null
        override fun updatePasswordAndIncrementStateVersion(id: UUID, passwordHash: String): Account? {
            val current = accounts[id] ?: return null
            return current.copy(passwordHash = passwordHash, stateVersion = current.stateVersion + 1).also { accounts[id] = it }
        }
        override fun softDelete(id: UUID, anonymizedEmail: String, anonymizedNickname: String) = false
    }
}
