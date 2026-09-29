package com.hanjjak.admin.api

import org.springframework.http.HttpStatus
import org.springframework.http.ResponseEntity
import jakarta.servlet.http.HttpServletRequest
import org.springframework.core.Ordered
import org.springframework.core.annotation.Order
import org.springframework.web.bind.annotation.ExceptionHandler
import org.springframework.web.bind.annotation.RestControllerAdvice
import java.time.Clock
import java.time.Instant
import java.util.UUID

@Order(Ordered.HIGHEST_PRECEDENCE)
@RestControllerAdvice(basePackages = ["com.hanjjak.admin"])
class AdminExceptionHandler(private val clock: Clock) {
    data class ErrorEnvelope(
        val requestId: UUID,
        val serverTime: Instant,
        val code: String,
        val messageKey: String,
        val retryable: Boolean,
        val details: List<Any>? = null,
    )

    @ExceptionHandler(IllegalArgumentException::class)
    fun invalid(exception: IllegalArgumentException, request: HttpServletRequest): ResponseEntity<ErrorEnvelope> {
        val code = exception.message?.takeIf { it.matches(Regex("[A-Z][A-Z0-9_]*")) } ?: "ADMIN_VALIDATION_FAILED"
        val status = when (code) {
            "ADMIN_AUTHENTICATION_REQUIRED" -> HttpStatus.UNAUTHORIZED
            "ADMIN_GITLAB_ACCESS_DENIED", "ADMIN_GITLAB_AUTHORIZATION_DENIED" -> HttpStatus.FORBIDDEN
            "ADMIN_PERMISSION_DENIED" -> HttpStatus.FORBIDDEN
            else -> HttpStatus.BAD_REQUEST
        }
        return response(status, code, false, requestId(request))
    }

    @ExceptionHandler(AdminLoginRateLimiter.LimitExceeded::class)
    fun limited(exception: AdminLoginRateLimiter.LimitExceeded, request: HttpServletRequest): ResponseEntity<ErrorEnvelope> = ResponseEntity.status(HttpStatus.TOO_MANY_REQUESTS)
        .header("Retry-After", exception.retryAfterSeconds.toString())
        .body(envelope("ADMIN_RATE_LIMITED", true, requestId(request)))

    private fun response(status: HttpStatus, code: String, retryable: Boolean, requestId: UUID) = ResponseEntity.status(status).body(envelope(code, retryable, requestId))
    private fun envelope(code: String, retryable: Boolean, requestId: UUID) = ErrorEnvelope(
        requestId,
        clock.instant(),
        code,
        "error.${code.lowercase().replace('_', '.')}",
        retryable,
    )

    private fun requestId(request: HttpServletRequest): UUID = request.getAttribute(AdminAccessFilter.REQUEST_ID_ATTRIBUTE) as? UUID ?: UUID.randomUUID()
}
