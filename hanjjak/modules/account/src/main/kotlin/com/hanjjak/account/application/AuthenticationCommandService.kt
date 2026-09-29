package com.hanjjak.account.application

import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import org.springframework.security.crypto.password.PasswordEncoder
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.util.Locale
import java.util.UUID

@Service
class AuthenticationCommandService(
    private val authentication: AuthenticationService,
    private val commands: AuthenticationCommandRepository,
    private val accounts: AccountRepository,
    private val passwordEncoder: PasswordEncoder,
) {
    @Transactional
    fun signup(key: UUID, email: String, password: String, nickname: String): AuthenticationResult = execute(key, authFingerprint("signup", email, password, nickname)) {
        authentication.signup(email, password, nickname)
    }

    @Transactional
    fun login(key: UUID, email: String, password: String): AuthenticationResult = execute(key, authFingerprint("login", email, password)) {
        authentication.login(email, password)
    }

    @Transactional
    fun updateNickname(key: UUID, accountId: UUID, nickname: String): AuthenticationResult = execute(key, sha256("updateNickname\u0000$accountId\u0000${nickname.trim()}")) {
        authentication.updateNickname(accountId, nickname)
    }
    @Transactional
    fun changePassword(
        key: UUID,
        accountId: UUID,
        currentPassword: String,
        newPassword: String,
        newPasswordConfirmation: String,
    ): AuthenticationResult = execute(
        key,
        authFingerprint("changePassword", accountId.toString(), currentPassword, "$newPassword\u0000$newPasswordConfirmation"),
    ) {
        authentication.changePassword(accountId, currentPassword, newPassword, newPasswordConfirmation)
    }


    private fun execute(key: UUID, fingerprint: String, action: () -> AuthenticationResult): AuthenticationResult {
        commands.lock(key)
        val existing = commands.find(key)
        if (existing != null) {
            require(matchesFingerprint(fingerprint, existing.fingerprint)) { "IDEMPOTENCY_KEY_REUSED" }
            val account = accounts.findById(existing.accountId) ?: error("ACCOUNT_NOT_FOUND")
            val character = accounts.findCharacter(existing.accountId) ?: error("CHARACTER_NOT_FOUND")
            return AuthenticationResult(account, character)
        }
        val result = action()
        commands.save(AuthenticationCommand(key, passwordEncoder.encode(fingerprint), result.account.id, result.character.id))
        return result
    }

    private fun authFingerprint(operation: String, email: String, password: String, nickname: String? = null): String =
        if (nickname == null) {
            sha256("$operation\u0000${email.trim().lowercase(Locale.ROOT)}\u0000$password")
        } else {
            sha256("$operation\u0000${email.trim().lowercase(Locale.ROOT)}\u0000$password\u0000${nickname.trim()}")
        }

    private fun matchesFingerprint(candidate: String, stored: String): Boolean =
        if (stored.startsWith("$2")) passwordEncoder.matches(candidate, stored) else stored == candidate

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
}
