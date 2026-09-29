package com.hanjjak.events.api

import com.hanjjak.events.application.EconomyDailyMetric
import com.hanjjak.events.application.EconomyMetricRefreshRequest
import com.hanjjak.events.application.EconomyMetricsService
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.time.LocalDate
import java.util.UUID

@RestController
@RequestMapping("/api/v1/metrics/economy")
class EconomyMetricsController(
    private val metrics: EconomyMetricsService,
    private val clock: Clock,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)
    @PostMapping("/daily/refresh")
    fun refreshDaily(@RequestBody request: EconomyMetricRefreshRequest): ResponseEntity<Envelope<List<EconomyDailyMetric>>> = success(metrics.refreshDaily(request.from, request.to))

    @GetMapping("/daily")
    fun listDaily(
        @RequestParam from: LocalDate,
        @RequestParam to: LocalDate,
    ): ResponseEntity<Envelope<List<EconomyDailyMetric>>> = success(metrics.listDaily(from, to))

    private fun <T> success(data: T): ResponseEntity<Envelope<T>> = ResponseEntity.ok()
        .cacheControl(CacheControl.noStore())
        .body(Envelope(UUID.randomUUID(), clock.instant(), 0, data))
}
