package com.hanjjak.account.api

import com.hanjjak.account.application.MaterialPreferenceService
import com.hanjjak.account.domain.MaterialPreferenceSelection
import com.hanjjak.account.domain.MaterialPreferenceState
import com.hanjjak.account.domain.MaterialType
import jakarta.servlet.http.HttpServletRequest
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/material-preference")
class MaterialPreferenceController(
    private val preferences: MaterialPreferenceService,
    private val clock: Clock,
) {
    data class SelectRequest(val materialType: MaterialType)
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping("/population")
    fun population(): Envelope<Map<MaterialType, Long>> = Envelope(UUID.randomUUID(), clock.instant(), 0, preferences.populationCounts())

    @GetMapping
    fun get(request: HttpServletRequest): Envelope<MaterialPreferenceState> {
        val snapshot = preferences.get(accountId(request))
        return Envelope(UUID.randomUUID(), clock.instant(), snapshot.stateVersion, snapshot.data)
    }

    @PostMapping
    fun select(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestBody request: SelectRequest,
        servletRequest: HttpServletRequest,
    ): Envelope<MaterialPreferenceSelection> {
        val snapshot = preferences.select(accountId(servletRequest), idempotencyKey, request.materialType)
        return Envelope(UUID.randomUUID(), clock.instant(), snapshot.stateVersion, snapshot.data)
    }

    private fun accountId(request: HttpServletRequest): UUID = request.getSession(false)?.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
}
