package com.hanjjak.account.api

import org.springframework.stereotype.Component
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicLong

class RateLimitExceededException(
    val code: String,
    val retryAfterSeconds: Long,
) : RuntimeException(code)

@Component
class RequestRateLimiter(
    private val properties: ApiSecurityProperties,
    private val clock: Clock,
) {
    private data class Window(val resetAtEpochSecond: Long, val count: Int)

    private val windows = ConcurrentHashMap<String, Window>()
    private val requestCount = AtomicLong()

    fun checkAuthentication(email: String, remoteAddress: String, login: Boolean) {
        val normalizedEmailHash = sha256(email.trim().lowercase())
        requireAllowed(
            properties.authenticationRateLimit,
            listOf("auth:ip:$remoteAddress", "auth:account:$normalizedEmailHash"),
            if (login) "LOGIN_FAILED" else "RATE_LIMITED",
        )
    }
    fun checkPasswordReset(email: String, remoteAddress: String) {
        val normalizedEmailHash = sha256(email.trim().lowercase())
        requireAllowed(
            properties.passwordResetRateLimit,
            listOf("password-reset:ip:$remoteAddress", "password-reset:email:$normalizedEmailHash"),
            "PASSWORD_RESET_REQUEST_ACCEPTED",
        )
    }

    fun checkMutation(accountId: UUID?, remoteAddress: String) {
        val keys = buildList {
            add("mutation:ip:$remoteAddress")
            if (accountId != null) add("mutation:account:$accountId")
        }
        requireAllowed(properties.mutationRateLimit, keys, "RATE_LIMITED")
    }

    private fun requireAllowed(limit: ApiSecurityProperties.RateLimit, keys: List<String>, errorCode: String) {
        val nowSeconds = clock.instant().epochSecond
        val windowSeconds = limit.window.seconds.coerceAtLeast(1)

        for (key in keys) {
            val updated = windows.compute(key) { _, current ->
                if (current == null || nowSeconds >= current.resetAtEpochSecond) {
                    Window(nowSeconds + windowSeconds, 1)
                } else {
                    current.copy(count = current.count + 1)
                }
            }!!
            if (updated.count > limit.maxRequests) {
                val retryAfter = updated.resetAtEpochSecond - nowSeconds
                throw RateLimitExceededException(errorCode, retryAfter.coerceAtLeast(1))
            }
        }

        if (requestCount.incrementAndGet() % 1024L == 0L) {
            windows.entries.removeIf { it.value.resetAtEpochSecond <= nowSeconds }
        }
    }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
}
