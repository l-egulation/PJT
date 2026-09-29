package com.hanjjak.account.application

import java.io.Serializable
import java.time.Instant
import java.util.UUID

enum class SocialProvider(val id: String, val displayName: String) {
    GOOGLE("google", "Google"),
    KAKAO("kakao", "Kakao"),
    NAVER("naver", "Naver"),
    SSAFY_GITLAB("ssafy-gitlab", "SSAFY GitLab");

    companion object {
        fun fromId(value: String): SocialProvider = entries.firstOrNull { it.id == value }
            ?: throw IllegalArgumentException("SOCIAL_PROVIDER_NOT_FOUND")
    }
}

data class SocialProviderStatus(val provider: SocialProvider, val enabled: Boolean)
data class SocialAuthorization(val authorizationUrl: String)
data class SocialProfile(val subject: String, val email: String?, val nickname: String?)
data class PendingSocialSignup(val token: String, val provider: SocialProvider, val email: String?, val suggestedNickname: String?, val expiresAt: Instant) : Serializable
data class SocialLoginResult(val authentication: AuthenticationResult?, val pendingSignup: PendingSocialSignup?, val pendingIdentity: PendingSocialIdentity?)
data class LinkedSocialIdentity(val provider: SocialProvider, val email: String?, val linkedAt: Instant, val lastLoginAt: Instant)
data class SocialFlow(
    val state: String,
    val provider: SocialProvider,
    val nonce: String,
    val codeVerifier: String,
    val accountId: UUID?,
    val expiresAt: Instant,
) : Serializable
data class PendingSocialIdentity(
    val token: String,
    val provider: SocialProvider,
    val subject: String,
    val email: String?,
    val suggestedNickname: String?,
    val expiresAt: Instant,
) : Serializable
