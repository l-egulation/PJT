package com.hanjjak.gameapi.ranking

import jakarta.servlet.http.HttpSession
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/rankings")
class CombatPowerRankingController(
    private val rankings: CombatPowerRankingService,
    private val clock: Clock,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping("/combat-power")
    fun combatPower(
        @RequestParam(defaultValue = "20") limit: Int,
        session: HttpSession,
    ): ResponseEntity<Envelope<CombatPowerRankingView>> {
        val accountId = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val result = rankings.snapshot(accountId, limit)
        return ResponseEntity.ok()
            .cacheControl(CacheControl.noStore())
            .body(Envelope(UUID.randomUUID(), clock.instant(), result.sourceStateVersion, result))
    }
}
