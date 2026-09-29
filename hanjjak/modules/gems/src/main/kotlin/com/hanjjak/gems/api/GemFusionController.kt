package com.hanjjak.gems.api

import com.hanjjak.gems.application.GemService
import com.hanjjak.gems.domain.GemFusionExecuteRequest
import com.hanjjak.gems.domain.GemFusionPreview
import com.hanjjak.gems.domain.GemFusionPreviewRequest
import com.hanjjak.gems.domain.GemFusionResult
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/gem-fusions")
class GemFusionController(private val gems: GemService, private val jdbc: JdbcClient, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)

    @PostMapping("/preview")
    fun preview(@RequestBody request: GemFusionPreviewRequest, session: HttpSession): Envelope<GemFusionPreview> {
        val accountId = accountId(session)
        return envelope(accountId, gems.previewFusion(accountId, request))
    }

    @PostMapping
    fun fuse(@RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: GemFusionExecuteRequest, session: HttpSession): Envelope<GemFusionResult> {
        val accountId = accountId(session)
        return envelope(accountId, gems.fuse(accountId, idempotencyKey, request))
    }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
    private fun <T> envelope(accountId: UUID, data: T): Envelope<T> = Envelope(UUID.randomUUID(), clock.instant(), stateVersion(accountId), data)
    private fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
}
