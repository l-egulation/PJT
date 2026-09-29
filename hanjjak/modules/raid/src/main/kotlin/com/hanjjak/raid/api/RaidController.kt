package com.hanjjak.raid.api

import com.hanjjak.raid.application.RaidService
import com.hanjjak.raid.domain.RaidAttemptMode
import jakarta.servlet.http.HttpSession
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/raid")
class RaidController(
    private val raids: RaidService,
    private val clock: Clock,
    private val queries: com.hanjjak.raid.application.RaidQueryPort = com.hanjjak.raid.application.RaidQueryPort.Empty,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)
    data class Command<T>(val commandId: UUID, val idempotencyKey: UUID, val status: String, val result: T)
    data class StartRequest(val mode: RaidAttemptMode)
    data class EmptyRequest(val ignored: Map<String, Any?> = emptyMap())

    private fun account(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")

    private fun <T> ok(accountId: UUID, data: T): ResponseEntity<Envelope<T>> = ResponseEntity.ok()
        .cacheControl(CacheControl.noStore())
        .body(Envelope(UUID.randomUUID(), clock.instant(), queries.stateVersion(accountId), data))

    @GetMapping
    fun state(session: HttpSession): ResponseEntity<Envelope<com.hanjjak.raid.application.RaidStateView>> {
        val accountId = account(session)
        return ok(accountId, raids.state(accountId))
    }

    @GetMapping("/attempts/current")
    fun currentAttempt(session: HttpSession): ResponseEntity<Envelope<com.hanjjak.raid.application.RaidAttemptView?>> {
        val accountId = account(session)
        return ok(accountId, raids.currentAttempt(accountId))
    }

    @GetMapping("/claims")
    fun claims(@RequestParam(required = false) cursor: String?, session: HttpSession): ResponseEntity<Envelope<com.hanjjak.raid.application.RaidClaimsView>> {
        val accountId = account(session)
        return ok(accountId, raids.claims(accountId, cursor))
    }

    @GetMapping("/ranking")
    fun ranking(
        @RequestParam(required = false) cursor: String?,
        @RequestParam(required = false, defaultValue = "100") limit: Int,
        @RequestParam(required = false) sessionId: UUID?,
        session: HttpSession,
    ): ResponseEntity<Envelope<com.hanjjak.raid.application.RaidRankingApiView>> {
        val accountId = account(session)
        return ok(accountId, raids.ranking(accountId, cursor, limit, sessionId))
    }

    @PostMapping("/attempts")
    fun start(@RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: StartRequest, session: HttpSession): ResponseEntity<Envelope<Command<com.hanjjak.raid.application.RaidAttemptView>>> {
        val accountId = account(session)
        return ok(accountId, Command(key, key, "SUCCEEDED", raids.startAttempt(accountId, key, request.mode)))
    }

    @PostMapping("/attempts/{attemptId}/retry")
    fun retry(@PathVariable attemptId: UUID, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: EmptyRequest, session: HttpSession): ResponseEntity<Envelope<Command<com.hanjjak.raid.application.RaidAttemptView>>> {
        val accountId = account(session)
        return ok(accountId, Command(key, key, "SUCCEEDED", raids.retryAttempt(accountId, key, attemptId)))
    }

    @PostMapping("/attempts/{attemptId}/confirm")
    fun confirm(@PathVariable attemptId: UUID, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: EmptyRequest, session: HttpSession): ResponseEntity<Envelope<Command<com.hanjjak.raid.application.RaidStateView>>> {
        val accountId = account(session)
        return ok(accountId, Command(key, key, "SUCCEEDED", raids.confirmAttempt(accountId, key, attemptId).state))
    }

    @PostMapping("/attempts/{attemptId}/discard")
    fun discard(@PathVariable attemptId: UUID, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: EmptyRequest, session: HttpSession): ResponseEntity<Envelope<Command<com.hanjjak.raid.application.RaidStateView>>> {
        val accountId = account(session)
        return ok(accountId, Command(key, key, "SUCCEEDED", raids.discardAttempt(accountId, key, attemptId)))
    }

    @PostMapping("/claims/{claimId}")
    fun claim(@PathVariable claimId: UUID, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: EmptyRequest, session: HttpSession): ResponseEntity<Envelope<Command<com.hanjjak.raid.application.RaidClaimResult>>> {
        val accountId = account(session)
        return ok(accountId, Command(key, key, "SUCCEEDED", raids.claimReward(accountId, key, claimId)))
    }
}
