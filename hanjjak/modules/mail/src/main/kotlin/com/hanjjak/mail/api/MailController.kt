package com.hanjjak.mail.api

import com.hanjjak.mail.application.MailService
import com.hanjjak.mail.domain.MailClaimAllResult
import com.hanjjak.mail.domain.MailClaimResult
import com.hanjjak.mail.domain.MailCommandResult
import com.hanjjak.mail.domain.MailPage
import jakarta.servlet.http.HttpSession
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/mails")
class MailController(private val service: MailService, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping
    fun list(
        @RequestParam(required = false) claimable: Boolean?,
        @RequestParam(required = false) cursor: String?,
        session: HttpSession,
    ): ResponseEntity<Envelope<MailPage>> {
        val accountId = accountId(session)
        val parsedCursor = cursor?.let { runCatching { Instant.parse(it) }.getOrElse { throw IllegalArgumentException("INVALID_CURSOR") } }
        return success(accountId, service.list(accountId, claimable, parsedCursor))
    }

    @PostMapping("/{mailId}/claim")
    fun claim(
        @PathVariable mailId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        session: HttpSession,
    ): ResponseEntity<Envelope<MailCommandResult<MailClaimResult>>> {
        val accountId = accountId(session)
        return success(accountId, service.claim(accountId, idempotencyKey, mailId))
    }

    @PostMapping("/claim-all")
    fun claimAll(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        session: HttpSession,
    ): ResponseEntity<Envelope<MailCommandResult<MailClaimAllResult>>> {
        val accountId = accountId(session)
        return success(accountId, service.claimAll(accountId, idempotencyKey))
    }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")

    private fun <T> success(accountId: UUID, data: T): ResponseEntity<Envelope<T>> = ResponseEntity.ok()
        .cacheControl(CacheControl.noStore())
        .body(Envelope(UUID.randomUUID(), clock.instant(), service.stateVersion(accountId), data))
}
