package com.hanjjak.cosmetics.api

import com.hanjjak.cosmetics.application.CosmeticAudit
import com.hanjjak.cosmetics.application.CosmeticGachaService
import com.hanjjak.cosmetics.application.CosmeticsCommandService
import jakarta.servlet.http.HttpSession
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/cosmetic-gacha/banners")
class CosmeticGachaController(private val service: CosmeticGachaService, private val commands: CosmeticsCommandService, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)
    data class DrawRequest(val count: Int)

    data class ClaimRequest(val count: Long)
    data class OpenBoxRequest(val cosmeticId: String)
    @GetMapping
    fun banners(session: HttpSession): Envelope<Any> {
        val accountId = accountId(session)
        return Envelope(UUID.randomUUID(), clock.instant(), 0, service.banners(accountId))
    }

    @GetMapping("/{bannerId}")
    fun detail(@PathVariable bannerId: String, session: HttpSession): Envelope<Any> {
        val accountId = accountId(session)
        return Envelope(UUID.randomUUID(), clock.instant(), 0, service.detail(accountId, bannerId))
    }

    @PostMapping("/{bannerId}/draws")
    fun draw(@PathVariable bannerId: String, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: DrawRequest, session: HttpSession): Envelope<Any> {
        val accountId = accountId(session)
        var audit: CosmeticAudit? = null
        val result = commands.executeVersioned(accountId, idempotencyKey, "draw:$bannerId:${request.count}", CosmeticGachaService.DrawResponse::class.java, audit = { audit ?: error("DRAW_AUDIT_NOT_READY") }) {
            val audited = service.drawAudited(accountId, bannerId, request.count, idempotencyKey)
            audit = audited.audit
            audited.response
        }
        return Envelope(UUID.randomUUID(), clock.instant(), result.stateVersion, result.data)
    }


    @PostMapping("/{bannerId}/milestone-claims")
    fun claim(@PathVariable bannerId: String, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: ClaimRequest, session: HttpSession): Envelope<Any> {
        val accountId = accountId(session)
        val result = commands.executeVersioned(accountId, key, "claim:$bannerId:${request.count}", MilestoneResult::class.java) { MilestoneResult(service.claimMilestone(accountId, bannerId, request.count)) }
        return Envelope(UUID.randomUUID(), clock.instant(), result.stateVersion, result.data)
    }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
}
