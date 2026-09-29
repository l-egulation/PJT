package com.hanjjak.account.application

import org.springframework.boot.context.properties.ConfigurationProperties
import org.springframework.security.crypto.password.PasswordEncoder
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.net.URI
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.security.SecureRandom
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.util.Base64
import java.util.Locale
import java.util.UUID

@ConfigurationProperties("hanjjak.password-reset")
data class PasswordResetProperties(
    val publicBaseUrl: URI = URI("https://localhost:5173"),
    val tokenLifetime: Duration = Duration.ofMinutes(30),
    val mail: Mail = Mail(),
) {
    data class Mail(
        val from: String = "",
        val subject: String = "한짝 비밀번호 재설정",
    )

    init {
        require(
            publicBaseUrl.scheme == "https" && publicBaseUrl.host != null && publicBaseUrl.rawUserInfo == null &&
                publicBaseUrl.rawQuery == null && publicBaseUrl.rawFragment == null && publicBaseUrl.rawPath in setOf("", "/"),
        ) { "hanjjak.password-reset.public-base-url must be an HTTPS origin" }
        require(!tokenLifetime.isZero && !tokenLifetime.isNegative) { "hanjjak.password-reset.token-lifetime must be positive" }
    }
}

data class PasswordResetToken(
    val id: UUID,
    val accountId: UUID,
    val tokenHash: String,
    val createdAt: Instant,
    val expiresAt: Instant,
    val consumedAt: Instant?,
)

interface PasswordResetTokenRepository {
    fun replaceActive(accountId: UUID, token: PasswordResetToken)
    fun consume(tokenHash: String, consumedAt: Instant): PasswordResetToken?
    fun updatePassword(accountId: UUID, passwordHash: String): Long
}

fun interface PasswordResetMailSender {
    fun send(to: String, resetLink: URI)
}

@Service
class PasswordResetService(
    private val accounts: AccountRepository,
    private val tokens: PasswordResetTokenRepository,
    private val mailSender: PasswordResetMailSender,
    private val passwordEncoder: PasswordEncoder,
    private val gameSessions: GameSessionLifecycle,
    private val authenticationSessions: AuthenticationSessionLifecycle,
    private val clock: Clock,
    private val properties: PasswordResetProperties,
) {
    private val secureRandom = SecureRandom()

    @Transactional
    fun request(email: String) {
        val normalizedEmail = normalizeEmail(email)
        val account = accounts.findByEmail(normalizedEmail) ?: return
        val rawToken = ByteArray(TOKEN_BYTES).also(secureRandom::nextBytes)
            .let(Base64.getUrlEncoder().withoutPadding()::encodeToString)
        val now = clock.instant()
        val token = PasswordResetToken(
            UUID.randomUUID(),
            account.id,
            sha256(rawToken),
            now,
            now.plus(properties.tokenLifetime),
            null,
        )
        tokens.replaceActive(account.id, token)
        mailSender.send(account.email, resetLink(rawToken))
    }

    @Transactional
    fun reset(rawToken: String, newPassword: String) {
        require(newPassword.length >= 8) { "PASSWORD_TOO_SHORT" }
        require(rawToken.isNotBlank() && rawToken.length <= MAX_TOKEN_LENGTH) { "PASSWORD_RESET_TOKEN_INVALID" }
        val consumed = tokens.consume(sha256(rawToken), clock.instant())
            ?: throw IllegalArgumentException("PASSWORD_RESET_TOKEN_INVALID")
        tokens.updatePassword(consumed.accountId, passwordEncoder.encode(newPassword))
        authenticationSessions.closeForPasswordReset(consumed.accountId)
        gameSessions.closeForPasswordReset(consumed.accountId)
    }

    private fun resetLink(rawToken: String): URI {
        val origin = properties.publicBaseUrl.toString().trimEnd('/')
        return URI.create("$origin/reset-password#token=$rawToken")
    }

    private fun normalizeEmail(email: String): String {
        val normalized = email.trim().lowercase(Locale.ROOT)
        require(normalized.length <= 320 && EMAIL_PATTERN.matches(normalized)) { "INVALID_EMAIL" }
        return normalized
    }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8))
        .joinToString("") { "%02x".format(it) }

    private companion object {
        const val TOKEN_BYTES = 32
        const val MAX_TOKEN_LENGTH = 128
        val EMAIL_PATTERN = Regex("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$")
    }
}
