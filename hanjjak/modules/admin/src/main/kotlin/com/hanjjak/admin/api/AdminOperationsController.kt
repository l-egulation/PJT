package com.hanjjak.admin.api

import com.hanjjak.admin.application.AdminAnomalyService
import com.hanjjak.admin.application.AdminAccountService
import com.hanjjak.admin.application.AdminCatalogService
import com.hanjjak.admin.application.AdminDashboardService
import com.hanjjak.admin.application.AdminEventService
import com.hanjjak.admin.application.AdminMarketQualityService
import com.hanjjak.admin.application.AdminKafkaOpsService
import com.hanjjak.admin.domain.AdminPermission
import com.hanjjak.admin.domain.AdminPrincipal
import com.hanjjak.admin.infrastructure.AdminAuditRepository
import com.hanjjak.events.application.EconomyDailyMetric
import com.hanjjak.events.application.EconomyMetricsService
import jakarta.servlet.http.HttpServletRequest
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.time.LocalDate
import java.util.UUID

@RestController
@RequestMapping("/api/admin/v1")
class AdminOperationsController(
    private val dashboardService: AdminDashboardService,
    private val accounts: AdminAccountService,
    private val catalog: AdminCatalogService,
    private val events: AdminEventService,
    private val metrics: EconomyMetricsService,
    private val audit: AdminAuditRepository,
    private val clock: Clock,
    private val anomalies: AdminAnomalyService,
    private val quality: AdminMarketQualityService,
    private val kafkaOps: AdminKafkaOpsService,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping("/dashboard")
    fun dashboard(request: HttpServletRequest): ResponseEntity<Envelope<AdminDashboardService.Dashboard>> = audited(
        request,
        "READ_DASHBOARD",
        "DASHBOARD",
    ) { dashboardService.dashboard() }

    @GetMapping("/kafka/ops")
    fun kafkaOps(request: HttpServletRequest): ResponseEntity<Envelope<AdminKafkaOpsService.KafkaOps>> = audited(request, "READ_KAFKA_OPS", "KAFKA") { kafkaOps.snapshot() }

    @GetMapping("/metrics/economy/daily")
    fun economyDaily(
        @RequestParam from: LocalDate,
        @RequestParam to: LocalDate,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<List<EconomyDailyMetric>>> = audited(request, "READ_ECONOMY", "ECONOMY_METRIC", "$from/$to") {
        require(!to.isBefore(from) && to.toEpochDay() - from.toEpochDay() <= 90) { "ADMIN_INVALID_DATE_RANGE" }
        metrics.listDaily(from, to)
    }

    @GetMapping("/accounts")
    fun searchAccounts(
        @RequestParam(required = false) query: String?,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<List<AdminAccountService.AccountSummary>>> {
        val principal = principal(request)
        val includeEmail = AdminPermission.ACCOUNT_PII_READ in principal.permissions
        return audited(request, "SEARCH_ACCOUNT", "ACCOUNT", if (includeEmail) "contains-query" else "masked-query") {
            accounts.search(query, includeEmail)
        }
    }

    @GetMapping("/catalog")
    fun catalog(request: HttpServletRequest): ResponseEntity<Envelope<AdminCatalogService.Catalog>> = audited(request, "READ_CATALOG", "CATALOG") { catalog.catalog() }

    @GetMapping("/accounts/{accountId}")
    fun accountDetail(
        @PathVariable accountId: UUID,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<AdminAccountService.AccountDetail>> {
        val principal = principal(request)
        val includeEmail = AdminPermission.ACCOUNT_PII_READ in principal.permissions
        return audited(request, "READ_ACCOUNT", "ACCOUNT", accountId.toString()) {
            accounts.detail(accountId, includeEmail)
        }
    }

    @GetMapping("/outbox")
    fun outbox(
        @RequestParam(required = false) status: String?,
        @RequestParam(defaultValue = "50") limit: Int,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<List<AdminEventService.OutboxEventSummary>>> = audited(request, "READ_OUTBOX", "OUTBOX_EVENT", status) {
        events.outbox(status, limit)
    }
    @PostMapping("/outbox/{eventId}/retry")
    fun retryOutbox(@PathVariable eventId: UUID, request: HttpServletRequest): ResponseEntity<Envelope<Unit>> = audited(request, "RETRY_OUTBOX", "OUTBOX_EVENT", eventId.toString()) {
        events.retry(eventId)
        Unit
    }


    @GetMapping("/market/anomalies")
    fun anomalies(@RequestParam(defaultValue = "50") limit: Int, request: HttpServletRequest): ResponseEntity<Envelope<List<AdminAnomalyService.Alert>>> = audited(request, "READ_MARKET_ANOMALY", "MARKET_ANOMALY") { anomalies.list(limit) }

    @PostMapping("/market/quality-check")
    fun qualityCheck(request: HttpServletRequest): ResponseEntity<Envelope<AdminMarketQualityService.Report>> = audited(request, "RUN_MARKET_QUALITY_CHECK", "MARKET_DATA") { quality.inspect() }
    @GetMapping("/audit")
    fun audit(
        @RequestParam(defaultValue = "100") limit: Int,
        @RequestParam(required = false) operatorId: UUID?,
        @RequestParam(required = false) action: String?,
        @RequestParam(defaultValue = "false") mutationsOnly: Boolean,
        request: HttpServletRequest,
    ): ResponseEntity<Envelope<List<AdminAuditRepository.AuditEvent>>> = audited(request, "READ_AUDIT", "AUDIT_EVENT") {
        audit.list(limit.coerceIn(1, 200), operatorId, action, mutationsOnly)
    }

    private fun <T> audited(
        request: HttpServletRequest,
        action: String,
        targetType: String,
        targetId: String? = null,
        operation: () -> T,
    ): ResponseEntity<Envelope<T>> {
        val principal = principal(request)
        val requestId = requestId(request)
        return try {
            val result = operation()
            audit.record(clock.instant(), principal.operatorId, principal.username, action, targetType, targetId, "SUCCEEDED", requestId, remoteAddress(request))
            success(requestId, result)
        } catch (exception: RuntimeException) {
            audit.record(clock.instant(), principal.operatorId, principal.username, action, targetType, targetId, "FAILED", requestId, remoteAddress(request))
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
