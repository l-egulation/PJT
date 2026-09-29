package com.hanjjak.account.application

import com.hanjjak.account.domain.Account
import com.hanjjak.account.domain.Character
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder
import java.security.SecureRandom
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNotEquals
import kotlin.test.assertTrue

class SocialLoginServiceTest {
    private val now = Instant.parse("2026-09-09T00:00:00Z")
    private val accounts = MemoryAccountRepository()
    private val identities = MemorySocialIdentityRepository(accounts)
    private val client = RecordingProviderClient()
    private val authentication = AuthenticationService(accounts, BCryptPasswordEncoder(4), Clock.fixed(now, ZoneOffset.UTC), BalanceVersionService.Companion.InMemory("progression-rebalance-v1"))
    private val properties = SocialLoginProperties(
        publicBaseUrl = "https://api.example.com",
        frontendSuccessUrl = "https://app.example.com",
        frontendFailureUrl = "https://app.example.com/login",
        providers = mapOf(
            "google" to SocialLoginProperties.Provider(
                enabled = true,
                clientId = "google-client",
                clientSecret = "secret",
                issuer = "https://accounts.google.com",
                authorizationUri = "https://accounts.google.com/o/oauth2/v2/auth",
                tokenUri = "https://oauth2.googleapis.com/token",
                jwkSetUri = "https://www.googleapis.com/oauth2/v3/certs",
                scopes = listOf("openid", "email", "profile"),
                oidc = true,
                pkce = true,
            ),
            "kakao" to SocialLoginProperties.Provider(
                enabled = true,
                clientId = "kakao-rest-api-key",
                clientSecret = "secret",
                issuer = "https://kauth.kakao.com",
                authorizationUri = "https://kauth.kakao.com/oauth/authorize",
                tokenUri = "https://kauth.kakao.com/oauth/token",
                jwkSetUri = "https://kauth.kakao.com/.well-known/jwks.json",
                scopes = listOf("openid"),
                oidc = true,
                pkce = true,
            ),
        ),
    )
    private val service = SocialLoginService(properties, client, identities, authentication, Clock.fixed(now, ZoneOffset.UTC), SecureRandom())

    @Test
    fun `social signup receives the active balance version`() {
        val result = authentication.signupSocial("social-version@example.com", "한짝")

        assertEquals("progression-rebalance-v1", authentication.balanceVersion(result.account.id))
    }

    @Test
    fun `authorization uses server state nonce and PKCE S256`() {
        val flow = service.begin(SocialProvider.GOOGLE)
        val authorization = service.authorizationUrl(flow)

        assertTrue(authorization.startsWith("https://accounts.google.com/o/oauth2/v2/auth?"))
        assertTrue(authorization.contains("response_type=code"))
        assertTrue(authorization.contains("state=${flow.state}"))
        assertTrue(authorization.contains("nonce=${flow.nonce}"))
        assertTrue(authorization.contains("code_challenge_method=S256"))
        assertTrue(authorization.contains("code_challenge="))
        assertNotEquals(flow.codeVerifier, authorization.substringAfter("code_challenge=").substringBefore("&"))
    }

    @Test
    fun `Kakao authorization needs no business-only personal information scope`() {
        val flow = service.begin(SocialProvider.KAKAO)
        val authorization = service.authorizationUrl(flow)

        assertTrue(authorization.contains("scope=openid"))
        assertTrue(!authorization.contains("account_email"))
        assertTrue(!authorization.contains("profile_nickname"))
        assertTrue(authorization.contains("nonce=${flow.nonce}"))
        assertTrue(authorization.contains("code_challenge_method=S256"))
    }

    @Test
    fun `callback rejects mismatched state before contacting provider`() {
        val flow = service.begin(SocialProvider.GOOGLE)

        val error = assertFailsWith<IllegalArgumentException> { service.complete(flow, "wrong-state", "code") }

        assertEquals("SOCIAL_STATE_INVALID", error.message)
        assertEquals(0, client.calls)
    }

