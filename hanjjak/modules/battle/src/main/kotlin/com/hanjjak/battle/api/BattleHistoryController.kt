package com.hanjjak.battle.api

import com.fasterxml.jackson.annotation.JsonInclude
import com.hanjjak.battle.application.BattleHistoryService
import jakarta.servlet.http.HttpSession
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/battle-history")
class BattleHistoryController(
    private val history: BattleHistoryService,
    private val clock: Clock,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class LatestFailure(val latestFailure: BattleHistoryService.Event?)

    @GetMapping
    fun recent(session: HttpSession): ResponseEntity<Envelope<List<BattleHistoryService.Event>>> {
        val accountId = accountId(session)
        return success(accountId, history.recent(accountId))
    }

    @GetMapping("/stages/{stageId}/latest-failure")
    fun latestFailure(
        @PathVariable stageId: String,
        session: HttpSession,
    ): ResponseEntity<Envelope<LatestFailure>> {
        val accountId = accountId(session)
        return success(accountId, LatestFailure(history.latestStageFailure(accountId, stageId)))
    }

    private fun <T> success(accountId: UUID, data: T): ResponseEntity<Envelope<T>> = ResponseEntity.ok()
        .cacheControl(CacheControl.noStore())
        .body(Envelope(UUID.randomUUID(), clock.instant(), history.stateVersion(accountId), data))

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
}
