package com.hanjjak.gems.api

import com.hanjjak.gems.application.GemService
import com.hanjjak.gems.domain.*
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/gems")
class GemController(private val gems: GemService, private val jdbc: JdbcClient, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)

    @GetMapping fun state(session: HttpSession): Envelope<GemState> {
        val accountId = accountId(session)
        return envelope(accountId, gems.state(accountId))
    }

    data class SlotLockRequest(val locked: Boolean)

    @PutMapping("/slots/{slotId}/lock")
    fun lockSlot(@PathVariable slotId: UUID, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: SlotLockRequest, session: HttpSession): Envelope<GemState> {
        val account = accountId(session)
        return envelope(account, gems.lockSlot(account, key, slotId, request.locked))
    }

    @PostMapping("/boxes/open")
    fun openBoxes(@RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: GemOpenRequest, session: HttpSession): Envelope<GemOpenResult> {
        val accountId = accountId(session)
        return envelope(accountId, gems.openBoxes(accountId, idempotencyKey, request.quantity))
    }

    @PutMapping("/presets/{preset}")
    fun updatePreset(@PathVariable preset: GemPreset, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: GemPresetRequest, session: HttpSession): Envelope<GemPresetUpdateResult> {
        val accountId = accountId(session)
        return envelope(accountId, gems.updatePreset(accountId, idempotencyKey, preset, request))
    }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
    private fun <T> envelope(accountId: UUID, data: T): Envelope<T> = Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), data)
    private fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
}