    @Test
    fun `unknown social email never merges a local account`() {
        val local = authentication.signup("same@example.com", "password123", "로컬")
        client.profile = SocialProfile("google-subject", "same@example.com", "구글")
        val flow = service.begin(SocialProvider.GOOGLE)

        val pending = service.complete(flow, flow.state, "code")
        val social = service.finishSignup(pending.pendingIdentity!!, "소셜")

        assertNotEquals(local.account.id, social.account.id)
        assertEquals(local.account.email, social.account.email)
        assertEquals(social.account.id, identities.findAccountId(SocialProvider.GOOGLE, "google-subject"))
    }

    @Test
    fun `last social login method cannot be removed`() {
        client.profile = SocialProfile("google-subject", null, null)
        val flow = service.begin(SocialProvider.GOOGLE)
        val pending = service.complete(flow, flow.state, "code")
        val account = service.finishSignup(pending.pendingIdentity!!, "소셜")

        val error = assertFailsWith<IllegalArgumentException> { service.unlink(account.account.id, SocialProvider.GOOGLE) }

        assertEquals("LAST_LOGIN_METHOD_REQUIRED", error.message)
    }

    private class RecordingProviderClient : SocialProviderClient {
        var calls = 0
        var profile = SocialProfile("subject", "player@example.com", "한짝")
        override fun exchange(provider: SocialProvider, code: String, redirectUri: String, codeVerifier: String, expectedNonce: String, state: String): SocialProfile {
            calls += 1
            return profile
        }
    }

    private class MemorySocialIdentityRepository(private val accounts: MemoryAccountRepository) : SocialIdentityRepository {
        private data class Key(val provider: SocialProvider, val subject: String)
        private data class Stored(val accountId: UUID, val email: String?, val linkedAt: Instant, val lastLoginAt: Instant)
        private val values = mutableMapOf<Key, Stored>()
        override fun findAccountId(provider: SocialProvider, subject: String) = values[Key(provider, subject)]?.accountId
        override fun link(accountId: UUID, provider: SocialProvider, subject: String, email: String?, now: Instant) {
            require(values.keys.none { it.provider == provider && values[it]?.accountId == accountId }) { "SOCIAL_IDENTITY_ALREADY_LINKED" }
            require(values.putIfAbsent(Key(provider, subject), Stored(accountId, email, now, now)) == null) { "SOCIAL_IDENTITY_ALREADY_LINKED" }
        }
        override fun touch(provider: SocialProvider, subject: String, email: String?, now: Instant) {
            val key = Key(provider, subject)
            values[key] = values.getValue(key).copy(email = email, lastLoginAt = now)
        }
        override fun list(accountId: UUID) = values.mapNotNull { (key, value) ->
            value.takeIf { it.accountId == accountId }?.let { LinkedSocialIdentity(key.provider, it.email, it.linkedAt, it.lastLoginAt) }
        }
        override fun unlink(accountId: UUID, provider: SocialProvider): Boolean {
            val entry = values.entries.firstOrNull { it.key.provider == provider && it.value.accountId == accountId } ?: return false
            values.remove(entry.key)
            return true
        }
        override fun hasPassword(accountId: UUID) = accounts.findById(accountId)?.passwordHash != null
    }

    private class MemoryAccountRepository : AccountRepository {
        private val accounts = mutableMapOf<UUID, Account>()
        private val characters = mutableMapOf<UUID, Character>()
        override fun findByEmail(email: String) = accounts.values.firstOrNull { it.email == email && it.passwordHash != null }
        override fun findById(id: UUID) = accounts[id]
        override fun findCharacter(accountId: UUID) = characters.values.firstOrNull { it.accountId == accountId }
        override fun save(account: Account) = account.also { accounts[it.id] = it }
        override fun saveCharacter(character: Character) = character.also { characters[it.id] = it }
        override fun updateCharacterNickname(accountId: UUID, nickname: String) = findCharacter(accountId)?.copy(nickname = nickname)?.also { characters[it.id] = it }
        override fun incrementStateVersion(id: UUID) = accounts[id]?.copy(stateVersion = accounts.getValue(id).stateVersion + 1)?.also { accounts[id] = it }
        override fun updatePasswordAndIncrementStateVersion(id: UUID, passwordHash: String) = accounts[id]?.copy(passwordHash = passwordHash, stateVersion = accounts.getValue(id).stateVersion + 1)?.also { accounts[id] = it }
        override fun softDelete(id: UUID, anonymizedEmail: String, anonymizedNickname: String) = false
    }
}
