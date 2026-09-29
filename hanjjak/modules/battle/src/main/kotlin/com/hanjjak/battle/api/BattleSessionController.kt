package com.hanjjak.battle.api

import com.hanjjak.battle.application.BattleSessionService
import jakarta.servlet.http.HttpSession
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/battles/sessions")
class BattleSessionController(
    private val battleSessions: BattleSessionService,
    private val clock: Clock,
) {
    data class StartRequest(val stageId: String)
    data class CompleteRequest(
        val predictedHash: String? = null,
        val renderingCheckpoint: BattleSessionService.RenderingCheckpoint? = null,
    )
    data class SettlementRequest(
        val throughEnemyIndex: Int,
        val renderingCheckpoint: BattleSessionService.RenderingCheckpoint,
    )
    data class AbortRequest(val renderingCheckpoint: BattleSessionService.RenderingCheckpoint? = null)
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @PostMapping
    fun start(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestHeader("X-Game-Session-Id") gameSessionId: UUID,
        @RequestBody request: StartRequest,
        session: HttpSession,
    ): Envelope<BattleSessionService.Started> {
        val accountId = accountId(session)
        return envelope(accountId, battleSessions.start(accountId, gameSessionId, idempotencyKey, request.stageId))
    }

    @PostMapping("/{battleSessionId}/heartbeat")
    fun heartbeat(
        @PathVariable battleSessionId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestHeader("X-Game-Session-Id") gameSessionId: UUID,
        @RequestHeader("X-Battle-Token") battleToken: UUID,
        session: HttpSession,
    ): Envelope<BattleSessionService.State> {
        val accountId = accountId(session)
        return envelope(accountId, battleSessions.heartbeat(accountId, gameSessionId, battleSessionId, battleToken, idempotencyKey))
    }

    @PostMapping("/{battleSessionId}/settlements")
    fun settle(
        @PathVariable battleSessionId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestHeader("X-Game-Session-Id") gameSessionId: UUID,
        @RequestHeader("X-Battle-Token") battleToken: UUID,
        @RequestBody request: SettlementRequest,
        session: HttpSession,
    ): Envelope<BattleSessionService.SettlementResult> {
        val accountId = accountId(session)
        validateCheckpoint(request.renderingCheckpoint)
        require(request.throughEnemyIndex in 1..20) { "INVALID_ENEMY_INDEX" }
        return envelope(accountId, battleSessions.settle(
            accountId,
            gameSessionId,
            battleSessionId,
            battleToken,
            idempotencyKey,
            request.throughEnemyIndex,
            request.renderingCheckpoint,
        ))
    }

    @PostMapping("/{battleSessionId}/complete")
    fun complete(
        @PathVariable battleSessionId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestHeader("X-Game-Session-Id") gameSessionId: UUID,
        @RequestHeader("X-Battle-Token") battleToken: UUID,
        @RequestBody request: CompleteRequest,
        session: HttpSession,
    ): Envelope<BattleSessionService.Completion> {
        val accountId = accountId(session)
        require(request.predictedHash == null || request.predictedHash.matches(Regex("[0-9a-fA-F]{64}"))) { "INVALID_PREDICTION_HASH" }
        request.renderingCheckpoint?.let(::validateCheckpoint)
        val result = battleSessions.complete(
            accountId,
            gameSessionId,
            battleSessionId,
            battleToken,
            idempotencyKey,
            request.predictedHash,
            request.renderingCheckpoint,
        )
        return envelope(accountId, result)
    }

    @PostMapping("/{battleSessionId}/abort")
    fun abort(
        @PathVariable battleSessionId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestHeader("X-Game-Session-Id") gameSessionId: UUID,
        @RequestHeader("X-Battle-Token") battleToken: UUID,
        @RequestBody(required = false) request: AbortRequest? = null,
        session: HttpSession,
    ): Envelope<BattleSessionService.State> {
        val accountId = accountId(session)
        return envelope(accountId, battleSessions.abort(accountId, gameSessionId, battleSessionId, battleToken, idempotencyKey, request?.renderingCheckpoint))
    }

    private fun validateCheckpoint(checkpoint: BattleSessionService.RenderingCheckpoint) {
        require(checkpoint.logicalTick >= 0 && checkpoint.defeatedNormals in 0..20 && checkpoint.playerHp >= 0) { "INVALID_RENDERING_CHECKPOINT" }
    }

    private fun <T> envelope(accountId: UUID, data: T): Envelope<T> = Envelope(
        UUID.randomUUID(),
        clock.instant(),
        battleSessions.stateVersion(accountId),
        data,
    )

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
}
