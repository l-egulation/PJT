package com.hanjjak.cosmetics.api

import com.hanjjak.cosmetics.application.CosmeticsService
import com.hanjjak.cosmetics.domain.CosmeticSlot
import com.hanjjak.cosmetics.domain.RegistrationMode
import com.hanjjak.cosmetics.domain.VersionedResult
import jakarta.servlet.http.HttpSession
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/cosmetics")
class CosmeticsController(private val service: CosmeticsService, private val commands: com.hanjjak.cosmetics.application.CosmeticsCommandService, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)
    data class RegistrationRequest(val mode: RegistrationMode)
    data class EquipmentRequest(val cosmeticId: String?)

    @GetMapping("/collection")
    fun collection(session: HttpSession): Envelope<Any> = envelope(service.versionedCollection(accountId(session)))

    @GetMapping("/catalog")
    fun catalog(session: HttpSession): Envelope<Any> = envelope(service.catalog(accountId(session)))

    @PostMapping("/{cosmeticId}/registrations")
    fun register(@PathVariable cosmeticId: String, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: RegistrationRequest, session: HttpSession): Envelope<Any> =
        envelope(commands.executeVersioned(accountId(session), idempotencyKey, "register:$cosmeticId:${request.mode}", com.hanjjak.cosmetics.domain.CollectionSnapshot::class.java) { service.register(accountId(session), cosmeticId, request.mode) })

    @PatchMapping("/equipment/{slotId}")
    fun equip(@PathVariable slotId: CosmeticSlot, @RequestHeader("Idempotency-Key") idempotencyKey: UUID, @RequestBody request: EquipmentRequest, session: HttpSession): Envelope<Any> =
        envelope(commands.executeVersioned(accountId(session), idempotencyKey, "equip:$slotId:${request.cosmeticId}", com.hanjjak.cosmetics.domain.CollectionSnapshot::class.java) { service.equip(accountId(session), slotId, request.cosmeticId) })

    private fun <T : Any> envelope(result: VersionedResult<T>): Envelope<Any> = Envelope(UUID.randomUUID(), clock.instant(), result.stateVersion, result.data)

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
}
