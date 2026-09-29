package com.hanjjak.admin.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.admin.application.AdminAuthenticationService
import com.hanjjak.admin.domain.AdminPermission
import com.hanjjak.admin.infrastructure.AdminAuditRepository
import jakarta.servlet.FilterChain
import jakarta.servlet.http.Cookie
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.http.MediaType
import org.springframework.web.filter.OncePerRequestFilter
import java.net.URI
import java.time.Clock
import java.time.Instant
import java.util.UUID

class AdminAccessFilter(
    private val authentication: AdminAuthenticationService,
    private val audit: AdminAuditRepository,
    private val allowedOrigins: List<String>,
    private val mapper: ObjectMapper,
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

    override fun shouldNotFilter(request: HttpServletRequest): Boolean = !request.requestURI.startsWith("/api/admin/v1/")

    override fun doFilterInternal(request: HttpServletRequest, response: HttpServletResponse, chain: FilterChain) {
        val requestId = request.getHeader("X-Request-Id")?.let { runCatching { UUID.fromString(it) }.getOrNull() } ?: UUID.randomUUID()
        request.setAttribute(REQUEST_ID_ATTRIBUTE, requestId)
        response.setHeader("X-Request-Id", requestId.toString())

        if (isMutation(request.method) && !originAllowed(request)) {
            auditAttempt(request, requestId, null, "ORIGIN_REJECTED", "FAILED")
            writeError(response, 403, "ADMIN_ORIGIN_FORBIDDEN", requestId)
            return
        }

        val token = request.cookies?.firstOrNull { it.name == ADMIN_COOKIE }?.value
        val principal = authentication.authenticate(token)
        if (principal != null) request.setAttribute(PRINCIPAL_ATTRIBUTE, principal)

        if (isPublicAuthenticationRoute(request)) {
            chain.doFilter(request, response)
            return
        }
        if (principal == null) {
            auditAttempt(request, requestId, null, "ACCESS", "FAILED")
            writeError(response, 401, "ADMIN_AUTHENTICATION_REQUIRED", requestId)
            return
        }

        if (request.requestURI.startsWith("/api/admin/v1/catalog")) {
            if (AdminPermission.ACCOUNT_READ !in principal.permissions && AdminPermission.MARKET_MANAGE !in principal.permissions) {
                auditAttempt(request, requestId, principal.operatorId, "ACCESS", "FAILED", principal.username)
                writeError(response, 403, "ADMIN_PERMISSION_DENIED", requestId)
                return
            }
        } else {
            val permission = permissionFor(request)
            if (permission != null && permission !in principal.permissions) {
                auditAttempt(request, requestId, principal.operatorId, "ACCESS", "FAILED", principal.username)
                writeError(response, 403, "ADMIN_PERMISSION_DENIED", requestId)
                return
            }
        }
        chain.doFilter(request, response)
    }

    private fun permissionFor(request: HttpServletRequest): AdminPermission? = when {
        request.requestURI.startsWith("/api/admin/v1/catalog") -> null
        request.requestURI.startsWith("/api/admin/v1/market/anomalies") -> AdminPermission.ECONOMY_READ
        request.requestURI.startsWith("/api/admin/v1/market/quality-check") && isMutation(request.method) -> AdminPermission.OUTBOX_RETRY
        request.requestURI.startsWith("/api/admin/v1/market") -> AdminPermission.MARKET_MANAGE
        request.requestURI.startsWith("/api/admin/v1/chat") -> AdminPermission.CHAT_MODERATE
        request.requestURI.startsWith("/api/admin/v1/audit") -> AdminPermission.AUDIT_READ
        request.requestURI.startsWith("/api/admin/v1/accounts") && isMutation(request.method) -> AdminPermission.USER_MANAGE
        request.requestURI.startsWith("/api/admin/v1/accounts") -> AdminPermission.ACCOUNT_READ
        request.requestURI.startsWith("/api/admin/v1/outbox") && isMutation(request.method) -> AdminPermission.OUTBOX_RETRY
        request.requestURI.startsWith("/api/admin/v1/outbox") -> AdminPermission.OUTBOX_READ
        request.requestURI.startsWith("/api/admin/v1/metrics/economy/replay") && isMutation(request.method) -> AdminPermission.OUTBOX_RETRY
        request.requestURI.startsWith("/api/admin/v1/dashboard") -> AdminPermission.DASHBOARD_READ
        else -> null
    }
    private fun isPublicAuthenticationRoute(request: HttpServletRequest): Boolean =
        request.requestURI == "/api/admin/v1/auth/gitlab/begin" || request.requestURI == "/api/admin/v1/auth/gitlab/callback"

    private fun auditAttempt(
        request: HttpServletRequest,
        requestId: UUID,
        operatorId: UUID?,
        action: String,
        outcome: String,
        username: String = "anonymous",
    ) {
        audit.record(clock.instant(), operatorId, username, action, "HTTP", request.requestURI, outcome, requestId, remoteAddress(request))
    }

    private fun originAllowed(request: HttpServletRequest): Boolean {
        val rawOrigin = request.getHeader("Origin") ?: return false
        val origin = normalizedOrigin(rawOrigin) ?: return false
        if (origin in allowedOrigins.mapNotNull(::normalizedOrigin)) return true
        return origin == requestOrigin(request)
    }

    private fun requestOrigin(request: HttpServletRequest): String {
        val proto = request.getHeader("X-Forwarded-Proto")?.substringBefore(',')?.trim()?.takeIf { it in setOf("http", "https") } ?: request.scheme
        val host = request.getHeader("X-Forwarded-Host")?.substringBefore(',')?.trim()?.takeIf { it.isNotBlank() } ?: request.serverName
        return "$proto://${host.substringBefore(':')}"
    }

    private fun normalizedOrigin(value: String): String? = runCatching {
        val uri = URI(value)
        if (uri.scheme !in setOf("http", "https") || uri.host == null || uri.rawUserInfo != null || uri.rawPath !in setOf("", null) || uri.rawQuery != null || uri.rawFragment != null) return null
        val defaultPort = uri.scheme == "http" && uri.port in setOf(-1, 80) || uri.scheme == "https" && uri.port in setOf(-1, 443)
        "${uri.scheme.lowercase()}://${uri.host.lowercase()}${if (defaultPort) "" else ":${uri.port}"}"
    }.getOrNull()

    private fun writeError(response: HttpServletResponse, status: Int, code: String, requestId: UUID) {
        response.status = status
        response.contentType = MediaType.APPLICATION_JSON_VALUE
        mapper.writeValue(response.outputStream, ErrorEnvelope(requestId, clock.instant(), code, "error.${code.lowercase().replace('_', '.')}", false))
    }

    private fun remoteAddress(request: HttpServletRequest): String = request.remoteAddr.orEmpty().ifBlank { "unknown" }
    private fun isMutation(method: String): Boolean = method in setOf("POST", "PUT", "PATCH", "DELETE")

    companion object {
        const val ADMIN_COOKIE = "HANJJAK_ADMIN_SESSION"
        const val PRINCIPAL_ATTRIBUTE = "hanjjak.admin.principal"
        const val REQUEST_ID_ATTRIBUTE = "hanjjak.admin.requestId"
    }
}
