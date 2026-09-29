package com.hanjjak.battle.api

import com.hanjjak.battle.application.OfflineRewardService
import jakarta.servlet.http.HttpSession
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/offline-rewards")
class OfflineRewardController(private val rewards: OfflineRewardService, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping("/pending")
    fun pending(session: HttpSession): Envelope<OfflineRewardService.Pending?> {
        val accountId = accountId(session)
        return Envelope(UUID.randomUUID(), clock.instant(), rewards.stateVersion(accountId), rewards.pending(accountId))
    }

    @PostMapping("/claim")
    fun claim(@RequestHeader("Idempotency-Key") idempotencyKey: UUID, session: HttpSession): Envelope<OfflineRewardService.ClaimResult> {
        val accountId = accountId(session)
        val result = rewards.claim(accountId, idempotencyKey)
        return Envelope(UUID.randomUUID(), clock.instant(), rewards.stateVersion(accountId), result)
    }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
}
