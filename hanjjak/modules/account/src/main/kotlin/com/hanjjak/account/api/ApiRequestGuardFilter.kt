package com.hanjjak.account.api

import com.fasterxml.jackson.databind.ObjectMapper
import jakarta.servlet.FilterChain
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import jakarta.servlet.http.HttpSession
import org.slf4j.LoggerFactory
import org.springframework.http.MediaType
import org.springframework.web.filter.OncePerRequestFilter
import java.net.URI
import java.time.Clock
import java.time.Instant
import java.util.UUID

class ApiRequestGuardFilter(
    private val properties: ApiSecurityProperties,
    private val sessions: AccountSessionRegistry,
    private val rateLimiter: RequestRateLimiter,
    private val objectMapper: ObjectMapper,
    private val clock: Clock,
) : OncePerRequestFilter() {
    data class ErrorEnvelope(
        val requestId: UUID,
        val serverTime: Instant,
        val code: String,
        val messageKey: String,
        val retryable: Boolean,
        val details: List<Any>? = null,
    )

    override fun shouldNotFilter(request: HttpServletRequest): Boolean = !request.requestURI.startsWith("/api/v1/")

    override fun doFilterInternal(request: HttpServletRequest, response: HttpServletResponse, filterChain: FilterChain) {
        var session = request.getSession(false)
        session = validateSession(session)
        if (isEconomyMetricsEndpoint(request.requestURI)) {
            val accountId = session?.getAttribute("accountId") as? UUID
            if (accountId == null) {
                writeError(response, HttpServletResponse.SC_UNAUTHORIZED, "AUTHENTICATION_REQUIRED", false)
            } else {
                writeError(response, HttpServletResponse.SC_FORBIDDEN, "ECONOMY_METRICS_ADMIN_ONLY", false)
            }
            return
        }


        if (!properties.legacyBattleRewardApiEnabled && isLegacyBattleRewardEndpoint(request)) {
            val accountId = session?.getAttribute("accountId") as? UUID
            auditLogger.warn(
                "Blocked legacy battle reward endpoint accountId={} path={} remoteAddress={}",
                accountId,
                request.requestURI,
                request.remoteAddr.orEmpty().ifBlank { "unknown" },
            )
            writeError(response, HttpServletResponse.SC_FORBIDDEN, "BATTLE_COMPATIBILITY_API_DISABLED", false)
            return
        }

        if (isMutation(request.method) && !originAllowed(request)) {
            writeError(response, HttpServletResponse.SC_FORBIDDEN, "ORIGIN_FORBIDDEN", false)
            return
        }

        if (isMutation(request.method) && !isAuthenticationAttempt(request.requestURI)) {
            val accountId = session?.getAttribute("accountId") as? UUID
            try {
                rateLimiter.checkMutation(accountId, request.remoteAddr.orEmpty().ifBlank { "unknown" })
            } catch (exception: RateLimitExceededException) {
                response.setHeader("Retry-After", exception.retryAfterSeconds.toString())
                writeError(response, 429, exception.code, true)
                return
            }
        }

        filterChain.doFilter(request, response)
    }

    private fun validateSession(session: HttpSession?): HttpSession? {
        if (session == null) return null
        val accountId = session.getAttribute("accountId") as? UUID ?: return session
        val absoluteExpiry = session.creationTime + properties.absoluteSessionLifetime.toMillis()
        val expired = clock.millis() >= absoluteExpiry
        if (!expired && sessions.isActive(accountId, session.id)) return session

        sessions.deactivate(accountId, session.id)
        session.invalidate()
        return null
    }

    private fun originAllowed(request: HttpServletRequest): Boolean {
        val rawOrigin = request.getHeader("Origin") ?: return false
        val origin = normalizedOrigin(rawOrigin) ?: return false
        if (properties.allowedOrigins.mapNotNull(::normalizedOrigin).contains(origin)) return true
        return origin == requestOrigin(request)
    }

    private fun requestOrigin(request: HttpServletRequest): String {
        val forwardedProto = request.getHeader("X-Forwarded-Proto")?.substringBefore(',')?.trim()?.takeIf { it in setOf("http", "https") } ?: request.scheme
        val forwardedHost = request.getHeader("X-Forwarded-Host")?.substringBefore(',')?.trim()?.takeIf { it.isNotBlank() } ?: request.serverName
        val host = forwardedHost.substringBefore(':')
        val port = forwardedHost.substringAfter(':', "").toIntOrNull()
        val defaultPort = forwardedProto == "http" && (port == null || port == 80) || forwardedProto == "https" && (port == null || port == 443)
        return "$forwardedProto://$host${if (defaultPort || port == null) "" else ":$port"}"
    }

    private fun normalizedOrigin(value: String): String? = runCatching {
        val uri = URI(value)
        if (uri.scheme !in setOf("http", "https") || uri.host == null || uri.rawUserInfo != null || uri.rawPath !in setOf("", null) || uri.rawQuery != null || uri.rawFragment != null) return null
        val defaultPort = uri.scheme == "http" && uri.port in setOf(-1, 80) || uri.scheme == "https" && uri.port in setOf(-1, 443)
        "${uri.scheme.lowercase()}://${uri.host.lowercase()}${if (defaultPort) "" else ":${uri.port}"}"
    }.getOrNull()

    private fun writeError(response: HttpServletResponse, status: Int, code: String, retryable: Boolean) {
        response.status = status
        response.contentType = MediaType.APPLICATION_JSON_VALUE
        objectMapper.writeValue(
            response.outputStream,
            ErrorEnvelope(UUID.randomUUID(), clock.instant(), code, "error.${code.lowercase().replace('_', '.')}", retryable),
        )
    }

    private fun isEconomyMetricsEndpoint(uri: String): Boolean = uri == "/api/v1/metrics/economy" || uri.startsWith("/api/v1/metrics/economy/")

    private fun isLegacyBattleRewardEndpoint(request: HttpServletRequest): Boolean =
        isMutation(request.method) && (
            request.requestURI == "/api/v1/battles/cycles" ||
                request.requestURI.matches(LEGACY_CHAPTER_AUTO_RUN)
            )

    private fun isAuthenticationAttempt(uri: String): Boolean = uri in setOf(
        "/api/v1/auth/login",
        "/api/v1/auth/signup",
        "/api/v1/auth/password-reset/request",
        "/api/v1/auth/password-reset/confirm",
        "/api/v1/auth/social/signup",
    ) || uri.matches(Regex("/api/v1/auth/social/[^/]+/begin"))

    private fun isMutation(method: String): Boolean = method in setOf("POST", "PUT", "PATCH", "DELETE")

    private companion object {
        val auditLogger = LoggerFactory.getLogger(ApiRequestGuardFilter::class.java)
        val LEGACY_CHAPTER_AUTO_RUN = Regex("/api/v1/battles/chapters/[^/]+/auto-run")
    }

}
