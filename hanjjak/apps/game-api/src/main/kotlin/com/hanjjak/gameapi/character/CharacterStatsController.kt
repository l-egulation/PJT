package com.hanjjak.gameapi.character

import jakarta.servlet.http.HttpSession
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
class CharacterStatsController(private val service: CharacterStatsService, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)
    @GetMapping("/api/v1/character/stats")
    fun stats(session: HttpSession): ResponseEntity<Envelope<CharacterStatsView>> {
        val accountId = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val snapshot = service.snapshot(accountId)
        return ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(Envelope(UUID.randomUUID(), clock.instant(), snapshot.stateVersion, snapshot.view))
    }
}
