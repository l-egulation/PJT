package com.hanjjak.account.application

import com.hanjjak.account.domain.Account
import com.hanjjak.account.domain.Character
import org.springframework.security.crypto.password.PasswordEncoder
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.time.Clock
import java.util.Locale
import java.util.UUID

@Service
class AuthenticationService(
    private val accounts: AccountRepository,
    private val passwordEncoder: PasswordEncoder,
    private val clock: Clock,
    private val balanceVersions: BalanceVersionService = BalanceVersionService.noop(),
) {
    @Transactional
    fun signup(email: String, password: String, nickname: String): AuthenticationResult {
        val normalizedEmail = normalizeEmail(email)
        val normalizedNickname = normalizeNickname(nickname)
        require(password.length >= 8) { "PASSWORD_TOO_SHORT" }
        require(accounts.findByEmail(normalizedEmail) == null) { "EMAIL_ALREADY_EXISTS" }
        val account = accounts.save(Account(UUID.randomUUID(), normalizedEmail, passwordEncoder.encode(password), normalizedEmail, 1, clock.instant()))
        balanceVersions.assignNewAccount(account.id)
        val character = accounts.saveCharacter(Character(UUID.randomUUID(), account.id, normalizedNickname, 1, 0, 0))
        return AuthenticationResult(account, character)
    }

    fun login(email: String, password: String): AuthenticationResult {
        val account = accounts.findByEmail(normalizeEmail(email)) ?: throw IllegalArgumentException("LOGIN_FAILED")
        val passwordHash = account.passwordHash ?: throw IllegalArgumentException("LOGIN_FAILED")
        require(passwordEncoder.matches(password, passwordHash)) { "LOGIN_FAILED" }
        val character = accounts.findCharacter(account.id) ?: error("CHARACTER_NOT_FOUND")
        return AuthenticationResult(account, character)
    }

    fun session(accountId: UUID): AuthenticationResult {
        val account = accounts.findById(accountId) ?: throw IllegalArgumentException("LOGIN_FAILED")
        val character = accounts.findCharacter(accountId) ?: error("CHARACTER_NOT_FOUND")
        return AuthenticationResult(account, character)
    }
    fun balanceVersion(accountId: UUID): String = balanceVersions.current(accountId)

    @Transactional
    fun updateNickname(accountId: UUID, nickname: String): AuthenticationResult {
        val character = accounts.updateCharacterNickname(accountId, normalizeNickname(nickname)) ?: error("CHARACTER_NOT_FOUND")
        val account = accounts.incrementStateVersion(accountId) ?: error("ACCOUNT_NOT_FOUND")
        return AuthenticationResult(account, character)
    }
    @Transactional
    fun changePassword(accountId: UUID, currentPassword: String, newPassword: String, newPasswordConfirmation: String): AuthenticationResult {
        val account = accounts.findById(accountId) ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val passwordHash = account.passwordHash ?: throw IllegalArgumentException("PASSWORD_LOGIN_NOT_CONFIGURED")
        require(passwordEncoder.matches(currentPassword, passwordHash)) { "CURRENT_PASSWORD_INVALID" }
        require(newPassword.length >= 8) { "PASSWORD_TOO_SHORT" }
        require(newPassword == newPasswordConfirmation) { "PASSWORD_CONFIRMATION_MISMATCH" }
        val updated = accounts.updatePasswordAndIncrementStateVersion(accountId, passwordEncoder.encode(newPassword))
            ?: error("ACCOUNT_NOT_FOUND")
        val character = accounts.findCharacter(accountId) ?: error("CHARACTER_NOT_FOUND")
        return AuthenticationResult(updated, character)
    }
    @Transactional
    fun signupSocial(email: String, nickname: String): AuthenticationResult {
        val normalizedEmail = normalizeEmail(email)
        val normalizedNickname = normalizeNickname(nickname)
        val account = accounts.save(Account(UUID.randomUUID(), normalizedEmail, null, null, 1, clock.instant()))
        balanceVersions.assignNewAccount(account.id)
        val character = accounts.saveCharacter(Character(UUID.randomUUID(), account.id, normalizedNickname, 1, 0, 0))
        return AuthenticationResult(account, character)
    }

    @Transactional
    fun deleteAccount(accountId: UUID): Unit {
        val account = accounts.findById(accountId) ?: throw IllegalArgumentException("ACCOUNT_NOT_FOUND")
        val anonymizedEmail = "deleted-${account.id}@deleted.local"
        val anonymizedNickname = "탈퇴한 사용자"
        require(accounts.softDelete(account.id, anonymizedEmail, anonymizedNickname)) { "ACCOUNT_ALREADY_DELETED" }
    }

    private fun normalizeNickname(nickname: String): String {
        val normalized = nickname.trim()
        require(normalized.isNotEmpty()) { "NICKNAME_REQUIRED" }
        require(normalized.length <= 20) { "NICKNAME_TOO_LONG" }
        return normalized
    }

    private fun normalizeEmail(email: String): String {
        val normalized = email.trim().lowercase(Locale.ROOT)
        require(normalized.length <= 320 && EMAIL_PATTERN.matches(normalized)) { "INVALID_EMAIL" }
        return normalized
    }

    private companion object {
        val EMAIL_PATTERN = Regex("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$")
    }
}
