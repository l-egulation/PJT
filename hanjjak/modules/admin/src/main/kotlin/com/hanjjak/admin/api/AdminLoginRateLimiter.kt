package com.hanjjak.admin.api

import org.springframework.stereotype.Component
import java.time.Clock
import java.util.concurrent.ConcurrentHashMap

@Component
class AdminLoginRateLimiter(
    private val properties: AdminProperties,
    private val clock: Clock,
) {
    class LimitExceeded(val retryAfterSeconds: Long) : RuntimeException("ADMIN_RATE_LIMITED")
    private data class Window(val resetAt: Long, val count: Int)
    private val windows = ConcurrentHashMap<String, Window>()

    fun check(remoteAddress: String) {
        val limit = properties.authenticationRateLimit
        require(limit.maxRequests > 0 && !limit.window.isNegative && !limit.window.isZero)
        val now = clock.instant().epochSecond
        val resetAfter = limit.window.seconds.coerceAtLeast(1)
        listOf("ip:$remoteAddress").forEach { key ->
            val updated = windows.compute(key) { _, current ->
                if (current == null || now >= current.resetAt) Window(now + resetAfter, 1) else current.copy(count = current.count + 1)
            }!!
            if (updated.count > limit.maxRequests) throw LimitExceeded((updated.resetAt - now).coerceAtLeast(1))
        }
    }

}
