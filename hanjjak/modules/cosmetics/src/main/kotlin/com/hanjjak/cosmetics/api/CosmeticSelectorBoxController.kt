package com.hanjjak.cosmetics.api

import com.hanjjak.cosmetics.application.CosmeticGachaService
import jakarta.servlet.http.HttpSession
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/cosmetic-selector-boxes")
class CosmeticSelectorBoxController(private val service: CosmeticGachaService, private val commands: com.hanjjak.cosmetics.application.CosmeticsCommandService, private val clock: Clock) {
    data class OpenRequest(val cosmeticId: String)
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)

    @PostMapping("/{boxItemId}/open")
    fun open(@PathVariable boxItemId: String, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: OpenRequest, session: HttpSession): Envelope<Any> {
        val accountId = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val result = commands.executeVersioned(accountId, key, "open:$boxItemId:${request.cosmeticId}", com.hanjjak.cosmetics.domain.CollectionSnapshot::class.java) { service.openBox(accountId, boxItemId, request.cosmeticId) }
        return Envelope(UUID.randomUUID(), clock.instant(), result.stateVersion, result.data)
    }
}
