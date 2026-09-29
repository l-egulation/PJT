package com.hanjjak.account.application

import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.util.UriComponentsBuilder
import java.security.MessageDigest
import java.security.SecureRandom
import java.time.Clock
import java.time.Instant
import java.util.Base64
import java.util.UUID

@Service
class SocialLoginService(
    private val properties: SocialLoginProperties,
    private val providerClient: SocialProviderClient,
    private val identities: SocialIdentityRepository,
    private val authentication: AuthenticationService,
    private val clock: Clock,
    private val secureRandom: SecureRandom,
) {
    fun statuses(): List<SocialProviderStatus> = SocialProvider.entries.map { provider ->
        SocialProviderStatus(provider, configured(provider) != null)
    }

    fun begin(provider: SocialProvider, accountId: UUID? = null): SocialFlow {
        val configuration = requireConfigured(provider)
        val state = randomToken(32)
        val nonce = randomToken(32)
        val verifier = randomToken(64)
        return SocialFlow(state, provider, nonce, verifier, accountId, clock.instant().plus(properties.flowLifetime)).also {
            authorizationUrl(it, configuration)
        }
    }

    fun authorizationUrl(flow: SocialFlow): String = authorizationUrl(flow, requireConfigured(flow.provider))

    fun complete(flow: SocialFlow, state: String, code: String): SocialLoginResult {
        require(clock.instant().isBefore(flow.expiresAt)) { "SOCIAL_LOGIN_EXPIRED" }
        require(constantTimeEquals(flow.state, state)) { "SOCIAL_STATE_INVALID" }
        val profile = providerClient.exchange(
            flow.provider,
            code,
            callbackUri(flow.provider),
            flow.codeVerifier,
            flow.nonce,
            flow.state,
        )
        val existingAccount = identities.findAccountId(flow.provider, profile.subject)
        if (flow.accountId != null) {
            if (existingAccount != null && existingAccount != flow.accountId) {
                throw IllegalArgumentException("SOCIAL_IDENTITY_ALREADY_LINKED")
            }
            if (existingAccount == null) identities.link(flow.accountId, flow.provider, profile.subject, profile.email, clock.instant())
            else identities.touch(flow.provider, profile.subject, profile.email, clock.instant())
            return SocialLoginResult(authentication.session(flow.accountId), null, null)
        }
        if (existingAccount != null) {
            identities.touch(flow.provider, profile.subject, profile.email, clock.instant())
            return SocialLoginResult(authentication.session(existingAccount), null, null)
        }
        val expiresAt = clock.instant().plus(properties.flowLifetime)
        val pendingIdentity = PendingSocialIdentity(
            randomToken(32),
            flow.provider,
            profile.subject,
            profile.email,
            suggestedNickname(profile.nickname),
            expiresAt,
        )
        val pending = PendingSocialSignup(
            pendingIdentity.token,
            flow.provider,
            profile.email,
            pendingIdentity.suggestedNickname,
            expiresAt,
        )
        return SocialLoginResult(null, pending, pendingIdentity)
    }

    @Transactional
    fun finishSignup(pending: PendingSocialIdentity, nickname: String): AuthenticationResult {
        require(clock.instant().isBefore(pending.expiresAt)) { "SOCIAL_SIGNUP_EXPIRED" }
        require(identities.findAccountId(pending.provider, pending.subject) == null) { "SOCIAL_IDENTITY_ALREADY_LINKED" }
        val result = authentication.signupSocial(displayEmail(pending), nickname)
        identities.link(result.account.id, pending.provider, pending.subject, pending.email, clock.instant())
        return result
    }


    fun list(accountId: UUID): List<LinkedSocialIdentity> = identities.list(accountId)

    @Transactional
    fun unlink(accountId: UUID, provider: SocialProvider) {
        val linked = identities.list(accountId)
        require(linked.any { it.provider == provider }) { "SOCIAL_IDENTITY_NOT_LINKED" }
        require(identities.hasPassword(accountId) || linked.size > 1) { "LAST_LOGIN_METHOD_REQUIRED" }
        require(identities.unlink(accountId, provider)) { "SOCIAL_IDENTITY_NOT_LINKED" }
    }
    fun passwordEnabled(accountId: UUID): Boolean = identities.hasPassword(accountId)

    fun callbackUri(provider: SocialProvider): String = "${properties.publicBaseUrl.trimEnd('/')}/api/v1/auth/social/${provider.id}/callback"
    fun pendingSignupUrl(token: String): String = UriComponentsBuilder.fromUriString(properties.frontendSuccessUrl)
        .fragment("social-signup=$token")
        .build()
        .toUriString()
    fun successUrl(): String = properties.frontendSuccessUrl
    fun failureUrl(code: String): String = UriComponentsBuilder.fromUriString(properties.frontendFailureUrl)
        .fragment("social-error=$code")
        .build()
        .toUriString()

    fun configured(provider: SocialProvider): SocialLoginProperties.Provider? = properties.providers[provider.id]
        ?.takeIf { it.enabled && it.clientId.isNotBlank() && it.clientSecret.isNotBlank() && it.authorizationUri.isNotBlank() && it.tokenUri.isNotBlank() && (!it.oidc || it.issuer.isNotBlank() && it.jwkSetUri.isNotBlank()) && (it.oidc || it.userInfoUri.isNotBlank()) }

    private fun requireConfigured(provider: SocialProvider): SocialLoginProperties.Provider = configured(provider)
        ?: throw IllegalArgumentException("SOCIAL_PROVIDER_DISABLED")

    private fun authorizationUrl(flow: SocialFlow, provider: SocialLoginProperties.Provider): String {
        val builder = UriComponentsBuilder.fromUriString(provider.authorizationUri)
            .queryParam("response_type", "code")
            .queryParam("client_id", provider.clientId)
            .queryParam("redirect_uri", callbackUri(flow.provider))
            .queryParam("scope", provider.scopes.joinToString(" "))
            .queryParam("state", flow.state)
        if (provider.oidc) builder.queryParam("nonce", flow.nonce)
        if (provider.pkce) {
            builder.queryParam("code_challenge", sha256Base64Url(flow.codeVerifier))
            builder.queryParam("code_challenge_method", "S256")
        }
        return builder.build().encode().toUriString()
    }

    private fun displayEmail(pending: PendingSocialIdentity): String = pending.email
        ?.trim()
        ?.lowercase()
        ?.takeIf { it.length <= 320 && it.contains('@') }
        ?: "${pending.provider.id}-${sha256Hex(pending.subject).take(32)}@social.hanjjak.local"

    private fun suggestedNickname(value: String?): String? = value?.trim()?.takeIf(String::isNotBlank)?.take(20)

    private fun randomToken(bytes: Int): String = ByteArray(bytes).also(secureRandom::nextBytes)
        .let(Base64.getUrlEncoder().withoutPadding()::encodeToString)

    private fun constantTimeEquals(left: String, right: String): Boolean = MessageDigest.isEqual(left.toByteArray(), right.toByteArray())
    private fun sha256Base64Url(value: String): String = MessageDigest.getInstance("SHA-256").digest(value.toByteArray())
        .let(Base64.getUrlEncoder().withoutPadding()::encodeToString)
    private fun sha256Hex(value: String): String = MessageDigest.getInstance("SHA-256").digest(value.toByteArray())
        .joinToString("") { "%02x".format(it) }
}
