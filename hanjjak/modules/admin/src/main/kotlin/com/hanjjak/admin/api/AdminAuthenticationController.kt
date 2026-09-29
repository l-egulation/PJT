package com.hanjjak.admin.api

import com.hanjjak.admin.application.AdminAuthenticationService
import com.hanjjak.admin.domain.AdminPrincipal
import com.hanjjak.admin.infrastructure.AdminAuditRepository
import jakarta.servlet.http.Cookie
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.http.ResponseCookie
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/admin/v1/auth")
class AdminAuthenticationController(
    private val authentication: AdminAuthenticationService,
    private val rateLimiter: AdminLoginRateLimiter,
    private val audit: AdminAuditRepository,
    private val properties: AdminProperties,
    private val clock: Clock,
) {
    data class Identity(val operatorId: UUID, val username: String, val displayName: String, val roles: List<String>, val permissions: List<String>)
    data class SessionState(val authenticated: Boolean, val operator: Identity?)
    data class Authorization(val authorizationUrl: String)
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @PostMapping("/gitlab/begin")
    fun beginGitlab(request: HttpServletRequest): Envelope<Authorization> {
        rateLimiter.check(remoteAddress(request))
        val requestId = requestId(request)
        val authorization = authentication.beginGitlabLogin()
        audit.record(clock.instant(), null, "anonymous", "LOGIN_BEGIN", "ADMIN_SESSION", null, "SUCCEEDED", requestId, remoteAddress(request))
        return envelope(requestId, Authorization(authorization.url))
    }

    @GetMapping("/gitlab/callback")
    fun gitlabCallback(
        @RequestParam(required = false) code: String?,
        @RequestParam(required = false) state: String?,
        @RequestParam(required = false) error: String?,
        request: HttpServletRequest,
        response: HttpServletResponse,
    ) {
        val requestId = requestId(request)
        val destination = try {
            require(error == null && !code.isNullOrBlank() && !state.isNullOrBlank()) { "ADMIN_GITLAB_AUTHORIZATION_DENIED" }
            val result = authentication.completeGitlabLogin(state, code)
            audit.record(clock.instant(), result.principal.operatorId, result.principal.username, "LOGIN", "ADMIN_SESSION", null, "SUCCEEDED", requestId, remoteAddress(request))
            response.addHeader("Set-Cookie", sessionCookie(result.token).toString())
            authentication.successUrl()
        } catch (failure: IllegalArgumentException) {
            val codeValue = failure.message?.takeIf { it.startsWith("ADMIN_") } ?: "ADMIN_GITLAB_LOGIN_FAILED"
            audit.record(clock.instant(), null, "anonymous", "LOGIN", "ADMIN_SESSION", null, "FAILED", requestId, remoteAddress(request))
            authentication.failureUrl(codeValue)
        }
        response.status = HttpServletResponse.SC_FOUND
        response.setHeader("Location", destination)
    }

    @GetMapping("/session")
    fun session(request: HttpServletRequest): Envelope<SessionState> {
        val principal = principal(request)
        audit.record(clock.instant(), principal.operatorId, principal.username, "READ_SESSION", "ADMIN_SESSION", null, "SUCCEEDED", requestId(request), remoteAddress(request))
        return envelope(requestId(request), SessionState(true, principal.toIdentity()))
    }

    @PostMapping("/logout")
    fun logout(request: HttpServletRequest, response: HttpServletResponse): Envelope<SessionState> {
        val principal = principal(request)
        authentication.logout(cookie(request)?.value)
        response.addHeader("Set-Cookie", expiredSessionCookie().toString())
        audit.record(clock.instant(), principal.operatorId, principal.username, "LOGOUT", "ADMIN_SESSION", null, "SUCCEEDED", requestId(request), remoteAddress(request))
        return envelope(requestId(request), SessionState(false, null))
    }

    private fun sessionCookie(token: String): ResponseCookie = ResponseCookie.from(AdminAccessFilter.ADMIN_COOKIE, token)
        .httpOnly(true)
        .secure(properties.session.secureCookie)
        .sameSite("Lax")
        .path("/api/admin")
        .maxAge(properties.session.absoluteLifetime)
        .build()

    private fun expiredSessionCookie(): ResponseCookie = ResponseCookie.from(AdminAccessFilter.ADMIN_COOKIE, "")
        .httpOnly(true)
        .secure(properties.session.secureCookie)
        .sameSite("Lax")
        .path("/api/admin")
        .maxAge(0)
        .build()

    private fun principal(request: HttpServletRequest): AdminPrincipal = request.getAttribute(AdminAccessFilter.PRINCIPAL_ATTRIBUTE) as? AdminPrincipal
        ?: throw IllegalArgumentException("ADMIN_AUTHENTICATION_REQUIRED")

    private fun cookie(request: HttpServletRequest): Cookie? = request.cookies?.firstOrNull { it.name == AdminAccessFilter.ADMIN_COOKIE }
    private fun requestId(request: HttpServletRequest): UUID = request.getAttribute(AdminAccessFilter.REQUEST_ID_ATTRIBUTE) as? UUID ?: UUID.randomUUID()
    private fun remoteAddress(request: HttpServletRequest): String = request.remoteAddr.orEmpty().ifBlank { "unknown" }
    private fun <T> envelope(requestId: UUID, data: T) = Envelope(requestId, clock.instant(), 0, data)
    private fun AdminPrincipal.toIdentity() = Identity(operatorId, username, displayName, roles.map { it.name }.sorted(), permissions.map { it.wireName }.sorted())
}
