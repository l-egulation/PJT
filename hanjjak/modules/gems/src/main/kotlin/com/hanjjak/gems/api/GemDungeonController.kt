package com.hanjjak.gems.api

import com.hanjjak.gems.application.GemDungeonService
import com.hanjjak.gems.domain.GemDungeonSweepRequest
import com.hanjjak.gems.domain.GemDungeonStartRequest
import com.hanjjak.gems.domain.GemPreset
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/gem-dungeons")
class GemDungeonController(private val dungeons: GemDungeonService, private val jdbc: JdbcClient, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping("/today")
    fun today(@RequestParam(required = false) boss: GemPreset?, session: HttpSession) = accountId(session).let { envelope(it, dungeons.today(it, boss)) }

    @PostMapping("/challenges")
    fun start(@RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: GemDungeonStartRequest = GemDungeonStartRequest(), session: HttpSession) =
        accountId(session).let { envelope(it, dungeons.start(it, key, request.boss)) }

    @PostMapping("/challenges/{challengeId}/complete")
    fun complete(@PathVariable challengeId: UUID, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: Map<String, String> = emptyMap(), session: HttpSession) =
        accountId(session).let { envelope(it, dungeons.complete(it, challengeId, key)) }

    @PostMapping("/challenges/{challengeId}/abort")
    fun abort(@PathVariable challengeId: UUID, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: Map<String, String> = emptyMap(), session: HttpSession) =
        accountId(session).let { envelope(it, dungeons.abort(it, challengeId, key)) }

    @PostMapping("/sweeps")
    fun sweep(@RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: GemDungeonSweepRequest, session: HttpSession) =
        accountId(session).let { envelope(it, dungeons.sweep(it, key, request)) }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
    private fun <T> envelope(accountId: UUID, data: T) = Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), data)
    private fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
}
