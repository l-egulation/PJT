package com.hanjjak.battle.api

import com.hanjjak.battle.application.GameSessionService
import jakarta.servlet.http.HttpSession
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/game-sessions")
class GameSessionController(private val sessions: GameSessionService, private val clock: Clock) {
    data class SessionState(
        val gameSessionId: UUID,
        val status: String,
        val heartbeatIntervalSeconds: Int,
        val expiresAfterSeconds: Int,
    )
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @PostMapping
    fun open(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        session: HttpSession,
    ): Envelope<SessionState> {
        val accountId = accountId(session)
        return envelope(accountId, sessions.open(accountId, idempotencyKey))
    }

    @PostMapping("/{gameSessionId}/heartbeat")
    fun heartbeat(
        @PathVariable gameSessionId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        session: HttpSession,
    ): Envelope<SessionState> {
        val accountId = accountId(session)
        return envelope(accountId, sessions.heartbeat(accountId, gameSessionId, idempotencyKey))
    }

    @PostMapping("/{gameSessionId}/close")
    fun close(
        @PathVariable gameSessionId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        session: HttpSession,
    ): Envelope<SessionState> {
        val accountId = accountId(session)
        return envelope(accountId, sessions.close(accountId, gameSessionId, idempotencyKey))
    }

    private fun envelope(accountId: UUID, result: GameSessionService.Session): Envelope<SessionState> = Envelope(
        UUID.randomUUID(),
        clock.instant(),
        sessions.stateVersion(accountId),
        SessionState(result.id, result.status, 30, com.hanjjak.battle.application.OfflineRewardPolicy.BUCKET_SECONDS.toInt()),
    )

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
}
