package com.hanjjak.equipment.api

import com.hanjjak.equipment.application.EquipmentService
import com.hanjjak.equipment.domain.EquipmentCommandResult
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.equipment.domain.EquipmentState
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/equipment")
class EquipmentController(
    private val equipment: EquipmentService,
    private val jdbc: JdbcClient,
    private val clock: Clock,
) {
    data class EmptyRequest(val ignored: String? = null)
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)

    @GetMapping
    fun state(session: HttpSession): Envelope<EquipmentState> {
        val accountId = accountId(session)
        return envelope(accountId, equipment.state(accountId))
    }

    @PostMapping("/{slot}/unlock")
    fun unlock(@PathVariable slot: EquipmentSlot, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: EmptyRequest = EmptyRequest(), session: HttpSession): Envelope<EquipmentCommandResult> {
        val accountId = accountId(session)
        return envelope(accountId, equipment.unlock(accountId, idempotencyKey, slot))
    }

    @PostMapping("/{slot}/enhance")
    fun enhance(@PathVariable slot: EquipmentSlot, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: EmptyRequest = EmptyRequest(), session: HttpSession): Envelope<EquipmentCommandResult> {
        val accountId = accountId(session)
        return envelope(accountId, equipment.enhance(accountId, idempotencyKey, slot))
    }

    @PostMapping("/{slot}/promote")
    fun promote(@PathVariable slot: EquipmentSlot, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: EmptyRequest = EmptyRequest(), session: HttpSession): Envelope<EquipmentCommandResult> {
        val accountId = accountId(session)
        return envelope(accountId, equipment.promote(accountId, idempotencyKey, slot))
    }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
    private fun <T> envelope(accountId: UUID, data: T): Envelope<T> = Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), data)
    private fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
}
