package com.hanjjak.account.api

import org.springframework.boot.context.properties.ConfigurationProperties
import java.time.Duration

@ConfigurationProperties("hanjjak.api-security")
data class ApiSecurityProperties(
    val allowedOrigins: List<String> = listOf("http://localhost:5173", "http://127.0.0.1:5173"),
    val absoluteSessionLifetime: Duration = Duration.ofDays(30),
    val authenticationRateLimit: RateLimit = RateLimit(10, Duration.ofMinutes(1)),
    val passwordResetRateLimit: RateLimit = RateLimit(3, Duration.ofMinutes(15)),
    val mutationRateLimit: RateLimit = RateLimit(120, Duration.ofMinutes(1)),
    val adminAuthenticationRateLimit: RateLimit = RateLimit(5, Duration.ofMinutes(1)),
    val legacyBattleRewardApiEnabled: Boolean = false,
) {
    data class RateLimit(
        val maxRequests: Int,
        val window: Duration,
    ) {
        init {
            require(maxRequests > 0) { "maxRequests must be positive" }
            require(!window.isZero && !window.isNegative) { "window must be positive" }
        }
    }
}
