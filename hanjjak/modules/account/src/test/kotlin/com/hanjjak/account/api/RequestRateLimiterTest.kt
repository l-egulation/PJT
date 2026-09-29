package com.hanjjak.account.api

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.ZoneId
import java.time.ZoneOffset

class RequestRateLimiterTest {
    @Test
    fun `authentication attempts are limited by both account and address`() {
        val limiter = RequestRateLimiter(properties(authMax = 2), Clock.fixed(Instant.parse("2026-09-07T00:00:00Z"), ZoneOffset.UTC))

        limiter.checkAuthentication("player@example.com", "127.0.0.1", true)
        limiter.checkAuthentication("player@example.com", "127.0.0.1", true)

        val error = assertFailsWith<RateLimitExceededException> {
            limiter.checkAuthentication("player@example.com", "127.0.0.1", true)
        }
        assertEquals("LOGIN_FAILED", error.code)
        assertEquals(60, error.retryAfterSeconds)
    }

    @Test
    fun `password reset requests are limited independently by email and address`() {
        val limiter = RequestRateLimiter(properties(authMax = 10, resetMax = 1), Clock.fixed(Instant.parse("2026-09-07T00:00:00Z"), ZoneOffset.UTC))

        limiter.checkPasswordReset("player@example.com", "127.0.0.1")

        val error = assertFailsWith<RateLimitExceededException> {
            limiter.checkPasswordReset("player@example.com", "127.0.0.2")
        }
        assertEquals("PASSWORD_RESET_REQUEST_ACCEPTED", error.code)
    }

    @Test
    fun `a new window accepts requests again`() {
        val clock = MutableClock(Instant.parse("2026-09-07T00:00:00Z"))
        val limiter = RequestRateLimiter(properties(authMax = 1), clock)

        limiter.checkAuthentication("player@example.com", "127.0.0.1", false)
        assertFailsWith<RateLimitExceededException> {
            limiter.checkAuthentication("player@example.com", "127.0.0.1", false)
        }

        clock.advance(Duration.ofMinutes(1))
        limiter.checkAuthentication("player@example.com", "127.0.0.1", false)
    }

    private fun properties(authMax: Int, resetMax: Int = 3) = ApiSecurityProperties(
        authenticationRateLimit = ApiSecurityProperties.RateLimit(authMax, Duration.ofMinutes(1)),
        passwordResetRateLimit = ApiSecurityProperties.RateLimit(resetMax, Duration.ofMinutes(15)),
        mutationRateLimit = ApiSecurityProperties.RateLimit(10, Duration.ofMinutes(1)),
    )

    private class MutableClock(private var current: Instant) : Clock() {
        override fun getZone(): ZoneId = ZoneOffset.UTC
        override fun withZone(zone: ZoneId): Clock = this
        override fun instant(): Instant = current
        fun advance(duration: Duration) {
            current = current.plus(duration)
        }
    }
}
