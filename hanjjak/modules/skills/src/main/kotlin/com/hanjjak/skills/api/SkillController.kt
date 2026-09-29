package com.hanjjak.skills.api

import com.hanjjak.skills.application.SkillService
import com.hanjjak.skills.domain.SkillEnhanceResult
import com.hanjjak.skills.domain.SkillLoadoutUpdateRequest
import com.hanjjak.skills.domain.SkillScreenState
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/skills")
class SkillController(private val skills: SkillService, private val jdbc: JdbcClient, private val clock: Clock) {
    data class EmptyRequest(val ignored: String? = null)
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)

    @GetMapping fun state(session: HttpSession): Envelope<SkillScreenState> {
        val accountId = accountId(session)
        return envelope(accountId, skills.state(accountId))
    }

    @PostMapping("/{skillId}/enhance")
    fun enhance(@PathVariable skillId: String, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: EmptyRequest = EmptyRequest(), session: HttpSession): Envelope<SkillEnhanceResult> {
        val accountId = accountId(session)
        return envelope(accountId, skills.enhance(accountId, idempotencyKey, skillId))
    }

    @PostMapping("/{skillId}/promote")
    fun promote(@PathVariable skillId: String, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: EmptyRequest = EmptyRequest(), session: HttpSession): Envelope<SkillEnhanceResult> {
        val accountId = accountId(session)
        return envelope(accountId, skills.promote(accountId, idempotencyKey, skillId))
    }

    @PutMapping("/loadout")
    fun loadout(@RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: SkillLoadoutUpdateRequest, session: HttpSession): Envelope<SkillScreenState> {
        val accountId = accountId(session)
        return envelope(accountId, skills.updateLoadout(accountId, idempotencyKey, request))
    }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
    private fun <T> envelope(accountId: UUID, data: T): Envelope<T> = Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), data)
    private fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
}
