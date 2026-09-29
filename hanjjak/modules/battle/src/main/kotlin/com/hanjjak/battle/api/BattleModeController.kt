package com.hanjjak.battle.api

import com.hanjjak.battle.application.BattleModeService
import jakarta.servlet.http.HttpSession
import org.springframework.web.bind.annotation.PatchMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/battle")
class BattleModeController(
    private val modes: BattleModeService,
    private val clock: Clock,
) {
    data class RepeatStageRequest(val stageId: String?)
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @PatchMapping("/repeat-stage")
    fun update(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestHeader("X-Game-Session-Id") gameSessionId: UUID,
        @RequestBody request: RepeatStageRequest,
        session: HttpSession,
    ): Envelope<BattleModeService.UpdateResult> {
        val accountId = session.getAttribute("accountId") as? UUID
            ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val idleMode = if (request.stageId == null) "AUTO_PROGRESS" else "REPEAT_STAGE"
        val result = modes.update(accountId, gameSessionId, idempotencyKey, idleMode, request.stageId)
        return Envelope(UUID.randomUUID(), clock.instant(), result.stateVersion, result)
    }
}
