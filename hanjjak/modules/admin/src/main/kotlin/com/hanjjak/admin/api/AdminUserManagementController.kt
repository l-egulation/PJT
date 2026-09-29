package com.hanjjak.admin.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.admin.application.AdminUserManagementService
import com.hanjjak.admin.domain.AdminPrincipal
import com.hanjjak.admin.infrastructure.AdminAuditRepository
import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.gems.domain.GemOption
import jakarta.servlet.http.HttpServletRequest
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/admin/v1/accounts/{accountId}/management")
class AdminUserManagementController(
    private val users: AdminUserManagementService,
    private val audit: AdminAuditRepository,
    private val mapper: ObjectMapper,
    private val clock: Clock,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)
    data class ProgressionRequest(val level: Int? = null, val experience: Long? = null, val reason: String = "")
    data class RiceRequest(val mode: AdminUserManagementService.NumericMode, val amount: Long, val reason: String = "")
    data class ItemRequest(val itemId: String, val mode: AdminUserManagementService.NumericMode, val quantity: Long, val reason: String = "")
    data class GemRequest(val level: Int, val option: GemOption, val delta: Int, val reason: String = "")
    data class StageRequest(val throughStageId: String, val reason: String = "")
    data class CosmeticRequest(val cosmeticId: String, val registeredQuantity: Int, val unregisteredQuantity: Int, val reason: String = "")
    data class EquipmentRequest(val slot: EquipmentSlot, val grade: EquipmentGrade? = null, val enhancementLevel: Int? = null, val reason: String = "")

    @GetMapping
    fun state(@PathVariable accountId: UUID, request: HttpServletRequest): ResponseEntity<Envelope<AdminUserManagementService.UserState>> =
        success(requestId(request), users.state(accountId))

    @PostMapping("/progression")
    @Transactional
    fun progression(
        @PathVariable accountId: UUID,
        @RequestHeader("Idempotency-Key") key: UUID,
        @RequestBody body: ProgressionRequest,
        request: HttpServletRequest,
    ) = mutate(request, accountId, key, "ADJUST_USER_PROGRESSION", body.reason) { principal ->
        users.adjustProgression(principal.operatorId, key, accountId, body.level, body.experience, body.reason)
    }

    @PostMapping("/rice")
    @Transactional
    fun rice(
        @PathVariable accountId: UUID,
        @RequestHeader("Idempotency-Key") key: UUID,
        @RequestBody body: RiceRequest,
        request: HttpServletRequest,
    ) = mutate(request, accountId, key, "ADJUST_USER_RICE", body.reason) { principal ->
        users.adjustRice(principal.operatorId, key, accountId, body.mode, body.amount, body.reason)
    }

    @PostMapping("/items")
    @Transactional
    fun item(
        @PathVariable accountId: UUID,
        @RequestHeader("Idempotency-Key") key: UUID,
        @RequestBody body: ItemRequest,
        request: HttpServletRequest,
    ) = mutate(request, accountId, key, "ADJUST_USER_ITEM", body.reason) { principal ->
        users.adjustItem(principal.operatorId, key, accountId, body.itemId, body.mode, body.quantity, body.reason)
    }

    @PostMapping("/gems")
    @Transactional
    fun gem(
        @PathVariable accountId: UUID,
        @RequestHeader("Idempotency-Key") key: UUID,
        @RequestBody body: GemRequest,
        request: HttpServletRequest,
    ) = mutate(request, accountId, key, "ADJUST_USER_GEM", body.reason) { principal ->
        users.adjustGem(principal.operatorId, key, accountId, body.level, body.option, body.delta, body.reason)
    }

    @PostMapping("/stages")
    @Transactional
    fun stage(
        @PathVariable accountId: UUID,
        @RequestHeader("Idempotency-Key") key: UUID,
        @RequestBody body: StageRequest,
        request: HttpServletRequest,
    ) = mutate(request, accountId, key, "UNLOCK_USER_STAGES", body.reason) { principal ->
        users.unlockStages(principal.operatorId, key, accountId, body.throughStageId, body.reason)
    }

    @PostMapping("/cosmetics")
    @Transactional
    fun cosmetic(
        @PathVariable accountId: UUID,
        @RequestHeader("Idempotency-Key") key: UUID,
        @RequestBody body: CosmeticRequest,
        request: HttpServletRequest,
    ) = mutate(request, accountId, key, "ADJUST_USER_COSMETIC", body.reason) { principal ->
        users.adjustCosmetic(principal.operatorId, key, accountId, body.cosmeticId, body.registeredQuantity, body.unregisteredQuantity, body.reason)
    }

    @PostMapping("/equipment")
    @Transactional
    fun equipment(
        @PathVariable accountId: UUID,
        @RequestHeader("Idempotency-Key") key: UUID,
        @RequestBody body: EquipmentRequest,
        request: HttpServletRequest,
    ) = mutate(request, accountId, key, "ADJUST_USER_EQUIPMENT", body.reason) { principal ->
        users.adjustEquipment(principal.operatorId, key, accountId, body.slot, body.grade, body.enhancementLevel, body.reason)
    }

    private fun mutate(
        request: HttpServletRequest,
        accountId: UUID,
        key: UUID,
        action: String,
        reason: String,
        operation: (AdminPrincipal) -> AdminUserManagementService.MutationResult,
    ): ResponseEntity<Envelope<AdminUserManagementService.MutationResult>> {
        val principal = principal(request)
        val requestId = requestId(request)
        return try {
            val result = operation(principal)
            audit.record(
                AdminAuditRepository.Record(
                    clock.instant(), principal.operatorId, principal.username, action, "ACCOUNT", accountId.toString(), "SUCCEEDED",
                    requestId, remoteAddress(request), true, reason.trim(), mapper.writeValueAsString(result.before), mapper.writeValueAsString(result.after), key,
                ),
            )
            success(requestId, result)
        } catch (exception: RuntimeException) {
            audit.recordFailure(
                AdminAuditRepository.Record(
                    clock.instant(), principal.operatorId, principal.username, action, "ACCOUNT", accountId.toString(), "FAILED",
                    requestId, remoteAddress(request), true, reason.trim(), idempotencyKey = key,
                ),
            )
            throw exception
        }
    }

    private fun <T> success(requestId: UUID, data: T): ResponseEntity<Envelope<T>> = ResponseEntity.ok()
        .cacheControl(CacheControl.noStore())
        .body(Envelope(requestId, clock.instant(), 0, data))

    private fun principal(request: HttpServletRequest): AdminPrincipal = request.getAttribute(AdminAccessFilter.PRINCIPAL_ATTRIBUTE) as? AdminPrincipal
        ?: throw IllegalArgumentException("ADMIN_AUTHENTICATION_REQUIRED")
    private fun requestId(request: HttpServletRequest): UUID = request.getAttribute(AdminAccessFilter.REQUEST_ID_ATTRIBUTE) as? UUID ?: UUID.randomUUID()
    private fun remoteAddress(request: HttpServletRequest): String = request.remoteAddr.orEmpty().ifBlank { "unknown" }
}
