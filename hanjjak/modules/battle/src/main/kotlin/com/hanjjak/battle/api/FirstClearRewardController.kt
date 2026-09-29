package com.hanjjak.battle.api

import com.hanjjak.battle.application.FirstClearRewardPresenter
import com.hanjjak.battle.application.FirstClearRewardService
import com.hanjjak.battle.application.FirstClearRewardView
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/first-clear-rewards")
class FirstClearRewardController(
    private val rewards: FirstClearRewardService,
    private val presenter: FirstClearRewardPresenter,
    private val jdbc: JdbcClient,
    private val clock: Clock,
) {
    data class PendingFirstClearRewardPage(val rewards: List<FirstClearRewardView>, val count: Int)
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping
    fun list(session: HttpSession): Envelope<PendingFirstClearRewardPage> {
        val accountId = accountId(session)
        val pending = presenter.presentPending(rewards.pending(accountId))
        return envelope(accountId, PendingFirstClearRewardPage(pending, pending.size))
    }

    @PostMapping("/{rewardId}/claim")
    fun claim(@PathVariable rewardId: UUID, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, session: HttpSession): Envelope<FirstClearRewardView> {
        val accountId = accountId(session)
        return envelope(accountId, presenter.present(rewards.claim(accountId, rewardId, idempotencyKey)))
    }

    private fun <T> envelope(accountId: UUID, data: T) = Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), data)
    private fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
}
