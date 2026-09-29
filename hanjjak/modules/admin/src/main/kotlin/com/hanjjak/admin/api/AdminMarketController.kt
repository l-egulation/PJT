package com.hanjjak.admin.api

import com.hanjjak.admin.domain.AdminPrincipal
import com.hanjjak.admin.infrastructure.AdminAuditRepository
import com.hanjjak.market.application.AdminMarketService
import com.hanjjak.market.domain.AdminMarketCancelResult
import com.hanjjak.market.domain.AdminMarketOrderPage
import com.hanjjak.market.domain.AdminMarketPurchaseResult
import com.hanjjak.market.domain.AdminMarketSellResult
import jakarta.servlet.http.HttpServletRequest
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/admin/v1/market")
class AdminMarketController(
    private val market: AdminMarketService,
    private val audit: AdminAuditRepository,
    private val clock: Clock,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)
    data class CancelRequest(val reason: String)
    data class PurchaseRequest(val quantity: Long? = null, val reason: String)
    data class SellRequest(val itemId: String, val quantity: Long, val unitPrice: Long, val reason: String)

    @GetMapping("/orders")
    fun orders(
        @RequestParam(required = false) query: String?,
        @RequestParam(required = false) cursor: String?,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<AdminMarketOrderPage>> = audited(request, "READ_MARKET_ORDERS", "MARKET_ORDER", query) {
        market.orders(query, cursor)
    }

    @PostMapping("/orders/{orderId}/cancel")
    fun cancel(
        @PathVariable orderId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestBody body: CancelRequest,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<AdminMarketCancelResult>> = audited(request, "CANCEL_MARKET_ORDER", "MARKET_ORDER", orderId.toString()) {
        market.cancelOrder(orderId, idempotencyKey, body.reason)
    }

    @PostMapping("/orders/{orderId}/purchase")
    fun purchase(
        @PathVariable orderId: UUID,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestBody body: PurchaseRequest,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<AdminMarketPurchaseResult>> = audited(request, "PURCHASE_MARKET_ORDER", "MARKET_ORDER", orderId.toString()) {
        market.purchaseOrder(orderId, idempotencyKey, body.quantity, body.reason)
    }

    @PostMapping("/system-orders")
    fun sell(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestBody body: SellRequest,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<AdminMarketSellResult>> = audited(request, "CREATE_SYSTEM_MARKET_ORDER", "MARKET_ITEM", body.itemId) {
        market.sell(body.itemId, body.quantity, body.unitPrice, idempotencyKey, body.reason)
    }

    private fun <T> audited(
        request: HttpServletRequest,
        action: String,
        targetType: String,
        targetId: String?,
        operation: () -> T,
    ): ResponseEntity<Envelope<T>> {
        val principal = principal(request)
        val requestId = requestId(request)
        return try {
            val result = operation()
            audit.record(AdminAuditRepository.Record(clock.instant(), principal.operatorId, principal.username, action, targetType, targetId, "SUCCEEDED", requestId, remoteAddress(request), mutation = request.method != "GET"))
            success(requestId, result)
        } catch (exception: RuntimeException) {
            audit.record(AdminAuditRepository.Record(clock.instant(), principal.operatorId, principal.username, action, targetType, targetId, "FAILED", requestId, remoteAddress(request), mutation = request.method != "GET"))
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
